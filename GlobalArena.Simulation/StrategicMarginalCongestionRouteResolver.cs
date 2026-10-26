using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Deterministic shortest-*marginal-cost* path on the existing strategic
/// surface graph. Edge weights are the smooth per-gram marginal costs at
/// current shared traffic loads. Weight ties prefer fewer hops, then stable
/// canonical node/edge order. A profile scale is never a traffic limit.
///
/// Each call searches one directional economic-point pair. No all-pairs
/// preprocessing, vehicle classes, hard throughput or modal inventories.
/// </summary>
public sealed class StrategicMarginalCongestionRouteResolver
{
    private readonly StrategicSurfaceGraph _graph;
    private readonly Dictionary<EconomicPointId, StrategicCellId> _pointCells;
    private readonly IReadOnlyList<(int Neighbor, StrategicEdgeId Edge)>[] _adjacency;
    private readonly HashSet<StrategicEdgeId> _graphEdges;

    public StrategicMarginalCongestionRouteResolver(
        StrategicSurfaceGraph surfaceGraph,
        EconomyRuntimeState economy)
    {
        ArgumentNullException.ThrowIfNull(surfaceGraph);
        ArgumentNullException.ThrowIfNull(economy);

        _graph = surfaceGraph;
        _pointCells = economy.EconomicPoints.ToDictionary(
            point => point.Id, point => point.StrategicCellId);
        _graphEdges = surfaceGraph.StrategicTopology.Edges
            .Select(edge => edge.Id).ToHashSet();

        var adjacency = Enumerable.Range(0, _graph.NodeCount)
            .Select(_ => new List<(int Neighbor, StrategicEdgeId Edge)>())
            .ToArray();

        foreach (var edge in surfaceGraph.StrategicTopology.Edges)
        {
            var first = _graph.GetNodeIndex(edge.IncidentCellIds[0]);
            var second = _graph.GetNodeIndex(edge.IncidentCellIds[1]);
            adjacency[first].Add((second, edge.Id));
            adjacency[second].Add((first, edge.Id));
        }

        _adjacency = adjacency.Select(rows =>
            (IReadOnlyList<(int Neighbor, StrategicEdgeId Edge)>)
                Array.AsReadOnly(rows
                    .OrderBy(row => row.Neighbor)
                    .ThenBy(row => row.Edge.Value)
                    .ToArray()))
            .ToArray();
    }

    public StrategicEconomicRoute? Resolve(
        StrategicEconomicRouteKey routeKey,
        StrategicEdgeAccessSnapshot access,
        IReadOnlyDictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile> profiles,
        IReadOnlyDictionary<StrategicEdgeId, BigInteger> currentLoads)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(currentLoads);

        foreach (var closed in access.ClosedEdgeIds)
            if (!_graphEdges.Contains(closed))
                throw new KeyNotFoundException(
                    "Closed strategic edge does not belong to the supplied graph.");

        if (!_pointCells.TryGetValue(
            routeKey.SourceEconomicPointId, out var sourceCell))
            throw new KeyNotFoundException(
                "Marginal route source economic point does not exist.");
        if (!_pointCells.TryGetValue(
            routeKey.DestinationEconomicPointId, out var destinationCell))
            throw new KeyNotFoundException(
                "Marginal route destination economic point does not exist.");

        var start = _graph.GetNodeIndex(sourceCell);
        var finish = _graph.GetNodeIndex(destinationCell);

        if (start == finish)
            return new StrategicEconomicRoute(
                routeKey, new[] { sourceCell }, Array.Empty<StrategicEdgeId>());

        var predecessor = Enumerable.Repeat(-1, _graph.NodeCount).ToArray();
        var previousEdges = new StrategicEdgeId[_graph.NodeCount];
        var best = new RouteRank?[_graph.NodeCount];
        var queue = new PriorityQueue<int, RouteRank>();

        var zero = new RouteRank(
            new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One),
            0, start);
        best[start] = zero;
        queue.Enqueue(start, zero);

        while (queue.TryDequeue(out var cell, out var rank))
        {
            if (best[cell] is not RouteRank accepted ||
                rank.CompareTo(accepted) != 0)
                continue;

            if (cell == finish)
                break;

            foreach (var (neighbor, edgeId) in _adjacency[cell])
            {
                if (!access.IsOpen(edgeId))
                    continue;

                if (!profiles.TryGetValue(edgeId, out var profile))
                    throw new InvalidOperationException(
                        $"Open strategic edge {edgeId.Value} is missing an explicit congestion price profile.");

                var load = currentLoads.TryGetValue(edgeId, out var current)
                    ? current : BigInteger.Zero;
                if (load.Sign < 0)
                    throw new ArgumentOutOfRangeException(nameof(currentLoads));

                var marginal = profile.EvaluateMarginalCostPerGram(load);
                var score = Add(rank.Cost, marginal);
                var next = new RouteRank(score, checked(rank.Hops + 1), neighbor);
                if (best[neighbor] is RouteRank prior &&
                    next.CompareTo(prior) >= 0)
                    continue;

                best[neighbor] = next;
                predecessor[neighbor] = cell;
                previousEdges[neighbor] = edgeId;
                queue.Enqueue(neighbor, next);
            }
        }

        if (best[finish] is null)
            return null;

        var reverseCells = new List<StrategicCellId> { destinationCell };
        var reverseEdges = new List<StrategicEdgeId>();
        var cursor = finish;

        while (cursor != start)
        {
            var parent = predecessor[cursor];
            if (parent < 0 || !previousEdges[cursor].IsValid)
                throw new InvalidOperationException(
                    "Congestion route reconstruction has a missing predecessor.");

            reverseEdges.Add(previousEdges[cursor]);
            cursor = parent;
            reverseCells.Add(_graph.GetCellId(cursor));
        }

        reverseCells.Reverse();
        reverseEdges.Reverse();
        return new StrategicEconomicRoute(routeKey, reverseCells, reverseEdges);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction left, FoodTransportCostFraction right) =>
        new(left.Numerator * right.Denominator
                + right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    private readonly record struct RouteRank(
        FoodTransportCostFraction Cost,
        int Hops,
        int Node) : IComparable<RouteRank>
    {
        public int CompareTo(RouteRank other)
        {
            var price = Cost.CompareTo(other.Cost);
            if (price != 0)
                return price;

            var hops = Hops.CompareTo(other.Hops);
            return hops != 0 ? hops : Node.CompareTo(other.Node);
        }
    }
}
