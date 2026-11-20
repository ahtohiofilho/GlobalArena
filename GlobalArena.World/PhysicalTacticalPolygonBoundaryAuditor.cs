namespace GlobalArena.World;

/// <summary>
/// Inspect polygon boundaries using the SAME generated fine topology and
/// incidence as production. Shared-incidence ties are resolved to the lowest
/// coarse ID only for this research overlay: they remain shared in the game.
/// This is not yet a proof of correctness of final strategic territory lines.
/// </summary>
internal static class PhysicalTacticalPolygonBoundaryAuditor
{
    internal static PhysicalTacticalPolygonBoundaryDiagnostics Analyze(
        GoldbergScaledRefinement refinement,
        StrategicTopology coarse, StrategicTopology fine,
        IReadOnlyCollection<StrategicCellId>[] multiOwnerSets)
    {
        if (multiOwnerSets.Length != fine.Cells.Count)
            throw new ArgumentException("Fine incidence does not cover all polygons.");

        var chosen = multiOwnerSets.Select(set =>
            set.OrderBy(id => id.Value).First()).ToArray();
        int shared = multiOwnerSets.Count(set => set.Count > 1);
        var expected = coarse.Edges.Select(edge =>
        {
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            return (Math.Min(a, b), Math.Max(a, b));
        }).ToHashSet();

        // A fine topology edge is precisely one common polygon side.
        // Its two incident topology vertices are the side's REAL corners,
        // not artificial midpoints between cell centers.
        var pairSegments = new Dictionary<(ulong, ulong),
            List<(int A, int B)>>();
        int invalidVertices = 0;
        int boundaryEdges = 0;
        foreach (var edge in fine.Edges)
        {
            int firstIndex = checked((int)edge.IncidentCellIds[0].Value - 1);
            int secondIndex = checked((int)edge.IncidentCellIds[1].Value - 1);
            var firstOwner = chosen[firstIndex].Value;
            var secondOwner = chosen[secondIndex].Value;
            if (firstOwner == secondOwner) continue;
            boundaryEdges++;
            int a = checked((int)edge.IncidentVertexIds[0].Value - 1);
            int b = checked((int)edge.IncidentVertexIds[1].Value - 1);
            if (a == b || a < 0 || b < 0 ||
                a >= fine.Vertices.Count || b >= fine.Vertices.Count)
            {
                invalidVertices++;
                continue;
            }
            var pair = (Math.Min(firstOwner, secondOwner),
                Math.Max(firstOwner, secondOwner));
            if (!pairSegments.TryGetValue(pair, out var segments))
                pairSegments.Add(pair, segments = new());
            segments.Add((a, b));
        }

        int branching = 0, disconnected = 0, closed = 0;
        foreach (var (_, segments) in pairSegments)
        {
            var graph = new Dictionary<int, HashSet<int>>();
            foreach (var (a, b) in segments)
            {
                if (!graph.TryGetValue(a, out var aNeighbors))
                    graph.Add(a, aNeighbors = new());
                if (!graph.TryGetValue(b, out var bNeighbors))
                    graph.Add(b, bNeighbors = new());
                aNeighbors.Add(b);
                bNeighbors.Add(a);
            }
            if (graph.Any(kv => kv.Value.Count > 2))
                branching++;
            var start = graph.Keys.Min();
            var visited = new HashSet<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                foreach (var adjacent in graph[id])
                    if (visited.Add(adjacent)) queue.Enqueue(adjacent);
            }
            if (visited.Count != graph.Count)
                disconnected++;
            if (graph.Count > 0 && graph.All(kv => kv.Value.Count == 2))
                closed++;
        }

        int represented = pairSegments.Keys.Count(expected.Contains);
        int unexpected = pairSegments.Keys.Count(pair => !expected.Contains(pair));
        return new PhysicalTacticalPolygonBoundaryDiagnostics(
            refinement.CoarseParameters, refinement.FineParameters,
            fine.Edges.Count, shared, boundaryEdges, expected.Count,
            represented, unexpected, branching, disconnected, closed,
            invalidVertices);
    }
}
