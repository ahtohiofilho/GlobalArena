using System;
using System.Linq;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2StartingPointSelectorTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void DiscardsTemporaryReferenceAndReturnsUniqueEligibleCells(int n, int m)
    {
        var topology = GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(n, m));
        var graph = new StrategicSurfaceGraph(topology);
        const int reference = 0;
        var selected = WorldGenerationV2StartingPointSelector.Select(graph, reference, 3,
            index => index % 2 == 1, (from, to) => 1);
        Assert.Equal(3, selected.Count);
        Assert.Equal(3, selected.Distinct().Count());
        Assert.All(selected, id =>
        {
            var index = graph.GetNodeIndex(id);
            Assert.NotEqual(reference, index);
            Assert.True(index % 2 == 1);
        });
    }

    [Fact]
    public void SameInputsHaveIdenticalOrderedResults()
    {
        var graph = new StrategicSurfaceGraph(GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(2, 0)));
        long WeightedCost(int from, int to) => (from % 3 == 0 || to % 3 == 0) ? 7L : 1L;
        var a = WorldGenerationV2StartingPointSelector.Select(graph, 0, 5, i => i != 0, WeightedCost);
        var b = WorldGenerationV2StartingPointSelector.Select(graph, 0, 5, i => i != 0, WeightedCost);
        Assert.Equal(a, b);
    }

    [Fact]
    public void InvalidCostIsRejected()
    {
        var graph = new StrategicSurfaceGraph(GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldGenerationV2StartingPointSelector.Select(graph, 0, 1, _ => true, (_, _) => -1));
    }
}
