using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs73PhysicalSourceReservationDiagnosticsTests
{
    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites, long price = 1) =>
        sites.Flows.DemandFlows.Select(x => new ExperimentalBuyerCommodityPrice(
            x.EconomicPointId, x.CommodityId,
            new FoodTransportCostFraction(price, 1))).ToArray();

    private static CanonicalPrimitiveSourceReservationDiagnostics Diagnose(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse>? uses = null,
        long price = 1, ulong quantum = 100_000UL,
        int quanta = 1, int configurations = 4096)
    {
        var region = portfolio.CalibrateRegionalPurchasing(
            GaVs70RegionalPhysicalPurchasingProductivityTests.References(sites),
            new FoodTransportCostFraction(1, 2),
            new FoodTransportCostFraction(4, 1));
        return portfolio.DiagnoseRegionalFreightAccess(
            uses ?? portfolio.CandidateUses, Prices(sites, price),
            new ExperimentalAbstractPurchasingPolicy(
                "VS56-PURCHASING", 1, new FoodTransportCostFraction(5000, 1),
                new FoodTransportCostFraction(1, 1)),
            new ExperimentalSharedFreightCongestionPolicy(1, 1_000_000),
            region, quantum, quanta, configurations)
            .DiagnoseLocalSupplyPotential().DiagnoseSourceReservations();
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    [Fact]
    public void FreightAndConditionalLocalHeadroomConservePhysicalSourcePerCommodity()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var before = f.Session.ExportSaveJson();
        var report = Diagnose(portfolio, sites);
        Assert.Equal(sites.Flows.DemandFlows.Count, report.Baseline.Sources.Count);
        Assert.Contains(report.Baseline.Sources,
            x => x.SelectedOutboundFreightRaw > 0UL);

        foreach (var scenario in new[] { report.Baseline, report.Regional })
        {
            var outcome = Assert.Single(
                report.RegionalFreightAccess.Comparison.Outcomes,
                x => x.Scenario.ScenarioId == scenario.ScenarioId);
            var sumFractions = new FoodTransportCostFraction(0, 1);
            var exportedPairs = 0;
            var reducedPairs = 0;
            foreach (var row in scenario.Sources)
            {
                var site = sites.At(row.Source)!;
                var produced = site.Supply
                    .Where(x => x.CommodityId == row.Commodity)
                    .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));
                var demand = Assert.Single(site.Demand,
                    x => x.CommodityId == row.Commodity).RawQuantity;
                var outbound = outcome.Selection.Lots
                    .Where(x => x.Candidate.Source == row.Source &&
                        x.Candidate.Commodity == row.Commodity)
                    .Aggregate(0UL, (sum, lot) =>
                        checked(sum + lot.RawQuantity));
                var inbound = outcome.Selection.Lots
                    .Where(x => x.Candidate.Buyer == row.Source &&
                        x.Candidate.Commodity == row.Commodity)
                    .Aggregate(0UL, (sum, lot) =>
                        checked(sum + lot.RawQuantity));
                Assert.Equal(produced, row.PhysicalProductionRaw);
                Assert.Equal(demand, row.PhysicalLocalDemandRaw);
                Assert.Equal(outbound, row.SelectedOutboundFreightRaw);
                Assert.Equal(inbound, row.SelectedInboundFreightRaw);
                Assert.True(outbound <= produced);
                var remaining = produced - outbound;
                var local = Math.Min(demand, remaining);
                Assert.Equal(remaining, row.UnreservedPhysicalProductionRaw);
                Assert.Equal(local, row.MaximumLocalUseAfterOutboundRaw);
                Assert.True(local + outbound <= produced);
                var ratio = new FoodTransportCostFraction(local, demand);
                Assert.Equal(0, ratio.CompareTo(
                    row.MaximumLocalCoverageAfterOutbound));
                sumFractions = Add(sumFractions, ratio);
                if (outbound > 0UL) exportedPairs++;
                if (local < Math.Min(demand, produced)) reducedPairs++;
            }
            Assert.Equal(exportedPairs,
                scenario.SourceCommodityPairsWithSelectedOutbound);
            Assert.Equal(reducedPairs,
                scenario.SourceCommodityPairsWithLowerLocalPotentialThanVs72);
            Assert.Equal(0, new FoodTransportCostFraction(
                sumFractions.Numerator,
                sumFractions.Denominator * scenario.Sources.Count)
                .CompareTo(scenario.MeanConditionalLocalCoverage));
        }
        Assert.Equal(before, f.Session.ExportSaveJson());
        Assert.False(report.CanAuthorizeRealSettlement);
        Assert.False(report.IsCommitted);
        Assert.False(report.IsActualConsumption);
        Assert.False(report.IsPersistentReservation);
        report.VerifyIndependent();
        Assert.Empty(typeof(CanonicalPrimitiveSourceReservationDiagnostics)
            .GetConstructors());
    }

    [Fact]
    public void UnaffordableFreightPreservesFullVs72LocalPotential()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var report = Diagnose(portfolio, sites, price: 1_000_000);
        foreach (var scenario in new[] { report.Baseline, report.Regional })
        {
            Assert.Equal(0, scenario.SourceCommodityPairsWithSelectedOutbound);
            Assert.Equal(0,
                scenario.SourceCommodityPairsWithLowerLocalPotentialThanVs72);
            Assert.All(scenario.Sources, x =>
            {
                Assert.Equal(0UL, x.SelectedOutboundFreightRaw);
                Assert.Equal(0UL, x.SelectedInboundFreightRaw);
                Assert.Equal(x.PhysicalProductionRaw,
                    x.UnreservedPhysicalProductionRaw);
                Assert.Equal(Math.Min(x.PhysicalLocalDemandRaw,
                    x.PhysicalProductionRaw),
                    x.MaximumLocalUseAfterOutboundRaw);
            });
        }
        report.VerifyIndependent();
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void DisjointRoadsKeepEachSourceAndProductIndependent()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        var report = Diagnose(portfolio, sites,
            new[] { wood, food }, quantum: quantum, quanta: 8,
            configurations: 10);
        foreach (var scenario in new[] { report.Baseline, report.Regional })
        {
            Assert.Equal(sites.Flows.DemandFlows.Count, scenario.Sources.Count);
            Assert.Contains(scenario.Sources,
                x => x.SelectedOutboundFreightRaw > 0UL);
            Assert.All(scenario.Sources, x =>
                Assert.True(x.SelectedOutboundFreightRaw +
                    x.MaximumLocalUseAfterOutboundRaw <=
                    x.PhysicalProductionRaw));
        }
        report.VerifyIndependent();
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void ReorderingCandidateUsesDoesNotChangeSourceHeadroom()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var left = Diagnose(portfolio, sites);
        var right = Diagnose(portfolio, sites,
            portfolio.CandidateUses.Reverse());
        Assert.Equal(
            left.Baseline.Sources.Select(x => (
                x.Source, x.Commodity, x.SelectedOutboundFreightRaw,
                x.UnreservedPhysicalProductionRaw)),
            right.Baseline.Sources.Select(x => (
                x.Source, x.Commodity, x.SelectedOutboundFreightRaw,
                x.UnreservedPhysicalProductionRaw)));
        Assert.Equal(
            left.Regional.Sources.Select(x => (
                x.Source, x.Commodity, x.SelectedOutboundFreightRaw,
                x.UnreservedPhysicalProductionRaw)),
            right.Regional.Sources.Select(x => (
                x.Source, x.Commodity, x.SelectedOutboundFreightRaw,
                x.UnreservedPhysicalProductionRaw)));
        left.VerifyIndependent();
        right.VerifyIndependent();
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void StaleNetworkAndRestoredSaveInvalidateConditionalReservations()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var report = Diagnose(portfolio, sites);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(restored));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            report.VerifyIndependent());
    }
}
