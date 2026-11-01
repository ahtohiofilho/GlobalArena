using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs68EconomicScenarioComparisonTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static FoodCommodityCatalog Catalog() => new(new[] {
        new FoodCommodityDefinition(Food, "food", 1),
        new FoodCommodityDefinition(Wood, "wood", 1),
        new FoodCommodityDefinition(Stone, "stone", 1)
    });

    private static CanonicalPrimitiveSiteDiscoveryPolicy SitesPolicy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] {
            new CanonicalPrimitiveDemandRate(Food,
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(Wood, 125_000),
            new CanonicalPrimitiveDemandRate(Stone, 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static CanonicalEligibleFreightDiscoveryPolicy Freight(
        CommodityId item, EconomicActivityKind activity) => new(
        item, activity,
        new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
        ExperimentalLandInfrastructureKind.StoneRoad, Catalog());

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites, long price = 1) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(price, 1))).ToArray();

    private static ExperimentalAbstractPurchasingPolicy Purchasing(
        long gross = 5000, long protectedInvisible = 0) => new(
        "VS56-PURCHASING", 1, new FoodTransportCostFraction(gross, 1),
        new FoodTransportCostFraction(protectedInvisible, 1));

    private static ExperimentalSharedFreightCongestionPolicy Congestion(
        BigInteger weight) => new(weight, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Setup()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var from = f.Session.InitialTile;
        var to = f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId];
        f.Session.TransportNetwork.SetEntitlement(
            f.Registry().Entitlements.Single() with {
                Source = CanonicalPrimitiveSiteDiscovery.SiteId(from),
                Destination = CanonicalPrimitiveSiteDiscovery.SiteId(to),
                ServiceId = 77
            });
        f.Session.ReassignWorkers(from,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitesPolicy());
        var portfolio = f.Session.DiscoverPrimitiveFreightPortfolio(
            sites, new[] {
                Freight(Food, EconomicActivityKind.Agriculture),
                Freight(Wood, EconomicActivityKind.Forestry),
                Freight(Stone, EconomicActivityKind.Mining)
            }, "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1,
            "GA-VS56-NOMINAL-LAB");
        return (f, sites, portfolio);
    }

    private static CanonicalPrimitiveFreightScenario[] Scenarios(
        CanonicalPrimitiveSiteDiscovery sites) => new[] {
        new CanonicalPrimitiveFreightScenario(
            "baseline", Prices(sites), Purchasing(5000, 1), Congestion(1)),
        new CanonicalPrimitiveFreightScenario(
            "high-prices", Prices(sites, 1_000_000),
            Purchasing(5000, 1), Congestion(1)),
        new CanonicalPrimitiveFreightScenario(
            "protected-H", Prices(sites), Purchasing(5000, 5000),
            Congestion(1)),
        new CanonicalPrimitiveFreightScenario(
            "congested", Prices(sites), Purchasing(5000, 1),
            Congestion(BigInteger.Pow(10, 20)))
        };

    [Fact]
    public void FourExplicitShadowsShareTheSamePhysicalSessionButNotBudgets()
    {
        var (f, sites, portfolio) = Setup();
        var before = f.Session.ExportSaveJson();
        var result = portfolio.CompareFreightScenarios(
            portfolio.CandidateUses.Reverse(), Scenarios(sites).Reverse(),
            "baseline", 100_000UL);
        Assert.Equal(4, result.Outcomes.Count);
        Assert.Equal(new[] { "baseline", "congested", "high-prices", "protected-H" },
            result.Outcomes.Select(x => x.Scenario.ScenarioId));
        Assert.Equal(3, result.Baseline.Selection.Lots.Count);
        Assert.True(result.Baseline.Selection.Preview!.IsBudgetFeasible);
        Assert.Single(result.Baseline.Selection.Preview.Freight.CorridorLoads);
        Assert.All(result.Outcomes, x => {
            Assert.True(x.Selection.PossibleGlobalConfigurations > 0);
            Assert.False(x.CanAuthorizeRealSettlement);
            Assert.False(x.IsCommitted);
        });
        Assert.Empty(result.Outcomes.Single(x => x.Scenario.ScenarioId == "high-prices")
            .Selection.Lots);
        Assert.Empty(result.Outcomes.Single(x => x.Scenario.ScenarioId == "protected-H")
            .Selection.Lots);
        Assert.Empty(result.Outcomes.Single(x => x.Scenario.ScenarioId == "congested")
            .Selection.Lots);
        Assert.All(result.Outcomes.Where(x => x.Scenario.ScenarioId != "baseline"),
            x => Assert.Equal(-1, x.FulfilledDemandDirectionVsBaseline));
        Assert.Equal(0, result.Baseline.FulfilledDemandDirectionVsBaseline);
        Assert.Equal(0, result.Baseline.BuyerCommitmentDirectionVsBaseline);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.IsCommitted);
        Assert.Equal(before, f.Session.ExportSaveJson());
        result.RequireCurrent(f.Session);
        result.VerifyRecomputed();
        Assert.Empty(typeof(CanonicalPrimitiveFreightScenarioComparison).GetConstructors());
    }

    [Fact]
    public void ReorderingScenariosAndCandidateOffersDoesNotChangeSelections()
    {
        var (f, sites, portfolio) = Setup();
        var original = Scenarios(sites);
        var left = portfolio.CompareFreightScenarios(
            portfolio.CandidateUses, original, "baseline", 100_000UL);
        var right = portfolio.CompareFreightScenarios(
            portfolio.CandidateUses.Reverse(), original.Reverse(),
            "baseline", 100_000UL);
        Assert.Equal(left.Outcomes.Select(x => x.Scenario.ScenarioId),
            right.Outcomes.Select(x => x.Scenario.ScenarioId));
        foreach (var (a, b) in left.Outcomes.Zip(right.Outcomes))
        {
            Assert.Equal(a.Selection.Lots, b.Selection.Lots);
            Assert.Equal(0, a.Selection.FulfilledDemandScore.CompareTo(
                b.Selection.FulfilledDemandScore));
            Assert.Equal(0, a.Selection.TotalBuyerCommitment.CompareTo(
                b.Selection.TotalBuyerCommitment));
        }
        left.VerifyRecomputed();
        right.VerifyRecomputed();
        left.RequireCurrent(f.Session);
    }

    [Fact]
    public void ScenarioPricesAreFrozenAgainstCallerMutationAndEachOneIsReaudited()
    {
        var (f, sites, portfolio) = Setup();
        var editablePrices = Prices(sites);
        var baseline = new CanonicalPrimitiveFreightScenario(
            "baseline", editablePrices, Purchasing(5000, 1), Congestion(1));
        var result = portfolio.CompareFreightScenarios(
            portfolio.CandidateUses, new[] { baseline }, "baseline", 100_000UL);
        var before = result.Baseline.Selection.Lots.ToArray();
        editablePrices[0] = new ExperimentalBuyerCommodityPrice(
            editablePrices[0].Buyer, editablePrices[0].Commodity,
            new FoodTransportCostFraction(1_000_000, 1));
        Assert.Equal(before, result.Baseline.Selection.Lots);
        Assert.NotSame(editablePrices, result.Baseline.Scenario.PostedPrices);
        result.VerifyRecomputed();
        Assert.False(result.CanAuthorizeRealSettlement);
        result.RequireCurrent(f.Session);
    }

    [Fact]
    public void DuplicateMissingForeignAndOversizedScenarioSetsFailClosed()
    {
        var (f, sites, portfolio) = Setup();
        var baseline = Scenarios(sites)[0];
        var extra = Enumerable.Range(0, 7).Select(i =>
            baseline with { ScenarioId = $"case-{i}" }).ToArray();
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            portfolio.CandidateUses, extra, "case-0", 100_000UL));
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            portfolio.CandidateUses, new[] { baseline, baseline },
            "baseline", 100_000UL));
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            portfolio.CandidateUses, new[] { baseline }, "missing", 100_000UL));
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            portfolio.CandidateUses,
            new[] { baseline with { PostedPrices = Prices(sites).Skip(1).ToArray() } },
            "baseline", 100_000UL));
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            portfolio.CandidateUses,
            new[] { baseline with { PurchasingPolicy =
                new ExperimentalAbstractPurchasingPolicy(
                    "FOREIGN", 1, new FoodTransportCostFraction(5000, 1),
                    new FoodTransportCostFraction(1, 1)) } },
            "baseline", 100_000UL));
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            new[] { portfolio.CandidateUses[0] with { } },
            new[] { baseline }, "baseline", 100_000UL));
        Assert.Throws<ArgumentException>(() => portfolio.CompareFreightScenarios(
            portfolio.CandidateUses,
            new[] { baseline }, "baseline", 100_000UL, 8, 10));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void TransportMutationAndRestoredSaveExpireAllHypotheticalOutcomes()
    {
        var (f, sites, portfolio) = Setup();
        var selected = portfolio.CompareFreightScenarios(
            portfolio.CandidateUses, Scenarios(sites), "baseline", 100_000UL);
        var save = f.Session.ExportSaveJson();
        var foreign = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() => selected.RequireCurrent(foreign));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => selected.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() => selected.VerifyRecomputed());
        Assert.Throws<InvalidOperationException>(() =>
            portfolio.CompareFreightScenarios(
                portfolio.CandidateUses, Scenarios(sites), "baseline", 100_000UL));
    }
}
