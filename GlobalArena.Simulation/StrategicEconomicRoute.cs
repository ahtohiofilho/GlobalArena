using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class StrategicEconomicRoute
{
    private readonly StrategicCellId[] _cellPath;

    private readonly IReadOnlyList<StrategicCellId> _readOnlyCellPath;

    private readonly StrategicEdgeId[] _edgePath;

    private readonly IReadOnlyList<StrategicEdgeId> _readOnlyEdgePath;

    public StrategicEconomicRouteKey Key { get; }

    public IReadOnlyList<StrategicCellId> CellPath =>
        _readOnlyCellPath;

    public IReadOnlyList<StrategicEdgeId> EdgePath =>
        _readOnlyEdgePath;

    public IReadOnlyList<StrategicEdgeId> DependencyEdgeIds =>
        _readOnlyEdgePath;

    public int HopCount =>
        _edgePath.Length;

    public StrategicCellId SourceStrategicCellId =>
        _cellPath[0];

    public StrategicCellId DestinationStrategicCellId =>
        _cellPath[^1];

    public StrategicEconomicRoute(
        StrategicEconomicRouteKey key,
        IEnumerable<StrategicCellId> cellPath,
        IEnumerable<StrategicEdgeId> edgePath)
    {
        if (!key.SourceEconomicPointId.IsValid
            || !key.DestinationEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Strategic economic route key must contain valid economic point identities.",
                nameof(key));
        }

        ArgumentNullException.ThrowIfNull(
            cellPath);

        ArgumentNullException.ThrowIfNull(
            edgePath);

        var cells =
            cellPath.ToArray();

        var edges =
            edgePath.ToArray();

        if (cells.Length == 0)
        {
            throw new ArgumentException(
                "Strategic economic route must contain at least one strategic cell.",
                nameof(cellPath));
        }

        if (cells.Any(
            cellId =>
                !cellId.IsValid))
        {
            throw new ArgumentException(
                "Strategic economic route cannot contain invalid strategic cell identities.",
                nameof(cellPath));
        }

        if (edges.Any(
            edgeId =>
                !edgeId.IsValid))
        {
            throw new ArgumentException(
                "Strategic economic route cannot contain invalid strategic edge identities.",
                nameof(edgePath));
        }

        if (edges.Length
            != cells.Length - 1)
        {
            throw new ArgumentException(
                "Strategic economic route edge count must be exactly one less than cell count.",
                nameof(edgePath));
        }

        if (cells.Distinct().Count()
            != cells.Length)
        {
            throw new ArgumentException(
                "Strategic economic route cannot contain repeated strategic cells.",
                nameof(cellPath));
        }

        if (edges.Distinct().Count()
            != edges.Length)
        {
            throw new ArgumentException(
                "Strategic economic route cannot contain repeated strategic edges.",
                nameof(edgePath));
        }

        Key = key;

        _cellPath =
            cells;

        _readOnlyCellPath =
            Array.AsReadOnly(
                _cellPath);

        _edgePath =
            edges;

        _readOnlyEdgePath =
            Array.AsReadOnly(
                _edgePath);
    }
}
