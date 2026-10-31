using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs34AutomaticRealCampAndSaveV17Tests
{
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
        var near = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !near.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    // Look for a physically unproductive route. In the canonical 01C
    // fixture, a 75%-of-crop primitive food quote need not cover a
    // worker's 0.5-unit demand. A valid refusal must never mint food.
    private static PhysicalTacticalTileId InsufficientForageDestination(
        CanonicalPhysicalTacticalSession game, ulong foodRaw)
    {
        var adjacent = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        var candidates = adjacent.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !adjacent.Contains(x))
            .Distinct().OrderBy(x => x.FineStrategicCellId.Value);
        foreach (var destination in candidates)
        {
            var forecast = JourneyProvisioningForecastProjection.PreviewPlanned(
                game, game.InitialTile, destination,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
                1UL, foodRaw);
            if (!forecast.CanArriveUnderAssumptions)
                return destination;
        }
        throw new InvalidOperationException(
            "Expected an unproductive primitive camp route in the fixed geography fixture.");
    }

    private static CanonicalPhysicalTacticalSession Supplied()
    {
        var game = Start();
        game.ReassignWorkers(game.InitialTile, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        game.ReassignWorkers(game.InitialTile, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var turn = 0; turn < 6; turn++)
            game.AdvanceMovementTurn();
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(1U)) > 0UL);
        return game;
    }

    private static PhysicalLongDistanceTransit Begin(
        CanonicalPhysicalTacticalSession game, ulong food,
        PhysicalTacticalTileId? destination = null) =>
        game.OrderProvisionedLongDistanceTransit(game.InitialTile,
            destination ?? Destination(game), EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, food, automaticCamps: true);

    [Fact]
    public void SufficientOwnedFoodGoesDirectlyWithoutMandatoryCamp()
    {
        var game = Supplied();
        var raw = Math.Min(3_000_000UL,
            game.StockAt(game.InitialTile, new CommodityId(1U)));
        var estimate = JourneyProvisioningForecastProjection.PreviewPlanned(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, raw);
        var original = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        // Mandatory zero-stop coverage: failure here means the test setup
        // was underfunded; do not treat that as a passing no-camp scenario.
        Assert.True(estimate.CanArriveUnderAssumptions);
        Assert.Empty(estimate.Stops);
        var trip = Begin(game, raw);
        var advancedTicks = 0;
        while (game.State.LongTrips.Any(x => x.OrderId == trip.OrderId))
        {
            game.AdvanceMovementTurn();
            advancedTicks++;
            Assert.True(advancedTicks <= 100,
                "Food-sufficient expedition did not arrive without camping.");
        }
        var camp = Assert.Single(game.State.JourneyCampStates!);
        Assert.Equal(0UL, camp.WaitingMovementTurns);
        Assert.Equal(0UL, camp.GatheredFoodRaw);
        Assert.Equal(original.EarliestArrivalTurn, game.State.MovementTurn);
    }

    [Fact]
    public void UnproductivePrimitiveCampRefusesDepartureWithoutInventingFoodOrMoney()
    {
        var game = Supplied();
        var startingFood = Math.Min(500_000UL,
            game.StockAt(game.InitialTile, new CommodityId(1U)));
        var destination = InsufficientForageDestination(game, startingFood);
        var forecast = JourneyProvisioningForecastProjection.PreviewPlanned(
            game, game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, startingFood);
        Assert.False(forecast.CanArriveUnderAssumptions);
        Assert.Equal(JourneyProvisioningStatus.InsufficientFoodAndNoProductiveStop,
            forecast.Status);
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        Assert.Throws<InvalidOperationException>(
            () => Begin(game, startingFood, destination));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.State.LongTrips);
        Assert.Null(game.State.TravelFoodLedgers);
        Assert.Null(game.State.JourneyCampStates);
    }

    [Fact]
    public void ProductiveCampFoodAccountingIsPhysicalConservativeAndCycleBound()
    {
        var game = Start();
        var source = game.InitialTile;
        var dest = Destination(game);
        var food = new PhysicalJourneyFoodLedger(
            1UL, source, dest,
            500_000UL, 500_000UL, 0UL, 1_000_000UL, 500_000UL, false);
        var camp = new PhysicalJourneyCampState(1UL, 0UL, 0UL, 0UL);
        var worldBefore = game.ExportSaveJson();

        var nonSettlement = PhysicalJourneyCampFoodAccounting.WaitOneTurn(
            food, camp, 1UL, 1_500_000UL, economicSettlement: false);
        Assert.Equal(0UL, nonSettlement.ProducedRaw);
        Assert.Equal(food.FoodRemainingRaw, nonSettlement.Food.FoodRemainingRaw);
        Assert.Equal(1UL, nonSettlement.Camp.WaitingMovementTurns);
        Assert.Equal(0UL, nonSettlement.Camp.GatheringEconomicCycles);

        var productive = PhysicalJourneyCampFoodAccounting.WaitOneTurn(
            nonSettlement.Food, nonSettlement.Camp,
            1UL, 1_500_000UL, economicSettlement: true);
        Assert.Equal(1_500_000UL, productive.ProducedRaw);
        Assert.Equal(2UL, productive.Camp.WaitingMovementTurns);
        Assert.Equal(1UL, productive.Camp.GatheringEconomicCycles);
        var fed = PhysicalJourneyCampFoodAccounting.ConsumeOneEconomicCycle(
            productive.Food, 1UL);
        Assert.Equal(500_000UL, fed.ConsumedRaw);
        Assert.Equal(0UL, fed.UnmetRaw);
        Assert.Equal((UInt128)fed.Food.FoodPurchasedRaw +
            productive.Camp.GatheredFoodRaw,
            (UInt128)fed.Food.FoodConsumedRaw + fed.Food.FoodRemainingRaw);
        Assert.Equal(worldBefore, game.ExportSaveJson());

        Assert.Throws<InvalidOperationException>(() =>
            PhysicalJourneyCampFoodAccounting.WaitOneTurn(
                food, camp, 1UL, 400_000UL, economicSettlement: true));
    }

    [Fact]
    public void MixedManagedAndLegacyTripsPreserveOneDemandAndVersionedSaves()
    {
        var game = Supplied();
        game.OrderLongDistanceTransit(game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        var stock = game.StockAt(game.InitialTile, new CommodityId(1U));
        var forecast = JourneyProvisioningForecastProjection.PreviewPlanned(
            game, game.InitialTile, Destination(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, Math.Min(stock, 2_000_000UL));
        // Do not silently skip the mixed legacy/new-rule invariants.
        Assert.True(forecast.CanArriveUnderAssumptions,
            "The mixed V15/V17 scenario must have a feasible physical supply plan.");
        var before = game.FoodDemandAt(game.InitialTile);
        Begin(game, Math.Min(stock, 2_000_000UL));
        Assert.Equal(before - CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            game.FoodDemandAt(game.InitialTile));
        Assert.Equal(2UL, game.State.WorkersInTransit);
        Assert.Single(game.State.JourneyCampStates!);
        var saved = game.ExportSaveJson();
        Assert.Equal(saved,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(saved).ExportSaveJson());
    }
}
