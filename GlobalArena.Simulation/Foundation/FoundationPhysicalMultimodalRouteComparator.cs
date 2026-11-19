using System.Numerics;
using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R55 RESEARCH-ONLY. Two real LAND endpoints, one of two alternatives:
/// a wholly terrestrial path, or land -> one coast transfer -> an
/// uninterrupted natural sea leg -> second coast transfer -> land.
///
/// The opening-cycle TRANSPORT SNAPSHOT is the only capital source.
/// No current freight, sales or market prices determine edge costs.
/// The sea discount is applied ONCE to the complete maritime leg,
/// with the actual embarkation/disembarkation edges as parameters.
/// This is not yet the authoritative marginal-market pathfinder.
/// </summary>
public static class FoundationPhysicalMultimodalRouteComparator
{
    /// <summary>R60 research alternatives. OnDemandArc uses the R56/R57
    /// per-terminal distances; EmbarkationState carries the origin terminal
    /// through a single shared multi-label maritime priority queue.</summary>
    public enum MaritimeResearchArchitecture
    {
        OnDemandArc = 0,
        EmbarkationState = 1
    }

    /// <summary>
    /// R65 observational timings only. Not part of the game state, route
    /// price, deterministic ordering or any cache. Counts are exact, timings
    /// are non-deterministic and suitable solely for aggregate profiling.
    /// </summary>
    public sealed record ResearchPhaseTelemetry(
        double PreparationMs, double LandSearchMs, double BoundMs,
        double SeaSearchMs, double PairEvaluationMs,
        long EvaluatedPairSlots, long SeaQuotes,
        long PotentialPairs, long ProvenExcludedPairs);

    public sealed record Result(
        ulong? LandOnlyRateMicro,
        ulong? OneSeaLegRateMicro,
        PhysicalTacticalEdgeId? EmbarkEdge,
        PhysicalTacticalEdgeId? DisembarkEdge,
        int ExploredSeaOrigins,
        int EligibleEmbarkations,
        int EligibleDisembarkations,
        long ExpandedSeaTiles = 0L,
        long PotentialMaritimePairs = 0L,
        long ProvenExcludedMaritimePairs = 0L)
    {
        public bool SeaRoutePreferred =>
            OneSeaLegRateMicro.HasValue &&
            (!LandOnlyRateMicro.HasValue ||
             OneSeaLegRateMicro.Value < LandOnlyRateMicro.Value);
    }

    /// <summary>
    /// Explicit caller-owned, bounded reuse of COMPLETE sea distances
    /// across comparisons sharing the SAME physical sea graph. Sea-sea
    /// edges remain natural throughout the game, so committed LAND/COAST
    /// investments do not invalidate these distances. The full canonical
    /// sea-edge inventory is checked on EVERY use; unrelated worlds,
    /// mutable sea rates or corrupted topology fail closed.
    ///
    /// Not a global/static cache and never stores commodity, market,
    /// congestion, tariff or current-turn state.
    /// </summary>
    public sealed class FrozenSeaDistanceCache
    {
        private readonly int _maximumEntries;
        private readonly Dictionary<ulong, ulong[]> _distances = new();
        private (ulong Id, ulong A, ulong B, ulong Rate)[]? _seaSignature;
        private WorldGenerationResult? _world;
        private PhysicalTacticalIncidenceMap? _incidence;
        private long _entryCount;

        public FrozenSeaDistanceCache(int maximumDistanceEntries = 4_000_000)
        {
            if (maximumDistanceEntries < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDistanceEntries));
            _maximumEntries = maximumDistanceEntries;
        }

        public long DistanceEntries => _entryCount;
        public int CachedSeaOrigins => _distances.Count;
        public long Hits { get; private set; }
        public long Misses { get; private set; }

        internal void Validate(
            WorldGenerationResult world,
            PhysicalTacticalIncidenceMap incidence,
            IReadOnlyList<(ulong Id, ulong A, ulong B, ulong Rate)> sea)
        {
            if (_seaSignature is null)
            {
                _world = world;
                _incidence = incidence;
                _seaSignature = sea.ToArray();
            }
            else if (!ReferenceEquals(_world, world) ||
                     !ReferenceEquals(_incidence, incidence) ||
                     !_seaSignature.SequenceEqual(sea))
                throw new InvalidOperationException(
                    "Frozen sea-distance cache cannot be reused with a " +
                    "different world, topology or maritime operating rate.");
        }

        internal ulong[] GetOrCompute(
            ulong source, Dictionary<ulong, List<Link>> graph,
            int count, out bool computed, out long explored)
        {
            if (_distances.TryGetValue(source, out var cached))
            {
                Hits++;
                computed = false;
                explored = 0L;
                return cached;
            }
            Misses++;
            computed = true;
            // Reusable results must settle the COMPLETE sea component.
            // A target-short-circuited search is not safe to share with
            // another pair of settlements requesting different targets.
            ulong[] distances = ShortestSea(
                source, graph, count, null, out explored);
            if (_entryCount + distances.LongLength <= _maximumEntries)
            {
                _distances.Add(source, distances);
                _entryCount += distances.LongLength;
            }
            // At budget exhaustion correctness is preserved: the result
            // is returned but never retained. No silent approximate path.
            return distances;
        }
    }

    internal sealed record Link(ulong Other, ulong Rate);
    private sealed record Coast(
        PhysicalTransportEdgeRuntimeRecord Record,
        ulong LandTile, ulong SeaTile, ulong Rate);

    /// <summary>
    /// Same investment rule for all finite land and coast edges:
    /// C0_effective = ceil(C0_natural / sqrt(1 + I/B)).
    /// Diminishing percentage benefit; quote stays positive. Exact
    /// BigInteger integer-sqrt fixed-point maths, no floating-point drift.
    /// This intensity is PROVISIONAL, not canonical balancing.
    /// </summary>
    public static ulong OperationalCost(
        PhysicalTransportEdgeRuntimeRecord edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ulong natural = edge.FreePhysicalCostMicroPerWholeCargoUnit;
        if (natural == 0UL)
            throw new InvalidOperationException(
                "Natural physical rate must remain positive.");
        if (edge.CargoMicroAtReferenceLoad == ulong.MaxValue)
        {
            if (edge.CommittedInvestmentMoneyMicro.GetValueOrDefault() != 0UL ||
                edge.OwnershipKind != PhysicalTransportOwnershipKind.NoInfrastructure)
                throw new InvalidOperationException(
                    "Open sea has no infrastructure.");
            return natural;
        }
        ulong invested = edge.CommittedInvestmentMoneyMicro ?? 0UL;
        if (invested == 0UL) return natural;
        ulong cost = edge.NaturalConstructionCostMoneyMicro
            ?? throw new InvalidOperationException(
                "Invested edge requires its real positive construction quote.");
        if (cost == 0UL)
            throw new InvalidOperationException(
                "Invested edge has invalid reference money.");
        BigInteger scale = new(1_000_000_000UL);
        BigInteger normalizedRoot = IntegerSquareRoot(
            scale * scale * ((BigInteger)cost + invested) / cost);
        BigInteger numerator = (BigInteger)natural * scale;
        return checked((ulong)((numerator + normalizedRoot - 1) /
            normalizedRoot));
    }

    public static Result Compare(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        PhysicalTransportInfrastructureRuntimeState transport,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        int maxEligibleEmbarkations = 128,
        bool useOptimizedSeaSearch = true,
        FrozenSeaDistanceCache? frozenSeaDistances = null,
        MaritimeResearchArchitecture architecture = MaritimeResearchArchitecture.OnDemandArc,
        bool useProvenLandBound = false,
        Action<ResearchPhaseTelemetry>? reportPhases = null)
    {
        long tickStart = reportPhases is null ? 0L :
            Stopwatch.GetTimestamp();
        long tickGraph = tickStart, tickLand = tickStart;
        long tickBound = tickStart, tickSea = tickStart;
        long countedPairSlots = 0L, countedQuotes = 0L;
        static double Milliseconds(long from, long to) =>
            1000.0 * (to - from) / Stopwatch.Frequency;
        void Emit(long tickPairs, long possible, long removed)
        {
            if (reportPhases is null) return;
            reportPhases(new ResearchPhaseTelemetry(
                Milliseconds(tickStart, tickGraph),
                Milliseconds(tickGraph, tickLand),
                Milliseconds(tickLand, tickBound),
                Milliseconds(tickBound, tickSea),
                Milliseconds(tickSea, tickPairs),
                countedPairSlots, countedQuotes, possible, removed));
        }
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(transport);
        if (!transport.IsInitialized ||
            incidence.Refinement.CoarseParameters !=
                world.Request.StrategicParameters ||
            source.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            destination.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            maxEligibleEmbarkations < 1)
            throw new ArgumentException(
                "R55 comparison needs one bound world, its complete real " +
                "transport inventory and a positive exploration budget.");

        int count = incidence.FineTopology.Cells.Count;
        if (!source.IsValid || !destination.IsValid ||
            source.FineStrategicCellId.Value > (ulong)count ||
            destination.FineStrategicCellId.Value > (ulong)count)
            throw new ArgumentException("Invalid source or destination tile.");
        var elevation = world.StrategicPhysicalFields.Elevation;
        long sea = world.StrategicPhysicalFields.SeaLevel.RawValue;
        var isLand = incidence.TileIncidences.Select(tile =>
        {
            long sum = 0;
            foreach (var id in tile.IncidentCoarseCellIds)
                sum = checked(sum + elevation.GetRawValue(id));
            return sum / tile.IncidentCoarseCellIds.Count > sea;
        }).ToArray();
        ulong s = source.FineStrategicCellId.Value;
        ulong d = destination.FineStrategicCellId.Value;
        if (!isLand[checked((int)s - 1)] ||
            !isLand[checked((int)d - 1)] || s == d)
            throw new ArgumentException(
                "R55 compares two distinct physical LAND settlements.");

        var records = transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        if (records.Count != incidence.FineTopology.Edges.Count)
            throw new InvalidOperationException(
                "Route comparison must not silently invent missing roads.");
        var landGraph = new Dictionary<ulong, List<Link>>();
        var seaGraph = new Dictionary<ulong, List<Link>>();
        var coast = new List<Coast>();
        var seaSignature = new List<
            (ulong Id, ulong A, ulong B, ulong Rate)>();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            if (!records.TryGetValue(edge.Id, out var record))
                throw new InvalidOperationException(
                    "Canonical transport inventory is missing a real edge.");
            var a = edge.IncidentCellIds[0].Value;
            var b = edge.IncidentCellIds[1].Value;
            bool la = isLand[checked((int)a - 1)];
            bool lb = isLand[checked((int)b - 1)];
            ulong rate = OperationalCost(record);
            if (la && lb)
            {
                if (record.CargoMicroAtReferenceLoad == ulong.MaxValue)
                    throw new InvalidOperationException(
                        "Terrestrial edge cannot claim sea capacity.");
                Add(landGraph, a, b, rate);
            }
            else if (!la && !lb)
            {
                if (record.CargoMicroAtReferenceLoad != ulong.MaxValue)
                    throw new InvalidOperationException(
                        "Sea edge requires naturally unbounded capacity.");
                Add(seaGraph, a, b, rate);
                seaSignature.Add((edge.Id.Value, Math.Min(a, b),
                    Math.Max(a, b), rate));
            }
            else
            {
                if (record.CargoMicroAtReferenceLoad == ulong.MaxValue)
                    throw new InvalidOperationException(
                        "Coastal transfer requires finite capacity.");
                coast.Add(new Coast(record,
                    la ? a : b, la ? b : a, rate));
            }
        }
        if (!Enum.IsDefined(architecture))
            throw new ArgumentOutOfRangeException(nameof(architecture));
        if (architecture == MaritimeResearchArchitecture.EmbarkationState &&
            (!useOptimizedSeaSearch || frozenSeaDistances is not null))
            throw new ArgumentException(
                "State-based research requires no R57 arc distance cache.");
        if (frozenSeaDistances is not null)
        {
            if (!useOptimizedSeaSearch)
                throw new ArgumentException(
                    "Frozen sea distances require optimized mode.");
            frozenSeaDistances.Validate(world, incidence, seaSignature);
        }
        if (reportPhases is not null) tickGraph = Stopwatch.GetTimestamp();
        var fromSource = Shortest(s, landGraph, count);
        var toDestination = Shortest(d, landGraph, count);
        ulong? landOnly = fromSource[checked((int)d)] == ulong.MaxValue
            ? null : fromSource[checked((int)d)];
        var entrances = coast.Where(x =>
            fromSource[checked((int)x.LandTile)] != ulong.MaxValue)
            .OrderBy(x => x.Record.Edge.FineStrategicEdgeId.Value).ToArray();
        var exits = coast.Where(x =>
            toDestination[checked((int)x.LandTile)] != ulong.MaxValue)
            .OrderBy(x => x.Record.Edge.FineStrategicEdgeId.Value).ToArray();
        if (entrances.Length > maxEligibleEmbarkations)
            throw new InvalidOperationException(
                "Unbounded port-pair discovery would exceed the explicit " +
                "R55 research budget; no silently truncated optimum.");

        if (reportPhases is not null) tickLand = Stopwatch.GetTimestamp();
        long potentialPairs = checked((long)entrances.Length * exits.Length);
        long safelyExcluded = 0L;

        // R62 RESEARCH-ONLY: the best land-only route is a feasible upper
        // bound KNOWN BEFORE any maritime search. For one REAL sea leg,
        // every sea quote is >= 1 micro, so:
        //   landToA + coastA + 1 + coastB + landFromB <= U
        // is a NECESSARY condition for a maritime path to compete.
        // Equality must be kept to preserve canonical sea ties.
        //
        // This pass only examines cheap terrestrial/costal arithmetic;
        // it does not request maritime distances or modify edge costs.
        // It also removes ALL sea searches when land already dominates
        // every possible terminal pair, rather than merely skipping Quote.
        if (useProvenLandBound && landOnly is ulong feasibleLandUpper)
        {
            var viableEmbark = new HashSet<PhysicalTacticalEdgeId>();
            var viableDisembark = new HashSet<PhysicalTacticalEdgeId>();
            foreach (var a in entrances)
            {
                UInt128 first = (UInt128)fromSource[
                    checked((int)a.LandTile)] + a.Rate + 1UL;
                foreach (var b in exits)
                {
                    UInt128 floor = first +
                        toDestination[checked((int)b.LandTile)] + b.Rate;
                    if (floor > feasibleLandUpper)
                    {
                        safelyExcluded++;
                        continue;
                    }
                    viableEmbark.Add(a.Record.Edge);
                    viableDisembark.Add(b.Record.Edge);
                }
            }
            entrances = entrances.Where(a =>
                viableEmbark.Contains(a.Record.Edge)).ToArray();
            exits = exits.Where(b =>
                viableDisembark.Contains(b.Record.Edge)).ToArray();
            if (entrances.Length == 0 || exits.Length == 0)
            {
                if (reportPhases is not null)
                {
                    tickBound = Stopwatch.GetTimestamp();
                    tickSea = tickBound;
                    Emit(tickBound, potentialPairs, safelyExcluded);
                }
                return new Result(landOnly, null, null, null, 0,
                    0, 0, 0L, potentialPairs, safelyExcluded);
            }
        }

        if (reportPhases is not null) tickBound = Stopwatch.GetTimestamp();
        ulong? seaBest = null;
        PhysicalTacticalEdgeId? embark = null, disembark = null;
        int explored = 0;
        long expandedSeaTiles = 0L;

        // The sea graph is undirected and the R54 terminal-pair discount
        // is symmetric. Therefore computing distances from the smaller set
        // of DISTINCT water tiles is exact. Multiple coast crossings can
        // share the same water tile: one solve serves all of those ports.
        // Dijkstra can also stop when every relevant destination water
        // tile is settled. Every result is still tested in the ORIGINAL
        // ordered port-pair traversal below, preserving tie-breaking.
        ulong[] entryTiles = entrances.Select(p => p.SeaTile)
            .Distinct().OrderBy(x => x).ToArray();
        ulong[] exitTiles = exits.Select(p => p.SeaTile)
            .Distinct().OrderBy(x => x).ToArray();
        bool searchFromExits = useOptimizedSeaSearch &&
            exitTiles.Length < entryTiles.Length;
        ulong[] searchTiles = searchFromExits ? exitTiles : entryTiles;
        ulong[] targetTiles = searchFromExits ? entryTiles : exitTiles;
        var cache = new Dictionary<ulong, ulong[]>();
        IReadOnlySet<ulong>? targets = useOptimizedSeaSearch
            ? targetTiles.ToHashSet() : null;
        if (architecture == MaritimeResearchArchitecture.EmbarkationState)
        {
            // Alternative B of R59: one shared queue of (embarkation tile,
            // current sea tile) states. Each origin label is preserved:
            // merging only by current tile would be mathematically wrong
            // because disembarkation discounts depend on the embarkation.
            // The sea graph remains natural and snapshot-frozen.
            cache = InterleavedTerminalStateDistances(
                searchTiles, targetTiles, seaGraph, count,
                maxEligibleEmbarkations, out explored,
                out expandedSeaTiles);
        }
        else if (useOptimizedSeaSearch)
        {
            foreach (ulong tile in searchTiles)
            {
                ulong[] distances;
                long visited;
                bool computed;
                if (frozenSeaDistances is null)
                {
                    distances = ShortestSea(tile, seaGraph, count,
                        targets!, out visited);
                    computed = true;
                }
                else
                    distances = frozenSeaDistances.GetOrCompute(tile,
                        seaGraph, count, out computed, out visited);
                cache.Add(tile, distances);
                if (computed) explored++;
                expandedSeaTiles = checked(expandedSeaTiles + visited);
            }
        }
        if (reportPhases is not null) tickSea = Stopwatch.GetTimestamp();
        foreach (var portA in entrances)
        {
            // R55 baseline remains selectable for an independent numerical
            // A/B. Never cache a result from another transport snapshot:
            // all graph and investment-derived costs belong to THIS call.
            ulong[]? baselineDistances = null;
            if (!useOptimizedSeaSearch)
            {
                baselineDistances = ShortestSea(portA.SeaTile,
                    seaGraph, count, null, out long visited);
                explored++;
                expandedSeaTiles = checked(expandedSeaTiles + visited);
            }
            foreach (var portB in exits)
            {
                if (reportPhases is not null) countedPairSlots++;
                if (useProvenLandBound && landOnly is ulong landLimit)
                {
                    UInt128 cheapFloor =
                        (UInt128)fromSource[
                            checked((int)portA.LandTile)] + portA.Rate +
                        1UL + portB.Rate +
                        toDestination[checked((int)portB.LandTile)];
                    if (cheapFloor > landLimit)
                        continue;
                }
                ulong[] seaDistances = useOptimizedSeaSearch
                    ? cache[searchFromExits
                        ? portB.SeaTile : portA.SeaTile]
                    : baselineDistances!;
                ulong target = searchFromExits
                    ? portA.SeaTile : portB.SeaTile;
                ulong seaRate = seaDistances[checked((int)target)];
                if (portA.Record.Edge == portB.Record.Edge ||
                    seaRate is 0UL or ulong.MaxValue)
                    continue;
                // The R54 discount is applied ONCE to the entire sea leg.
                // Never modify the underlying sea-sea edge inventory.
                if (reportPhases is not null) countedQuotes++;
                ulong seaQuote = FoundationPhysicalPairedCoastalSeaLegQuote
                    .Quote(seaRate, portA.Record, portB.Record);
                ulong candidate = checked(
                    fromSource[checked((int)portA.LandTile)] +
                    portA.Rate + seaQuote + portB.Rate +
                    toDestination[checked((int)portB.LandTile)]);
                if (!seaBest.HasValue || candidate < seaBest.Value ||
                    candidate == seaBest.Value &&
                    (portA.Record.Edge.FineStrategicEdgeId.Value <
                     embark!.Value.FineStrategicEdgeId.Value ||
                     portA.Record.Edge.FineStrategicEdgeId.Value ==
                     embark!.Value.FineStrategicEdgeId.Value &&
                     portB.Record.Edge.FineStrategicEdgeId.Value <
                     disembark!.Value.FineStrategicEdgeId.Value))
                {
                    seaBest = candidate;
                    embark = portA.Record.Edge;
                    disembark = portB.Record.Edge;
                }
            }
        }
        if (reportPhases is not null)
            Emit(Stopwatch.GetTimestamp(), potentialPairs, safelyExcluded);
        return new Result(landOnly, seaBest, embark, disembark,
            explored, entrances.Length, exits.Length, expandedSeaTiles,
            potentialPairs, safelyExcluded);
    }

    /// <summary>
    /// R60 architectural spike B. A single interleaved Dijkstra over
    /// (embarkation, current sea tile) preserves the actual terminal identity.
    /// It is exact for one contiguous maritime leg, but may require
    /// O(ports * sea tiles) labels. No heuristic dominance or truncation.
    /// Unlike the R57 atlas, this approach does NOT persist between calls.
    /// </summary>
    private static Dictionary<ulong, ulong[]> InterleavedTerminalStateDistances(
        ulong[] origins, ulong[] targetTiles,
        Dictionary<ulong, List<Link>> graph, int count,
        int maximumOrigins, out int searchedOrigins, out long expandedStates)
    {
        if (origins.Length > maximumOrigins)
            throw new InvalidOperationException(
                "Research state search exceeds its explicit terminal budget.");
        searchedOrigins = origins.Length;
        expandedStates = 0L;
        var results = new Dictionary<ulong, ulong[]>(origins.Length);
        var targetSet = targetTiles.ToHashSet();
        var remaining = new int[origins.Length];
        var queue = new PriorityQueue<(int OriginIndex, ulong Tile),
            (ulong Rate, ulong Origin, ulong Tile)>();
        for (int i = 0; i < origins.Length; i++)
        {
            ulong[] distances = Enumerable.Repeat(ulong.MaxValue, count + 1)
                .ToArray();
            distances[checked((int)origins[i])] = 0UL;
            results.Add(origins[i], distances);
            remaining[i] = targetSet.Count;
            if (remaining[i] > 0)
                queue.Enqueue((i, origins[i]), (0UL, origins[i], origins[i]));
        }
        while (queue.TryDequeue(out var state, out var priority))
        {
            var distances = results[origins[state.OriginIndex]];
            if (distances[checked((int)state.Tile)] != priority.Rate ||
                remaining[state.OriginIndex] == 0)
                continue;
            expandedStates++;
            if (targetSet.Contains(state.Tile) &&
                --remaining[state.OriginIndex] == 0)
                continue;
            if (!graph.TryGetValue(state.Tile, out var links))
                continue;
            foreach (var edge in links)
            {
                ulong proposal = checked(priority.Rate + edge.Rate);
                int index = checked((int)edge.Other);
                if (proposal >= distances[index])
                    continue;
                distances[index] = proposal;
                queue.Enqueue((state.OriginIndex, edge.Other),
                    (proposal, origins[state.OriginIndex], edge.Other));
            }
        }
        return results;
    }

    /// <summary>
    /// Exact positive-weight Dijkstra. With a target set, every necessary
    /// target distance is final when its tile is dequeued at the minimum
    /// priority. Stop only after ALL target tiles have been settled.
    /// Unreachable target tiles correctly force full component exploration.
    /// Empty target sets require no sea work.
    /// </summary>
    private static ulong[] ShortestSea(
        ulong start, Dictionary<ulong, List<Link>> graph, int count,
        IReadOnlySet<ulong>? targets, out long explored)
    {
        explored = 0L;
        var distance = Enumerable.Repeat(ulong.MaxValue, count + 1)
            .ToArray();
        distance[checked((int)start)] = 0UL;
        if (targets is { Count: 0 })
            return distance;
        int remaining = targets?.Count ?? 0;
        var queue = new PriorityQueue<ulong, (ulong Cost, ulong Tile)>();
        queue.Enqueue(start, (0UL, start));
        while (queue.TryDequeue(out ulong at, out var priority))
        {
            if (priority.Cost != distance[checked((int)at)])
                continue;
            explored++;
            if (targets is not null && targets.Contains(at) &&
                --remaining == 0)
                break;
            if (!graph.TryGetValue(at, out var links))
                continue;
            foreach (var link in links)
            {
                ulong next = checked(priority.Cost + link.Rate);
                if (next >= distance[checked((int)link.Other)])
                    continue;
                distance[checked((int)link.Other)] = next;
                queue.Enqueue(link.Other, (next, link.Other));
            }
        }
        return distance;
    }

    private static void Add(Dictionary<ulong, List<Link>> graph,
        ulong a, ulong b, ulong cost)
    {
        if (!graph.TryGetValue(a, out var left))
            graph[a] = left = new();
        if (!graph.TryGetValue(b, out var right))
            graph[b] = right = new();
        left.Add(new Link(b, cost));
        right.Add(new Link(a, cost));
    }

    private static ulong[] Shortest(
        ulong start, Dictionary<ulong, List<Link>> graph, int count)
    {
        var distance = Enumerable.Repeat(ulong.MaxValue, count + 1)
            .ToArray();
        distance[checked((int)start)] = 0UL;
        var queue =
            new PriorityQueue<ulong, (ulong Cost, ulong Tile)>();
        queue.Enqueue(start, (0UL, start));
        while (queue.TryDequeue(out ulong at, out var priority))
        {
            if (priority.Cost != distance[checked((int)at)] ||
                !graph.TryGetValue(at, out var links))
                continue;
            foreach (var link in links)
            {
                ulong next = checked(priority.Cost + link.Rate);
                if (next >= distance[checked((int)link.Other)]) continue;
                distance[checked((int)link.Other)] = next;
                queue.Enqueue(link.Other, (next, link.Other));
            }
        }
        return distance;
    }

    private static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value.IsZero)
            return BigInteger.Zero;
        BigInteger x = BigInteger.One <<
            ((checked((int)value.GetBitLength()) + 1) / 2);
        while (true)
        {
            BigInteger next = (x + value / x) >> 1;
            if (next >= x) return x;
            x = next;
        }
    }
}
