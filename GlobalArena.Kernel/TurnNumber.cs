namespace GlobalArena.Kernel;

public readonly record struct TurnNumber
{
    public ulong Value { get; }

    public TurnNumber(ulong value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Turn number must be greater than zero.");
        }

        Value = value;
    }

    public TurnNumber Next()
    {
        if (Value == ulong.MaxValue)
        {
            throw new InvalidOperationException(
                "Turn number cannot advance beyond UInt64.MaxValue.");
        }

        return new TurnNumber(Value + 1);
    }
}
