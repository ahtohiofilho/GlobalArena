using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-J3b: replayable, externally authorized movement command WITNESSES.
/// Does not publish a military change to the durable GAFULL09 head.
/// </summary>
public sealed class G3J3bAuthorizedMilitaryMoveAuditTests
{
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly MilitaryUnitId Unit = new(9UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);

    private sealed class Authority : IMilitaryMoveOrderAuthority
    {
        public bool Approved { get; set; } = true;
        public bool IsAuthorized(
            MilitaryMoveCommand command, WorldState acceptedWorld,
            SimulationContext context) =>
            Approved && command.IssuingCivilizationId == Owner &&
            command.MilitaryUnitId == Unit && command.Id.Turn == context.Turn;
    }

    private sealed record OtherCommand(CommandId Id) : ISimulationCommand;

    private sealed class Folder : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), "ga-g3j3b-" + Guid.NewGuid().ToString("N"));
        public Folder() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static EconomicEcologicalCheckpoint Accepted(WorldGenerationResult generated)
    {
        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithWarfare(new WarfareRuntimeState(new[]
            {
                new MilitaryUnitRuntimeState(
                    Unit, Owner, new StrategicCellId(1UL))
            }))
            .WithTransportFacilities(new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(42UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    true, new[]
                    {
                        new TransportFacilityTransferCapability(1UL, 2UL, 1UL)
                    })
            }));

        var policy = new EconomicNaturalCapitalPolicy(
            10UL * EconomicTransferPolicy.FixedPointDenominator,
            10UL * EconomicTransferPolicy.FixedPointDenominator,
            0L, 0L, 0L);
        return new EconomicEcologicalCheckpoint(generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, policy),
            EconomicMaterialReserveBook.Empty, policy,
            new EconomicPrimitiveCommodityMapping(Food, Wood, Stone));
    }

    private static MilitaryMoveCommand Move(
        WorldGenerationResult generated, ulong turn = 1UL,
        ulong sequence = 1UL, int neighbor = 0)
    {
        var start = generated.StrategicSurfaceGraph.GetNodeIndex(
            new StrategicCellId(1UL));
        var adjacent = generated.StrategicSurfaceGraph.GetNeighborIndexes(start);
        Assert.True(adjacent.Count >= 2);
        return new MilitaryMoveCommand(
            new CommandId(new TurnNumber(turn), sequence), Owner, Unit,
            generated.StrategicSurfaceGraph.GetCellId(adjacent[neighbor]));
    }

    private static SimulationContext Context(ulong turn = 1UL) =>
        new(new TurnNumber(turn), new SimulationSeed(73UL));

    [Fact]
    public void ApprovedCommandExecutesAndWitnessReplaysWithoutPublishingSave()
    {
        var generated = Generate();
        var initial = Accepted(generated);
        var authority = new Authority();
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, authority);
        var originalHash = new CanonicalWorldStateHasher().Compute(initial.World);
        var command = Move(generated);

        var projection = audit.Project(initial, initial.Sha256Hex,
            Context(), new ISimulationCommand[] { command });

        Assert.Equal(9U, originalHash.FormatVersion);
        Assert.Single(projection.Resolution.Events);
        var evt = Assert.IsType<MilitaryMoveEvent>(
            Assert.Single(projection.Resolution.Events));
        Assert.Equal(EventOriginKind.Command, evt.Id.OriginKind);
        Assert.Equal(command.Id, evt.Id.OriginCommandId);
        Assert.True(Assert.Single(projection.Resolution.EventLog.Entries).WasExecuted);
        Assert.Equal(command.DestinationStrategicCellId,
            Assert.Single(projection.Resolution.ResultingWorldState.Warfare.Units)
                .StrategicCellId);
        Assert.Equal(new StrategicCellId(1UL),
            Assert.Single(initial.World.Warfare.Units).StrategicCellId);
        Assert.Equal(originalHash, new CanonicalWorldStateHasher().Compute(initial.World));
        Assert.NotEqual(originalHash,
            new CanonicalWorldStateHasher().Compute(
                projection.Resolution.ResultingWorldState));
        Assert.Equal(0UL, initial.NaturalCapital.LastAppliedTurn);
        Assert.Empty(initial.MaterialReserves.Balances);

        var loadedSave = EconomicCompleteV9SaveBundleCodec.Decode(
            generated, EconomicCompleteV9SaveBundleCodec.Encode(initial));
        var replay = audit.VerifyReplay(loadedSave, initial.Sha256Hex,
            projection.Witness);
        Assert.Equal(projection.WitnessSha256Hex, replay.WitnessSha256Hex);
        Assert.Equal(projection.Witness, replay.Witness);
    }

    [Fact]
    public void CommandOrderingIsCanonicalAndConflictingMovesLogRejection()
    {
        var generated = Generate();
        var initial = Accepted(generated);
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, new Authority());
        var a = Move(generated, sequence: 1UL, neighbor: 0);
        var b = Move(generated, sequence: 2UL, neighbor: 1);

        var forward = audit.Project(initial, initial.Sha256Hex,
            Context(), new ISimulationCommand[] { a, b });
        var reverse = audit.Project(initial, initial.Sha256Hex,
            Context(), new ISimulationCommand[] { b, a });

        Assert.Equal(forward.Witness, reverse.Witness);
        Assert.Equal(2, forward.Resolution.EventLog.Entries.Count);
        Assert.Single(forward.Resolution.EventLog.Entries.Where(x => x.WasExecuted));
        Assert.Single(forward.Resolution.EventLog.Entries.Where(x => !x.WasExecuted));
        Assert.Equal(forward.Witness, audit.VerifyReplay(
            initial, initial.Sha256Hex, reverse.Witness).Witness);
    }

    [Fact]
    public void DeniedOrUnsupportedCommandAndInvalidMovementFailClosed()
    {
        var generated = Generate();
        var initial = Accepted(generated);
        var authority = new Authority { Approved = false };
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, authority);
        var before = EconomicCompleteV9SaveBundleCodec.Encode(initial);

        Assert.Throws<UnauthorizedAccessException>(() =>
            audit.Project(initial, initial.Sha256Hex, Context(),
                new ISimulationCommand[] { Move(generated) }));
        authority.Approved = true;
        Assert.Throws<NotSupportedException>(() =>
            audit.Project(initial, initial.Sha256Hex, Context(),
                new ISimulationCommand[]
                {
                    new OtherCommand(new CommandId(new TurnNumber(1UL), 1UL))
                }));
        Assert.Throws<InvalidOperationException>(() =>
            audit.Project(initial, initial.Sha256Hex, Context(),
                new ISimulationCommand[] { Move(generated, turn: 2UL) }));
        Assert.Throws<InvalidOperationException>(() =>
            audit.Project(initial, initial.Sha256Hex, Context(),
                new ISimulationCommand[] { Move(generated), Move(generated) }));
        Assert.Throws<ArgumentException>(() =>
            audit.Project(initial, initial.Sha256Hex, Context(),
                Array.Empty<ISimulationCommand>()));
        Assert.Equal(before, EconomicCompleteV9SaveBundleCodec.Encode(initial));
    }

    [Fact]
    public void RevokedAuthorityBlocksEvenAnEarlierValidWitness()
    {
        var generated = Generate();
        var initial = Accepted(generated);
        var authority = new Authority();
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, authority);
        var witness = audit.Project(initial, initial.Sha256Hex,
            Context(), new ISimulationCommand[] { Move(generated) }).Witness;

        authority.Approved = false;
        Assert.Throws<UnauthorizedAccessException>(() =>
            audit.VerifyReplay(initial, initial.Sha256Hex, witness));
    }

    [Fact]
    public void ChangedFieldsOrRecomputedOuterChecksumDoNotPassReplay()
    {
        var generated = Generate();
        var initial = Accepted(generated);
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, new Authority());
        var original = audit.Project(initial, initial.Sha256Hex,
            Context(), new ISimulationCommand[] { Move(generated) }).Witness;

        var eventTampered = (byte[])original.Clone();
        // Last 72-byte event starts immediately before the 32-byte footer.
        var flags = eventTampered.Length - 32 - 4;
        BinaryPrimitives.WriteUInt32BigEndian(
            eventTampered.AsSpan(flags, 4), 0U);
        Rechecksum(eventTampered);
        Assert.Throws<InvalidDataException>(() =>
            audit.VerifyReplay(initial, initial.Sha256Hex, eventTampered));

        var seedTampered = (byte[])original.Clone();
        seedTampered[8 + 4 + 8 + 7] ^= 0x01;
        Rechecksum(seedTampered);
        Assert.Throws<InvalidDataException>(() =>
            audit.VerifyReplay(initial, initial.Sha256Hex, seedTampered));

        Assert.Throws<InvalidDataException>(() =>
            audit.VerifyReplay(initial, initial.Sha256Hex, original[..^1]));
        Assert.Throws<InvalidOperationException>(() =>
            audit.VerifyReplay(initial, new string('F', 64), original));
        Assert.Throws<InvalidOperationException>(() =>
            audit.VerifyReplay(Accepted(Generate(1UL)),
                initial.Sha256Hex, original));
    }

    [Fact]
    public void ImmutableArchiveChecksTrustedWitnessAndRejectsDamage()
    {
        using var folder = new Folder();
        var generated = Generate();
        var initial = Accepted(generated);
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, new Authority());
        var projection = audit.Project(initial, initial.Sha256Hex,
            Context(), new ISimulationCommand[] { Move(generated) });
        var archive = new AuthorizedMilitaryMoveAuditArchive(folder.Root);

        archive.Store(projection);
        archive.Store(projection);
        Assert.Equal(projection.Witness, archive.LoadVerified(
            audit, initial, initial.Sha256Hex,
            projection.WitnessSha256Hex).Witness);
        Assert.Throws<ArgumentException>(() =>
            archive.WitnessPath("../../not-allowed"));
        var path = archive.WitnessPath(projection.WitnessSha256Hex);
        var damaged = File.ReadAllBytes(path);
        damaged[damaged.Length / 2] ^= 0x01;
        File.WriteAllBytes(path, damaged);
        Assert.Throws<InvalidDataException>(() => archive.Store(projection));
        Assert.Throws<InvalidDataException>(() =>
            archive.LoadVerified(audit, initial, initial.Sha256Hex,
                projection.WitnessSha256Hex));
    }

    [Fact]
    public void WitnessChangesDoNotModifyV9DurableHeadOrEcologicalJournal()
    {
        using var folder = new Folder();
        var generated = Generate();
        var initial = Accepted(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            folder.Root, generated, initial);
        var head = Path.Combine(folder.Root, "full-v9-head.gahead");
        var beforeHead = File.ReadAllBytes(head);
        var audit = new AuthorizedMilitaryMoveTurnAudit(generated, new Authority());
        var projection = audit.Project(initial, durable.AcceptedSha256Hex,
            Context(), new ISimulationCommand[] { Move(generated) });

        new AuthorizedMilitaryMoveAuditArchive(folder.Root).Store(projection);
        Assert.Equal(beforeHead, File.ReadAllBytes(head));
        Assert.Equal(initial.Sha256Hex, durable.AcceptedSha256Hex);
        Assert.Equal(0UL, durable.LastAppliedTurn);
        Assert.Equal(0UL, durable.CommitSequence);
        Assert.Empty(Directory.GetFiles(folder.Root, "*.gajournal"));
    }

    private static void Rechecksum(byte[] bytes) =>
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32))
            .CopyTo(bytes, bytes.Length - 32);
}
