using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R61 RESEARCH-ONLY structural diagnostic. It does not pick a path, prune
/// the canonical routing algorithm or alter economic settlement.
///
/// For any valid one-sea-leg route from land to land:
///   total >= land(source, embark) + coast(embark)
///           + 1 + coast(disembark) + land(disembark, destination).
/// The "+1" is mathematically sound because natural sea edges have a
/// positive base cost and the once-per-leg R54 quote is a positive integer.
/// A pair is UNABLE to improve an already-known feasible upper bound U
/// only when this lower bound is STRICTLY GREATER than U.
/// Equality must remain eligible to preserve canonical tie-breaking.
///
/// Report counts using a sorted destination-side cost vector, WITHOUT
/// building the P x P candidate matrix or calculating any sea distances.
/// No unproved heuristic distance, mode bonus or implicit port object.
/// </summary>
public static class FoundationPhysicalMaritimePairLowerBoundAudit
{
    public sealed record Evidence(
        ulong UpperBound,
        long PotentialPairs,
        long ProvablyExcludedPairs,
        long RemainingPairs,
        int ReachableEmbarkations,
        int ReachableDisembarkations,
        bool GlobalWinnerPreserved)
    {
        public double ExcludedPercent => PotentialPairs == 0L ? 0.0 :
            100.0 * ProvablyExcludedPairs / PotentialPairs;
    }

    private sealed record CoastalEnd(
        PhysicalTacticalEdgeId Edge, ulong LandTile,
        ulong LandApproachPlusTransfer);

    public static Evidence Analyze(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        PhysicalTransportInfrastructureRuntimeState transport,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to,
        FoundationPhysicalMultimodalRouteComparator.Result oracle)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(oracle);
        if (!transport.IsInitialized ||
            incidence.Refinement.CoarseParameters !=
                world.Request.StrategicParameters ||
            from.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            to.FineGoldbergParameters != incidence.Refinement.FineParameters)
            throw new ArgumentException(
                "Physical lower bound must use the matching authoritative graph.");

        int n = incidence.FineTopology.Cells.Count;
        if (!from.IsValid || !to.IsValid ||
            from.FineStrategicCellId.Value > (ulong)n ||
            to.FineStrategicCellId.Value > (ulong)n ||
            from == to)
            throw new ArgumentException("Both settlements must be distinct valid tiles.");

        var isLand = incidence.TileIncidences.Select(tile =>
        {
            long sum = 0L;
            foreach (var cell in tile.IncidentCoarseCellIds)
                sum = checked(sum +
                    world.StrategicPhysicalFields.Elevation.GetRawValue(cell));
            return sum / tile.IncidentCoarseCellIds.Count >
                world.StrategicPhysicalFields.SeaLevel.RawValue;
        }).ToArray();
        if (!isLand[checked((int)from.FineStrategicCellId.Value - 1)] ||
            !isLand[checked((int)to.FineStrategicCellId.Value - 1)])
            throw new ArgumentException("Both settlements must be terrestrial.");

        var records = transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        if (records.Count != incidence.FineTopology.Edges.Count)
            throw new InvalidOperationException("The physical inventory is incomplete.");

        var graph = new Dictionary<ulong, List<(ulong Next, ulong Cost)>>();
        var coastal = new List<(PhysicalTacticalEdgeId Edge,
            ulong LandTile, ulong Cost)>();
        static void Add(
            Dictionary<ulong, List<(ulong Next, ulong Cost)>> network,
            ulong a, ulong b, ulong cost)
        {
            if (!network.TryGetValue(a, out var left))
                network[a] = left = new();
            if (!network.TryGetValue(b, out var right))
                network[b] = right = new();
            left.Add((b, cost));
            right.Add((a, cost));
        }
        foreach (var edge in incidence.FineTopology.Edges)
        {
            if (!records.TryGetValue(edge.Id, out var record))
                throw new InvalidOperationException("A real crossing is missing.");
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            bool la = isLand[checked((int)a - 1)];
            bool lb = isLand[checked((int)b - 1)];
            if (la && lb)
                Add(graph, a, b,
                    FoundationPhysicalMultimodalRouteComparator
                        .OperationalCost(record));
            else if (la != lb)
                coastal.Add((record.Edge, la ? a : b,
                    FoundationPhysicalMultimodalRouteComparator
                        .OperationalCost(record)));
        }

        ulong[] sourceDistance = Distances(
            from.FineStrategicCellId.Value, graph, n);
        ulong[] destinationDistance = Distances(
            to.FineStrategicCellId.Value, graph, n);

        var entries = coastal.Where(c =>
            sourceDistance[checked((int)c.LandTile)] != ulong.MaxValue)
            .Select(c => new CoastalEnd(c.Edge, c.LandTile,
                checked(sourceDistance[checked((int)c.LandTile)] +
                    c.Cost))).ToArray();
        var exits = coastal.Where(c =>
            destinationDistance[checked((int)c.LandTile)] != ulong.MaxValue)
            .Select(c => new CoastalEnd(c.Edge, c.LandTile,
                checked(destinationDistance[checked((int)c.LandTile)] +
                    c.Cost))).ToArray();

        // The exact oracle is used ONLY to provide an observed feasible
        // upper bound, not to choose the pairs or supply any distances.
        ulong? upper = oracle.LandOnlyRateMicro;
        if (oracle.OneSeaLegRateMicro is ulong sea &&
            (!upper.HasValue || sea < upper.Value))
            upper = sea;
        if (!upper.HasValue)
            throw new InvalidOperationException(
                "A safe pruning budget needs one independently proven feasible route.");
        ulong[] sortedExits = exits
            .Select(x => x.LandApproachPlusTransfer)
            .OrderBy(x => x).ToArray();

        long retained = 0L;
        foreach (var entry in entries)
        {
            // For ANY allowed return port, sea quote >= 1. Work in UInt128
            // to avoid wrapping an otherwise correct lower bound.
            UInt128 entryFloor =
                (UInt128)entry.LandApproachPlusTransfer + 1UL;
            if (entryFloor > upper.Value)
                continue;
            ulong ceilingForExit =
                checked((ulong)((UInt128)upper.Value - entryFloor));
            // upper_bound(sortedExits, ceilingForExit): equality must
            // remain to allow a deterministic tie.
            int low = 0, high = sortedExits.Length;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (sortedExits[mid] <= ceilingForExit)
                    low = mid + 1;
                else
                    high = mid;
            }
            retained = checked(retained + low);
        }
        long potential = checked((long)entries.Length * exits.Length);
        long excluded = checked(potential - retained);

        // Only verify the reported WINNING maritime pair when the oracle
        // actually has one. Its two real terminal costs must never
        // be marked as definitely inferior to a feasible upper bound.
        bool winnerSafe = true;
        if (oracle.EmbarkEdge is { } embark &&
            oracle.DisembarkEdge is { } disembark)
        {
            var a = entries.SingleOrDefault(x => x.Edge == embark)
                ?? throw new InvalidOperationException(
                    "The reference embarkation is not reachable by land.");
            var b = exits.SingleOrDefault(x => x.Edge == disembark)
                ?? throw new InvalidOperationException(
                    "The reference disembarkation is not reachable by land.");
            UInt128 lower = (UInt128)a.LandApproachPlusTransfer +
                b.LandApproachPlusTransfer + 1UL;
            // The oracle reports its best SEA alternative even when
            // the terrestrial route is globally cheaper. In that case
            // legitimately eliminating the losing sea choice is exactly
            // what the bound is intended to do. Protect the sea pair only
            // when it is the GLOBAL winning mode (tie: land preferred).
            winnerSafe = !oracle.SeaRoutePreferred ||
                lower <= upper.Value;
            if (!winnerSafe)
                throw new InvalidOperationException(
                    "Provable exclusion would lose the globally winning sea pair.");
        }
        return new Evidence(upper.Value, potential, excluded, retained,
            entries.Length, exits.Length, winnerSafe);
    }

    private static ulong[] Distances(
        ulong source,
        Dictionary<ulong, List<(ulong Next, ulong Cost)>> graph,
        int n)
    {
        ulong[] distance = Enumerable.Repeat(ulong.MaxValue, n + 1).ToArray();
        var queue = new PriorityQueue<ulong, (ulong Cost, ulong Tile)>();
        distance[checked((int)source)] = 0UL;
        queue.Enqueue(source, (0UL, source));
        while (queue.TryDequeue(out var at, out var priority))
        {
            if (priority.Cost != distance[checked((int)at)] ||
                !graph.TryGetValue(at, out var links))
                continue;
            foreach (var link in links)
            {
                ulong next = checked(priority.Cost + link.Cost);
                if (next >= distance[checked((int)link.Next)])
                    continue;
                distance[checked((int)link.Next)] = next;
                queue.Enqueue(link.Next, (next, link.Next));
            }
        }
        return distance;
    }
}
