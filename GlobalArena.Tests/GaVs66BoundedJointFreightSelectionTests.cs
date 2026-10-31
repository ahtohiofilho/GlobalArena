using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs66BoundedJointFreightSelectionTests
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

    private static CanonicalEligibleFreightDiscoveryPolicy FreightPolicy(
        CommodityId commodity, EconomicActivityKind activity) => new(
        commodity, activity, new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
        ExperimentalLandInfrastructureKind.StoneRoad, Catalog());

    private static ExperimentalAbstractPurchasingPolicy Purchasing(
        long grossPerResident = 5000) => new(
        "VS56-PURCHASING", 1,
        new FoodTransportCostFraction(grossPerResident, 1),
        new FoodTransportCostFraction(5, 8));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Setup(
            bool secondRight = false)
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var src = CanonicalPrimitiveSiteDiscovery.SiteId(
            f.Anchors[f.Flows.SupplyFlows.Single().EconomicPointId]);
        var dst = CanonicalPrimitiveSiteDiscovery.SiteId(
            f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId]);
        var right = f.Registry().Entitlements.Single() with {
            Source = src, Destination = dst, ServiceId = 77
        };
        f.Session.TransportNetwork.SetEntitlement(right);
        if (secondRight)
            f.Session.TransportNetwork.SetEntitlement(right with { ServiceId = 78 });
        f.Session.ReassignWorkers(f.Session.InitialTile,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var portfolio = f.Session.DiscoverPrimitiveFreightPortfolio(
            sites, new[] {
                FreightPolicy(Food, EconomicActivityKind.Agriculture),
                FreightPolicy(Wood, EconomicActivityKind.Forestry),
                FreightPolicy(Stone, EconomicActivityKind.Mining)
            }, "VS56-PURCHASING", 1,
            "VS56-CALIBRATION", 1, "GA-VS56-NOMINAL-LAB");
        return (f, sites, portfolio);
    }

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites, long wholePrice = 1) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(wholePrice, 1))).ToArray();

    private static CanonicalPrimitiveJointFreightSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        long wholePrice = 1, long grossPerResident = 5000,
        ulong quantum = 100_000, int maxQuanta = 1,
        int maxConfigurations = 4096,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse>? candidates = null) =>
        portfolio.SelectJointFreight(candidates ?? portfolio.CandidateUses,
            Prices(sites, wholePrice), Purchasing(grossPerResident),
            Congestion(), quantum, maxQuanta, maxConfigurations);

    [Fact]
    public void JointSelectionUsesThreeRealProductsAndAuditsOneSharedRoadAndBudget()
    {
        var (f, sites, portfolio) = Setup();
        var before = f.Session.ExportSaveJson();
        var result = Select(portfolio, sites);
        Assert.Equal(new[] { Food, Wood, Stone },
            result.Lots.Select(x => x.Candidate.Commodity));
        Assert.Equal(3, result.Lots.Count);
        Assert.Equal(8, result.EvaluatedConfigurations);
        Assert.Equal(8, result.BudgetFeasibleConfigurations);
        Assert.Equal(8, result.PossibleConfigurations);
        Assert.All(result.Lots, x => Assert.Equal(100_000UL, x.RawQuantity));
        Assert.True(result.FulfilledDemandScore.Numerator > 0);
        Assert.True(result.TotalBuyerCommitment.Numerator > 0);
        Assert.True(result.Preview!.IsBudgetFeasible);
        Assert.Single(result.Preview.Freight.CorridorLoads);
        Assert.Equal(0, result.Preview.Freight.TotalCost.CompareTo(
            result.Preview.Buyers.TotalBuyerFreight));
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.IsCommitted);
        Assert.Equal(before, f.Session.ExportSaveJson());
        result.RequireCurrent(f.Session);
        result.VerifyIndependent();
        Assert.Empty(typeof(CanonicalPrimitiveJointFreightSelection).GetConstructors());
    }

    [Fact]
    public void InsufficientSharedPurchasingPowerSelectsTheZeroLotBaselineWithoutCredit()
    {
        var (f, sites, portfolio) = Setup();
        var before = f.Session.ExportSaveJson();
        var result = Select(portfolio, sites,
            wholePrice: 1_000_000, grossPerResident: 5);
        Assert.Empty(result.Lots);
        Assert.Null(result.Preview);
        Assert.Equal(8, result.EvaluatedConfigurations);
        Assert.Equal(1, result.BudgetFeasibleConfigurations);
        Assert.Equal(0, result.FulfilledDemandScore.Numerator);
        Assert.Equal(0, result.TotalBuyerCommitment.Numerator);
        Assert.Equal(before, f.Session.ExportSaveJson());
        result.VerifyIndependent();
    }

    [Fact]
    public void CompetingLicensedServicesCannotDuplicateOnePhysicalFoodFlow()
    {
        var (f, sites, portfolio) = Setup(secondRight: true);
        var food = portfolio.CandidateUses
            .Where(x => x.Commodity == Food).Reverse().ToArray();
        Assert.Equal(2, food.Length);
        var result = Select(portfolio, sites, candidates: food,
            quantum: 100_000, maxQuanta: 1);
        Assert.Single(result.Lots);
        Assert.Equal(77UL, result.Lots[0].Candidate.ServiceId);
        Assert.Equal(4, result.EvaluatedConfigurations);
        Assert.Equal(3, result.BudgetFeasibleConfigurations);
        result.VerifyIndependent();
        result.RequireCurrent(f.Session);
    }

    [Fact]
    public void QuantizedSelectionRespectsExplicitFiniteDomainAndCanonicalOrdering()
    {
        var (f, sites, portfolio) = Setup();
        var forward = Select(portfolio, sites, quantum: 25_000, maxQuanta: 2);
        var reverse = Select(portfolio, sites, quantum: 25_000, maxQuanta: 2,
            candidates: portfolio.CandidateUses.Reverse());
        Assert.Equal(27, forward.EvaluatedConfigurations);
        Assert.Equal(27, forward.PossibleConfigurations);
        Assert.Equal(3, forward.Lots.Count);
        Assert.Equal(forward.Lots, reverse.Lots);
        Assert.Equal(0, forward.FulfilledDemandScore.CompareTo(reverse.FulfilledDemandScore));
        Assert.Equal(0, forward.TotalBuyerCommitment.CompareTo(reverse.TotalBuyerCommitment));
        Assert.All(forward.Lots, x => Assert.Equal(50_000UL, x.RawQuantity));
        Assert.Throws<ArgumentException>(() =>
            Select(portfolio, sites, quantum: 25_000, maxQuanta: 2,
                maxConfigurations: 26));
        forward.VerifyIndependent();
        Assert.False(forward.CanAuthorizeRealSettlement);
        forward.RequireCurrent(f.Session);
    }

    [Fact]
    public void InvalidDomainForgedRightsPricesAndPolicyAreRejectedBeforeSearch()
    {
        var (f, sites, portfolio) = Setup();
        var first = portfolio.CandidateUses[0];
        var posted = Prices(sites);
        Assert.Throws<ArgumentException>(() => portfolio.SelectJointFreight(
            [first, first], posted, Purchasing(), Congestion(), 100_000));
        Assert.Throws<ArgumentException>(() => portfolio.SelectJointFreight(
            [first with { }], posted, Purchasing(), Congestion(), 100_000));
        Assert.Throws<ArgumentException>(() => portfolio.SelectJointFreight(
            [first], posted.Skip(1), Purchasing(), Congestion(), 100_000));
        Assert.Throws<ArgumentException>(() => portfolio.SelectJointFreight(
            [first], posted, new ExperimentalAbstractPurchasingPolicy(
                "FOREIGN", 1, new FoodTransportCostFraction(5000, 1),
                new FoodTransportCostFraction(5, 8)), Congestion(), 100_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.SelectJointFreight(
            [first], posted, Purchasing(), Congestion(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.SelectJointFreight(
            [first], posted, Purchasing(), Congestion(), 100_000, 9));
        Assert.Throws<ArgumentException>(() => portfolio.SelectJointFreight(
            Array.Empty<CanonicalPrimitiveFreightCandidateUse>(), posted,
            Purchasing(), Congestion(), 100_000));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void RestoredWorldAndChangedNetworkInvalidateSelectedCommercialBatch()
    {
        var (f, sites, portfolio) = Setup();
        var result = Select(portfolio, sites);
        var saved = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => result.RequireCurrent(restored));
        var newSites = restored.DiscoverPrimitiveEconomicSites(SitePolicy());
        var newPortfolio = restored.DiscoverPrimitiveFreightPortfolio(
            newSites, portfolio.Slices.Select(x => x.Policy),
            "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1,
            "GA-VS56-NOMINAL-LAB");
        var renewed = Select(newPortfolio, newSites);
        Assert.Equal(result.Lots.Select(x => (x.Candidate.Commodity, x.RawQuantity)),
            renewed.Lots.Select(x => (x.Candidate.Commodity, x.RawQuantity)));
        Assert.Equal(saved, restored.ExportSaveJson());
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => result.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() => result.VerifyIndependent());
        Assert.Throws<InvalidOperationException>(() =>
            Select(portfolio, sites));
    }
}
