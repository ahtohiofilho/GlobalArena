namespace GlobalArena.World;

/// <summary>
/// Qualified identity of a FINE GEOMETRIC SHARED BORDER between two Goldberg
/// cells. The border connects two geometric vertices and separates two cells.
/// This identity can key a DUAL-GRAPH CROSSING from one incident cell to the
/// other; it must NEVER be interpreted as a longitudinal transport route
/// running ALONG the border, or as the location of a cargo terminal.
/// A bare StrategicEdgeId has only topology-local meaning; qualification
/// prevents coarse/fine ordinal aliasing. No transport asset is implied.
/// </summary>
public readonly record struct PhysicalTacticalEdgeId
{
    private readonly GoldbergParameters _fineGoldbergParameters;
    private readonly StrategicEdgeId _fineStrategicEdgeId;

    public bool IsValid =>
        _fineGoldbergParameters.IsValid &&
        _fineStrategicEdgeId.IsValid &&
        _fineStrategicEdgeId.Value <= _fineGoldbergParameters.StrategicEdgeCount;

    public GoldbergParameters FineGoldbergParameters
    {
        get
        {
            EnsureValid();
            return _fineGoldbergParameters;
        }
    }

    public StrategicEdgeId FineStrategicEdgeId
    {
        get
        {
            EnsureValid();
            return _fineStrategicEdgeId;
        }
    }

    public PhysicalTacticalEdgeId(
        GoldbergParameters fineGoldbergParameters,
        StrategicEdgeId fineStrategicEdgeId)
    {
        if (!fineGoldbergParameters.IsValid)
            throw new ArgumentException(
                "Fine corridor requires valid Goldberg parameters.",
                nameof(fineGoldbergParameters));
        if (!fineStrategicEdgeId.IsValid)
            throw new ArgumentException(
                "Fine corridor requires a valid edge identity.",
                nameof(fineStrategicEdgeId));
        if (fineStrategicEdgeId.Value > fineGoldbergParameters.StrategicEdgeCount)
            throw new ArgumentOutOfRangeException(nameof(fineStrategicEdgeId),
                "Fine edge must exist in its qualified topology.");
        _fineGoldbergParameters = fineGoldbergParameters;
        _fineStrategicEdgeId = fineStrategicEdgeId;
    }

    private void EnsureValid()
    {
        if (!IsValid)
            throw new InvalidOperationException(
                "Default fine corridor identity is invalid.");
    }
}
