namespace GlobalArena.World;

/// <summary>
/// R108: an isolated diagnostic for candidate boundary paths in the real
/// class-I physical adjacency graph. This never changes owner incidence,
/// never unlocks a non-sixfold scale, and never creates strategic control.
/// </summary>
internal static class PhysicalTacticalBoundaryPathCandidateAuditor
{
    internal static PhysicalTacticalBoundaryPathCandidateDiagnostics Analyze(
        GoldbergParameters coarseParameters,
        GoldbergParameters fineParameters,
        StrategicTopology coarse,
        StrategicTopology fine,
        IReadOnlyCollection<StrategicCellId>[] ownerSets)
    {
        ArgumentNullException.ThrowIfNull(coarse);
        ArgumentNullException.ThrowIfNull(fine);
        ArgumentNullException.ThrowIfNull(ownerSets);
        if (ownerSets.Length != fine.Cells.Count)
            throw new ArgumentException("Physical owner sets must cover the fine graph.");

        static string Key(IEnumerable<StrategicCellId> ids) =>
            string.Join(",", ids.Select(id => id.Value).Order());

        var doubleAnchors = ownerSets
            .Select((owners, index) => (owners, index))
            .Where(item => item.owners.Count == 2)
            .GroupBy(item => Key(item.owners))
            .ToDictionary(g => g.Key, g => g.Select(x => x.index).ToArray());
        var tripleAnchors = ownerSets
            .Select((owners, index) => (owners, index))
            .Where(item => item.owners.Count == 3)
            .GroupBy(item => Key(item.owners))
            .ToDictionary(g => g.Key, g => g.Select(x => x.index).ToArray());

        int complete = 0, missing = 0, ambiguous = 0, disconnected = 0;
        int repeated = 0, overlaps = 0, alternativeHalfPaths = 0;
        int min = int.MaxValue, max = 0;
        var usedSegments = new HashSet<(int A, int B)>();

        foreach (var edge in coarse.Edges.OrderBy(e => e.Id.Value))
        {
            string pair = Key(edge.IncidentCellIds);
            var junctionCandidates = new int[2][];
            for (int j = 0; j < 2; j++)
            {
                var vertex = coarse.Vertices[
                    checked((int)edge.IncidentVertexIds[j].Value - 1)];
                tripleAnchors.TryGetValue(Key(vertex.IncidentCellIds),
                    out var candidates);
                junctionCandidates[j] = candidates ?? Array.Empty<int>();
            }
            doubleAnchors.TryGetValue(pair, out var midpoints);
            midpoints ??= Array.Empty<int>();
            if (midpoints.Length == 0 ||
                junctionCandidates.Any(j => j.Length == 0))
            {
                missing++;
                continue;
            }
            if (midpoints.Length != 1 ||
                junctionCandidates.Any(j => j.Length != 1))
            {
                ambiguous++;
                continue;
            }

            int midpoint = midpoints[0];
            int first = junctionCandidates[0][0];
            int last = junctionCandidates[1][0];
            var (firstHalf, firstAlternatives) = FindShortestHalf(
                fine, ownerSets, edge.IncidentCellIds,
                first, midpoint);
            var (secondHalf, secondAlternatives) = FindShortestHalf(
                fine, ownerSets, edge.IncidentCellIds,
                midpoint, last);
            if (firstHalf is null || secondHalf is null)
            {
                disconnected++;
                continue;
            }
            if (firstAlternatives || secondAlternatives)
                alternativeHalfPaths++;
            var path = firstHalf.Concat(secondHalf.Skip(1)).ToArray();
            if (path.Distinct().Count() != path.Length)
            {
                repeated++;
                continue;
            }

            // All links must correspond to an actual physical adjacency.
            for (int i = 1; i < path.Length; i++)
            {
                int a = path[i - 1], b = path[i];
                if (!fine.Cells[a].AdjacentCellIds.Any(
                    n => n.Value == (ulong)b + 1UL))
                    throw new InvalidOperationException(
                        "A candidate corridor contains a non-physical edge.");
                if (!usedSegments.Add((Math.Min(a, b), Math.Max(a, b))))
                    overlaps++;
            }
            complete++;
            min = Math.Min(min, path.Length);
            max = Math.Max(max, path.Length);
        }

        return new PhysicalTacticalBoundaryPathCandidateDiagnostics(
            coarseParameters, fineParameters, coarse.Edges.Count,
            complete, missing, ambiguous, disconnected,
            repeated, overlaps, alternativeHalfPaths,
            min == int.MaxValue ? 0 : min, max);
    }

    private static (int[]? Path, bool Alternatives) FindShortestHalf(
        StrategicTopology fine,
        IReadOnlyCollection<StrategicCellId>[] owners,
        IReadOnlyList<StrategicCellId> pair,
        int start, int goal)
    {
        var distance = Enumerable.Repeat(-1, fine.Cells.Count).ToArray();
        var parent = Enumerable.Repeat(-1, fine.Cells.Count).ToArray();
        var ways = new byte[fine.Cells.Count];
        distance[start] = 0;
        parent[start] = start;
        ways[start] = 1;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (var adjacent in fine.Cells[current].AdjacentCellIds
                .OrderBy(id => id.Value))
            {
                int next = checked((int)adjacent.Value - 1);
                if (next != goal && next != start &&
                    (owners[next].Count != 1 ||
                     !owners[next].Any(owner => pair.Contains(owner))))
                    continue;
                int step = distance[current] + 1;
                if (distance[next] == -1)
                {
                    distance[next] = step;
                    parent[next] = current;
                    ways[next] = ways[current];
                    queue.Enqueue(next);
                }
                else if (distance[next] == step)
                    ways[next] = (byte)Math.Min(2,
                        ways[next] + ways[current]);
            }
        }
        if (distance[goal] < 0)
            return (null, false);

        var reversed = new List<int>();
        int cursor = goal;
        for (int i = 0; i <= fine.Cells.Count; i++)
        {
            reversed.Add(cursor);
            if (cursor == start)
            {
                reversed.Reverse();
                return (reversed.ToArray(), ways[goal] > 1);
            }
            cursor = parent[cursor];
            if (cursor < 0)
                break;
        }
        throw new InvalidOperationException(
            "Candidate physical corridor has a broken BFS predecessor.");
    }
}
