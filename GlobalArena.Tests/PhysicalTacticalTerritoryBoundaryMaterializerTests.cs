using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalTerritoryBoundaryMaterializerTests
{
    private static readonly GoldbergScaledRefinement Refinement = new(
        new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));

    [Fact]
    public void BuildsOneContinuousUniqueRouteForEveryStrategicEdge()
    {
        var map = PhysicalTacticalIncidenceMapper.Materialize(Refinement);
        var routes = PhysicalTacticalTerritoryBoundaryMaterializer.Materialize(map);
        Assert.Equal(270, routes.Count);
        Assert.Equal(Enumerable.Range(1, 270).Select(x => (ulong)x),
            routes.Select(route => route.StrategicEdgeId.Value));
        var segments = new HashSet<(ulong, ulong)>();
        var junctionCounts = new Dictionary<ulong, int>();
        foreach (var route in routes)
        {
            var ids = route.FineCellIds.Select(id => id.Value).ToArray();
            Assert.Equal(5, ids.Length);
            Assert.Equal(5, ids.Distinct().Count());
            var matchingEdge = map.CoarseTopology.Edges[checked((int)route.StrategicEdgeId.Value - 1)];
            Assert.Equal(matchingEdge.IncidentCellIds,
                map.TileIncidences[checked((int)ids[2] - 1)].IncidentCoarseCellIds);
            foreach (var joint in new[] { ids[0], ids[^1] })
            {
                Assert.Equal(3, map.TileIncidences[checked((int)joint - 1)].IncidentCoarseCellIds.Count);
                junctionCounts[joint] = junctionCounts.GetValueOrDefault(joint) + 1;
            }
            for (var i = 1; i < ids.Length; i++)
            {
                Assert.Contains(map.FineTopology.Cells[checked((int)ids[i-1] - 1)].AdjacentCellIds,
                    id => id.Value == ids[i]);
                Assert.True(segments.Add((Math.Min(ids[i-1], ids[i]), Math.Max(ids[i-1], ids[i]))));
            }
        }
        Assert.Equal(1080, segments.Count);
        Assert.Equal(180, junctionCounts.Count);
        Assert.All(junctionCounts.Values, value => Assert.Equal(3, value));
    }

    [Fact]
    public void MaterializationIsDeterministicAndRejectsNonAuditedRefinements()
    {
        var map = PhysicalTacticalIncidenceMapper.Materialize(Refinement);
        string Signature() => string.Join(";",
            PhysicalTacticalTerritoryBoundaryMaterializer.Materialize(map)
                .Select(route => route.StrategicEdgeId.Value + ":" +
                    string.Join(",", route.FineCellIds.Select(id => id.Value))));
        Assert.Equal(Signature(), Signature());
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalTerritoryBoundaryMaterializer.Materialize(null!));
        var other = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(
            new GoldbergParameters(1, 0), new GoldbergParameters(6, 0)));
        Assert.Throws<NotSupportedException>(() => PhysicalTacticalTerritoryBoundaryMaterializer.Materialize(other));
    }
}