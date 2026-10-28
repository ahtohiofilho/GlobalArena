using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-J3: a policy-closed, commandless ecological turn is preflighted
/// through the existing phased engine, then accepted ONLY by the single
/// durable v9 head after the proposal commitment is rechecked.
/// </summary>
public sealed class G3J3ClosedEcologicalDurableTurnGateTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId Site = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);

    private sealed class Folder : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "ga-g3j3-" + Guid.NewGuid().ToString("N"));
        public Folder() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class ChangingPolicy : ITurnPolicy
    {
        private int _calls;
        public bool ShouldClose(TurnPolicyInput input) =>
            ++_calls == 1;
    }

    private static WorldGenerationResult Generate() =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261028UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static EconomicEcologicalCheckpoint Genesis(WorldGenerationResult generated)
    {
        var environment = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            environment.WoodyVegetation.GetRawValue(id) > 0L &&
            environment.ExposedStone.GetRawValue(id) > 0L);
        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(Site, Owner, cell, 4UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 4UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(Site, 4UL)
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
            100UL * D, 100UL * D, 0L, 0L, 0L);
        return new EconomicEcologicalCheckpoint(
            generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, policy),
            EconomicMaterialReserveBook.Empty, policy, Mapping);
    }

    private static EconomicDemandProfile[] Demand() =>
        new[] { new EconomicDemandProfile(Site, Food, 500_000L) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[] { new EconomicMaterialOriginHoldOrder(
            Site, Wood, EconomicActivityKind.Forestry, D / 100UL) };

    private static TurnPolicyInput PolicyInput(ulong ordinal, bool ready) =>
        new(new TurnNumber(ordinal), ready, false);

    private static EconomicClosedEcologicalTurnResult Run(
        EconomicClosedEcologicalDurableTurnGate gate, string accepted,
        ulong turn, bool ready = true) =>
        gate.ResolveAndCommit(PolicyInput(turn, ready),
            new SimulationSeed(7331UL), accepted, Demand(), Holds());

    [Fact]
    public void ClosedTurnPreflightsPhaseAndDurablyCommitsExactlyOnce()
    {
        using var dir = new Folder();
        var generated = Generate();
        var original = Genesis(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, original);
        var gate = new EconomicClosedEcologicalDurableTurnGate(
            generated, durable, new ManualReadyTurnPolicy());

        var outcome = Run(gate, original.Sha256Hex, 1UL);

        Assert.True(outcome.WasCommitted);
        var audit = Assert.IsType<EconomicEcologicalTurnProjectedEvent>(
            outcome.AuditEvent);
        Assert.Equal(new TurnNumber(1UL), audit.Id.Turn);
        Assert.Equal(EventOriginKind.System, audit.Id.OriginKind);
        Assert.Equal(EconomicProductionDemandTurnPhase.StablePhaseId,
            audit.Id.SystemPhaseId);
        Assert.Equal(original.Sha256Hex, audit.PreviousCheckpointSha256Hex);
        Assert.Equal(outcome.AcceptedCheckpointSha256Hex,
            audit.ProposedCheckpointSha256Hex);
        Assert.Equal(outcome.AcceptedCheckpointSha256Hex, durable.AcceptedSha256Hex);
        Assert.Equal(1UL, durable.LastAppliedTurn);
        Assert.Equal(1UL, durable.CommitSequence);

        var full = EconomicCompleteV9SaveBundleCodec.Decode(
            generated, durable.CaptureCompleteSave());
        Assert.Equal(9U, full.WorldHash.FormatVersion);
        Assert.Equal(D / 100UL, full.MaterialReserves.RawAvailable(Site, Wood));
        var facility = Assert.Single(full.World.TransportFacilities.Facilities);
        Assert.Equal(42UL, facility.FacilityId);
        Assert.True(facility.CanTransfer(1UL, 2UL, 1UL));
        Assert.Equal(0UL, original.NaturalCapital.LastAppliedTurn);
        Assert.Empty(original.MaterialReserves.Balances);

        var journal = new EconomicEcologicalTransitionJournalArchive(dir.Root)
            .Load(full.Sha256Hex);
        Assert.Equal(1UL, journal.TurnOrdinal);
        Assert.Equal(original.Sha256Hex, journal.PreviousCheckpointSha256Hex);
        Assert.Single(journal.WorkerDemand);
        Assert.Single(journal.OriginHolds);
    }

    [Fact]
    public void OpenTurnNeverProjectsOrPublishesAnyCheckpoint()
    {
        using var dir = new Folder();
        var generated = Generate();
        var initial = Genesis(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        var gate = new EconomicClosedEcologicalDurableTurnGate(
            generated, durable, new ManualReadyTurnPolicy());
        var before = durable.CaptureCompleteSave();

        var result = Run(gate, initial.Sha256Hex, 1UL, ready: false);

        Assert.False(result.WasCommitted);
        Assert.Null(result.AuditEvent);
        Assert.Null(result.AcceptedCheckpointSha256Hex);
        Assert.Equal(initial.Sha256Hex, durable.AcceptedSha256Hex);
        Assert.Equal(0UL, durable.CommitSequence);
        Assert.Equal(before, durable.CaptureCompleteSave());
        Assert.Empty(Directory.GetFiles(dir.Root, "*.gajournal"));
        Assert.Single(Directory.GetFiles(dir.Root, "*.gafull09"));
    }

    [Fact]
    public void StaleDigestSkippedTurnAndRepeatedTurnFailWithoutPublication()
    {
        using var dir = new Folder();
        var generated = Generate();
        var initial = Genesis(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        var gate = new EconomicClosedEcologicalDurableTurnGate(
            generated, durable, new ManualReadyTurnPolicy());

        Assert.Throws<InvalidOperationException>(() =>
            Run(gate, initial.Sha256Hex, 2UL));
        Assert.Equal(0UL, durable.CommitSequence);
        var first = Run(gate, initial.Sha256Hex, 1UL);
        Assert.True(first.WasCommitted);
        Assert.Throws<InvalidOperationException>(() =>
            Run(gate, initial.Sha256Hex, 2UL));
        Assert.Throws<InvalidOperationException>(() =>
            Run(gate, durable.AcceptedSha256Hex, 1UL));
        Assert.Equal(1UL, durable.CommitSequence);
    }

    [Fact]
    public void ImpossibleHoldAndChangingClosurePolicyCannotReachDurableHead()
    {
        using var dir = new Folder();
        var generated = Generate();
        var initial = Genesis(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        var gate = new EconomicClosedEcologicalDurableTurnGate(
            generated, durable, new ManualReadyTurnPolicy());
        var headPath = Path.Combine(dir.Root, "full-v9-head.gahead");
        var before = File.ReadAllBytes(headPath);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            gate.ResolveAndCommit(PolicyInput(1UL, true),
                new SimulationSeed(7UL), initial.Sha256Hex, Demand(), new[]
                {
                    new EconomicMaterialOriginHoldOrder(
                        Site, Wood, EconomicActivityKind.Forestry, ulong.MaxValue)
                }));
        Assert.Equal(before, File.ReadAllBytes(headPath));

        var unstable = new EconomicClosedEcologicalDurableTurnGate(
            generated, durable, new ChangingPolicy());
        Assert.Throws<InvalidOperationException>(() =>
            Run(unstable, initial.Sha256Hex, 1UL));
        Assert.Equal(initial.Sha256Hex, durable.AcceptedSha256Hex);
        Assert.Equal(before, File.ReadAllBytes(headPath));
        Assert.Empty(Directory.GetFiles(dir.Root, "*.gajournal"));
    }

    [Fact]
    public void ProposedDigestMismatchIsRejectedBeforeAnyJournalOrHeadPublish()
    {
        using var dir = new Folder();
        var generated = Generate();
        var initial = Genesis(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        var headPath = Path.Combine(dir.Root, "full-v9-head.gahead");
        var before = File.ReadAllBytes(headPath);

        Assert.Throws<InvalidOperationException>(() =>
            durable.CommitProjected(initial.Sha256Hex, new string('A', 64),
                1UL, Demand(), Holds()));

        Assert.Equal(before, File.ReadAllBytes(headPath));
        Assert.Equal(initial.Sha256Hex, durable.AcceptedSha256Hex);
        Assert.Equal(0UL, durable.CommitSequence);
        Assert.Empty(Directory.GetFiles(dir.Root, "*.gajournal"));
        Assert.Single(Directory.GetFiles(dir.Root, "*.gafull09"));
    }

    [Fact]
    public void RestoredDurableHostCanContinueViaSamePolicyGatedPath()
    {
        using var dir = new Folder();
        var generated = Generate();
        var initial = Genesis(generated);
        string accepted;
        using (var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial))
        {
            var gate = new EconomicClosedEcologicalDurableTurnGate(
                generated, durable, new ManualReadyTurnPolicy());
            accepted = Run(gate, initial.Sha256Hex, 1UL)
                .AcceptedCheckpointSha256Hex!;
        }

        using (var restored = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, accepted))
        {
            var gate = new EconomicClosedEcologicalDurableTurnGate(
                generated, restored, new ManualReadyTurnPolicy());
            var next = Run(gate, accepted, 2UL);
            Assert.True(next.WasCommitted);
            Assert.Equal(2UL, restored.CommitSequence);
            Assert.Equal(2UL, restored.LastAppliedTurn);
            accepted = next.AcceptedCheckpointSha256Hex!;
        }
        using var again = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, accepted);
        Assert.Equal(2UL, again.CommitSequence);
    }

    [Fact]
    public void DifferentWorldCannotBeUsedToPreflightAcceptedDurableSave()
    {
        using var dir = new Folder();
        var generated = Generate();
        var initial = Genesis(generated);
        using var durable = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        var foreign = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(9UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));
        var gate = new EconomicClosedEcologicalDurableTurnGate(
            foreign, durable, new ManualReadyTurnPolicy());
        Assert.Throws<InvalidOperationException>(() =>
            Run(gate, initial.Sha256Hex, 1UL));
        Assert.Equal(0UL, durable.CommitSequence);
    }
}
