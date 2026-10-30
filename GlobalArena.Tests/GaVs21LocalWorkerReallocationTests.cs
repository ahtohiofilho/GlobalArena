using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs21LocalWorkerReallocationTests
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
    public void LocalJobChangePreservesPopulationCashStocksAndTimeAndReplaysV14()
    {
        var game = Start();
        var tile = game.InitialTile;
        var before = game.State;
        var originalHash = game.CurrentHash;
        var originalAccounts = before.FoodCash!.Accounts.ToArray();
        var originalStocks = before.Stocks.ToArray();

        game.ReassignWorkers(tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        var state = game.State;
        var settlement = Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);

        Assert.Equal(8UL, state.TotalWorkers);
        Assert.Equal(0UL, state.WorkersInTransit);
        Assert.Equal(before.MovementTurn, state.MovementTurn);
        Assert.Equal(before.WorkerRevision + 1UL, state.WorkerRevision);
        Assert.True(state.Cycles.SequenceEqual(before.Cycles));
        Assert.True(state.Transits.SequenceEqual(before.Transits));
        Assert.True(state.Stocks.SequenceEqual(originalStocks));
        Assert.True(state.FoodCash!.Accounts.SequenceEqual(originalAccounts));
        Assert.Equal(before.FoodCash.InitialMoneySupplyMicrocredits,
            state.FoodCash.InitialMoneySupplyMicrocredits);
        Assert.Equal(tile, settlement.Tile);
        Assert.Equal(2UL, settlement.AgriculturalWorkers);
        Assert.Equal(4UL, settlement.ForestryWorkers);
        Assert.Equal(2UL, settlement.MiningWorkers);
        Assert.Equal(8UL, settlement.Workers);
        Assert.Equal(320_000_000UL, settlement.TotalLocalMoney);
        Assert.Equal("reassign", Assert.Single(game.Events).Kind);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.NotEqual(originalHash, game.CurrentHash);

        var save = game.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(settlement, Assert.Single(
            EmergentSettlementProjection.FromSession(restored).Sites));
    }

    [Fact]
    public void InvalidReallocationsNeverMutateJournalOrWorld()
    {
        var game = Start();
        var tile = game.InitialTile;
        var hash = game.CurrentHash;
        var save = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() => game.ReassignWorkers(
            tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Agriculture, 1UL));
        Assert.Throws<InvalidOperationException>(() => game.ReassignWorkers(
            tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 0UL));
        Assert.Throws<InvalidOperationException>(() => game.ReassignWorkers(
            tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 5UL));
        Assert.Throws<InvalidOperationException>(() => game.ReassignWorkers(
            tile, EconomicActivityKind.Mining,
            EconomicActivityKind.Forestry, 3UL));
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }

    [Fact]
    public void ReassignDuringJourneyKeepsTransitAndSecondSettlementAfterArrival()
    {
        var game = Start();
        var from = game.InitialTile;
        var destination = game.AdjacentWorkable(from)[0];
        game.OrderWorkerTransit(from, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        var journey = game.State.Transits.ToArray();
        game.ReassignWorkers(from,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 1UL);

        var beforeArrival = EmergentSettlementProjection.FromSession(game);
        Assert.Equal(6UL, beforeArrival.TotalResidentWorkers);
        Assert.Equal(2UL, beforeArrival.TotalTravellers);
        Assert.Null(beforeArrival.At(destination));
        Assert.True(game.State.Transits.SequenceEqual(journey));
        Assert.Equal(1UL, beforeArrival.At(from)!.AgriculturalWorkers);
        Assert.Equal(3UL, beforeArrival.At(from)!.MiningWorkers);

        var snapshot = game.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(snapshot);
        Assert.Equal(snapshot, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);

        for (var i = 0; i < 2; i++)
        {
            game.AdvanceMovementTurn();
            restored.AdvanceMovementTurn();
        }
        var arrived = EmergentSettlementProjection.FromSession(game);
        Assert.Equal(2, arrived.Sites.Count);
        Assert.Equal(8UL, arrived.TotalResidentWorkers);
        Assert.Equal(0UL, arrived.TotalTravellers);
        Assert.Equal(2UL, arrived.At(destination)!.ForestryWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(game.ExportSaveJson(), restored.ExportSaveJson());
    }

    [Fact]
    public void LegacyV13CanReplaySameTileActionWithoutNewCivilization()
    {
        var game = Start(born: false);
        game.ReassignWorkers(game.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        var save = game.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(13U, game.CurrentHash.FormatVersion);
        Assert.Null(Assert.Single(
            EmergentSettlementProjection.FromSession(restored).Sites).Community);
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(save, restored.ExportSaveJson());
    }
}
