using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs32TravelProvisioningAndCampForecastTests
{
    private sealed class FixedSupply(ulong perWorker) : ITravelFoodSource
    {
        public string Kind => "test-provider";
        public ulong RawFoodPerWorkerPerSettlement(
            CanonicalPhysicalTacticalSession game, PhysicalTacticalTileId at) =>
            perWorker;
    }

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static PhysicalTacticalTileId Destination(
        CanonicalPhysicalTacticalSession game)
    {
        var adjacent = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return adjacent.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !adjacent.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    private static JourneyProvisioningForecast Preview(
        CanonicalPhysicalTacticalSession game, ulong cargo,
        ITravelFoodSource? provider = null, ulong workers = 2UL) =>
        JourneyProvisioningForecastProjection.PreviewPlanned(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            workers, cargo, provider);

    [Fact]
    public void AdequateHypotheticalCargoDoesNotCreateMandatoryCamps()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var plan = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        var estimate = Preview(game, 15_000_000UL, new FixedSupply(0UL));
        Assert.True(estimate.CanArriveUnderAssumptions);
        Assert.Equal(JourneyProvisioningStatus.ArrivalPossible, estimate.Status);
        Assert.Equal(plan.EarliestArrivalTurn, estimate.ProjectedArrivalMovementTurn);
        Assert.Equal(plan.PrimitiveTravelTurns, estimate.TravelMovementTurns);
        Assert.Equal(0UL, estimate.WaitingMovementTurns);
        Assert.Empty(estimate.Stops);
        Assert.Equal(0UL, estimate.GatheredFoodRaw);
        Assert.Equal(15_000_000UL - estimate.ConsumedFoodRaw, estimate.RemainingCargoRaw);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void ZeroCargoCanRecoverOnlyByWorkingThroughActualEconomicTime()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var forecast = Preview(game, 0UL, new FixedSupply(1_500_000UL));
        var route = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        Assert.True(forecast.CanArriveUnderAssumptions);
        Assert.True(forecast.WaitingMovementTurns >= 1UL);
        Assert.NotEmpty(forecast.Stops);
        Assert.Equal(route.PrimitiveTravelTurns, forecast.TravelMovementTurns);
        Assert.Equal(forecast.ProjectedArrivalMovementTurn,
            game.State.MovementTurn + forecast.TravelMovementTurns +
                forecast.WaitingMovementTurns);
        Assert.All(forecast.Stops, stop =>
        {
            Assert.Equal("test-provider", stop.SourceKind);
            Assert.True(stop.EconomicSettlements >= 1UL);
            Assert.True(stop.FoodGatheredRaw > stop.FoodConsumedRaw);
            Assert.Contains(stop.Tile, route.Tiles);
        });
        Assert.Equal(forecast.HypotheticalInitialCargoRaw +
            forecast.GatheredFoodRaw - forecast.ConsumedFoodRaw,
            forecast.RemainingCargoRaw);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(0UL, game.State.WorkersInTransit);
        Assert.Empty(game.State.LongTrips);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        var replayed = Preview(reopened, 0UL, new FixedSupply(1_500_000UL));
        Assert.Equal(forecast.Status, replayed.Status);
        Assert.Equal(forecast.ProjectedArrivalMovementTurn,
            replayed.ProjectedArrivalMovementTurn);
        Assert.Equal(forecast.RemainingCargoRaw, replayed.RemainingCargoRaw);
        Assert.Equal(forecast.WaitingMovementTurns, replayed.WaitingMovementTurns);
        Assert.Equal(forecast.Stops.ToArray(), replayed.Stops.ToArray());
    }

    [Fact]
    public void UnproductiveTerrainCannotMagicallyCreateFoodOrMakeDistanceIllegal()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var forecast = Preview(game, 0UL, new FixedSupply(0UL));
        Assert.False(forecast.CanArriveUnderAssumptions);
        Assert.Equal(JourneyProvisioningStatus.InsufficientFoodAndNoProductiveStop,
            forecast.Status);
        Assert.Null(forecast.ProjectedArrivalMovementTurn);
        Assert.Null(forecast.RemainingCargoRaw);
        Assert.Equal(0UL, forecast.GatheredFoodRaw);
        Assert.Equal(0UL, forecast.WaitingMovementTurns);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Empty(game.Events);
        Assert.NotEmpty(game.AdjacentWorkable(game.InitialTile));
    }

    [Fact]
    public void DefaultForageQuotesLowYieldRelativeToActualGeographyWithoutBuyingStock()
    {
        var game = Start();
        var food = new PrimitiveForageFoodSource();
        var source = game.InitialTile;
        var sourceYield = game.PrimitiveYieldAt(source, EconomicActivityKind.Agriculture);
        var quoted = food.RawFoodPerWorkerPerSettlement(game, source);
        Assert.Equal((ulong)((UInt128)(ulong)Math.Max(0L, sourceYield) *
            PrimitiveForageFoodSource.InitialEfficiencyPercent / 100U), quoted);
        Assert.True(quoted <= (ulong)Math.Max(0L, sourceYield));
        Assert.Equal("temporary-forage", food.Kind);
        var before = game.ExportSaveJson();
        _ = Preview(game, 0UL);
        Assert.Equal(before, game.ExportSaveJson());
    }

    [Fact]
    public void LongerEconomicIntervalsCanChangeCampWaitingWithoutChangingWalkingSpeed()
    {
        var game = Start();
        for (var i = 0; i < 30; i++)
            game.AdvanceMovementTurn();
        var before = game.ExportSaveJson();
        var plan = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        var forecast = Preview(game, 0UL, new FixedSupply(1_500_000UL));
        Assert.True(forecast.CanArriveUnderAssumptions);
        Assert.Equal(plan.PrimitiveTravelTurns, forecast.TravelMovementTurns);
        Assert.True(forecast.ProjectedArrivalMovementTurn >= plan.EarliestArrivalTurn);
        Assert.Equal(before, game.ExportSaveJson());
    }

    [Fact]
    public void RejectsUnknownProfessionInsufficientWorkersAndNonexistentJourney()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            Preview(game, 0UL, workers: 5UL));
        Assert.Throws<InvalidOperationException>(() =>
            JourneyProvisioningForecastProjection.PreviewPlanned(
                game, game.InitialTile, game.AdjacentWorkable(game.InitialTile)[0],
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
                1UL, 0UL));
        Assert.Equal(before, game.ExportSaveJson());
    }
}
