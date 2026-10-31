using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs23MulticycleEconomicOutcomesTests
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
    public void FiveEconomicCyclesEqualFiveCanonicalTicksAndProduceReadOnlyFiveCycleWindow()
    {
        var batch = Start();
        var single = Start();
        foreach (var game in new[] { batch, single })
        {
            var origin = game.InitialTile;
            game.ReassignWorkers(origin, EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 1UL);
            game.OrderWorkerTransit(origin, game.AdjacentWorkable(origin)[0],
                EconomicActivityKind.Forestry, EconomicActivityKind.Mining, 2UL);
        }

        var snapshot = batch.ExportSaveJson();
        var hash = batch.CurrentHash;
        var preview = PhysicalEconomicCycleProgression.PreviewBatch(batch);
        Assert.Equal(0UL, preview.StartingMovementTurn);
        Assert.Equal(5UL, preview.FinalSettlementTurn);
        Assert.Equal(5UL, preview.MovementTurnsToAdvance);
        Assert.Equal(5, preview.EconomicCycles);
        Assert.Equal(hash, batch.CurrentHash);
        Assert.Equal(snapshot, batch.ExportSaveJson());
        Assert.Equal(0, PhysicalEconomicCycleProgression.SummarizeRecent(batch).CycleCount);

        var beforeActions = batch.Events.Count;
        Assert.Equal(preview, PhysicalEconomicCycleProgression.AdvanceBatch(batch));
        for (var i = 0; i < 5; i++) single.AdvanceMovementTurn();
        Assert.Equal(beforeActions + 5, batch.Events.Count);
        Assert.Equal(single.CurrentHash, batch.CurrentHash);
        Assert.Equal(single.ExportSaveJson(), batch.ExportSaveJson());
        Assert.Equal(2, EmergentSettlementProjection.FromSession(batch).Sites.Count);
        Assert.Equal(8UL, batch.State.TotalWorkers);
        Assert.Empty(batch.Game.Snapshot.Territory.Controls);

        var stateHash = batch.CurrentHash;
        var stateSave = batch.ExportSaveJson();
        var recent = PhysicalEconomicCycleProgression.SummarizeRecent(batch);
        Assert.Equal(5, recent.CycleCount);
        Assert.Equal(1UL, recent.FirstSettlementTurn);
        Assert.Equal(5UL, recent.LastSettlementTurn);
        Assert.True(recent.GrossProducedRaw > 0UL);
        Assert.True(recent.FoodConsumedRaw + recent.FoodUnmetRaw > 0UL);
        Assert.Equal(batch.State.Cycles.Aggregate(0UL,
            (sum, x) => checked(sum + x.ProducedRaw)), recent.GrossProducedRaw);
        Assert.Equal(batch.State.Cycles.Aggregate(0UL,
            (sum, x) => checked(sum + x.FoodConsumedRaw)), recent.FoodConsumedRaw);
        Assert.Equal(batch.State.Cycles.Aggregate(0UL,
            (sum, x) => checked(sum + x.FoodUnmetRaw)), recent.FoodUnmetRaw);
        Assert.Equal(stateHash, batch.CurrentHash);
        Assert.Equal(stateSave, batch.ExportSaveJson());

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(stateSave);
        Assert.Equal(stateHash, reopened.CurrentHash);
        Assert.Equal(stateSave, reopened.ExportSaveJson());
        Assert.Equal(recent, PhysicalEconomicCycleProgression.SummarizeRecent(reopened));
    }

    [Fact]
    public void FiveCycleBatchCrossesAdaptiveThirtyTurnBoundaryWithoutChangingCadence()
    {
        var batch = Start();
        var control = Start();
        for (var t = 0; t < 30; t++)
        {
            batch.AdvanceMovementTurn();
            control.AdvanceMovementTurn();
        }
        var proposed = PhysicalEconomicCycleProgression.PreviewBatch(batch);
        Assert.Equal(30UL, proposed.StartingMovementTurn);
        Assert.Equal(40UL, proposed.FinalSettlementTurn);
        Assert.Equal(10UL, proposed.MovementTurnsToAdvance);

        var executed = PhysicalEconomicCycleProgression.AdvanceBatch(batch);
        Assert.Equal(proposed, executed);
        for (var t = 0; t < 10; t++) control.AdvanceMovementTurn();
        Assert.Equal(40UL, batch.State.MovementTurn);
        Assert.Equal(35, batch.State.Cycles.Count);
        Assert.Equal(batch.CurrentHash, control.CurrentHash);
        Assert.Equal(batch.ExportSaveJson(), control.ExportSaveJson());
        Assert.Equal(32UL, PhysicalEconomicCycleProgression.SummarizeRecent(batch).FirstSettlementTurn);
        Assert.Equal(40UL, PhysicalEconomicCycleProgression.SummarizeRecent(batch).LastSettlementTurn);
        Assert.Equal(2UL, batch.NextEconomicInterval);
    }

    [Fact]
    public void LegacyV9AndV13SavesReplayTheFullBatchWithoutNewStateFields()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start();
        var planned = PhysicalEconomicCycleProgression.PreviewBatch(legacy);
        Assert.Equal(30UL, planned.FinalSettlementTurn);
        Assert.Equal(30UL, planned.MovementTurnsToAdvance);
        PhysicalEconomicCycleProgression.AdvanceBatch(legacy);
        Assert.Equal(9U, legacy.CurrentHash.FormatVersion);
        Assert.Equal(30, legacy.Events.Count);
        Assert.Equal(5, legacy.State.Cycles.Count);
        var legacySave = legacy.ExportSaveJson();
        Assert.Equal(legacySave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(legacySave).ExportSaveJson());

        var v13 = Start(born: false);
        PhysicalEconomicCycleProgression.AdvanceBatch(v13);
        Assert.Equal(13U, v13.CurrentHash.FormatVersion);
        var payload = v13.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(payload);
        Assert.Equal(payload, restored.ExportSaveJson());
        Assert.Equal(v13.CurrentHash, restored.CurrentHash);
        Assert.Null(Assert.Single(
            EmergentSettlementProjection.FromSession(restored).Sites).Community);
    }

    [Fact]
    public void InvalidBatchRequestsAreRejectedWithoutMutatingAnyCanonicalState()
    {
        var game = Start();
        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalEconomicCycleProgression.PreviewBatch(game, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalEconomicCycleProgression.AdvanceBatch(game, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalEconomicCycleProgression.SummarizeRecent(game, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalEconomicCycleProgression.SummarizeRecent(game, 6));
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }
}
