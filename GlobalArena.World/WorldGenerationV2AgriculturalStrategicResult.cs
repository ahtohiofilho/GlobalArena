using System;

namespace GlobalArena.World;

/// <summary>
/// Opt-in V2 strategic aggregation of existing hydrology and agriculture.
/// Does not replace the V1 world result or recalculate physical geography.
/// </summary>
public sealed class WorldGenerationV2AgriculturalStrategicResult
{
    public WorldGenerationV2HydrologyResult HydrologyResult { get; }
    public StrategicScalarField AgriculturalProductivity { get; }
    public WorldGenerationV2SharedPhysicalAdapter Physical => HydrologyResult.Physical;

    private WorldGenerationV2AgriculturalStrategicResult(
        WorldGenerationV2HydrologyResult hydrologyResult,
        StrategicScalarField agriculturalProductivity)
    {
        HydrologyResult = hydrologyResult;
        AgriculturalProductivity = agriculturalProductivity;
    }

    public static WorldGenerationV2AgriculturalStrategicResult Create(
        WorldGenerationV2PhysicalResult physicalResult)
    {
        ArgumentNullException.ThrowIfNull(physicalResult);
        var hydrologyResult = WorldGenerationV2HydrologyResult.Create(physicalResult);
        var agriculture = WorldGenerationV2AgriculturalProductivity.Generate(hydrologyResult.Physical);
        if (!ReferenceEquals(agriculture.SurfaceGraph, hydrologyResult.Physical.SurfaceGraph))
            throw new InvalidOperationException("V2 agricultural field must share the hydrology graph.");
        return new WorldGenerationV2AgriculturalStrategicResult(hydrologyResult, agriculture);
    }
}