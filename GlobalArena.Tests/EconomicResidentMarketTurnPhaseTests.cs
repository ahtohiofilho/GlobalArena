using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicResidentMarketTurnPhaseTests
{
    private static WorldState State(
        ulong consumerResidents = 10UL,
        ulong producerWorkers = 2UL)
    {
        return WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(new[]
            {
                new CivilizationId(1UL),
                new CivilizationId(2UL)
            }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        producerWorkers,
                        producerWorkers == 0UL
                            ? Array.Empty<EconomicActivityWorkforceAllocation>()
                            : new[]
                            {
                                new EconomicActivityWorkforceAllocation(
                                    EconomicActivityKind.Agriculture, producerWorkers)
                            }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL),
                        new CivilizationId(2UL),
                        new StrategicCellId(2UL),
                        0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), producerWorkers),
                    new PopulationPointRuntimeState(
                        new EconomicPointId(2UL), consumerResidents)
                }));
    }

    private static EconomicProductionPotential Supply() =>
        new(new EconomicPointId(1UL), new CommodityId(1U),
            EconomicActivityKind.Agriculture, 1_000_000L);

    private static EconomicResidentDemandProfile Demand(
        long rate = 500_000L) =>
        new(new EconomicPointId(2UL), new CommodityId(1U), rate);

    private static EconomicTransferSignal Signal(
        ulong capacity = 1_000_000UL,
        ulong unitCost = 100_000UL) =>
        new(new StrategicEconomicRouteKey(
                new EconomicPointId(1UL), new EconomicPointId(2UL)),
            1, unitCost, capacity, 0UL);

    private static EconomicMarketAllocationPolicy Policy() =>
        new(900_000UL, 900_000UL, 1, 1);

    private static EconomicResidentMarketTurnPhase Phase(
        IEnumerable<EconomicTransferSignal>? signals = null) =>
        new(new[] { Supply() }, new[] { Demand() },
            signals ?? new[] { Signal() }, Policy());

    private static TurnResolutionInput Input(WorldState state, ulong turn = 1UL) =>
        new(state, Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(101UL)));

    [Fact]
    public void PeriodicResidentMarketProducesFlowsAndCapacityLimitedTradeEvents()
    {
        var state = State();
        var resolution = Phase().Resolve(Input(state), state);
        var flow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            resolution.Events[0]);
        var market = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolution.Events[1]);

        Assert.Equal(2, resolution.Events.Count);
        Assert.Equal(2, resolution.EventLog.Entries.Count);
        Assert.All(resolution.EventLog.Entries, entry => Assert.True(entry.WasExecuted));
        Assert.Equal(2_000_000UL, flow.Flows.SupplyFlows.Single().RawQuantity);
        Assert.Equal(5_000_000UL, flow.Flows.DemandFlows.Single().RawQuantity);
        Assert.Equal(1_000_000UL, market.Allocation.TotalRawAllocatedQuantity);
        Assert.Single(market.Allocation.Allocations);
        Assert.Equal(900_000UL, market.Allocation.TotalRawGrossSettlementValue);
        Assert.Equal(100_000UL, market.Allocation.TotalRawTransferSettlementCost);
        Assert.Equal(800_000UL, market.Allocation.TotalRawNetSettlementReturn);
        Assert.Equal(new CommodityId(1U),
            market.Allocation.Allocations.Single().CommodityId);
        Assert.Equal(EventOriginKind.System, market.Id.OriginKind);
        Assert.Equal(Phase().PhaseId, market.Id.SystemPhaseId);
        Assert.NotEqual(flow.Id, market.Id);
        Assert.Same(state, resolution.ResultingWorldState);
    }

    [Fact]
    public void WithoutReachableTransferSignalThereIsDemandButNoTrade()
    {
        var state = State();
        var resolution = Phase(Array.Empty<EconomicTransferSignal>())
            .Resolve(Input(state), state);
        var flow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            resolution.Events[0]);
        var market = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolution.Events[1]);

        Assert.Single(flow.Flows.DemandFlows);
        Assert.Empty(market.Allocation.Allocations);
        Assert.Equal(0UL, market.Allocation.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void UnprofitableTransferPreventsTradeWithoutDestroyingDemand()
    {
        var state = State();
        var resolution = Phase(new[] { Signal(unitCost: 1_000_000UL) })
            .Resolve(Input(state), state);
        Assert.Empty(Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolution.Events[1]).Allocation.Allocations);
        Assert.Equal(5_000_000UL,
            Assert.IsType<EconomicProductionDemandResolvedEvent>(
                resolution.Events[0]).Flows.DemandFlows.Single().RawQuantity);
    }

    [Fact]
    public void ResidentCountAffectsMarketDemandWithoutIncreasingWorkerProduction()
    {
        var small = Phase().Resolve(Input(State(consumerResidents: 2UL)),
            State(consumerResidents: 2UL));
        var large = Phase().Resolve(Input(State(consumerResidents: 10UL)),
            State(consumerResidents: 10UL));

        var first = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            small.Events[0]).Flows;
        var second = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            large.Events[0]).Flows;

        Assert.Equal(first.SupplyFlows.Single().RawQuantity,
            second.SupplyFlows.Single().RawQuantity);
        Assert.Equal(1_000_000UL, first.DemandFlows.Single().RawQuantity);
        Assert.Equal(5_000_000UL, second.DemandFlows.Single().RawQuantity);
    }

    [Fact]
    public void PopulationCensusMustBeInitializedForEntirePhase()
    {
        var state = WorldState.CreateInitial();
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicResidentMarketTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>(),
                Array.Empty<EconomicTransferSignal>(),
                Policy()).Resolve(Input(state), state));
    }

    [Fact]
    public void InvalidMarketSignalsFailAtomicallyBeforePublication()
    {
        var state = State();
        var hash = new CanonicalWorldStateHasher().Compute(state);
        var invalid = new EconomicTransferSignal(
            new StrategicEconomicRouteKey(
                new EconomicPointId(99UL), new EconomicPointId(2UL)),
            1, 100_000UL, 1_000_000UL, 0UL);

        Assert.Throws<ArgumentException>(() =>
            Phase(new[] { invalid }).Resolve(Input(state), state));
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void DuplicateRouteSignalsAreRejectedRatherThanDoubleCounted()
    {
        var state = State();
        var signal = Signal();
        Assert.Throws<ArgumentException>(() =>
            Phase(new[] { signal, signal }).Resolve(Input(state), state));
    }

    [Fact]
    public void HistoricalAndIntegratedEconomicPhasesAreMutuallyExclusive()
    {
        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                Phase(),
                new EconomicResidentProductionDemandTurnPhase(
                    new[] { Supply() },
                    new[] { Demand() })
            }));

        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                Phase(),
                new EconomicProductionDemandTurnPhase(
                    new[] { Supply() },
                    new[]
                    {
                        new EconomicDemandProfile(
                            new EconomicPointId(2UL), new CommodityId(1U),
                            500_000L)
                    })
            }));
    }

    [Fact]
    public void SequentialTurnsProduceStableFlowsAndDistinctEventIdentities()
    {
        var state = State();
        var phase = Phase();
        var hash = new CanonicalWorldStateHasher().Compute(state);
        var first = phase.Resolve(Input(state, 1UL), state);
        var second = phase.Resolve(Input(state, 2UL), state);

        Assert.NotEqual(first.Events[0].Id, second.Events[0].Id);
        Assert.NotEqual(first.Events[1].Id, second.Events[1].Id);
        Assert.Equal(
            Assert.IsType<EconomicMarketAllocationResolvedEvent>(
                first.Events[1]).Allocation.TotalRawAllocatedQuantity,
            Assert.IsType<EconomicMarketAllocationResolvedEvent>(
                second.Events[1]).Allocation.TotalRawAllocatedQuantity);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void InputsAreSnapshottedBeforeTurnResolution()
    {
        var production = new List<EconomicProductionPotential> { Supply() };
        var demand = new List<EconomicResidentDemandProfile> { Demand() };
        var signals = new List<EconomicTransferSignal> { Signal() };
        var phase = new EconomicResidentMarketTurnPhase(
            production, demand, signals, Policy());
        production.Clear();
        demand.Clear();
        signals.Clear();

        var state = State();
        var market = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            phase.Resolve(Input(state), state).Events[1]);
        Assert.Equal(1_000_000UL, market.Allocation.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void RealGeneratedWorldRoutesAndClosedTurnResolveWithoutMutation()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 2);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
            civilizations);
        var production = new EconomicPointRuntimeState(
            new EconomicPointId(1UL), civilizations.Records[0].Id,
            civilizations.Records[0].StartCellId!.Value, 2UL,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 2UL)
            });
        var consumers = new EconomicPointRuntimeState(
            new EconomicPointId(2UL), civilizations.Records[1].Id,
            civilizations.Records[1].StartCellId!.Value, 0UL);

        var state = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[] { production, consumers }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(
                        new EconomicPointId(2UL), 10UL)
                }));

        var route = new StrategicEconomicRouteResolver(
            world.StrategicSurfaceGraph).ResolveDetailed(
                state.Economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL), new EconomicPointId(2UL)),
                StrategicEdgeAccessSnapshot.AllOpen);
        Assert.True(route.IsReachable);
        var transfer = new EconomicTransferSignalResolver().Resolve(
            state.Economy, route.Route!,
            new EconomicTransferPolicy(1UL, 1_000_000UL, 0UL, 0UL));

        var phase = Phase(new[] { transfer });
        var session = new CanonicalGameSession(world);
        session.AcceptSnapshot(state);
        var initialHash = new CanonicalWorldStateHasher().Compute(session.Snapshot);

        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
                {
                    phase
                })),
            new TurnNumber(1UL), 1U);
        var turn = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            Input(session.Snapshot)));

        Assert.True(turn.WasResolved);
        Assert.Same(state, session.Snapshot);
        Assert.Equal(initialHash, new CanonicalWorldStateHasher().Compute(session.Snapshot));
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
        var resolved = turn.ResolutionResult!;
        Assert.Equal(2, resolved.Events.Count);
        Assert.Equal(2, resolved.EventLog.Entries.Count);
        Assert.Equal(1_000_000UL,
            Assert.IsType<EconomicMarketAllocationResolvedEvent>(
                resolved.Events[1]).Allocation.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void NullConstructorAndResolverInputsFailFast()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentMarketTurnPhase(
                null!, Array.Empty<EconomicResidentDemandProfile>(),
                Array.Empty<EconomicTransferSignal>(), Policy()));
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentMarketTurnPhase(
                Array.Empty<EconomicProductionPotential>(), null!,
                Array.Empty<EconomicTransferSignal>(), Policy()));
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentMarketTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>(), null!, Policy()));
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentMarketTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>(),
                Array.Empty<EconomicTransferSignal>(), null!));
    }
}
