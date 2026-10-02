namespace GlobalArena.Runtime;

public readonly record struct EconomicPointId
{
    private readonly ulong _value;

    public ulong Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default EconomicPointId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0UL;

    public EconomicPointId(
        ulong value)
    {
        if (value == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Economic point ID must be greater than zero.");
        }

        _value = value;
    }
}