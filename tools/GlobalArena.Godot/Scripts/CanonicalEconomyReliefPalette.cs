using Godot;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R80 presentation-only reuse of the PR277 continuous hypsometric palette,
/// now driven EXCLUSIVELY by the current authoritative V1 economy elevation
/// and sea-level. Never classify economic land using independent 01C biomes;
/// never modify topology, simulation, save, hash, route costs or journal.
/// </summary>
internal static class CanonicalEconomyReliefPalette
{
    /// <summary>
    /// Same physical G18 tint on flat tactical chart and unprojected sphere.
    /// Geography, thermal field, and sea-ice mobility are authoritative;
    /// this is a pure presentation function, not a second world generator.
    /// </summary>
    public static Color FromSignedFineSurface(
        FoundationFineFirstPhysicalGeography physical,
        int index, long minimum, long maximum)
    {
        ArgumentNullException.ThrowIfNull(physical);
        if ((uint)index >= (uint)physical.FineElevationRaw.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        long elevation = physical.FineElevationRaw[index];
        var relief = FromElevation(
            elevation, physical.SeaLevelRaw, minimum, maximum);
        if (elevation <= physical.SeaLevelRaw)
            return physical.SurfaceAtIndex(index) ==
                FoundationFineSurfaceKind.FirmSeaIce
                ? new Color("#d9edf3") : relief;
        float snow = ContinuousLandSnowWeight(physical.FineTemperatureRaw[index]);
        return relief.Lerp(new Color("#edf3ee"), snow);
    }

    public static float ContinuousLandSnowWeight(long temperatureRaw)
    {
        const double cold = -0.70;
        const double warm = -0.55;
        double temperature = temperatureRaw /
            (double)ExperimentalIceTemperatureComposer.Scale;
        double t = Math.Clamp((warm - temperature) / (warm - cold), 0.0, 1.0);
        return (float)(t * t * (3.0 - 2.0 * t));
    }

    public static Color FromElevation(
        long elevation, long seaLevel, long minimum, long maximum)
    {
        if (minimum > maximum || seaLevel < minimum || seaLevel > maximum ||
            elevation < minimum || elevation > maximum)
            throw new ArgumentOutOfRangeException(nameof(elevation),
                "Economy relief must use the same bounded physical field.");

        static float Fraction(long part, long whole) =>
            whole <= 0L ? 0f :
                Mathf.Clamp((float)((double)part / whole), 0f, 1f);

        // Strictly the SAME land/water threshold as physical edge seeding.
        // Depth and height are real continuous values, not biome categories.
        if (elevation <= seaLevel)
        {
            var depth = Fraction(seaLevel - elevation, seaLevel - minimum);
            return new Color("#2a668e").Lerp(new Color("#132b55"), depth);
        }

        var altitude = Fraction(elevation - seaLevel, maximum - seaLevel);
        if (altitude < 0.35f)
            return new Color("#5fa079").Lerp(
                new Color("#a2b86d"), altitude / 0.35f);
        if (altitude < 0.70f)
            return new Color("#a2b86d").Lerp(
                new Color("#aa8159"), (altitude - 0.35f) / 0.35f);
        return new Color("#aa8159").Lerp(
            new Color("#e4e6e3"), (altitude - 0.70f) / 0.30f);
    }
}
