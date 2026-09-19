using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class MinimalG10StrategicTopologyTests
{
    [Fact]
    public void UnsupportedGoldbergParametersAreRejected()
    {
        Assert.Throws<NotSupportedException>(
            () =>
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        1,
                        1)));
    }

    [Fact]
    public void G10HasExpectedParametersAndCounts()
    {
        var topology = CreateG10();

        Assert.Equal(
            new GoldbergParameters(
                1,
                0),
            topology.Parameters);
        Assert.Equal(12, topology.Cells.Count);
        Assert.Equal(30, topology.Edges.Count);
        Assert.Equal(20, topology.Vertices.Count);
    }

    [Fact]
    public void G10UsesContiguousCanonicalIds()
    {
        var topology = CreateG10();

        Assert.Equal(
            Enumerable.Range(1, 12)
                .Select(value => (ulong)value)
                .ToArray(),
            topology.Cells
                .Select(cell => cell.Id.Value)
                .ToArray());

        Assert.Equal(
            Enumerable.Range(1, 30)
                .Select(value => (ulong)value)
                .ToArray(),
            topology.Edges
                .Select(edge => edge.Id.Value)
                .ToArray());

        Assert.Equal(
            Enumerable.Range(1, 20)
                .Select(value => (ulong)value)
                .ToArray(),
            topology.Vertices
                .Select(vertex => vertex.Id.Value)
                .ToArray());
    }

    [Fact]
    public void AllG10CellsArePentagonsWithDegreeFive()
    {
        var topology = CreateG10();

        Assert.All(
            topology.Cells,
            cell =>
            {
                Assert.Equal(
                    StrategicCellKind.Pentagon,
                    cell.Kind);
                Assert.Equal(
                    5,
                    cell.AdjacentCellIds.Count);
                Assert.Equal(
                    5,
                    cell.IncidentEdgeIds.Count);
                Assert.Equal(
                    5,
                    cell.IncidentVertexIds.Count);
                Assert.DoesNotContain(
                    cell.Id,
                    cell.AdjacentCellIds);
                Assert.Equal(
                    cell.AdjacentCellIds.Count,
                    cell.AdjacentCellIds.Distinct().Count());
                Assert.Equal(
                    cell.IncidentEdgeIds.Count,
                    cell.IncidentEdgeIds.Distinct().Count());
                Assert.Equal(
                    cell.IncidentVertexIds.Count,
                    cell.IncidentVertexIds.Distinct().Count());
            });
    }

    [Fact]
    public void EveryEdgeHasTwoDistinctIncidentCellsAndVertices()
    {
        var topology = CreateG10();

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
                Assert.Equal(
                    2,
                    edge.IncidentCellIds.Distinct().Count());
                Assert.Equal(
                    2,
                    edge.IncidentVertexIds.Distinct().Count());
            });
    }

    [Fact]
    public void EveryVertexHasThreeDistinctIncidentCellsAndEdges()
    {
        var topology = CreateG10();

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
                Assert.Equal(
                    3,
                    vertex.IncidentCellIds.Distinct().Count());
                Assert.Equal(
                    3,
                    vertex.IncidentEdgeIds.Distinct().Count());
            });
    }

    [Fact]
    public void CellAdjacencyIsReciprocal()
    {
        var topology = CreateG10();

        foreach (var cell in topology.Cells)
        {
            foreach (var adjacentId in cell.AdjacentCellIds)
            {
                var adjacent =
                    topology.Cells.Single(
                        candidate =>
                            candidate.Id == adjacentId);

                Assert.Contains(
                    cell.Id,
                    adjacent.AdjacentCellIds);
            }
        }
    }

    [Fact]
    public void CellEdgeIncidenceIsReciprocal()
    {
        var topology = CreateG10();

        foreach (var cell in topology.Cells)
        {
            foreach (var edgeId in cell.IncidentEdgeIds)
            {
                var edge =
                    topology.Edges.Single(
                        candidate =>
                            candidate.Id == edgeId);

                Assert.Contains(
                    cell.Id,
                    edge.IncidentCellIds);
            }
        }

        foreach (var edge in topology.Edges)
        {
            foreach (var cellId in edge.IncidentCellIds)
            {
                var cell =
                    topology.Cells.Single(
                        candidate =>
                            candidate.Id == cellId);

                Assert.Contains(
                    edge.Id,
                    cell.IncidentEdgeIds);
            }
        }
    }

    [Fact]
    public void CellVertexIncidenceIsReciprocal()
    {
        var topology = CreateG10();

        foreach (var cell in topology.Cells)
        {
            foreach (var vertexId in cell.IncidentVertexIds)
            {
                var vertex =
                    topology.Vertices.Single(
                        candidate =>
                            candidate.Id == vertexId);

                Assert.Contains(
                    cell.Id,
                    vertex.IncidentCellIds);
            }
        }

        foreach (var vertex in topology.Vertices)
        {
            foreach (var cellId in vertex.IncidentCellIds)
            {
                var cell =
                    topology.Cells.Single(
                        candidate =>
                            candidate.Id == cellId);

                Assert.Contains(
                    vertex.Id,
                    cell.IncidentVertexIds);
            }
        }
    }

    [Fact]
    public void EdgeVertexIncidenceIsReciprocal()
    {
        var topology = CreateG10();

        foreach (var edge in topology.Edges)
        {
            foreach (var vertexId in edge.IncidentVertexIds)
            {
                var vertex =
                    topology.Vertices.Single(
                        candidate =>
                            candidate.Id == vertexId);

                Assert.Contains(
                    edge.Id,
                    vertex.IncidentEdgeIds);
            }
        }

        foreach (var vertex in topology.Vertices)
        {
            foreach (var edgeId in vertex.IncidentEdgeIds)
            {
                var edge =
                    topology.Edges.Single(
                        candidate =>
                            candidate.Id == edgeId);

                Assert.Contains(
                    vertex.Id,
                    edge.IncidentVertexIds);
            }
        }
    }

    [Fact]
    public void EveryAdjacentCellPairHasExactlyOneEdge()
    {
        var topology = CreateG10();

        foreach (var cell in topology.Cells)
        {
            foreach (var adjacentId in cell.AdjacentCellIds)
            {
                if (cell.Id.Value >= adjacentId.Value)
                {
                    continue;
                }

                var matchingEdges =
                    topology.Edges.Count(
                        edge =>
                            edge.IncidentCellIds.Contains(cell.Id)
                            && edge.IncidentCellIds.Contains(adjacentId));

                Assert.Equal(
                    1,
                    matchingEdges);
            }
        }
    }

    [Fact]
    public void CellGraphIsConnected()
    {
        var topology = CreateG10();
        var visited =
            new HashSet<StrategicCellId>();
        var queue =
            new Queue<StrategicCellId>();

        queue.Enqueue(
            topology.Cells[0].Id);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            if (!visited.Add(currentId))
            {
                continue;
            }

            var current =
                topology.Cells.Single(
                    cell =>
                        cell.Id == currentId);

            foreach (var adjacentId in current.AdjacentCellIds)
            {
                if (!visited.Contains(adjacentId))
                {
                    queue.Enqueue(adjacentId);
                }
            }
        }

        Assert.Equal(
            topology.Cells.Count,
            visited.Count);
    }

    [Fact]
    public void G10SatisfiesEulerIdentity()
    {
        var topology = CreateG10();

        var euler =
            topology.Vertices.Count
            - topology.Edges.Count
            + topology.Cells.Count;

        Assert.Equal(
            2,
            euler);
    }

    [Fact]
    public void RepeatedGenerationProducesSameCanonicalTopology()
    {
        var first = CreateG10();
        var second = CreateG10();

        for (var index = 0; index < first.Cells.Count; index++)
        {
            Assert.Equal(
                first.Cells[index].Id,
                second.Cells[index].Id);
            Assert.Equal(
                first.Cells[index].AdjacentCellIds.ToArray(),
                second.Cells[index].AdjacentCellIds.ToArray());
            Assert.Equal(
                first.Cells[index].IncidentEdgeIds.ToArray(),
                second.Cells[index].IncidentEdgeIds.ToArray());
            Assert.Equal(
                first.Cells[index].IncidentVertexIds.ToArray(),
                second.Cells[index].IncidentVertexIds.ToArray());
        }

        for (var index = 0; index < first.Edges.Count; index++)
        {
            Assert.Equal(
                first.Edges[index].Id,
                second.Edges[index].Id);
            Assert.Equal(
                first.Edges[index].IncidentCellIds.ToArray(),
                second.Edges[index].IncidentCellIds.ToArray());
            Assert.Equal(
                first.Edges[index].IncidentVertexIds.ToArray(),
                second.Edges[index].IncidentVertexIds.ToArray());
        }

        for (var index = 0; index < first.Vertices.Count; index++)
        {
            Assert.Equal(
                first.Vertices[index].Id,
                second.Vertices[index].Id);
            Assert.Equal(
                first.Vertices[index].IncidentCellIds.ToArray(),
                second.Vertices[index].IncidentCellIds.ToArray());
            Assert.Equal(
                first.Vertices[index].IncidentEdgeIds.ToArray(),
                second.Vertices[index].IncidentEdgeIds.ToArray());
        }
    }

    [Fact]
    public void CanonicalReferenceCellsHaveExpectedNeighbors()
    {
        var topology = CreateG10();

        Assert.Equal(
            new ulong[] { 2, 3, 4, 5, 6 },
            topology.Cells[0]
                .AdjacentCellIds
                .Select(id => id.Value)
                .ToArray());

        Assert.Equal(
            new ulong[] { 7, 8, 9, 10, 11 },
            topology.Cells[11]
                .AdjacentCellIds
                .Select(id => id.Value)
                .ToArray());
    }

    [Fact]
    public void FirstCanonicalEdgeHasExpectedIncidence()
    {
        var topology = CreateG10();
        var firstEdge = topology.Edges[0];

        Assert.Equal(
            new ulong[] { 1, 2 },
            firstEdge.IncidentCellIds
                .Select(id => id.Value)
                .ToArray());

        Assert.Equal(
            new ulong[] { 1, 2 },
            firstEdge.IncidentVertexIds
                .Select(id => id.Value)
                .ToArray());
    }

    private static StrategicTopology CreateG10()
    {
        return GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(
                1,
                0));
    }
}
