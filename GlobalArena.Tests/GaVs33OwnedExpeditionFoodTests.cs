using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs33OwnedExpeditionFoodTests
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

    private static PhysicalTacticalTileId Remote(
        CanonicalPhysicalTacticalSession game)
    {
        var near = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !near.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    private static CanonicalPhysicalTacticalSession FundLocalFood()
    {
        var game = Start();
        game.ReassignWorkers(game.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 2UL);
        game.ReassignWorkers(game.InitialTile,
            EconomicActivityKind.Mining, EconomicActivityKind.Agriculture, 2UL);
        var target = Remote(game);
        for (var i = 0; i < 40; i++)
        {
            var quote = PhysicalJourneySupplyProjection.PreviewPlanned(game,
                game.InitialTile, target, EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 1UL);
            if (game.StockAt(game.InitialTile, new CommodityId(1U)) >=
                quote.TravellersBaselineFoodDemandRaw &&
                game.FoodCash!.At(game.InitialTile).HouseholdMicrocredits > 0UL)
                return game;
            game.AdvanceMovementTurn();
        }
        throw new InvalidOperationException(
            "The canonical founding tile could not accumulate a legitimate producer food stock.");
    }

    [Fact]
    public void PurchaseTransfersExactProducerFoodAndCashWithNoFreeReserves()
    {
        var game = FundLocalFood();
        var source = game.InitialTile;
        var target = Remote(game);
        var oldFood = game.StockAt(source, new CommodityId(1U));
        var oldWallet = game.FoodCash!.At(source);
        var oldMoney = Money(game);
        var expected = PhysicalJourneySupplyProjection.PreviewPlanned(
            game, source, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL).TravellersBaselineFoodDemandRaw;
        Assert.True(oldFood >= expected);
        var result = game.OrderProvisionedLongDistanceTransit(
            source, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(expected, result.FoodRaw);
        Assert.Equal(16U, game.CurrentHash.FormatVersion);
        Assert.Single(game.State.ExpeditionFood);
        Assert.Equal(expected, game.State.ExpeditionFood[0].RemainingRaw);
        var receipt = Assert.Single(game.State.ExpeditionPurchases);
        Assert.Equal(result.Journey.OrderId, receipt.OrderId);
        Assert.Equal(expected, receipt.PurchasedRaw);
        Assert.Equal(result.PaidMicrocredits, receipt.PaidMicrocredits);
        Assert.Equal(checked(oldFood - expected),
            game.StockAt(source, new CommodityId(1U)));
        Assert.Equal(oldFood, checked(game.StockAt(source, new CommodityId(1U)) +
            game.State.ExpeditionFood[0].RemainingRaw));
        var wallet = game.FoodCash.At(source);
        Assert.Equal(oldWallet.HouseholdMicrocredits - result.PaidMicrocredits,
            wallet.HouseholdMicrocredits);
        Assert.Equal(oldWallet.ProducerMicrocredits + result.PaidMicrocredits,
            wallet.ProducerMicrocredits);
        Assert.Equal(oldMoney, Money(game));
        Assert.Equal(7UL * CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            game.FoodDemandAt(source));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal("depart_remote_food", game.Events[^1].Kind);
        Assert.Equal(expected, game.Events[^1].FoodRaw);
        Assert.Equal(16, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!.Version);
        Assert.Equal(game.ExportSaveJson(),
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                game.ExportSaveJson()).ExportSaveJson());
    }

    [Fact]
    public void TravellerEatsOnlyFromOwnedCargoAndV16ReplaysMidrouteAndAfterArrival()
    {
        var game = FundLocalFood();
        var result = game.OrderProvisionedLongDistanceTransit(
            game.InitialTile, Remote(game), EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL);
        var expectedRemaining = result.FoodRaw;
        var money = Money(game);
        var lastCycleCount = game.State.Cycles.Count;
        while (game.State.LongTrips.Any(x => x.OrderId == result.Journey.OrderId))
        {
            game.AdvanceMovementTurn();
            if (game.State.Cycles.Count != lastCycleCount &&
                game.State.LongTrips.Any(x => x.OrderId == result.Journey.OrderId))
                expectedRemaining -= CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker;
            lastCycleCount = game.State.Cycles.Count;
            var parcel = game.State.ExpeditionFood.FirstOrDefault(
                x => x.OrderId == result.Journey.OrderId);
            if (parcel is not null)
                Assert.Equal(expectedRemaining, parcel.RemainingRaw);
            Assert.Equal(money, Money(game));
            Assert.Equal(8UL, game.State.TotalWorkers);
            Assert.Equal(16U, game.CurrentHash.FormatVersion);
            Assert.Empty(game.Game.Snapshot.Territory.Controls);
            var save = game.ExportSaveJson();
            var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
            Assert.Equal(save, reopened.ExportSaveJson());
            Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        }
        Assert.Empty(game.State.ExpeditionFood);
        Assert.Single(game.State.ExpeditionPurchases);
        Assert.Equal(1UL, game.WorkersAt(result.Journey.Destination));
        Assert.Equal(result.Journey.ArrivalTurn, game.State.MovementTurn);
        Assert.Equal(16, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!.Version);
    }

    [Fact]
    public void NoFoodOrBudgetRefusesExpeditionAtomicallyAndOldV15IsUnmodified()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.OrderProvisionedLongDistanceTransit(
                game.InitialTile, Remote(game), EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 1UL));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        Assert.Empty(game.State.ExpeditionPurchases);
        Assert.Empty(game.State.ExpeditionFood);

        var old = game.OrderLongDistanceTransit(
            game.InitialTile, Remote(game), EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(15U, game.CurrentHash.FormatVersion);
        Assert.Equal(15, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!.Version);
        Assert.Equal("depart_remote", game.Events[^1].Kind);
        Assert.Equal(0UL, game.Events[^1].FoodRaw);
        Assert.Equal(8UL * CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            game.FoodDemandAt(game.InitialTile));
        Assert.Equal(game.ExportSaveJson(),
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                game.ExportSaveJson()).ExportSaveJson());
    }

    [Fact]
    public void OldV15AndNewV16TripsCanCoexistWithSeparateFoodDemandPolicies()
    {
        var game = FundLocalFood();
        var source = game.InitialTile;
        var target = Remote(game);
        game.OrderLongDistanceTransit(source, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        game.OrderProvisionedLongDistanceTransit(source, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(6UL * CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker +
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker,
            game.FoodDemandAt(source)); // 6 residents + one old V15 traveller
        Assert.Equal(2UL, game.State.WorkersInTransit);
        Assert.Equal(16U, game.CurrentHash.FormatVersion);
        Assert.Single(game.State.ExpeditionFood);
        Assert.Equal(game.ExportSaveJson(),
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                game.ExportSaveJson()).ExportSaveJson());
    }

    private static ulong Money(CanonicalPhysicalTacticalSession game) =>
        game.FoodCash!.Accounts.Aggregate(0UL,
            (sum, row) => checked(sum +
                row.HouseholdMicrocredits + row.ProducerMicrocredits));
}
