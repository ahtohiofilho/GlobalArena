namespace GlobalArena.World;

public readonly record struct StrategicEdgeId
{
    private readonly ulong _value;

    public ulong Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default StrategicEdgeId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0UL;

    public StrategicEdgeId(ulong value)
    {
        if (value == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Strategic edge ID must be greater than zero.");
        }

        _value = value;
    }
}
