namespace GlobalArena.World;

public readonly record struct StrategicTacticalAggregate
{
    public StrategicCellId StrategicCellId { get; }

    public long RawValue { get; }

    public StrategicTacticalAggregate(
        StrategicCellId strategicCellId,
        long rawValue)
    {
        if (!strategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Strategic cell ID must be valid.",
                nameof(strategicCellId));
        }

        StrategicCellId =
            strategicCellId;

        RawValue =
            rawValue;
    }
}
