namespace GlobalArena.World;

public sealed class StrategicSurfaceGraph
{
    private readonly StrategicCellId[] _cellIds;

    private readonly IReadOnlyList<StrategicCellId> _readOnlyCellIds;

    private readonly int[][] _neighborIndexes;

    private readonly IReadOnlyList<int>[] _readOnlyNeighborIndexes;

    public StrategicTopology StrategicTopology { get; }

    public int NodeCount => _cellIds.Length;

    public IReadOnlyList<StrategicCellId> CellIds =>
        _readOnlyCellIds;

    public StrategicSurfaceGraph(
        StrategicTopology strategicTopology)
    {
        ArgumentNullException.ThrowIfNull(
            strategicTopology);

        StrategicTopology =
            strategicTopology;

        var nodeCount =
            strategicTopology.Cells.Count;

        _cellIds =
            new StrategicCellId[nodeCount];

        _neighborIndexes =
            new int[nodeCount][];

        _readOnlyNeighborIndexes =
            new IReadOnlyList<int>[nodeCount];

        for (var index = 0;
             index < nodeCount;
             index++)
        {
            var cell =
                strategicTopology.Cells[index];

            var expectedValue =
                checked((ulong)index + 1UL);

            if (cell.Id.Value
                != expectedValue)
            {
                throw new ArgumentException(
                    "Strategic topology must preserve contiguous canonical one-based cell IDs.",
                    nameof(strategicTopology));
            }

            _cellIds[index] =
                cell.Id;

            var neighbors =
                cell.AdjacentCellIds
                    .Select(
                        neighborId =>
                            checked(
                                (int)(
                                    neighborId.Value
                                    - 1UL)))
                    .Order()
                    .ToArray();

            if (neighbors.Any(
                neighborIndex =>
                    neighborIndex < 0
                    || neighborIndex >= nodeCount
                    || neighborIndex == index))
            {
                throw new ArgumentException(
                    "Strategic topology contains an invalid adjacent cell reference.",
                    nameof(strategicTopology));
            }

            if (neighbors.Distinct().Count()
                != neighbors.Length)
            {
                throw new ArgumentException(
                    "Strategic topology contains duplicate adjacent cell references.",
                    nameof(strategicTopology));
            }

            _neighborIndexes[index] =
                neighbors;

            _readOnlyNeighborIndexes[index] =
                Array.AsReadOnly(
                    neighbors);
        }

        _readOnlyCellIds =
            Array.AsReadOnly(
                _cellIds);
    }

    public int GetNodeIndex(
        StrategicCellId cellId)
    {
        if (!cellId.IsValid)
        {
            throw new ArgumentException(
                "Strategic cell ID must be valid.",
                nameof(cellId));
        }

        var ordinal =
            cellId.Value;

        if (ordinal
            > (ulong)_cellIds.Length)
        {
            throw new KeyNotFoundException(
                $"Strategic cell ID {ordinal} does not belong to this surface graph.");
        }

        return checked(
            (int)(ordinal - 1UL));
    }

    public StrategicCellId GetCellId(
        int nodeIndex)
    {
        ValidateNodeIndex(
            nodeIndex);

        return _cellIds[nodeIndex];
    }

    public IReadOnlyList<int> GetNeighborIndexes(
        int nodeIndex)
    {
        ValidateNodeIndex(
            nodeIndex);

        return _readOnlyNeighborIndexes[
            nodeIndex];
    }

    public IReadOnlyList<int> GetNeighborIndexes(
        StrategicCellId cellId)
    {
        return GetNeighborIndexes(
            GetNodeIndex(
                cellId));
    }

    private void ValidateNodeIndex(
        int nodeIndex)
    {
        if (nodeIndex < 0
            || nodeIndex >= _cellIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeIndex),
                "Surface graph node index is outside the canonical node range.");
        }
    }
}
