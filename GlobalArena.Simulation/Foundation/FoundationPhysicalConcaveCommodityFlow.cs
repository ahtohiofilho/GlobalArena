using System.Numerics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// An opt-in, uncommitted min-cost flow solver for a ONE-commodity,
/// piecewise-linear concave market benefit. It jointly optimizes every
/// seller/buyer arc; reverse residual arcs permit reassigning earlier sellers.
/// This solves the DECLARED SEGMENTED objective, not the unsegmented square-root
/// equilibrium, congestion feedback, real pricing or economic settlement.
/// </summary>
internal static class FoundationPhysicalConcaveCommodityFlow
{
    internal sealed record Market(ulong QuoteMicro, ulong DemandMicro);
    internal sealed record Seller(ulong SupplyQuanta,
        IReadOnlyList<BigInteger?> TransportCostPerQuantumScaled);

    internal sealed class Result
    {
        internal ulong[][] AllocationQuanta { get; }
        internal ulong[] MarketQuanta { get; }
        internal int Augmentations { get; }
        internal BigInteger SegmentedNetPotentialScaled { get; }
        internal BigInteger IntegratedNetPotentialScaled { get; }

        internal Result(ulong[][] allocations, ulong[] markets, int augmentations,
            BigInteger segmented, BigInteger integrated)
        {
            AllocationQuanta = allocations;
            MarketQuanta = markets;
            Augmentations = augmentations;
            SegmentedNetPotentialScaled = segmented;
            IntegratedNetPotentialScaled = integrated;
        }
    }

    private sealed class Edge
    {
        internal int To { get; }
        internal int ReverseIndex { get; }
        internal BigInteger Cost { get; }
        internal ulong Remaining;

        internal Edge(int to, int reverseIndex, ulong remaining, BigInteger cost)
        {
            To = to;
            ReverseIndex = reverseIndex;
            Remaining = remaining;
            Cost = cost;
        }
    }

    private sealed class Network
    {
        internal List<Edge>[] Links { get; }

        internal Network(int vertices)
        {
            Links = Enumerable.Range(0, vertices)
                .Select(_ => new List<Edge>()).ToArray();
        }

        internal Edge Add(int from, int to, ulong capacity, BigInteger cost)
        {
            if (from == to)
                throw new InvalidOperationException("Residual self-edge is invalid.");
            var forward = new Edge(to, Links[to].Count, capacity, cost);
            var backward = new Edge(from, Links[from].Count, 0UL, -cost);
            Links[from].Add(forward);
            Links[to].Add(backward);
            return forward;
        }
    }

    /// <summary>
    /// At most ONE live entry per residual vertex, with exact priority
    /// (distance, vertex id). Replaces duplicates and stale BCL entries
    /// with an in-place decrease-key operation. Comparisons refer to the
    /// same BigInteger distances used by Dijkstra, without rounding.
    /// Strictly improving relaxations preserve the legacy predecessor tie
    /// order. Reused only within one Solve call (never across turns).
    /// </summary>
    private sealed class IndexedResidualMinHeap
    {
        private readonly int[] _nodes;
        private readonly int[] _position;
        private readonly BigInteger?[] _distance;
        private int _count;

        internal IndexedResidualMinHeap(
            int vertices, BigInteger?[] distance)
        {
            _nodes = new int[vertices];
            _position = new int[vertices];
            _distance = distance;
            Array.Fill(_position, -1);
        }

        internal void Reset()
        {
            _count = 0;
            Array.Fill(_position, -1);
        }

        private bool Less(int left, int right)
        {
            int comparison = _distance[left]!.Value.CompareTo(
                _distance[right]!.Value);
            return comparison < 0 || comparison == 0 && left < right;
        }

        private void Swap(int a, int b)
        {
            int first = _nodes[a], second = _nodes[b];
            _nodes[a] = second;
            _nodes[b] = first;
            _position[first] = b;
            _position[second] = a;
        }

        internal void InsertOrDecrease(int node)
        {
            int index = _position[node];
            if (index < 0)
            {
                if (_count == _nodes.Length)
                    throw new InvalidOperationException(
                        "Indexed residual heap exceeded physical graph vertices.");
                index = _count++;
                _nodes[index] = node;
                _position[node] = index;
            }
            // All calls follow a STRICT decrease of this node's exact
            // distance (or first discovery). It can only move toward root.
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!Less(_nodes[index], _nodes[parent]))
                    break;
                Swap(index, parent);
                index = parent;
            }
        }

        internal bool TryPop(out int node)
        {
            if (_count == 0)
            {
                node = 0;
                return false;
            }
            node = _nodes[0];
            _position[node] = -1;
            --_count;
            if (_count == 0)
                return true;

            int moved = _nodes[_count];
            _nodes[0] = moved;
            _position[moved] = 0;
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= _count)
                    break;
                int right = left + 1;
                int better = right < _count &&
                    Less(_nodes[right], _nodes[left]) ? right : left;
                if (!Less(_nodes[better], _nodes[index]))
                    break;
                Swap(index, better);
                index = better;
            }
            return true;
        }
    }

    private sealed class MarketSegments
    {
        private readonly Network _network;
        private readonly int _node;
        private readonly int _sink;
        private readonly Market _market;
        private readonly ulong _quantum;
        private readonly ulong _total;
        private readonly ulong _width;
        private BigInteger? _lastSlope;
        private ulong _covered;
        private Edge? _latest;

        internal MarketSegments(Network network, int node, int sink,
            Market market, ulong quantum, ulong total, ulong width)
        {
            _network = network;
            _node = node;
            _sink = sink;
            _market = market;
            _quantum = quantum;
            _total = total;
            _width = width;
        }

        // Exposes only the next unsaturated, consecutively activated interval.
        // Concavity ensures that a later interval is never cheaper than an
        // earlier interval on the buyer-to-sink residual graph.
        internal void ActivateIfNeeded()
        {
            if (_covered == _total || _latest?.Remaining > 0UL)
                return;
            ulong count = Math.Min(_width, _total - _covered);
            ulong upper = checked(_covered + count);
            ulong beforeGoods = checked(_covered * _quantum);
            ulong afterGoods = checked(upper * _quantum);
            BigInteger benefit = FoundationPhysicalMarginalIntegratedUtility
                .MarketBenefitScaled(_market.QuoteMicro,
                    _market.DemandMicro, afterGoods) -
                FoundationPhysicalMarginalIntegratedUtility
                .MarketBenefitScaled(_market.QuoteMicro,
                    _market.DemandMicro, beforeGoods);
            BigInteger slope = benefit / count;
            // Guard the monotone-segment invariant against integer sqrt floors.
            if (_lastSlope.HasValue && slope > _lastSlope.Value)
                slope = _lastSlope.Value;
            _latest = _network.Add(_node, _sink, count, -slope);
            _covered = upper;
            _lastSlope = slope;
        }

        internal Edge? Latest => _latest;
    }

    internal static Result Solve(IReadOnlyList<Seller> sellers,
        IReadOnlyList<Market> markets, ulong goodsQuantum,
        int segmentsPerAverageMarket, int maxAugmentations,
        bool reuseSearchWorkspace = true,
        FoundationPhysicalGlobalFlowCostProfile? profile = null,
        bool useIndexedHeap = true,
        bool useShiftedLabels = true,
        bool useResidualDominanceFastPath = true)
    {
        var setupStart = profile?.Start() ?? default;
        ArgumentNullException.ThrowIfNull(sellers);
        ArgumentNullException.ThrowIfNull(markets);
        if (sellers.Count == 0 || markets.Count == 0 ||
            goodsQuantum == 0UL || segmentsPerAverageMarket < 1 ||
            maxAugmentations < 1)
            throw new ArgumentException(
                "Positive sources, markets, quantum, segments and augmentation budget are required.");

        ulong totalSupply = 0UL;
        foreach (var seller in sellers)
        {
            if (seller is null || seller.SupplyQuanta == 0UL ||
                seller.TransportCostPerQuantumScaled is null ||
                seller.TransportCostPerQuantumScaled.Count != markets.Count)
                throw new ArgumentException(
                    "Every positive seller requires a cost or null for each real market.");
            totalSupply = checked(totalSupply + seller.SupplyQuanta);
            if (seller.TransportCostPerQuantumScaled.All(cost =>
                    !cost.HasValue))
                throw new InvalidOperationException(
                    "A producing seller has no physically reachable market.");
            if (seller.TransportCostPerQuantumScaled.Any(cost =>
                    cost.HasValue && cost.Value.Sign < 0))
                throw new ArgumentException(
                    "Physical freight and external-owner tariffs cannot be negative.");
        }
        checked { _ = totalSupply * goodsQuantum; }
        foreach (var market in markets)
            if (market is null || market.QuoteMicro == 0UL ||
                market.DemandMicro == 0UL)
                throw new ArgumentException(
                    "A real buyer needs positive quoted marginal scale and demand.");

        ulong denominator = checked((ulong)markets.Count *
            (ulong)segmentsPerAverageMarket);
        ulong width = totalSupply / denominator +
            (totalSupply % denominator == 0UL ? 0UL : 1UL);
        width = Math.Max(1UL, width);

        int sellerCount = sellers.Count;
        int marketCount = markets.Count;
        int source = 0, sellerStart = 1, marketStart = 1 + sellerCount;
        int sink = checked(marketStart + marketCount);
        var network = new Network(checked(sink + 1));
        var allocations = new Edge?[sellerCount, marketCount];
        var cheapest = new BigInteger?[marketCount];

        for (int i = 0; i < sellerCount; i++)
        {
            var seller = sellers[i];
            network.Add(source, sellerStart + i, seller.SupplyQuanta,
                BigInteger.Zero);
            for (int j = 0; j < marketCount; j++)
            {
                var cost = seller.TransportCostPerQuantumScaled[j];
                if (!cost.HasValue) continue;
                allocations[i, j] = network.Add(sellerStart + i,
                    marketStart + j, seller.SupplyQuanta, cost.Value);
                var currentCheapest = cheapest[j];
                if (!currentCheapest.HasValue ||
                    cost.Value < currentCheapest.Value)
                    cheapest[j] = cost.Value;
            }
        }

        var segments = new MarketSegments?[marketCount];
        for (int j = 0; j < marketCount; j++)
        {
            if (!cheapest[j].HasValue) continue;
            var segment = new MarketSegments(network, marketStart + j, sink,
                markets[j], goodsQuantum, totalSupply, width);
            segment.ActivateIfNeeded();
            segments[j] = segment;
        }

        // Initial reduced-cost potentials are exact shortest distances in
        // the initial acyclic source -> sellers -> buyers -> sink network.
        var potential = new BigInteger[sink + 1];
        BigInteger? sinkDistance = null;
        for (int j = 0; j < marketCount; j++)
        {
            var cheapestRoute = cheapest[j];
            if (!cheapestRoute.HasValue) continue;
            potential[marketStart + j] = cheapestRoute.Value;
            var first = segments[j]!.Latest!;
            var viaMarket = cheapestRoute.Value + first.Cost;
            if (!sinkDistance.HasValue || viaMarket < sinkDistance.Value)
                sinkDistance = viaMarket;
        }
        if (!sinkDistance.HasValue)
            throw new InvalidOperationException(
                "No real market can receive positive physical supply.");
        potential[sink] = sinkDistance.Value;

        // A residual augmentation changes only capacities and potentials;
        // graph vertex count and predecessor-workspace shape never change.
        // Reuse scratch storage within this one solver call, never across
        // workers, commodities, economic cycles or concurrent invocations.
        // The opt-out retains the allocation-per-augmentation reference path
        // for exact A/B verification of deliveries and integrated potential.
        BigInteger?[]? retainedDistance = reuseSearchWorkspace
            ? new BigInteger?[sink + 1] : null;
        int[]? retainedPreviousNode = reuseSearchWorkspace
            ? new int[sink + 1] : null;
        int[]? retainedPreviousEdge = reuseSearchWorkspace
            ? new int[sink + 1] : null;
        PriorityQueue<int, (BigInteger Distance, int Node)>? retainedQueue =
            reuseSearchWorkspace && !useIndexedHeap ? new() : null;
        // The optimized queue MUST use the retained distance array. The
        // legacy scratch opt-out remains independent for reference tests.
        BigInteger?[]? indexedDistance = useIndexedHeap
            ? retainedDistance ?? new BigInteger?[sink + 1] : null;
        IndexedResidualMinHeap? indexedQueue = useIndexedHeap
            ? new IndexedResidualMinHeap(sink + 1, indexedDistance!) : null;

        // Exact dual label: shifted[v] = reducedDistance[v] + potential[v].
        // A discarded relaxation needs only two BigInteger additions and
        // one exact comparison. Convert back to the SAME reduced key only
        // when a candidate strictly improves that vertex. No fixed-width
        // conversion, rounding or approximate numeric comparison is used.
        // The retained array is local to this Solve, not shared by players.
        BigInteger?[]? shifted = useShiftedLabels
            ? new BigInteger?[sink + 1] : null;

        profile?.End("residual-network-setup", setupStart);
        var searchStart = profile?.Start() ?? default;
        ulong shipped = 0UL;
        int augmentations = 0;
        BigInteger minimumCost = BigInteger.Zero;
        while (shipped != totalSupply)
        {
            if (augmentations >= maxAugmentations)
                throw new InvalidOperationException(
                    "Global segmented concave flow exceeded its explicit " +
                    "augmentation budget; no partial market candidate is returned.");

            var distance = indexedDistance ?? retainedDistance ??
                new BigInteger?[sink + 1];
            var previousNode = retainedPreviousNode ??
                Enumerable.Repeat(-1, sink + 1).ToArray();
            var previousEdge = retainedPreviousEdge ?? new int[sink + 1];
            var queue = useIndexedHeap ? null : retainedQueue ??
                new PriorityQueue<int, (BigInteger Distance, int Node)>();
            if (reuseSearchWorkspace || useIndexedHeap)
            {
                // A new augmentation MUST begin without the previous
                // distances, predecessor ties or queue state.
                Array.Clear(distance);
                if (reuseSearchWorkspace)
                {
                    Array.Fill(previousNode, -1);
                    Array.Clear(previousEdge);
                }
                if (useIndexedHeap)
                    indexedQueue!.Reset();
                else
                    queue!.Clear();
            }
            distance[source] = BigInteger.Zero;
            if (useShiftedLabels)
            {
                Array.Clear(shifted!);
                shifted![source] = potential[source];
            }
            if (useIndexedHeap)
                indexedQueue!.InsertOrDecrease(source);
            else
                queue!.Enqueue(source, (BigInteger.Zero, source));

            while (true)
            {
                int at;
                BigInteger settledDistance;
                if (useIndexedHeap)
                {
                    if (!indexedQueue!.TryPop(out at))
                        break;
                    settledDistance = distance[at]!.Value;
                }
                else
                {
                    if (!queue!.TryDequeue(out at, out var priority))
                        break;
                    if (!distance[at].HasValue ||
                        priority.Distance != distance[at]!.Value)
                        continue;
                    settledDistance = priority.Distance;
                }
                var edges = network.Links[at];
                for (int e = 0; e < edges.Count; e++)
                {
                    var edge = edges[e];
                    if (edge.Remaining == 0UL) continue;
                    BigInteger candidate;
                    if (useShiftedLabels)
                    {
                        // reducedCost=cost+potential[from]-potential[to]
                        // must remain nonnegative on every traversed arc.
                        // Comparing BEFORE subtracting avoids allocating
                        // an intermediate reduced-cost BigInteger.
                        // Cost >= 0 AND potential[from] >= potential[to]
                        // PROVES the exact reduced cost is nonnegative.
                        // The guarded fast path avoids constructing the
                        // often very large temporary BigInteger sum. The
                        // prior exact check remains the A/B oracle for all
                        // other cases and when the fast path is disabled.
                        bool nonnegativeCost = edge.Cost.Sign >= 0;
                        bool invariantProven = useResidualDominanceFastPath &&
                            nonnegativeCost &&
                            potential[at] >= potential[edge.To];
                        if (!invariantProven)
                        {
                            BigInteger upper = potential[at] + edge.Cost;
                            if (upper < potential[edge.To])
                                throw new InvalidOperationException(
                                    "Residual reduced-cost invariant violated.");
                        }

                        // A nonnegative cost cannot improve a destination
                        // whose shifted label is already <= that of its
                        // source. This exact dominance test avoids a second
                        // per-edge BigInteger sum for the common reject.
                        // Crucially, the invariant above is still checked
                        // first for EVERY residual edge with capacity.
                        BigInteger sourceShifted = shifted![at]!.Value;
                        BigInteger? targetShifted = shifted[edge.To];
                        if (useResidualDominanceFastPath &&
                            nonnegativeCost && targetShifted.HasValue &&
                            sourceShifted >= targetShifted.Value)
                            continue;

                        // Addition by zero also needs no BigInteger math.
                        // Strict comparisons and the indexed-heap keys
                        // remain exactly equivalent to the old expression.
                        BigInteger shiftedCandidate =
                            useResidualDominanceFastPath && edge.Cost.IsZero
                                ? sourceShifted : sourceShifted + edge.Cost;
                        if (targetShifted.HasValue &&
                            shiftedCandidate >= targetShifted.Value)
                            continue;
                        shifted[edge.To] = shiftedCandidate;
                        candidate = shiftedCandidate - potential[edge.To];
                    }
                    else
                    {
                        // Original exact BigInteger expression is retained
                        // as an independent reference for strict A/B tests.
                        var reduced = edge.Cost + potential[at] -
                            potential[edge.To];
                        if (reduced.Sign < 0)
                            throw new InvalidOperationException(
                                "Residual reduced-cost invariant violated.");
                        candidate = settledDistance + reduced;
                        if (distance[edge.To].HasValue &&
                            candidate >= distance[edge.To]!.Value)
                            continue;
                    }
                    distance[edge.To] = candidate;
                    previousNode[edge.To] = at;
                    previousEdge[edge.To] = e;
                    if (useIndexedHeap)
                        indexedQueue!.InsertOrDecrease(edge.To);
                    else
                        queue!.Enqueue(edge.To, (candidate, edge.To));
                }
            }

            if (!distance[sink].HasValue)
                throw new InvalidOperationException(
                    "Declared real routes cannot conserve all physical output.");
            for (int v = 0; v <= sink; v++)
                if (distance[v].HasValue)
                    potential[v] += distance[v]!.Value;

            ulong send = totalSupply - shipped;
            for (int at = sink; at != source; at = previousNode[at])
            {
                int from = previousNode[at];
                if (from < 0)
                    throw new InvalidOperationException("Broken residual augmenting path.");
                send = Math.Min(send,
                    network.Links[from][previousEdge[at]].Remaining);
            }
            if (send == 0UL)
                throw new InvalidOperationException("Zero-volume flow augmentation.");
            for (int at = sink; at != source; at = previousNode[at])
            {
                int from = previousNode[at];
                var edge = network.Links[from][previousEdge[at]];
                var reverse = network.Links[edge.To][edge.ReverseIndex];
                edge.Remaining -= send;
                reverse.Remaining = checked(reverse.Remaining + send);
                minimumCost += edge.Cost * send;
            }
            shipped = checked(shipped + send);
            augmentations = checked(augmentations + 1);
            foreach (var segment in segments)
                segment?.ActivateIfNeeded();
        }

        profile?.End("residual-augmenting-search", searchStart);
        var reconciliationStart = profile?.Start() ?? default;
        var matrix = new ulong[sellerCount][];
        var marketTotals = new ulong[marketCount];
        BigInteger transport = BigInteger.Zero;
        for (int i = 0; i < sellerCount; i++)
        {
            matrix[i] = new ulong[marketCount];
            ulong row = 0UL;
            for (int j = 0; j < marketCount; j++)
            {
                var forward = allocations[i, j];
                if (forward is null) continue;
                ulong quantity = sellers[i].SupplyQuanta - forward.Remaining;
                matrix[i][j] = quantity;
                row = checked(row + quantity);
                marketTotals[j] = checked(marketTotals[j] + quantity);
                transport += sellers[i].TransportCostPerQuantumScaled[j]!.Value *
                    quantity;
            }
            if (row != sellers[i].SupplyQuanta)
                throw new InvalidOperationException(
                    "Global flow does not conserve an individual seller lot.");
        }
        if (marketTotals.Aggregate(BigInteger.Zero,
                (sum, n) => sum + n) != totalSupply)
            throw new InvalidOperationException(
                "Global flow does not conserve all producing workers.");

        BigInteger integrated = -transport;
        for (int j = 0; j < marketCount; j++)
            integrated += FoundationPhysicalMarginalIntegratedUtility
                .MarketBenefitScaled(markets[j].QuoteMicro,
                    markets[j].DemandMicro,
                    checked(marketTotals[j] * goodsQuantum));
        BigInteger segmented = -minimumCost;
        if (integrated < segmented)
            throw new InvalidOperationException(
                "The declared piecewise chord must understate exact integrated utility.");

        profile?.End("residual-conservation-and-utility", reconciliationStart);
        return new Result(matrix, marketTotals, augmentations,
            segmented, integrated);
    }
}
