using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class ClassIGoldbergStrategicTopologyTests
{
    [Theory]
    [InlineData(2, 0, 42, 120, 80, 30)]
    [InlineData(0, 2, 42, 120, 80, 30)]
    [InlineData(3, 0, 92, 270, 180, 80)]
    public void ClassITopologiesHaveExpectedCountsAndDegrees(
        int m,
        int n,
        int expectedCells,
        int expectedEdges,
        int expectedVertices,
        int expectedHexagons)
    {
        var topology =
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    m,
                    n));

        Assert.Equal(
            expectedCells,
            topology.Cells.Count);
        Assert.Equal(
            expectedEdges,
            topology.Edges.Count);
        Assert.Equal(
            expectedVertices,
            topology.Vertices.Count);

        Assert.Equal(
            12,
            topology.Cells.Count(
                cell =>
                    cell.Kind
                    == StrategicCellKind.Pentagon));

        Assert.Equal(
            expectedHexagons,
            topology.Cells.Count(
                cell =>
                    cell.Kind
                    == StrategicCellKind.Hexagon));

        Assert.All(
            topology.Cells,
            cell =>
            {
                var expectedDegree =
                    cell.Kind
                    == StrategicCellKind.Pentagon
                        ? 5
                        : 6;

                Assert.Equal(
                    expectedDegree,
                    cell.AdjacentCellIds.Count);
                Assert.Equal(
                    expectedDegree,
                    cell.IncidentEdgeIds.Count);
                Assert.Equal(
                    expectedDegree,
                    cell.IncidentVertexIds.Count);
            });

        Assert.All(
            topology.Edges,
            edge =>
            {
                Assert.Equal(
                    2,
                    edge.IncidentCellIds.Count);
                Assert.Equal(
                    2,
                    edge.IncidentVertexIds.Count);
            });

        Assert.All(
            topology.Vertices,
            vertex =>
            {
                Assert.Equal(
                    3,
                    vertex.IncidentCellIds.Count);
                Assert.Equal(
                    3,
                    vertex.IncidentEdgeIds.Count);
            });
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    [InlineData(3, 0)]
    public void ClassITopologiesSatisfyGlobalInvariants(
        int m,
        int n)
    {
        var topology =
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    m,
                    n));

        AssertReciprocalIncidence(topology);
        AssertConnected(topology);

        var euler =
            topology.Vertices.Count
            - topology.Edges.Count
            + topology.Cells.Count;

        Assert.Equal(
            2,
            euler);
    }

    [Fact]
    public void RepeatedG20GenerationIsCanonical()
    {
        var parameters =
            new GoldbergParameters(
                2,
                0);

        var first =
            GoldbergStrategicTopologyGenerator.Generate(
                parameters);

        var second =
            GoldbergStrategicTopologyGenerator.Generate(
                parameters);

        Assert.Equal(
            GetSignature(first),
            GetSignature(second));
    }

    [Fact]
    public void ClassIIAndIIIRemainExplicitlyUnsupportedInTrancheA()
    {
        Assert.Throws<NotSupportedException>(
            () =>
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        1,
                        1)));

        Assert.Throws<NotSupportedException>(
            () =>
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        2,
                        1)));
    }

    private static void AssertReciprocalIncidence(
        StrategicTopology topology)
    {
        foreach (var cell in topology.Cells)
        {
            foreach (var adjacentId in cell.AdjacentCellIds)
            {
                var adjacent =
                    topology.Cells[
                        checked((int)adjacentId.Value - 1)];

                Assert.Contains(
                    cell.Id,
                    adjacent.AdjacentCellIds);
            }

            foreach (var edgeId in cell.IncidentEdgeIds)
            {
                var edge =
                    topology.Edges[
                        checked((int)edgeId.Value - 1)];

                Assert.Contains(
                    cell.Id,
                    edge.IncidentCellIds);
            }

            foreach (var vertexId in cell.IncidentVertexIds)
            {
                var vertex =
                    topology.Vertices[
                        checked((int)vertexId.Value - 1)];

                Assert.Contains(
                    cell.Id,
                    vertex.IncidentCellIds);
            }
        }

        foreach (var edge in topology.Edges)
        {
            foreach (var vertexId in edge.IncidentVertexIds)
            {
                var vertex =
                    topology.Vertices[
                        checked((int)vertexId.Value - 1)];

                Assert.Contains(
                    edge.Id,
                    vertex.IncidentEdgeIds);
            }
        }
    }

    private static void AssertConnected(
        StrategicTopology topology)
    {
        var visited =
            new HashSet<StrategicCellId>();
        var queue =
            new Queue<StrategicCellId>();

        queue.Enqueue(
            topology.Cells[0].Id);

        while (queue.Count > 0)
        {
            var currentId =
                queue.Dequeue();

            if (!visited.Add(currentId))
            {
                continue;
            }

            var current =
                topology.Cells[
                    checked((int)currentId.Value - 1)];

            foreach (var adjacentId in current.AdjacentCellIds)
            {
                if (!visited.Contains(adjacentId))
                {
                    queue.Enqueue(
                        adjacentId);
                }
            }
        }

        Assert.Equal(
            topology.Cells.Count,
            visited.Count);
    }

    private static string GetSignature(
        StrategicTopology topology)
    {
        return string.Join(
            "|",
            topology.Cells.Select(
                cell =>
                    $"C{cell.Id.Value}:"
                    + $"{cell.Kind}:"
                    + $"{string.Join(',', cell.AdjacentCellIds.Select(id => id.Value))}:"
                    + $"{string.Join(',', cell.IncidentEdgeIds.Select(id => id.Value))}:"
                    + $"{string.Join(',', cell.IncidentVertexIds.Select(id => id.Value))}")
            .Concat(
                topology.Edges.Select(
                    edge =>
                        $"E{edge.Id.Value}:"
                        + $"{string.Join(',', edge.IncidentCellIds.Select(id => id.Value))}:"
                        + $"{string.Join(',', edge.IncidentVertexIds.Select(id => id.Value))}"))
            .Concat(
                topology.Vertices.Select(
                    vertex =>
                        $"V{vertex.Id.Value}:"
                        + $"{string.Join(',', vertex.IncidentCellIds.Select(id => id.Value))}:"
                        + $"{string.Join(',', vertex.IncidentEdgeIds.Select(id => id.Value))}")));
    }
}
