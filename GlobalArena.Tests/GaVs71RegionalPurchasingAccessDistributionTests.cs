using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs71RegionalPurchasingAccessDistributionTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static ExperimentalAbstractPurchasingPolicy Purchasing(
        long units = 5000, long h = 1) => new(
        "VS56-PURCHASING", 1, new FoodTransportCostFraction(units, 1),
        new FoodTransportCostFraction(h, 1));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites, long price = 1) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(price, 1))).ToArray();

    private static CanonicalPrimitiveRegionalProductivityProjection Region(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites) =>
        portfolio.CalibrateRegionalPurchasing(
            GaVs70RegionalPhysicalPurchasingProductivityTests.References(sites),
            new FoodTransportCostFraction(1, 2),
            new FoodTransportCostFraction(4, 1));

    private static CanonicalPrimitiveRegionalAccessDistribution Diagnose(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveRegionalProductivityProjection region,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse>? uses = null,
        IEnumerable<ExperimentalBuyerCommodityPrice>? prices = null,
        ExperimentalAbstractPurchasingPolicy? policy = null,
        ulong quantum = 100_000UL, int quanta = 1, int configurations = 4096) =>
        portfolio.DiagnoseRegionalFreightAccess(
            uses ?? portfolio.CandidateUses, prices ?? Prices(sites),
            policy ?? Purchasing(), Congestion(), region,
            quantum, quanta, configurations);

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    [Fact]
    public void ReportsAllInhabitedBuyersAndEveryCommodityWithoutSummingIncompatibleUnits()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var before = f.Session.ExportSaveJson();
        var region = Region(portfolio, sites);
        var report = Diagnose(portfolio, sites, region);
        Assert.Equal(sites.Sites.Count, report.Baseline.Buyers.Count);
        Assert.Equal(sites.Sites.Count, report.Regional.Buyers.Count);
        Assert.Equal(sites.Flows.DemandFlows.Count,
            report.Baseline.Buyers.Sum(x => x.Commodities.Count));
        Assert.True(report.Baseline.Buyers.Count > 1);
        Assert.Equal(3, report.Comparison.Baseline.Selection.Lots.Count);
        Assert.Equal(0, report.Baseline.TotalProtectedH.CompareTo(
            report.Regional.TotalProtectedH));
        var totalCoverage = new FoodTransportCostFraction(0, 1);
        var count = 0;
        foreach (var resident in report.Regional.Buyers)
        {
            var site = sites.At(resident.Buyer)!;
            Assert.Equal(site.Tile, resident.Tile);
            Assert.Equal(site.ResidentWorkers, resident.ResidentWorkers);
            Assert.True(resident.GrossPurchasing.CompareTo(resident.ProtectedH) >= 0);
            Assert.Equal(0, resident.GrossPurchasing.CompareTo(
                Add(resident.ProtectedH, resident.SpendablePurchasing)));
            var buyerCoverage = new FoodTransportCostFraction(0, 1);
            foreach (var item in resident.Commodities)
            {
                Assert.Equal(item.PhysicalDemandRaw,
                    Assert.Single(site.Demand,
                        x => x.CommodityId == item.Commodity).RawQuantity);
                Assert.True(item.PlannedFreightRaw <= item.PhysicalDemandRaw);
                var expected = new FoodTransportCostFraction(
                    item.PlannedFreightRaw, item.PhysicalDemandRaw);
                Assert.Equal(0, expected.CompareTo(item.PlannedDemandCoverage));
                buyerCoverage = Add(buyerCoverage, expected);
                totalCoverage = Add(totalCoverage, expected);
                count++;
            }
            Assert.Equal(0, new FoodTransportCostFraction(
                buyerCoverage.Numerator,
                buyerCoverage.Denominator * resident.Commodities.Count)
                .CompareTo(resident.MeanCommodityFreightCoverage));
        }
        Assert.Equal(0, new FoodTransportCostFraction(
            totalCoverage.Numerator, totalCoverage.Denominator * count)
            .CompareTo(report.Regional.MeanBuyerCommodityFreightCoverage));
        Assert.Equal(0, report.Regional.TotalPlannedCommitment.CompareTo(
            report.Comparison.Outcomes.Single(x =>
                x.Scenario.ScenarioId == "regional-productivity")
                .Selection.TotalBuyerCommitment));
        Assert.Equal(before, f.Session.ExportSaveJson());
        Assert.False(report.CanAuthorizeRealSettlement);
        Assert.False(report.IsCommitted);
        report.VerifyIndependent();
        Assert.Empty(typeof(CanonicalPrimitiveRegionalAccessDistribution).GetConstructors());
    }

    [Fact]
    public void TwoDisjointBuiltRoadsHaveSeparateBuyerCommodityOffersAndNoInventedDeliveries()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var region = Region(portfolio, sites);
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        var report = Diagnose(portfolio, sites, region,
            new[] { wood, food }, quantum: quantum, quanta: 8,
            configurations: 10);
        Assert.Equal(2, report.Comparison.Baseline.Selection.IndependentComponentCount);
        Assert.Equal(81, report.Comparison.Baseline.Selection.PossibleGlobalConfigurations);
        Assert.Equal(18,
            report.Comparison.Baseline.Selection.EvaluatedComponentConfigurations);

        foreach (var scenario in new[] { report.Baseline, report.Regional })
        {
            var buyerA = Assert.Single(scenario.Buyers, x => x.Buyer == food.Buyer);
            var buyerB = Assert.Single(scenario.Buyers, x => x.Buyer == wood.Buyer);
            Assert.Equal(1, Assert.Single(buyerA.Commodities,
                x => x.Commodity == Food).AuthorizedFreightOffers);
            Assert.Equal(1, Assert.Single(buyerB.Commodities,
                x => x.Commodity == Wood).AuthorizedFreightOffers);
            Assert.Equal(0, Assert.Single(buyerA.Commodities,
                x => x.Commodity == Wood).AuthorizedFreightOffers);
            Assert.Equal(0, Assert.Single(buyerB.Commodities,
                x => x.Commodity == Food).AuthorizedFreightOffers);
            Assert.True(scenario.BuyerCommodityPairsWithoutAuthorizedFreight >= 4);
            Assert.True(scenario.BuyerCommodityPairsWithoutSelectedFreight >= 4);
            Assert.All(scenario.Buyers, x =>
                Assert.True(x.PlannedCommitment.CompareTo(x.SpendablePurchasing) <= 0));
        }
        Assert.Equal(0, report.Baseline.TotalProtectedH.CompareTo(
            report.Regional.TotalProtectedH));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
        report.VerifyIndependent();
    }

    [Fact]
    public void ZeroAffordableFreightIsStillACompleteBuyerLedgerWithZeroCoverage()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var region = Region(portfolio, sites);
        var report = Diagnose(portfolio, sites, region,
            prices: Prices(sites, 1_000_000));
        foreach (var scenario in new[] { report.Baseline, report.Regional })
        {
            Assert.All(scenario.Buyers, x => {
                Assert.True(x.SpendablePurchasing.Numerator >= 0);
                Assert.Equal(0, x.PlannedCommitment.Numerator);
                Assert.All(x.Commodities, y =>
                    Assert.Equal(0UL, y.PlannedFreightRaw));
            });
            Assert.Equal(0, scenario.MeanBuyerCommodityFreightCoverage.Numerator);
            Assert.Equal(sites.Sites.Count, scenario.Buyers.Count);
            Assert.Equal(sites.Flows.DemandFlows.Count,
                scenario.BuyerCommodityPairsWithoutSelectedFreight);
        }
        Assert.Empty(report.Comparison.Baseline.Selection.Lots);
        Assert.Null(report.Comparison.Baseline.Selection.Preview);
        report.VerifyIndependent();
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void ARegionChangesHypotheticalPurchasingNotResidentDemandOrH()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var region = Region(portfolio, sites);
        var report = Diagnose(portfolio, sites, region);
        Assert.Equal(
            report.Baseline.Buyers.Select(x => x.Buyer),
            report.Regional.Buyers.Select(x => x.Buyer));
        Assert.Equal(report.Baseline.Buyers.Select(x => x.ResidentWorkers),
            report.Regional.Buyers.Select(x => x.ResidentWorkers));
        foreach (var (uniform, calibrated) in
            report.Baseline.Buyers.Zip(report.Regional.Buyers))
        {
            Assert.Equal(0, uniform.ProtectedH.CompareTo(calibrated.ProtectedH));
            Assert.Equal(
                uniform.Commodities.Select(x => (x.Commodity, x.PhysicalDemandRaw,
                    x.AuthorizedFreightOffers)),
                calibrated.Commodities.Select(x => (x.Commodity, x.PhysicalDemandRaw,
                    x.AuthorizedFreightOffers)));
            var physical = Assert.Single(region.Buyers,
                x => x.Buyer == uniform.Buyer);
            Assert.Equal(0, calibrated.GrossPurchasing.CompareTo(
                new FoodTransportCostFraction(
                    uniform.GrossPurchasing.Numerator *
                    physical.PurchasingProductivityFactor.Numerator,
                    uniform.GrossPurchasing.Denominator *
                    physical.PurchasingProductivityFactor.Denominator)));
        }
        Assert.Equal(sites.ResidentWorkers,
            (ulong)report.Regional.Buyers.Sum(x => (long)x.ResidentWorkers));
        Assert.False(report.CanAuthorizeRealSettlement);
        report.RequireCurrent(f.Session);
    }

    [Fact]
    public void InputReorderingAndMutationCannotRewriteAuditedRegionalAccess()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var region = Region(portfolio, sites);
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        var prices = Prices(sites);
        var left = Diagnose(portfolio, sites, region,
            new[] { food, wood }, prices, quantum: quantum,
            quanta: 8, configurations: 10);
        var right = Diagnose(portfolio, sites, region,
            new[] { wood, food }, prices.Reverse(), quantum: quantum,
            quanta: 8, configurations: 10);
        Assert.Equal(left.Baseline.Buyers.Select(x => x.Buyer),
            right.Baseline.Buyers.Select(x => x.Buyer));
        Assert.Equal(0, left.Baseline.MeanBuyerCommodityFreightCoverage.CompareTo(
            right.Baseline.MeanBuyerCommodityFreightCoverage));
        Assert.Equal(0, left.Regional.TotalGrossPurchasing.CompareTo(
            right.Regional.TotalGrossPurchasing));
        prices[0] = new ExperimentalBuyerCommodityPrice(
            prices[0].Buyer, prices[0].Commodity,
            new FoodTransportCostFraction(1_000_000, 1));
        left.VerifyIndependent();
        right.VerifyIndependent();
        Assert.True(left.Baseline.Buyers.All(x => x.Commodities is not Array));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void ForeignCandidateIncompletePricesAndStaleSessionFailClosed()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var region = Region(portfolio, sites);
        Assert.Throws<ArgumentException>(() => Diagnose(
            portfolio, sites, region,
            uses: new[] { portfolio.CandidateUses[0] with { } }));
        Assert.Throws<ArgumentException>(() => Diagnose(
            portfolio, sites, region, prices: Prices(sites).Skip(1)));
        Assert.Throws<ArgumentException>(() => Diagnose(
            portfolio, sites, region, prices: Prices(sites).Concat(
                Prices(sites).Take(1))));
        Assert.Throws<ArgumentException>(() => Diagnose(
            portfolio, sites, region, policy:
                new ExperimentalAbstractPurchasingPolicy(
                    "FOREIGN", 1, new FoodTransportCostFraction(5000, 1),
                    new FoodTransportCostFraction(1, 1))));
        var valid = Diagnose(portfolio, sites, region);
        var saved = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => valid.RequireCurrent(restored));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => valid.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() => valid.VerifyIndependent());
    }
}
