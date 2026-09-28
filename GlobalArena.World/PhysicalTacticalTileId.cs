namespace GlobalArena.World;

public readonly record struct PhysicalTacticalTileId
{
    private readonly GoldbergParameters _fineGoldbergParameters;
    private readonly StrategicCellId _fineStrategicCellId;

    public GoldbergParameters FineGoldbergParameters
    {
        get
        {
            EnsureValid();
            return _fineGoldbergParameters;
        }
    }

    public StrategicCellId FineStrategicCellId
    {
        get
        {
            EnsureValid();
            return _fineStrategicCellId;
        }
    }

    public bool IsValid =>
        _fineGoldbergParameters.IsValid
        && _fineStrategicCellId.IsValid
        && _fineStrategicCellId.Value <= _fineGoldbergParameters.StrategicCellCount;

    public PhysicalTacticalTileId(
        GoldbergParameters fineGoldbergParameters,
        StrategicCellId fineStrategicCellId)
    {
        if (!fineGoldbergParameters.IsValid)
        {
            throw new ArgumentException(
                "Fine Goldberg parameters must be valid.",
                nameof(fineGoldbergParameters));
        }

        if (!fineStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Fine strategic cell ID must be valid.",
                nameof(fineStrategicCellId));
        }

        if (fineStrategicCellId.Value > fineGoldbergParameters.StrategicCellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fineStrategicCellId),
                "Fine strategic cell ID must exist within the selected fine Goldberg topology.");
        }

        _fineGoldbergParameters =
            fineGoldbergParameters;

        _fineStrategicCellId =
            fineStrategicCellId;
    }

    private void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                "Default PhysicalTacticalTileId is not a valid identity.");
        }
    }
}