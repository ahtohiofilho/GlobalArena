namespace GlobalArena.World;

/// <summary>
/// Fine-grid corridor identity qualified by its generated Goldberg topology.
/// A bare StrategicEdgeId has only topology-local meaning; this wrapper stops
/// identical coarse/fine edge ordinals being mistaken for the same asset site.
/// Does not imply that a physical transport asset was constructed here.
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
