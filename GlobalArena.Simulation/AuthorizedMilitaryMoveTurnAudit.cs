using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Host-owned authorization decision. Command issuer IDs and game-state
/// ownership checks are NOT authentication of a player or AI principal.
/// Implementations must verify actual credentials outside this simulator.
/// </summary>
public interface IMilitaryMoveOrderAuthority
{
    bool IsAuthorized(
        MilitaryMoveCommand command, WorldState acceptedWorld,
        SimulationContext context);
}

/// <summary>
/// Read-only command preflight and reproducible audit for the first supported
/// player command family, MilitaryMoveCommand. This is NOT a save commit:
/// no CanonicalGameSession or GAFULL09 head is ever published here.
/// </summary>
public sealed class AuthorizedMilitaryMoveTurnAudit
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GAMVJ001");

    private readonly WorldGenerationResult _generatedWorld;
    private readonly IMilitaryMoveOrderAuthority _authority;
    private readonly MilitaryMoveCommandValidator _validator;
    private readonly TurnResolver _resolver;

    public AuthorizedMilitaryMoveTurnAudit(
        WorldGenerationResult generatedWorld,
        IMilitaryMoveOrderAuthority orderAuthority)
    {
        _generatedWorld = generatedWorld ??
            throw new ArgumentNullException(nameof(generatedWorld));
        _authority = orderAuthority ??
            throw new ArgumentNullException(nameof(orderAuthority));
        _validator = new MilitaryMoveCommandValidator(generatedWorld);
        _resolver = new TurnResolver(
            _validator, new MilitaryMoveCommandProcessor(generatedWorld),
            new SeededSimulationEventOrderer(),
            new MilitaryMoveEventRevalidator(generatedWorld),
            new MilitaryMoveEventExecutor());
    }

    /// <summary>
    /// The caller MUST provide the accepted full-v9 checkpoint from a
    /// trusted head and its independently accepted current digest.
    /// Any command rejected by either external authorization or domain
    /// validation aborts the WHOLE proposed batch; no silent skipping.
    /// </summary>
    public AuthorizedMilitaryMoveAuditProjection Project(
        EconomicEcologicalCheckpoint accepted,
        string independentlyAcceptedCheckpointSha256Hex,
        SimulationContext context, IEnumerable<ISimulationCommand> submitted)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(submitted);
        accepted.RequireExpectedDigest(independentlyAcceptedCheckpointSha256Hex);
        if (accepted.World.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(_generatedWorld))
            throw new InvalidOperationException("Command audit belongs to another generated world.");
        if (accepted.WorldHash.FormatVersion != 9U)
            throw new NotSupportedException("Command audit requires a real WorldState v9.");

        var commands = submitted.ToArray();
        if (commands.Length == 0)
            throw new ArgumentException("Command audit requires a nonempty closed batch.",
                nameof(submitted));
        if (commands.Any(x => x is not MilitaryMoveCommand))
            throw new NotSupportedException(
                "Only military movement commands have a canonical command audit schema.");

        var moves = commands.Cast<MilitaryMoveCommand>()
            .OrderBy(x => x.Id.Sequence).ToArray();
        for (var i = 0; i < moves.Length; i++)
        {
            var command = moves[i];
            if (command.Id.Turn != context.Turn ||
                i > 0 && moves[i - 1].Id.Sequence == command.Id.Sequence)
                throw new InvalidOperationException(
                    "Duplicate or wrong-turn military command identity.");
            if (!_authority.IsAuthorized(command, accepted.World, context))
                throw new UnauthorizedAccessException(
                    "Host did not authorize a military movement submission.");
            if (!_validator.IsValid(accepted.World, command, context))
                throw new InvalidOperationException(
                    "Military movement fails domain validation on accepted world.");
        }

        // TurnResolver uses seeded deterministic event order and revalidates
        // each event as other events change the staged world. Rejected
        // later moves are explicitly logged, not fabricated as executed.
        var resolved = _resolver.Resolve(new TurnResolutionInput(
            accepted.World, moves, context));
        if (resolved.Events.Count != resolved.EventLog.Entries.Count ||
            resolved.Events.Count != moves.Length)
            throw new InvalidOperationException(
                "Movement audit requires one traceable event per authorized command.");
        var knownIds = moves.Select(x => x.Id).ToHashSet();
        foreach (var entry in resolved.EventLog.Entries)
        {
            if (entry.Event is not MilitaryMoveEvent move ||
                move.Id.OriginKind != EventOriginKind.Command ||
                move.Id.Turn != context.Turn ||
                !knownIds.Contains(move.Id.OriginCommandId) ||
                move.Id.Sequence != 1UL)
                throw new InvalidOperationException(
                    "Movement event log has unsupported or untrusted provenance.");
        }

        var bytes = Encode(
            context, accepted.Sha256Hex, accepted.WorldHash,
            new CanonicalWorldStateHasher().Compute(resolved.ResultingWorldState),
            moves, resolved.EventLog);
        return new AuthorizedMilitaryMoveAuditProjection(
            accepted, resolved, bytes);
    }

    /// <summary>
    /// Decode a binary witness, reauthorize every recorded command against
    /// the same accepted world and re-execute the kernel. Any altered event,
    /// eligibility flag, seed, command or result hash is refused.
    /// </summary>
    public AuthorizedMilitaryMoveAuditProjection VerifyReplay(
        EconomicEcologicalCheckpoint accepted,
        string independentlyAcceptedCheckpointSha256Hex,
        SimulationContext independentlyTrustedContext, byte[] witness)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(witness);
        accepted.RequireExpectedDigest(independentlyAcceptedCheckpointSha256Hex);
        if (witness.Length < 8 + 4 + 8 + 8 + 32 + 4 + 32 + 4 + 32 + 4 + 4 + 32)
            throw new InvalidDataException("Command audit witness truncated.");
        var bodyLength = witness.Length - 32;
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(witness.AsSpan(0, bodyLength)),
            witness.AsSpan(bodyLength, 32)))
            throw new InvalidDataException("Command audit witness checksum mismatch.");

        var r = new Reader(witness, bodyLength);
        if (!r.Bytes(8).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown command audit magic.");
        if (r.U32() != FormatVersion)
            throw new NotSupportedException("Unsupported command audit format.");
        var turn = r.U64();
        var seed = r.U64();
        if (turn != independentlyTrustedContext.Turn.Value ||
            seed != independentlyTrustedContext.Seed.Value)
            throw new InvalidDataException(
                "Command audit turn or seed differs from independently trusted execution context.");
        var checkpointDigest = Convert.ToHexString(r.Bytes(32));
        var beforeVersion = r.U32();
        var beforeDigest = Convert.ToHexString(r.Bytes(32));
        var afterVersion = r.U32();
        var afterDigest = Convert.ToHexString(r.Bytes(32));
        if (checkpointDigest != accepted.Sha256Hex ||
            beforeVersion != 9U || beforeDigest != accepted.WorldHash.HexDigest ||
            afterVersion != 9U)
            throw new InvalidOperationException(
                "Command audit witness refers to a different accepted v9 save.");

        var commandCount = r.Count(32);
        if (commandCount == 0)
            throw new InvalidDataException("Command audit cannot have empty command batch.");
        var commands = new MilitaryMoveCommand[commandCount];
        for (var i = 0; i < commands.Length; i++)
            commands[i] = new MilitaryMoveCommand(
                new CommandId(new TurnNumber(turn), r.U64()),
                new CivilizationId(r.U64()), new MilitaryUnitId(r.U64()),
                new StrategicCellId(r.U64()));

        var eventCount = r.Count(72);
        for (var i = 0; i < eventCount; i++)
            r.Bytes(72);
        r.RequireEnd();

        var replayed = Project(accepted, independentlyAcceptedCheckpointSha256Hex,
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(seed)),
            commands);
        if (new CanonicalWorldStateHasher().Compute(
            replayed.Resolution.ResultingWorldState).HexDigest != afterDigest ||
            !CryptographicOperations.FixedTimeEquals(replayed.Witness, witness))
            throw new InvalidDataException(
                "Authorized command execution diverges from canonical event transcript.");

        return replayed;
    }

    private static byte[] Encode(
        SimulationContext context, string checkpointSha256Hex,
        WorldStateHash beforeHash, WorldStateHash afterHash,
        IReadOnlyList<MilitaryMoveCommand> commands, SimulationEventLog log)
    {
        using var stream = new MemoryStream();
        stream.Write(Magic);
        WriteU32(stream, FormatVersion);
        WriteU64(stream, context.Turn.Value);
        WriteU64(stream, context.Seed.Value);
        stream.Write(Convert.FromHexString(checkpointSha256Hex));
        WriteU32(stream, beforeHash.FormatVersion);
        stream.Write(Convert.FromHexString(beforeHash.HexDigest));
        WriteU32(stream, afterHash.FormatVersion);
        stream.Write(Convert.FromHexString(afterHash.HexDigest));
        WriteU32(stream, checked((uint)commands.Count));
        foreach (var command in commands)
        {
            WriteU64(stream, command.Id.Sequence);
            WriteU64(stream, command.IssuingCivilizationId.Value);
            WriteU64(stream, command.MilitaryUnitId.Value);
            WriteU64(stream, command.DestinationStrategicCellId.Value);
        }

        WriteU32(stream, checked((uint)log.Entries.Count));
        foreach (var entry in log.Entries)
        {
            var evt = (MilitaryMoveEvent)entry.Event;
            WriteU64(stream, entry.ResolutionSequence);
            WriteU64(stream, evt.Id.OriginCommandId.Sequence);
            WriteU64(stream, evt.Id.Sequence);
            WriteU64(stream, evt.IssuingCivilizationId.Value);
            WriteU64(stream, evt.MilitaryUnitId.Value);
            WriteU64(stream, evt.ExpectedSourceStrategicCellId.Value);
            WriteU64(stream, evt.DestinationStrategicCellId.Value);
            WriteU64(stream, evt.TraversedStrategicEdgeId.Value);
            WriteU32(stream, entry.WasEligible ? 1U : 0U);
            WriteU32(stream, entry.WasExecuted ? 1U : 0U);
        }

        stream.Write(SHA256.HashData(stream.ToArray()));
        return stream.ToArray();
    }

    private static void WriteU32(Stream target, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        target.Write(bytes);
    }

    private static void WriteU64(Stream target, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        target.Write(bytes);
    }

    private sealed class Reader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _offset;

        internal Reader(byte[] data, int end)
        {
            _data = data;
            _end = end;
        }

        internal ReadOnlySpan<byte> Bytes(int count)
        {
            if (count < 0 || count > _end - _offset)
                throw new InvalidDataException("Truncated command audit row.");
            var result = _data.AsSpan(_offset, count);
            _offset += count;
            return result;
        }

        internal uint U32() => BinaryPrimitives.ReadUInt32BigEndian(Bytes(4));
        internal ulong U64() => BinaryPrimitives.ReadUInt64BigEndian(Bytes(8));

        internal int Count(int rowLength)
        {
            var count = U32();
            if (count > (uint)((_end - _offset) / rowLength))
                throw new InvalidDataException("Command audit row count exceeds payload.");
            return checked((int)count);
        }

        internal void RequireEnd()
        {
            if (_offset != _end)
                throw new InvalidDataException("Command audit has trailing bytes.");
        }
    }
}

public sealed class AuthorizedMilitaryMoveAuditProjection
{
    public EconomicEcologicalCheckpoint AcceptedCheckpoint { get; }
    public TurnResolutionResult Resolution { get; }
    private readonly byte[] _witness;
    public byte[] Witness => (byte[])_witness.Clone();
    public string WitnessSha256Hex { get; }

    internal AuthorizedMilitaryMoveAuditProjection(
        EconomicEcologicalCheckpoint accepted,
        TurnResolutionResult resolution, byte[] witness)
    {
        AcceptedCheckpoint = accepted;
        Resolution = resolution;
        _witness = (byte[])witness.Clone();
        WitnessSha256Hex = Convert.ToHexString(SHA256.HashData(_witness));
    }
}
