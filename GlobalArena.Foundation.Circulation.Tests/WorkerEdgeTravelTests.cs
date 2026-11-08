using GlobalArena.Foundation.Circulation;

namespace GlobalArena.Foundation.Circulation.Tests;

public sealed class WorkerEdgeTravelTests
{
    private static readonly WorkerTravelCalibration Travel = WorkerTravelReferenceProfiles.CandidateB;
    private static readonly NaturalEdgeCalibration Freight = NaturalEdgeReferenceProfiles.CandidateA;

    private static NaturalLogicalEdge Land(
        int roughness = 0, int fromElevation = 0, int toElevation = 0) =>
        new(NodeMedium.Land, NodeMedium.Land, fromElevation, toElevation, roughness);

    private static NaturalLogicalEdge Water(int from = -300_000, int to = -900_000) =>
        new(NodeMedium.Water, NodeMedium.Water, from, to, 0);

    private static NaturalLogicalEdge Coast(int roughness = 0) =>
        new(NodeMedium.Land, NodeMedium.Water, 500_000, -900_000, roughness);

    private static long Duration(NaturalLogicalEdge edge, long workerEfficiencyRaw = 1_000_000) =>
        WorkerEdgeTravel.RequiredTimeRaw(edge, workerEfficiencyRaw, Travel);

    [Fact]
    public void OneLogicalHopHasConfigurablePhysicalTimeNotApprovedAsCanonicalCadence()
    {
        Assert.Equal(1, NaturalEdgeEvaluation.LogicalDistance);
        Assert.Equal(1_000_000, Duration(Land()));
        Assert.Equal(650_000, Duration(Water()));
        Assert.Equal(1_400_000, Duration(Coast()));
        Assert.Equal(2_200_000, Duration(Land(1_000_000, -500_000, 500_000)));
    }

    [Fact]
    public void WorkerEfficiencyChangesDurationWithoutChangingCommercialImpedance()
    {
        var edge = Land(500_000, -100_000, 150_000);
        long baseTime = Duration(edge);
        long fast = Duration(edge, 2_000_000);
        long slow = Duration(edge, 500_000);
        Assert.Equal(1_475_000, baseTime);
        Assert.Equal(737_500, fast);
        Assert.Equal(2_950_000, slow);

        var commercial = NaturalEdgeEvaluator.Evaluate(edge, Freight);
        Assert.Equal(1_400_000, commercial.ImpedanceRaw);
        Assert.NotEqual(commercial.ImpedanceRaw, baseTime);
    }

    [Fact]
    public void CommercialCalibrationCannotChangePhysicalWorkerDuration()
    {
        var edge = Land(600_000, 0, 300_000);
        var baseline = Duration(edge);
        var marketA = NaturalEdgeEvaluator.Evaluate(edge, Freight);
        var marketB = NaturalEdgeEvaluator.Evaluate(
            edge, Freight with { RoughnessImpedanceWeightRaw = 1_800_000 });
        Assert.NotEqual(marketA.ImpedanceRaw, marketB.ImpedanceRaw);
        Assert.Equal(baseline, Duration(edge));
    }

    [Fact]
    public void ChangingOnlyWorkerCalibrationLeavesCommercialImpedanceUntouched()
    {
        var edge = Land(350_000, 0, 200_000);
        var commercial = NaturalEdgeEvaluator.Evaluate(edge, Freight);
        var slower = Travel with { GradeEffortWeightRaw = 1_300_000 };
        Assert.True(WorkerEdgeTravel.RequiredTimeRaw(edge, 1_000_000, slower) > Duration(edge));
        Assert.Equal(commercial, NaturalEdgeEvaluator.Evaluate(edge, Freight));
    }

    [Fact]
    public void TravelOnlyPlacesWorkerAtDestinationAfterRequiredTime()
    {
        var transit = WorkerEdgeTransit.Begin(10, 20, Duration(Land()));
        Assert.False(transit.IsArrived);
        Assert.Null(transit.CurrentNodeId);

        var budget = WorkerActionTimeBudget.Create(1_000_000).Spend(300_000);
        var part = WorkerTravelProgress.Advance(transit, budget, 600_000);
        Assert.False(part.Arrived);
        Assert.Null(part.ArrivedAtNodeId);
        Assert.Equal(600_000, part.Transit.ElapsedTimeRaw);
        Assert.Equal(900_000, part.Budget.SpentRaw);
        Assert.Equal(100_000, part.Budget.RemainingRaw);

        var restThisTurn = WorkerTravelProgress.Advance(part.Transit, part.Budget, 100_000);
        Assert.False(restThisTurn.Arrived);
        Assert.Equal(700_000, restThisTurn.Transit.ElapsedTimeRaw);
        Assert.Equal(0, restThisTurn.Budget.RemainingRaw);

        var newTurn = WorkerActionTimeBudget.Create(1_000_000);
        var arrival = WorkerTravelProgress.Advance(restThisTurn.Transit, newTurn, 900_000);
        Assert.True(arrival.Arrived);
        Assert.Equal(20, arrival.ArrivedAtNodeId);
        Assert.Equal(300_000, arrival.TimeUsedRaw);
        Assert.Equal(700_000, arrival.Budget.RemainingRaw);
        Assert.Equal(1_000_000, arrival.Budget.Spend(700_000).SpentRaw);
        Assert.Throws<InvalidOperationException>(() =>
            WorkerTravelProgress.Advance(arrival.Transit, arrival.Budget, 1));
    }

    [Fact]
    public void DifficultEdgeCanRequireSeveralPeriodsWithNoProgressLoss()
    {
        var transit = WorkerEdgeTransit.Begin(1, 2, Duration(Land(1_000_000, -500_000, 500_000)));
        Assert.Equal(2_200_000, transit.RequiredTimeRaw);

        var a = WorkerTravelProgress.Advance(transit, WorkerActionTimeBudget.Create(1_000_000), 1_000_000);
        var b = WorkerTravelProgress.Advance(a.Transit, WorkerActionTimeBudget.Create(1_000_000), 1_000_000);
        var c = WorkerTravelProgress.Advance(b.Transit, WorkerActionTimeBudget.Create(1_000_000), 1_000_000);

        Assert.False(a.Arrived);
        Assert.False(b.Arrived);
        Assert.True(c.Arrived);
        Assert.Equal(200_000, c.TimeUsedRaw);
        Assert.Equal(800_000, c.Budget.RemainingRaw);
        Assert.Equal(2_200_000, c.Transit.ElapsedTimeRaw);
    }

    [Fact]
    public void TwoDistinctEdgesCanBeCrossedWithinOneBudgetWhenWorkerIsFast()
    {
        var budget = WorkerActionTimeBudget.Create(1_000_000);
        var first = WorkerEdgeTransit.Begin(10, 20, Duration(Land(), 2_000_000));
        var a = WorkerTravelProgress.Advance(first, budget, 1_000_000);
        Assert.True(a.Arrived);
        Assert.Equal(500_000, a.TimeUsedRaw);
        Assert.Equal(500_000, a.Budget.RemainingRaw);

        // Arrival at node 20 is required before starting the adjacent 20 -> 30.
        var second = WorkerEdgeTransit.Begin(a.ArrivedAtNodeId!.Value, 30, Duration(Land(), 2_000_000));
        var b = WorkerTravelProgress.Advance(second, a.Budget, 500_000);
        Assert.True(b.Arrived);
        Assert.Equal(30, b.ArrivedAtNodeId);
        Assert.Equal(1_000_000, b.Budget.SpentRaw);
        Assert.Equal(0, b.Budget.RemainingRaw);
    }

    [Fact]
    public void SplittingAContinuousTravelIntervalCannotChangeTheResult()
    {
        var initial = WorkerEdgeTransit.Begin(100, 200, 1_000_000);
        var single = WorkerTravelProgress.Advance(
            initial, WorkerActionTimeBudget.Create(1_000_000), 1_000_000);

        var a = WorkerTravelProgress.Advance(
            initial, WorkerActionTimeBudget.Create(1_000_000), 120_000);
        var b = WorkerTravelProgress.Advance(a.Transit, a.Budget, 230_000);
        var c = WorkerTravelProgress.Advance(b.Transit, b.Budget, 650_000);

        Assert.Equal(single.Transit, c.Transit);
        Assert.Equal(single.Budget, c.Budget);
        Assert.True(c.Arrived);
    }

    [Fact]
    public void ZeroRequestedTimeMakesNoProgressAndSpendsNothing()
    {
        var transit = WorkerEdgeTransit.Begin(0, 1, 5_000_000);
        var budget = WorkerActionTimeBudget.Create(1_000_000);
        var result = WorkerTravelProgress.Advance(transit, budget, 0);
        Assert.Equal(transit, result.Transit);
        Assert.Equal(budget, result.Budget);
        Assert.Equal(0, result.TimeUsedRaw);
        Assert.False(result.Arrived);
    }

    [Fact]
    public void ReverseDirectionRemainsSymmetricInThisOptInLaboratory()
    {
        var forward = new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Water, 650_000, -700_000, 240_000);
        var reverse = new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Land, -700_000, 650_000, 240_000);
        Assert.Equal(Duration(forward), Duration(reverse));
        Assert.Equal(Duration(Land(150_000, -500_000, 100_000)),
                     Duration(Land(150_000, 100_000, -500_000)));
    }

    [Fact]
    public void NoAbsoluteAltitudeOrOceanFloorMovementPenalty()
    {
        Assert.Equal(Duration(Land(180_000, -300_000, -200_000)),
                     Duration(Land(180_000, 300_000, 400_000)));
        Assert.Equal(Duration(Water(-900_000, 900_000)), Duration(Water(-900_000, -900_000)));
        Assert.Equal(Duration(Coast()), Duration(
            new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Land, -100_000, 100_000, 0)));
    }

    [Fact]
    public void SmallPhysicalChangesDoNotCreateBiomeCliffs()
    {
        long a = Duration(Land(500_000, 0, 500_000));
        long b = Duration(Land(500_001, 0, 500_000));
        long c = Duration(Land(500_000, 0, 500_001));
        Assert.InRange(Math.Abs(a - b), 0, 2);
        Assert.InRange(Math.Abs(a - c), 0, 2);
    }

    [Fact]
    public void BudgetPreventsOverdraftAndNegativeTime()
    {
        var budget = WorkerActionTimeBudget.Create(1_000_000).Spend(600_000);
        Assert.Equal(400_000, budget.RemainingRaw);
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Spend(400_001));
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Spend(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkerActionTimeBudget.Create(-1));

        var transit = WorkerEdgeTransit.Begin(1, 2, Duration(Land()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkerTravelProgress.Advance(transit, budget, 400_001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkerTravelProgress.Advance(transit, budget, -1));
    }

    [Fact]
    public void InvalidEdgesCalibrationsAndIDsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Duration(Land(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Duration(Land(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Duration(new NaturalLogicalEdge((NodeMedium)42, NodeMedium.Land, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Duration(new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Land, 0, 0, -1)));
        Assert.Throws<ArgumentException>(() =>
            Duration(new NaturalLogicalEdge(NodeMedium.Water, NodeMedium.Water, 0, 0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkerEdgeTravel.RequiredTimeRaw(Land(), 1_000_000,
                Travel with { NeutralLandEffortRaw = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkerEdgeTravel.RequiredTimeRaw(Land(), 1_000_000,
                Travel with { GradeEffortWeightRaw = -1 }));
        Assert.Throws<ArgumentException>(() => WorkerEdgeTransit.Begin(1, 1, 1_000_000));
        Assert.Throws<ArgumentException>(() => WorkerEdgeTransit.Begin(-1, 2, 1_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkerEdgeTransit.Begin(1, 2, 0));
        Assert.Throws<ArgumentException>(() =>
            WorkerTravelProgress.Advance(default, WorkerActionTimeBudget.Create(1_000_000), 1));
    }

    [Fact]
    public void HugeEfficiencyStillRequiresAtLeastOneUnitOfTime()
    {
        Assert.Equal(1, Duration(Land(), long.MaxValue));
    }

    [Fact]
    public void GoldenChecksumIsIndependentOfFloatingPointAndEvaluationOrder()
    {
        long checksum = 0;
        for (int i = 0; i < 4_096; i++)
        {
            var from = i % 7 == 0 ? NodeMedium.Water : NodeMedium.Land;
            var to = i % 11 == 0 ? NodeMedium.Water : NodeMedium.Land;
            int roughness = from == NodeMedium.Water && to == NodeMedium.Water
                ? 0 : (int)(i * 9_101L % 1_000_001);
            var edge = new NaturalLogicalEdge(from, to,
                (int)(i * 37_001L % 2_000_001) - 1_000_000,
                (int)(i * 97_003L % 2_000_001) - 1_000_000,
                roughness);
            long efficiency = 300_000 + (i * 131_071L % 2_700_001);
            checksum = checked(checksum + Duration(edge, efficiency) * (i % 5 + 1));
        }
        Assert.Equal(REFERENCE_CHECKSUM, checksum);
    }
}
