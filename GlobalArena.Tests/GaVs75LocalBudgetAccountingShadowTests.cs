using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs75LocalBudgetAccountingShadowTests
{
    private static (CanonicalPhysicalTacticalSession Session, CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveSourceReservationDiagnostics Source) Build(long price = 1)
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var region = portfolio.CalibrateRegionalPurchasing(
            GaVs70RegionalPhysicalPurchasingProductivityTests.References(sites),
            new FoodTransportCostFraction(1, 2),
            new FoodTransportCostFraction(4, 1));
        var prices = sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(price, 1))).ToArray();
        var source = portfolio.DiagnoseRegionalFreightAccess(
            portfolio.CandidateUses, prices,
            new ExperimentalAbstractPurchasingPolicy(
                "VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5000, 1),
                new FoodTransportCostFraction(1, 1)),
            new ExperimentalSharedFreightCongestionPolicy(1, 1_000_000),
            region, 100_000UL).DiagnoseLocalSupplyPotential()
                .DiagnoseSourceReservations();
        return (f.Session, sites, source);
    }

    private static CanonicalPrimitiveLocalUseProposal Local(
        CanonicalPrimitiveSiteDiscovery sites, bool maximal = false)
    {
        var site = sites.Sites.First(x => x.Supply.Any(s =>
            s.RawQuantity > 0UL && x.Demand.Any(d =>
                d.CommodityId == s.CommodityId && d.RawQuantity > 0UL)));
        var supply = site.Supply.First(x => x.RawQuantity > 0UL &&
            site.Demand.Any(d => d.CommodityId == x.CommodityId &&
                d.RawQuantity > 0UL));
        var demand = site.Demand.Single(x => x.CommodityId == supply.CommodityId);
        return new(site.Point, supply.CommodityId, supply.Activity,
            maximal ? Math.Min(demand.RawQuantity, supply.RawQuantity) : 1UL);
    }

    [Fact]
    public void EmptyLocalTrialPreservesExactFreightBudgetAndH()
    {
        var (_, sites, source) = Build();
        foreach (var scenario in new[] { source.Baseline, source.Regional })
        {
            var trial = source.PreviewJointPhysicalUse(
                scenario.ScenarioId, Array.Empty<CanonicalPrimitiveLocalUseProposal>());
            var report = trial.DiagnoseLocalBudgetHypotheses();
            Assert.Equal(0, report.Snapshot.TotalMarketPricedLocalQuote.CompareTo(
                new FoodTransportCostFraction(0, 1)));
            Assert.Equal(0, report.Snapshot.TotalMarketPricedCombinedCommitment
                .CompareTo(report.Snapshot.TotalSelectedFreightCommitment));
            Assert.Equal(0, report.Snapshot.SumFulfilledDemandFractions.CompareTo(
                source.RegionalFreightAccess.Comparison.Outcomes.Single(
                    x => x.Scenario.ScenarioId == scenario.ScenarioId)
                    .Selection.FulfilledDemandScore));
            Assert.Equal(0, report.Snapshot.BuyersWhoseMarketPricedCaseExceedsH);
            Assert.All(report.Snapshot.Buyers, x =>
            {
                Assert.True(x.MarketPricedCaseFitsH);
                Assert.True(x.NoLocalCashDebitCaseFitsH);
            });
            Assert.False(report.IsMarketAccountingSelected);
            Assert.False(report.IsFreeLocalUseAuthorized);
            Assert.False(report.IsCoverageScoreAnApprovedSocialPolicy);
            Assert.False(report.IsActualConsumption);
            Assert.False(report.CanAuthorizeRealSettlement);
            report.VerifyIndependent();
        }
        Assert.Equal(sites.Flows.DemandFlows.Count,
            source.PreviewJointPhysicalUse(source.Baseline.ScenarioId,
                Array.Empty<CanonicalPrimitiveLocalUseProposal>()).Buyers.Count);
        Assert.Empty(typeof(CanonicalPrimitiveLocalBudgetShadowDiagnostics)
            .GetConstructors());
    }

    [Fact]
    public void ExpensiveLocalUseShowsTwoCashHypothesesWithoutMarketAuthorization()
    {
        var (_, sites, source) = Build(price: 1_000_000);
        var proposal = Local(sites, maximal: true);
        foreach (var scenario in new[] { source.Baseline, source.Regional })
        {
            var physical = source.PreviewJointPhysicalUse(scenario.ScenarioId, [proposal]);
            var report = physical.DiagnoseLocalBudgetHypotheses();
            var buyer = Assert.Single(report.Snapshot.Buyers,
                x => x.Buyer == proposal.Point);
            var exactQuote = new FoodTransportCostFraction(
                new BigInteger(1_000_000) * proposal.RawQuantity,
                new BigInteger(EconomicTransferPolicy.FixedPointDenominator));
            Assert.Equal(0, buyer.LocalMarketPriceQuote.CompareTo(exactQuote));
            Assert.Equal(0, buyer.MarketPricedCombinedCommitment.CompareTo(
                new FoodTransportCostFraction(
                    buyer.AlreadySelectedFreightCommitment.Numerator * exactQuote.Denominator +
                    exactQuote.Numerator * buyer.AlreadySelectedFreightCommitment.Denominator,
                    buyer.AlreadySelectedFreightCommitment.Denominator * exactQuote.Denominator)));
            Assert.True(buyer.NoLocalCashDebitCaseFitsH);
            Assert.False(report.IsMarketAccountingSelected);
            Assert.False(report.IsFreeLocalUseAuthorized);
            Assert.True(report.Snapshot.SumFulfilledDemandFractions.Numerator > 0);
            report.VerifyIndependent();
        }
    }

    [Fact]
    public void MarketPriceCaseCanExceedHEvenWhenTrialIsPhysicallyValid()
    {
        var (_, sites, source) = Build(price: 1_000_000);
        var physical = source.PreviewJointPhysicalUse(
            source.Baseline.ScenarioId, [Local(sites, maximal: true)]);
        var report = physical.DiagnoseLocalBudgetHypotheses();
        Assert.True(physical.IsPhysicallyFeasible);
        Assert.False(physical.IsLocalPurchaseBudgetEvaluated);
        Assert.True(report.Snapshot.BuyersWhoseMarketPricedCaseExceedsH > 0);
        Assert.Contains(report.Snapshot.Buyers,
            x => !x.MarketPricedCaseFitsH && x.NoLocalCashDebitCaseFitsH);
        Assert.True(report.Snapshot.MeanFulfilledDemandFraction.CompareTo(
            report.Snapshot.MinimumFulfilledDemandFraction) >= 0);
        Assert.False(report.IsCoverageScoreAnApprovedSocialPolicy);
        report.VerifyIndependent();
    }

    [Fact]
    public void RestoredSessionInvalidatesBothInterpretations()
    {
        var (session, sites, source) = Build();
        var report = source.PreviewJointPhysicalUse(source.Baseline.ScenarioId,
            Array.Empty<CanonicalPrimitiveLocalUseProposal>())
            .DiagnoseLocalBudgetHypotheses();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(restored));
        report.VerifyIndependent();
    }
}
