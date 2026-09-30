namespace GlobalArena.World;

public readonly record struct StrategicSeaLevel
{
    public static StrategicSeaLevel Default { get; } =
        new(0L);

    public long RawValue { get; }

    public StrategicSeaLevel(
        long rawValue)
    {
        RawValue =
            rawValue;
    }
}
