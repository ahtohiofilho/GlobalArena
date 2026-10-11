using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class MeshPresentationPlanTests
{
    private static PhysicalTacticalIncidenceMap CreateMap(int k) =>
        PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(k, 0),
                new GoldbergParameters(k * 6, 0)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void StrategicOverviewUsesOnlyCanonicalCoarseIdentities(int k)
    {
        var map = CreateMap(k);
        var plan = MeshPresentationPlan.StrategicOverview(map.CoarseTopology);
        var compatibility = MeshPresentationPlan.StrategicOverview(map);
        Assert.Equal(plan.StrategicCellIds, compatibility.StrategicCellIds);
        Assert.Equal(MeshPresentationMode.StrategicOverview, plan.Mode);
        Assert.Null(plan.Focus);
        Assert.Empty(plan.PhysicalTileIds);
        Assert.Equal(map.CoarseTopology.Cells.Select(c => c.Id).OrderBy(id => id.Value),
            plan.StrategicCellIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StrategicCellId>)plan.StrategicCellIds).Clear());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TacticalNeighborhoodExactlyReusesTheCanonicalViewportFrame(int k)
    {
        var map = CreateMap(k);
        foreach (var cell in map.CoarseTopology.Cells)
        {
            var frame = PhysicalTacticalViewportFrame.Create(map, cell.Id);
            var plan = MeshPresentationPlan.TacticalNeighborhood(map, cell.Id);
            Assert.Equal(MeshPresentationMode.TacticalNeighborhood, plan.Mode);
            Assert.Equal(cell.Id, plan.Focus);
            Assert.Equal(frame.StrategicCellIds, plan.StrategicCellIds);
            Assert.Equal(frame.PhysicalTileIds, plan.PhysicalTileIds);
            Assert.Equal(plan.PhysicalTileIds.Count, plan.PhysicalTileIds.Distinct().Count());
            Assert.All(plan.PhysicalTileIds,
                id => Assert.Equal(map.Refinement.FineParameters, id.FineGoldbergParameters));
        }
    }

    [Fact]
    public void RejectsMissingMapsAndUnknownFocusAndExposesReadOnlyPhysicalIds()
    {
        var map = CreateMap(1);
        Assert.Throws<ArgumentNullException>(() => MeshPresentationPlan.StrategicOverview((StrategicTopology)null!));
        Assert.Throws<ArgumentNullException>(() => MeshPresentationPlan.StrategicOverview((PhysicalTacticalIncidenceMap)null!));
        Assert.Throws<ArgumentNullException>(() =>
            MeshPresentationPlan.TacticalNeighborhood(null!, map.CoarseTopology.Cells[0].Id));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeshPresentationPlan.TacticalNeighborhood(map, default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeshPresentationPlan.TacticalNeighborhood(map, new StrategicCellId(999999)));
        var plan = MeshPresentationPlan.TacticalNeighborhood(map, map.CoarseTopology.Cells[0].Id);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PhysicalTacticalTileId>)plan.PhysicalTileIds).Clear());
    }
}