using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs07PhysicalTacticalOfflineLoopTests
{
    private static EconomicPhysicalTacticalOfflineSession NewGame() =>
        new(20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));

    [Fact]
    public void SixMovementTurnsTriggerExactlyOneRealPhysicalEconomicCycle()
    {
        var game = NewGame();
        var initial = game.Workers.InitialTile;
        Assert.Empty(game.Stocks);
        for (var i = 0; i < 5; i++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.Equal(5UL, game.MovementTurn);
        Assert.Equal(0UL, game.EconomicCycles);
        Assert.Empty(game.Stocks);

        var preview = game.Workers.Preview();
        var cycle = game.AdvanceMovementTurn();
        Assert.NotNull(cycle);
        Assert.Equal(6UL, cycle.MovementTurn);
        Assert.Equal(1UL, game.EconomicCycles);
        Assert.Equal(preview.Supply.Aggregate(0UL,
            (sum, x) => checked(sum + x.RawQuantity)), cycle.ProducedRaw);
        var requested = preview.Demand.Aggregate(0UL,
            (sum, x) => checked(sum + x.RawQuantity));
        Assert.Equal(requested, checked(cycle.FoodConsumedRaw + cycle.FoodUnmetRaw));
        Assert.Equal(8UL, game.Workers.TotalWorkers);
        Assert.Equal(0UL, game.Workers.Revision);

        // Primitive wood and stone remain in their ORIGINAL physical tile,
        // not in a fictional strategic sector or a global warehouse.
        var wood = game.StockAt(initial, new CommodityId(3U));
        var stone = game.StockAt(initial, new CommodityId(2U));
        Assert.True(wood > 0);
        Assert.True(stone > 0);
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        Assert.Equal(12UL, game.MovementTurn);
        Assert.Equal(2UL, game.EconomicCycles);
        Assert.Equal(checked(wood * 2UL), game.StockAt(initial, new CommodityId(3U)));
        Assert.Equal(checked(stone * 2UL), game.StockAt(initial, new CommodityId(2U)));
    }

    [Fact]
    public void WorkersProduceOnTheirOwnPhysicalTilesWithoutFreeTrade()
    {
        var game = NewGame();
        var start = game.Workers.InitialTile;
        var target = game.Workers.AdjacentWorkable(start).First();
        game.MoveWorkers(start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        Assert.Equal(8UL, game.Workers.TotalWorkers);
        Assert.Equal(1UL, game.Workers.Revision);

        for (var t = 0; t < 6; t++) game.AdvanceMovementTurn();
        Assert.Equal(6UL, game.MovementTurn);
        Assert.Equal(2, game.Workers.Preview().Demand.Count);
        Assert.Equal(5UL, game.Workers.WorkersAt(start));
        Assert.Equal(3UL, game.Workers.WorkersAt(target));
        Assert.Equal(8UL, game.Workers.StrategicDisplayTotals().Values.Aggregate(
            0UL, (sum, x) => checked(sum + x)));
        Assert.Equal(0UL, game.StockAt(target, new CommodityId(1U)));
        // Foresting at destination does not create food, transport or a city.
        Assert.Equal(0UL, game.Stocks.Where(x =>
            x.TileId == target && x.Commodity.Value == 1U)
            .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity)));
    }

    [Fact]
    public void SavingThenReopeningReconstructsExactWorldStateAndFutureTurns()
    {
        var game = NewGame();
        var start = game.Workers.InitialTile;
        var target = game.Workers.AdjacentWorkable(start).First();
        game.MoveWorkers(start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        for (var t = 0; t < 15; t++) game.AdvanceMovementTurn();
        var text = game.ExportSaveJson();
        var restored = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(text);
        Assert.Equal(text, restored.ExportSaveJson());
        Assert.Equal(15UL, restored.MovementTurn);
        Assert.Equal(2UL, restored.EconomicCycles);
        Assert.Equal(game.Workers.Assignments, restored.Workers.Assignments);
        Assert.Equal(game.Stocks, restored.Stocks);
        for (var t = 0; t < 9; t++)
        {
            game.AdvanceMovementTurn();
            restored.AdvanceMovementTurn();
        }
        Assert.Equal(24UL, restored.MovementTurn);
        Assert.Equal(4UL, restored.EconomicCycles);
        Assert.Equal(game.ExportSaveJson(), restored.ExportSaveJson());
    }

    [Fact]
    public void TamperedAndNonReplayableSavesAreRejected()
    {
        var game = NewGame();
        for (var t = 0; t < 6; t++) game.AdvanceMovementTurn();
        var valid = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<TacticalOfflineEnvelope>(valid)!;
        var badDigest = envelope with { PayloadSha256Hex = new string('0', 64) };
        Assert.Throws<InvalidOperationException>(() =>
            EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
                JsonSerializer.Serialize(badDigest)));
        var payload = JsonSerializer.Deserialize<TacticalOfflinePayload>(envelope.PayloadJson)!;
        var falsified = payload with { TotalWorkers = 9999UL };
        var forgedContent = JsonSerializer.Serialize(falsified);
        var hashed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(forgedContent)));
        var forged = envelope with { PayloadJson = forgedContent, PayloadSha256Hex = hashed };
        Assert.Throws<InvalidOperationException>(() =>
            EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
                JsonSerializer.Serialize(forged)));

        Assert.Throws<InvalidOperationException>(() =>
            EconomicPhysicalTacticalOfflineSession.RestoreSaveJson("{oops"));
        Assert.Throws<InvalidOperationException>(() =>
            EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
                new string('x', 4_000_001)));
        Assert.Equal(8UL, game.Workers.TotalWorkers);
        Assert.Equal(1UL, game.EconomicCycles);
    }

    [Fact]
    public void WorkerMoveOrdersReplayAndFailAtomically()
    {
        var game = NewGame();
        var start = game.Workers.InitialTile;
        var target = game.Workers.AdjacentWorkable(start).First();
        var snapshot = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() => game.MoveWorkers(
            start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 5UL));
        Assert.Equal(snapshot, game.ExportSaveJson());

        game.MoveWorkers(start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        Assert.Equal(3UL, game.Workers.WorkersAt(target));
        var replay = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
            game.ExportSaveJson());
        Assert.Equal(3UL, replay.Workers.WorkersAt(target));
        Assert.Equal(8UL, replay.Workers.TotalWorkers);
        Assert.Equal(0UL, replay.EconomicCycles);
    }

    [Fact]
    public void SeparateGamesAndWorldsRejectForeignPhysicalTiles()
    {
        var game = NewGame();
        var wrong = new PhysicalTacticalTileId(
            new GoldbergParameters(12, 0), new StrategicCellId(1UL));
        Assert.Throws<ArgumentException>(() =>
            game.StockAt(wrong, new CommodityId(1U)));
        Assert.Empty(game.Stocks);
        Assert.Equal(0UL, game.MovementTurn);
    }
}
