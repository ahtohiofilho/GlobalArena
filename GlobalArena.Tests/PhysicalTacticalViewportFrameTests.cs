using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalViewportFrameTests
{
    private static PhysicalTacticalIncidenceMap CreateMap(int k) =>
        PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(k, 0), new GoldbergParameters(6 * k, 0)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryStrategicFocusHasExactNeighborhoodAndCanonicalPhysicalUnion(int k)
    {
        var map = CreateMap(k);
        foreach (var cell in map.CoarseTopology.Cells)
        {
            var frame = PhysicalTacticalViewportFrame.Create(map, cell.Id);
            var expectedStrategic = cell.AdjacentCellIds.Append(cell.Id).Distinct().OrderBy(x => x.Value).ToArray();
            Assert.Equal(cell.Id, frame.Focus);
            Assert.Equal(expectedStrategic, frame.StrategicCellIds);
            var owners = expectedStrategic.ToHashSet();
            var expectedPhysical = map.TileIncidences
                .Where(x => x.IncidentCoarseCellIds.Any(owners.Contains))
                .Select(x => x.PhysicalTacticalTileId)
                .OrderBy(x => x.FineStrategicCellId.Value).ToArray();
            Assert.Equal(expectedPhysical, frame.PhysicalTileIds);
            Assert.Equal(frame.PhysicalTileIds.Count, frame.PhysicalTileIds.Distinct().Count());
        }
    }

    [Fact]
    public void InvalidFocusAndImmutableCollectionsAreEnforced()
    {
        var map = CreateMap(1);
        var focus = map.CoarseTopology.Cells[0].Id;
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalViewportFrame.Create(null!, focus));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalViewportFrame.Create(map, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalViewportFrame.Create(map, new StrategicCellId(999999)));
        var frame = PhysicalTacticalViewportFrame.Create(map, focus);
        Assert.Throws<NotSupportedException>(() => ((IList<StrategicCellId>)frame.StrategicCellIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PhysicalTacticalTileId>)frame.PhysicalTileIds).Clear());
    }
}