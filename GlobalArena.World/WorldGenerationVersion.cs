namespace GlobalArena.World;

public readonly record struct WorldGenerationVersion
{
    private readonly int _value;

    public int Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default WorldGenerationVersion is not a valid generator version.");
            }

            return _value;
        }
    }

    public bool IsValid => _value > 0;

    public static WorldGenerationVersion Initial =>
        new(1);

    public WorldGenerationVersion(
        int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "World generation version must be greater than zero.");
        }

        _value = value;
    }
}
