namespace GlobalArena.Runtime;

public readonly record struct MilitaryUnitId
{
    private readonly ulong _value;

    public ulong Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default MilitaryUnitId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0UL;

    public MilitaryUnitId(
        ulong value)
    {
        if (value == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Military unit ID must be greater than zero.");
        }

        _value = value;
    }
}
