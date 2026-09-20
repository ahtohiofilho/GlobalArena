namespace GlobalArena.World;

public readonly record struct TacticalCellId
{
    private readonly StrategicCellId _parentStrategicCellId;
    private readonly ulong _localOrdinal;

    public StrategicCellId ParentStrategicCellId
    {
        get
        {
            EnsureValid();
            return _parentStrategicCellId;
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
        _parentStrategicCellId.IsValid
        && _localOrdinal != 0UL;

    public TacticalCellId(
        StrategicCellId parentStrategicCellId,
        ulong localOrdinal)
    {
        if (!parentStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Parent strategic cell ID must be valid.",
                nameof(parentStrategicCellId));
        }

        if (localOrdinal == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localOrdinal),
                "Tactical cell local ordinal must be greater than zero.");
        }

        _parentStrategicCellId =
            parentStrategicCellId;

        _localOrdinal =
            localOrdinal;
    }

    private void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                "Default TacticalCellId is not a valid identity.");
        }
    }
}
