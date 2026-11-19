using System.Numerics;
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
    public sealed record Result(
        ulong? LandOnlyRateMicro,
        ulong? OneSeaLegRateMicro,
        PhysicalTacticalEdgeId? EmbarkEdge,
        PhysicalTacticalEdgeId? DisembarkEdge,
        int ExploredSeaOrigins,
        int EligibleEmbarkations,
        int EligibleDisembarkations)
    {
        public bool SeaRoutePreferred =>
            OneSeaLegRateMicro.HasValue &&
            (!LandOnlyRateMicro.HasValue ||
             OneSeaLegRateMicro.Value < LandOnlyRateMicro.Value);
    }

    private sealed record Link(ulong Other, ulong Rate);
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
        int maxEligibleEmbarkations = 128)
    {
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
        ulong? seaBest = null;
        PhysicalTacticalEdgeId? embark = null, disembark = null;
        int explored = 0;
        foreach (var portA in entrances)
        {
            // One exact sea-subgraph Dijkstra per ACTUAL reachable
            // embarkation port, never once per commodity or cargo unit.
            ulong[] seaDistances = Shortest(portA.SeaTile,
                seaGraph, count);
            explored++;
            foreach (var portB in exits)
            {
                if (portA.Record.Edge == portB.Record.Edge ||
                    seaDistances[checked((int)portB.SeaTile)] is
                        0UL or ulong.MaxValue)
                    continue;
                ulong seaRate = seaDistances[
                    checked((int)portB.SeaTile)];
                // This rate is for the ENTIRE sea leg, not a rate to write
                // into each sea-sea edge of the authoritative ledger.
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
        return new Result(landOnly, seaBest, embark, disembark,
            explored, entrances.Length, exits.Length);
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
