using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs29OneOrderMultilegJourneyTests
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

    private static PhysicalTacticalTileId FindReachableNonadjacent(
        CanonicalPhysicalTacticalSession game)
    {
        var start = game.InitialTile;
        var direct = game.AdjacentWorkable(start).ToHashSet();
        return direct.SelectMany(game.AdjacentWorkable)
            .Where(x => x != start && !direct.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneOrderCrossesSeveralLegsAndArrivesOnlyAtTheFinalTile(bool born)
    {
        var game = Start(born);
        var initial = game.ExportSaveJson();
        var beforeMoney = Money(game);
        var source = game.InitialTile;
        var destination = FindReachableNonadjacent(game);
        var plan = PhysicalRemoteDestinationRouteProjection.Preview(
            game, source, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        Assert.True(plan.MovementLegs >= 2UL);
        Assert.Equal(initial, game.ExportSaveJson());
        var trip = game.OrderLongDistanceTransit(source, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        Assert.Equal(plan.MovementLegs, (ulong)trip.TotalLegs);
        Assert.Equal(plan.EarliestArrivalTurn, trip.ArrivalTurn);
        Assert.Equal(2UL, game.State.WorkersInTransit);
        Assert.Single(game.State.LongTrips);
        Assert.Empty(game.State.Transits);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(15U, game.CurrentHash.FormatVersion);
        Assert.Single(game.Events);
        Assert.Equal("depart_remote", game.Events[0].Kind);
        Assert.Equal(15, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!.Version);
        Assert.Equal(beforeMoney, Money(game));

        for (ulong turn = 1UL; turn < trip.ArrivalTurn; turn++)
        {
            game.AdvanceMovementTurn();
            Assert.Equal(turn, game.State.MovementTurn);
            Assert.Equal(2UL, game.State.WorkersInTransit);
            Assert.Equal(0UL, game.WorkersAt(destination));
            Assert.Equal(8UL, game.State.TotalWorkers);
            Assert.Empty(game.Game.Snapshot.Territory.Controls);
            foreach (var intermediate in trip.Route.Skip(1).SkipLast(1))
                Assert.Equal(0UL, game.WorkersAt(intermediate));
            Assert.Equal(beforeMoney, Money(game));
            if (turn % 2UL == 0UL)
                Assert.Equal((int)(turn / 2UL),
                    Assert.Single(game.State.LongTrips).CompletedLegs);

            var saved = game.ExportSaveJson();
            var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
            Assert.Equal(saved, reopened.ExportSaveJson());
            Assert.Equal(game.CurrentHash, reopened.CurrentHash);
            Assert.Equal(15U, reopened.CurrentHash.FormatVersion);
        }

        game.AdvanceMovementTurn();
        Assert.Equal(trip.ArrivalTurn, game.State.MovementTurn);
        Assert.Equal(0UL, game.State.WorkersInTransit);
        Assert.Empty(game.State.LongTrips);
        Assert.Equal(2UL, game.WorkersAt(destination));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(beforeMoney, Money(game));
        Assert.Equal(15U, game.CurrentHash.FormatVersion);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.Equal(2, EmergentSettlementProjection.FromSession(game).Sites.Count);
        var finished = game.ExportSaveJson();
        Assert.Equal(finished, CanonicalPhysicalTacticalSession
            .RestoreSaveJson(finished).ExportSaveJson());
        Assert.Equal(initial, Start(born).ExportSaveJson());
    }

    [Fact]
    public void MultiLegJourneysCoexistWithOrdinaryAdjacentTransitAndRealFood()
    {
        var game = Start();
        var source = game.InitialTile;
        var adjacent = game.AdjacentWorkable(source)[0];
        var destination = FindReachableNonadjacent(game);
        game.OrderLongDistanceTransit(source, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        game.OrderWorkerTransit(source, adjacent,
            EconomicActivityKind.Mining, EconomicActivityKind.Mining, 1UL);
        Assert.Equal(3UL, game.State.WorkersInTransit);
        Assert.Equal(8UL, game.State.TotalWorkers);
        var reference = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(reference);
        var budget = Money(game);
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(game.AdvanceMovementTurn(), replay.AdvanceMovementTurn());
            Assert.Equal(game.CurrentHash, replay.CurrentHash);
            Assert.Equal(game.ExportSaveJson(), replay.ExportSaveJson());
            Assert.Equal(8UL, game.State.TotalWorkers);
            Assert.Equal(budget, Money(game));
        }
        Assert.Equal(1UL, game.WorkersAt(adjacent));
        Assert.Equal(2UL, game.WorkersAt(destination));
        Assert.Equal(5UL, game.WorkersAt(source));
    }

    [Fact]
    public void OldV14SaveAndOrdinaryAdjacentTravelRemainByteForByteCompatible()
    {
        var game = Start();
        var destination = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 1UL);
        game.AdvanceMovementTurn();
        var json = game.ExportSaveJson();
        Assert.Equal(14, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)!.Version);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        Assert.Equal(json, CanonicalPhysicalTacticalSession.RestoreSaveJson(json)
            .ExportSaveJson());
        Assert.Empty(game.State.LongTrips);
        Assert.False(game.State.RemoteRoutingEnabled);
    }

    [Fact]
    public void UnreachableModeOrInvalidStaffCannotCommitRemoteOrder()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var neighbour = game.AdjacentWorkable(game.InitialTile)[0];
        Assert.Throws<InvalidOperationException>(() =>
            game.OrderLongDistanceTransit(game.InitialTile, neighbour,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            game.OrderLongDistanceTransit(game.InitialTile, FindReachableNonadjacent(game),
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 0UL));
        Assert.Throws<InvalidOperationException>(() =>
            game.OrderLongDistanceTransit(game.InitialTile, FindReachableNonadjacent(game),
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 5UL));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Empty(game.Events);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
    }

    private static ulong Money(CanonicalPhysicalTacticalSession game) =>
        game.FoodCash!.Accounts.Aggregate(0UL, (total, x) => checked(total +
            x.HouseholdMicrocredits + x.ProducerMicrocredits));
}
