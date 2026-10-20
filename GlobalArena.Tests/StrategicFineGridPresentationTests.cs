using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicFineGridPresentationTests
{
    private static PhysicalTacticalIncidenceMap CreateMap(int frequency) =>
        PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(
                new GoldbergParameters(frequency, 0),
                new GoldbergParameters(frequency * 6, 0)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OverviewCanRequestAllPhysicalTilesWithoutChangingStrategicScope(int frequency)
    {
        var map = CreateMap(frequency);
        var legacy = MeshPresentationPlan.StrategicOverview(map);
        var coarseOnly = MeshPresentationPlan.StrategicOverview(map, false);
        var fine = MeshPresentationPlan.StrategicOverview(map, true);

        Assert.Equal(MeshPresentationMode.StrategicOverview, fine.Mode);
        Assert.Null(fine.Focus);
        Assert.Equal(legacy.StrategicCellIds, fine.StrategicCellIds);
        Assert.Equal(legacy.StrategicCellIds, coarseOnly.StrategicCellIds);
        Assert.Empty(legacy.PhysicalTileIds);
        Assert.Empty(coarseOnly.PhysicalTileIds);
        Assert.Equal(map.FineTopology.Cells.Count, fine.PhysicalTileIds.Count);
        Assert.Equal(map.TileIncidences.Select(i => i.PhysicalTacticalTileId),
            fine.PhysicalTileIds);
        Assert.Equal(fine.PhysicalTileIds.Count, fine.PhysicalTileIds.Distinct().Count());
        Assert.All(fine.PhysicalTileIds,
            id => Assert.Equal(map.Refinement.FineParameters, id.FineGoldbergParameters));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PhysicalTacticalTileId>)fine.PhysicalTileIds).Clear());

        var repeated = MeshPresentationPlan.StrategicOverview(map, true);
        Assert.Equal(fine.PhysicalTileIds, repeated.PhysicalTileIds);
    }

    [Fact]
    public void InvalidIncidenceMapIsRejectedAndLocalTacticalFrameRemainsUnchanged()
    {
        Assert.Throws<ArgumentNullException>(() =>
            MeshPresentationPlan.StrategicOverview((PhysicalTacticalIncidenceMap)null!, true));
        Assert.Throws<ArgumentNullException>(() =>
            MeshPresentationPlan.StrategicOverview((PhysicalTacticalIncidenceMap)null!, false));

        var map = CreateMap(1);
        var focus = map.CoarseTopology.Cells[0].Id;
        var before = MeshPresentationPlan.TacticalNeighborhood(map, focus);
        _ = MeshPresentationPlan.StrategicOverview(map, true);
        var after = MeshPresentationPlan.TacticalNeighborhood(map, focus);
        Assert.Equal(MeshPresentationMode.TacticalNeighborhood, after.Mode);
        Assert.Equal(before.Focus, after.Focus);
        Assert.Equal(before.StrategicCellIds, after.StrategicCellIds);
        Assert.Equal(before.PhysicalTileIds, after.PhysicalTileIds);
    }
}
