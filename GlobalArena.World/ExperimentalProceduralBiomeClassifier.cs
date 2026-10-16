using System;
using System.Collections.Generic;

namespace GlobalArena.World;

/// <summary>01C experimental biome classification. Inputs remain authoritative and unchanged.</summary>
public static class ExperimentalProceduralBiomeClassifier
{
    public enum Biome { Ocean, Desert, Steppe, Prairie, Forest, Hills, Mountains, Ice }
    public readonly record struct Tile(Biome VisibleBiome, Biome UnderlyingBiome, bool IsWater);

    // Experimental thresholds; not final terrain calibration.
    public const long HillElevation = 250_000L;
    public const long MountainElevation = 600_000L;
    public const long IceThreshold = -550_000L;

    public static Tile Classify(long elevationRaw,
        ExperimentalRandomMoisturePatchGenerator.MoistureBand moistureBand,
        long thermalRaw, long seaLevelRaw = 0L)
    {
        if (elevationRaw < -1_000_000 || elevationRaw > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(elevationRaw));
        if (thermalRaw < -1_000_000 || thermalRaw > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(thermalRaw));
        if (seaLevelRaw < -1_000_000 || seaLevelRaw > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(seaLevelRaw));
        if (!Enum.IsDefined(typeof(ExperimentalRandomMoisturePatchGenerator.MoistureBand), moistureBand))
            throw new ArgumentOutOfRangeException(nameof(moistureBand));
        bool water = elevationRaw < seaLevelRaw;
        Biome underlying;
        if (water) underlying = Biome.Ocean;
        else
        {
            long aboveSea = elevationRaw - seaLevelRaw;
            if (aboveSea >= MountainElevation) underlying = Biome.Mountains;
            else if (aboveSea >= HillElevation) underlying = Biome.Hills;
            else underlying = moistureBand switch
            {
                ExperimentalRandomMoisturePatchGenerator.MoistureBand.Arid => Biome.Desert,
                ExperimentalRandomMoisturePatchGenerator.MoistureBand.SemiArid => Biome.Steppe,
                ExperimentalRandomMoisturePatchGenerator.MoistureBand.Fertile => Biome.Prairie,
                ExperimentalRandomMoisturePatchGenerator.MoistureBand.Humid => Biome.Forest,
                _ => throw new ArgumentOutOfRangeException(nameof(moistureBand))
            };
        }
        return new Tile(thermalRaw <= IceThreshold ? Biome.Ice : underlying, underlying, water);
    }

    public static IReadOnlyList<Tile> Generate(
        IReadOnlyList<long> elevationRaw, IReadOnlyList<long> moistureRaw,
        IReadOnlyList<long> temperatureRaw, long seaLevelRaw = 0L)
    {
        ArgumentNullException.ThrowIfNull(elevationRaw);
        ArgumentNullException.ThrowIfNull(moistureRaw);
        ArgumentNullException.ThrowIfNull(temperatureRaw);
        if (elevationRaw.Count != moistureRaw.Count || elevationRaw.Count != temperatureRaw.Count)
            throw new ArgumentException("Physical fields must share the same spherical grid.");
        var thresholds = ExperimentalPlanetMoistureQuartiles.Calculate(moistureRaw);
        var output = new Tile[elevationRaw.Count];
        for (int i = 0; i < output.Length; i++)
        {
            var band = ExperimentalPlanetMoistureQuartiles.Classify(moistureRaw[i], thresholds);
            output[i] = Classify(elevationRaw[i], band, temperatureRaw[i], seaLevelRaw);
        }
        return Array.AsReadOnly(output);
    }
}