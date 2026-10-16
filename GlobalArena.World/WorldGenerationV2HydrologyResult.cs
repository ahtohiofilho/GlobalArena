namespace GlobalArena.World;

/// <summary>
/// Opt-in V2 hydrology, derived from V2 physical scalars.
/// No physical regeneration, V1 result construction, or biome replacement.
/// </summary>
public sealed class WorldGenerationV2HydrologyResult
{
    public WorldGenerationV2SharedPhysicalAdapter Physical { get; }
    public StrategicScalarField WaterAvailability { get; }
    public StrategicHydrologyFieldSet Hydrology { get; }

    private WorldGenerationV2HydrologyResult(
        WorldGenerationV2SharedPhysicalAdapter physical,
        StrategicScalarField waterAvailability,
        StrategicHydrologyFieldSet hydrology)
    {
        Physical = physical;
        WaterAvailability = waterAvailability;
        Hydrology = hydrology;
    }

    public static WorldGenerationV2HydrologyResult Create(
        WorldGenerationV2PhysicalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var physical = WorldGenerationV2SharedPhysicalAdapter.Create(
            result.Request, result.PhysicalFields);
        var availability = StrategicWaterAvailabilityFieldGenerator.Generate(
            physical.Moisture, physical.Elevation, physical.LandWater);
        var hydrology = StrategicHydrologyFieldGenerator.Generate(
            physical.Elevation, physical.LandWater, availability);
        return new WorldGenerationV2HydrologyResult(
            physical, availability, hydrology);
    }
}