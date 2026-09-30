namespace GlobalArena.World;

public static class StrategicHabitabilityFieldGenerator
{
    public const long MinimumRawHabitability =
        0L;

    public const long MaximumRawHabitability =
        StrategicScalarField.Denominator;

    public static StrategicScalarField Generate(
        StrategicPhysicalFieldSet physicalFields,
        StrategicClimateFieldSet climateFields)
    {
        return Generate(
            physicalFields,
            climateFields,
            StrategicHabitabilityPolicy.Default);
    }

    public static StrategicScalarField Generate(
        StrategicPhysicalFieldSet physicalFields,
        StrategicClimateFieldSet climateFields,
        StrategicHabitabilityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(
            physicalFields);

        ArgumentNullException.ThrowIfNull(
            climateFields);

        ArgumentNullException.ThrowIfNull(
            policy);

        var graph =
            physicalFields.Elevation.SurfaceGraph;

        if (!ReferenceEquals(
            graph,
            climateFields.Temperature.SurfaceGraph)
            || !ReferenceEquals(
                graph,
                climateFields.WaterAvailability.SurfaceGraph))
        {
            throw new ArgumentException(
                "Habitability inputs must share one canonical strategic surface graph.");
        }

        var values =
            new long[
                graph.NodeCount];

        var weightSum =
            checked(
                policy.WaterAvailabilityWeight
                + policy.TemperatureComfortWeight);

        for (var index = 0;
             index < values.Length;
             index++)
        {
            var availability =
                climateFields.WaterAvailability.GetRawValue(
                    index);

            if (availability
                < StrategicWaterAvailabilityFieldGenerator.MinimumRawAvailability
                || availability
                > StrategicWaterAvailabilityFieldGenerator.MaximumRawAvailability)
            {
                throw new ArgumentException(
                    "Water availability must remain inside the normalized fixed-point range.",
                    nameof(climateFields));
            }

            var temperature =
                climateFields.Temperature.GetRawValue(
                    index);

            if (temperature
                < StrategicTemperatureFieldGenerator.MinimumRawTemperature
                || temperature
                > StrategicTemperatureFieldGenerator.MaximumRawTemperature)
            {
                throw new ArgumentException(
                    "Temperature must remain inside the accepted normalized fixed-point range.",
                    nameof(climateFields));
            }

            var temperatureComfort =
                checked(
                    StrategicScalarField.Denominator
                    - Math.Abs(
                        temperature));

            var weighted =
                ((Int128)availability
                    * policy.WaterAvailabilityWeight)
                + ((Int128)temperatureComfort
                    * policy.TemperatureComfortWeight);

            var habitability =
                checked(
                    (long)(
                        weighted
                        / weightSum));

            if (physicalFields.LandWater.GetKind(
                index)
                == StrategicLandWaterKind.Water)
            {
                habitability =
                    checked(
                        (long)(
                            ((Int128)habitability
                                * policy.WaterCellMultiplier)
                            / StrategicScalarField.Denominator));
            }

            values[index] =
                habitability;
        }

        return new StrategicScalarField(
            graph,
            values);
    }
}
