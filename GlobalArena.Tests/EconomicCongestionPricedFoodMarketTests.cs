using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicCongestionPricedFoodMarketTests
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

    private static FoodCommodityCatalog Catalog() => new(new[]
    {
        new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL),
        new FoodCommodityDefinition(new CommodityId(8U), "roots", 2_000UL)
    });

    private static EconomicProductionDemandResult Flows(
        ulong grainSupply = 2_000_000UL,
        ulong rootsSupply = 2_000_000UL,
        ulong grainDemand = 5_000_000UL,
        ulong rootsDemand = 5_000_000UL)
    {
        return new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, grainSupply),
                new EconomicSupplyFlow(
                    new EconomicPointId(1UL), new CommodityId(8U),
                    EconomicActivityKind.Agriculture, rootsSupply)
            },
            new[]
            {
                new EconomicDemandFlow(
                    new EconomicPointId(2UL), new CommodityId(7U), grainDemand),
                new EconomicDemandFlow(
                    new EconomicPointId(3UL), new CommodityId(8U), rootsDemand)
            });
    }

    private static EconomicCongestionDemandBid[] Bids(
        ulong grainPrice = 900_000UL, ulong rootsPrice = 900_000UL) =>
        new[]
        {
            new EconomicCongestionDemandBid(
                new EconomicPointId(2UL), new CommodityId(7U), grainPrice),
            new EconomicCongestionDemandBid(
                new EconomicPointId(3UL), new CommodityId(8U), rootsPrice)
        };

    private static EconomicCongestionRouteOffer[] Offers() => new[]
    {
        new EconomicCongestionRouteOffer(
            new StrategicEconomicRouteKey(
                new EconomicPointId(1UL), new EconomicPointId(2UL)), 0UL),
        new EconomicCongestionRouteOffer(
            new StrategicEconomicRouteKey(
                new EconomicPointId(1UL), new EconomicPointId(3UL)), 0UL)
    };

    private static StrategicEconomicRoute Route(
        WorldGenerationResult world, WorldState state) =>
        new StrategicEconomicRouteResolver(world.StrategicSurfaceGraph)
            .ResolveDetailed(
                state.Economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL), new EconomicPointId(2UL)),
                StrategicEdgeAccessSnapshot.AllOpen).Route!;

    private static FoodStrategicEdgeCongestionProfile[] Profiles(
        WorldGenerationResult world, WorldState state,
        ulong referenceGrams = 1_000UL,
        ulong baseCost = 0UL,
        ulong congestionCost = 100UL) =>
        Route(world, state).EdgePath.Select(edge =>
            new FoodStrategicEdgeCongestionProfile(
                edge, referenceGrams, baseCost, congestionCost)).ToArray();

    private static EconomicCongestionMarketResult Resolve(
        WorldGenerationResult world, WorldState state,
        EconomicProductionDemandResult? flows = null,
        IEnumerable<EconomicCongestionDemandBid>? bids = null,
        IEnumerable<EconomicCongestionRouteOffer>? offers = null,
        IEnumerable<FoodStrategicEdgeCongestionProfile>? profiles = null,
        EconomicCongestionMarketSearchPolicy? policy = null,
        StrategicEdgeAccessSnapshot? access = null) =>
        new EconomicCongestionPricedFoodMarketResolver().Resolve(
            world, state,
            access ?? StrategicEdgeAccessSnapshot.AllOpen,
            flows ?? Flows(),
            bids ?? Bids(),
            offers ?? Offers(),
            Catalog(),
            profiles ?? Profiles(world, state),
            policy ?? new EconomicCongestionMarketSearchPolicy(
                1_000_000UL, 32));

    [Fact]
    public void FoodCommoditiesCompeteForSameEdgeDuringSelectionNotAfterSettlement()
    {
        var (world, state) = Fixture();
        var result = Resolve(world, state);
        var route = Route(world, state);

        Assert.Equal(route.HopCount, result.FinalCongestion.UsedEdges.Count);
        Assert.NotEmpty(result.Selections);
        Assert.Contains(result.Selections, row =>
            row.CommodityId == new CommodityId(7U));
        Assert.Contains(result.Selections, row =>
            row.CommodityId == new CommodityId(8U));

        ulong grain = 0UL, roots = 0UL;
        foreach (var row in result.Selections)
        {
            if (row.CommodityId == new CommodityId(7U))
                grain = checked(grain + row.RawQuantity);
            else
                roots = checked(roots + row.RawQuantity);
            Assert.True(row.RawNetTradeReturn.Numerator > BigInteger.Zero);
        }

        Assert.Equal(2_000_000UL, grain);
        Assert.InRange(roots, 1UL, 999_999UL);
        var expectedMicrograms = (BigInteger)grain * 1_000UL
            + (BigInteger)roots * 2_000UL;
        Assert.All(result.FinalCongestion.UsedEdges,
            edge => Assert.Equal(expectedMicrograms, edge.LoadMicrograms));
        Assert.True(expectedMicrograms > (BigInteger)1_000_000_000UL);
    }

    [Fact]
    public void HigherExplicitValuationAllowsMoreLoadWithoutPhysicalCapacityCliff()
    {
        var (world, state) = Fixture();
        var flows = Flows(grainSupply: 10_000_000UL);
        var low = Resolve(world, state, flows,
            Bids(grainPrice: 900_000UL));
        var high = Resolve(world, state, flows,
            Bids(grainPrice: 9_000_000UL));

        ulong Count(EconomicCongestionMarketResult result) =>
            checked((ulong)result.Selections
                .Where(row => row.CommodityId == new CommodityId(7U))
                .Sum(row => checked((long)row.RawQuantity)));

        Assert.True(Count(high) > Count(low));
        Assert.True(Count(high) >= 4_000_000UL);
        Assert.All(high.Selections, row =>
            Assert.True(row.RawNetTradeReturn.Numerator > BigInteger.Zero));
    }

    [Fact]
    public void VerySmallCongestionParameterAllowsTradeFarBeyondReferenceScale()
    {
        var (world, state) = Fixture();
        var single = new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 2_000_000UL)
            },
            new[]
            {
                new EconomicDemandFlow(
                    new EconomicPointId(2UL), new CommodityId(7U),
                    2_000_000UL)
            });
        var result = Resolve(world, state, single,
            Bids().Take(1), Offers().Take(1),
            Profiles(world, state, referenceGrams: 100UL, congestionCost: 1UL));

        Assert.Equal(2_000_000UL,
            result.Selections.Aggregate(0UL,
                (total, row) => checked(total + row.RawQuantity)));
        Assert.All(result.FinalCongestion.UsedEdges, edge =>
            Assert.Equal((BigInteger)2_000_000_000UL, edge.LoadMicrograms));
    }

    [Fact]
    public void ExactConvexIncrementIsChargedBeforeNextDecision()
    {
        var (world, state) = Fixture();
        var single = new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000UL)
            },
            new[]
            {
                new EconomicDemandFlow(
                    new EconomicPointId(2UL), new CommodityId(7U),
                    1_000_000UL)
            });
        var result = Resolve(world, state, single,
            Bids().Take(1), Offers().Take(1));
        var selection = Assert.Single(result.Selections);
        var hops = Route(world, state).HopCount;

        Assert.Equal(new BigInteger(900_000), selection.RawGrossTradeValue.Numerator);
        Assert.Equal(BigInteger.One, selection.RawGrossTradeValue.Denominator);
        Assert.Equal(new BigInteger(100_000 * hops),
            selection.RawAdditionalCongestionCost.Numerator *
                3 / selection.RawAdditionalCongestionCost.Denominator);
        Assert.True(selection.RawNetTradeReturn.Numerator > 0);
    }

    [Fact]
    public void SearchBudgetFailsClosedInsteadOfSilentlyClippingTrade()
    {
        var (world, state) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(world, state,
                policy: new EconomicCongestionMarketSearchPolicy(
                    1_000_000UL, 1)));
    }

    [Fact]
    public void ClosedStrategicRoutesDoNotCauseFreightCapacityCutoffs()
    {
        var (world, state) = Fixture();
        var allClosed = new StrategicEdgeAccessSnapshot(
            world.StrategicTopology.Edges.Select(edge => edge.Id));
        var result = Resolve(world, state, access: allClosed);
        Assert.Empty(result.Selections);
        Assert.Empty(result.FinalCongestion.UsedEdges);
    }

    [Fact]
    public void UnknownProfilesAndNonfoodFlowsFailClosed()
    {
        var (world, state) = Fixture();
        var profiles = Profiles(world, state);
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(world, state, profiles: profiles.Skip(1)));

        var nonfood = new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(
                    new EconomicPointId(1UL), new CommodityId(99U),
                    EconomicActivityKind.Agriculture, 1UL)
            },
            new[]
            {
                new EconomicDemandFlow(
                    new EconomicPointId(2UL), new CommodityId(99U), 1UL)
            });
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(world, state, flows: nonfood));
    }

    [Fact]
    public void InputEnumerationOrderIsDeterministicAndWorldHashIsUnchanged()
    {
        var (world, state) = Fixture();
        var hash = new CanonicalWorldStateHasher().Compute(state);
        var a = Resolve(world, state);
        var b = Resolve(world, state, bids: Bids().Reverse(),
            offers: Offers().Reverse(),
            profiles: Profiles(world, state).Reverse());

        Assert.Equal(
            a.Selections.Select(row =>
                (row.CommodityId, row.RawQuantity,
                    row.RawAdditionalCongestionCost.Numerator,
                    row.RawAdditionalCongestionCost.Denominator)),
            b.Selections.Select(row =>
                (row.CommodityId, row.RawQuantity,
                    row.RawAdditionalCongestionCost.Numerator,
                    row.RawAdditionalCongestionCost.Denominator)));
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EconomicCongestionMarketSelection>)a.Selections).Clear());
    }

    [Fact]
    public void DuplicateOrMissingBidsAndDuplicateOffersAreRejected()
    {
        var (world, state) = Fixture();
        Assert.Throws<ArgumentException>(() =>
            Resolve(world, state, bids: Bids().Take(1)));
        Assert.Throws<ArgumentException>(() =>
            Resolve(world, state, bids: Bids().Concat(Bids().Take(1))));
        Assert.Throws<ArgumentException>(() =>
            Resolve(world, state, offers: Offers().Concat(Offers().Take(1))));
    }

    [Fact]
    public void SearchPolicyAndMarketSelectionsValidateTheirOwnContracts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicCongestionMarketSearchPolicy(0UL, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicCongestionMarketSearchPolicy(1UL, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicCongestionDemandBid(
                new EconomicPointId(1UL), new CommodityId(7U), 0UL));
        Assert.Throws<ArgumentException>(() =>
            new EconomicCongestionMarketResult(
                new[] { new EconomicCongestionMarketSelection(
                    2UL, new EconomicPointId(1UL),
                    EconomicActivityKind.Agriculture, new EconomicPointId(2UL),
                    new CommodityId(7U), 1UL, 2UL, 0UL,
                    new FoodTransportCostFraction(0, 1)) },
                new FoodStrategicEdgeCongestionResult(
                    Array.Empty<FoodStrategicEdgeCongestionEntry>())));
    }

    [Fact]
    public void CanonicalTurnPhaseEmitsTwoEventsWithActualJointClearing()
    {
        var (world, state) = Fixture();
        var catalog = Catalog();
        var demand = FoodResidentDemandProfileMaterializer.Materialize(
            state, catalog,
            new[]
            {
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(2UL), new CommodityId(7U), 500UL),
                new FoodResidentConsumptionRequirement(
                    new EconomicPointId(3UL), new CommodityId(8U), 1_000UL)
            });
        var phase = new EconomicResidentCongestionPricedTurnPhase(
            world, StrategicEdgeAccessSnapshot.AllOpen,
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L),
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(8U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            demand, Bids(), Offers(), catalog, Profiles(world, state),
            new EconomicCongestionMarketSearchPolicy(1_000_000UL, 32));

        var hash = new CanonicalWorldStateHasher().Compute(state);
        var result = phase.Resolve(
            new TurnResolutionInput(
                state, Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(1UL), new SimulationSeed(1UL))),
            state);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(2, result.EventLog.Entries.Count);
        var flow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            result.Events[0]);
        var market = Assert.IsType<EconomicCongestionPricedMarketResolvedEvent>(
            result.Events[1]);
        Assert.Equal(2, flow.Flows.SupplyFlows.Count);
        Assert.NotEmpty(market.Market.Selections);
        Assert.Equal(phase.PhaseId, market.Id.SystemPhaseId);
        Assert.Equal(EventOriginKind.System, market.Id.OriginKind);
        Assert.NotEqual(flow.Id, market.Id);
        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));

        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                phase,
                new EconomicProductionDemandTurnPhase(
                    Array.Empty<EconomicProductionPotential>(),
                    Array.Empty<EconomicDemandProfile>())
            }));
    }

    [Fact]
    public void WrongWorldOrMissingRequiredArgumentsAreRejected()
    {
        var (world, state) = Fixture();
        var wrong = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(19UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(wrong, state));

        var resolver = new EconomicCongestionPricedFoodMarketResolver();
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(null!, state, StrategicEdgeAccessSnapshot.AllOpen,
                Flows(), Bids(), Offers(), Catalog(), Profiles(world, state),
                new EconomicCongestionMarketSearchPolicy(1UL, 100)));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(world, state, StrategicEdgeAccessSnapshot.AllOpen,
                Flows(), Bids(), Offers(), Catalog(), Profiles(world, state),
                null!));
    }
}
