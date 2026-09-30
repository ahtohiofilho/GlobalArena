namespace GlobalArena.World;

public static class StrategicHydrologyFieldGenerator
{
    public static StrategicHydrologyFieldSet Generate(
        StrategicPhysicalFieldSet physicalFields,
        StrategicClimateFieldSet climateFields)
    {
        ArgumentNullException.ThrowIfNull(
            physicalFields);

        ArgumentNullException.ThrowIfNull(
            climateFields);

        if (!ReferenceEquals(
            physicalFields.Elevation.SurfaceGraph,
            climateFields.WaterAvailability.SurfaceGraph))
        {
            throw new ArgumentException(
                "Physical and climate fields must share one canonical strategic surface graph.",
                nameof(climateFields));
        }

        return Generate(
            physicalFields.Elevation,
            physicalFields.LandWater,
            climateFields.WaterAvailability);
    }

    public static StrategicHydrologyFieldSet Generate(
        StrategicScalarField elevation,
        StrategicLandWaterMap landWater,
        StrategicScalarField waterAvailability)
    {
        ArgumentNullException.ThrowIfNull(
            elevation);

        ArgumentNullException.ThrowIfNull(
            landWater);

        ArgumentNullException.ThrowIfNull(
            waterAvailability);

        if (!ReferenceEquals(
            elevation,
            landWater.Elevation))
        {
            throw new ArgumentException(
                "Land/water classification must be derived from the supplied elevation field.",
                nameof(landWater));
        }

        if (!ReferenceEquals(
            elevation.SurfaceGraph,
            waterAvailability.SurfaceGraph))
        {
            throw new ArgumentException(
                "Elevation and water availability must share one canonical strategic surface graph.",
                nameof(waterAvailability));
        }

        var graph =
            elevation.SurfaceGraph;

        var nodeCount =
            graph.NodeCount;

        var downstream =
            Enumerable.Repeat(
                -1,
                nodeCount)
                .ToArray();

        var kinds =
            new StrategicHydrologyNodeKind[
                nodeCount];

        var accumulation =
            new Int128[
                nodeCount];

        for (var index = 0;
             index < nodeCount;
             index++)
        {
            var availability =
                waterAvailability.GetRawValue(
                    index);

            if (availability < 0L
                || availability
                > StrategicScalarField.Denominator)
            {
                throw new ArgumentException(
                    "Water availability must remain inside the normalized fixed-point [0, 1] range.",
                    nameof(waterAvailability));
            }

            if (landWater.GetKind(
                index)
                == StrategicLandWaterKind.Water)
            {
                kinds[index] =
                    StrategicHydrologyNodeKind.WaterOutlet;

                accumulation[index] =
                    0;

                continue;
            }

            accumulation[index] =
                availability;

            var currentElevation =
                elevation.GetRawValue(
                    index);

            var bestNeighbor =
                -1;

            var bestElevation =
                long.MaxValue;

            foreach (var neighborIndex
                in graph.GetNeighborIndexes(
                    index))
            {
                var neighborElevation =
                    elevation.GetRawValue(
                        neighborIndex);

                if (neighborElevation
                    >= currentElevation)
                {
                    continue;
                }

                if (bestNeighbor < 0
                    || neighborElevation
                    < bestElevation
                    || (neighborElevation
                        == bestElevation
                        && neighborIndex
                        < bestNeighbor))
                {
                    bestNeighbor =
                        neighborIndex;

                    bestElevation =
                        neighborElevation;
                }
            }

            if (bestNeighbor < 0)
            {
                kinds[index] =
                    StrategicHydrologyNodeKind.InlandSink;

                continue;
            }

            downstream[index] =
                bestNeighbor;

            kinds[index] =
                StrategicHydrologyNodeKind.Downstream;
        }

        var propagationOrder =
            Enumerable.Range(
                0,
                nodeCount)
                .OrderByDescending(
                    index =>
                        elevation.GetRawValue(
                            index))
                .ThenBy(
                    index =>
                        index)
                .ToArray();

        foreach (var index
            in propagationOrder)
        {
            var target =
                downstream[
                    index];

            if (target < 0)
            {
                continue;
            }

            accumulation[target] =
                checked(
                    accumulation[target]
                    + accumulation[index]);
        }

        var rawAccumulation =
            new long[
                nodeCount];

        for (var index = 0;
             index < nodeCount;
             index++)
        {
            rawAccumulation[index] =
                checked(
                    (long)accumulation[index]);
        }

        var flowAccumulation =
            new StrategicScalarField(
                graph,
                rawAccumulation);

        return new StrategicHydrologyFieldSet(
            elevation,
            landWater,
            waterAvailability,
            downstream,
            kinds,
            flowAccumulation);
    }
}
