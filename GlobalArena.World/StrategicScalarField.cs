namespace GlobalArena.World;

public sealed class StrategicScalarField
{
    public const long Denominator =
        1_000_000L;

    private readonly long[] _rawValues;

    private readonly IReadOnlyList<long> _readOnlyRawValues;

    public StrategicSurfaceGraph SurfaceGraph { get; }

    public int Count => _rawValues.Length;

    public IReadOnlyList<long> RawValues =>
        _readOnlyRawValues;

    public StrategicScalarField(
        StrategicSurfaceGraph surfaceGraph,
        IEnumerable<long> rawValues)
    {
        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        ArgumentNullException.ThrowIfNull(
            rawValues);

        var values =
            rawValues.ToArray();

        if (values.Length
            != surfaceGraph.NodeCount)
        {
            throw new ArgumentException(
                "Strategic scalar field must contain exactly one raw value per strategic surface node.",
                nameof(rawValues));
        }

        SurfaceGraph =
            surfaceGraph;

        _rawValues =
            values;

        _readOnlyRawValues =
            Array.AsReadOnly(
                _rawValues);
    }

    public long GetRawValue(
        int nodeIndex)
    {
        if (nodeIndex < 0
            || nodeIndex >= _rawValues.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeIndex),
                "Scalar field node index is outside the canonical node range.");
        }

        return _rawValues[
            nodeIndex];
    }

    public long GetRawValue(
        StrategicCellId cellId)
    {
        return GetRawValue(
            SurfaceGraph.GetNodeIndex(
                cellId));
    }
}
