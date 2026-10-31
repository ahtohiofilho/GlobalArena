using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs33PhysicalJourneyFoodAndSaveV16Tests
{
    private static readonly CommodityId Food = new(1U);

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static PhysicalTacticalTileId FarTile(CanonicalPhysicalTacticalSession game)
    {
        var near = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !near.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    private static CanonicalPhysicalTacticalSession WithFood()
    {
        var game = Start();
        game.ReassignWorkers(game.InitialTile, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, 2UL);
        game.ReassignWorkers(game.InitialTile, EconomicActivityKind.Mining,
            EconomicActivityKind.Agriculture, 2UL);
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        Assert.True(game.StockAt(game.InitialTile, Food) > 0UL);
        return game;
    }

    private static PhysicalLongDistanceTransit Order(
        CanonicalPhysicalTacticalSession game, ulong foodRaw) =>
        game.OrderProvisionedLongDistanceTransit(
            game.InitialTile, FarTile(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, foodRaw);

    [Fact]
    public void DeparturePurchasesRealFoodMovesExistingCashAndDoesNotChargeTwice()
    {
        var game = WithFood();
        var tile = game.InitialTile;
        var foodRaw = Math.Min(game.StockAt(tile, Food), 2_000_000UL);
        var stockBefore = game.StockAt(tile, Food);
        var cashBefore = game.FoodCash!.At(tile);
        var moneySupply = game.FoodCash.InitialMoneySupplyMicrocredits;
        var totalWorkers = game.State.TotalWorkers;
        var originalDemand = game.FoodDemandAt(tile);

        var order = Order(game, foodRaw);
        var lot = Assert.Single(game.State.TravelFoodLedgers!);
        var cashAfter = game.FoodCash!.At(tile);
        Assert.Equal(order.OrderId, lot.OrderId);
        Assert.Equal(foodRaw, lot.FoodPurchasedRaw);
        Assert.Equal(foodRaw, lot.FoodRemainingRaw);
        Assert.Equal(0UL, lot.FoodConsumedRaw);
        Assert.Equal(stockBefore - foodRaw, game.StockAt(tile, Food));
        Assert.Equal(cashBefore.HouseholdMicrocredits - lot.PaidMicrocredits,
            cashAfter.HouseholdMicrocredits);
        Assert.Equal(cashBefore.ProducerMicrocredits + lot.PaidMicrocredits,
            cashAfter.ProducerMicrocredits);
        Assert.Equal(moneySupply, game.FoodCash.InitialMoneySupplyMicrocredits);
        Assert.Equal(PhysicalJourneyFoodLedger.PurchasePayment(
            foodRaw, lot.UnitPriceMicrocredits), lot.PaidMicrocredits);
        Assert.Equal(originalDemand - CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            game.FoodDemandAt(tile));
        Assert.Equal(totalWorkers, game.State.TotalWorkers);
        Assert.Equal(16, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!.Version);

        game.AdvanceMovementTurn();
        var afterCycle = Assert.Single(game.State.TravelFoodLedgers!);
        Assert.True(afterCycle.FoodConsumedRaw > 0UL);
        Assert.Equal(lot.FoodPurchasedRaw,
            afterCycle.FoodRemainingRaw + afterCycle.FoodConsumedRaw);
        Assert.Equal(stockBefore - foodRaw +
            (ulong)game.PrimitiveYieldAt(tile, EconomicActivityKind.Agriculture) *
                (totalWorkers - 1UL) -
            (game.State.Cycles[^1].FoodConsumedRaw - afterCycle.FoodConsumedRaw),
            game.StockAt(tile, Food));
    }

    [Fact]
    public void ArrivalRetainsUnusedPrivateFoodOutsideCommercialStockAndReplaysExactly()
    {
        var game = WithFood();
        var loaded = Math.Min(game.StockAt(game.InitialTile, Food), 8_000_000UL);
        var order = Order(game, loaded);
        var afterDeparture = game.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(afterDeparture);
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(afterDeparture, reopened.ExportSaveJson());

        while (game.State.MovementTurn < order.ArrivalTurn)
        {
            game.AdvanceMovementTurn();
            reopened.AdvanceMovementTurn();
            Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        }
        Assert.Empty(game.State.LongTrips);
        var lot = Assert.Single(game.State.TravelFoodLedgers!);
        Assert.True(lot.Arrived);
        Assert.Equal(loaded, lot.FoodRemainingRaw + lot.FoodConsumedRaw);
        Assert.Equal(order.Destination, lot.Destination);
        Assert.Equal(game.ExportSaveJson(), reopened.ExportSaveJson());
        // The unused lot remains privately owned and was not offered
        // for sale at the destination. Local production may add its own stock.
        Assert.Equal(0UL, game.StockAt(order.Destination, Food));
        Assert.Equal(game.State.TotalWorkers, reopened.State.TotalWorkers);
    }

    [Fact]
    public void FailedAcquisitionIsAtomicAndDoesNotInventFoodOrCredit()
    {
        var game = WithFood();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        Assert.Throws<InvalidOperationException>(() => Order(game, 0UL));
        Assert.Throws<InvalidOperationException>(() =>
            Order(game, checked(game.StockAt(game.InitialTile, Food) + 1UL)));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Null(game.State.TravelFoodLedgers);
    }

    [Fact]
    public void MixedV15AndV16TravellersHaveExactlyOneFoodDemandEach()
    {
        var game = WithFood();
        var source = game.InitialTile;
        var originalDemand = game.FoodDemandAt(source);
        game.OrderLongDistanceTransit(source, FarTile(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(originalDemand, game.FoodDemandAt(source));
        var quantity = Math.Min(game.StockAt(source, Food), 1_000_000UL);
        Order(game, quantity);
        Assert.Equal(originalDemand -
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            game.FoodDemandAt(source));
        Assert.Equal(2UL, game.State.WorkersInTransit);
        Assert.Equal(2, game.State.LongTrips.Count);
        Assert.Single(game.State.TravelFoodLedgers!);
        var payload = game.ExportSaveJson();
        Assert.Equal(payload,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(payload).ExportSaveJson());
    }

    [Fact]
    public void LegacyV15JourneyKeepsIdenticalSaveVersionAndReplayBytes()
    {
        var game = Start();
        game.OrderLongDistanceTransit(game.InitialTile, FarTile(game),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        game.AdvanceMovementTurn();
        var json = game.ExportSaveJson();
        Assert.Equal(15, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            json)!.Version);
        Assert.Null(game.State.TravelFoodLedgers);
        Assert.Equal(json, CanonicalPhysicalTacticalSession.RestoreSaveJson(json)
            .ExportSaveJson());
        Assert.DoesNotContain("FoodRaw", json);
        Assert.DoesNotContain("TravelFoodLedgers", json);
    }
}
