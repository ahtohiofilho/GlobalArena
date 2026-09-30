namespace GlobalArena.World;

public sealed class StrategicLandWaterMap
{
    private readonly StrategicLandWaterKind[] _kinds;

    private readonly IReadOnlyList<StrategicLandWaterKind> _readOnlyKinds;

    public StrategicScalarField Elevation { get; }

    public StrategicSeaLevel SeaLevel { get; }

    public int Count => _kinds.Length;

    public IReadOnlyList<StrategicLandWaterKind> Kinds =>
        _readOnlyKinds;

    public int LandCount { get; }

    public int WaterCount { get; }

    public StrategicLandWaterMap(
        StrategicScalarField elevation,
        StrategicSeaLevel seaLevel)
    {
        ArgumentNullException.ThrowIfNull(
            elevation);

        Elevation =
            elevation;

        SeaLevel =
            seaLevel;

        _kinds =
            new StrategicLandWaterKind[
                elevation.Count];

        var landCount =
            0;

        var waterCount =
            0;

        for (var index = 0;
             index < _kinds.Length;
             index++)
        {
            var kind =
                elevation.GetRawValue(
                    index)
                > seaLevel.RawValue
                    ? StrategicLandWaterKind.Land
                    : StrategicLandWaterKind.Water;

            _kinds[index] =
                kind;

            if (kind
                == StrategicLandWaterKind.Land)
            {
                landCount++;
            }
            else
            {
                waterCount++;
            }
        }

        LandCount =
            landCount;

        WaterCount =
            waterCount;

        _readOnlyKinds =
            Array.AsReadOnly(
                _kinds);
    }

    public StrategicLandWaterKind GetKind(
        int nodeIndex)
    {
        if (nodeIndex < 0
            || nodeIndex >= _kinds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeIndex),
                "Land/water node index is outside the canonical node range.");
        }

        return _kinds[
            nodeIndex];
    }

    public StrategicLandWaterKind GetKind(
        StrategicCellId cellId)
    {
        return GetKind(
            Elevation.SurfaceGraph.GetNodeIndex(
                cellId));
    }
}
