namespace GlobalArena.World;

public static class StrategicBiomeClassifier
{
    public const long PolarIceMaximumTemperature =
        -600_000L;

    public const long TundraMaximumTemperature =
        -250_000L;

    public const long HighlandMinimumElevation =
        600_000L;

    public const long WetlandMinimumWaterAvailability =
        650_000L;

    public const long WetlandMinimumFlowAccumulation =
        1_500_000L;

    public const long DesertMaximumWaterAvailability =
        200_000L;

    public const long RainforestMinimumTemperature =
        400_000L;

    public const long RainforestMinimumMoisture =
        700_000L;

    public const long ForestMinimumMoisture =
        450_000L;

    public static StrategicBiomeMap Generate(
        StrategicPhysicalFieldSet physicalFields,
        StrategicClimateFieldSet climateFields,
        StrategicHydrologyFieldSet hydrologyFields)
    {
        ArgumentNullException.ThrowIfNull(
            physicalFields);

        ArgumentNullException.ThrowIfNull(
            climateFields);

        ArgumentNullException.ThrowIfNull(
            hydrologyFields);

        if (!ReferenceEquals(
            physicalFields.LandWater,
            hydrologyFields.LandWater)
            || !ReferenceEquals(
                climateFields.WaterAvailability,
                hydrologyFields.WaterAvailability))
        {
            throw new ArgumentException(
                "Biome inputs must share the accepted physical, climate, and hydrology field instances.");
        }

        return Generate(
            physicalFields.LandWater,
            climateFields.Temperature,
            climateFields.Moisture,
            climateFields.WaterAvailability,
            hydrologyFields);
    }

    public static StrategicBiomeMap Generate(
        StrategicLandWaterMap landWater,
        StrategicScalarField temperature,
        StrategicScalarField moisture,
        StrategicScalarField waterAvailability,
        StrategicHydrologyFieldSet hydrologyFields)
    {
        ArgumentNullException.ThrowIfNull(
            landWater);

        ArgumentNullException.ThrowIfNull(
            temperature);

        ArgumentNullException.ThrowIfNull(
            moisture);

        ArgumentNullException.ThrowIfNull(
            waterAvailability);

        ArgumentNullException.ThrowIfNull(
            hydrologyFields);

        var graph =
            landWater.Elevation.SurfaceGraph;

        if (!ReferenceEquals(
            graph,
            temperature.SurfaceGraph)
            || !ReferenceEquals(
                graph,
                moisture.SurfaceGraph)
            || !ReferenceEquals(
                graph,
                waterAvailability.SurfaceGraph)
            || !ReferenceEquals(
                graph,
                hydrologyFields.SurfaceGraph)
            || !ReferenceEquals(
                landWater,
                hydrologyFields.LandWater)
            || !ReferenceEquals(
                waterAvailability,
                hydrologyFields.WaterAvailability))
        {
            throw new ArgumentException(
                "All biome inputs must share one canonical strategic field graph and accepted source instances.");
        }

        var kinds =
            new StrategicBiomeKind[
                graph.NodeCount];

        for (var index = 0;
             index < kinds.Length;
             index++)
        {
            var temperatureRaw =
                temperature.GetRawValue(
                    index);

            var moistureRaw =
                moisture.GetRawValue(
                    index);

            var availabilityRaw =
                waterAvailability.GetRawValue(
                    index);

            if (temperatureRaw
                < StrategicTemperatureFieldGenerator.MinimumRawTemperature
                || temperatureRaw
                > StrategicTemperatureFieldGenerator.MaximumRawTemperature)
            {
                throw new ArgumentException(
                    "Temperature must remain inside the accepted normalized fixed-point range.",
                    nameof(temperature));
            }

            if (moistureRaw
                < StrategicMoistureFieldGenerator.MinimumRawMoisture
                || moistureRaw
                > StrategicMoistureFieldGenerator.MaximumRawMoisture)
            {
                throw new ArgumentException(
                    "Moisture must remain inside the accepted normalized fixed-point range.",
                    nameof(moisture));
            }

            if (availabilityRaw
                < StrategicWaterAvailabilityFieldGenerator.MinimumRawAvailability
                || availabilityRaw
                > StrategicWaterAvailabilityFieldGenerator.MaximumRawAvailability)
            {
                throw new ArgumentException(
                    "Water availability must remain inside the accepted normalized fixed-point range.",
                    nameof(waterAvailability));
            }

            kinds[index] =
                Classify(
                    landWater,
                    temperature,
                    moisture,
                    waterAvailability,
                    hydrologyFields,
                    index);
        }

        return new StrategicBiomeMap(
            graph,
            kinds);
    }

    private static StrategicBiomeKind Classify(
        StrategicLandWaterMap landWater,
        StrategicScalarField temperature,
        StrategicScalarField moisture,
        StrategicScalarField waterAvailability,
        StrategicHydrologyFieldSet hydrologyFields,
        int index)
    {
        if (landWater.GetKind(
            index)
            == StrategicLandWaterKind.Water)
        {
            return StrategicBiomeKind.Water;
        }

        var temperatureRaw =
            temperature.GetRawValue(
                index);

        if (temperatureRaw
            <= PolarIceMaximumTemperature)
        {
            return StrategicBiomeKind.PolarIce;
        }

        if (temperatureRaw
            <= TundraMaximumTemperature)
        {
            return StrategicBiomeKind.Tundra;
        }

        if (landWater.Elevation.GetRawValue(
            index)
            >= HighlandMinimumElevation)
        {
            return StrategicBiomeKind.Highland;
        }

        var availabilityRaw =
            waterAvailability.GetRawValue(
                index);

        if (hydrologyFields.GetNodeKind(
            index)
            == StrategicHydrologyNodeKind.InlandSink
            && availabilityRaw
            >= WetlandMinimumWaterAvailability
            && hydrologyFields.FlowAccumulation.GetRawValue(
                index)
            >= WetlandMinimumFlowAccumulation)
        {
            return StrategicBiomeKind.Wetland;
        }

        if (availabilityRaw
            <= DesertMaximumWaterAvailability)
        {
            return StrategicBiomeKind.Desert;
        }

        var moistureRaw =
            moisture.GetRawValue(
                index);

        if (temperatureRaw
            >= RainforestMinimumTemperature
            && moistureRaw
            >= RainforestMinimumMoisture)
        {
            return StrategicBiomeKind.Rainforest;
        }

        if (moistureRaw
            >= ForestMinimumMoisture)
        {
            return StrategicBiomeKind.Forest;
        }

        return StrategicBiomeKind.Grassland;
    }
}
