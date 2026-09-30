namespace GlobalArena.World;

public sealed class StrategicClimateFieldSet
{
    public StrategicScalarField Temperature { get; }

    public StrategicScalarField Moisture { get; }

    public StrategicScalarField WaterAvailability { get; }

    private StrategicClimateFieldSet(
        StrategicScalarField temperature,
        StrategicScalarField moisture,
        StrategicScalarField waterAvailability)
    {
        Temperature =
            temperature;

        Moisture =
            moisture;

        WaterAvailability =
            waterAvailability;
    }

    public static StrategicClimateFieldSet Generate(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph,
        StrategicPhysicalFieldSet physicalFields)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        ArgumentNullException.ThrowIfNull(
            physicalFields);

        if (surfaceGraph.StrategicTopology.Parameters
            != request.StrategicParameters)
        {
            throw new ArgumentException(
                "Strategic surface graph parameters must match the generation request.",
                nameof(surfaceGraph));
        }

        if (!ReferenceEquals(
            surfaceGraph,
            physicalFields.Elevation.SurfaceGraph))
        {
            throw new ArgumentException(
                "Strategic physical fields must share the supplied surface graph.",
                nameof(physicalFields));
        }

        var temperature =
            StrategicTemperatureFieldGenerator.Generate(
                request,
                surfaceGraph);

        var moisture =
            StrategicMoistureFieldGenerator.Generate(
                request,
                surfaceGraph);

        var waterAvailability =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physicalFields);

        return new StrategicClimateFieldSet(
            temperature,
            moisture,
            waterAvailability);
    }
}
