using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs72LocalSupplyPotentialDiagnosticsTests
{
    private static ExperimentalAbstractPurchasingPolicy Purchasing() => new(
        "VS56-PURCHASING", 1, new FoodTransportCostFraction(5000, 1),
        new FoodTransportCostFraction(1, 1));

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites, long price = 1) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(price, 1))).ToArray();

    private static CanonicalPrimitiveLocalSupplyPotentialDiagnostics Diagnose(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse>? uses = null,
        long price = 1)
    {
        var region = portfolio.CalibrateRegionalPurchasing(
            GaVs70RegionalPhysicalPurchasingProductivityTests.References(sites),
            new FoodTransportCostFraction(1, 2),
            new FoodTransportCostFraction(4, 1));
        return portfolio.DiagnoseRegionalFreightAccess(
            uses ?? portfolio.CandidateUses, Prices(sites, price), Purchasing(),
            new ExperimentalSharedFreightCongestionPolicy(1, 1_000_000),
            region, 100_000UL).DiagnoseLocalSupplyPotential();
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    [Fact]
    public void AuditsPerCommodityLocalPhysicalOpportunityWithoutWorldMutation()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var before = fixture.Session.ExportSaveJson();
        var report = Diagnose(portfolio, sites);
        var snapshot = report.Snapshot;

        Assert.Equal(sites.Sites.Count, snapshot.Buyers.Count);
        Assert.Equal(sites.Flows.DemandFlows.Count,
            snapshot.Buyers.Sum(x => x.Commodities.Count));
        Assert.Contains(snapshot.Buyers.SelectMany(x => x.Commodities),
            x => x.PhysicalLocalProductionRaw > 0UL);
        var pairCount = 0;
        var zeroProduction = 0;
        var fullPotential = 0;
        var totalCoverage = new FoodTransportCostFraction(0, 1);
        foreach (var buyer in snapshot.Buyers)
        {
            var site = sites.At(buyer.Buyer)!;
            Assert.Equal(site.Tile, buyer.Tile);
            Assert.Equal(site.ResidentWorkers, buyer.ResidentWorkers);
            foreach (var product in buyer.Commodities)
            {
                var demand = Assert.Single(site.Demand,
                    x => x.CommodityId == product.Commodity);
                var production = site.Supply
                    .Where(x => x.CommodityId == product.Commodity)
                    .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));
                var localUpper = Math.Min(production, demand.RawQuantity);
                Assert.Equal(demand.RawQuantity, product.PhysicalDemandRaw);
                Assert.Equal(production, product.PhysicalLocalProductionRaw);
                Assert.Equal(localUpper, product.MaximumHypotheticalLocalUseRaw);
                Assert.Equal(0, new FoodTransportCostFraction(
                    localUpper, demand.RawQuantity)
                    .CompareTo(product.MaximumLocalCoverage));
                Assert.Equal(
                    Assert.Single(report.RegionalFreightAccess.Baseline.Buyers,
                        x => x.Buyer == buyer.Buyer).Commodities.Single(
                        x => x.Commodity == product.Commodity).PlannedFreightRaw,
                    product.BaselinePlannedFreightRaw);
                Assert.Equal(
                    Assert.Single(report.RegionalFreightAccess.Regional.Buyers,
                        x => x.Buyer == buyer.Buyer).Commodities.Single(
                        x => x.Commodity == product.Commodity).PlannedFreightRaw,
                    product.RegionalPlannedFreightRaw);
                if (production == 0) zeroProduction++;
                if (localUpper == demand.RawQuantity) fullPotential++;
                pairCount++;
                totalCoverage = Add(totalCoverage, product.MaximumLocalCoverage);
            }
        }
        Assert.Equal(zeroProduction,
            snapshot.BuyerCommodityPairsWithoutLocalProduction);
        Assert.Equal(fullPotential,
            snapshot.BuyerCommodityPairsWithFullLocalPotential);
        Assert.Equal(0, new FoodTransportCostFraction(
            totalCoverage.Numerator, totalCoverage.Denominator * pairCount)
            .CompareTo(snapshot.MeanMaximumLocalCoverage));
        Assert.Equal(before, fixture.Session.ExportSaveJson());
        Assert.False(report.CanAuthorizeRealSettlement);
        Assert.False(report.IsCommitted);
        Assert.False(report.IsActualConsumption);
        report.VerifyIndependent();
        Assert.Empty(typeof(CanonicalPrimitiveLocalSupplyPotentialDiagnostics)
            .GetConstructors());
    }

    [Fact]
    public void FreightAffordabilityDoesNotChangeLocalProductionOpportunity()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var affordable = Diagnose(portfolio, sites);
        var prohibitive = Diagnose(portfolio, sites, price: 1_000_000);
        Assert.Empty(prohibitive.RegionalFreightAccess.Comparison
            .Baseline.Selection.Lots);
        foreach (var (left, right) in affordable.Snapshot.Buyers
                     .Zip(prohibitive.Snapshot.Buyers))
        {
            Assert.Equal(left.Buyer, right.Buyer);
            Assert.Equal(left.ResidentWorkers, right.ResidentWorkers);
            foreach (var (a, b) in left.Commodities.Zip(right.Commodities))
            {
                Assert.Equal(a.PhysicalDemandRaw, b.PhysicalDemandRaw);
                Assert.Equal(a.PhysicalLocalProductionRaw,
                    b.PhysicalLocalProductionRaw);
                Assert.Equal(a.MaximumHypotheticalLocalUseRaw,
                    b.MaximumHypotheticalLocalUseRaw);
                Assert.Equal(0, a.MaximumLocalCoverage.CompareTo(
                    b.MaximumLocalCoverage));
                Assert.Equal(0UL, b.BaselinePlannedFreightRaw);
                Assert.Equal(0UL, b.RegionalPlannedFreightRaw);
            }
        }
        Assert.Equal(0, affordable.Snapshot.MeanMaximumLocalCoverage
            .CompareTo(prohibitive.Snapshot.MeanMaximumLocalCoverage));
        Assert.True(prohibitive.Snapshot.MeanMaximumLocalCoverage.Numerator > 0);
        prohibitive.VerifyIndependent();
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void TwoAuthorizedRoadsDoNotBecomeLocalProductionOrDoubleCountedConsumption()
    {
        var (fixture, sites, portfolio, _, _) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var report = Diagnose(portfolio, sites);
        var snapshot = report.Snapshot;
        Assert.Equal(sites.Sites.Count, snapshot.Buyers.Count);
        Assert.True(report.RegionalFreightAccess.Baseline.Buyers
            .SelectMany(x => x.Commodities).Any(
                x => x.AuthorizedFreightOffers > 0));
        Assert.All(snapshot.Buyers.SelectMany(x => x.Commodities), x =>
        {
            Assert.True(x.MaximumHypotheticalLocalUseRaw <=
                x.PhysicalDemandRaw);
            Assert.True(x.MaximumHypotheticalLocalUseRaw <=
                x.PhysicalLocalProductionRaw);
        });
        report.VerifyIndependent();
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void CandidateReorderingCannotChangeLocalPhysicalPotential()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var left = Diagnose(portfolio, sites);
        var right = Diagnose(portfolio, sites, portfolio.CandidateUses.Reverse());
        Assert.Equal(
            left.Snapshot.Buyers.SelectMany(x => x.Commodities)
                .Select(x => (x.Buyer, x.Commodity, x.PhysicalLocalProductionRaw,
                    x.MaximumHypotheticalLocalUseRaw)),
            right.Snapshot.Buyers.SelectMany(x => x.Commodities)
                .Select(x => (x.Buyer, x.Commodity, x.PhysicalLocalProductionRaw,
                    x.MaximumHypotheticalLocalUseRaw)));
        left.VerifyIndependent();
        right.VerifyIndependent();
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void RestoredOrChangedPhysicalSessionInvalidatesTheLocalReport()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var report = Diagnose(portfolio, sites);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(restored));
        fixture.Session.TransportNetwork.SetAccess(
            fixture.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(fixture.Session));
        Assert.Throws<InvalidOperationException>(() =>
            report.VerifyIndependent());
    }
}
