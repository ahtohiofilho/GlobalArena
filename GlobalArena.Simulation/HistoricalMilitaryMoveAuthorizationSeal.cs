using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Symmetric, host-issued historical authorization over the entire canonical
/// military command/event witness. A valid MAC certifies that the trusted
/// issuer approved every order when it SEALED the witness. It does not
/// identify a player by itself; host authentication is required at issuance.
/// Keep keys outside save files and preserve retired verification keys.
/// </summary>
public sealed class HistoricalMilitaryMoveAuthorizationSeal
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GAHMAC01");
    private const int MaxWitnessBytes = 16 * 1024 * 1024;

    private readonly WorldGenerationResult _generated;
    private readonly byte[] _key;
    private readonly string _keyId;

    public HistoricalMilitaryMoveAuthorizationSeal(
        WorldGenerationResult generatedWorld, string keyId,
        byte[] trustedHostKey)
    {
        _generated = generatedWorld ??
            throw new ArgumentNullException(nameof(generatedWorld));
        ArgumentNullException.ThrowIfNull(trustedHostKey);
        if (string.IsNullOrWhiteSpace(keyId) ||
            keyId.Length > 128 ||
            keyId.Any(c => c is < '!' or > '~'))
            throw new ArgumentException("Key identity must be printable ASCII (1–128).",
                nameof(keyId));
        if (trustedHostKey.Length < 32)
            throw new ArgumentException("Historical MAC requires at least a 256-bit trusted key.",
                nameof(trustedHostKey));

        _keyId = keyId;
        _key = (byte[])trustedHostKey.Clone();
    }

    /// <summary>
    /// Only the issuer with a live, externally provided authorization service
    /// can create a seal. All submitted orders must pass that service.
    /// </summary>
    public byte[] Issue(
        EconomicEcologicalCheckpoint accepted,
        string independentlyTrustedCheckpointDigest,
        SimulationContext trustedContext,
        IEnumerable<ISimulationCommand> submitted,
        IMilitaryMoveOrderAuthority liveAuthority)
    {
        ArgumentNullException.ThrowIfNull(liveAuthority);
        var audited = new AuthorizedMilitaryMoveTurnAudit(
            _generated, liveAuthority).Project(
            accepted, independentlyTrustedCheckpointDigest, trustedContext,
            submitted);
        return Sign(audited.Witness, accepted, trustedContext);
    }

    /// <summary>
    /// Historical replay does NOT ask today's authority to reapprove an old
    /// order: first verify the original host MAC, then replay strictly through
    /// the existing kernel validators using the authenticated witness.
    /// </summary>
    public AuthorizedMilitaryMoveAuditProjection Verify(
        EconomicEcologicalCheckpoint accepted,
        string independentlyTrustedCheckpointDigest,
        SimulationContext independentlyTrustedContext,
        byte[] certificate)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(certificate);
        accepted.RequireExpectedDigest(independentlyTrustedCheckpointDigest);
        if (accepted.WorldHash.FormatVersion != 9U)
            throw new NotSupportedException("Historical military seals require v9 state.");
        if (accepted.World.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(_generated))
            throw new InvalidOperationException("Historical seal belongs to another world.");

        var minimum = 8 + 4 + 4 + 1 + 8 + 8 + 32 + 32 + 4 + 1 + 32;
        if (certificate.Length < minimum ||
            certificate.Length > MaxWitnessBytes + 512)
            throw new InvalidDataException("Historical authorization record has invalid length.");
        var bodyLength = certificate.Length - 32;
        var actual = HMACSHA256.HashData(_key, certificate.AsSpan(0, bodyLength));
        if (!CryptographicOperations.FixedTimeEquals(
            actual, certificate.AsSpan(bodyLength, 32)))
            throw new InvalidDataException("Historical authorization MAC did not verify.");

        var reader = new Reader(certificate, bodyLength);
        if (!reader.Bytes(8).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown historical authorization domain.");
        if (reader.U32() != FormatVersion)
            throw new NotSupportedException("Unsupported historical authorization version.");
        var idLength = reader.U32();
        if (idLength == 0U || idLength > 128U)
            throw new InvalidDataException("Invalid historical key identity length.");
        var keyId = Encoding.ASCII.GetString(reader.Bytes((int)idLength));
        if (keyId != _keyId)
            throw new InvalidOperationException("Historical authorization key identity differs.");
        var turn = reader.U64();
        var seed = reader.U64();
        var checkpoint = Convert.ToHexString(reader.Bytes(32));
        var worldHash = Convert.ToHexString(reader.Bytes(32));
        if (turn != independentlyTrustedContext.Turn.Value ||
            seed != independentlyTrustedContext.Seed.Value ||
            checkpoint != accepted.Sha256Hex ||
            worldHash != accepted.WorldHash.HexDigest)
            throw new InvalidOperationException(
                "Historical authorization does not bind to the trusted context or v9 checkpoint.");
        var witnessLength = reader.U32();
        if (witnessLength == 0U || witnessLength > MaxWitnessBytes)
            throw new InvalidDataException("Invalid historical witness size.");
        var witness = reader.Bytes(checked((int)witnessLength)).ToArray();
        reader.RequireEnd();

        // The authenticated witness binds each serialized command and
        // event. A permit-all replay authority is safe only AFTER successful
        // MAC, trusted context, world binding and key identity verification.
        return new AuthorizedMilitaryMoveTurnAudit(
            _generated, HistoricalReplayAuthority.Instance).VerifyReplay(
            accepted, independentlyTrustedCheckpointDigest,
            independentlyTrustedContext, witness);
    }

    private byte[] Sign(byte[] witness,
        EconomicEcologicalCheckpoint accepted, SimulationContext context)
    {
        if (witness.Length == 0 || witness.Length > MaxWitnessBytes)
            throw new InvalidOperationException("Military witness exceeds certification limit.");
        using var stream = new MemoryStream();
        stream.Write(Magic);
        WriteU32(stream, FormatVersion);
        var id = Encoding.ASCII.GetBytes(_keyId);
        WriteU32(stream, checked((uint)id.Length));
        stream.Write(id);
        WriteU64(stream, context.Turn.Value);
        WriteU64(stream, context.Seed.Value);
        stream.Write(Convert.FromHexString(accepted.Sha256Hex));
        stream.Write(Convert.FromHexString(accepted.WorldHash.HexDigest));
        WriteU32(stream, checked((uint)witness.Length));
        stream.Write(witness);
        stream.Write(HMACSHA256.HashData(_key, stream.ToArray()));
        return stream.ToArray();
    }

    private static void WriteU32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteU64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private readonly int _end;
        private int _cursor;

        public Reader(byte[] bytes, int bodyLength)
        {
            _bytes = bytes;
            _end = bodyLength;
        }

        public ReadOnlySpan<byte> Bytes(int count)
        {
            if (count < 0 || count > _end - _cursor)
                throw new InvalidDataException("Historical authorization record is truncated.");
            var value = _bytes.AsSpan(_cursor, count);
            _cursor += count;
            return value;
        }

        public uint U32() => BinaryPrimitives.ReadUInt32BigEndian(Bytes(4));
        public ulong U64() => BinaryPrimitives.ReadUInt64BigEndian(Bytes(8));
        public void RequireEnd()
        {
            if (_cursor != _end)
                throw new InvalidDataException("Historical authorization record has trailing bytes.");
        }
    }

    private sealed class HistoricalReplayAuthority : IMilitaryMoveOrderAuthority
    {
        internal static readonly HistoricalReplayAuthority Instance = new();
        public bool IsAuthorized(
            MilitaryMoveCommand command, WorldState acceptedWorld,
            SimulationContext context) => true;
    }
}
