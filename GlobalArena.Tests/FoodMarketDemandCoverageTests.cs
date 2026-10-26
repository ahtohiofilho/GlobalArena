using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoodMarketDemandCoverageTests
{
    private static FoodCommodityCatalog Catalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(8U), "roots", 2_000UL),
            new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
        });

    private static EconomicDemandFlow Demand(
        ulong point = 2UL, uint commodity = 7U, ulong quantity = 5_000_000UL) =>
        new(new EconomicPointId(point), new CommodityId(commodity), quantity);

    private static EconomicTradeAllocation Trade(
        ulong source = 1UL, ulong destination = 2UL,
        uint commodity = 7U, ulong quantity = 1_000_000UL) =>
        new(new EconomicPointId(source), EconomicActivityKind.Agriculture,
            new EconomicPointId(destination), new CommodityId(commodity),
            quantity, 900_000UL, 100_000UL);

    private static EconomicProductionDemandResult Flows(
        params EconomicDemandFlow[] demands) =>
        new(Array.Empty<EconomicSupplyFlow>(), demands);

    private static EconomicMarketAllocationResult Market(
        params EconomicTradeAllocation[] allocations) =>
        new(allocations);

    private static FoodMarketDemandCoverageResult Resolve(
        EconomicProductionDemandResult flows,
        EconomicMarketAllocationResult market,
        FoodCommodityCatalog? catalog = null) =>
        new FoodMarketDemandCoverageResolver().Resolve(
            flows, market, catalog ?? Catalog());

    private static WorldState State(ulong consumers = 10UL)
    {
        return WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL), new CivilizationId(2UL) }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL), new CivilizationId(1UL),
                        new StrategicCellId(1UL), 2UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL), new CivilizationId(2UL),
                        new StrategicCellId(2UL), 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(new EconomicPointId(2UL), consumers)
                }));
    }

    private static EconomicResidentFoodMarketTurnPhase Phase(
        WorldState state,
        IEnumerable<EconomicTransferSignal>? signals = null,
        FoodCommodityCatalog? catalog = null)
    {
        var profiles = FoodResidentDemandProfileMaterializer.Materialize(
            state, catalog ?? Catalog(),
            new[]
            {
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(2UL), new CommodityId(7U), 500UL)
            });

        return new EconomicResidentFoodMarketTurnPhase(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            profiles,
            signals ?? new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(2UL)),
                    1, 100_000UL, 1_000_000UL, 0UL)
            },
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            catalog ?? Catalog());
    }

    private static TurnResolutionInput Input(WorldState state, ulong turn = 1UL) =>
        new(state, Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(123UL)));

    [Fact]
    public void MultipleSuppliersAccumulateAgainstOneFoodDemandWithoutDoubleCounting()
    {
        var coverage = Resolve(
            Flows(Demand()),
            Market(Trade(source: 3UL, quantity: 2_000_000UL),
                Trade(source: 1UL, quantity: 1_000_000UL)));

        var row = Assert.Single(coverage.Entries);
        Assert.Equal(new EconomicPointId(2UL), row.EconomicPointId);
        Assert.Equal(new CommodityId(7U), row.CommodityId);
        Assert.Equal(5_000_000UL, row.RawDemandQuantity);
        Assert.Equal(3_000_000UL, row.RawTradeAllocatedQuantity);
        Assert.Equal(2_000_000UL, row.RawUnallocatedQuantity);
        Assert.False(row.IsFullyTradeAllocated);
        Assert.Equal(5_000_000UL, coverage.TotalRawDemandQuantity);
        Assert.Equal(3_000_000UL, coverage.TotalRawTradeAllocatedQuantity);
        Assert.Equal(2_000_000UL, coverage.TotalRawUnallocatedQuantity);
    }

    [Fact]
    public void NoMarketRouteLeavesAllRegisteredDemandUnallocated()
    {
        var coverage = Resolve(Flows(Demand()), Market());

        Assert.Equal(0UL, coverage.TotalRawTradeAllocatedQuantity);
        Assert.Equal(5_000_000UL, coverage.TotalRawUnallocatedQuantity);
        Assert.False(coverage.Entries.Single().IsFullyTradeAllocated);
    }

    [Fact]
    public void FullyAllocatedFoodDemandIsNotClaimedAsConsumed()
    {
        var coverage = Resolve(Flows(Demand(quantity: 1_000_000UL)),
            Market(Trade()));

        var row = Assert.Single(coverage.Entries);
        Assert.True(row.IsFullyTradeAllocated);
        Assert.Equal(0UL, row.RawUnallocatedQuantity);
    }

    [Fact]
    public void NonfoodTradeDoesNotAlterFoodCoverage()
    {
        var coverage = Resolve(
            Flows(Demand(), Demand(point: 3UL, commodity: 20U,
                quantity: 10_000_000UL)),
            Market(Trade(quantity: 1_000_000UL),
                Trade(destination: 3UL, commodity: 20U,
                    quantity: 10_000_000UL)));

        Assert.Single(coverage.Entries);
        Assert.Equal(4_000_000UL, coverage.TotalRawUnallocatedQuantity);
        Assert.Equal(new CommodityId(7U), coverage.Entries[0].CommodityId);
    }

    [Fact]
    public void OnlyRegisteredFoodDemandIsConsideredNotAnImplicitDiet()
    {
        var coverage = Resolve(
            Flows(Demand(commodity: 30U)), Market(),
            new FoodCommodityCatalog(Array.Empty<FoodCommodityDefinition>()));

        Assert.Empty(coverage.Entries);
        Assert.Equal(0UL, coverage.TotalRawDemandQuantity);
    }

    [Fact]
    public void FoodTradeWithoutCorrespondingDestinationDemandIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Flows(Demand()), Market(Trade(destination: 4UL))));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Flows(Demand(commodity: 8U)), Market(Trade())));
    }

    [Fact]
    public void OversubscribedDestinationDemandIsRejectedForOneOrSeveralSuppliers()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Flows(Demand(quantity: 1_000_000UL)),
                Market(Trade(quantity: 2_000_000UL))));

        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Flows(Demand(quantity: 1_000_000UL)),
                Market(Trade(quantity: 600_000UL),
                    Trade(source: 3UL, quantity: 600_000UL))));
    }

    [Fact]
    public void CoverageIsCanonicalAcrossInputOrderAndFoodIdentities()
    {
        var a = Resolve(
            Flows(Demand(point: 3UL, commodity: 8U,
                      quantity: 2_000_000UL),
                  Demand(point: 2UL, commodity: 7U,
                      quantity: 1_000_000UL)),
            Market(Trade(destination: 3UL, commodity: 8U,
                    quantity: 1_000_000UL),
                Trade(destination: 2UL, commodity: 7U,
                    quantity: 1_000_000UL)));

        var b = Resolve(
            Flows(Demand(point: 2UL, commodity: 7U,
                      quantity: 1_000_000UL),
                  Demand(point: 3UL, commodity: 8U,
                      quantity: 2_000_000UL)),
            Market(Trade(destination: 2UL, commodity: 7U,
                    quantity: 1_000_000UL),
                Trade(destination: 3UL, commodity: 8U,
                    quantity: 1_000_000UL)));

        Assert.Equal(
            new[] { (2UL, 7U), (3UL, 8U) },
            a.Entries.Select(r =>
                (r.EconomicPointId.Value, r.CommodityId.Value)));
        Assert.Equal(
            a.Entries.Select(r =>
                (r.EconomicPointId, r.CommodityId,
                    r.RawDemandQuantity, r.RawTradeAllocatedQuantity)),
            b.Entries.Select(r =>
                (r.EconomicPointId, r.CommodityId,
                    r.RawDemandQuantity, r.RawTradeAllocatedQuantity)));
    }

    [Fact]
    public void CoverageAndEntriesAreImmutableViews()
    {
        var list = new List<FoodMarketDemandCoverageEntry>
        {
            new(new EconomicPointId(1UL), new CommodityId(7U), 3UL, 2UL)
        };

        var result = new FoodMarketDemandCoverageResult(list);
        list.Clear();

        Assert.Single(result.Entries);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodMarketDemandCoverageEntry>)result.Entries)
            .Clear());
    }

    [Fact]
    public void DirectCoverageConstructionRejectsInvalidIdentitiesAndQuantities()
    {
        Assert.Throws<ArgumentException>(() =>
            new FoodMarketDemandCoverageEntry(default,
                new CommodityId(7U), 5UL, 1UL));
        Assert.Throws<ArgumentException>(() =>
            new FoodMarketDemandCoverageEntry(new EconomicPointId(1UL),
                default, 5UL, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodMarketDemandCoverageEntry(new EconomicPointId(1UL),
                new CommodityId(7U), 0UL, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoodMarketDemandCoverageEntry(new EconomicPointId(1UL),
                new CommodityId(7U), 5UL, 6UL));
    }

    [Fact]
    public void DuplicateEntriesAndUnsignedAggregateOverflowFailClosed()
    {
        var row = new FoodMarketDemandCoverageEntry(
            new EconomicPointId(1UL), new CommodityId(7U), 1UL, 0UL);
        Assert.Throws<ArgumentException>(() =>
            new FoodMarketDemandCoverageResult(new[] { row, row }));
        Assert.Throws<ArgumentException>(() =>
            new FoodMarketDemandCoverageResult(
                new FoodMarketDemandCoverageEntry[] { null! }));

        Assert.Throws<OverflowException>(() =>
            new FoodMarketDemandCoverageResult(new[]
            {
                new FoodMarketDemandCoverageEntry(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    ulong.MaxValue, ulong.MaxValue),
                new FoodMarketDemandCoverageEntry(
                    new EconomicPointId(2UL), new CommodityId(7U), 1UL, 0UL)
            }));
    }

    [Fact]
    public void NullResolverArgumentsFailFast()
    {
        var resolver = new FoodMarketDemandCoverageResolver();
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(null!, Market(), Catalog()));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(Flows(Demand()), null!, Catalog()));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(Flows(Demand()), Market(), null!));
    }

    [Fact]
    public void IntegratedTurnEmitsThreeEventsWithFoodTradeCoverage()
    {
        var state = State();
        var phase = Phase(state);
        var originalHash = new CanonicalWorldStateHasher().Compute(state);

        var result = phase.Resolve(Input(state), state);

        Assert.Equal(3, result.Events.Count);
        Assert.Equal(3, result.EventLog.Entries.Count);
        Assert.All(result.EventLog.Entries, e => Assert.True(e.WasExecuted));

        var production = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            result.Events[0]);
        var market = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            result.Events[1]);
        var coverage = Assert.IsType<EconomicFoodMarketCoverageResolvedEvent>(
            result.Events[2]);
        Assert.Equal(2_000_000UL,
            production.Flows.SupplyFlows.Single().RawQuantity);
        Assert.Equal(5_000_000UL,
            production.Flows.DemandFlows.Single().RawQuantity);
        Assert.Equal(1_000_000UL,
            market.Allocation.TotalRawAllocatedQuantity);
        Assert.Equal(4_000_000UL,
            coverage.Coverage.TotalRawUnallocatedQuantity);
        Assert.Equal(1_000_000UL,
            coverage.Coverage.TotalRawTradeAllocatedQuantity);

        Assert.Equal(phase.PhaseId, coverage.Id.SystemPhaseId);
        Assert.Equal(EventOriginKind.System, coverage.Id.OriginKind);
        Assert.NotEqual(production.Id, market.Id);
        Assert.NotEqual(market.Id, coverage.Id);
        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(originalHash, new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void IntegratedPhaseHasNoTradeCoverageWhenRouteIsAbsent()
    {
        var state = State();
        var result = Phase(state,
            Array.Empty<EconomicTransferSignal>()).Resolve(
                Input(state), state);

        var coverage = Assert.IsType<EconomicFoodMarketCoverageResolvedEvent>(
            result.Events[2]).Coverage;
        Assert.Equal(5_000_000UL, coverage.TotalRawUnallocatedQuantity);
        Assert.Equal(0UL, coverage.TotalRawTradeAllocatedQuantity);
    }

    [Fact]
    public void IntegratedPhaseCannotCoexistWithMarketOrWorkerDemandPhase()
    {
        var state = State();
        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                Phase(state),
                new EconomicResidentMarketTurnPhase(
                    Array.Empty<EconomicProductionPotential>(),
                    Array.Empty<EconomicResidentDemandProfile>(),
                    Array.Empty<EconomicTransferSignal>(),
                    new EconomicMarketAllocationPolicy(
                        900_000UL, 900_000UL, 1, 1))
            }));
    }

    [Fact]
    public void FoodCoverageTurnPreservesPopulationAndHashAcrossAuthoritativeClosure()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 2);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
            civilizations);

        var economicPoints = new[]
        {
            new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                civilizations.Records[0].Id,
                civilizations.Records[0].StartCellId!.Value, 2UL,
                new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 2UL)
                }),
            new EconomicPointRuntimeState(
                new EconomicPointId(2UL),
                civilizations.Records[1].Id,
                civilizations.Records[1].StartCellId!.Value, 0UL)
        };
        var initial = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(economicPoints),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(
                        new EconomicPointId(2UL), 10UL)
                }));

        var route = new StrategicEconomicRouteResolver(
            world.StrategicSurfaceGraph).ResolveDetailed(
                initial.Economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                StrategicEdgeAccessSnapshot.AllOpen);
        Assert.True(route.IsReachable);

        var signal = new EconomicTransferSignalResolver().Resolve(
            initial.Economy, route.Route!,
            new EconomicTransferPolicy(1UL, 1_000_000UL, 0UL, 0UL));

        var session = new CanonicalGameSession(world);
        session.AcceptSnapshot(initial);
        var phase = Phase(initial, new[] { signal });
        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
                {
                    phase
                })),
            new TurnNumber(1UL), 1U);

        var oldHash = new CanonicalWorldStateHasher().Compute(initial);
        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            Input(initial)));

        Assert.True(result.WasResolved);
        Assert.Equal(3, result.ResolutionResult!.Events.Count);
        var coverage = Assert.IsType<EconomicFoodMarketCoverageResolvedEvent>(
            result.ResolutionResult.Events[2]);
        Assert.Equal(1_000_000UL,
            coverage.Coverage.TotalRawTradeAllocatedQuantity);
        Assert.Equal(4_000_000UL,
            coverage.Coverage.TotalRawUnallocatedQuantity);
        Assert.Same(initial, session.Snapshot);
        Assert.Equal(oldHash,
            new CanonicalWorldStateHasher().Compute(session.Snapshot));
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
    }

    [Fact]
    public void PhaseFailsWhenCensusIsNotInitialized()
    {
        var state = WorldState.CreateInitial();
        var phase = new EconomicResidentFoodMarketTurnPhase(
            Array.Empty<EconomicProductionPotential>(),
            Array.Empty<EconomicResidentDemandProfile>(),
            Array.Empty<EconomicTransferSignal>(),
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            Catalog());

        Assert.Throws<InvalidOperationException>(() =>
            phase.Resolve(Input(state), state));
    }

    [Fact]
    public void PhaseChecksNullCatalogAndNullResolutionInputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentFoodMarketTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>(),
                Array.Empty<EconomicTransferSignal>(),
                new EconomicMarketAllocationPolicy(
                    900_000UL, 900_000UL, 1, 1), null!));

        var phase = Phase(State());
        Assert.Throws<ArgumentNullException>(() =>
            phase.Resolve(null!, State()));
        Assert.Throws<ArgumentNullException>(() =>
            phase.Resolve(Input(State()), null!));
    }
}
