using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalRenderSelectionTests
{
    private static PhysicalTacticalIncidenceMap Map() =>
        PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(1, 0), new GoldbergParameters(6, 0)));

    [Fact]
    public void EveryFocusProducesTheExactCanonicalViewportSelection()
    {
        var map = Map();
        foreach (var cell in map.CoarseTopology.Cells)
        {
            foreach (var steps in new[] { 0, 1, 2, map.CoarseTopology.Cells.Count })
            {
                var expected = PhysicalTacticalViewportQuery.SelectTiles(map, cell.Id, steps);
                var selection = PhysicalTacticalRenderSelection.Create(map, cell.Id, steps);
                Assert.Equal(cell.Id, selection.Focus);
                Assert.Equal(steps, selection.AdjacentSteps);
                Assert.Equal(expected, selection.Items.Select(item => item.TileId).ToArray());
                Assert.Equal(selection.Items.Count, selection.Items.Select(item => item.TileId).Distinct().Count());
            }
        }
    }

    [Fact]
    public void EachRenderItemRetainsAuthoritativeOneTwoThreeIncidence()
    {
        var map = Map();
        var selection = PhysicalTacticalRenderSelection.Create(
            map, map.CoarseTopology.Cells[0].Id, map.CoarseTopology.Cells.Count);
        Assert.Equal(map.TileIncidences.Count, selection.Items.Count);
        var expected = map.TileIncidences.ToDictionary(i => i.PhysicalTacticalTileId);
        foreach (var item in selection.Items)
            Assert.Equal(expected[item.TileId].IncidentCoarseCellIds, item.IncidentStrategicCells);
        Assert.Equal(312, selection.Items.Count(i => i.IncidentStrategicCells.Count == 1));
        Assert.Equal(30, selection.Items.Count(i => i.IncidentStrategicCells.Count == 2));
        Assert.Equal(20, selection.Items.Count(i => i.IncidentStrategicCells.Count == 3));
    }

    [Fact]
    public void ItemsAndTheirIncidenceListsCannotBeMutated()
    {
        var map = Map();
        var selection = PhysicalTacticalRenderSelection.Create(map, map.CoarseTopology.Cells[0].Id, 1);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PhysicalTacticalRenderItem>)selection.Items).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StrategicCellId>)selection.Items[0].IncidentStrategicCells).Clear());
    }

    [Fact]
    public void InvalidInputsAreRejectedByAuthoritativeViewportQuery()
    {
        var map = Map();
        var focus = map.CoarseTopology.Cells[0].Id;
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalRenderSelection.Create(null!, focus, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalRenderSelection.Create(map, default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalRenderSelection.Create(map, focus, -1));
    }
}
