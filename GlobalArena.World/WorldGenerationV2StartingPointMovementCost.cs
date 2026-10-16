using System;

namespace GlobalArena.World;

/// <summary>Parameterized movement costs for V2 initial placement; no economic coefficients fixed.</summary>
public static class WorldGenerationV2StartingPointMovementCost
{
    public static Func<int, int, long> Create(
        WorldGenerationV2AgriculturalStrategicResult result,
        Func<long, long> landProductivityCost,
        long landWaterTransitionCost)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(landProductivityCost);
        if (landWaterTransitionCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(landWaterTransitionCost));
        var physical = result.Physical;
        var graph = physical.SurfaceGraph;
        var landWater = physical.LandWater;
        var agriculture = result.AgriculturalProductivity;
        if (!ReferenceEquals(agriculture.SurfaceGraph, graph) ||
            !ReferenceEquals(landWater.Elevation.SurfaceGraph, graph))
            throw new InvalidOperationException("Movement inputs must share the V2 strategic graph.");
        for (var i = 0; i < graph.NodeCount; i++)
        {
            if (landWater.GetKind(i) != StrategicLandWaterKind.Land) continue;
            var cost = landProductivityCost(agriculture.GetRawValue(i));
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(landProductivityCost));
        }
        return (from, to) =>
        {
            if (from < 0 || from >= graph.NodeCount) throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0 || to >= graph.NodeCount) throw new ArgumentOutOfRangeException(nameof(to));
            var fromLand = landWater.GetKind(from) == StrategicLandWaterKind.Land;
            var toLand = landWater.GetKind(to) == StrategicLandWaterKind.Land;
            var destinationCost = toLand ? landProductivityCost(agriculture.GetRawValue(to)) : 0L;
            if (destinationCost < 0) throw new ArgumentOutOfRangeException(nameof(landProductivityCost));
            return fromLand != toLand
                ? checked(landWaterTransitionCost + destinationCost)
                : destinationCost;
        };
    }
}
