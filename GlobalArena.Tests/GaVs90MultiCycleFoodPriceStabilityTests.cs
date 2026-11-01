using System.Numerics;
using GlobalArena.Simulation;
using GlobalArena.Runtime;

namespace GlobalArena.Tests;

public sealed class GaVs90MultiCycleFoodPriceStabilityTests
{
    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalFoodPriceCalibrationMatrix Matrix,
        CanonicalMultiCycleFoodPriceStabilityReport Report) Build(int count = 3)
    {
        var (f, _, matrix) =
            GaVs89FoodPriceCalibrationMatrixTests.Build();
        var report = CanonicalMultiCycleFoodPriceStabilityReport
            .FromVerifiedMatrix(f.Session, matrix, count);
        return (f, matrix, report);
    }

    [Fact]
    public void ThreeRealEconomicCyclesYieldTwoAuditableExecutedFoodWindows()
    {
        var (f, matrix, report) = Build();
        Assert.Equal(3, report.ObservedEconomicCycles);
        Assert.Equal(2, report.Windows.Count);
        Assert.Equal(5, report.GainSensitivities.Count);
        Assert.True(report.UsesActualCanonicalFoodCashHistory);
        Assert.False(report.ReconstructsHistoricalHypotheticalVs84Prices);
        Assert.False(report.ClaimsHistoricalPredictionAccuracy);
        Assert.False(report.ProvesCausality);
        Assert.False(report.AutomaticallyChoosesGain);
        Assert.False(report.ModifiesPricesProductionOrSave);
        Assert.False(report.CanAuthorizeActualSettlement);
        Assert.False(report.IntroducesStocksOrCredit);
        Assert.Equal(matrix.TargetCycleMovementTurn,
            report.Windows[^1].TargetCycleMovementTurn);
        Assert.Equal(matrix.SourceCycleMovementTurn,
            report.Windows[^1].SourceCycleMovementTurn);
        Assert.Equal(matrix.SourceExecutedFoodRaw,
            report.Windows[^1].SourceExecutedFoodRaw);
        Assert.Equal(matrix.TargetExecutedFoodRaw,
            report.Windows[^1].TargetExecutedFoodRaw);
        Assert.Equal(matrix.MatchedCommonFoodRaw,
            report.Windows[^1].CommonExecutedFoodRaw);
        Assert.Equal(matrix.SourceMatchedWeightedNominalPriceRaw,
            report.Windows[^1].SourceMatchedWeightedPriceRaw);
        Assert.Equal(matrix.TargetMatchedWeightedNominalPriceRaw,
            report.Windows[^1].TargetMatchedWeightedPriceRaw);
        Assert.Equal(report.Windows[^1].SourceCycleMovementTurn,
            report.Windows[^2].TargetCycleMovementTurn);
        report.VerifyIndependent();
    }

    [Fact]
    public void EveryPeriodRecalculatesPhysicalCohortAndPaidNominalPricesExactly()
    {
        var (f, matrix, report) = Build();
        var ledger = f.Session.State.FoodCash!.Receipts;
        var cycles = f.Session.State.Cycles;
        foreach (var window in report.Windows)
        {
            var before = ledger.Where(x =>
                x.MovementTurn == window.SourceCycleMovementTurn)
                .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
            var after = ledger.Where(x =>
                x.MovementTurn == window.TargetCycleMovementTurn)
                .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
            BigInteger left = 0, right = 0, common = 0;
            var matched = 0;
            foreach (var x in before)
            {
                if (!after.TryGetValue(x.Key, out var y) ||
                    x.Value.PaidMicrocredits == 0 ||
                    y.PaidMicrocredits == 0 ||
                    x.Value.PriceMicrocreditsPerUnit == 0 ||
                    y.PriceMicrocreditsPerUnit == 0)
                    continue;
                var w = Math.Min(x.Value.FoodRaw, y.FoodRaw);
                left += (BigInteger)w * x.Value.PriceMicrocreditsPerUnit;
                right += (BigInteger)w * y.PriceMicrocreditsPerUnit;
                common += w;
                matched++;
            }
            Assert.Equal(before.Values.Aggregate(BigInteger.Zero,
                (n, x) => n + x.FoodRaw), window.SourceExecutedFoodRaw);
            Assert.Equal(after.Values.Aggregate(BigInteger.Zero,
                (n, x) => n + x.FoodRaw), window.TargetExecutedFoodRaw);
            Assert.Equal(common, window.CommonExecutedFoodRaw);
            Assert.Equal(left, window.SourceMatchedWeightedPriceRaw);
            Assert.Equal(right, window.TargetMatchedWeightedPriceRaw);
            Assert.Equal(matched, window.MatchedTileCount);
            Assert.True(common <= window.SourceExecutedFoodRaw);
            Assert.True(common <= window.TargetExecutedFoodRaw);
            Assert.Equal(matched != 0 && common == window.SourceExecutedFoodRaw &&
                common == window.TargetExecutedFoodRaw,
                window.HasCompletePhysicalCoverage);
            Assert.Contains(cycles, c =>
                c.MovementTurn == window.SourceCycleMovementTurn);
            Assert.Contains(cycles, c =>
                c.MovementTurn == window.TargetCycleMovementTurn);
        }
        foreach (var row in report.GainSensitivities)
        {
            var signals = report.Windows.Select(x =>
                x.HasComparableExecutedSales
                    ? (int?)checked((int)((x.TargetMatchedWeightedPriceRaw -
                        x.SourceMatchedWeightedPriceRaw) *
                        row.CandidateStepPermille /
                        (x.TargetMatchedWeightedPriceRaw +
                        x.SourceMatchedWeightedPriceRaw)))
                    : null).ToArray();
            Assert.Equal(signals.Count(x => x.HasValue),
                row.ComparableWindowCount);
            Assert.Equal(signals.Count(x => x is { Value: not 0 }),
                row.NonzeroWindowCount);
            Assert.Equal(
                CanonicalMultiCycleFoodPriceStabilityReport
                    .CountNonzeroAdjacentReversals(signals),
                row.AdjacentNonzeroDirectionalReversals);
            Assert.Equal(
                signals.Where(x => x.HasValue).Select(x => Math.Abs(x!.Value))
                    .DefaultIfEmpty(0).Max(),
                row.MaximumAbsoluteAdvisoryDeltaPermille);
            Assert.Equal(
                matrix.Candidates.Single(x =>
                    x.CandidateStepPermille == row.CandidateStepPermille)
                    .Vs88ExecutedFoodPriceTrendDeltaPermille,
                row.LastExecutedAdvisoryDeltaPermille);
        }
    }

    [Theory]
    [InlineData(new int?[] { 2, -2, 4 }, 2)]
    [InlineData(new int?[] { 2, null, -1 }, 0)]
    [InlineData(new int?[] { 2, 0, -1 }, 0)]
    [InlineData(new int?[] { 0, 5, -7, 0, -1, 4 }, 2)]
    [InlineData(new int?[] { null, null, 0 }, 0)]
    [InlineData(new int?[] { -3, -4, -5 }, 0)]
    public void ReversalsAreOnlyAdjacentNonzeroActuallyComparableWindows(
        int?[] windowDeltas, int expected)
    {
        Assert.Equal(expected,
            CanonicalMultiCycleFoodPriceStabilityReport
                .CountNonzeroAdjacentReversals(windowDeltas));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    public void CannotInventHistoryOutsideBoundedActualEconomicCycleRange(
        int count)
    {
        var (f, matrix, _) = Build();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalMultiCycleFoodPriceStabilityReport.FromVerifiedMatrix(
                f.Session, matrix, count));
    }

    [Fact]
    public void LastPairMustRemainTheSameVs89SourceAndTargetWithoutPromotion()
    {
        var (f, matrix, report) = Build();
        var last = report.Windows[^1];
        Assert.True(last.HasComparableExecutedSales);
        Assert.Equal(matrix.Candidates.Select(x => x.CandidateStepPermille),
            report.GainSensitivities.Select(x => x.CandidateStepPermille));
        Assert.All(report.GainSensitivities, s =>
        {
            Assert.InRange(s.LastExecutedAdvisoryDeltaPermille,
                -s.CandidateStepPermille, s.CandidateStepPermille);
            Assert.InRange(s.MaximumAbsoluteAdvisoryDeltaPermille,
                0, s.CandidateStepPermille);
            Assert.InRange(s.AdjacentNonzeroDirectionalReversals, 0, 1);
        });
        Assert.Equal(report.ComparableWindowCount +
            report.MissingComparableWindowCount, report.Windows.Count);
    }

    [Fact]
    public void RestoredSaveRequiresANewCertifiedVs89MatrixNotPriorSessionAuthority()
    {
        var (f, matrix, report) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalMultiCycleFoodPriceStabilityReport.FromVerifiedMatrix(
                restored, matrix, 3));
    }

    [Fact]
    public void ActualNextMovementInvalidatesCachedStabilityDiagnostics()
    {
        var (f, matrix, report) = Build();
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            report.VerifyIndependent());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalMultiCycleFoodPriceStabilityReport.FromVerifiedMatrix(
                f.Session, matrix, 3));
    }

    [Fact]
    public void InstitutionalNetworkChangeInvalidatesEconomicSignalWindow()
    {
        var (f, matrix, report) = Build();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalMultiCycleFoodPriceStabilityReport.FromVerifiedMatrix(
                f.Session, matrix, 3));
    }

    [Fact]
    public void RepeatableMulticycleAuditNeverChangesWorldOrCashSave()
    {
        var (f, matrix, report) = Build();
        var hash = f.Session.CurrentHash;
        var save = f.Session.ExportSaveJson();
        for (var i = 0; i < 3; i++)
        {
            var other = CanonicalMultiCycleFoodPriceStabilityReport
                .FromVerifiedMatrix(f.Session, matrix, 3);
            Assert.Equal(report.Windows, other.Windows);
            Assert.Equal(report.GainSensitivities, other.GainSensitivities);
            other.VerifyIndependent();
            Assert.Equal(hash, f.Session.CurrentHash);
            Assert.Equal(save, f.Session.ExportSaveJson());
        }
    }
}
