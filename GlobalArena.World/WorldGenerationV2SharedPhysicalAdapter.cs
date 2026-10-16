using System;

namespace GlobalArena.World;

/// <summary>
/// Shares V2 physical scalar values with canonical graph-based consumers.
/// Does not activate V1 hydrology, climate, resources, or biome generation.
/// </summary>
public sealed class WorldGenerationV2SharedPhysicalAdapter
{
    public StrategicSurfaceGraph SurfaceGraph { get; }
    public StrategicScalarField Elevation { get; }
    public StrategicScalarField Moisture { get; }
    public StrategicScalarField Temperature { get; }
    public StrategicSeaLevel SeaLevel { get; }
    public StrategicLandWaterMap LandWater { get; }

    private WorldGenerationV2SharedPhysicalAdapter(
        StrategicSurfaceGraph graph, StrategicScalarField elevation,
        StrategicScalarField moisture, StrategicScalarField temperature,
        StrategicSeaLevel seaLevel, StrategicLandWaterMap landWater)
    {
        SurfaceGraph = graph;
        Elevation = elevation;
        Moisture = moisture;
        Temperature = temperature;
        SeaLevel = seaLevel;
        LandWater = landWater;
    }

    public static WorldGenerationV2SharedPhysicalAdapter Create(
        WorldGenerationRequest request, WorldGenerationV2PhysicalComposition fields)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fields);
        if (request.Version != WorldGenerationVersion.ProceduralBiomesV2)
            throw new NotSupportedException("Shared V2 fields require ProceduralBiomesV2.");

        var topology = GoldbergStrategicTopologyGenerator.Generate(request.StrategicParameters);
        var graph = new StrategicSurfaceGraph(topology);
        var count = graph.NodeCount;
        if (fields.Geometry.Cells.Count != count || fields.Elevation.Count != count ||
            fields.Moisture.Count != count || fields.Temperature.Count != count ||
            fields.Biomes.Count != count)
            throw new ArgumentException("V2 physical field counts must match the strategic graph.", nameof(fields));

        for (var index = 0; index < count; index++)
            if (fields.Geometry.Cells[index].CellId != graph.GetCellId(index))
                throw new ArgumentException("V2 geometry cell identity differs from strategic graph.", nameof(fields));

        // Copy only scalar values. No generator is re-executed by the adapter.
        var elevation = new StrategicScalarField(graph, fields.Elevation);
        var moisture = new StrategicScalarField(graph, fields.Moisture);
        var temperature = new StrategicScalarField(graph, fields.Temperature);
        var seaLevel = new StrategicSeaLevel(fields.SeaLevel);
        var landWater = new StrategicLandWaterMap(elevation, seaLevel);
        return new WorldGenerationV2SharedPhysicalAdapter(
            graph, elevation, moisture, temperature, seaLevel, landWater);
    }
}