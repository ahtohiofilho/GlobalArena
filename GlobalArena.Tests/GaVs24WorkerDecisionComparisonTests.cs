using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs24WorkerDecisionComparisonTests
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

    private static PhysicalWorkerScenarioOutcome ReadOutcome(
        CanonicalPhysicalTacticalSession session, PhysicalTacticalTileId tile) =>
        new(
            PhysicalEconomicCycleProgression.SummarizeRecent(session),
            session.StockAt(tile, new CommodityId(1U)),
            session.StockAt(tile, new CommodityId(3U)),
            session.StockAt(tile, new CommodityId(2U)));

    [Fact]
    public void HypotheticalWorkforceComparisonDoesNotAlterTheLiveGame()
    {
        var live = Start();
        var tile = live.InitialTile;
        var firstSave = live.ExportSaveJson();
        var firstHash = live.CurrentHash;
        var firstJournal = live.Events.ToArray();

        var comparison = PhysicalWorkerDecisionProjection.Compare(live, tile,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);

        Assert.Equal(0UL, comparison.StartingMovementTurn);
        Assert.Equal(5UL, comparison.ProjectedFinalMovementTurn);
        Assert.Equal(2UL, comparison.Workers);
        Assert.Equal(5, comparison.Unchanged.FoodService.CycleCount);
        Assert.Equal(5, comparison.Reassigned.FoodService.CycleCount);
        Assert.Equal(firstSave, live.ExportSaveJson());
        Assert.Equal(firstHash, live.CurrentHash);
        Assert.Equal(firstJournal, live.Events);
        Assert.Equal(0UL, live.State.MovementTurn);
        Assert.Empty(live.State.Cycles);
        Assert.Equal(8UL, live.State.TotalWorkers);
        Assert.Equal(4UL, live.WorkersAt(tile) -
            live.State.Allocations.Where(a => a.TileId == tile &&
                a.Activity != EconomicActivityKind.Agriculture)
                .Aggregate(0UL, (sum, a) => checked(sum + a.Workers)));

        // Compare with what the actual canonical actions do in both worlds.
        var unchanged = CanonicalPhysicalTacticalSession.RestoreSaveJson(firstSave);
        var modified = CanonicalPhysicalTacticalSession.RestoreSaveJson(firstSave);
        modified.ReassignWorkers(tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        PhysicalEconomicCycleProgression.AdvanceBatch(unchanged);
        PhysicalEconomicCycleProgression.AdvanceBatch(modified);
        Assert.Equal(ReadOutcome(unchanged, tile), comparison.Unchanged);
        Assert.Equal(ReadOutcome(modified, tile), comparison.Reassigned);
        Assert.True(comparison.Reassigned.LocalWoodStockRaw >
                    comparison.Unchanged.LocalWoodStockRaw);
        Assert.Equal(8UL, modified.State.TotalWorkers);
        Assert.Equal(320_000_000UL,
            modified.FoodCash!.Accounts.Aggregate(0UL, (sum, account) =>
                checked(sum + account.HouseholdMicrocredits +
                    account.ProducerMicrocredits)));
        Assert.Empty(modified.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void SameWorkerChoiceCanBeProjectedAfterTravelAndAcrossCadenceBoundary()
    {
        var game = Start();
        var origin = game.InitialTile;
        game.OrderWorkerTransit(origin, game.AdjacentWorkable(origin)[0],
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 2UL);
        for (var i = 0; i < 30; i++) game.AdvanceMovementTurn();

        var before = game.ExportSaveJson();
        var beforeHash = game.CurrentHash;
        var preview = PhysicalWorkerDecisionProjection.Compare(game, origin,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Equal(30UL, preview.StartingMovementTurn);
        Assert.Equal(40UL, preview.ProjectedFinalMovementTurn);
        Assert.Equal(32UL, preview.Unchanged.FoodService.FirstSettlementTurn);
        Assert.Equal(40UL, preview.Reassigned.FoodService.LastSettlementTurn);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(beforeHash, game.CurrentHash);
        Assert.Equal(8UL, game.State.TotalWorkers);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(preview, PhysicalWorkerDecisionProjection.Compare(reopened,
            origin, EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL));
    }

    [Fact]
    public void InvalidOptionsRejectWithoutModifyingSourceAndLegacyV13Works()
    {
        var game = Start();
        var tile = game.InitialTile;
        var before = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalWorkerDecisionProjection.Compare(game, tile,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Agriculture, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalWorkerDecisionProjection.Compare(game, tile,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 0UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalWorkerDecisionProjection.Compare(game, tile,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 20UL));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Empty(game.Events);

        var v13 = Start(born: false);
        var v13Before = v13.ExportSaveJson();
        var forecast = PhysicalWorkerDecisionProjection.Compare(v13,
            v13.InitialTile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 1UL);
        Assert.Equal(5UL, forecast.ProjectedFinalMovementTurn);
        Assert.Equal(v13Before, v13.ExportSaveJson());
        Assert.Equal(13U, v13.CurrentHash.FormatVersion);
        Assert.Null(Assert.Single(
            EmergentSettlementProjection.FromSession(v13).Sites).Community);
    }
}
