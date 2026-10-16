using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalArena.World;

/// <summary>Version 2 physical composition candidate. No canonical activation.</summary>
public sealed class WorldGenerationV2PhysicalComposition
{
    public StrategicSphericalGeometry Geometry { get; }
    public IReadOnlyList<long> Elevation { get; }
    public IReadOnlyList<long> Moisture { get; }
    public IReadOnlyList<long> Temperature { get; }
    public long SeaLevel { get; }
    public int LandTargetPercent { get; }
    public IReadOnlyList<ExperimentalProceduralBiomeClassifier.Tile> Biomes { get; }

    private WorldGenerationV2PhysicalComposition(StrategicSphericalGeometry geometry,
        IReadOnlyList<long> elevation, IReadOnlyList<long> moisture,
        IReadOnlyList<long> temperature, long seaLevel, int landTargetPercent,
        IReadOnlyList<ExperimentalProceduralBiomeClassifier.Tile> biomes)
    {
        Geometry = geometry;
        Elevation = elevation;
        Moisture = moisture;
        Temperature = temperature;
        SeaLevel = seaLevel;
        LandTargetPercent = landTargetPercent;
        Biomes = biomes;
    }

    public static WorldGenerationV2PhysicalComposition Generate(StrategicSphericalGeometry geometry, ulong seed, int landTargetPercent = 35)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (landTargetPercent < 25 || landTargetPercent > 45 || landTargetPercent % 5 != 0)
            throw new ArgumentOutOfRangeException(nameof(landTargetPercent));
        var elevation = TacticalIndependentScaleGenerator.GenerateWeighted(geometry, seed, 0.5, 2.0, 3.0);
        var moisture = ExperimentalRandomMoisturePatchGenerator.Generate(geometry, seed);
        var temperature = ExperimentalIceTemperatureComposer.Generate(geometry, elevation, moisture);
        // Exact BIOMES ULTRA percentile rule: classify relative to elevation, not temperature.
        var sortedElevation = elevation.OrderBy(value => value).ToArray();
        int cutoffIndex = Math.Clamp((int)Math.Floor(elevation.Count * (100.0 - landTargetPercent) / 100.0), 0, elevation.Count - 1);
        long seaLevel = sortedElevation[cutoffIndex];
        var biomes = ExperimentalProceduralBiomeClassifier.Generate(elevation, moisture, temperature, seaLevel);
        if (elevation.Count != geometry.Cells.Count || moisture.Count != geometry.Cells.Count ||
            temperature.Count != geometry.Cells.Count || biomes.Count != geometry.Cells.Count)
            throw new InvalidOperationException("V2 physical fields must share a single cell geometry.");
        return new WorldGenerationV2PhysicalComposition(geometry, elevation, moisture, temperature, seaLevel, landTargetPercent, biomes);
    }
}
