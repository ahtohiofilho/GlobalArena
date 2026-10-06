using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class StrategicEconomicRouteResolver
{
    private readonly StrategicSurfaceGraph _surfaceGraph;

    private readonly Dictionary<(ulong First, ulong Second), StrategicEdgeId> _edgeByCellPair;

    public StrategicSurfaceGraph SurfaceGraph =>
        _surfaceGraph;

    public StrategicEconomicRouteResolver(
        StrategicSurfaceGraph surfaceGraph)
    {
        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        _surfaceGraph =
            surfaceGraph;

        _edgeByCellPair =
            new Dictionary<(ulong First, ulong Second), StrategicEdgeId>(
                surfaceGraph.StrategicTopology.Edges.Count);

        foreach (var edge in
            surfaceGraph.StrategicTopology.Edges)
        {
            var first =
                edge.IncidentCellIds[0].Value;

            var second =
                edge.IncidentCellIds[1].Value;

            var key =
                NormalizePair(
                    first,
                    second);

            if (!_edgeByCellPair.TryAdd(
                key,
                edge.Id))
            {
                throw new ArgumentException(
                    "Strategic topology contains duplicate cell-pair edge identity.",
                    nameof(surfaceGraph));
            }
        }

        ValidateGraphEdgeCoverage();
    }

    public bool TryResolve(
        EconomyRuntimeState economy,
        StrategicEconomicRouteKey key,
        StrategicEdgeAccessSnapshot access,
        out StrategicEconomicRoute? route)
    {
        ArgumentNullException.ThrowIfNull(
            economy);

        ArgumentNullException.ThrowIfNull(
            access);

        ValidateAccessSnapshot(
            access);

        var points =
            economy
                .EconomicPoints
                .ToDictionary(
                    point =>
                        point.Id);

        if (!points.TryGetValue(
            key.SourceEconomicPointId,
            out var sourcePoint))
        {
            throw new KeyNotFoundException(
                $"Source economic point {key.SourceEconomicPointId.Value} does not exist in the Economy state.");
        }

        if (!points.TryGetValue(
            key.DestinationEconomicPointId,
            out var destinationPoint))
        {
            throw new KeyNotFoundException(
                $"Destination economic point {key.DestinationEconomicPointId.Value} does not exist in the Economy state.");
        }

        var sourceIndex =
            _surfaceGraph.GetNodeIndex(
                sourcePoint.StrategicCellId);

        var destinationIndex =
            _surfaceGraph.GetNodeIndex(
                destinationPoint.StrategicCellId);

        if (sourceIndex
            == destinationIndex)
        {
            route =
                new StrategicEconomicRoute(
                    key,
                    new[]
                    {
                        sourcePoint.StrategicCellId
                    },
                    Array.Empty<StrategicEdgeId>());

            return true;
        }

        var nodeCount =
            _surfaceGraph.NodeCount;

        var visited =
            new bool[nodeCount];

        var parent =
            Enumerable
                .Repeat(
                    -1,
                    nodeCount)
                .ToArray();

        var parentEdge =
            new StrategicEdgeId[nodeCount];

        var queue =
            new Queue<int>();

        visited[sourceIndex] =
            true;

        queue.Enqueue(
            sourceIndex);

        var found =
            false;

        while (queue.Count > 0
            && !found)
        {
            var currentIndex =
                queue.Dequeue();

            foreach (var neighborIndex in
                _surfaceGraph.GetNeighborIndexes(
                    currentIndex))
            {
                if (visited[neighborIndex])
                {
                    continue;
                }

                var edgeId =
                    GetEdgeId(
                        currentIndex,
                        neighborIndex);

                if (!access.IsOpen(
                    edgeId))
                {
                    continue;
                }

                visited[neighborIndex] =
                    true;

                parent[neighborIndex] =
                    currentIndex;

                parentEdge[neighborIndex] =
                    edgeId;

                if (neighborIndex
                    == destinationIndex)
                {
                    found =
                        true;

                    break;
                }

                queue.Enqueue(
                    neighborIndex);
            }
        }

        if (!found)
        {
            route =
                null;

            return false;
        }

        var reverseCells =
            new List<StrategicCellId>();

        var reverseEdges =
            new List<StrategicEdgeId>();

        var cursor =
            destinationIndex;

        reverseCells.Add(
            _surfaceGraph.GetCellId(
                cursor));

        while (cursor
            != sourceIndex)
        {
            var predecessor =
                parent[cursor];

            if (predecessor < 0)
            {
                throw new InvalidOperationException(
                    "Strategic route reconstruction encountered a missing predecessor.");
            }

            var edgeId =
                parentEdge[cursor];

            if (!edgeId.IsValid)
            {
                throw new InvalidOperationException(
                    "Strategic route reconstruction encountered a missing edge dependency.");
            }

            reverseEdges.Add(
                edgeId);

            cursor =
                predecessor;

            reverseCells.Add(
                _surfaceGraph.GetCellId(
                    cursor));
        }

        reverseCells.Reverse();
        reverseEdges.Reverse();

        route =
            new StrategicEconomicRoute(
                key,
                reverseCells,
                reverseEdges);

        return true;
    }

    private void ValidateGraphEdgeCoverage()
    {
        for (var nodeIndex = 0;
             nodeIndex < _surfaceGraph.NodeCount;
             nodeIndex++)
        {
            foreach (var neighborIndex in
                _surfaceGraph.GetNeighborIndexes(
                    nodeIndex))
            {
                _ =
                    GetEdgeId(
                        nodeIndex,
                        neighborIndex);
            }
        }
    }

    private void ValidateAccessSnapshot(
        StrategicEdgeAccessSnapshot access)
    {
        var edgeCount =
            (ulong)_surfaceGraph.StrategicTopology.Edges.Count;

        foreach (var edgeId in
            access.ClosedEdgeIds)
        {
            if (edgeId.Value
                > edgeCount)
            {
                throw new KeyNotFoundException(
                    $"Strategic edge {edgeId.Value} does not belong to this surface graph.");
            }
        }
    }

    private StrategicEdgeId GetEdgeId(
        int firstNodeIndex,
        int secondNodeIndex)
    {
        var first =
            _surfaceGraph
                .GetCellId(
                    firstNodeIndex)
                .Value;

        var second =
            _surfaceGraph
                .GetCellId(
                    secondNodeIndex)
                .Value;

        var key =
            NormalizePair(
                first,
                second);

        if (!_edgeByCellPair.TryGetValue(
            key,
            out var edgeId))
        {
            throw new InvalidOperationException(
                $"Strategic surface adjacency {key.First}<->{key.Second} has no matching StrategicEdge identity.");
        }

        return edgeId;
    }

    private static (ulong First, ulong Second) NormalizePair(
        ulong first,
        ulong second)
    {
        return first < second
            ? (first, second)
            : (second, first);
    }
}
