namespace GlobalArena.World;

public sealed class PhysicalTacticalPath
{
    public IReadOnlyList<PhysicalTacticalTileId> TileIds { get; }

    public PhysicalTacticalTileId Start => TileIds[0];

    public PhysicalTacticalTileId End => TileIds[^1];

    public int StepCount => TileIds.Count - 1;

    internal PhysicalTacticalPath(
        PhysicalTacticalIncidenceMap incidenceMap,
        IEnumerable<PhysicalTacticalTileId> tileIds)
    {
        ArgumentNullException.ThrowIfNull(incidenceMap);
        ArgumentNullException.ThrowIfNull(tileIds);

        var tiles =
            tileIds.ToArray();

        if (tiles.Length == 0)
        {
            throw new ArgumentException(
                "A physical tactical path must contain at least one tile.",
                nameof(tileIds));
        }

        if (tiles.Any(tileId => !tileId.IsValid))
        {
            throw new ArgumentException(
                "Every physical tactical path tile identity must be valid.",
                nameof(tileIds));
        }

        if (tiles.Any(
            tileId =>
                tileId.FineGoldbergParameters
                != incidenceMap.Refinement.FineParameters))
        {
            throw new ArgumentException(
                "Every physical tactical path tile must belong to the incidence map fine topology.",
                nameof(tileIds));
        }

        if (tiles.Distinct().Count() != tiles.Length)
        {
            throw new ArgumentException(
                "A physical tactical path cannot repeat a tile identity.",
                nameof(tileIds));
        }

        for (var index = 1; index < tiles.Length; index++)
        {
            var previous =
                tiles[index - 1]
                    .FineStrategicCellId;

            var current =
                tiles[index]
                    .FineStrategicCellId;

            var previousCell =
                incidenceMap.FineTopology.Cells[
                    checked((int)previous.Value - 1)];

            if (!previousCell.AdjacentCellIds.Contains(current))
            {
                throw new ArgumentException(
                    "Consecutive physical tactical path tiles must be adjacent in the authoritative fine topology.",
                    nameof(tileIds));
            }
        }

        TileIds =
            Array.AsReadOnly(
                tiles);
    }
}