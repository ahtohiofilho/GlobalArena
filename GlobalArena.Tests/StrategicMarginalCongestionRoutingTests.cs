using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicMarginalCongestionRoutingTests
{
    private static (WorldGenerationResult World, WorldState State) Fixture()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civ = RuntimeCivilizationMaterializer.Materialize(world, 2);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(civ);

        var state = WorldState.CreateBound(world)
            .WithCivilizations(civ)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL), civ.Records[0].Id,
                        civ.Records[0].StartCellId!.Value, 2UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL), civ.Records[1].Id,
                        civ.Records[1].StartCellId!.Value, 0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(3UL), civ.Records[1].Id,
                        civ.Records[1].StartCellId!.Value, 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(new EconomicPointId(2UL), 10UL),
                    new PopulationPointRuntimeState(new EconomicPointId(3UL), 10UL)
                }));
        return (world, state);
    }

    private static StrategicEconomicRouteKey Key(
        ulong source = 1UL, ulong destination = 2UL) =>
        new(new EconomicPointId(source), new EconomicPointId(destination));

    private static StrategicEconomicRoute Shortest(
        WorldGenerationResult world, WorldState state) =>
        new StrategicEconomicRouteResolver(world.StrategicSurfaceGraph)
            .ResolveDetailed(
                state.Economy, Key(), StrategicEdgeAccessSnapshot.AllOpen)
            .Route!;

    private static FoodCommodityCatalog FoodCatalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
        });

    private static EconomicProductionDemandResult SingleFlows(
        ulong quantity = 1_000_000UL) =>
        new(
            new[]
            {
                new EconomicSupplyFlow(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, quantity)
            },
            new[]
            {
                new EconomicDemandFlow(
                    new EconomicPointId(2UL), new CommodityId(7U), quantity)
            });

    private static EconomicCongestionDemandBid[] Bids(
        ulong unitValue = 900_000UL) =>
        new[]
        {
            new EconomicCongestionDemandBid(
                new EconomicPointId(2UL), new CommodityId(7U), unitValue)
        };

    private static EconomicCongestionRouteOffer[] Offers() =>
        new[] { new EconomicCongestionRouteOffer(Key(), 0UL) };

    private static FoodStrategicEdgeCongestionProfile[] Profiles(
        WorldGenerationResult world,
        StrategicEdgeId? expensive = null,
        ulong characteristic = 1_000_000UL,
        ulong congestionCost = 1UL) =>
        world.StrategicTopology.Edges
            .Select(edge => new FoodStrategicEdgeCongestionProfile(
                edge.Id, characteristic,
                expensive == edge.Id ? 1_000_000UL : 1UL,
                congestionCost))
            .ToArray();

    private static EconomicCongestionMarketResult Market(
        WorldGenerationResult world,
        WorldState state,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles,
        StrategicCongestionRoutingMode mode,
        ulong quantity = 1_000_000UL,
        ulong quantum = 1_000_000UL,
        ulong unitValue = 900_000UL,
        StrategicEdgeAccessSnapshot? access = null) =>
        new EconomicCongestionPricedFoodMarketResolver().Resolve(
            world, state,
            access ?? StrategicEdgeAccessSnapshot.AllOpen,
            SingleFlows(quantity),
            Bids(unitValue), Offers(), FoodCatalog(), profiles,
            new EconomicCongestionMarketSearchPolicy(quantum, 128),
            mode);

    [Fact]
    public void AdaptiveMarketChoosesLongerAffordableRouteInsteadOfExpensiveShortestHop()
    {
        var (world, state) = Fixture();
        var defaultRoute = Shortest(world, state);
        Assert.True(defaultRoute.HopCount > 0);
        var expensiveEdge = defaultRoute.EdgePath[0];
        var profiles = Profiles(world, expensive: expensiveEdge);

        var legacyChoice = Market(
            world, state, profiles, StrategicCongestionRoutingMode.FixedShortestHop);
        var adaptiveChoice = Market(
            world, state, profiles, StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost);

        Assert.Empty(legacyChoice.Selections);

        var selected = Assert.Single(adaptiveChoice.Selections);
        Assert.Equal(1_000_000UL, selected.RawQuantity);
        Assert.True(selected.RawNetTradeReturn.Numerator > 0);
        Assert.NotEmpty(selected.RoutedEdgePath);
        Assert.DoesNotContain(expensiveEdge, selected.RoutedEdgePath);
        Assert.True(selected.RoutedEdgePath.Count >= defaultRoute.HopCount);
        Assert.Equal(selected.RoutedEdgePath.Count,
            adaptiveChoice.FinalCongestion.UsedEdges.Count);
        Assert.All(adaptiveChoice.FinalCongestion.UsedEdges,
            row => Assert.Equal(new BigInteger(1_000_000_000UL),
                row.LoadMicrograms));
    }

    [Fact]
    public void RepeatedMarginalDecisionsCanDivertTradeToAlternatePaths()
    {
        var (world, state) = Fixture();
        var profiles = world.StrategicTopology.Edges
            .Select(edge => new FoodStrategicEdgeCongestionProfile(
                edge.Id, 100UL, 0UL, 100UL))
            .ToArray();

        var adaptive = Market(world, state, profiles,
            StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost,
            quantity: 400_000UL, quantum: 50_000UL);

        Assert.Equal(400_000UL,
            adaptive.Selections.Aggregate(0UL,
                (sum, row) => checked(sum + row.RawQuantity)));
        Assert.True(adaptive.Selections.Count > 1);

        var routeVariants = adaptive.Selections
            .Select(row => string.Join("-", row.RoutedEdgePath.Select(edge => edge.Value)))
            .Distinct()
            .ToArray();
        Assert.True(routeVariants.Length > 1,
            "A low-cost unused alternate path should attract later tranches after initial shared-edge congestion.");
        Assert.All(adaptive.Selections,
            row => Assert.True(row.RawNetTradeReturn.Numerator > 0));
    }

    [Fact]
    public void CheapestRouteSearchFavorsShortestHopOnExactMarginalCostTies()
    {
        var (world, state) = Fixture();
        var constant = world.StrategicTopology.Edges
            .Select(edge => new FoodStrategicEdgeCongestionProfile(
                edge.Id, 100UL, 0UL, 1UL))
            .ToArray();
        var solver = new StrategicMarginalCongestionRouteResolver(
            world.StrategicSurfaceGraph, state.Economy);
        var found = solver.Resolve(Key(), StrategicEdgeAccessSnapshot.AllOpen,
            constant.ToDictionary(row => row.StrategicEdgeId),
            new Dictionary<StrategicEdgeId, BigInteger>());

        Assert.NotNull(found);
        Assert.Equal(Shortest(world, state).HopCount, found!.HopCount);
    }

    [Fact]
    public void RouteAndDecisionsAreCanonicalDespiteProfileInputOrder()
    {
        var (world, state) = Fixture();
        var profiles = Profiles(world, Shortest(world, state).EdgePath[0]);
        var before = new CanonicalWorldStateHasher().Compute(state);

        var a = Market(world, state, profiles,
            StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost);
        var b = Market(world, state, profiles.Reverse(),
            StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost);

        Assert.Equal(
            a.Selections.Select(row =>
                (row.RawQuantity, row.RawNetTradeReturn.Numerator,
                    row.RawNetTradeReturn.Denominator,
                    string.Join(",", row.RoutedEdgePath.Select(edge => edge.Value)))),
            b.Selections.Select(row =>
                (row.RawQuantity, row.RawNetTradeReturn.Numerator,
                    row.RawNetTradeReturn.Denominator,
                    string.Join(",", row.RoutedEdgePath.Select(edge => edge.Value)))));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(state));

        var selected = Assert.Single(a.Selections);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StrategicEdgeId>)selected.RoutedEdgePath).Clear());
    }

    [Fact]
    public void AdaptiveSearchRequiresEveryOpenEdgeToHaveAnExplicitCostProfile()
    {
        var (world, state) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            Market(world, state, Profiles(world).Skip(1),
                StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost));
    }

    [Fact]
    public void ActualEdgeClosureOverridesEvenAnEconomicallyAttractiveRoute()
    {
        var (world, state) = Fixture();
        var profiles = Profiles(world);
        var closed = new StrategicEdgeAccessSnapshot(
            world.StrategicTopology.Edges.Select(edge => edge.Id));
        var outcome = Market(world, state, profiles,
            StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost,
            access: closed);

        Assert.Empty(outcome.Selections);
        Assert.Empty(outcome.FinalCongestion.UsedEdges);
    }

    [Fact]
    public void EconomicTurnCanExplicitlyEnableDynamicRoutingWithoutChangingHash()
    {
        var (world, state) = Fixture();
        var catalogue = FoodCatalog();
        var profiles = Profiles(world, Shortest(world, state).EdgePath[0]);
        var residentDemand = FoodResidentDemandProfileMaterializer.Materialize(
            state, catalogue,
            new[]
            {
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(2UL), new CommodityId(7U), 500UL)
            });

        var phase = new EconomicResidentCongestionPricedTurnPhase(
            world, StrategicEdgeAccessSnapshot.AllOpen,
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            residentDemand, Bids(), Offers(), catalogue, profiles,
            new EconomicCongestionMarketSearchPolicy(1_000_000UL, 32),
            StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost);

        var hash = new CanonicalWorldStateHasher().Compute(state);
        var outcome = phase.Resolve(
            new TurnResolutionInput(
                state, Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(1UL), new SimulationSeed(7UL))),
            state);

        Assert.Equal(2, outcome.Events.Count);
        var market = Assert.IsType<EconomicCongestionPricedMarketResolvedEvent>(
            outcome.Events[1]);
        Assert.NotEmpty(market.Market.Selections);
        Assert.All(market.Market.Selections, row =>
            Assert.DoesNotContain(Shortest(world, state).EdgePath[0],
                row.RoutedEdgePath));
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
        Assert.Same(state, outcome.ResultingWorldState);
    }

    [Fact]
    public void InvalidModeIsRejectedBeforeAnySelection()
    {
        var (world, state) = Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Market(world, state, Profiles(world),
                (StrategicCongestionRoutingMode)42));
    }

    [Fact]
    public void MarginalPathResolverRejectsInvalidInputsAndUnknownEconomicPoints()
    {
        var (world, state) = Fixture();
        var router = new StrategicMarginalCongestionRouteResolver(
            world.StrategicSurfaceGraph, state.Economy);
        var profiles = Profiles(world)
            .ToDictionary(row => row.StrategicEdgeId);
        var loads = new Dictionary<StrategicEdgeId, BigInteger>();

        Assert.Throws<ArgumentNullException>(() =>
            router.Resolve(Key(), null!, profiles, loads));
        Assert.Throws<ArgumentNullException>(() =>
            router.Resolve(Key(), StrategicEdgeAccessSnapshot.AllOpen,
                null!, loads));
        Assert.Throws<ArgumentNullException>(() =>
            router.Resolve(Key(), StrategicEdgeAccessSnapshot.AllOpen,
                profiles, null!));
        Assert.Throws<KeyNotFoundException>(() =>
            router.Resolve(Key(destination: 999UL),
                StrategicEdgeAccessSnapshot.AllOpen, profiles, loads));
        Assert.Throws<ArgumentNullException>(() =>
            new StrategicMarginalCongestionRouteResolver(
                null!, state.Economy));
    }

    [Fact]
    public void SameCellTradeHasEmptyRouteWithoutPhysicalLoadThreshold()
    {
        var (world, state) = Fixture();
        var router = new StrategicMarginalCongestionRouteResolver(
            world.StrategicSurfaceGraph, state.Economy);
        var local = router.Resolve(Key(2UL, 3UL),
            StrategicEdgeAccessSnapshot.AllOpen,
            new Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile>(),
            new Dictionary<StrategicEdgeId, BigInteger>());

        Assert.NotNull(local);
        Assert.Equal(0, local!.HopCount);
        Assert.Empty(local.EdgePath);
    }
}
