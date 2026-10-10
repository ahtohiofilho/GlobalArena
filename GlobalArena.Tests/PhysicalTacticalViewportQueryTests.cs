using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalViewportQueryTests
{
    private static PhysicalTacticalIncidenceMap Create(int m) =>
        PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(
                new GoldbergParameters(m, 0),
                new GoldbergParameters(m * 6, 0)));

    // Independent reference: breadth-first sets, then explicit incidence union.
    private static ulong[] Expected(PhysicalTacticalIncidenceMap map, StrategicCellId focus, int steps)
    {
        var selected = new HashSet<StrategicCellId> { focus };
        for (var distance = 0; distance < steps; distance++)
        {
            var next = new HashSet<StrategicCellId>(selected);
            foreach (var cell in map.CoarseTopology.Cells)
                if (selected.Contains(cell.Id))
                    next.UnionWith(cell.AdjacentCellIds);
            selected = next;
        }
        return map.TileIncidences
            .Where(x => x.IncidentCoarseCellIds.Intersect(selected).Any())
            .Select(x => x.PhysicalTacticalTileId.FineStrategicCellId.Value)
            .Order().ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryFocusMatchesIndependentIncidenceUnion(int m)
    {
        var map = Create(m);
        foreach (var focus in map.CoarseTopology.Cells.Select(cell => cell.Id))
        {
            foreach (var steps in new[] { 0, 1, 2, map.CoarseTopology.Cells.Count })
            {
                var actual = PhysicalTacticalViewportQuery.SelectTiles(map, focus, steps);
                var ids = actual.Select(x => x.FineStrategicCellId.Value).ToArray();
                Assert.Equal(Expected(map, focus, steps), ids);
                Assert.Equal(ids.Length, ids.Distinct().Count());
                Assert.Equal(ids, ids.Order().ToArray());
                Assert.Equal(ids, PhysicalTacticalViewportQuery.SelectTiles(map, focus, steps)
                    .Select(x => x.FineStrategicCellId.Value).ToArray());
            }
        }
    }

    [Fact]
    public void SharedEdgeAndVertexTilesAreSelectedOnceAndKeepCanonicalIds()
    {
        var map = Create(1);
        foreach (var incidence in map.TileIncidences.Where(x => x.IncidentCoarseCellIds.Count > 1))
        {
            foreach (var focus in incidence.IncidentCoarseCellIds)
            {
                var result = PhysicalTacticalViewportQuery.SelectTiles(map, focus, 0);
                Assert.Equal(1, result.Count(x => x == incidence.PhysicalTacticalTileId));
            }
        }
    }

    [Fact]
    public void InvalidInputsAndReadOnlyResultAreEnforced()
    {
        var map = Create(1);
        var focus = map.CoarseTopology.Cells[0].Id;
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalViewportQuery.SelectTiles(null!, focus, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalViewportQuery.SelectTiles(map, default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalViewportQuery.SelectTiles(map, new StrategicCellId(999999), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalViewportQuery.SelectTiles(map, focus, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalViewportQuery.SelectTiles(map, focus, map.CoarseTopology.Cells.Count + 1));
        var result = PhysicalTacticalViewportQuery.SelectTiles(map, focus, 0);
        Assert.Throws<NotSupportedException>(() => ((IList<PhysicalTacticalTileId>)result).Clear());
    }
}
