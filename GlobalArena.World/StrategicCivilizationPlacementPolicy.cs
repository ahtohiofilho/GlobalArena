namespace GlobalArena.World;

public sealed class StrategicCivilizationPlacementPolicy
{
    public static StrategicCivilizationPlacementPolicy Default { get; } =
        new(
            version: 1,
            habitabilityWeight:
                500_000L,
            resourcePotentialWeight:
                500_000L,
            localSuitabilityWeight:
                500_000L,
            dispersionWeight:
                500_000L,
            waterSuitabilityMultiplier:
                0L,
            excludeInitialReference:
                true);

    public int Version { get; }

    public long HabitabilityWeight { get; }

    public long ResourcePotentialWeight { get; }

    public long LocalSuitabilityWeight { get; }

    public long DispersionWeight { get; }

    public long WaterSuitabilityMultiplier { get; }

    public bool ExcludeInitialReference { get; }

    public StrategicCivilizationPlacementPolicy(
        int version,
        long habitabilityWeight,
        long resourcePotentialWeight,
        long localSuitabilityWeight,
        long dispersionWeight,
        long waterSuitabilityMultiplier,
        bool excludeInitialReference)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "Placement policy version must be positive.");
        }

        ValidateWeight(
            habitabilityWeight,
            nameof(habitabilityWeight));

        ValidateWeight(
            resourcePotentialWeight,
            nameof(resourcePotentialWeight));

        ValidateWeight(
            localSuitabilityWeight,
            nameof(localSuitabilityWeight));

        ValidateWeight(
            dispersionWeight,
            nameof(dispersionWeight));

        if (habitabilityWeight == 0L
            && resourcePotentialWeight == 0L)
        {
            throw new ArgumentException(
                "Placement suitability requires at least one positive local component weight.");
        }

        if (localSuitabilityWeight == 0L
            && dispersionWeight == 0L)
        {
            throw new ArgumentException(
                "Placement selection requires at least one positive score component weight.");
        }

        if (waterSuitabilityMultiplier < 0L
            || waterSuitabilityMultiplier
            > StrategicScalarField.Denominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(waterSuitabilityMultiplier),
                "Water suitability multiplier must remain inside the normalized [0, 1] range.");
        }

        Version =
            version;

        HabitabilityWeight =
            habitabilityWeight;

        ResourcePotentialWeight =
            resourcePotentialWeight;

        LocalSuitabilityWeight =
            localSuitabilityWeight;

        DispersionWeight =
            dispersionWeight;

        WaterSuitabilityMultiplier =
            waterSuitabilityMultiplier;

        ExcludeInitialReference =
            excludeInitialReference;
    }

    private static void ValidateWeight(
        long value,
        string parameterName)
    {
        if (value < 0L
            || value
            > StrategicScalarField.Denominator)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Policy weights must remain inside the normalized [0, 1] range.");
        }
    }
}
