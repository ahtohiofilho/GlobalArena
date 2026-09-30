namespace GlobalArena.World;

public static class StrategicWaterAvailabilityFieldGenerator
{
    public const long MinimumRawAvailability =
        0L;

    public const long MaximumRawAvailability =
        StrategicScalarField.Denominator;

    public static StrategicScalarField Generate(
        StrategicScalarField moisture,
        StrategicPhysicalFieldSet physicalFields)
    {
        ArgumentNullException.ThrowIfNull(
            moisture);

        ArgumentNullException.ThrowIfNull(
            physicalFields);

        var graph =
            moisture.SurfaceGraph;

        if (!ReferenceEquals(
            graph,
            physicalFields.Elevation.SurfaceGraph))
        {
            throw new ArgumentException(
                "Moisture and physical fields must share the same strategic surface graph.",
                nameof(physicalFields));
        }

        var values =
            new long[
                graph.NodeCount];

        for (var index = 0;
             index < values.Length;
             index++)
        {
            var moistureRaw =
                moisture.GetRawValue(
                    index);

            if (moistureRaw
                < MinimumRawAvailability
                || moistureRaw
                > MaximumRawAvailability)
            {
                throw new ArgumentException(
                    "Moisture values must be normalized to the fixed-point [0, 1] range.",
                    nameof(moisture));
            }

            values[index] =
                physicalFields.LandWater.GetKind(
                    index)
                == StrategicLandWaterKind.Water
                    ? MaximumRawAvailability
                    : moistureRaw;
        }

        return new StrategicScalarField(
            graph,
            values);
    }
}
