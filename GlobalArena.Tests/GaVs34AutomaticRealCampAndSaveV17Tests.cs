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

    // Find a concrete land route where primitive forage is actually viable.
    // A test that merely returns on an infeasible forecast does not prove
    // that V17 gathers anything. This makes the scenario mandatory.
    private static PhysicalTacticalTileId ProductiveCampDestination(
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
            if (forecast.CanArriveUnderAssumptions &&
                forecast.Stops.Count > 0 &&
                forecast.WaitingMovementTurns is > 0UL and <= 100UL)
                return destination;
        }
        throw new InvalidOperationException(
            "This test seed has no productive two-leg camp route; V17 positive coverage is mandatory.");
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
        while (game.State.LongTrips.Any(x => x.OrderId == trip.OrderId))
            game.AdvanceMovementTurn();
        var camp = Assert.Single(game.State.JourneyCampStates!);
        Assert.Equal(0UL, camp.WaitingMovementTurns);
        Assert.Equal(0UL, camp.GatheredFoodRaw);
        Assert.Equal(original.EarliestArrivalTurn, game.State.MovementTurn);
    }

    [Fact]
    public void FoodScarcityTriggersRealEconomicTimeOrRefusesUnproductiveStrategy()
    {
        var game = Supplied();
        var startRaw = Math.Min(500_000UL,
            game.StockAt(game.InitialTile, new CommodityId(1U)));
        var destination = ProductiveCampDestination(game, startRaw);
        var plan = JourneyProvisioningForecastProjection.PreviewPlanned(
            game, game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, startRaw);
        Assert.True(plan.CanArriveUnderAssumptions);
        Assert.NotEmpty(plan.Stops);
        Assert.True(plan.WaitingMovementTurns > 0UL);
        var trip = Begin(game, startRaw, destination);
        Assert.Equal(17, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!.Version);
        var initialCash = game.FoodCash!.InitialMoneySupplyMicrocredits;
        var originalETA = trip.ArrivalTurn;
        var spentTicks = 0;
        var previousGather = 0UL;
        while (game.State.LongTrips.Any(x => x.OrderId == trip.OrderId))
        {
            game.AdvanceMovementTurn();
            spentTicks++;
            Assert.True(spentTicks < 200, "Automatic camp did not finish its finite plan.");
            var lot = Assert.Single(game.State.TravelFoodLedgers!);
            var camp = Assert.Single(game.State.JourneyCampStates!);
            Assert.Equal((UInt128)lot.FoodPurchasedRaw + camp.GatheredFoodRaw,
                (UInt128)lot.FoodRemainingRaw + lot.FoodConsumedRaw);
            Assert.True(camp.GatheredFoodRaw >= previousGather);
            previousGather = camp.GatheredFoodRaw;

            if (camp.WaitingMovementTurns > 0UL && spentTicks == 1)
            {
                var saved = game.ExportSaveJson();
                var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
                Assert.Equal(saved, restored.ExportSaveJson());
                Assert.Equal(game.CurrentHash, restored.CurrentHash);
            }
        }
        var lastCamp = Assert.Single(game.State.JourneyCampStates!);
        var lastLot = Assert.Single(game.State.TravelFoodLedgers!);
        Assert.True(lastLot.Arrived);
        Assert.Equal(originalETA + lastCamp.WaitingMovementTurns,
            game.State.MovementTurn);
        Assert.Equal(plan.WaitingMovementTurns, lastCamp.WaitingMovementTurns);
        Assert.Equal(plan.GatheredFoodRaw, lastCamp.GatheredFoodRaw);
        Assert.True(lastCamp.WaitingMovementTurns > 0UL);
        Assert.True(lastCamp.GatheringEconomicCycles > 0UL);
        Assert.Equal(initialCash, game.FoodCash!.InitialMoneySupplyMicrocredits);
        var json = game.ExportSaveJson();
        var replayed = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(json, replayed.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replayed.CurrentHash);
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
        if (!forecast.CanArriveUnderAssumptions) return;
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
