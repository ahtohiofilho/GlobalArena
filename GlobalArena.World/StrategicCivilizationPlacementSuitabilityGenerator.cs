namespace GlobalArena.World;

public static class StrategicCivilizationPlacementSuitabilityGenerator
{
    public const long MinimumRawSuitability =
        0L;

    public const long MaximumRawSuitability =
        StrategicScalarField.Denominator;

    public static StrategicScalarField Generate(
        StrategicPhysicalFieldSet physicalFields,
        StrategicScalarField habitability,
        StrategicResourcePotentialFieldSet resourcePotentialFields)
    {
        return Generate(
            physicalFields,
            habitability,
            resourcePotentialFields,
            StrategicCivilizationPlacementPolicy.Default);
    }

    public static StrategicScalarField Generate(
        StrategicPhysicalFieldSet physicalFields,
        StrategicScalarField habitability,
        StrategicResourcePotentialFieldSet resourcePotentialFields,
        StrategicCivilizationPlacementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(
            physicalFields);

        ArgumentNullException.ThrowIfNull(
            habitability);

        ArgumentNullException.ThrowIfNull(
            resourcePotentialFields);

        ArgumentNullException.ThrowIfNull(
            policy);

        var graph =
            physicalFields.Elevation.SurfaceGraph;

        if (!ReferenceEquals(
            graph,
            habitability.SurfaceGraph)
            || !ReferenceEquals(
                graph,
                resourcePotentialFields.GeneralPotential.SurfaceGraph))
        {
            throw new ArgumentException(
                "Placement-suitability inputs must share one canonical strategic surface graph.");
        }

        var values =
            new long[
                graph.NodeCount];

        var weightSum =
            checked(
                policy.HabitabilityWeight
                + policy.ResourcePotentialWeight);

        for (var index = 0;
             index < values.Length;
             index++)
        {
            var habitabilityRaw =
                habitability.GetRawValue(
                    index);

            var resourceRaw =
                resourcePotentialFields.GeneralPotential.GetRawValue(
                    index);

            ValidateNormalized(
                habitabilityRaw,
                nameof(habitability));

            ValidateNormalized(
                resourceRaw,
                nameof(resourcePotentialFields));

            var weighted =
                ((Int128)habitabilityRaw
                    * policy.HabitabilityWeight)
                + ((Int128)resourceRaw
                    * policy.ResourcePotentialWeight);

            var suitability =
                checked(
                    (long)(
                        weighted
                        / weightSum));

            if (physicalFields.LandWater.GetKind(
                index)
                == StrategicLandWaterKind.Water)
            {
                suitability =
                    checked(
                        (long)(
                            ((Int128)suitability
                                * policy.WaterSuitabilityMultiplier)
                            / StrategicScalarField.Denominator));
            }

            values[index] =
                suitability;
        }

        return new StrategicScalarField(
            graph,
            values);
    }

    private static void ValidateNormalized(
        long value,
        string parameterName)
    {
        if (value < MinimumRawSuitability
            || value > MaximumRawSuitability)
        {
            throw new ArgumentException(
                "Placement-suitability inputs must remain inside the normalized fixed-point range.",
                parameterName);
        }
    }
}
