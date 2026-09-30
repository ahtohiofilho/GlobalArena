namespace GlobalArena.World;

public sealed class StrategicBiomeMap
{
    private readonly StrategicBiomeKind[] _kinds;

    private readonly IReadOnlyList<StrategicBiomeKind> _readOnlyKinds;

    public StrategicSurfaceGraph SurfaceGraph { get; }

    public int Count =>
        _kinds.Length;

    public IReadOnlyList<StrategicBiomeKind> Kinds =>
        _readOnlyKinds;

    internal StrategicBiomeMap(
        StrategicSurfaceGraph surfaceGraph,
        IEnumerable<StrategicBiomeKind> kinds)
    {
        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        ArgumentNullException.ThrowIfNull(
            kinds);

        var materialized =
            kinds.ToArray();

        if (materialized.Length
            != surfaceGraph.NodeCount)
        {
            throw new ArgumentException(
                "Strategic biome map must contain exactly one biome per strategic node.",
                nameof(kinds));
        }

        if (materialized.Any(
            kind =>
                !Enum.IsDefined(
                    kind)))
        {
            throw new ArgumentException(
                "Strategic biome map contains an undefined biome kind.",
                nameof(kinds));
        }

        SurfaceGraph =
            surfaceGraph;

        _kinds =
            materialized;

        _readOnlyKinds =
            Array.AsReadOnly(
                _kinds);
    }

    public StrategicBiomeKind GetKind(
        int nodeIndex)
    {
        if (nodeIndex < 0
            || nodeIndex >= _kinds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeIndex),
                "Biome node index is outside the canonical strategic range.");
        }

        return _kinds[
            nodeIndex];
    }

    public StrategicBiomeKind GetKind(
        StrategicCellId cellId)
    {
        return GetKind(
            SurfaceGraph.GetNodeIndex(
                cellId));
    }
}
