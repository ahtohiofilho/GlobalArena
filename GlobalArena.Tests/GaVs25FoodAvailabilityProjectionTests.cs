using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs25FoodAvailabilityProjectionTests
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
    public void NextCycleFoodDiagnosticsPreserveLiveGameAndReconcileLocalPhysicalGoods()
    {
        var live = Start();
        var tile = live.InitialTile;
        var save = live.ExportSaveJson();
        var hash = live.CurrentHash;
        var journal = live.Events.ToArray();
        var report = PhysicalFoodAvailabilityProjection.PreviewNextCycle(live, tile);

        Assert.Equal(0UL, report.StartingMovementTurn);
        Assert.Equal(1UL, report.SettlementMovementTurn);
        Assert.Equal(0UL, report.InitialFoodStockRaw);
        Assert.Equal(report.ProducedFoodRaw, report.TotalAvailableFoodRaw);
        Assert.Equal(4_000_000UL, report.FoodDemandRaw);
        Assert.Equal(report.FoodDemandRaw,
            checked(report.ActuallyConsumedRaw + report.PhysicalShortageRaw +
                report.UnconsumedDespiteSupplyRaw));
        Assert.Equal(report.TotalAvailableFoodRaw,
            checked(report.ActuallyConsumedRaw + report.FinalFoodStockRaw));
        Assert.Equal(report.TotalUnmetRaw,
            checked(report.PhysicalShortageRaw + report.UnconsumedDespiteSupplyRaw));
        Assert.Equal(save, live.ExportSaveJson());
        Assert.Equal(hash, live.CurrentHash);
        Assert.Equal(journal, live.Events);
        Assert.Equal(0UL, live.State.MovementTurn);
        Assert.Empty(live.State.Cycles);

        var expected = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var cycle = PhysicalEconomicCycleProgression.AdvanceToNext(expected);
        Assert.Equal(cycle.MovementTurn, report.SettlementMovementTurn);
        Assert.Equal(cycle.FoodConsumedRaw, report.ActuallyConsumedRaw);
        Assert.Equal(cycle.FoodUnmetRaw, report.TotalUnmetRaw);
        Assert.Equal(expected.StockAt(tile, new CommodityId(1U)),
            report.FinalFoodStockRaw);
    }

    [Fact]
    public void NewLocalAssignmentsAndTimedArrivalsUseTheRealNextEconomicCycle()
    {
        var game = Start();
        var origin = game.InitialTile;
        var destination = game.AdjacentWorkable(origin)[0];
        game.OrderWorkerTransit(origin, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        // At turn 0, the destination has no residents, but the upcoming
        // settlement can occur before the workers arrive.
        var empty = PhysicalFoodAvailabilityProjection.PreviewNextCycle(
            game, destination);
        Assert.Equal(0UL, empty.FoodDemandRaw);
        Assert.Equal(0UL, empty.TotalUnmetRaw);

        game.AdvanceMovementTurn(); // turn 1, still travelling
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var arriving = PhysicalFoodAvailabilityProjection.PreviewNextCycle(
            game, destination);
        Assert.Equal(2UL, arriving.SettlementMovementTurn);
        Assert.Equal(1_000_000UL, arriving.FoodDemandRaw);
        Assert.Equal(1_000_000UL,
            checked(arriving.ActuallyConsumedRaw + arriving.TotalUnmetRaw));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);

        game.ReassignWorkers(origin,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        for (var t = 0; t < 29; t++) game.AdvanceMovementTurn();
        Assert.Equal(30UL, game.State.MovementTurn);
        var boundarySave = game.ExportSaveJson();
        var boundary = PhysicalFoodAvailabilityProjection.PreviewNextCycle(
            game, origin);
        Assert.Equal(32UL, boundary.SettlementMovementTurn);
        Assert.Equal(boundarySave, game.ExportSaveJson());
        Assert.Equal(8UL, game.State.TotalWorkers);
    }

    [Fact]
    public void DifferentWorkerAllocationsAreObservableButDoNotChangeTheEconomy()
    {
        var game = Start();
        var tile = game.InitialTile;
        var saved = game.ExportSaveJson();
        var baseline = PhysicalFoodAvailabilityProjection.PreviewNextCycle(game, tile);
        var reallocated = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        reallocated.ReassignWorkers(tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        var changed = PhysicalFoodAvailabilityProjection.PreviewNextCycle(
            reallocated, tile);
        Assert.Equal(baseline.FoodDemandRaw, changed.FoodDemandRaw);
        Assert.True(changed.ProducedFoodRaw < baseline.ProducedFoodRaw);
        Assert.True(changed.PhysicalShortageRaw >= baseline.PhysicalShortageRaw);
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Equal(0UL, game.State.MovementTurn);
        Assert.Equal(320_000_000UL, game.FoodCash!.InitialMoneySupplyMicrocredits);
    }

    [Fact]
    public void OldVersionsRemainUntouchedAndV13StillSupportsTheDiagnostic()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start();
        var oldSave = legacy.ExportSaveJson();
        Assert.Throws<NotSupportedException>(() =>
            PhysicalFoodAvailabilityProjection.PreviewNextCycle(
                legacy, legacy.InitialTile));
        Assert.Equal(oldSave, legacy.ExportSaveJson());
        Assert.Equal(9U, legacy.CurrentHash.FormatVersion);

        var v13 = Start(born: false);
        var saved = v13.ExportSaveJson();
        var report = PhysicalFoodAvailabilityProjection.PreviewNextCycle(
            v13, v13.InitialTile);
        Assert.Equal(1UL, report.SettlementMovementTurn);
        Assert.Equal(saved, v13.ExportSaveJson());
        Assert.Equal(13U, v13.CurrentHash.FormatVersion);
        Assert.Null(Assert.Single(
            EmergentSettlementProjection.FromSession(v13).Sites).Community);
    }
}
