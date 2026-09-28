namespace GlobalArena.World;

public static class PhysicalTacticalPathfinder
{
    public static PhysicalTacticalPath FindShortestPath(
        PhysicalTacticalIncidenceMap incidenceMap,
        PhysicalTacticalTileId start,
        PhysicalTacticalTileId end)
    {
        ArgumentNullException.ThrowIfNull(incidenceMap);

        ValidateEndpoint(
            incidenceMap,
            start,
            nameof(start));

        ValidateEndpoint(
            incidenceMap,
            end,
            nameof(end));

        if (start == end)
        {
            return new PhysicalTacticalPath(
                incidenceMap,
                new[]
                {
                    start
                });
        }

        var startCellId =
            start.FineStrategicCellId;

        var endCellId =
            end.FineStrategicCellId;

        var visited =
            new HashSet<StrategicCellId>
            {
                startCellId
            };

        var predecessors =
            new Dictionary<StrategicCellId, StrategicCellId>();

        var queue =
            new Queue<StrategicCellId>();

        queue.Enqueue(
            startCellId);

        var found = false;

        while (queue.Count > 0 && !found)
        {
            var current =
                queue.Dequeue();

            var currentCell =
                incidenceMap.FineTopology.Cells[
                    checked((int)current.Value - 1)];

            foreach (var neighborId in currentCell.AdjacentCellIds)
            {
                if (!visited.Add(neighborId))
                {
                    continue;
                }

                predecessors.Add(
                    neighborId,
                    current);

                if (neighborId == endCellId)
                {
                    found = true;
                    break;
                }

                queue.Enqueue(
                    neighborId);
            }
        }

        if (!found)
        {
            throw new InvalidOperationException(
                "No physical tactical path exists between the selected fine topology tiles.");
        }

        var reversed =
            new List<StrategicCellId>
            {
                endCellId
            };

        var cursor =
            endCellId;

        while (cursor != startCellId)
        {
            if (!predecessors.TryGetValue(
                cursor,
                out var previous))
            {
                throw new InvalidOperationException(
                    "Physical tactical path reconstruction encountered incomplete predecessor state.");
            }

            cursor =
                previous;

            reversed.Add(
                cursor);
        }

        reversed.Reverse();

        var physicalIds =
            reversed
                .Select(
                    cellId =>
                        new PhysicalTacticalTileId(
                            incidenceMap.Refinement.FineParameters,
                            cellId))
                .ToArray();

        return new PhysicalTacticalPath(
            incidenceMap,
            physicalIds);
    }

    private static void ValidateEndpoint(
        PhysicalTacticalIncidenceMap incidenceMap,
        PhysicalTacticalTileId tileId,
        string parameterName)
    {
        if (!tileId.IsValid)
        {
            throw new ArgumentException(
                "Physical tactical traversal endpoint must be valid.",
                parameterName);
        }

        if (tileId.FineGoldbergParameters
            != incidenceMap.Refinement.FineParameters)
        {
            throw new ArgumentException(
                "Physical tactical traversal endpoint must belong to the incidence map fine topology.",
                parameterName);
        }
    }
}