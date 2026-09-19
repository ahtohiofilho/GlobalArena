namespace GlobalArena.World;

public readonly record struct StrategicCellId
{
    private readonly ulong _value;

    public ulong Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default StrategicCellId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0UL;

    public StrategicCellId(ulong value)
    {
        if (value == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Strategic cell ID must be greater than zero.");
        }

        _value = value;
    }
}
