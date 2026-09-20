namespace GlobalArena.World;

public sealed class TacticalCell
{
    public TacticalCellId Id { get; }

    public IReadOnlyList<TacticalCellId> AdjacentCellIds { get; }

    public TacticalCell(
        TacticalCellId id,
        IEnumerable<TacticalCellId> adjacentCellIds)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Tactical cell ID must be valid.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(
            adjacentCellIds);

        var adjacent =
            adjacentCellIds.ToArray();

        if (adjacent.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Adjacent tactical cell IDs must be valid.",
                nameof(adjacentCellIds));
        }

        if (adjacent.Any(
            candidate =>
                candidate.ParentStrategicCellId
                != id.ParentStrategicCellId))
        {
            throw new ArgumentException(
                "M2.2 tactical adjacency must remain within the same strategic parent region.",
                nameof(adjacentCellIds));
        }

        if (adjacent.Contains(id))
        {
            throw new ArgumentException(
                "A tactical cell cannot be adjacent to itself.",
                nameof(adjacentCellIds));
        }

        if (adjacent.Distinct().Count()
            != adjacent.Length)
        {
            throw new ArgumentException(
                "Adjacent tactical cell IDs cannot contain duplicates.",
                nameof(adjacentCellIds));
        }

        Id = id;

        AdjacentCellIds =
            Array.AsReadOnly(
                adjacent
                    .OrderBy(
                        candidate =>
                            candidate.LocalOrdinal)
                    .ToArray());
    }
}
