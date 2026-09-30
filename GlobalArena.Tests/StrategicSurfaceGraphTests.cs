using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicSurfaceGraphTests
{
    [Fact]
    public void ConstructorRejectsNullTopology()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicSurfaceGraph(
                    null!));
    }

    [Fact]
    public void NodeCountMatchesTopology()
    {
        var topology =
            CreateTopology();

        var graph =
            new StrategicSurfaceGraph(
                topology);

        Assert.Equal(
            topology.Cells.Count,
            graph.NodeCount);
    }

    [Fact]
    public void CellIdsFollowCanonicalIndexes()
    {
        var graph =
            CreateGraph();

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            Assert.Equal(
                checked((ulong)index + 1UL),
                graph.CellIds[index].Value);
        }
    }

    [Fact]
    public void GetNodeIndexMapsCanonicalIds()
    {
        var graph =
            CreateGraph();

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            Assert.Equal(
                index,
                graph.GetNodeIndex(
                    graph.CellIds[index]));
        }
    }

    [Fact]
    public void GetCellIdMapsCanonicalIndexes()
    {
        var graph =
            CreateGraph();

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            Assert.Equal(
                graph.CellIds[index],
                graph.GetCellId(
                    index));
        }
    }

    [Fact]
    public void NeighborIndexesMatchTopologyAdjacency()
    {
        var topology =
            CreateTopology();

        var graph =
            new StrategicSurfaceGraph(
                topology);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var expected =
                topology.Cells[index]
                    .AdjacentCellIds
                    .Select(
                        id =>
                            checked(
                                (int)(
                                    id.Value
                                    - 1UL)))
                    .Order()
                    .ToArray();

            Assert.Equal(
                expected,
                graph.GetNeighborIndexes(
                    index));
        }
    }

    [Fact]
    public void NeighborIndexesAreCanonicalAscending()
    {
        var graph =
            CreateGraph();

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var neighbors =
                graph.GetNeighborIndexes(
                    index);

            Assert.Equal(
                neighbors.Order().ToArray(),
                neighbors);
        }
    }

    [Fact]
    public void PentagonAndHexagonDegreesMatchTopology()
    {
        var topology =
            CreateTopology();

        var graph =
            new StrategicSurfaceGraph(
                topology);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var expectedDegree =
                topology.Cells[index].Kind switch
                {
                    StrategicCellKind.Pentagon => 5,
                    StrategicCellKind.Hexagon => 6,
                    _ => throw new InvalidOperationException()
                };

            Assert.Equal(
                expectedDegree,
                graph.GetNeighborIndexes(
                    index).Count);
        }
    }

    [Fact]
    public void NeighborRelationIsReciprocal()
    {
        var graph =
            CreateGraph();

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            foreach (var neighborIndex
                in graph.GetNeighborIndexes(
                    index))
            {
                Assert.Contains(
                    index,
                    graph.GetNeighborIndexes(
                        neighborIndex));
            }
        }
    }

    [Fact]
    public void CellIdsSnapshotIsReadOnly()
    {
        var graph =
            CreateGraph();

        var list =
            Assert.IsAssignableFrom<
                IList<StrategicCellId>>(
                graph.CellIds);

        Assert.Throws<NotSupportedException>(
            () =>
                list.Add(
                    new StrategicCellId(
                        1UL)));
    }

    [Fact]
    public void NeighborSnapshotIsReadOnly()
    {
        var graph =
            CreateGraph();

        var neighbors =
            Assert.IsAssignableFrom<
                IList<int>>(
                graph.GetNeighborIndexes(
                    0));

        Assert.Throws<NotSupportedException>(
            () =>
                neighbors.Add(
                    0));
    }

    [Fact]
    public void DefaultCellIdIsRejected()
    {
        var graph =
            CreateGraph();

        Assert.Throws<ArgumentException>(
            () =>
                graph.GetNodeIndex(
                    default));
    }

    [Fact]
    public void ForeignCellIdIsRejected()
    {
        var graph =
            CreateGraph();

        var foreign =
            new StrategicCellId(
                checked(
                    (ulong)graph.NodeCount
                    + 1UL));

        Assert.Throws<KeyNotFoundException>(
            () =>
                graph.GetNodeIndex(
                    foreign));
    }

    [Fact]
    public void OutOfRangeNodeIndexesAreRejected()
    {
        var graph =
            CreateGraph();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                graph.GetCellId(
                    -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                graph.GetNeighborIndexes(
                    graph.NodeCount));
    }

    [Fact]
    public void RepeatedConstructionProducesSameSignature()
    {
        var topology =
            CreateTopology();

        var first =
            new StrategicSurfaceGraph(
                topology);

        var second =
            new StrategicSurfaceGraph(
                topology);

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));
    }

    [Fact]
    public void GeneratorResultIncludesSurfaceGraphOverAuthoritativeTopology()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            new WorldGenerationRequest(
                new WorldSeed(
                    77UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        var result =
            generator.Generate(
                request);

        Assert.Same(
            result.StrategicTopology,
            result.StrategicSurfaceGraph.StrategicTopology);

        Assert.Equal(
            result.StrategicTopology.Cells.Count,
            result.StrategicSurfaceGraph.NodeCount);
    }

    private static StrategicTopology CreateTopology()
    {
        return GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(
                2,
                1));
    }

    private static StrategicSurfaceGraph CreateGraph()
    {
        return new StrategicSurfaceGraph(
            CreateTopology());
    }

    private static string CreateSignature(
        StrategicSurfaceGraph graph)
    {
        return string.Join(
            "|",
            Enumerable.Range(
                0,
                graph.NodeCount)
                .Select(
                    index =>
                        string.Concat(
                            graph.GetCellId(index).Value,
                            ":",
                            string.Join(
                                ",",
                                graph.GetNeighborIndexes(
                                    index)))));
    }
}
