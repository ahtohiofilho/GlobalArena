using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

internal static class GaVs99CalibratedTreasuryFixture
{
    internal sealed class Reviewer(bool accept) :
        ICanonicalAbstractCurrencyCalibrationReviewer
    {
        public int Calls { get; private set; }
        public bool AcceptReadOnlyCalibration(CanonicalAbstractCurrencyCalibration calibration)
        {
            Calls++;
            return accept;
        }
    }

    internal sealed record Data(
        GaVs98InstitutionalRevenueFixture.Data Source,
        CanonicalAbstractInstitutionalRevenuePreflight Abstract,
        CanonicalAbstractCurrencyCalibration Calibration);

    internal static Data Build(bool empty = false,
        CanonicalEndogenousCurveVariant curve =
            CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand)
    {
        var f = GaVs98InstitutionalRevenueFixture.Build(zero: empty);
        var abstract98 = GaVs98InstitutionalRevenueFixture.Preview(f);
        var calibration = new CanonicalAbstractCurrencyCalibration(
            "VS99-LAB-ONLY-CALIBRATION", 1, "GA-LAB-UNIT-NOT-RATIFIED",
            "VS99-LAB-EXPLICIT-REVIEW-ONLY",
            f.Physical.Fixture.Session.CurrentHash.HexDigest,
            abstract98.CompletedEconomicCycleMovementTurn, curve,
            new[] {
                new CanonicalCommodityMonetaryCalibration(new CommodityId(7), 1, 1),
                new CanonicalCommodityMonetaryCalibration(new CommodityId(8), 2, 1),
                new CanonicalCommodityMonetaryCalibration(new CommodityId(9), 3, 1)
            });
        return new(f, abstract98, calibration);
    }

    internal static CanonicalCalibratedStateRevenuePreflight Preview(
        Data d,
        CanonicalAbstractCurrencyCalibration? policy = null,
        Reviewer? reviewer = null,
        CanonicalAbstractInstitutionalRevenuePreflight? source = null) =>
        CanonicalCalibratedStateRevenuePreflight.FromReviewedCalibration(
            d.Source.Physical.Fixture.Session,
            source ?? d.Abstract, policy ?? d.Calibration,
            reviewer ?? new Reviewer(true));
}

public sealed class GaVs99AbstractCurrencyCalibrationTests
{
    private static CanonicalCalibratedMonetaryAmount Converted(
        CanonicalEndogenousPriceIndex index,
        CanonicalCommodityMonetaryCalibration factor) =>
        CanonicalCalibratedMonetaryAmount.Of(
            index.Numerator * factor.CurrencyNumeratorPerRelativeIndex,
            index.Denominator * factor.CurrencyDenominatorPerRelativeIndex);

    [Fact]
    public void ReviewedCurrencyPolicyProducesConservativePublicProposalWithoutBuyerAccount()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build();
        var save = f.Source.Physical.Fixture.Session.ExportSaveJson();
        var hash = f.Source.Physical.Fixture.Session.CurrentHash;
        var reviewer = new GaVs99CalibratedTreasuryFixture.Reviewer(true);
        var result = GaVs99CalibratedTreasuryFixture.Preview(f, reviewer: reviewer);
        Assert.Equal(1, reviewer.Calls);
        Assert.Equal(3, result.Commodities.Count);
        Assert.Equal(f.Abstract.Institutions.Count, result.Institutions.Count);
        Assert.True(result.HasExplicitCurrencyUnitAndFactors);
        Assert.True(result.UsesReviewedReadOnlyPolicy);
        Assert.True(result.IncomeOriginatesFromEconomicFunctions);
        Assert.False(result.RequiresPopulationOrBuyerCash);
        Assert.False(result.DebitsPopulationOrBuyerCash);
        Assert.False(result.RequiresInitialMoneySupply);
        Assert.False(result.IsActualTaxationOrExecutedCommerce);
        Assert.False(result.IsScaleRatifiedForRealSettlement);
        Assert.False(result.IssuesSpendableCurrency);
        Assert.False(result.PostsToStateTreasury);
        Assert.False(result.ModifiesLegacyFoodCashLedger);
        Assert.False(result.CreatesInventoryOrCredit);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.MutatesWorldOrSave);
        Assert.Equal(f.Calibration.CurrencyUnitId, result.CurrencyUnitId);

        var state = CanonicalCalibratedMonetaryAmount.Zero;
        var gross = CanonicalCalibratedMonetaryAmount.Zero;
        foreach (var commodity in result.Commodities)
        {
            var factor = f.Calibration.CommodityFactors.Single(x =>
                x.Commodity == commodity.Commodity);
            var from98 = f.Abstract.Commodities.Single(x =>
                x.Commodity == commodity.Commodity);
            Assert.Equal(Converted(from98.TotalGentleRelativeGrossIndex, factor),
                commodity.GrossNonpostedAmount);
            Assert.Equal(Converted(from98.PublicGentleRelativeGrossIndex, factor),
                commodity.PublicNonpostedAmount);
            var institutionTotal = result.Institutions
                .Where(x => x.Commodity == commodity.Commodity)
                .Aggregate(CanonicalCalibratedMonetaryAmount.Zero,
                    (sum, x) => CanonicalCalibratedMonetaryAmount.Add(
                        sum, x.ProposedNonpostedAmount));
            Assert.Equal(commodity.GrossNonpostedAmount, institutionTotal);
            state = CanonicalCalibratedMonetaryAmount.Add(
                state, commodity.PublicNonpostedAmount);
            gross = CanonicalCalibratedMonetaryAmount.Add(
                gross, commodity.GrossNonpostedAmount);
        }
        Assert.Equal(state, result.ProposedPublicNonpostedTotal);
        Assert.Equal(gross, result.ProposedAllInstitutionsNonpostedTotal);
        Assert.True(state.CompareTo(gross) < 0);
        result.VerifyIndependent();
        Assert.Equal(save, f.Source.Physical.Fixture.Session.ExportSaveJson());
        Assert.Equal(hash, f.Source.Physical.Fixture.Session.CurrentHash);
    }

    [Fact]
    public void DeclaredDifferentSensitivitiesChangeProposalsWithoutChangingPhysicalWorld()
    {
        var gentle = GaVs99CalibratedTreasuryFixture.Build();
        var baseline = GaVs99CalibratedTreasuryFixture.Preview(gentle);
        var steepPolicy = gentle.Calibration with {
            Curve = CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand
        };
        var alternative = GaVs99CalibratedTreasuryFixture.Preview(gentle,
            policy: steepPolicy);
        Assert.Equal(baseline.CurrencyUnitId, alternative.CurrencyUnitId);
        Assert.True(baseline.ProposedPublicNonpostedTotal.CompareTo(
            alternative.ProposedPublicNonpostedTotal) >= 0);
        Assert.NotEqual(baseline.ProposedPublicNonpostedTotal,
            alternative.ProposedPublicNonpostedTotal);
        Assert.False(alternative.PostsToStateTreasury);
        Assert.False(baseline.IssuesSpendableCurrency);
    }

    [Fact]
    public void NoOutputGivesExactZeroAndDoesNotAssumeTreasuryIssuance()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build(empty: true);
        var report = GaVs99CalibratedTreasuryFixture.Preview(f);
        Assert.Empty(report.Institutions);
        Assert.All(report.Commodities, x =>
        {
            Assert.Equal(CanonicalCalibratedMonetaryAmount.Zero,
                x.GrossNonpostedAmount);
            Assert.Equal(CanonicalCalibratedMonetaryAmount.Zero,
                x.PublicNonpostedAmount);
        });
        Assert.Equal(CanonicalCalibratedMonetaryAmount.Zero,
            report.ProposedPublicNonpostedTotal);
        report.VerifyIndependent();
    }

    [Fact]
    public void ExternalReviewerIsMandatoryAndRejectionDoesNotCreateAnyCash()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build();
        var snapshot = f.Source.Physical.Fixture.Session.ExportSaveJson();
        var denied = new GaVs99CalibratedTreasuryFixture.Reviewer(false);
        Assert.Throws<InvalidOperationException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f, reviewer: denied));
        Assert.Equal(1, denied.Calls);
        Assert.Equal(snapshot,
            f.Source.Physical.Fixture.Session.ExportSaveJson());
    }

    [Fact]
    public void MissingDuplicateNonpositiveFactorsAndUnapprovedDefaultsFailClosed()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build();
        var calibration = f.Calibration;
        var factors = calibration.CommodityFactors.ToArray();
        Assert.Throws<ArgumentException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f,
                policy: calibration with { CommodityFactors = factors.Take(2).ToArray() }));
        Assert.Throws<ArgumentException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f,
                policy: calibration with { CommodityFactors = new[] {
                    factors[0], factors[0], factors[2]
                } }));
        Assert.Throws<ArgumentException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f,
                policy: calibration with { CommodityFactors = new[] {
                    factors[0] with { CurrencyNumeratorPerRelativeIndex = 0 },
                    factors[1], factors[2]
                } }));
        Assert.Throws<ArgumentException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f,
                policy: calibration with { CommodityFactors = new[] {
                    factors[0], factors[1] with { CurrencyDenominatorPerRelativeIndex = -1 },
                    factors[2]
                } }));
        Assert.Throws<ArgumentException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f,
                policy: calibration with { CommodityFactors = new[] {
                    factors[0], factors[1],
                    factors[2] with { Commodity = new CommodityId(99) }
                } }));
    }

    [Fact]
    public void BadWorldUnitEvidenceVersionOrCurveCannotCreateTreasuryProposal()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build();
        var p = f.Calibration;
        var invalids = new[] {
            p with { CurrencyUnitId = "" },
            p with { PolicyId = " " },
            p with { EvidenceId = "" },
            p with { PolicyVersion = 0 },
            p with { SourceWorldHashHex = "FAKE-WORLD-HASH" },
            p with { CompletedEconomicCycleMovementTurn =
                p.CompletedEconomicCycleMovementTurn + 1 },
            p with { Curve = (CanonicalEndogenousCurveVariant)255 }
        };
        foreach (var invalid in invalids)
            Assert.Throws<ArgumentException>(() =>
                GaVs99CalibratedTreasuryFixture.Preview(f, policy: invalid));
    }

    [Fact]
    public void ExactRationalArithmeticPreservesVeryLargeResultsAndRejectsBadInputs()
    {
        var huge = BigInteger.Pow(10, 120);
        var a = CanonicalCalibratedMonetaryAmount.Of(huge, huge * 2);
        Assert.Equal(new CanonicalCalibratedMonetaryAmount(1, 2), a);
        Assert.Equal(CanonicalCalibratedMonetaryAmount.Zero,
            CanonicalCalibratedMonetaryAmount.Of(0, huge));
        Assert.Equal(new CanonicalCalibratedMonetaryAmount(1, 1),
            CanonicalCalibratedMonetaryAmount.Add(a, a));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalCalibratedMonetaryAmount.Of(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalCalibratedMonetaryAmount.Of(-1, 1));
        Assert.Throws<InvalidOperationException>(() =>
            new CanonicalCalibratedMonetaryAmount(1, 0).CompareTo(a));
    }

    [Fact]
    public void MixedEvidenceAndStaleNetworkTurnOrRestoredSessionFailClosed()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build();
        var report = GaVs99CalibratedTreasuryFixture.Preview(f);
        var session = f.Source.Physical.Fixture.Session;
        var save = session.ExportSaveJson();
        var restore = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(restore));

        var other = GaVs99CalibratedTreasuryFixture.Build();
        Assert.Throws<InvalidOperationException>(() =>
            GaVs99CalibratedTreasuryFixture.Preview(f,
                source: other.Abstract));

        session.TransportNetwork.SetAccess(
            f.Source.Physical.Fixture.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            report.VerifyIndependent());

        var next = GaVs99CalibratedTreasuryFixture.Build();
        var nextReport = GaVs99CalibratedTreasuryFixture.Preview(next);
        next.Source.Physical.Fixture.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            nextReport.RequireCurrent(next.Source.Physical.Fixture.Session));
    }

    [Fact]
    public void PolicyReorderingDoesNotChangeAmountsAndLegacyAccountsStayUntouched()
    {
        var f = GaVs99CalibratedTreasuryFixture.Build();
        var session = f.Source.Physical.Fixture.Session;
        var cash = session.FoodCash!.Accounts.ToArray();
        var supply = session.FoodCash.InitialMoneySupplyMicrocredits;
        var save = session.ExportSaveJson();
        var original = GaVs99CalibratedTreasuryFixture.Preview(f);
        var reversed = GaVs99CalibratedTreasuryFixture.Preview(f,
            policy: f.Calibration with {
                CommodityFactors = f.Calibration.CommodityFactors.Reverse().ToArray()
            });
        Assert.Equal(original.Commodities, reversed.Commodities);
        Assert.Equal(original.Institutions, reversed.Institutions);
        Assert.Equal(original.ProposedPublicNonpostedTotal,
            reversed.ProposedPublicNonpostedTotal);
        Assert.Equal(cash, session.FoodCash.Accounts);
        Assert.Equal(supply, session.FoodCash.InitialMoneySupplyMicrocredits);
        Assert.Equal(save, session.ExportSaveJson());
    }
}
