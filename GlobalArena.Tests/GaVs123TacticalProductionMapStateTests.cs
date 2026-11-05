using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS123: same canonical material/worker/facility lifecycle, with ONE
/// read-only production-stage map marker per physical tile. No city tiers.
/// </summary>
public sealed class GaVs123TacticalProductionMapStateTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start();

    private static CanonicalTacticalWorksiteMapProjection Projection(
        CanonicalPhysicalTacticalSession game) =>
        CanonicalTacticalWorksiteMapProjection.FromSession(game);

    private static void CloseCycle(CanonicalPhysicalTacticalSession game)
    {
        for (var i = 0; i < 5; i++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.NotNull(game.AdvanceMovementTurn());
    }

    [Fact]
    public void NoConstructionMeansNoInventedProductionBuildingOnSettlement()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var view = Projection(game);
        Assert.Empty(view.Tiles);
        Assert.Null(view.At(game.InitialTile));
        Assert.Equal(0, view.PhysicalFacilityCount);
        Assert.False(view.MutatesCanonicalWorld);
        Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
    }

    [Fact]
    public void VisualStagesTrackActualAssemblyLaborCompletionAndOperatingStatus()
    {
        var game = Start();
        CloseCycle(game);
        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL);
        var mounting = Assert.Single(Projection(game).Tiles);
        Assert.Equal(game.InitialTile.FineStrategicCellId.Value,mounting.FineTile);
        Assert.Equal(TacticalWorksiteMapStage.MaterialFrame,mounting.Stage);
        Assert.Equal(1,mounting.UnusedMaterialFrames);
        Assert.Equal(0,mounting.BuildingFacilities);
        Assert.Equal(0,mounting.CompletedFacilities);
        Assert.Equal(0,mounting.OperatingFacilities);

        game.StartPrimitiveFacility(game.InitialTile,frame.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);
        var building = Assert.Single(Projection(game).Tiles);
        Assert.Equal(TacticalWorksiteMapStage.UnderConstruction,building.Stage);
        Assert.Equal(0,building.UnusedMaterialFrames);
        Assert.Equal(1,building.BuildingFacilities);
        Assert.Equal(1UL,building.ReservedConstructionWorkers);
        Assert.Equal(0UL,building.OperatingWorkers);
        Assert.False(building.IsProductivityBonus);
        Assert.False(building.GeneratesPeopleOrResources);

        CloseCycle(game);
        var ready = Assert.Single(Projection(game).Tiles);
        Assert.Equal(TacticalWorksiteMapStage.Completed,ready.Stage);
        Assert.Equal(0,ready.BuildingFacilities);
        Assert.Equal(1,ready.CompletedFacilities);
        Assert.Equal(0,ready.OperatingFacilities);
        Assert.Equal(0UL,ready.ReservedConstructionWorkers);

        game.StaffPrimitiveFacility(game.InitialTile,frame.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);
        var running = Assert.Single(Projection(game).Tiles);
        Assert.Equal(TacticalWorksiteMapStage.Operating,running.Stage);
        Assert.Equal(1,running.OperatingFacilities);
        Assert.Equal(1UL,running.OperatingWorkers);
        Assert.Equal(1,running.CompletedFacilities);
        Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);

        game.StaffPrimitiveFacility(game.InitialTile,frame.ActionOrdinal,
            EconomicActivityKind.Forestry,0UL);
        var stopped = Assert.Single(Projection(game).Tiles);
        Assert.Equal(TacticalWorksiteMapStage.Completed,stopped.Stage);
        Assert.Equal(0UL,running.ReservedConstructionWorkers);
        Assert.Equal(0UL,stopped.OperatingWorkers);
        Assert.Single(game.State.PrimitiveFacilities);
    }

    [Fact]
    public void ReadOnlyMarkersAreByteExactAfterSaveAndReopenInEveryStage()
    {
        var game = Start();
        CloseCycle(game);
        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,1UL,1UL);
        Check(game);
        game.StartPrimitiveFacility(game.InitialTile,frame.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);
        Check(game);
        CloseCycle(game);
        Check(game);
        game.StaffPrimitiveFacility(game.InitialTile,frame.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);
        Check(game);
        CloseCycle(game);
        Check(game);

        static void Check(CanonicalPhysicalTacticalSession game)
        {
            var before = game.ExportSaveJson();
            var hash = game.CurrentHash;
            var revision = game.Game.Snapshot.Revision;
            var projection = Projection(game);
            var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
            Assert.Equal(projection.Tiles.ToArray(),Projection(restored).Tiles.ToArray());
            Assert.Equal(projection.PhysicalFacilityCount,
                Projection(restored).PhysicalFacilityCount);
            Assert.Equal(hash,game.CurrentHash);
            Assert.Equal(revision,game.Game.Snapshot.Revision);
            Assert.Equal(before,game.ExportSaveJson());
            Assert.Equal(before,restored.ExportSaveJson());
        }
    }

    [Fact]
    public void MoreThanOnePhysicalActivityStillMeansOneUnifiedPlaceAndOneMapMarker()
    {
        var game = Start();
        CloseCycle(game);
        var first = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,1UL,1UL);
        game.StartPrimitiveFacility(game.InitialTile,first.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);
        CloseCycle(game);
        // Once the first shelter is finished, more genuine wood/stone can
        // be assembled at the same tile without creating a second town.
        var second = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,1UL,1UL);
        game.StartPrimitiveFacility(game.InitialTile,second.ActionOrdinal,
            EconomicActivityKind.Mining,1UL);
        game.StaffPrimitiveFacility(game.InitialTile,first.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);

        var projection = Projection(game);
        var marker = Assert.Single(projection.Tiles);
        Assert.Equal(TacticalWorksiteMapStage.UnderConstruction,marker.Stage);
        Assert.Equal(2,projection.PhysicalFacilityCount);
        Assert.Equal(1,marker.BuildingFacilities);
        Assert.Equal(1,marker.CompletedFacilities);
        Assert.Equal(1,marker.OperatingFacilities);
        Assert.Equal(1UL,marker.ReservedConstructionWorkers);
        Assert.Equal(1UL,marker.OperatingWorkers);
        Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);
    }

    [Fact]
    public void RealPeopleMovementDoesNotConvertAProductionAssetIntoACity()
    {
        var game = Start();
        CloseCycle(game);
        var first = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,1UL,1UL);
        game.StartPrimitiveFacility(game.InitialTile,first.ActionOrdinal,
            EconomicActivityKind.Forestry,1UL);
        CloseCycle(game);
        var before = Projection(game);
        var icon = Assert.Single(before.Tiles);
        var from = game.InitialTile;
        var dest = game.AdjacentWorkable(from)[0];
        game.OrderWorkerTransit(from,dest,
            EconomicActivityKind.Agriculture,EconomicActivityKind.Agriculture,2UL);
        var during = Projection(game);
        Assert.Equal(icon,during.At(from));
        Assert.Null(during.At(dest));
        Assert.Equal(before.PhysicalFacilityCount,during.PhysicalFacilityCount);
        Assert.Equal(6UL,EmergentSettlementProjection.FromSession(game)
            .TotalResidentWorkers);
    }
}
