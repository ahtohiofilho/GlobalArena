namespace GlobalArena.World;

public readonly record struct SharedBorderElementId
{
    private readonly StrategicEdgeId _strategicEdgeId;
    private readonly ulong _localOrdinal;

    public StrategicEdgeId StrategicEdgeId
    {
        get
        {
            EnsureValid();
            return _strategicEdgeId;
        }
    }

    public ulong LocalOrdinal
    {
        get
        {
            EnsureValid();
            return _localOrdinal;
        }
    }

    public bool IsValid =>
        _strategicEdgeId.IsValid
        && _localOrdinal != 0UL;

    public SharedBorderElementId(
        StrategicEdgeId strategicEdgeId,
        ulong localOrdinal)
    {
        if (!strategicEdgeId.IsValid)
        {
            throw new ArgumentException(
                "Strategic edge ID must be valid.",
                nameof(strategicEdgeId));
        }

        if (localOrdinal == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localOrdinal),
                "Shared border element local ordinal must be greater than zero.");
        }

        _strategicEdgeId =
            strategicEdgeId;

        _localOrdinal =
            localOrdinal;
    }

    private void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                "Default SharedBorderElementId is not a valid identity.");
        }
    }
}
