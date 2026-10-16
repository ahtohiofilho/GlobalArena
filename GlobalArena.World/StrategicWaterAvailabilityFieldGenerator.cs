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
    /// <summary>
    /// Builds local water availability from precomputed graph-aligned fields.
    /// Water cells use maximum availability; land cells preserve moisture.
    /// No physical generator is run by this overload.
    /// </summary>
    public static StrategicScalarField Generate(
        StrategicScalarField moisture,
        StrategicScalarField elevation,
        StrategicLandWaterMap landWater)
    {
        ArgumentNullException.ThrowIfNull(moisture);
        ArgumentNullException.ThrowIfNull(elevation);
        ArgumentNullException.ThrowIfNull(landWater);
        if (!ReferenceEquals(moisture.SurfaceGraph, elevation.SurfaceGraph))
            throw new ArgumentException("Moisture and elevation must share one graph.", nameof(elevation));
        if (!ReferenceEquals(landWater.Elevation, elevation))
            throw new ArgumentException("Land/water must reference the exact elevation field.", nameof(landWater));
        var values = new long[elevation.SurfaceGraph.NodeCount];
        for (var index = 0; index < values.Length; index++)
        {
            var moistureRaw = moisture.GetRawValue(index);
            if (moistureRaw < MinimumRawAvailability || moistureRaw > MaximumRawAvailability)
                throw new ArgumentException("Moisture must be normalized to [0, 1].", nameof(moisture));
            values[index] = landWater.GetKind(index) == StrategicLandWaterKind.Water
                ? MaximumRawAvailability : moistureRaw;
        }
        return new StrategicScalarField(elevation.SurfaceGraph, values);
    }
}
