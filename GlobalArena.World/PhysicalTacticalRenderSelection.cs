namespace GlobalArena.World;

/// <summary>A canonical physical tile selected for rendering, independent of any graphics API.</summary>
public sealed class PhysicalTacticalRenderItem
{
    public PhysicalTacticalTileId TileId { get; }
    public IReadOnlyList<StrategicCellId> IncidentStrategicCells { get; }

    internal PhysicalTacticalRenderItem(PhysicalTacticalTileIncidence incidence)
    {
        TileId = incidence.PhysicalTacticalTileId;
        IncidentStrategicCells = Array.AsReadOnly(incidence.IncidentCoarseCellIds.ToArray());
    }
}

/// <summary>Immutable canonical identity snapshot for a GPU-independent rendering pipeline.</summary>
public sealed class PhysicalTacticalRenderSelection
{
    public StrategicCellId Focus { get; }
    public int AdjacentSteps { get; }
    public IReadOnlyList<PhysicalTacticalRenderItem> Items { get; }

    private PhysicalTacticalRenderSelection(
        StrategicCellId focus, int adjacentSteps, PhysicalTacticalRenderItem[] items)
    {
        Focus = focus;
        AdjacentSteps = adjacentSteps;
        Items = Array.AsReadOnly(items);
    }

    public static PhysicalTacticalRenderSelection Create(
        PhysicalTacticalIncidenceMap map, StrategicCellId focus, int adjacentSteps)
    {
        ArgumentNullException.ThrowIfNull(map);
        var selected = PhysicalTacticalViewportQuery.SelectTiles(map, focus, adjacentSteps);
        var byId = map.TileIncidences.ToDictionary(item => item.PhysicalTacticalTileId);
        var items = new PhysicalTacticalRenderItem[selected.Count];
        for (var i = 0; i < selected.Count; i++)
        {
            if (!byId.TryGetValue(selected[i], out var incidence))
                throw new InvalidOperationException("Selected physical tile is missing its canonical incidence.");
            items[i] = new PhysicalTacticalRenderItem(incidence);
        }
        return new PhysicalTacticalRenderSelection(focus, adjacentSteps, items);
    }
}
