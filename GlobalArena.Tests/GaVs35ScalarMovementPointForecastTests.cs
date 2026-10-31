using GlobalArena.Runtime;

namespace GlobalArena.Tests;

public sealed class GaVs35ScalarMovementPointForecastTests
{
    [Fact]
    public void TwoHundredPointsOnTwoHundredFiftyPointEdgeTakesTwoTurns()
    {
        var route = ScalarMovementPointForecast.Preview(
            8UL, 200UL, new ulong[] { 250UL });
        Assert.Equal(250UL, route.TotalCostPoints);
        Assert.Equal(2UL, route.TotalMovementTurns);
        Assert.Equal(10UL, route.ArrivalTurn);
        Assert.Equal(150UL, route.UnusedPointsOnArrivalTurn);
        Assert.Equal(10UL, Assert.Single(route.Legs).CompletionTurn);
    }

    [Fact]
    public void MultipleEdgesCanBeCompletedWithinOneTurnWithoutExtraOrders()
    {
        var route = ScalarMovementPointForecast.Preview(
            5UL, 200UL, new ulong[] { 60UL, 80UL });
        Assert.Equal(6UL, route.ArrivalTurn);
        Assert.Equal(1UL, route.TotalMovementTurns);
        Assert.Equal(6UL, route.Legs[0].CompletionTurn);
        Assert.Equal(6UL, route.Legs[1].CompletionTurn);
        Assert.Equal(140UL, route.Legs[0].UnusedPointsOnCompletionTurn);
        Assert.Equal(60UL, route.Legs[1].UnusedPointsOnCompletionTurn);
        Assert.Equal(1, route.Legs[0].LegNumber);
        Assert.Equal(2, route.Legs[1].LegNumber);
    }

    [Fact]
    public void RemainingBudgetCarriesIntoNextLegOfSameJourney()
    {
        var route = ScalarMovementPointForecast.Preview(
            0UL, 200UL, new ulong[] { 250UL, 50UL });
        Assert.Equal(2UL, route.ArrivalTurn);
        Assert.Equal(2UL, route.Legs[1].CompletionTurn);
        Assert.Equal(100UL, route.UnusedPointsOnArrivalTurn);
    }

    [Fact]
    public void ExactBoundaryLeavesNoPointsInArrivalTurn()
    {
        var route = ScalarMovementPointForecast.Preview(
            1UL, 200UL, new ulong[] { 200UL, 200UL });
        Assert.Equal(3UL, route.ArrivalTurn);
        Assert.Equal(0UL, route.UnusedPointsOnArrivalTurn);
    }

    [Fact]
    public void SlowUnitsTakeLongerOnIdenticalRouteCosts()
    {
        var costs = new ulong[] { 250UL, 170UL };
        var infantry = ScalarMovementPointForecast.Preview(11UL, 100UL, costs);
        var cavalry = ScalarMovementPointForecast.Preview(11UL, 200UL, costs);
        Assert.Equal(16UL, infantry.ArrivalTurn);
        Assert.Equal(14UL, cavalry.ArrivalTurn);
        Assert.Equal(infantry.TotalCostPoints, cavalry.TotalCostPoints);
    }

    [Fact]
    public void PredictionsAreDeterministicAcrossRepeatedCalls()
    {
        var costs = new ulong[] { 40UL, 250UL, 100UL, 60UL };
        var a = ScalarMovementPointForecast.Preview(30UL, 200UL, costs);
        var b = ScalarMovementPointForecast.Preview(30UL, 200UL, costs);
        Assert.Equal(a.DepartureTurn, b.DepartureTurn);
        Assert.Equal(a.TotalCostPoints, b.TotalCostPoints);
        Assert.Equal(a.ArrivalTurn, b.ArrivalTurn);
        Assert.Equal(a.Legs.ToArray(), b.Legs.ToArray());
        Assert.Equal(33UL, a.ArrivalTurn);
    }

    [Fact]
    public void RejectsZeroSpeedEmptyRouteAndZeroCostEdges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScalarMovementPointForecast.Preview(0UL, 0UL, new ulong[] { 1UL }));
        Assert.Throws<ArgumentNullException>(
            () => ScalarMovementPointForecast.Preview(0UL, 1UL, null!));
        Assert.Throws<ArgumentException>(
            () => ScalarMovementPointForecast.Preview(0UL, 1UL, Array.Empty<ulong>()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScalarMovementPointForecast.Preview(0UL, 1UL, new ulong[] { 1UL, 0UL }));
    }

    [Fact]
    public void HugeValuesAvoidPrematureRoundingOverflow()
    {
        var route = ScalarMovementPointForecast.Preview(
            0UL, ulong.MaxValue, new ulong[] { ulong.MaxValue });
        Assert.Equal(1UL, route.ArrivalTurn);
        Assert.Equal(0UL, route.UnusedPointsOnArrivalTurn);
        Assert.Throws<OverflowException>(() =>
            ScalarMovementPointForecast.Preview(
                1UL, 1UL, new ulong[] { ulong.MaxValue }));
        Assert.Throws<OverflowException>(() =>
            ScalarMovementPointForecast.Preview(
                0UL, ulong.MaxValue, new ulong[] { ulong.MaxValue, 1UL }));
    }

    [Fact]
    public void ExistingPrimitiveTimelineRemainsFixedAtTwoTurnsPerEdge()
    {
        var primitive = PhysicalMovementTimelines.ExistingPrimitive;
        Assert.Equal(13UL, primitive.RouteArrivalTurn(9UL, 2UL));
        Assert.Equal(11UL, primitive.LegCompletionTurn(9UL, 0));
        Assert.Equal(13UL, primitive.LegCompletionTurn(9UL, 1));
    }
}
