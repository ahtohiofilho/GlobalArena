using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs10CanonicalPhysicalLoopTests
{
    private static CanonicalPhysicalTacticalSession NewGame() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0));

    [Fact]
    public void CanonicalSessionIsOnlyLiveSourceAndAdvancesOneRevisionPerTick()
    {
        var game = NewGame();
        var before = game.Game.Snapshot;
        Assert.Equal(1UL, before.Revision);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.State.Stocks);
        Assert.Equal(9U, game.CurrentHash.FormatVersion);

        for (var i = 0; i < 5; i++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.Equal(6UL, game.Game.Snapshot.Revision);
        Assert.Equal(5UL, game.State.MovementTurn);
        Assert.Empty(game.State.Stocks);
        Assert.Empty(game.State.Cycles);

        var eventBeforeCycle = game.CurrentHash;
        var cycle = game.AdvanceMovementTurn();
        Assert.NotNull(cycle);
        Assert.Equal(6UL, cycle.MovementTurn);
        Assert.Equal(7UL, game.Game.Snapshot.Revision);
        Assert.Single(game.State.Cycles);
        Assert.NotEmpty(game.State.Stocks);
        Assert.NotEqual(eventBeforeCycle, game.CurrentHash);
        Assert.Same(game.State, game.Game.Snapshot.PhysicalEconomy);
        Assert.Empty(game.Game.Snapshot.Economy.EconomicPoints);
        Assert.False(game.Game.Snapshot.Population.IsInitialized);
        Assert.Equal(6, game.Events.Count);
        Assert.Equal(game.CurrentHash.HexDigest, game.Events[^1].AfterHashHex);
        Assert.All(game.Events, x => Assert.Equal(x.BeforeRevision + 1UL, x.AfterRevision));
    }

    [Fact]
    public void MovementConservesWorkersWithoutInventingStrategicResidentsOrFreeTrade()
    {
        var game = NewGame();
        var start = game.InitialTile;
        var target = game.AdjacentWorkable(start).First();
        var frozen = game.Game.Snapshot;
        game.MoveWorkers(start, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);

        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(5UL, game.WorkersAt(start));
        Assert.Equal(3UL, game.WorkersAt(target));
        Assert.Equal(2UL, game.Game.Snapshot.Revision);
        Assert.NotSame(frozen, game.Game.Snapshot);
        Assert.Equal(8UL, frozen.PhysicalEconomy!.TotalWorkers);
        Assert.Single(frozen.PhysicalEconomy.Allocations.Select(x => x.TileId).Distinct());

        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        Assert.Equal(0UL, game.StockAt(target, new CommodityId(1U)));
        Assert.True(game.StockAt(start, new CommodityId(2U)) > 0UL);
        Assert.True(game.StockAt(start, new CommodityId(3U)) > 0UL);
        Assert.Empty(game.Game.Snapshot.Economy.EconomicPoints);
    }

    [Fact]
    public void CanonicalReducerMatchesValidatedVs07ProductionConsumptionAndStocks()
    {
        var game = NewGame();
        var reference = new EconomicPhysicalTacticalOfflineSession(
            20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        var start = game.InitialTile;
        Assert.Equal(reference.Workers.InitialTile, start);
        var target = game.AdjacentWorkable(start).First();
        game.MoveWorkers(start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        reference.MoveWorkers(start, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);

        for (var i = 0; i < 24; i++)
        {
            var actual = game.AdvanceMovementTurn();
            var expected = reference.AdvanceMovementTurn();
            Assert.Equal(expected is null, actual is null);
            if (expected is not null)
                Assert.Equal(new PhysicalEconomicCycle(expected.MovementTurn,
                    expected.ProducedRaw, expected.FoodConsumedRaw,
                    expected.FoodUnmetRaw), actual);
        }
        Assert.Equal(reference.MovementTurn, game.State.MovementTurn);
        Assert.Equal(reference.Workers.Revision, game.State.WorkerRevision);
        Assert.Equal(reference.Stocks.Select(x =>
            new PhysicalCommodityStock(x.TileId, x.Commodity, x.RawQuantity)),
            game.State.Stocks);
        Assert.Equal(reference.Cycles.Select(x =>
            new PhysicalEconomicCycle(x.MovementTurn, x.ProducedRaw,
                x.FoodConsumedRaw, x.FoodUnmetRaw)), game.State.Cycles);
    }

    [Fact]
    public void V9SaveCanReopenContinueAndReproduceIdenticalHashAndEventJournal()
    {
        var game = NewGame();
        var target = game.AdjacentWorkable(game.InitialTile).First();
        game.MoveWorkers(game.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        for (var i = 0; i < 15; i++) game.AdvanceMovementTurn();
        var saved = game.ExportSaveJson();
        var reloaded = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(saved, reloaded.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reloaded.CurrentHash);
        Assert.Equal(game.Game.Snapshot.Revision, reloaded.Game.Snapshot.Revision);
        Assert.Equal(game.Events, reloaded.Events);
        for (var i = 0; i < 9; i++)
        {
            game.AdvanceMovementTurn();
            reloaded.AdvanceMovementTurn();
        }
        Assert.Equal(24UL, reloaded.State.MovementTurn);
        Assert.Equal(4, reloaded.State.Cycles.Count);
        Assert.Equal(game.CurrentHash, reloaded.CurrentHash);
        Assert.Equal(game.ExportSaveJson(), reloaded.ExportSaveJson());
    }

    [Fact]
    public void ExplicitVs07MigrationMatchesStocksWorkersAndFutureCycles()
    {
        var old = new EconomicPhysicalTacticalOfflineSession(
            20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        var target = old.Workers.AdjacentWorkable(old.Workers.InitialTile).First();
        old.MoveWorkers(old.Workers.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        for (var i = 0; i < 12; i++) old.AdvanceMovementTurn();

        var legacyJson = old.ExportSaveJson();
        var migrated = CanonicalPhysicalTacticalSession.MigrateVs07SaveJson(legacyJson);
        Assert.Equal(12UL, migrated.State.MovementTurn);
        Assert.Equal(old.Workers.TotalWorkers, migrated.State.TotalWorkers);
        Assert.Equal(old.Stocks.Select(x => new PhysicalCommodityStock(
            x.TileId, x.Commodity, x.RawQuantity)), migrated.State.Stocks);
        Assert.Equal(old.Cycles.Select(x => new PhysicalEconomicCycle(
            x.MovementTurn, x.ProducedRaw, x.FoodConsumedRaw, x.FoodUnmetRaw)),
            migrated.State.Cycles);
        Assert.Equal(legacyJson, old.ExportSaveJson()); // no overwrite
        var version9Json = migrated.ExportSaveJson();
        Assert.Equal(version9Json,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(version9Json).ExportSaveJson());
    }

    [Fact]
    public void InvalidRelocationIsAtomicAndDoesNotAppendAnEvent()
    {
        var game = NewGame();
        var before = game.Game.Snapshot;
        var saved = game.ExportSaveJson();
        var target = game.AdjacentWorkable(game.InitialTile).First();
        Assert.Throws<InvalidOperationException>(() => game.MoveWorkers(
            game.InitialTile, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 5UL));
        Assert.Throws<InvalidOperationException>(() => game.MoveWorkers(
            game.InitialTile, game.InitialTile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL));
        Assert.Throws<ArgumentException>(() => game.MoveWorkers(
            game.InitialTile, new PhysicalTacticalTileId(
                new GoldbergParameters(12, 0), new StrategicCellId(1UL)),
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL));
        Assert.Same(before, game.Game.Snapshot);
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }

    [Fact]
    public void CanonicalSessionRejectsStalePhysicalOverwriteOrRemoval()
    {
        var game = NewGame();
        var baseline = game.Game.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            game.Game.AcceptSnapshot(WorldState.CreateBound(game.Game.GeneratedWorld)
                .AdvanceRevision()));
        var sameRevision = baseline.WithPhysicalEconomy(game.State);
        Assert.Throws<InvalidOperationException>(() =>
            game.Game.AcceptSnapshot(sameRevision));
        Assert.Same(baseline, game.Game.Snapshot);
        game.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            new CanonicalPhysicalTacticalSession(
                game.Game, new GoldbergParameters(18, 0)));
        Assert.Equal(1UL, game.State.MovementTurn);
    }

    [Fact]
    public void ChecksumAndSelfHashedFabricatedEventsAreRejected()
    {
        var game = NewGame();
        game.AdvanceMovementTurn();
        var valid = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(valid)!;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                JsonSerializer.Serialize(envelope with {
                    PayloadSha256Hex = new string('0', 64) })));

        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            envelope.PayloadJson)!;
        var forgedEvents = payload.Actions.Select((x, index) =>
            index == 0 ? x with { AfterHashHex = new string('A', 64) } : x).ToArray();
        var forgedPayloadJson = JsonSerializer.Serialize(payload with { Actions = forgedEvents });
        var forgedEnvelope = envelope with {
            PayloadJson = forgedPayloadJson,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(forgedPayloadJson)))
        };
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                JsonSerializer.Serialize(forgedEnvelope)));
    }
}
