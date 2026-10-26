using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PeriodicEconomicProductionDemandTurnPhaseTests
{
    private static EconomicPointRuntimeState Point(ulong workforce = 8UL) =>
        new(
            new EconomicPointId(1UL),
            new CivilizationId(1UL),
            new StrategicCellId(1UL),
            workforce,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 3UL),
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Mining, 2UL)
            });

    private static WorldState EconomicState(ulong workforce = 8UL) =>
        WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL) }))
            .WithEconomy(new EconomyRuntimeState(new[] { Point(workforce) }));

    private static EconomicProductionDemandTurnPhase Phase() =>
        new(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(2U),
                    EconomicActivityKind.Mining, 250_000L),
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(1U),
                    EconomicActivityKind.Agriculture, 500_000L)
            },
            new[]
            {
                new EconomicDemandProfile(
                    new EconomicPointId(1UL), new CommodityId(2U),
                    500_000L),
                new EconomicDemandProfile(
                    new EconomicPointId(1UL), new CommodityId(1U),
                    125_000L)
            });

    private static TurnResolutionInput Input(WorldState state, ulong turn) =>
        new(
            state,
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(77UL)));

    [Fact]
    public void EmptyCommandTurnResolvesCanonicalFlowsWithoutChangingWorldState()
    {
        var initial = EconomicState();
        var before = new CanonicalWorldStateHasher().Compute(initial);

        var result = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
        {
            Phase()
        }).Resolve(Input(initial, 1UL));

        Assert.Same(initial, result.ResultingWorldState);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(initial));
        Assert.Single(result.Events);
        Assert.Single(result.EventLog.Entries);
        Assert.True(result.EventLog.Entries[0].WasExecuted);

        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(result.Events[0]);
        Assert.Equal(EventOriginKind.System, evt.Id.OriginKind);
        Assert.Equal(EconomicProductionDemandTurnPhase.StablePhaseId, evt.Id.SystemPhaseId);
        Assert.Equal(new TurnNumber(1UL), evt.Id.Turn);
        Assert.Equal(1UL, evt.Id.SystemOrdinal);
        Assert.Throws<InvalidOperationException>(() => { _ = evt.Id.OriginCommandId; });

        Assert.Equal(
            new[] { (1U, EconomicActivityKind.Agriculture, 1_500_000UL),
                    (2U, EconomicActivityKind.Mining, 500_000UL) },
            evt.Flows.SupplyFlows.Select(f => (f.CommodityId.Value, f.Activity, f.RawQuantity)));
        Assert.Equal(
            new[] { (1U, 1_000_000UL), (2U, 4_000_000UL) },
            evt.Flows.DemandFlows.Select(f => (f.CommodityId.Value, f.RawQuantity)));
    }

    [Fact]
    public void DistinctTurnsProduceFreshAuditEventsWithRepeatableFlowValues()
    {
        var initial = EconomicState();
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { Phase() });
        var first = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            engine.Resolve(Input(initial, 1UL)).Events.Single());
        var second = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            engine.Resolve(Input(initial, 2UL)).Events.Single());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(
            first.Flows.SupplyFlows.Select(f => (f.EconomicPointId, f.CommodityId, f.Activity, f.RawQuantity)),
            second.Flows.SupplyFlows.Select(f => (f.EconomicPointId, f.CommodityId, f.Activity, f.RawQuantity)));
        Assert.Equal(
            first.Flows.DemandFlows.Select(f => (f.EconomicPointId, f.CommodityId, f.RawQuantity)),
            second.Flows.DemandFlows.Select(f => (f.EconomicPointId, f.CommodityId, f.RawQuantity)));
    }

    [Fact]
    public void PhaseUsesStagedEconomyRatherThanTurnStartEconomy()
    {
        var planning = EconomicState(workforce: 8UL);
        var staged = EconomicState(workforce: 12UL);

        var result = Phase().Resolve(Input(planning, 1UL), staged);

        Assert.Same(staged, result.ResultingWorldState);
        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(result.Events.Single());
        Assert.Equal(1_500_000UL, evt.Flows.DemandFlows[0].RawQuantity);
    }

    [Fact]
    public void SourceInputsAreSnapshotsNotMutableCallerCollections()
    {
        var potentials = new List<EconomicProductionPotential>
        {
            new(new EconomicPointId(1UL), new CommodityId(1U),
                EconomicActivityKind.Agriculture, 500_000L)
        };
        var profiles = new List<EconomicDemandProfile>
        {
            new(new EconomicPointId(1UL), new CommodityId(1U), 125_000L)
        };
        var phase = new EconomicProductionDemandTurnPhase(potentials, profiles);
        potentials.Clear();
        profiles.Clear();

        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            phase.Resolve(Input(EconomicState(), 1UL), EconomicState()).Events.Single());
        Assert.Single(evt.Flows.SupplyFlows);
        Assert.Single(evt.Flows.DemandFlows);
    }

    [Fact]
    public void EmptyEconomyStillEmitsOneSystemAuditEvent()
    {
        var state = WorldState.CreateInitial();
        var phase = new EconomicProductionDemandTurnPhase(
            Array.Empty<EconomicProductionPotential>(),
            Array.Empty<EconomicDemandProfile>());

        var result = phase.Resolve(Input(state, 1UL), state);

        Assert.Same(state, result.ResultingWorldState);
        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(result.Events.Single());
        Assert.Empty(evt.Flows.SupplyFlows);
        Assert.Empty(evt.Flows.DemandFlows);
    }

    [Fact]
    public void AuthoritativeGateClosesMultipleCommandlessEconomicTurns()
    {
        var session = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        session.AcceptSnapshot(
            session.Snapshot
                .WithCivilizations(new CivilizationRuntimeState(
                    new[] { new CivilizationId(1UL) }))
                .WithEconomy(new EconomyRuntimeState(new[] { Point() })));

        var initial = session.Snapshot;
        var hash = new CanonicalWorldStateHasher().Compute(initial);
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { Phase() });
        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);

        for (ulong turn = 1UL; turn <= 3UL; turn++)
        {
            var resolved = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(new TurnNumber(turn), true, false),
                Input(session.Snapshot, turn)));

            Assert.True(resolved.WasResolved);
            Assert.IsType<EconomicProductionDemandResolvedEvent>(
                resolved.ResolutionResult!.Events.Single());
            Assert.Same(initial, session.Snapshot);
            Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(session.Snapshot));
            Assert.Equal(new TurnNumber(turn + 1UL), gate.NextTurn);
        }
    }

    [Fact]
    public void InvalidEconomicInputsFailWithoutCommittingTheTurn()
    {
        var session = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        var original = session.Snapshot;
        var badPhase = new EconomicProductionDemandTurnPhase(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(999UL), new CommodityId(1U),
                    EconomicActivityKind.Agriculture, 500_000L)
            },
            Array.Empty<EconomicDemandProfile>());
        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { badPhase })),
            new TurnNumber(1UL), 1U);

        Assert.Throws<InvalidOperationException>(() =>
            gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(new TurnNumber(1UL), true, false),
                Input(original, 1UL))));

        Assert.Same(original, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), gate.NextTurn);
        Assert.Null(gate.LastCommittedTurn);
    }
}
