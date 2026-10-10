namespace GlobalArena.World;

/// <summary>
/// Read-only selection of canonical physical tiles by strategic graph distance.
/// The radius is in adjacency steps, not a geometry refinement parameter.
/// </summary>
public static class PhysicalTacticalViewportQuery
{
    public static IReadOnlyList<PhysicalTacticalTileId> SelectTiles(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId focus,
        int adjacentSteps)
    {
        ArgumentNullException.ThrowIfNull(map);
        var cells = map.CoarseTopology.Cells;
        // This is a conservative upper bound rather than the graph diameter.
        if (adjacentSteps < 0 || adjacentSteps > cells.Count)
            throw new ArgumentOutOfRangeException(nameof(adjacentSteps));

        var byId = cells.ToDictionary(cell => cell.Id);
        if (!focus.IsValid || !byId.ContainsKey(focus))
            throw new ArgumentOutOfRangeException(nameof(focus));

        var included = new HashSet<StrategicCellId> { focus };
        var frontier = new Queue<(StrategicCellId Cell, int Distance)>();
        frontier.Enqueue((focus, 0));
        while (frontier.Count > 0)
        {
            var (cellId, distance) = frontier.Dequeue();
            if (distance >= adjacentSteps)
                continue;
            foreach (var neighbor in byId[cellId].AdjacentCellIds)
            {
                if (!byId.ContainsKey(neighbor))
                    throw new InvalidOperationException("Coarse topology contains an unknown adjacent cell.");
                if (included.Add(neighbor))
                    frontier.Enqueue((neighbor, distance + 1));
            }
        }

        return Array.AsReadOnly(
            map.TileIncidences
                .Where(tile => tile.IncidentCoarseCellIds.Any(included.Contains))
                .Select(tile => tile.PhysicalTacticalTileId)
                .OrderBy(tile => tile.FineStrategicCellId.Value)
                .ToArray());
    }
}
