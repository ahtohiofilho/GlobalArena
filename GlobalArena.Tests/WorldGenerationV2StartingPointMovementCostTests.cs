using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2StartingPointMovementCostTests
{
    [Fact]
    public void NullResultIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorldGenerationV2StartingPointMovementCost.Create(null!, _ => 1L, 10L));
    }

    [Fact]
    public void NullCostFunctionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorldGenerationV2StartingPointMovementCost.Create(null!, null!, 10L));
    }

    [Theory]
    [InlineData(1, 0, 17UL, 25)]
    [InlineData(2, 0, 29UL, 35)]
    [InlineData(3, 0, 41UL, 45)]
    public void DirectedEdgesRespectDestinationCostAndParameterizedBoundary(
        int n, int m, ulong seed, int landPercent)
    {
        var result = Build(n, m, seed, landPercent);
        const long boundary = 913;
        static long AgriculturalCost(long value) => value / 1000 + 17;
        var cost = WorldGenerationV2StartingPointMovementCost.Create(result, AgriculturalCost, boundary);
        var graph = result.Physical.SurfaceGraph;
        var kinds = result.Physical.LandWater;
        var counts = new int[3];
        for (var from = 0; from < graph.NodeCount; from++)
        {
            foreach (var to in graph.GetNeighborIndexes(from))
            {
                var fromLand = kinds.GetKind(from) == StrategicLandWaterKind.Land;
                var toLand = kinds.GetKind(to) == StrategicLandWaterKind.Land;
                var destination = toLand ? AgriculturalCost(result.AgriculturalProductivity.GetRawValue(to)) : 0L;
                var expected = fromLand != toLand ? boundary + destination : destination;
                Assert.Equal(expected, cost(from, to));
                counts[fromLand != toLand ? 2 : (toLand ? 1 : 0)]++;
            }
        }
        // Each generated world must contain water and transitions. Land-to-land
        // is assessed across a larger set of worlds by the companion theory.
        Assert.True(counts[0] > 0, "Expected at least one water-to-water edge.");
        Assert.True(counts[2] > 0, "Expected at least one land/water boundary edge.");
    }

    [Fact]
    public void LandEdgesExistAndBoundaryParameterChangesOnlyCrossings()
    {
        var landEdges = 0;
        var crossingEdges = 0;
        foreach (var (n, m, seed, percent) in new (int, int, ulong, int)[]
        {
            // Percentages 35 and 45 are already proven valid by the existing V2 test cases.
            // R3 rejected 65 before reaching its 90/95 samples.
            (2, 0, 29UL, 35), (3, 0, 41UL, 45), (3, 0, 53UL, 45),
            (3, 0, 71UL, 45), (4, 0, 73UL, 45)
        })
        {
            var result = Build(n, m, seed, percent);
            var graph = result.Physical.SurfaceGraph;
            var kinds = result.Physical.LandWater;
            var low = WorldGenerationV2StartingPointMovementCost.Create(result, x => x / 1000 + 5, 101);
            var high = WorldGenerationV2StartingPointMovementCost.Create(result, x => x / 1000 + 5, 707);
            for (var from = 0; from < graph.NodeCount; from++)
            foreach (var to in graph.GetNeighborIndexes(from))
            {
                var a = kinds.GetKind(from) == StrategicLandWaterKind.Land;
                var b = kinds.GetKind(to) == StrategicLandWaterKind.Land;
                if (a != b)
                {
                    crossingEdges++;
                    Assert.Equal(606L, high(from, to) - low(from, to));
                }
                else
                {
                    Assert.Equal(low(from, to), high(from, to));
                    if (a) landEdges++;
                }
            }
        }
        Assert.True(landEdges > 0, "Expected land-to-land edges in sampled V2 worlds.");
        Assert.True(crossingEdges > 0, "Expected boundary edges in sampled V2 worlds.");
    }

    [Fact]
    public void SelectorConsumesV2CostDelegateDeterministically()
    {
        var result = Build(3, 0, 41UL, 45);
        var graph = result.Physical.SurfaceGraph;
        var movement = WorldGenerationV2StartingPointMovementCost.Create(result, x => x / 1000 + 1, 10);
        var invoked = 0;
        long CountedMovement(int from, int to)
        {
            invoked++;
            return movement(from, to);
        }
        var first = WorldGenerationV2StartingPointSelector.Select(graph, 0, 3,
            index => index != 0, CountedMovement);
        Assert.True(invoked > 0, "Selector did not invoke the real V2 movement delegate.");
        var second = WorldGenerationV2StartingPointSelector.Select(graph, 0, 3,
            index => index != 0, movement);
        Assert.Equal(first, second);
        Assert.Equal(3, first.Distinct().Count());
        Assert.All(first, id => Assert.NotEqual(0, graph.GetNodeIndex(id)));
    }

    private static WorldGenerationV2AgriculturalStrategicResult Build(
        int n, int m, ulong seed, int landPercent)
    {
        var request = new WorldGenerationRequest(new WorldSeed(seed),
            WorldGenerationVersion.ProceduralBiomesV2, new GoldbergParameters(n, m));
        return WorldGenerationV2AgriculturalStrategicResult.Create(
            WorldGenerationV2PhysicalResult.Generate(request, landPercent));
    }
}
