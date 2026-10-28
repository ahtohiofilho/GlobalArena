namespace GlobalArena.World;

/// <summary>
/// G3-E: opt-in, deterministic version-2 environmental endowment proxies.
/// Inputs are existing climate and relief plus an independently seeded,
/// neighborhood-smoothed lithology proxy. These are NOT surveyed mineral
/// deposits, fine-tile resource locations or depletable stock quantities.
/// </summary>
public sealed class StrategicPrimitiveNaturalEndowmentFieldSet
{
    public const uint DerivationVersion = 2U;

    public StrategicScalarField SoilSuitability { get; }
    public StrategicScalarField WoodyVegetation { get; }
    public StrategicScalarField ExposedStone { get; }
    public StrategicScalarField LithologyProxy { get; }

    private StrategicPrimitiveNaturalEndowmentFieldSet(
        StrategicScalarField soil, StrategicScalarField woody,
        StrategicScalarField stone, StrategicScalarField lithology)
    {
        SoilSuitability = soil;
        WoodyVegetation = woody;
        ExposedStone = stone;
        LithologyProxy = lithology;
    }

    public static StrategicPrimitiveNaturalEndowmentFieldSet Derive(
        WorldGenerationResult world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var graph = world.StrategicSurfaceGraph;
        var physical = world.StrategicPhysicalFields;
        var climate = world.StrategicClimateFields;
        var biomes = world.StrategicBiomes;

        if (!ReferenceEquals(physical.Elevation.SurfaceGraph, graph)
            || !ReferenceEquals(physical.Relief.SurfaceGraph, graph)
            || !ReferenceEquals(climate.Moisture.SurfaceGraph, graph)
            || !ReferenceEquals(climate.Temperature.SurfaceGraph, graph)
            || !ReferenceEquals(climate.WaterAvailability.SurfaceGraph, graph)
            || !ReferenceEquals(biomes.SurfaceGraph, graph))
            throw new InvalidOperationException(
                "Natural endowments require matching generated strategic fields.");

        const long d = StrategicScalarField.Denominator;
        var count = graph.NodeCount;
        var land = new bool[count];
        var localWoody = new long[count];
        var rawRock = new long[count];
        var seedStream = WorldGenerationRandomStreamFactory.Create(
            world.Request, WorldGenerationRandomDomain.PrimitiveLithology);

        for (var i = 0; i < count; i++)
        {
            // Sampling for EVERY node retains fixed stream consumption even
            // when ocean or polar masks vary between generated worlds.
            rawRock[i] = checked((long)(seedStream.NextUInt64() % ((ulong)d + 1UL)));

            land[i] = physical.LandWater.GetKind(i) == StrategicLandWaterKind.Land
                && biomes.GetKind(i) != StrategicBiomeKind.PolarIce;
            if (!land[i])
                continue;

            var moisture = climate.Moisture.GetRawValue(i);
            var temperature = climate.Temperature.GetRawValue(i);
            CheckNormalized(moisture, nameof(climate.Moisture));
            if (temperature < -d || temperature > d)
                throw new InvalidOperationException("Generated temperature is out of range.");

            var comfort = d - Math.Abs(temperature);
            localWoody[i] = checked((long)(((Int128)moisture * comfort) / d));
        }

        var soil = new long[count];
        var vegetation = new long[count];
        var exposedStone = new long[count];
        var lithology = new long[count];

        for (var i = 0; i < count; i++)
        {
            if (!land[i])
                continue;

            // One-ring, land-only spatial correlation. Cells share most
            // samples with their neighbors, without generating independent
            // random resources or granting an infrastructure productivity buff.
            var totalRock = (Int128)rawRock[i] * 2;
            var totalWoody = (Int128)localWoody[i] * 2;
            long sampleCount = 2;
            foreach (var neighbor in graph.GetNeighborIndexes(i))
            {
                if (!land[neighbor])
                    continue;
                totalRock += rawRock[neighbor];
                totalWoody += localWoody[neighbor];
                sampleCount++;
            }

            var rock = checked((long)(totalRock / sampleCount));
            lithology[i] = rock;
            vegetation[i] = checked((long)(totalWoody / sampleCount));

            // The accepted elevation generator emits [-D,+D], therefore
            // its maximum neighbor delta is 2D, NOT D.
            var rawRelief = physical.Relief.GetRawValue(i);
            if (rawRelief < 0L || rawRelief > 2L * d)
                throw new InvalidOperationException("Generated relief is out of range.");
            var relief = rawRelief / 2L;
            var flatness = d - relief;

            // Soil quality and surface rock are separate, competing proxies:
            // smooth, less rocky ground is easier to cultivate; exposed stone
            // is easier to gather when rocky geology meets rugged terrain.
            soil[i] = checked((long)(((Int128)flatness * (d - rock)) / d));
            exposedStone[i] = checked((long)(
                ((Int128)rock * (d + relief)) / (2L * d)));
        }

        return new StrategicPrimitiveNaturalEndowmentFieldSet(
            new StrategicScalarField(graph, soil),
            new StrategicScalarField(graph, vegetation),
            new StrategicScalarField(graph, exposedStone),
            new StrategicScalarField(graph, lithology));
    }

    private static void CheckNormalized(long value, string name)
    {
        if (value < 0L || value > StrategicScalarField.Denominator)
            throw new InvalidOperationException(
                $"Generated field {name} is out of the normalized range.");
    }
}
