using GlobalArena.Foundation.Circulation;

namespace GlobalArena.Foundation.Circulation.Tests;

public sealed class EdgeCongestionLaboratoryTests
{
    private const long S = EdgeCongestionLaboratory.Scale;

    private static readonly NaturalEdgeCalibration NaturalCalibration =
        NaturalEdgeReferenceProfiles.CandidateA;

    private static NaturalEdgeEvaluation Land() =>
        NaturalEdgeEvaluator.Evaluate(
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Land, 0, 0, 0),
            NaturalCalibration);

    private static NaturalEdgeEvaluation Coast() =>
        NaturalEdgeEvaluator.Evaluate(
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Water, 0, -900_000, 0),
            NaturalCalibration);

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void FreeFlowAndReferenceCapacityHaveStableMeaning(int exponent)
    {
        var natural = Land();
        var noLoad = EdgeCongestionLaboratory.Evaluate(natural, 0, exponent);
        Assert.Equal(S, noLoad.EffectiveImpedanceRaw);
        Assert.Equal(S, noLoad.CongestionMultiplierRaw);
        Assert.Equal(0, noLoad.OccupancyRaw);

        var atCapacity = EdgeCongestionLaboratory.Evaluate(natural, S, exponent);
        Assert.Equal(S, atCapacity.OccupancyRaw);
        Assert.Equal(2 * S, atCapacity.CongestionMultiplierRaw);
        Assert.Equal(2 * S, atCapacity.EffectiveImpedanceRaw);
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
    }

    [Theory]
    [InlineData(3, 1_125_000, 9_000_000)]
    [InlineData(4, 1_062_500, 17_000_000)]
    [InlineData(5, 1_031_250, 33_000_000)]
    public void CandidateExponentsAgreeOnReferenceButDifferBelowAndAbove(int exponent,
        long halfLoadCost, long doubleLoadCost)
    {
        var natural = Land();
        Assert.Equal(halfLoadCost,
            EdgeCongestionLaboratory.Evaluate(natural, S / 2, exponent).EffectiveImpedanceRaw);
        Assert.Equal(doubleLoadCost,
            EdgeCongestionLaboratory.Evaluate(natural, 2 * S, exponent).EffectiveImpedanceRaw);
    }

    [Fact]
    public void ExponentComparisonBecomesMoreConvexAboveReferenceAndSofterBelow()
    {
        var below = new long[3];
        var above = new long[3];
        for (int p = 3; p <= 5; p++)
        {
            below[p - 3] = EdgeCongestionLaboratory.Evaluate(Land(), S / 2, p).EffectiveImpedanceRaw;
            above[p - 3] = EdgeCongestionLaboratory.Evaluate(Land(), 2 * S, p).EffectiveImpedanceRaw;
        }
        Assert.True(below[0] > below[1] && below[1] > below[2]);
        Assert.True(above[0] < above[1] && above[1] < above[2]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CongestionRisesMonotonicallyAndDoesNotImposeHardCapacity(int exponent)
    {
        var natural = Land();
        long previous = 0;
        for (int tenth = 0; tenth <= 40; tenth++)
        {
            var sample = EdgeCongestionLaboratory.Evaluate(
                natural, tenth * S / 10, exponent);
            Assert.True(sample.EffectiveImpedanceRaw >= previous);
            Assert.True(sample.CongestionMultiplierRaw >= S);
            previous = sample.EffectiveImpedanceRaw;
        }

        var severelyLoaded = EdgeCongestionLaboratory.Evaluate(
            natural, 3 * S, exponent);
        Assert.True(severelyLoaded.EffectiveImpedanceRaw > 2 * S);
        Assert.Equal(3 * S, severelyLoaded.OccupancyRaw);
        Assert.Equal(S * (1 + (long)Math.Pow(3, exponent)),
            severelyLoaded.EffectiveImpedanceRaw);
    }

    [Fact]
    public void NaturalCoastIsASevereBottleneckButNotAnAbsoluteBlock()
    {
        var coast = Coast();
        Assert.Equal(350_000, coast.NaturalCapacityRateRaw);
        var open = EdgeCongestionLaboratory.Evaluate(coast, 0, 4);
        var congested = EdgeCongestionLaboratory.Evaluate(coast, S, 4);
        Assert.Equal(1_500_000, open.EffectiveImpedanceRaw);
        Assert.True(congested.EffectiveImpedanceRaw > 20 * S);
        Assert.Equal(EdgeMedium.Coast, coast.Medium);
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
    }

    [Fact]
    public void InvestmentImprovesCoastalCapacityAndRelievesCongestion()
    {
        var natural = Coast();
        long load = S;
        var before = EdgeCongestionLaboratory.Evaluate(natural, load, 4);

        var improved = EdgeInvestmentLaboratory.Evaluate(
            natural, incorporatedCapitalRaw: 3 * S, referenceCapitalRaw: S);
        Assert.Equal(3 * S, improved.ImprovementFactorRaw);
        Assert.Equal(1_050_000, improved.CapacityRateRaw);
        Assert.Equal(500_000, improved.FreeFlowImpedanceRaw);

        var after = EdgeCongestionLaboratory.Evaluate(improved, load, 4);
        Assert.Equal(before.LoadRateRaw, after.LoadRateRaw);
        Assert.True(after.OccupancyRaw < before.OccupancyRaw);
        Assert.True(after.EffectiveImpedanceRaw < before.EffectiveImpedanceRaw);
        Assert.True(after.EffectiveImpedanceRaw >= improved.FreeFlowImpedanceRaw);
        Assert.Equal(natural,
            Coast()); // no mutation of original physical data
    }

    [Fact]
    public void InvestmentZeroMatchesNaturalCongestionForEveryCandidateExponent()
    {
        var natural = Coast();
        var zero = EdgeInvestmentLaboratory.Evaluate(natural, 0, S);
        for (int p = 3; p <= 5; p++)
            Assert.Equal(EdgeCongestionLaboratory.Evaluate(natural, 300_000, p),
                         EdgeCongestionLaboratory.Evaluate(zero, 300_000, p));
    }

    [Fact]
    public void CommercialEquivalentFlowNormalizesCycleDuration()
    {
        long rateA = EdgeCongestionLaboratory.RatePerTurn(
            volumePerPeriodRaw: 6 * S, periodDurationTurnsRaw: 2 * S);
        long rateB = EdgeCongestionLaboratory.RatePerTurn(
            volumePerPeriodRaw: 12 * S, periodDurationTurnsRaw: 4 * S);
        Assert.Equal(3 * S, rateA);
        Assert.Equal(rateA, rateB);
        Assert.Equal(
            EdgeCongestionLaboratory.Evaluate(Land(), rateA, 4),
            EdgeCongestionLaboratory.Evaluate(Land(), rateB, 4));
    }

    [Fact]
    public void EquivalentLoadAggregatesWithoutPerCommodityWeightsOrPrices()
    {
        // Same normalized load, whether classified as one or several
        // economic products. This lab cannot invent individual coefficients.
        long woodRate = 400_000;
        long foodRate = 600_000;
        long combined = checked(woodRate + foodRate);
        var together = EdgeCongestionLaboratory.Evaluate(Land(), combined, 4);
        var notDecomposed = EdgeCongestionLaboratory.Evaluate(Land(), S, 4);
        Assert.Equal(together, notDecomposed);
    }

    [Fact]
    public void CongestionDoesNotChangeWorkerTravelOrDistance()
    {
        var edge = new NaturalLogicalEdge(NodeMedium.Land,
            NodeMedium.Land, 0, 350_000, 100_000);
        var natural = NaturalEdgeEvaluator.Evaluate(edge, NaturalCalibration);
        var workerTime = WorkerEdgeTravel.RequiredTimeRaw(edge, S,
            WorkerTravelReferenceProfiles.CandidateB);
        var idle = EdgeCongestionLaboratory.Evaluate(natural, 0, 5);
        var loaded = EdgeCongestionLaboratory.Evaluate(natural, 3 * S, 5);
        Assert.True(loaded.EffectiveImpedanceRaw > idle.EffectiveImpedanceRaw);
        Assert.Equal(workerTime, WorkerEdgeTravel.RequiredTimeRaw(
            edge, S, WorkerTravelReferenceProfiles.CandidateB));
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
    }

    [Fact]
    public void InvalidRatesExponentsAndUnrepresentableOverloadFailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.Evaluate(0, S, S, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.Evaluate(S, 0, S, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.Evaluate(S, S, -1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.Evaluate(S, S, S, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.Evaluate(S, S, S, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.RatePerTurn(-1, S));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeCongestionLaboratory.RatePerTurn(S, 0));
        Assert.Throws<OverflowException>(() =>
            EdgeCongestionLaboratory.Evaluate(S, 1, long.MaxValue, 5));
    }

    [Fact]
    public void CrossPlatformFixedPointChecksumIsFrozen()
    {
        long checksum = 0;
        for (int i = 0; i < 4_096; i++)
        {
            long c = 600_000 + (i % 19) * 90_000L;
            long k = 350_000 + (i % 11) * 170_000L;
            long load = i * 53_271L % 3_500_001L;
            int p = 3 + i % 3;
            var result = EdgeCongestionLaboratory.Evaluate(c, k, load, p);
            checksum = checked(checksum +
                result.EffectiveImpedanceRaw * 7
                + result.CongestionMultiplierRaw * 3
                + result.OccupancyRaw);
        }
        Assert.Equal(41_521_891_305_303L, checksum);
    }
}
