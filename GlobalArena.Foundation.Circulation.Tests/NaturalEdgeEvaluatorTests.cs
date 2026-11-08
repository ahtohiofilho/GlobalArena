using GlobalArena.Foundation.Circulation;

namespace GlobalArena.Foundation.Circulation.Tests;

public sealed class NaturalEdgeEvaluatorTests
{
    private static readonly NaturalEdgeCalibration Cal = NaturalEdgeReferenceProfiles.CandidateA;

    private static NaturalLogicalEdge Land(int roughness = 0, int fromElevation = 0, int toElevation = 0) =>
        new(NodeMedium.Land, NodeMedium.Land, fromElevation, toElevation, roughness);

    private static NaturalEdgeEvaluation Score(NaturalLogicalEdge edge) =>
        NaturalEdgeEvaluator.Evaluate(edge, Cal);

    [Fact]
    public void NeutralLandHasExactlyOneLogicalHopAndOneUnitOfImpedance()
    {
        var result = Score(Land());
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
        Assert.Equal(EdgeMedium.Land, result.Medium);
        Assert.Equal(1_000_000, result.ImpedanceRaw);
        Assert.Equal(1_000_000, result.NaturalCapacityRateRaw);
    }

    [Fact]
    public void RoughAndSteepLandMatchesF083ReferenceButNotItsTariffOrInvestment()
    {
        var result = Score(Land(roughness: 1_000_000, fromElevation: -500_000, toElevation: 500_000));
        Assert.Equal(2_000_000, result.ImpedanceRaw);
        Assert.Equal(500_000, result.NaturalCapacityRateRaw);
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
    }

    [Fact]
    public void WaterHasDifferentNaturalCostWithoutPenalizingUnderseaSlope()
    {
        var flat = Score(new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Water, -900_000, -900_000, 0));
        var uneven = Score(new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Water, -900_000, 900_000, 0));
        Assert.Equal(EdgeMedium.Water, flat.Medium);
        Assert.Equal(800_000, flat.ImpedanceRaw);
        Assert.Equal(1_500_000, flat.NaturalCapacityRateRaw);
        Assert.Equal(flat, uneven);
    }

    [Fact]
    public void CoastIsOneUnitEdgeWithOneLocalInterfaceBaseline()
    {
        var neutral = Score(new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Water, 800_000, -700_000, 0));
        Assert.Equal(EdgeMedium.Coast, neutral.Medium);
        Assert.Equal(1_500_000, neutral.ImpedanceRaw);
        Assert.Equal(350_000, neutral.NaturalCapacityRateRaw);
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);

        var rugged = Score(new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Water, 800_000, -700_000, 1_000_000));
        Assert.Equal(2_100_000, rugged.ImpedanceRaw);
        Assert.True(rugged.NaturalCapacityRateRaw < neutral.NaturalCapacityRateRaw);
    }

    [Theory]
    [InlineData(NodeMedium.Land, NodeMedium.Land, 250_000, 350_000, 400_000)]
    [InlineData(NodeMedium.Land, NodeMedium.Water, 250_000, -350_000, 400_000)]
    [InlineData(NodeMedium.Water, NodeMedium.Water, -250_000, -350_000, 0)]
    public void UndirectedEdgesAreSymmetric(NodeMedium from, NodeMedium to, int hA, int hB, int roughness)
    {
        var original = Score(new NaturalLogicalEdge(from, to, hA, hB, roughness));
        var reversed = Score(new NaturalLogicalEdge(to, from, hB, hA, roughness));
        Assert.Equal(original, reversed);
    }

    [Fact]
    public void ShiftingAbsoluteAltitudeDoesNotRepriceSameLogicalGrade()
    {
        var first = Score(Land(roughness: 100_000, fromElevation: -100_000, toElevation: 120_000));
        var second = Score(Land(roughness: 100_000, fromElevation: 400_000, toElevation: 620_000));
        Assert.Equal(first, second);
    }

    [Fact]
    public void TerrainWorseningIncreasesCostAndReducesNaturalCapacity()
    {
        var best = Score(Land());
        var rugged = Score(Land(roughness: 500_000));
        var ruggedAndSteep = Score(Land(roughness: 500_000, toElevation: 500_000));
        Assert.True(best.ImpedanceRaw < rugged.ImpedanceRaw);
        Assert.True(rugged.ImpedanceRaw < ruggedAndSteep.ImpedanceRaw);
        Assert.True(best.NaturalCapacityRateRaw > rugged.NaturalCapacityRateRaw);
        Assert.True(rugged.NaturalCapacityRateRaw > ruggedAndSteep.NaturalCapacityRateRaw);
    }

    [Fact]
    public void SmallContinuousChangesDoNotCauseBiomeCliffs()
    {
        var a = Score(Land(roughness: 500_000, toElevation: 500_000));
        var r = Score(Land(roughness: 500_001, toElevation: 500_000));
        var g = Score(Land(roughness: 500_000, toElevation: 500_001));
        Assert.InRange(Math.Abs(a.ImpedanceRaw - r.ImpedanceRaw), 0, 1);
        Assert.InRange(Math.Abs(a.ImpedanceRaw - g.ImpedanceRaw), 0, 1);
        Assert.InRange(Math.Abs(a.NaturalCapacityRateRaw - r.NaturalCapacityRateRaw), 0, 2);
        Assert.InRange(Math.Abs(a.NaturalCapacityRateRaw - g.NaturalCapacityRateRaw), 0, 2);
    }

    [Fact]
    public void F083TwoDifficultHopsAreMoreExpensiveThanThreeNeutralOnes()
    {
        // Path A-B-D: 2 logical edges at C=2; alternate A-C-E-D: 3 neutral C=1.
        var hard = Score(Land(1_000_000, -500_000, 500_000));
        var neutral = Score(Land());
        long shortRouteImpedance = checked(hard.ImpedanceRaw + hard.ImpedanceRaw);
        long longRouteImpedance = checked(neutral.ImpedanceRaw * 3);
        Assert.Equal(4_000_000, shortRouteImpedance);
        Assert.Equal(3_000_000, longRouteImpedance);
        Assert.True(shortRouteImpedance > longRouteImpedance);
        Assert.Equal(2, NaturalEdgeEvaluation.LogicalDistance * 2);
        Assert.Equal(3, NaturalEdgeEvaluation.LogicalDistance * 3);
    }

    [Fact]
    public void DifferentCalibrationChangesOnlyTheSuppliedEdgeEvaluation()
    {
        var edge = Land(500_000, -100_000, 300_000);
        var a = Score(edge);
        var candidateB = Cal with { RoughnessImpedanceWeightRaw = 100_000 };
        var b = NaturalEdgeEvaluator.Evaluate(edge, candidateB);
        var aAgain = Score(edge);
        Assert.Equal(a, aAgain);
        Assert.True(a.ImpedanceRaw > b.ImpedanceRaw);
        Assert.Equal(a.NaturalCapacityRateRaw, b.NaturalCapacityRateRaw);
    }

    [Fact]
    public void CumulativePhysicalCostDoesNotBecomeCashOrWorkerTime()
    {
        // This function exposes only fixed-point natural impedance and throughput.
        // No currency amount, price, toll, timestep or worker position is present.
        var edge = Score(Land());
        Assert.Equal(2 * NaturalEdgeCalibration.Scale, edge.ImpedanceRaw * 2);
        Assert.Equal(1_000_000, edge.NaturalCapacityRateRaw);
    }

    [Fact]
    public void InvalidMediumTerrainAndParametersFailLoudly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Score(new NaturalLogicalEdge((NodeMedium)0, NodeMedium.Land, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Score(Land(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Score(Land(0, -1_000_001, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Score(Land(0, 0, 1_000_001)));
        Assert.Throws<ArgumentException>(() =>
            Score(new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Water, 0, 0, 1)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NaturalEdgeEvaluator.Evaluate(Land(), Cal with { LandImpedanceBaseRaw = 999_999 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NaturalEdgeEvaluator.Evaluate(Land(), Cal with { CoastCapacityRateRaw = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NaturalEdgeEvaluator.Evaluate(Land(), Cal with { GradeImpedanceWeightRaw = -1 }));
    }

    [Fact]
    public void EveryRegularLandEdgeRemainsPassableAndIsDeterministic()
    {
        long checksumA = 0;
        long checksumB = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            long checksum = 0;
            for (int i = 0; i < 2_048; i++)
            {
                var edge = Land(i * 7_091 % 1_000_001,
                    (int)(i * 37_001L % 2_000_001) - 1_000_000,
                    (int)(i * 97_003L % 2_000_001) - 1_000_000);
                var value = Score(edge);
                Assert.True(value.ImpedanceRaw >= NaturalEdgeCalibration.Scale);
                Assert.True(value.NaturalCapacityRateRaw > 0);
                checksum = checked(checksum + value.ImpedanceRaw * 11 + value.NaturalCapacityRateRaw);
            }
            if (pass == 0) checksumA = checksum;
            else checksumB = checksum;
        }
        Assert.Equal(checksumA, checksumB);
    }
}
