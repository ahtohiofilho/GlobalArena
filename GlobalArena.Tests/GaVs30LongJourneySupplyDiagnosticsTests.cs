using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs30LongJourneySupplyDiagnosticsTests
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

    private static PhysicalTacticalTileId Destination(
        CanonicalPhysicalTacticalSession game)
    {
        var adjacent = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return adjacent.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !adjacent.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProposedFoodAndForgoneOutputTrackExactPhysicalSettlementCalendar(
        bool born)
    {
        var game = Start(born);
        var destination = Destination(game);
        var source = game.InitialTile;
        var start = game.ExportSaveJson();
        var beforeHash = game.CurrentHash;
        var events = game.Events.ToArray();
        var route = PhysicalRemoteDestinationRouteProjection.Preview(
            game, source, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        var a = PhysicalJourneySupplyProjection.PreviewPlanned(
            game, source, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);

        Assert.False(a.IsExistingJourney);
        Assert.True(route.MovementLegs >= 2UL);
        Assert.Equal(route.EarliestArrivalTurn, a.ExpectedArrivalTurn);
        Assert.Equal(route.PrimitiveTravelTurns, a.RemainingMovementTurns);
        Assert.Equal(a.RemainingMovementTurns - 1UL,
            a.EconomicSettlementsBeforeArrival); // first thirty turns: 1/1
        Assert.Equal(checked(a.EconomicSettlementsBeforeArrival * 2UL *
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker),
            a.TravellersBaselineFoodDemandRaw);
        Assert.Equal(checked(a.EconomicSettlementsBeforeArrival * 2UL *
            (ulong)game.PrimitiveYieldAt(source, EconomicActivityKind.Agriculture)),
            a.ForegoneSourceProductionRaw);
        Assert.Equal(game.StockAt(source, new CommodityId(1U)),
            a.ExistingOriginFoodStockRaw);
        Assert.Equal(Math.Min(a.ExistingOriginFoodStockRaw,
                a.TravellersBaselineFoodDemandRaw), a.StockOnlyUpperBoundRaw);
        Assert.Equal(a.TravellersBaselineFoodDemandRaw - a.StockOnlyUpperBoundRaw,
            a.StockOnlyUncoveredRaw);
        Assert.Equal(start, game.ExportSaveJson());
        Assert.Equal(beforeHash, game.CurrentHash);
        Assert.Equal(events, game.Events);
        Assert.Empty(game.State.LongTrips);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(start);
        Assert.Equal(a, PhysicalJourneySupplyProjection.PreviewPlanned(
            reopened, source, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL));
    }

    [Fact]
    public void ActiveJourneySupplyDeclinesByActualSettlementsAndSurvivesV15Replay()
    {
        var game = Start();
        var dest = Destination(game);
        var origin = game.InitialTile;
        var prospective = PhysicalJourneySupplyProjection.PreviewPlanned(
            game, origin, dest, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL);
        var trip = game.OrderLongDistanceTransit(
            origin, dest, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL);
        var first = PhysicalJourneySupplyProjection.PreviewActive(game, trip.OrderId);
        Assert.True(first.IsExistingJourney);
        Assert.Equal(prospective.TravellersBaselineFoodDemandRaw,
            first.TravellersBaselineFoodDemandRaw);
        Assert.Equal(prospective.ForegoneSourceProductionRaw,
            first.ForegoneSourceProductionRaw);

        game.AdvanceMovementTurn();
        var midway = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var after = PhysicalJourneySupplyProjection.PreviewActive(game, trip.OrderId);
        Assert.Equal(first.EconomicSettlementsBeforeArrival - 1UL,
            after.EconomicSettlementsBeforeArrival);
        Assert.Equal(first.TravellersBaselineFoodDemandRaw -
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            after.TravellersBaselineFoodDemandRaw);
        Assert.Equal(midway, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(midway);
        Assert.Equal(after, PhysicalJourneySupplyProjection.PreviewActive(
            restored, trip.OrderId));
        Assert.Equal(15U, restored.CurrentHash.FormatVersion);

        while (game.State.LongTrips.Count != 0)
            game.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalJourneySupplyProjection.PreviewActive(game, trip.OrderId));
        Assert.Equal(1UL, game.WorkersAt(dest));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void CalendarAfterTurnThirtyUsesCurrentAdaptiveCycleFrequency()
    {
        var game = Start();
        for (var i = 0; i < 30; i++)
            game.AdvanceMovementTurn();
        var destination = Destination(game);
        var before = game.ExportSaveJson();
        var route = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 1UL);
        var preview = PhysicalJourneySupplyProjection.PreviewPlanned(
            game, game.InitialTile, destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 1UL);
        Assert.Equal(route.EarliestArrivalTurn, preview.ExpectedArrivalTurn);
        Assert.True(preview.EconomicSettlementsBeforeArrival <
            preview.RemainingMovementTurns);
        ulong predictedSettlements = 0UL, last = game.State.Cycles[^1].MovementTurn;
        for (var turn = game.State.MovementTurn + 1UL;
             turn < preview.ExpectedArrivalTurn; turn++)
        {
            if (!PhysicalEconomicCadence.IsSettlementTurn(
                    turn, last, PhysicalEconomicCadenceMode.AdaptiveThirty))
                continue;
            last = turn;
            predictedSettlements++;
        }
        Assert.Equal(predictedSettlements, preview.EconomicSettlementsBeforeArrival);
        Assert.Equal(before, game.ExportSaveJson());
    }

    [Fact]
    public void PlannerRefusesNonWalkableAndUnknownJourneyWithoutAnyMutation()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var direct = game.AdjacentWorkable(game.InitialTile)[0];
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalJourneySupplyProjection.PreviewPlanned(game,
                game.InitialTile, direct, EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalJourneySupplyProjection.PreviewActive(game, 999UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalJourneySupplyProjection.PreviewPlanned(game,
                game.InitialTile, Destination(game),
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 5UL));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }
}
