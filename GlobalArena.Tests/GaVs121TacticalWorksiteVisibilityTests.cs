using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS121: one end-to-end physical primitive facility lifecycle through
/// immutable snapshots, read-only tactical projection and canonical saves.
/// The Godot cockpit consumes this projection rather than estimating state.
/// </summary>
public sealed class GaVs121TacticalWorksiteVisibilityTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start();

    private static CanonicalPrimitiveWorksiteTileView View(
        CanonicalPhysicalTacticalSession game,
        EconomicActivityKind activity = EconomicActivityKind.Forestry) =>
        CanonicalPrimitiveWorksiteTacticalProjection.Read(
            game, game.InitialTile, activity);

    private static void CloseOneEconomicCycle(
        CanonicalPhysicalTacticalSession game)
    {
        for (var i = 0; i < 5; i++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.NotNull(game.AdvanceMovementTurn());
    }

    private static int Assemble(CanonicalPhysicalTacticalSession game)
    {
        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL);
        return frame.ActionOrdinal;
    }

    [Fact]
    public void ReadOnlyCockpitNeverCreatesPopulationProductionOrWorldRevision()
    {
        var game = Start();
        var save = game.ExportSaveJson();
        var originalHash = game.CurrentHash;
        var originalRevision = game.Game.Snapshot.Revision;

        var state = View(game);
        Assert.Equal(PrimitiveWorksiteStage.NeedsPhysicalMaterials, state.Stage);
        Assert.Equal(0, state.TotalWorldFacilities);
        Assert.Equal(0, state.AvailableAssemblyOrdinal);
        Assert.Equal(0, state.FacilityAssemblyOrdinal);
        Assert.Equal(0UL, state.LastAssistedProducedRaw);
        Assert.False(state.HasFreshAssistedOutput);
        Assert.False(state.CreatesAdditionalProduction);
        Assert.False(state.CanAuthorizeRealPurchasingPower);
        Assert.Equal(originalHash, game.CurrentHash);
        Assert.Equal(originalRevision, game.Game.Snapshot.Revision);
        Assert.Equal(save, game.ExportSaveJson());
    }

    [Fact]
    public void StageProgressesOnlyOnActualMaterialAndConstructionEvents()
    {
        var game = Start();
        CloseOneEconomicCycle(game);
        var material = View(game);
        Assert.Equal(PrimitiveWorksiteStage.MaterialsAvailable, material.Stage);
        Assert.True(material.LocalWoodRaw >= 1UL);
        Assert.True(material.LocalStoneRaw >= 1UL);
        Assert.Equal(game.State.TotalWorkers, material.ResidentWorkers);

        var frame = Assemble(game);
        var assembled = View(game);
        Assert.Equal(PrimitiveWorksiteStage.AssemblyReady, assembled.Stage);
        Assert.Equal(frame, assembled.AvailableAssemblyOrdinal);
        Assert.True(assembled.MayStartConstruction);
        Assert.False(assembled.HasWorldFacility);

        game.StartPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 1UL);
        var building = View(game);
        Assert.Equal(PrimitiveWorksiteStage.UnderConstruction, building.Stage);
        Assert.Equal(1UL, building.ConstructionWorkers);
        Assert.Equal(1, building.TotalBuildingFacilities);
        Assert.Equal(0, building.TotalOperatingFacilities);
        Assert.Equal(6UL, building.StartedTurn);
        Assert.Equal(0UL, building.CompletedTurn);
        Assert.False(building.MayOperate);
        Assert.Equal(game.State.TotalWorkers, building.ResidentWorkers);
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(game.AdvanceMovementTurn());
            Assert.Equal(PrimitiveWorksiteStage.UnderConstruction, View(game).Stage);
        }
        Assert.NotNull(game.AdvanceMovementTurn());
        var built = View(game);
        Assert.Equal(PrimitiveWorksiteStage.BuiltUnstaffed, built.Stage);
        Assert.Equal(12UL, built.CompletedTurn);
        Assert.True(built.MayOperate);
        Assert.Equal(0UL, built.ConstructionWorkers);
        Assert.Equal(1, built.TotalWorldFacilities);
        Assert.Equal(0, built.TotalBuildingFacilities);
        Assert.Equal(0, built.TotalOperatingFacilities);
    }

    [Fact]
    public void OperatingShelterReconcilesPhysicalProductionOnClosingTick()
    {
        var game = Start();
        CloseOneEconomicCycle(game);
        var frame = Assemble(game);
        game.StartPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 1UL);
        CloseOneEconomicCycle(game);
        Assert.Equal(PrimitiveWorksiteStage.BuiltUnstaffed, View(game).Stage);

        game.StaffPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 1UL);
        var before = View(game);
        Assert.Equal(PrimitiveWorksiteStage.Operating, before.Stage);
        Assert.Equal(1UL, before.OperatingWorkers);
        Assert.Equal(1, before.TotalOperatingFacilities);
        Assert.False(before.HasFreshAssistedOutput);
        Assert.Equal(0UL, before.LastAssistedProducedRaw);
        Assert.True(before.MaySuspend);

        CloseOneEconomicCycle(game);
        var realized = View(game);
        Assert.True(realized.HasFreshAssistedOutput);
        Assert.Equal(PrimitiveWorksiteStage.Operating, realized.Stage);
        Assert.Equal(18UL, realized.LastCycleMovementTurn);
        Assert.Equal(checked((ulong)game.PrimitiveYieldAt(
            game.InitialTile, EconomicActivityKind.Forestry)),
            realized.LastAssistedProducedRaw);
        Assert.True(realized.LastAssistedProducedRaw <=
            realized.LastCycleGlobalProductionRaw);
        Assert.Equal(game.CurrentHash.FormatVersion,
            CanonicalWorldStateHasher.FacilityAssistedWorkHashFormatVersion);
        Assert.False(realized.CreatesAdditionalProduction);

        // A new explicit staffing action advances the canonical journal and
        // deliberately invalidates the previous closing-tick evidence.
        game.StaffPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 0UL);
        var suspended = View(game);
        Assert.Equal(PrimitiveWorksiteStage.BuiltUnstaffed, suspended.Stage);
        Assert.Equal(0UL, suspended.OperatingWorkers);
        Assert.False(suspended.HasFreshAssistedOutput);
        Assert.True(suspended.MayOperate);
    }

    [Fact]
    public void ReadOnlyPreviewMatchesExactlyAfterSaveAndReplayAtEveryStage()
    {
        var game = Start();
        CloseOneEconomicCycle(game);
        int frame = Assemble(game);
        game.StartPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 1UL);
        var constructionSave = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(constructionSave);
        Assert.Equal(View(game), View(replay));
        Assert.Equal(constructionSave, replay.ExportSaveJson());

        CloseOneEconomicCycle(game);
        var completedSave = game.ExportSaveJson();
        replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(completedSave);
        Assert.Equal(View(game), View(replay));
        Assert.Equal(completedSave, replay.ExportSaveJson());

        game.StaffPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 1UL);
        CloseOneEconomicCycle(game);
        var liveSave = game.ExportSaveJson();
        replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(liveSave);
        Assert.Equal(View(game), View(replay));
        Assert.True(View(replay).HasFreshAssistedOutput);
        Assert.Equal(liveSave, replay.ExportSaveJson());
    }

    [Fact]
    public void TileAndActivitySelectionShowNoPhantomFacility()
    {
        var game = Start();
        CloseOneEconomicCycle(game);
        var frame = Assemble(game);
        game.StartPrimitiveFacility(game.InitialTile, frame,
            EconomicActivityKind.Forestry, 1UL);
        CloseOneEconomicCycle(game);

        var otherJob = View(game, EconomicActivityKind.Mining);
        Assert.Equal(0, otherJob.FacilityAssemblyOrdinal);
        Assert.Equal(PrimitiveWorksiteStage.MaterialsAvailable, otherJob.Stage);
        Assert.Equal(1, otherJob.TotalWorldFacilities);
        Assert.Equal(0, otherJob.TotalOperatingFacilities);

        var neighbor = game.AdjacentWorkable(game.InitialTile)[0];
        var otherTile = CanonicalPrimitiveWorksiteTacticalProjection.Read(
            game, neighbor, EconomicActivityKind.Forestry);
        Assert.Equal(0, otherTile.FacilityAssemblyOrdinal);
        Assert.Equal(1, otherTile.TotalWorldFacilities);

        var hash = game.CurrentHash;
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPrimitiveWorksiteTacticalProjection.Read(game,
                game.InitialTile, EconomicActivityKind.TradeLogistics));
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPrimitiveWorksiteTacticalProjection.Read(game,
                new PhysicalTacticalTileId(new GoldbergParameters(2, 0),
                    new StrategicCellId(1UL)), EconomicActivityKind.Forestry));
        Assert.Equal(hash, game.CurrentHash);
    }
}
