namespace GlobalArena.World;

public sealed class StrategicPhysicalFieldSet
{
    public StrategicScalarField Elevation { get; }

    public StrategicScalarField Relief { get; }

    public StrategicSeaLevel SeaLevel { get; }

    public StrategicLandWaterMap LandWater { get; }

    private StrategicPhysicalFieldSet(
        StrategicScalarField elevation,
        StrategicScalarField relief,
        StrategicSeaLevel seaLevel,
        StrategicLandWaterMap landWater)
    {
        Elevation =
            elevation;

        Relief =
            relief;

        SeaLevel =
            seaLevel;

        LandWater =
            landWater;
    }

    public static StrategicPhysicalFieldSet Generate(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph)
    {
        return Generate(
            request,
            surfaceGraph,
            StrategicSeaLevel.Default);
    }

    public static StrategicPhysicalFieldSet Generate(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph,
        StrategicSeaLevel seaLevel)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        if (surfaceGraph.StrategicTopology.Parameters
            != request.StrategicParameters)
        {
            throw new ArgumentException(
                "Strategic surface graph parameters must match the generation request.",
                nameof(surfaceGraph));
        }

        var elevation =
            StrategicElevationFieldGenerator.Generate(
                request,
                surfaceGraph);

        var relief =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        var landWater =
            new StrategicLandWaterMap(
                elevation,
                seaLevel);

        return new StrategicPhysicalFieldSet(
            elevation,
            relief,
            seaLevel,
            landWater);
    }
}
