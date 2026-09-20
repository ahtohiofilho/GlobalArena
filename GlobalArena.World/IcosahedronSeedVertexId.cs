namespace GlobalArena.World;

public readonly record struct IcosahedronSeedVertexId
{
    private readonly int _value;

    public int Value
    {
        get
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Default IcosahedronSeedVertexId is not a valid provenance identity.");
            }

            return _value;
        }
    }

    public bool IsValid =>
        _value is >= 1 and <= 12;

    public IcosahedronSeedVertexId(
        int value)
    {
        if (value is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Icosahedron seed vertex ID must be between 1 and 12.");
        }

        _value = value;
    }
}
