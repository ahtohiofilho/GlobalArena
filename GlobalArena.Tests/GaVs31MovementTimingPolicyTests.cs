using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// VS31 is an architectural refactor, not a new speed model.
/// The test matrix locks the exact old movement schedule and save hashes.
/// </summary>
public sealed class GaVs31MovementTimingPolicyTests
{
    private static CanonicalPhysicalTacticalSession Start(bool born = true) =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            born ? PhysicalCivilizationIdentityMode.BornWithPopulation
                : PhysicalCivilizationIdentityMode.LegacyUnattributed);

    private static PhysicalTacticalTileId Remote(CanonicalPhysicalTacticalSession session)
    {
        var origin = session.InitialTile;
        var adjacent = session.AdjacentWorkable(origin).ToHashSet();
        return adjacent.SelectMany(session.AdjacentWorkable)
            .Where(x => x != origin && !adjacent.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    [Theory]
    [InlineData(0UL, 1UL, 2UL)]
    [InlineData(0UL, 3UL, 6UL)]
    [InlineData(17UL, 1UL, 19UL)]
    [InlineData(17UL, 5UL, 27UL)]
    [InlineData(30UL, 2UL, 34UL)]
    public void ExistingTwoTurnRouteCalendarNeverChanges(
        ulong start, ulong legs, ulong expectedArrival)
    {
        var schedule = PhysicalMovementTimelines.ExistingPrimitive;
        Assert.Same(schedule, PhysicalMovementTimelines.ForExistingMode(
            PhysicalWorkerMovementMode.TimedAdjacent));
        Assert.Equal(expectedArrival, schedule.RouteArrivalTurn(start, legs));
        Assert.Equal(checked(start + 2UL), schedule.AdjacentArrivalTurn(start));
        for (var completed = 0; completed < (int)legs; completed++)
        {
            Assert.Equal(start + checked((ulong)completed * 2UL),
                schedule.LegStartTurn(start, completed));
            Assert.Equal(start + checked(((ulong)completed + 1UL) * 2UL),
                schedule.LegCompletionTurn(start, completed));
        }
    }

    [Fact]
    public void TimingInputsAreValidatedAndOverflowCannotWrapAround()
    {
        var timeline = PhysicalMovementTimelines.ExistingPrimitive;
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalMovementTimelines.ForExistingMode(
                PhysicalWorkerMovementMode.LegacyInstant));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timeline.RouteArrivalTurn(0UL, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timeline.LegCompletionTurn(0UL, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timeline.LegStartTurn(0UL, -1));
        Assert.Throws<OverflowException>(() =>
            timeline.AdjacentArrivalTurn(ulong.MaxValue));
        Assert.Throws<OverflowException>(() =>
            timeline.RouteArrivalTurn(0UL, ulong.MaxValue));
        Assert.Throws<OverflowException>(() =>
            timeline.LegCompletionTurn(ulong.MaxValue, 0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanonicalAdjacentAndLongDistanceOrdersFollowTheSameSchedule(
        bool born)
    {
        var game = Start(born);
        var initial = game.ExportSaveJson();
        var origin = game.InitialTile;
        var nearby = game.AdjacentWorkable(origin)[0];
        var remote = Remote(game);
        var timeline = PhysicalMovementTimelines.ExistingPrimitive;

        var predicted = PhysicalRemoteDestinationRouteProjection.Preview(
            game, origin, remote, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        Assert.Equal(timeline.RouteArrivalTurn(0UL, predicted.MovementLegs),
            predicted.EarliestArrivalTurn);
        Assert.Equal(initial, game.ExportSaveJson()); // read-only route projection

        var adjacentOrder = game.OrderWorkerTransit(
            origin, nearby, EconomicActivityKind.Mining,
            EconomicActivityKind.Mining, 1UL);
        Assert.Equal(2UL, adjacentOrder.ArrivalTurn);
        var remoteOrder = game.OrderLongDistanceTransit(
            origin, remote, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        Assert.Equal(timeline.RouteArrivalTurn(0UL, (ulong)remoteOrder.TotalLegs),
            remoteOrder.ArrivalTurn);
        Assert.Equal(timeline.LegCompletionTurn(0UL, 0),
            remoteOrder.NextLegCompletionTurn);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(15U, game.CurrentHash.FormatVersion);

        for (ulong turn = 1UL; turn < remoteOrder.ArrivalTurn; turn++)
        {
            game.AdvanceMovementTurn();
            Assert.Equal(turn, game.State.MovementTurn);
            var trip = Assert.Single(game.State.LongTrips);
            var expectedCompleted = (int)(turn / 2UL);
            Assert.Equal(expectedCompleted, trip.CompletedLegs);
            Assert.Equal(timeline.LegStartTurn(0UL, trip.CompletedLegs),
                timeline.LegCompletionTurn(0UL, trip.CompletedLegs) -
                    PrimitiveFixedLegMovementTimeline.TurnsPerLeg);
            Assert.Equal(timeline.LegCompletionTurn(0UL, trip.CompletedLegs),
                trip.NextLegCompletionTurn);
            Assert.Equal(0UL, game.WorkersAt(remote));
            Assert.Equal(8UL, game.State.TotalWorkers);
            if (turn == 2UL)
                Assert.Equal(1UL, game.WorkersAt(nearby));
            var json = game.ExportSaveJson();
            var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
            Assert.Equal(json, restored.ExportSaveJson());
            Assert.Equal(game.CurrentHash, restored.CurrentHash);
        }
        game.AdvanceMovementTurn();
        Assert.Equal(2UL, game.WorkersAt(remote));
        Assert.Empty(game.State.LongTrips);
        Assert.Equal(15U, game.CurrentHash.FormatVersion);
        Assert.Equal(8UL, game.State.TotalWorkers);
    }

    [Fact]
    public void V14AdjacentOnlyStateKeepsTheSameSaveAndHash()
    {
        var game = Start();
        var origin = game.InitialTile;
        game.OrderWorkerTransit(origin, game.AdjacentWorkable(origin)[0],
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(before, restored.ExportSaveJson());
        Assert.Equal(hash, restored.CurrentHash);
        Assert.Equal(2UL, Assert.Single(restored.State.Transits).ArrivalTurn);
    }
}
