namespace GlobalArena.World;

public sealed class StrategicHabitabilityPolicy
{
    public static StrategicHabitabilityPolicy Default { get; } =
        new(
            version: 1,
            waterAvailabilityWeight:
                500_000L,
            temperatureComfortWeight:
                500_000L,
            waterCellMultiplier:
                0L);

    public int Version { get; }

    public long WaterAvailabilityWeight { get; }

    public long TemperatureComfortWeight { get; }

    public long WaterCellMultiplier { get; }

    public StrategicHabitabilityPolicy(
        int version,
        long waterAvailabilityWeight,
        long temperatureComfortWeight,
        long waterCellMultiplier)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "Habitability policy version must be positive.");
        }

        ValidateWeight(
            waterAvailabilityWeight,
            nameof(waterAvailabilityWeight));

        ValidateWeight(
            temperatureComfortWeight,
            nameof(temperatureComfortWeight));

        if (waterAvailabilityWeight == 0L
            && temperatureComfortWeight == 0L)
        {
            throw new ArgumentException(
                "Habitability policy requires at least one positive component weight.");
        }

        if (waterCellMultiplier < 0L
            || waterCellMultiplier
            > StrategicScalarField.Denominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(waterCellMultiplier),
                "Water-cell multiplier must remain inside the normalized [0, 1] range.");
        }

        Version =
            version;

        WaterAvailabilityWeight =
            waterAvailabilityWeight;

        TemperatureComfortWeight =
            temperatureComfortWeight;

        WaterCellMultiplier =
            waterCellMultiplier;
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
