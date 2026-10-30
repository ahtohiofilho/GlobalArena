using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs20EmergentSettlementTests
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

    [Fact]
    public void OriginalWorkforceIsExactlyOneSettlementWithoutAnyFoundedCityOrTerritory()
    {
        var game = Start();
        var hash = game.CurrentHash;
        var save = game.ExportSaveJson();
        var projection = EmergentSettlementProjection.FromSession(game);
        var first = Assert.Single(projection.Sites);
        Assert.Equal(game.InitialTile, first.Tile);
        Assert.Equal(8UL, first.Workers);
        Assert.Equal(4UL, first.AgriculturalWorkers);
        Assert.Equal(2UL, first.ForestryWorkers);
        Assert.Equal(2UL, first.MiningWorkers);
        Assert.Equal(8UL, projection.TotalResidentWorkers);
        Assert.Equal(0UL, projection.TotalTravellers);
        Assert.Equal(8_000_000UL, first.FoodDemandRaw);
        Assert.Equal(320_000_000UL, first.HouseholdMicrocredits);
        Assert.Equal(0UL, first.ProducerMicrocredits);
        Assert.Equal(1UL, first.Community!.Value.Value);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.Empty(game.Events);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
    }

    [Fact]
    public void JourneyCreatesNoPrematureSettlementAndArrivalCreatesOneWithoutDeclaration()
    {
        var game = Start();
        var origin = game.InitialTile;
        var destination = game.AdjacentWorkable(origin)[0];
        game.OrderWorkerTransit(origin, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);

        var departing = EmergentSettlementProjection.FromSession(game);
        Assert.Single(departing.Sites);
        Assert.Null(departing.At(destination));
        Assert.Equal(6UL, departing.TotalResidentWorkers);
        Assert.Equal(2UL, departing.TotalTravellers);
        Assert.Equal(8_000_000UL, departing.At(origin)!.FoodDemandRaw);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        game.AdvanceMovementTurn();
        var middle = EmergentSettlementProjection.FromSession(game);
        Assert.Single(middle.Sites);
        Assert.Null(middle.At(destination));
        Assert.Equal(6UL, middle.TotalResidentWorkers);
        Assert.Equal(2UL, middle.TotalTravellers);

        var midHash = game.CurrentHash;
        var saved = game.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(midHash, reopened.CurrentHash);
        Assert.Equal(saved, reopened.ExportSaveJson());
        Assert.Equal(middle.Sites, EmergentSettlementProjection.FromSession(reopened).Sites);

        game.AdvanceMovementTurn();
        reopened.AdvanceMovementTurn();
        var landed = EmergentSettlementProjection.FromSession(game);
        Assert.Equal(2, landed.Sites.Count);
        Assert.Equal(8UL, landed.TotalResidentWorkers);
        Assert.Equal(0UL, landed.TotalTravellers);
        var second = landed.At(destination)!;
        Assert.NotNull(second);
        Assert.Equal(2UL, second.Workers);
        Assert.Equal(2UL, second.ForestryWorkers);
        Assert.Equal(2_000_000UL, second.FoodDemandRaw);
        Assert.Equal(1UL, second.Community!.Value.Value);
        Assert.Equal(320_000_000UL,
            landed.Sites.Aggregate(0UL, (sum, site) =>
                checked(sum + site.TotalLocalMoney)) + landed.UninhabitedAccountMoney);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.DoesNotContain(game.Events, x => x.Kind == "found");
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(game.ExportSaveJson(), reopened.ExportSaveJson());
        Assert.Equal(landed.Sites, EmergentSettlementProjection.FromSession(reopened).Sites);
    }

    [Fact]
    public void LeavingAnEntireTileRemovesItsSettlementButPreservesItsLedgerAndStocks()
    {
        var game = Start();
        var origin = game.InitialTile;
        var target = game.AdjacentWorkable(origin)[0];
        game.AdvanceMovementTurn();
        var before = EmergentSettlementProjection.FromSession(game);
        Assert.NotEmpty(game.State.Stocks);
        Assert.Equal(1, before.Sites.Count);

        game.OrderWorkerTransit(origin, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Agriculture, 4UL);
        game.OrderWorkerTransit(origin, target,
            EconomicActivityKind.Forestry, EconomicActivityKind.Forestry, 2UL);
        game.OrderWorkerTransit(origin, target,
            EconomicActivityKind.Mining, EconomicActivityKind.Mining, 2UL);
        var departing = EmergentSettlementProjection.FromSession(game);
        Assert.Empty(departing.Sites);
        Assert.Equal(0UL, departing.TotalResidentWorkers);
        Assert.Equal(8UL, departing.TotalTravellers);
        Assert.True(departing.UninhabitedAccountMoney > 0UL);
        Assert.True(departing.UninhabitedStockRecords > 0);
        Assert.Equal(8UL, game.State.TotalWorkers);

        game.AdvanceMovementTurn();
        Assert.Empty(EmergentSettlementProjection.FromSession(game).Sites);
        game.AdvanceMovementTurn();
        var arrived = EmergentSettlementProjection.FromSession(game);
        var site = Assert.Single(arrived.Sites);
        Assert.Equal(target, site.Tile);
        Assert.Equal(8UL, site.Workers);
        Assert.Equal(0UL, arrived.TotalTravellers);
        Assert.True(arrived.UninhabitedStockRecords > 0);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        var totalMoney = arrived.Sites.Aggregate(0UL,
            (sum, x) => checked(sum + x.TotalLocalMoney));
        Assert.Equal(320_000_000UL,
            checked(totalMoney + arrived.UninhabitedAccountMoney));
    }

    [Fact]
    public void LegacyV13StillDisplaysPhysicalSettlementWithoutInventingCivilization()
    {
        var game = Start(born: false);
        var hash = game.CurrentHash;
        var save = game.ExportSaveJson();
        var settlements = EmergentSettlementProjection.FromSession(game);
        Assert.Equal(13U, hash.FormatVersion);
        var first = Assert.Single(settlements.Sites);
        Assert.Null(first.Community);
        Assert.Equal(8UL, first.Workers);
        Assert.Empty(game.Game.Snapshot.Civilizations.Records);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(settlements.Sites,
            EmergentSettlementProjection.FromSession(restored).Sites);
        Assert.Equal(hash, restored.CurrentHash);
    }
}
