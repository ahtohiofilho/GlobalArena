using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs67IndependentFreightComponentsTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static FoodCommodityCatalog Catalog() => new(new[] {
        new FoodCommodityDefinition(Food, "food", 1),
        new FoodCommodityDefinition(Wood, "wood", 1),
        new FoodCommodityDefinition(Stone, "stone", 1)
    });

    private static CanonicalPrimitiveSiteDiscoveryPolicy SitePolicy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] {
            new CanonicalPrimitiveDemandRate(Food,
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(Wood, 125_000),
            new CanonicalPrimitiveDemandRate(Stone, 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static CanonicalEligibleFreightDiscoveryPolicy Freight(
        CommodityId commodity, EconomicActivityKind activity) => new(
        commodity, activity, new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
        ExperimentalLandInfrastructureKind.StoneRoad, Catalog());

    private static ExperimentalAbstractPurchasingPolicy Purchasing() => new(
        "VS56-PURCHASING", 1, new FoodTransportCostFraction(5000, 1),
        new FoodTransportCostFraction(5, 8));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId, new FoodTransportCostFraction(1, 1)))
            .ToArray();

    private static (GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) SharedBuyer()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var initial = f.Session.InitialTile;
        var buyer = f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId];
        var rights = f.Registry().Entitlements.Single() with {
            Source = CanonicalPrimitiveSiteDiscovery.SiteId(initial),
            Destination = CanonicalPrimitiveSiteDiscovery.SiteId(buyer),
            ServiceId = 77
        };
        f.Session.TransportNetwork.SetEntitlement(rights);
        f.Session.ReassignWorkers(initial,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var portfolio = f.Session.DiscoverPrimitiveFreightPortfolio(
            sites, new[] {
                Freight(Food, EconomicActivityKind.Agriculture),
                Freight(Wood, EconomicActivityKind.Forestry),
                Freight(Stone, EconomicActivityKind.Mining)
            }, "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1,
            "GA-VS56-NOMINAL-LAB");
        return (f, sites, portfolio);
    }

    private static (GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio,
        CanonicalPrimitiveFreightCandidateUse FoodUse,
        CanonicalPrimitiveFreightCandidateUse WoodUse) TwoDisjointRoads()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var source = f.Session.InitialTile;
        var buyerA = f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId];
        // Buyer A also supplies wood to buyer B: the seller's separate
        // production does not mint extra abstract purchasing power.
        // Keep one of A's two existing forestry workers producing locally.
        var buyerB = f.Session.AdjacentWorkable(buyerA)
            .First(x => x != source);

        f.Session.OrderWorkerTransit(buyerA, buyerB,
            EconomicActivityKind.Forestry, EconomicActivityKind.Mining, 1);
        f.Session.AdvanceMovementTurn();
        f.Session.AdvanceMovementTurn();

        var edge = f.Session.FineWorld.StrategicTopology.Edges.First(x =>
            x.IncidentCellIds.Contains(buyerA.FineStrategicCellId) &&
            x.IncidentCellIds.Contains(buyerB.FineStrategicCellId));
        var secondAsset = new EconomicTransportBuiltAsset(
            32, 101, new PhysicalTacticalEdgeId(
                source.FineGoldbergParameters, edge.Id), 7, true);
        f.Session.TransportNetwork.Construct(secondAsset, f.Grant().InfrastructureOwner);
        f.Session.TransportNetwork.IssueGrant(
            f.Grant(assetId: 32, id: "GRANT-ROAD-32", from: 3, through: 100));

        var template = f.Registry().Entitlements.Single();
        f.Session.TransportNetwork.SetEntitlement(template with {
            Source = CanonicalPrimitiveSiteDiscovery.SiteId(source),
            Destination = CanonicalPrimitiveSiteDiscovery.SiteId(buyerA),
            ServiceId = 77
        });
        f.Session.TransportNetwork.SetEntitlement(template with {
            Source = CanonicalPrimitiveSiteDiscovery.SiteId(buyerA),
            Destination = CanonicalPrimitiveSiteDiscovery.SiteId(buyerB),
            ServiceId = 78
        });
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var portfolio = f.Session.DiscoverPrimitiveFreightPortfolio(
            sites, new[] {
                Freight(Food, EconomicActivityKind.Agriculture),
                Freight(Wood, EconomicActivityKind.Forestry)
            }, "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1,
            "GA-VS56-NOMINAL-LAB");
        var food = Assert.Single(portfolio.CandidateUses,
            x => x.Commodity == Food && x.ServiceId == 77);
        var wood = Assert.Single(portfolio.CandidateUses,
            x => x.Commodity == Wood && x.ServiceId == 78);
        return (f, sites, portfolio, food, wood);
    }

    private static CanonicalPrimitiveFreightComponentSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidates,
        ulong quantum = 10_000, int maxQuanta = 8, int cap = 10) =>
        portfolio.SelectIndependentFreightComponents(candidates,
            Prices(sites), Purchasing(), Congestion(),
            quantum, maxQuanta, cap);

    [Fact]
    public void DistinctBuiltRoadsAndBuyersFactorAnOversizedCartesianDomain()
    {
        var (f, sites, portfolio, food, wood) = TwoDisjointRoads();
        var before = f.Session.ExportSaveJson();
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        Assert.True(quantum > 0UL);
        var result = Select(portfolio, sites, new[] { wood, food }, quantum: quantum);
        Assert.Equal(2, result.IndependentComponentCount);
        Assert.Equal(81, result.PossibleGlobalConfigurations);
        Assert.Equal(18, result.EvaluatedComponentConfigurations);
        Assert.Equal(new[] { Food, Wood }, result.Lots.Select(x => x.Candidate.Commodity));
        Assert.All(result.Components, x => Assert.Equal(9, x.EvaluatedConfigurations));
        Assert.True(result.Preview!.IsBudgetFeasible);
        Assert.Equal(0, result.TotalBuyerCommitment.CompareTo(
            result.Preview.Buyers.Buyers.Aggregate(
                new FoodTransportCostFraction(0, 1), (sum, x) =>
                    new FoodTransportCostFraction(
                        sum.Numerator * x.TotalCommitment.Denominator +
                        x.TotalCommitment.Numerator * sum.Denominator,
                        sum.Denominator * x.TotalCommitment.Denominator))));
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.IsCommitted);
        Assert.Equal(before, f.Session.ExportSaveJson());
        result.VerifyIndependent();
        Assert.Empty(typeof(CanonicalPrimitiveFreightComponentSelection).GetConstructors());

        Assert.Throws<ArgumentException>(() => portfolio.SelectJointFreight(
            new[] { food, wood }, Prices(sites), Purchasing(), Congestion(),
            quantum, 8, 10));
    }

    [Fact]
    public void SharedBuyerAndRoadRemainOneComponentWithExactVs66Result()
    {
        var (f, sites, portfolio) = SharedBuyer();
        var candidates = portfolio.CandidateUses;
        var decomposed = Select(portfolio, sites, candidates,
            quantum: 100_000, maxQuanta: 1, cap: 8);
        var reference = portfolio.SelectJointFreight(candidates,
            Prices(sites), Purchasing(), Congestion(), 100_000UL, 1, 8);
        Assert.Single(decomposed.Components);
        Assert.Equal(reference.Lots, decomposed.Lots);
        Assert.Equal(0, reference.FulfilledDemandScore.CompareTo(
            decomposed.FulfilledDemandScore));
        Assert.Equal(0, reference.TotalBuyerCommitment.CompareTo(
            decomposed.TotalBuyerCommitment));
        decomposed.RequireCurrent(f.Session);
        decomposed.VerifyIndependent();
    }

    [Fact]
    public void TwelveSyntheticIndependentCandidatesDoNotCreateFictitiousBuyerCoupling()
    {
        var isolated = Enumerable.Range(1, 12).Select(i =>
            new CanonicalPrimitiveFreightCouplingDescriptor(
                new CanonicalPrimitiveFreightCandidateUse(
                    Food, EconomicActivityKind.Agriculture, 101, 7,
                    (ulong)(100 + i), new EconomicPointId((ulong)(200 + i)),
                    new EconomicPointId((ulong)(300 + i)), 100_000),
                Array.Empty<ExperimentalSharedFreightCorridorKey>(),
                new ulong[] { (ulong)(400 + i) })).ToArray();
        var groups = CanonicalPrimitiveFreightCouplingPlanner.Partition(isolated);
        Assert.Equal(12, groups.Count);
        Assert.All(groups, g => Assert.Single(g));
        Assert.True(System.Numerics.BigInteger.Pow(9, 12) > 4096);
        // Synthetic descriptors validate ONLY the graph algorithm. The
        // actual selector must rebind routes to authenticated portfolio.
        var sharedBuyer = isolated[1] with {
            Candidate = isolated[1].Candidate with {
                Buyer = isolated[0].Candidate.Buyer
            }
        };
        var sharedRoad = isolated[2] with { AssetIds = isolated[0].AssetIds };
        var connected = isolated.ToArray();
        connected[1] = sharedBuyer;
        connected[2] = sharedRoad;
        var merged = CanonicalPrimitiveFreightCouplingPlanner.Partition(connected);
        Assert.Equal(10, merged.Count);
        Assert.Contains(merged, g => g.SequenceEqual(new[] { 0, 1, 2 }));
    }

    [Fact]
    public void ForgedCandidatesIncompletePricesAndLiveNetworkChangesFailClosed()
    {
        var (f, sites, portfolio, food, wood) = TwoDisjointRoads();
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        Assert.True(quantum > 0UL);
        var selected = Select(portfolio, sites, new[] { food, wood },
            quantum: quantum);
        var prices = Prices(sites);
        Assert.Throws<ArgumentException>(() =>
            portfolio.SelectIndependentFreightComponents(
                new[] { food with { } }, prices, Purchasing(),
                Congestion(), 10_000));
        Assert.Throws<ArgumentException>(() =>
            portfolio.SelectIndependentFreightComponents(
                new[] { food, food }, prices, Purchasing(),
                Congestion(), 10_000));
        Assert.Throws<ArgumentException>(() =>
            portfolio.SelectIndependentFreightComponents(
                new[] { food }, prices.Skip(1), Purchasing(),
                Congestion(), 10_000));
        Assert.Throws<ArgumentException>(() =>
            portfolio.SelectIndependentFreightComponents(
                new[] { food, wood }, prices, Purchasing(),
                Congestion(), quantum, 8, 8));
        var save = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() =>
            selected.RequireCurrent(restored));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() =>
            selected.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            selected.VerifyIndependent());
    }
}
