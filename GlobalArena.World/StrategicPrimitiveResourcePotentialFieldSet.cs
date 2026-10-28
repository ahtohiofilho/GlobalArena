namespace GlobalArena.World;

/// <summary>
/// G3-D version-1 DERIVED strategic reference potentials for primitive worker
/// production. These are environmental proxies, NOT a geological inventory,
/// soil-fertility survey, physical-tactical-tile resource map or authoritative
/// resource-depletion state. No new worldgen randomness is introduced.
/// </summary>
public sealed class StrategicPrimitiveResourcePotentialFieldSet
{
    public const uint DerivationVersion = 1U;

    public StrategicScalarField Agriculture { get; }
    public StrategicScalarField WoodGathering { get; }
    public StrategicScalarField SurfaceStone { get; }

    private StrategicPrimitiveResourcePotentialFieldSet(
        StrategicScalarField agriculture,
        StrategicScalarField woodGathering,
        StrategicScalarField surfaceStone)
    {
        Agriculture = agriculture;
        WoodGathering = woodGathering;
        SurfaceStone = surfaceStone;
    }

    public static StrategicPrimitiveResourcePotentialFieldSet Derive(
        WorldGenerationResult generatedWorld)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);

        var graph = generatedWorld.StrategicSurfaceGraph;
        var physical = generatedWorld.StrategicPhysicalFields;
        var climate = generatedWorld.StrategicClimateFields;
        var biomes = generatedWorld.StrategicBiomes;
        var general = generatedWorld.StrategicResourcePotentialFields.GeneralPotential;

        if (!ReferenceEquals(physical.Relief.SurfaceGraph, graph) ||
            !ReferenceEquals(climate.Temperature.SurfaceGraph, graph) ||
            !ReferenceEquals(climate.Moisture.SurfaceGraph, graph) ||
            !ReferenceEquals(climate.WaterAvailability.SurfaceGraph, graph) ||
            !ReferenceEquals(general.SurfaceGraph, graph) ||
            !ReferenceEquals(biomes.SurfaceGraph, graph))
            throw new InvalidOperationException(
                "Primitive resource proxies require one generated strategic surface graph.");

        const long d = StrategicScalarField.Denominator;
        var crops = new long[graph.NodeCount];
        var wood = new long[graph.NodeCount];
        var stone = new long[graph.NodeCount];

        for (var index = 0; index < graph.NodeCount; index++)
        {
            if (physical.LandWater.GetKind(index) != StrategicLandWaterKind.Land)
                continue;

            var biome = biomes.GetKind(index);
            if (biome == StrategicBiomeKind.PolarIce)
                continue;

            var water = climate.WaterAvailability.GetRawValue(index);
            var moisture = climate.Moisture.GetRawValue(index);
            var temperature = climate.Temperature.GetRawValue(index);
            var relief = physical.Relief.GetRawValue(index);
            var generalPotential = general.GetRawValue(index);

            CheckNormalized(water, nameof(climate.WaterAvailability));
            CheckNormalized(moisture, nameof(climate.Moisture));
            CheckNormalized(relief, nameof(physical.Relief));
            CheckNormalized(generalPotential, nameof(general));
            if (temperature < -d || temperature > d)
                throw new InvalidOperationException(
                    "Generated temperature is outside the accepted normalized range.");

            // Cultivation proxy: access to water, temperature comfort and
            // gentle relief jointly determine yield per worker. No facility
            // unlock, binary fertility threshold or bonus for ownership.
            var comfort = d - Math.Abs(temperature);
            var flatness = d - relief;
            crops[index] = checked((long)(
                ((Int128)water * comfort * flatness) / ((Int128)d * d)));

            // Forest cover is currently observable only via biome class;
            // non-forest land does NOT grow free timber. Forest moisture
            // governs the provisional productivity, not a fixed extra bonus.
            wood[index] = biome == StrategicBiomeKind.Forest ||
                biome == StrategicBiomeKind.Rainforest
                ? moisture : 0L;

            // Surface stone is only a proxy combining already generated
            // general resource potential and ruggedness; no quarry deposit,
            // rock type, ore, or proven extractable tonnage is asserted.
            stone[index] = checked((generalPotential + relief) / 2L);
        }

        return new StrategicPrimitiveResourcePotentialFieldSet(
            new StrategicScalarField(graph, crops),
            new StrategicScalarField(graph, wood),
            new StrategicScalarField(graph, stone));
    }

    private static void CheckNormalized(long value, string source)
    {
        if (value < 0L || value > StrategicScalarField.Denominator)
            throw new InvalidOperationException(
                $"Generated {source} is outside the normalized range.");
    }
}
