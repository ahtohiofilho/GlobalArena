namespace GlobalArena.World;

// Renderer-neutral paths on the authoritative fine tile graph. These are
// presentation carriers; they do not change territory ownership or movement.
public sealed class PhysicalTacticalTerritoryBoundaryPath
{
    public StrategicEdgeId StrategicEdgeId { get; }
    public IReadOnlyList<StrategicCellId> FineCellIds { get; }

    internal PhysicalTacticalTerritoryBoundaryPath(
        StrategicEdgeId strategicEdgeId, IReadOnlyList<StrategicCellId> fineCellIds)
    {
        StrategicEdgeId = strategicEdgeId;
        FineCellIds = Array.AsReadOnly(fineCellIds.ToArray());
    }
}

public static class PhysicalTacticalTerritoryBoundaryMaterializer
{
    // MVP contract proven by the 92-focus G(3,0)->G(18,0) geometric audit.
    // Do not extrapolate to other Goldberg families without an independent gate.
    public static IReadOnlyList<PhysicalTacticalTerritoryBoundaryPath> Materialize(
        PhysicalTacticalIncidenceMap physical)
    {
        ArgumentNullException.ThrowIfNull(physical);
        if (physical.Refinement.CoarseParameters != new GoldbergParameters(3, 0) ||
            physical.Refinement.FineParameters != new GoldbergParameters(18, 0))
            throw new NotSupportedException("Territory boundary path MVP supports only G(3,0) -> G(18,0).");

        var owners = physical.TileIncidences.ToDictionary(
            item => item.PhysicalTacticalTileId.FineStrategicCellId.Value,
            item => item.IncidentCoarseCellIds.Select(id => id.Value).ToArray());
        var edgeAnchors = physical.TileIncidences
            .Where(item => item.IncidentCoarseCellIds.Count == 2)
            .GroupBy(item => Signature(item.IncidentCoarseCellIds.Select(id => id.Value)))
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.PhysicalTacticalTileId.FineStrategicCellId.Value).ToArray());
        var vertexAnchors = physical.TileIncidences
            .Where(item => item.IncidentCoarseCellIds.Count == 3)
            .GroupBy(item => Signature(item.IncidentCoarseCellIds.Select(id => id.Value)))
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.PhysicalTacticalTileId.FineStrategicCellId.Value).ToArray());
        if (edgeAnchors.Count != physical.CoarseTopology.Edges.Count ||
            vertexAnchors.Count != physical.CoarseTopology.Vertices.Count)
            throw new InvalidOperationException("Strategic boundary anchor coverage mismatch.");

        var usedSegments = new HashSet<(ulong A, ulong B)>();
        var paths = new List<PhysicalTacticalTerritoryBoundaryPath>();
        foreach (var edge in physical.CoarseTopology.Edges.OrderBy(item => item.Id.Value))
        {
            var pair = edge.IncidentCellIds.Select(id => id.Value).Order().ToArray();
            if (!edgeAnchors.TryGetValue(Signature(pair), out var midpoints) || midpoints.Length != 1)
                throw new InvalidOperationException("Expected exactly one physical midpoint per strategic edge.");
            var junctions = edge.IncidentVertexIds.Select(vertexId =>
            {
                var vertex = physical.CoarseTopology.Vertices[checked((int)vertexId.Value - 1)];
                if (!vertexAnchors.TryGetValue(
                    Signature(vertex.IncidentCellIds.Select(id => id.Value)), out var ids) || ids.Length != 1)
                    throw new InvalidOperationException("Expected exactly one physical three-way junction.");
                return ids[0];
            }).ToArray();
            var first = FindPath(physical, owners, junctions[0], midpoints[0], pair);
            var second = FindPath(physical, owners, midpoints[0], junctions[1], pair);
            var route = first.Concat(second.Skip(1)).ToArray();
            if (route.Length != 5 || route.Distinct().Count() != route.Length)
                throw new InvalidOperationException("MVP boundary path must have five distinct physical anchors.");
            for (var index = 1; index < route.Length; index++)
            {
                var a = Math.Min(route[index - 1], route[index]);
                var b = Math.Max(route[index - 1], route[index]);
                if (!usedSegments.Add((a, b)))
                    throw new InvalidOperationException("Physical boundary segment duplicated between strategic edges.");
            }
            paths.Add(new PhysicalTacticalTerritoryBoundaryPath(
                edge.Id, route.Select(id => new StrategicCellId(id)).ToArray()));
        }
        if (paths.Count != 270 || usedSegments.Count != 1080)
            throw new InvalidOperationException("MVP strategic boundary coverage invariant failed.");
        return Array.AsReadOnly(paths.ToArray());
    }

    private static IReadOnlyList<ulong> FindPath(
        PhysicalTacticalIncidenceMap physical,
        IReadOnlyDictionary<ulong, ulong[]> owners,
        ulong start,
        ulong goal,
        IReadOnlyList<ulong> pair)
    {
        var parent = new Dictionary<ulong, ulong> { [start] = start };
        var pending = new Queue<ulong>();
        pending.Enqueue(start);
        while (pending.Count > 0 && !parent.ContainsKey(goal))
        {
            var current = pending.Dequeue();
            var cell = physical.FineTopology.Cells[checked((int)current - 1)];
            foreach (var adjacent in cell.AdjacentCellIds.Select(id => id.Value).Order())
            {
                if (parent.ContainsKey(adjacent)) continue;
                var incidence = owners[adjacent];
                if (adjacent != goal && (incidence.Length != 1 || !pair.Contains(incidence[0])))
                    continue;
                parent.Add(adjacent, current);
                pending.Enqueue(adjacent);
            }
        }
        if (!parent.ContainsKey(goal))
            throw new InvalidOperationException("Physical boundary anchors lack a canonical adjacency path.");
        var reversed = new List<ulong>();
        var cursor = goal;
        for (var index = 0; index <= physical.FineTopology.Cells.Count; index++)
        {
            reversed.Add(cursor);
            if (cursor == start)
            {
                reversed.Reverse();
                return reversed;
            }
            cursor = parent[cursor];
        }
        throw new InvalidOperationException("Physical boundary graph contains an invalid predecessor cycle.");
    }

    private static string Signature(IEnumerable<ulong> ids) => string.Join(",", ids.Order());
}