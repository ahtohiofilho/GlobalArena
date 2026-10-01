namespace GlobalArena.Runtime;

public readonly record struct CivilizationId
{
    private readonly ulong _value;

    public ulong Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default CivilizationId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0UL;

    public CivilizationId(
        ulong value)
    {
        if (value == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Civilization ID must be greater than zero.");
        }

        _value = value;
    }
}
