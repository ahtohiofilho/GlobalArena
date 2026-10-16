using System;
using System.Collections.Generic;

namespace GlobalArena.World;

/// <summary>V2 agricultural productivity, independent of legacy habitability and biome bans.</summary>
public static class WorldGenerationV2AgriculturalProductivity
{
    // Interpolated approved moisture response: percentage moisture -> percentage productivity.
    private static readonly (long Moisture, long Yield)[] Curve =
    {
        (0, 50_000), (100_000, 120_000), (200_000, 250_000),
        (300_000, 390_000), (400_000, 600_000), (500_000, 800_000),
        (600_000, 950_000), (650_000, 1_000_000), (700_000, 970_000),
        (750_000, 920_000), (800_000, 860_000), (850_000, 810_000),
        (900_000, 750_000), (950_000, 700_000), (1_000_000, 650_000)
    };

    public static long MoistureYield(long moisture)
    {
        if (moisture < 0 || moisture > StrategicScalarField.Denominator)
            throw new ArgumentOutOfRangeException(nameof(moisture));
        for (var i = 1; i < Curve.Length; i++)
        {
            var right = Curve[i];
            if (moisture > right.Moisture) continue;
            var left = Curve[i - 1];
            return left.Yield + (long)(((Int128)(right.Yield - left.Yield) *
                (moisture - left.Moisture)) / (right.Moisture - left.Moisture));
        }
        return Curve[^1].Yield;
    }

    public static StrategicScalarField Generate(WorldGenerationV2SharedPhysicalAdapter physical)
    {
        ArgumentNullException.ThrowIfNull(physical);
        var graph = physical.SurfaceGraph;
        if (!ReferenceEquals(graph, physical.Moisture.SurfaceGraph) ||
            !ReferenceEquals(graph, physical.Elevation.SurfaceGraph))
            throw new ArgumentException("Agricultural inputs must share one strategic graph.", nameof(physical));
        var count = graph.NodeCount;
        var local = new long[count];
        for (var i = 0; i < count; i++)
            local[i] = MoistureYield(physical.Moisture.GetRawValue(i));
        var result = new long[count];
        for (var root = 0; root < count; root++)
        {
            // Water contributes no agricultural productive capacity; Ice does not block land.
            if (physical.LandWater.GetKind(root) == StrategicLandWaterKind.Water)
            {
                result[root] = 0;
                continue;
            }
            var visited = new HashSet<int> { root };
            var frontier = new List<int> { root };
            Int128 weightedSum = 0;
            long totalWeight = 0;
            for (var ring = 1; ring <= 3; ring++)
            {
                var next = new List<int>();
                foreach (var node in frontier)
                    foreach (var neighbor in graph.GetNeighborIndexes(node))
                        if (visited.Add(neighbor)) next.Add(neighbor);
                var ringWeight = 4 - ring;
                foreach (var index in next)
                {
                    // Only land cells supply surrounding agricultural productivity.
                    if (physical.LandWater.GetKind(index) != StrategicLandWaterKind.Land) continue;
                    weightedSum += (Int128)local[index] * ringWeight;
                    totalWeight += ringWeight;
                }
                frontier = next;
                if (frontier.Count == 0) break;
            }
            var neighborValue = totalWeight > 0 ? (long)(weightedSum / totalWeight) : local[root];
            result[root] = (local[root] + neighborValue) / 2;
        }
        return new StrategicScalarField(graph, result);
    }
}