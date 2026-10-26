using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoodStrategicEdgeCapacityAuditorTests
{
    private static (WorldGenerationResult World, WorldState State) Fixture()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 2);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
            civilizations);
        var source = civilizations.Records[0];
        var destination = civilizations.Records[1];

        // Two destination points occupy the same legitimate strategic cell:
        // their food trades must count toward the same strategic edge capacity.
        var state = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL), source.Id,
                        source.StartCellId!.Value, 2UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL), destination.Id,
                        destination.StartCellId!.Value, 0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(3UL), destination.Id,
                        destination.StartCellId!.Value, 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(new EconomicPointId(2UL), 10UL),
                    new PopulationPointRuntimeState(new EconomicPointId(3UL), 10UL)
                }));
        return (world, state);
    }

    private static FoodCommodityCatalog Catalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(8U), "roots", 2_000UL),
            new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
        });

    private static EconomicTradeAllocation Trade(
        ulong source = 1UL,
        ulong destination = 2UL,
        uint food = 7U,
        ulong quantity = 1_000_000UL) =>
        new(new EconomicPointId(source),
            EconomicActivityKind.Agriculture,
            new EconomicPointId(destination),
            new CommodityId(food),
            quantity, 900_000UL, 100_000UL);

    private static StrategicEconomicRoute Route(
        WorldGenerationResult world, WorldState state,
        ulong from = 1UL, ulong to = 2UL)
    {
        var route = new StrategicEconomicRouteResolver(
            world.StrategicSurfaceGraph).ResolveDetailed(
                state.Economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(from), new EconomicPointId(to)),
                StrategicEdgeAccessSnapshot.AllOpen);
        Assert.True(route.IsReachable);
        return route.Route!;
    }

    private static FoodStrategicEdgeMassCapacity[] Capacities(
        StrategicEconomicRoute route, ulong grams) =>
        route.EdgePath
            .Select(edge => new FoodStrategicEdgeMassCapacity(edge, grams))
            .ToArray();

    private static FoodStrategicEdgeCapacityAudit Audit(
        WorldGenerationResult world,
        WorldState state,
        EconomicMarketAllocationResult market,
        IEnumerable<FoodStrategicEdgeMassCapacity> capacities,
        StrategicEdgeAccessSnapshot? access = null,
        FoodCommodityCatalog? catalog = null) =>
        new FoodStrategicEdgeCapacityAuditor().Audit(
            world, state,
            access ?? StrategicEdgeAccessSnapshot.AllOpen,
            market, catalog ?? Catalog(), capacities);

    [Fact]
    public void SharedEdgeTotalsTwoDifferentFoodMassScalesAndDetectsOverload()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        Assert.NotEmpty(route.EdgePath);

        var market = new EconomicMarketAllocationResult(new[]
        {
            Trade(destination: 3UL, food: 8U),  // 2 kg
            Trade(destination: 2UL, food: 7U)   // 1 kg
        });

        var report = Audit(world, state, market,
            Capacities(route, grams: 2_500UL));
        Assert.False(report.AllUsedEdgesWithinCapacity);
        Assert.Equal(route.HopCount, report.OverloadedEdgeCount);
        Assert.Equal(route.HopCount, report.UsedEdges.Count);

        foreach (var row in report.UsedEdges)
        {
            Assert.Equal((UInt128)3_000_000_000UL, row.RawMicrograms);
            Assert.Equal((UInt128)2_500_000_000UL, row.RawCapacityMicrograms);
            Assert.Equal((UInt128)500_000_000UL, row.RawOverCapacityMicrograms);
            Assert.True(row.IsOverCapacity);
        }
    }

    [Fact]
    public void ExactOrGenerousEdgeCapacitiesPassWithoutInventingThroughput()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var market = new EconomicMarketAllocationResult(new[]
        {
            Trade(food: 7U), Trade(destination: 3UL, food: 8U)
        });

        var exact = Audit(world, state, market,
            Capacities(route, 3_000UL));
        var generous = Audit(world, state, market,
            Capacities(route, 4_000UL));

        Assert.True(exact.AllUsedEdgesWithinCapacity);
        Assert.Equal(0, exact.OverloadedEdgeCount);
        Assert.All(exact.UsedEdges, row =>
            Assert.Equal((UInt128)0U, row.RawOverCapacityMicrograms));

        Assert.True(generous.AllUsedEdgesWithinCapacity);
        Assert.All(generous.UsedEdges, row =>
            Assert.Equal((UInt128)4_000_000_000UL,
                row.RawCapacityMicrograms));
    }

    [Fact]
    public void ActualResidentMarketAllocatesFoodsSeparatelyButSharedEdgesRevealOverload()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var catalog = Catalog();
        var foodProfiles = FoodResidentDemandProfileMaterializer.Materialize(
            state, catalog, new[]
            {
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(2UL), new CommodityId(7U), 500UL),
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(3UL), new CommodityId(8U), 1_000UL)
            });

        var marketPhase = new EconomicResidentFoodMarketTurnPhase(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L),
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(8U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            foodProfiles,
            new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(2UL)),
                    route.HopCount, 100_000UL, 1_000_000UL, 0UL),
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(3UL)),
                    route.HopCount, 100_000UL, 1_000_000UL, 0UL)
            },
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            catalog);

        var hash = new CanonicalWorldStateHasher().Compute(state);
        var resolved = marketPhase.Resolve(
            new TurnResolutionInput(
                state, Array.Empty<ISimulationCommand>(),
                new SimulationContext(
                    new TurnNumber(1UL), new SimulationSeed(5UL))),
            state);

        var chosen = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolved.Events[1]).Allocation;
        Assert.Equal(2, chosen.Allocations.Count);
        Assert.Equal(new[] { 7U, 8U },
            chosen.Allocations.Select(trade => trade.CommodityId.Value));

        // Existing market has per-commodity capacity. Two different foods
        // legitimately obtain separate route allocations, while the physical
        // edge audit reveals their combined 3 kg load against 2.5 kg capacity.
        var audit = Audit(world, state, chosen, Capacities(route, 2_500UL));
        Assert.False(audit.AllUsedEdgesWithinCapacity);
        Assert.All(audit.UsedEdges, edge =>
            Assert.Equal((UInt128)500_000_000UL,
                edge.RawOverCapacityMicrograms));
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
        Assert.Same(state, resolved.ResultingWorldState);
    }

    [Fact]
    public void MissingCapacityOnAnyUsedStrategicEdgeFailsClosed()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var capacities = Capacities(route, 4_000UL).Skip(1).ToArray();

        Assert.Throws<InvalidOperationException>(() =>
            Audit(world, state,
                new EconomicMarketAllocationResult(new[] { Trade() }),
                capacities));
    }

    [Fact]
    public void ExplicitZeroCapacityProducesAuditableOverload()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var report = Audit(world, state,
            new EconomicMarketAllocationResult(new[] { Trade() }),
            Capacities(route, 0UL));

        Assert.False(report.AllUsedEdgesWithinCapacity);
        Assert.All(report.UsedEdges, row =>
        {
            Assert.Equal((UInt128)0U, row.RawCapacityMicrograms);
            Assert.Equal((UInt128)1_000_000_000UL,
                row.RawOverCapacityMicrograms);
        });
    }

    [Fact]
    public void OmittedOrUnregisteredCommodityCannotMakeSharedCapacityLookSafe()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);

        Assert.Throws<InvalidOperationException>(() =>
            Audit(world, state,
                new EconomicMarketAllocationResult(new[]
                {
                    Trade(food: 7U),
                    Trade(destination: 3UL, food: 42U)
                }),
                Capacities(route, 100_000UL)));
    }

    [Fact]
    public void ClosedStrategicNetworkCannotAcceptPreallocatedMarketTrade()
    {
        var (world, state) = Fixture();
        var allEdgesClosed = new StrategicEdgeAccessSnapshot(
            world.StrategicTopology.Edges.Select(edge => edge.Id));

        Assert.Throws<InvalidOperationException>(() =>
            Audit(world, state,
                new EconomicMarketAllocationResult(new[] { Trade() }),
                Capacities(Route(world, state), 100_000UL),
                allEdgesClosed));
    }

    [Fact]
    public void DuplicateUnknownOrNullEdgeCapacitiesAreRejected()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var edge = route.EdgePath[0];
        var capacity = new FoodStrategicEdgeMassCapacity(edge, 10UL);
        var market = new EconomicMarketAllocationResult(new[] { Trade() });

        Assert.Throws<ArgumentException>(() =>
            Audit(world, state, market, new[] { capacity, capacity }));
        Assert.Throws<ArgumentException>(() =>
            Audit(world, state, market,
                new FoodStrategicEdgeMassCapacity[] { null! }));

        var unknown = new StrategicEdgeId(
            world.StrategicTopology.Edges.Max(e => e.Id.Value) + 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            Audit(world, state, market,
                new[] { new FoodStrategicEdgeMassCapacity(unknown, 1UL) }));
        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeMassCapacity(default, 1UL));
    }

    [Fact]
    public void UnboundOrWrongGeneratedWorldIsRejected()
    {
        var (world, state) = Fixture();
        var market = new EconomicMarketAllocationResult(new[] { Trade() });
        var capacities = Capacities(Route(world, state), 5_000UL);

        Assert.Throws<InvalidOperationException>(() =>
            Audit(world, WorldState.CreateInitial(), market, capacities));

        var anotherWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(9UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            Audit(anotherWorld, state, market, capacities));
    }

    [Fact]
    public void SameCellLocalTradeRequiresNoIntercellEdgeCapacity()
    {
        var (world, state) = Fixture();
        Assert.Equal(0, Route(world, state, from: 2UL, to: 3UL).HopCount);

        var audit = Audit(world, state,
            new EconomicMarketAllocationResult(new[]
            {
                Trade(source: 2UL, destination: 3UL)
            }), Array.Empty<FoodStrategicEdgeMassCapacity>());

        Assert.Empty(audit.UsedEdges);
        Assert.True(audit.AllUsedEdgesWithinCapacity);
        Assert.Equal(0, audit.OverloadedEdgeCount);
    }

    [Fact]
    public void EmptyMarketDoesNotRequireGuessingEdgeCapacities()
    {
        var (world, state) = Fixture();
        var audit = Audit(world, state,
            new EconomicMarketAllocationResult(
                Array.Empty<EconomicTradeAllocation>()),
            Array.Empty<FoodStrategicEdgeMassCapacity>());

        Assert.Empty(audit.UsedEdges);
        Assert.True(audit.AllUsedEdgesWithinCapacity);
    }

    [Fact]
    public void InputOrderingAndRepeatedAuditAreDeterministicAndReadOnly()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var market = new EconomicMarketAllocationResult(new[]
        {
            Trade(destination: 3UL, food: 8U),
            Trade(food: 7U)
        });
        var capacities = Capacities(route, 3_000UL);
        var hash = new CanonicalWorldStateHasher().Compute(state);

        var original = Audit(world, state, market, capacities);
        var reversed = Audit(world, state, market, capacities.Reverse());

        Assert.Equal(
            original.UsedEdges.Select(row =>
                (row.StrategicEdgeId, row.RawMicrograms,
                    row.RawCapacityMicrograms)),
            reversed.UsedEdges.Select(row =>
                (row.StrategicEdgeId, row.RawMicrograms,
                    row.RawCapacityMicrograms)));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodStrategicEdgeMassLoad>)original.UsedEdges).Clear());
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void MicrogramArithmeticPreservesFractionalGramWithoutRounding()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var report = Audit(world, state,
            new EconomicMarketAllocationResult(new[]
            {
                Trade(quantity: 1UL)
            }), Capacities(route, 0UL));

        Assert.All(report.UsedEdges, row =>
        {
            // One raw commodity subunit of 1,000 grams equals 1 mg,
            // exactly 1,000 micrograms. The audit does not round to grams.
            Assert.Equal((UInt128)1_000U, row.RawMicrograms);
            Assert.Equal((UInt128)1_000U,
                row.RawOverCapacityMicrograms);
        });
    }

    [Fact]
    public void RawMicrogramsCanExceedUInt64WithoutSilentOverflow()
    {
        var (world, state) = Fixture();
        var route = Route(world, state);
        var hugeFood = new FoodCommodityCatalog(new[]
        {
            new FoodCommodityDefinition(new CommodityId(7U),
                "large-unit", ulong.MaxValue)
        });
        var market = new EconomicMarketAllocationResult(new[]
        {
            new EconomicTradeAllocation(new EconomicPointId(1UL),
                EconomicActivityKind.Agriculture,
                new EconomicPointId(2UL), new CommodityId(7U),
                20_000_000UL, 1UL, 0UL)
        });
        var result = Audit(world, state, market,
            Capacities(route, ulong.MaxValue), catalog: hugeFood);

        Assert.All(result.UsedEdges, row =>
            Assert.True(row.RawMicrograms > ulong.MaxValue));
    }

    [Fact]
    public void AuditConstructorRejectsDuplicatedOrInvalidResultRows()
    {
        var edge = new StrategicEdgeId(1UL);
        var row = new FoodStrategicEdgeMassLoad(edge, 10U, 5U);

        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeCapacityAudit(new[] { row, row }));
        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeCapacityAudit(
                new FoodStrategicEdgeMassLoad[] { null! }));
        Assert.Throws<ArgumentException>(() =>
            new FoodStrategicEdgeMassLoad(default, 1U, 1U));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodStrategicEdgeCapacityAudit(null!));
    }

    [Fact]
    public void AuditorRequiresAllExplicitInputs()
    {
        var (world, state) = Fixture();
        var auditor = new FoodStrategicEdgeCapacityAuditor();
        var market = new EconomicMarketAllocationResult(
            Array.Empty<EconomicTradeAllocation>());
        var capacities = Array.Empty<FoodStrategicEdgeMassCapacity>();

        Assert.Throws<ArgumentNullException>(() =>
            auditor.Audit(null!, state, StrategicEdgeAccessSnapshot.AllOpen,
                market, Catalog(), capacities));
        Assert.Throws<ArgumentNullException>(() =>
            auditor.Audit(world, null!, StrategicEdgeAccessSnapshot.AllOpen,
                market, Catalog(), capacities));
        Assert.Throws<ArgumentNullException>(() =>
            auditor.Audit(world, state, null!, market, Catalog(), capacities));
        Assert.Throws<ArgumentNullException>(() =>
            auditor.Audit(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                null!, Catalog(), capacities));
        Assert.Throws<ArgumentNullException>(() =>
            auditor.Audit(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                market, null!, capacities));
        Assert.Throws<ArgumentNullException>(() =>
            auditor.Audit(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                market, Catalog(), null!));
    }
}
