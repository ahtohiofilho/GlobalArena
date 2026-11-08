using GlobalArena.Foundation.Circulation;

namespace GlobalArena.Foundation.Circulation.Tests;

public sealed class EdgeInvestmentLaboratoryTests
{
    private const long Unit = EdgeInvestmentLaboratory.Scale;
    private static readonly NaturalEdgeCalibration FreightCalibration = NaturalEdgeReferenceProfiles.CandidateA;
    private static readonly WorkerTravelCalibration WorkerCalibration = WorkerTravelReferenceProfiles.CandidateB;
    private static readonly NaturalLogicalEdge NeutralLand =
        new(NodeMedium.Land, NodeMedium.Land, 0, 0, 0);
    private static readonly NaturalLogicalEdge RoughSteepLand =
        new(NodeMedium.Land, NodeMedium.Land, -500_000, 500_000, 1_000_000);

    private static NaturalEdgeEvaluation Natural(NaturalLogicalEdge edge) =>
        NaturalEdgeEvaluator.Evaluate(edge, FreightCalibration);

    private static FreeFlowInvestedEdge Improved(
        NaturalLogicalEdge edge, long capital, long reference = Unit) =>
        EdgeInvestmentLaboratory.Evaluate(Natural(edge), capital, reference);

    [Fact]
    public void ZeroCapitalIsExactlyTheNaturalInfrastructureForEveryMedium()
    {
        var edges = new[]
        {
            NeutralLand,
            RoughSteepLand,
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Water, 200_000, -200_000, 100_000),
            new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Water, -500_000, -700_000, 0)
        };
        foreach (var edge in edges)
        {
            var natural = Natural(edge);
            var result = Improved(edge, 0);
            Assert.Equal(Unit, result.ImprovementFactorRaw);
            Assert.Equal(natural.ImpedanceRaw, result.FreeFlowImpedanceRaw);
            Assert.Equal(natural.NaturalCapacityRateRaw, result.CapacityRateRaw);
            Assert.Equal(0, result.TotalInvestedCapitalRaw);
            Assert.Equal(Unit, result.ReferenceCapitalRaw);
        }
    }

    [Theory]
    [InlineData(0L, 1_000_000L, 1_000_000L, 1_000_000L)]
    [InlineData(1_000_000L, 2_000_000L, 500_000L, 2_000_000L)]
    [InlineData(3_000_000L, 3_000_000L, 333_333L, 3_000_000L)]
    [InlineData(7_000_000L, 4_000_000L, 250_000L, 4_000_000L)]
    [InlineData(15_000_000L, 5_000_000L, 200_000L, 5_000_000L)]
    public void DyadicReferenceLandmarksHaveExactFixedPointValues(
        long capital, long factor, long impedance, long capacity)
    {
        var actual = Improved(NeutralLand, capital);
        Assert.Equal(factor, actual.ImprovementFactorRaw);
        Assert.Equal(impedance, actual.FreeFlowImpedanceRaw);
        Assert.Equal(capacity, actual.CapacityRateRaw);
    }

    [Fact]
    public void SmallerAdditionalReturnsAreNeverNegative()
    {
        long prevFactor = 0;
        long prevCapacity = 0;
        long prevCost = long.MaxValue;
        long prevStepGain = long.MaxValue;
        for (int i = 0; i <= 40; i++)
        {
            var value = Improved(NeutralLand, (long)i * Unit);
            Assert.True(value.ImprovementFactorRaw >= prevFactor);
            Assert.True(value.CapacityRateRaw >= prevCapacity);
            Assert.True(value.FreeFlowImpedanceRaw <= prevCost);
            if (i > 0)
            {
                long stepGain = value.ImprovementFactorRaw - prevFactor;
                Assert.True(stepGain <= prevStepGain,
                    "Dyadic linear interpolation must not increase marginal gains.");
                prevStepGain = stepGain;
            }
            prevFactor = value.ImprovementFactorRaw;
            prevCapacity = value.CapacityRateRaw;
            prevCost = value.FreeFlowImpedanceRaw;
        }
    }

    [Fact]
    public void FixedPointInterpolationStaysContinuousAtCapitalMilestones()
    {
        var justBefore = Improved(NeutralLand, Unit - 1);
        var exactly = Improved(NeutralLand, Unit);
        var justAfter = Improved(NeutralLand, Unit + 1);
        Assert.Equal(1_999_999, justBefore.ImprovementFactorRaw);
        Assert.Equal(2_000_000, exactly.ImprovementFactorRaw);
        Assert.Equal(2_000_000, justAfter.ImprovementFactorRaw);
        Assert.InRange(exactly.FreeFlowImpedanceRaw - justBefore.FreeFlowImpedanceRaw, -1, 0);
        Assert.InRange(justAfter.CapacityRateRaw - exactly.CapacityRateRaw, 0, 1);
    }

    [Fact]
    public void NoInventedInvestmentCapOrSeparateInfrastructureLayer()
    {
        var small = Improved(NeutralLand, 7 * Unit);
        var larger = Improved(NeutralLand, 31 * Unit);
        var largest = Improved(NeutralLand, 127 * Unit);
        Assert.True(small.ImprovementFactorRaw < larger.ImprovementFactorRaw);
        Assert.True(larger.ImprovementFactorRaw < largest.ImprovementFactorRaw);
        Assert.True(small.FreeFlowImpedanceRaw > larger.FreeFlowImpedanceRaw);
        Assert.True(larger.FreeFlowImpedanceRaw > largest.FreeFlowImpedanceRaw);
        Assert.True(largest.CapacityRateRaw > larger.CapacityRateRaw);
    }

    [Fact]
    public void SmallerReferenceCapitalAcceleratesImprovementWithoutBeingLengthDependent()
    {
        var lessEfficient = Improved(NeutralLand, Unit, 2 * Unit);
        var baseline = Improved(NeutralLand, Unit, Unit);
        var moreEfficient = Improved(NeutralLand, Unit, Unit / 2);
        Assert.True(lessEfficient.ImprovementFactorRaw < baseline.ImprovementFactorRaw);
        Assert.True(baseline.ImprovementFactorRaw < moreEfficient.ImprovementFactorRaw);
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
    }

    [Fact]
    public void PhysicalResultDependsOnAggregateCapitalNotOnShareCountOrOrder()
    {
        var natural = Natural(RoughSteepLand);
        var oneOwner = new long[] { 3 * Unit };
        var manyOwners = new long[] { Unit, Unit / 2, 3 * Unit / 2 };
        var shuffled = new long[] { 3 * Unit / 2, Unit, Unit / 2 };
        long one = EdgeInvestmentLaboratory.SumCapital(oneOwner);
        long many = EdgeInvestmentLaboratory.SumCapital(manyOwners);
        long reorder = EdgeInvestmentLaboratory.SumCapital(shuffled);
        Assert.Equal(3 * Unit, one);
        Assert.Equal(one, many);
        Assert.Equal(one, reorder);
        Assert.Equal(
            EdgeInvestmentLaboratory.Evaluate(natural, one, Unit),
            EdgeInvestmentLaboratory.Evaluate(natural, many, Unit));
    }

    [Fact]
    public void InvestmentInOneEdgeDoesNotAlterAnotherEdgeOrWorkerTravel()
    {
        var edgeA = RoughSteepLand;
        var edgeB = NeutralLand;
        var naturalA = Natural(edgeA);
        var naturalB = Natural(edgeB);
        long workerA = WorkerEdgeTravel.RequiredTimeRaw(edgeA, Unit, WorkerCalibration);
        long workerB = WorkerEdgeTravel.RequiredTimeRaw(edgeB, Unit, WorkerCalibration);
        var improvedA = EdgeInvestmentLaboratory.Evaluate(naturalA, 3 * Unit, Unit);

        Assert.True(improvedA.FreeFlowImpedanceRaw < naturalA.ImpedanceRaw);
        Assert.True(improvedA.CapacityRateRaw > naturalA.NaturalCapacityRateRaw);
        Assert.Equal(naturalB.ImpedanceRaw, Improved(edgeB, 0).FreeFlowImpedanceRaw);
        Assert.Equal(naturalB.NaturalCapacityRateRaw, Improved(edgeB, 0).CapacityRateRaw);
        Assert.Equal(workerA, WorkerEdgeTravel.RequiredTimeRaw(edgeA, Unit, WorkerCalibration));
        Assert.Equal(workerB, WorkerEdgeTravel.RequiredTimeRaw(edgeB, Unit, WorkerCalibration));
    }

    [Fact]
    public void F083RoutePreferenceCanFlipWithoutChangingHopCountOrSimulatingTolls()
    {
        long hard = Natural(RoughSteepLand).ImpedanceRaw;
        long easy = Natural(NeutralLand).ImpedanceRaw;
        Assert.Equal(2 * Unit, hard);
        Assert.Equal(1 * Unit, easy);
        Assert.Equal(4 * Unit, 2 * hard);
        Assert.Equal(3 * Unit, 3 * easy);

        long improvedHard = Improved(RoughSteepLand, Unit).FreeFlowImpedanceRaw;
        Assert.Equal(Unit, improvedHard);
        Assert.True(2 * improvedHard < 3 * easy);
        Assert.Equal(2, 2 * NaturalEdgeEvaluation.LogicalDistance);
        Assert.Equal(3, 3 * NaturalEdgeEvaluation.LogicalDistance);
    }

    [Fact]
    public void MoreLogicalNodesRequireMoreHopsForComparableIndexSeparation()
    {
        // Fixed fraction of two different logical rings; this intentionally
        // tests two DIFFERENT world graphs, not renderer-only resolution.
        long smallTime = NeutralRingTravelTime(nodeCount: 12, fractionDenominator: 4);
        long largeTime = NeutralRingTravelTime(nodeCount: 120, fractionDenominator: 4);
        Assert.Equal(3 * Unit, smallTime);
        Assert.Equal(30 * Unit, largeTime);
        Assert.Equal(10 * smallTime, largeTime);

        // A visual-only LOD parameter does not exist in the input contract.
        Assert.Equal(smallTime, NeutralRingTravelTime(12, 4));
    }

    [Fact]
    public void WorkerCanFinishWithinPeriodOrCarryProgressAcrossDifferentGraphSizes()
    {
        int shortHopCount = 3;
        int longHopCount = 30;
        Assert.Equal(3 * Unit, NeutralRingTravelTime(12, 4));
        Assert.Equal(30 * Unit, NeutralRingTravelTime(120, 4));

        // Same physical worker calibration. Faster worker crosses two neutral
        // edges per turn; no speed derived from the trade impedance.
        Assert.Equal(1_500_000, shortHopCount *
            WorkerEdgeTravel.RequiredTimeRaw(NeutralLand, 2 * Unit, WorkerCalibration));
        Assert.Equal(15_000_000, longHopCount *
            WorkerEdgeTravel.RequiredTimeRaw(NeutralLand, 2 * Unit, WorkerCalibration));

        var start = WorkerEdgeTransit.Begin(10, 11,
            WorkerEdgeTravel.RequiredTimeRaw(RoughSteepLand, Unit, WorkerCalibration));
        var first = WorkerTravelProgress.Advance(start,
            WorkerActionTimeBudget.Create(Unit), Unit);
        var second = WorkerTravelProgress.Advance(first.Transit,
            WorkerActionTimeBudget.Create(Unit), Unit);
        var last = WorkerTravelProgress.Advance(second.Transit,
            WorkerActionTimeBudget.Create(Unit), Unit);
        Assert.True(last.Arrived);
        Assert.Equal(200_000, last.TimeUsedRaw);
        Assert.Equal(800_000, last.Budget.RemainingRaw);
    }

    [Fact]
    public void OutOfRangeCapitalAndNaturalConditionsAreRejected()
    {
        var natural = Natural(NeutralLand);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeInvestmentLaboratory.Evaluate(natural, -1, Unit));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeInvestmentLaboratory.Evaluate(natural, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeInvestmentLaboratory.Evaluate(new NaturalEdgeEvaluation(EdgeMedium.Land, 0, Unit),
                Unit, Unit));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeInvestmentLaboratory.SumCapital(new long[] { Unit, -1 }));
        Assert.Throws<OverflowException>(() =>
            EdgeInvestmentLaboratory.SumCapital(new long[] { long.MaxValue, 1 }));
    }

    [Fact]
    public void FixedPointChecksumIsReproducibleAcrossPlatforms()
    {
        long checksumA = 0;
        long checksumB = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            long checksum = 0;
            for (int i = 0; i < 4_096; i++)
            {
                var natural = new NaturalEdgeEvaluation(
                    EdgeMedium.Land, Unit + (i % 17) * 50_000,
                    800_000 + (i % 11) * 30_000);
                var improved = EdgeInvestmentLaboratory.Evaluate(
                    natural, i * 13_781L, Unit);
                checksum = checked(checksum
                    + improved.FreeFlowImpedanceRaw * 3
                    + improved.CapacityRateRaw * 7
                    + improved.ImprovementFactorRaw);
            }
            if (pass == 0) checksumA = checksum;
            else checksumB = checksum;
        }
        Assert.Equal(REFERENCE_CHECKSUM, checksumA);
        Assert.Equal(checksumA, checksumB);
    }

    private static long NeutralRingTravelTime(int nodeCount, int fractionDenominator)
    {
        if (nodeCount < 4 || fractionDenominator < 2)
            throw new ArgumentOutOfRangeException(nameof(nodeCount));
        int hops = nodeCount / fractionDenominator;
        long total = 0;
        for (int i = 0; i < hops; i++)
            total = checked(total + WorkerEdgeTravel.RequiredTimeRaw(
                NeutralLand, Unit, WorkerCalibration));
        return total;
    }
}
