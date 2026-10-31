using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs28DistanceNeutralRouteTests
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

    private static PhysicalTacticalTileId FindRemoteLand(
        CanonicalPhysicalTacticalSession session)
    {
        var origin = session.InitialTile;
        var adjacent = session.AdjacentWorkable(origin).ToHashSet();
        var remote = adjacent
            .SelectMany(session.AdjacentWorkable)
            .Where(x => x != origin && !adjacent.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value)
            .ToArray();
        Assert.NotEmpty(remote);
        return remote[0];
    }

    [Theory]
    [InlineData(EconomicActivityKind.Agriculture)]
    [InlineData(EconomicActivityKind.Forestry)]
    [InlineData(EconomicActivityKind.Mining)]
    public void AnySelectedRemoteLandUsesRealMultiLegTopologyWithoutColonizing(
        EconomicActivityKind destinationJob)
    {
        var game = Start();
        var origin = game.InitialTile;
        var destination = FindRemoteLand(game);
        Assert.DoesNotContain(destination, game.AdjacentWorkable(origin));
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var events = game.Events.ToArray();

        var result = PhysicalRemoteDestinationRouteProjection.Preview(
            game, origin, destination,
            EconomicActivityKind.Agriculture, destinationJob, 2UL);

        Assert.True(result.HasFootpath);
        Assert.Equal(PhysicalPrimitiveRouteAvailability.Walkable, result.Availability);
        Assert.Equal(origin, result.Tiles[0]);
        Assert.Equal(destination, result.Tiles[^1]);
        Assert.True(result.MovementLegs >= 2UL);
        Assert.Equal((ulong)result.Tiles.Count - 1UL, result.MovementLegs);
        Assert.Equal(checked(result.MovementLegs * 2UL), result.PrimitiveTravelTurns);
        Assert.Equal(game.State.MovementTurn + result.PrimitiveTravelTurns,
            result.EarliestArrivalTurn);
        for (var i = 1; i < result.Tiles.Count; i++)
            Assert.Contains(result.Tiles[i], game.AdjacentWorkable(result.Tiles[i - 1]));

        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(events, game.Events);
        Assert.Empty(game.State.Transits);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        var rerun = PhysicalRemoteDestinationRouteProjection.Preview(
            restored, origin, destination,
            EconomicActivityKind.Agriculture, destinationJob, 2UL);
        Assert.Equal(result.Availability, rerun.Availability);
        Assert.Equal(result.MovementLegs, rerun.MovementLegs);
        Assert.Equal(result.PrimitiveTravelTurns, rerun.PrimitiveTravelTurns);
        Assert.Equal(result.Tiles, rerun.Tiles);
    }

    [Fact]
    public void WalkingRouteIsNotAProhibitionOnFutureOverseasColonization()
    {
        var game = Start();
        var origin = game.InitialTile;
        var remoteWaterId = Enumerable.Range(1,
                checked((int)game.State.FineParameters.StrategicCellCount))
            .Select(v => new StrategicCellId((ulong)v))
            .First(id => !game.IsPhysicalWorkableLand(id));
        var destination = new PhysicalTacticalTileId(
            game.State.FineParameters, remoteWaterId);
        var save = game.ExportSaveJson();
        var forecast = PhysicalRemoteDestinationRouteProjection.Preview(
            game, origin, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL);

        Assert.False(forecast.HasFootpath);
        Assert.Equal(
            PhysicalPrimitiveRouteAvailability.DestinationNeedsOtherTechnology,
            forecast.Availability);
        Assert.Empty(forecast.Tiles);
        Assert.Null(forecast.PrimitiveTravelTurns);
        Assert.Null(forecast.EarliestArrivalTurn);
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }

    [Fact]
    public void RemoteRouteCanCrossCurrentCadenceWindowWithoutChangingItsClock()
    {
        var game = Start(born: false);
        for (var i = 0; i < 30; i++)
            game.AdvanceMovementTurn();
        var dest = FindRemoteLand(game);
        var before = game.ExportSaveJson();
        var route = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, dest, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 1UL);

        Assert.Equal(30UL, game.State.MovementTurn);
        Assert.Equal(30UL + route.PrimitiveTravelTurns!.Value,
            route.EarliestArrivalTurn);
        Assert.Equal(13U, game.CurrentHash.FormatVersion);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void InvalidOriginWorkersProfessionOrWorldCannotProduceRoute()
    {
        var game = Start();
        var destination = FindRemoteLand(game);
        var save = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalRemoteDestinationRouteProjection.Preview(game,
                game.InitialTile, game.InitialTile,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalRemoteDestinationRouteProjection.Preview(game,
                game.InitialTile, destination,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 0UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalRemoteDestinationRouteProjection.Preview(game,
                game.InitialTile, destination,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 5UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalRemoteDestinationRouteProjection.Preview(game,
                game.InitialTile, destination,
                EconomicActivityKind.Agriculture, (EconomicActivityKind)255, 1UL));
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }
}
