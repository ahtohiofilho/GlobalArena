namespace GlobalArena.World;

/// <summary>Immutable strategic focus neighborhood and its canonical physical tiles.</summary>
public sealed class PhysicalTacticalViewportFrame
{
    public StrategicCellId Focus { get; }
    public IReadOnlyList<StrategicCellId> StrategicCellIds { get; }
    public IReadOnlyList<PhysicalTacticalTileId> PhysicalTileIds { get; }

    private PhysicalTacticalViewportFrame(
        StrategicCellId focus,
        StrategicCellId[] strategic,
        PhysicalTacticalTileId[] physical)
    {
        Focus = focus;
        StrategicCellIds = Array.AsReadOnly(strategic);
        PhysicalTileIds = Array.AsReadOnly(physical);
    }

    public static PhysicalTacticalViewportFrame Create(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId focus)
    {
        ArgumentNullException.ThrowIfNull(map);
        var cells = map.CoarseTopology.Cells.ToDictionary(cell => cell.Id);
        if (!focus.IsValid || !cells.ContainsKey(focus))
            throw new ArgumentOutOfRangeException(nameof(focus));

        var neighborhood = new HashSet<StrategicCellId>(cells[focus].AdjacentCellIds)
        {
            focus
        };
        foreach (var id in neighborhood)
            if (!cells.ContainsKey(id))
                throw new InvalidOperationException("Unknown strategic neighbor.");

        var strategic = neighborhood.OrderBy(id => id.Value).ToArray();
        var physical = PhysicalTacticalViewportQuery.SelectTiles(map, focus, 1).ToArray();
        return new PhysicalTacticalViewportFrame(focus, strategic, physical);
    }
}