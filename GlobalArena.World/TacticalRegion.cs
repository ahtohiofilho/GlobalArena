namespace GlobalArena.World;

public sealed class TacticalRegion
{
    public StrategicCellId StrategicCellId { get; }

    public IReadOnlyList<TacticalCell> Cells { get; }

    public TacticalRegion(
        StrategicCellId strategicCellId,
        IEnumerable<TacticalCell> cells)
    {
        if (!strategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Strategic cell ID must be valid.",
                nameof(strategicCellId));
        }

        ArgumentNullException.ThrowIfNull(
            cells);

        var orderedCells =
            cells
                .OrderBy(
                    cell =>
                        cell.Id.LocalOrdinal)
                .ToArray();

        if (orderedCells.Length == 0)
        {
            throw new ArgumentException(
                "A tactical region must contain at least one tactical cell.",
                nameof(cells));
        }

        if (orderedCells.Any(
            cell =>
                cell.Id.ParentStrategicCellId
                != strategicCellId))
        {
            throw new ArgumentException(
                "Every tactical cell must belong to the tactical region's strategic parent.",
                nameof(cells));
        }

        if (orderedCells
            .Select(cell => cell.Id)
            .Distinct()
            .Count()
            != orderedCells.Length)
        {
            throw new ArgumentException(
                "Tactical cell IDs cannot contain duplicates.",
                nameof(cells));
        }

        var cellsById =
            orderedCells.ToDictionary(
                cell => cell.Id);

        foreach (var cell in orderedCells)
        {
            foreach (var adjacentId in cell.AdjacentCellIds)
            {
                if (!cellsById.TryGetValue(
                    adjacentId,
                    out var adjacentCell))
                {
                    throw new ArgumentException(
                        "Every tactical adjacency must resolve to a cell in the same region.",
                        nameof(cells));
                }

                if (!adjacentCell.AdjacentCellIds.Contains(
                    cell.Id))
                {
                    throw new ArgumentException(
                        "Tactical adjacency must be reciprocal.",
                        nameof(cells));
                }
            }
        }

        var visited =
            new HashSet<TacticalCellId>();

        var queue =
            new Queue<TacticalCellId>();

        queue.Enqueue(
            orderedCells[0].Id);

        while (queue.Count > 0)
        {
            var currentId =
                queue.Dequeue();

            if (!visited.Add(
                currentId))
            {
                continue;
            }

            foreach (var adjacentId
                in cellsById[currentId].AdjacentCellIds)
            {
                if (!visited.Contains(
                    adjacentId))
                {
                    queue.Enqueue(
                        adjacentId);
                }
            }
        }

        if (visited.Count
            != orderedCells.Length)
        {
            throw new ArgumentException(
                "Tactical region graph must be connected.",
                nameof(cells));
        }

        StrategicCellId =
            strategicCellId;

        Cells =
            Array.AsReadOnly(
                orderedCells);
    }
}
