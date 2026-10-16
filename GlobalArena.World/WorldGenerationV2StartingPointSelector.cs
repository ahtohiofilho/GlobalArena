using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalArena.World;

/// <summary>Cost-aware V2 start-point selection; no economic cost coefficients are fixed here.</summary>
public static class WorldGenerationV2StartingPointSelector
{
    public static IReadOnlyList<StrategicCellId> Select(
        StrategicSurfaceGraph graph,
        int temporaryReferenceIndex,
        int count,
        Func<int, bool> isEligible,
        Func<int, int, long> movementCost)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(isEligible);
        ArgumentNullException.ThrowIfNull(movementCost);
        if (temporaryReferenceIndex < 0 || temporaryReferenceIndex >= graph.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(temporaryReferenceIndex));
        if (count < 1 || count >= graph.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(count));

        var allowed = new bool[graph.NodeCount];
        var eligibleCount = 0;
        for (var i = 0; i < allowed.Length; i++)
        {
            allowed[i] = i != temporaryReferenceIndex && isEligible(i);
            if (allowed[i]) eligibleCount++;
        }
        if (count > eligibleCount)
            throw new ArgumentOutOfRangeException(nameof(count), "Not enough eligible cells other than the temporary reference.");

        var chosen = new List<int>(count);
        var chosenFlags = new bool[graph.NodeCount];
        for (var k = 0; k < count; k++)
        {
            var seedSources = k == 0 ? new[] { temporaryReferenceIndex } : chosen.ToArray();
            var distances = ShortestPaths(graph, seedSources, movementCost);
            long farthest = long.MinValue;
            var best = -1;
            for (var i = 0; i < graph.NodeCount; i++)
            {
                if (!allowed[i] || chosenFlags[i]) continue;
                if (distances[i] == long.MaxValue)
                    throw new InvalidOperationException("Eligible cell is unreachable by cost paths.");
                if (distances[i] > farthest || (distances[i] == farthest && (best < 0 || i < best)))
                {
                    best = i;
                    farthest = distances[i];
                }
            }
            if (best < 0) throw new InvalidOperationException("No remaining eligible start point.");
            chosen.Add(best);
            chosenFlags[best] = true;
        }
        return chosen.Select(graph.GetCellId).ToArray();
    }

    private static long[] ShortestPaths(StrategicSurfaceGraph graph, IReadOnlyList<int> sources,
        Func<int, int, long> movementCost)
    {
        var distances = Enumerable.Repeat(long.MaxValue, graph.NodeCount).ToArray();
        var queue = new PriorityQueue<int, (long Distance, int Index)>();
        foreach (var i in sources)
        {
            if (distances[i] == 0) continue;
            distances[i] = 0;
            queue.Enqueue(i, (0, i));
        }
        while (queue.TryDequeue(out var node, out var priority))
        {
            if (priority.Distance != distances[node]) continue;
            foreach (var neighbor in graph.GetNeighborIndexes(node))
            {
                var cost = movementCost(node, neighbor);
                if (cost < 0) throw new ArgumentOutOfRangeException(nameof(movementCost), "Negative movement cost is unsupported.");
                if (cost > long.MaxValue - distances[node])
                    throw new OverflowException("Movement cost path overflow.");
                var candidate = distances[node] + cost;
                if (candidate >= distances[neighbor]) continue;
                distances[neighbor] = candidate;
                queue.Enqueue(neighbor, (candidate, neighbor));
            }
        }
        return distances;
    }
}
