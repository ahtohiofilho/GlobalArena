using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs16TacticalWorkerMarkerProjectionTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent);

    [Fact]
    public void GroupMarkersMapToExistingCanonicalActivityAllocationsWithoutMutation()
    {
        var game = Start();
        var beforeHash = game.CurrentHash;
        var beforeSave = game.ExportSaveJson();

        var view = TacticalWorkerMarkerProjection.FromPhysicalState(game.State);
        Assert.Equal(0UL, view.Turn);
        Assert.Equal(3, view.Groups.Count);
        Assert.Empty(view.Journeys);
        Assert.Equal(8UL, view.Groups.Aggregate(0UL,
            (sum, x) => checked(sum + x.Workers)));
        Assert.Contains(view.Groups, x => x.Activity == EconomicActivityKind.Agriculture &&
            x.Workers == 4UL && x.Tile == game.InitialTile);
        Assert.Contains(view.Groups, x => x.Activity == EconomicActivityKind.Mining &&
            x.Workers == 2UL && x.Tile == game.InitialTile);
        Assert.Contains(view.Groups, x => x.Activity == EconomicActivityKind.Forestry &&
            x.Workers == 2UL && x.Tile == game.InitialTile);
        Assert.Equal(beforeHash, game.CurrentHash);
        Assert.Equal(beforeSave, game.ExportSaveJson());
    }

    [Fact]
    public void TransitMarkerProgressMatchesTurnAndBecomesDestinationGroupOnlyOnArrival()
    {
        var game = Start();
        var dest = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, dest,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);

        var departed = TacticalWorkerMarkerProjection.FromPhysicalState(game.State);
        var trip = Assert.Single(departed.Journeys);
        Assert.Equal(0UL, trip.Elapsed);
        Assert.Equal(2UL, trip.Remaining);
        Assert.Equal(2UL, trip.Duration);
        Assert.Equal(0.0, trip.Fraction);
        Assert.Equal(2UL, trip.Workers);
        Assert.Equal(game.InitialTile, trip.Source);
        Assert.Equal(dest, trip.Destination);
        Assert.DoesNotContain(departed.Groups, x => x.Tile == dest);
        Assert.Equal(8UL, CountMarkers(departed));

        game.AdvanceMovementTurn();
        var enRoute = TacticalWorkerMarkerProjection.FromPhysicalState(game.State);
        var halfway = Assert.Single(enRoute.Journeys);
        Assert.Equal(1UL, halfway.Elapsed);
        Assert.Equal(1UL, halfway.Remaining);
        Assert.Equal(0.5, halfway.Fraction);
        Assert.Equal(8UL, CountMarkers(enRoute));
        Assert.DoesNotContain(enRoute.Groups, x => x.Tile == dest);

        var midSave = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(midSave);
        var replayView = TacticalWorkerMarkerProjection.FromPhysicalState(reopened.State);
        Assert.Equal(enRoute.Groups, replayView.Groups);
        Assert.Equal(enRoute.Journeys, replayView.Journeys);
        Assert.Equal(hash, reopened.CurrentHash);
        Assert.Equal(midSave, reopened.ExportSaveJson());

        game.AdvanceMovementTurn();
        reopened.AdvanceMovementTurn();
        var arrived = TacticalWorkerMarkerProjection.FromPhysicalState(game.State);
        Assert.Empty(arrived.Journeys);
        Assert.Equal(8UL, CountMarkers(arrived));
        Assert.Contains(arrived.Groups, x => x.Tile == dest &&
            x.Activity == EconomicActivityKind.Forestry && x.Workers == 2UL);
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
    }

    [Fact]
    public void LegacyV12MarketStillProjectsGroupsButNoTemporalRoutes()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement);
        legacy.AdvanceMovementTurn();
        var view = TacticalWorkerMarkerProjection.FromPhysicalState(legacy.State);
        Assert.Empty(view.Journeys);
        Assert.Equal(8UL, CountMarkers(view));
        Assert.Equal(12U, legacy.CurrentHash.FormatVersion);
    }

    private static ulong CountMarkers(TacticalWorkerMarkerProjection x) =>
        checked(x.Groups.Aggregate(0UL, (sum, group) =>
            checked(sum + group.Workers)) +
        x.Journeys.Aggregate(0UL, (sum, journey) =>
            checked(sum + journey.Workers)));
}
