using System;
using System.Linq;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2CellIdentityRuntimeTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void SharedTopologyPreservesEveryCellAndNeighborhood(int n, int m)
    {
        var topology = GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(n, m));
        // Exercise the public V2 path rather than the internal geometry generator.
        var request = new WorldGenerationRequest(
            new WorldSeed(42UL),
            WorldGenerationVersion.ProceduralBiomesV2,
            new GoldbergParameters(n, m));
        var geometry = WorldGenerationV2EntryPoint.GeneratePhysical(request).Geometry;
        var graph = new StrategicSurfaceGraph(topology);

        Assert.Same(topology, graph.StrategicTopology);
        Assert.Equal(topology.Cells.Count, geometry.Cells.Count);
        Assert.Equal(topology.Cells.Count, graph.NodeCount);

        for (var index = 0; index < graph.NodeCount; index++)
        {
            var canonical = topology.Cells[index];
            var id = canonical.Id;
            Assert.Equal(id, geometry.Cells[index].CellId);
            Assert.Equal(id, graph.GetCellId(index));
            Assert.Equal(index, graph.GetNodeIndex(id));
            Assert.Same(geometry.Cells[index], geometry.GetCell(id));
            var expectedNeighbors = canonical.AdjacentCellIds
                .Select(neighbor => checked((int)neighbor.Value - 1)).OrderBy(value => value);
            Assert.Equal(expectedNeighbors, graph.GetNeighborIndexes(index));
            foreach (var neighborIndex in graph.GetNeighborIndexes(index))
            {
                Assert.Contains(index, graph.GetNeighborIndexes(neighborIndex));
                Assert.Equal(topology.Cells[neighborIndex].Id, geometry.Cells[neighborIndex].CellId);
            }
        }
    }
}
