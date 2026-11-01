using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs89FoodPriceCalibrationMatrixTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static CanonicalPrimitiveSiteDiscoveryPolicy Policy() => new(
        new EconomicPrimitiveCommodityMapping(
            new CommodityId(7), new CommodityId(8), new CommodityId(9)),
        new[] {
            new CanonicalPrimitiveDemandRate(new CommodityId(7),
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
            new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalExecutedFoodPriceTrendBridge Trend,
        CanonicalFoodPriceCalibrationMatrix Matrix) Build(
            int[]? steps = null)
    {
        var (f, _, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var session = f.Session;
        // A fresh completed cash-food cycle must follow the fixture's
        // earlier reassign action before any VS85 source quote is frozen.
        Assert.NotNull(session.AdvanceMovementTurn());
        var sites = session.DiscoverPrimitiveEconomicSites(Policy());
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var policies = sites.Flows.SupplyFlows.Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct().Select(x =>
                new CanonicalWorkforceActivityRealizationPolicy(x, 500, 100));
        var old = CanonicalWorkforceRealizationProjection.FromSites(
            session, sites, freight, policies,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var response = CanonicalLaggedProductionResponse.FromFrozenProjection(
            session, sites, freight, old, 100,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var receipt = CanonicalCycleStampedPriceReceipt.Capture(
            session, response);
        Assert.NotNull(session.AdvanceMovementTurn());
        var next = session.DiscoverPrimitiveEconomicSites(Policy());
        var nextFreight = CanonicalSimultaneousFreightReference.FromSites(
            session, next, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var actual = receipt.ApplyAfterNextSettledCycle(
            session, next, nextFreight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var witness = CanonicalExecutedLocalFoodPriceWitness
            .FromLatestRealSettlement(session);
        var trend = CanonicalExecutedFoodPriceTrendBridge.FromConsecutiveSettlements(
            session, actual, witness, 100);
        var matrix = CanonicalFoodPriceCalibrationMatrix.FromVerifiedSignals(
            session, trend, steps ?? new[] { 25, 50, 100, 150, 250 });
        return (f, trend, matrix);
    }

    [Fact]
    public void VerifiedRealFoodAndHypotheticalSignalsAreNeverConfusedForRealTransactions()
    {
        var (f, trend, matrix) = Build();
        Assert.True(matrix.ComparesOnlyDirectionsNotPriceUnits);
        Assert.True(matrix.HasComparableExecutedFoodPrice);
        Assert.False(matrix.ProvesEconomicCausalityOrPredictionAccuracy);
        Assert.False(matrix.SelectsOptimalGainAutomatically);
        Assert.False(matrix.TreatsVs84IndexAsRealPayment);
        Assert.False(matrix.ClaimsNonFoodExecutedPrices);
        Assert.False(matrix.ModifiesWorkerProductionOrWorldSave);
        Assert.False(matrix.CreatesCashInventoryOrCredit);
        Assert.False(matrix.AuthorizesSettlement);
        Assert.Equal(trend.SourceCycleMovementTurn,
            matrix.SourceCycleMovementTurn);
        Assert.Equal(trend.TargetCycleMovementTurn,
            matrix.TargetCycleMovementTurn);
        Assert.Equal(new[] { 25, 50, 100, 150, 250 },
            matrix.CandidateStepPermilles);
        Assert.Equal(matrix.CandidateStepPermilles.Count,
            matrix.Candidates.Count);
        Assert.Equal(f.Session.State.Cycles[^1].MovementTurn,
            matrix.TargetCycleMovementTurn);
        matrix.VerifyIndependent();
    }

    [Fact]
    public void EachCandidateIsDerivedFromExactIntegerFractionsWithoutCrossingPriceUnits()
    {
        var (f, trend, matrix) = Build();
        var price = new CanonicalEndogenousPriceIndex(
            matrix.FrozenHypotheticalIndexNumerator,
            matrix.FrozenHypotheticalIndexDenominator);
        var source = trend.MatchedTiles.Aggregate(
            BigInteger.Zero, (sum, x) =>
                sum + (BigInteger)x.CommonObservedFoodRaw *
                    x.SourcePriceMicrocreditsPerWholeUnit);
        var target = trend.MatchedTiles.Aggregate(
            BigInteger.Zero, (sum, x) =>
                sum + (BigInteger)x.CommonObservedFoodRaw *
                    x.TargetPriceMicrocreditsPerWholeUnit);
        Assert.True(source > 0 && target > 0);
        Assert.Equal(source, matrix.SourceMatchedWeightedNominalPriceRaw);
        Assert.Equal(target, matrix.TargetMatchedWeightedNominalPriceRaw);
        foreach (var row in matrix.Candidates)
        {
            var hypothetical =
                CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                    price, row.CandidateStepPermille);
            var executed = checked((int)((target - source) *
                row.CandidateStepPermille / (target + source)));
            Assert.Equal(hypothetical,
                row.Vs84HypotheticalAgricultureDeltaPermille);
            Assert.Equal(executed,
                row.Vs88ExecutedFoodPriceTrendDeltaPermille);
            Assert.Equal(Math.Abs(hypothetical - executed),
                row.AbsoluteAdvisoryDeltaGapPermille);
            Assert.Equal(CanonicalFoodPriceCalibrationMatrix.ClassifyDirections(
                    true, hypothetical, executed), row.Direction);
            Assert.InRange(hypothetical, -row.CandidateStepPermille,
                row.CandidateStepPermille);
            Assert.InRange(executed, -row.CandidateStepPermille,
                row.CandidateStepPermille);
        }
        matrix.VerifyIndependent();
    }

    [Fact]
    public void CoverageCountsOnlyPhysicalMatchedFoodAndNeverClaimsCompleteMarketWithoutProof()
    {
        var (f, trend, matrix) = Build();
        var origin = f.Session.State.FoodCash!.Receipts
            .Where(x => x.MovementTurn == matrix.SourceCycleMovementTurn)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.FoodRaw);
        var destination = f.Session.State.FoodCash.Receipts
            .Where(x => x.MovementTurn == matrix.TargetCycleMovementTurn)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.FoodRaw);
        var paired = trend.MatchedTiles.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.CommonObservedFoodRaw);
        Assert.Equal(origin, matrix.SourceExecutedFoodRaw);
        Assert.Equal(destination, matrix.TargetExecutedFoodRaw);
        Assert.Equal(paired, matrix.MatchedCommonFoodRaw);
        Assert.InRange(paired, BigInteger.One, origin);
        Assert.True(paired <= destination);
        Assert.Equal(paired == origin && paired == destination,
            matrix.CoverageIsComplete);
    }

    [Theory]
    [InlineData(false, 10, -20,
        CanonicalFoodCalibrationDirection.IndeterminateNoComparableSales)]
    [InlineData(true, 0, 0,
        CanonicalFoodCalibrationDirection.IndeterminateBothZero)]
    [InlineData(true, 0, -8,
        CanonicalFoodCalibrationDirection.IndeterminateHypotheticalZero)]
    [InlineData(true, -8, 0,
        CanonicalFoodCalibrationDirection.IndeterminateExecutedZero)]
    [InlineData(true, -9, -1, CanonicalFoodCalibrationDirection.Concordant)]
    [InlineData(true, 2, 14, CanonicalFoodCalibrationDirection.Concordant)]
    [InlineData(true, -9, 4, CanonicalFoodCalibrationDirection.Opposed)]
    [InlineData(true, 9, -4, CanonicalFoodCalibrationDirection.Opposed)]
    public void DirectionAgreementNeverOverclaimsCausality(
        bool comparable, int hypo, int executed,
        CanonicalFoodCalibrationDirection expected)
    {
        Assert.Equal(expected,
            CanonicalFoodPriceCalibrationMatrix.ClassifyDirections(
                comparable, hypo, executed));
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 251 })]
    [InlineData(new[] { 20, 20 })]
    [InlineData(new[] { 20, 5 })]
    [InlineData(new[] { -1, 10 })]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
    public void NoncanonicalGainGridIsRejected(int[] steps)
    {
        var (f, trend, _) = Build();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalFoodPriceCalibrationMatrix.FromVerifiedSignals(
                f.Session, trend, steps));
    }

    [Fact]
    public void AnotherSessionCannotClaimTheExistingHistoricalCalibration()
    {
        var (f, trend, matrix) = Build();
        var restore = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            matrix.RequireCurrent(restore));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalFoodPriceCalibrationMatrix.FromVerifiedSignals(
                restore, trend, new[] { 25, 50, 100 }));
    }

    [Fact]
    public void NextRealMovementInvalidatesOldMatrixAndPriceProof()
    {
        var (f, trend, matrix) = Build();
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            matrix.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            matrix.VerifyIndependent());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalFoodPriceCalibrationMatrix.FromVerifiedSignals(
                f.Session, trend, new[] { 25, 50, 100 }));
    }

    [Fact]
    public void RepeatedIndependentCalibrationDoesNotMutateCanonicalSave()
    {
        var (f, trend, matrix) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var replay = CanonicalFoodPriceCalibrationMatrix.FromVerifiedSignals(
                f.Session, trend, new[] { 25, 50, 100, 150, 250 });
            Assert.Equal(matrix.Candidates, replay.Candidates);
            Assert.Equal(matrix.MatchedCommonFoodRaw,
                replay.MatchedCommonFoodRaw);
            replay.VerifyIndependent();
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
