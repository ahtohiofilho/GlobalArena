namespace GlobalArena.World;

public readonly record struct StrategicVertexId
{
    private readonly ulong _value;

    public ulong Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default StrategicVertexId is not a valid identity.");
            }

            return _value;
        }
    }

    public bool IsValid => _value != 0UL;

    public StrategicVertexId(ulong value)
    {
        if (value == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Strategic vertex ID must be greater than zero.");
        }

        _value = value;
    }
}
