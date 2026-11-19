using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R67 RESEARCH-ONLY world-bound proof of owner-aware one-sea-leg pruning.
/// Reads real finite crossing inventory and ownership from one WorldState,
/// and derives ALL three land-only distances for the SAME seller/snapshot.
/// No user-supplied tariff, road, settlement, market price, or land bound.
/// Sea distance is not quoted here; the R54 positive whole-leg floor is
/// the only maritime fact used. Not a live pathfinder or economic receipt.
/// </summary>
public static class FoundationPhysicalWorldBoundMaritimeFreightProof
{
    public sealed record Evidence(
        FoundationPhysicalMaritimeMarginalFreightBound.Decision Bound,
        PhysicalTacticalEdgeId EmbarkEdge,
        PhysicalTacticalEdgeId DisembarkEdge,
        ulong EmbarkLandTile,
        ulong DisembarkLandTile,
        bool HasRealConnectedSeaLeg,
        bool UsesAuthoritativeWorldTransport);

    private sealed record Crossing(
        PhysicalTacticalEdgeId Id, ulong Land, ulong Sea);

    public static Evidence Evaluate(
        WorldState snapshot,
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        CivilizationId seller,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        PhysicalTacticalEdgeId embarkEdge,
        PhysicalTacticalEdgeId disembarkEdge)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        if (!seller.IsValid || source == destination ||
            !source.IsValid || !destination.IsValid ||
            !embarkEdge.IsValid || !disembarkEdge.IsValid ||
            embarkEdge == disembarkEdge ||
            incidence.Refinement.CoarseParameters !=
                world.Request.StrategicParameters ||
            source.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            destination.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            embarkEdge.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            disembarkEdge.FineGoldbergParameters != incidence.Refinement.FineParameters)
            throw new ArgumentException(
                "R67 needs one real seller, world and two distinct coastal edges.");

        int count = incidence.FineTopology.Cells.Count;
        if (source.FineStrategicCellId.Value > (ulong)count ||
            destination.FineStrategicCellId.Value > (ulong)count)
            throw new ArgumentException("Market tiles do not exist in this world.");

        // The authoritative projector checks the WorldState binding,
        // worker occupation provenance and ALL real crossings, including
        // duplicate or foreign edge IDs. It is the only quote source.
        if (snapshot.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(world))
            throw new ArgumentException(
                "World generation signature does not match the bound snapshot.");
        var (roads, _) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                snapshot, incidence);
        var byId = roads.ToDictionary(x => x.Edge);
        var investedById = snapshot.Transport.Edges.ToDictionary(x => x.Edge);
        var isLand = incidence.TileIncidences.Select(tile =>
        {
            long sum = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                sum = checked(sum + world.StrategicPhysicalFields
                    .Elevation.GetRawValue(coarse));
            return sum / tile.IncidentCoarseCellIds.Count >
                world.StrategicPhysicalFields.SeaLevel.RawValue;
        }).ToArray();
        ulong s = source.FineStrategicCellId.Value;
        ulong d = destination.FineStrategicCellId.Value;
        if (!isLand[checked((int)s - 1)] ||
            !isLand[checked((int)d - 1)])
            throw new ArgumentException(
                "Both markets must be real terrestrial settlements.");

        var coasts = new Dictionary<PhysicalTacticalEdgeId, Crossing>();
        var landGraph = new Dictionary<ulong, List<(ulong Tile, ulong Cost)>>();
        var oceanGraph = new Dictionary<ulong, List<ulong>>();
        static void AddLand(
            Dictionary<ulong, List<(ulong Tile, ulong Cost)>> graph,
            ulong a, ulong b, ulong cost)
        {
            if (!graph.TryGetValue(a, out var left))
                graph[a] = left = new();
            if (!graph.TryGetValue(b, out var right))
                graph[b] = right = new();
            left.Add((b, cost));
            right.Add((a, cost));
        }
        static void AddSea(Dictionary<ulong, List<ulong>> graph,
            ulong a, ulong b)
        {
            if (!graph.TryGetValue(a, out var left))
                graph[a] = left = new();
            if (!graph.TryGetValue(b, out var right))
                graph[b] = right = new();
            left.Add(b);
            right.Add(a);
        }
        foreach (var edge in incidence.FineTopology.Edges)
        {
            var id = new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, edge.Id);
            var record = byId[id];
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            bool la = isLand[checked((int)a - 1)];
            bool lb = isLand[checked((int)b - 1)];
            if (la && lb)
            {
                // Canonical seller-specific owner exemption, same as
                // FoundationPhysicalTradeRouteCandidatePlanner.RouteFrom.
                ulong toll = record.InfrastructureOwner.HasValue &&
                    record.InfrastructureOwner.Value != seller
                    ? record.ExternalTariffMicroPerWholeCargoUnit : 0UL;
                AddLand(landGraph, a, b,
                    checked(record.PhysicalCostMicroPerWholeCargoUnit + toll));
            }
            else if (!la && !lb)
            {
                // No freighter, port or capital on open sea. This graph
                // only establishes that the one maritime leg exists.
                if (record.InfrastructureOwner.HasValue ||
                    record.ExternalTariffMicroPerWholeCargoUnit != 0UL ||
                    investedById[id].CargoMicroAtReferenceLoad != ulong.MaxValue)
                    throw new InvalidOperationException(
                        "Natural sea must remain untariffed and unbounded.");
                AddSea(oceanGraph, a, b);
            }
            else
            {
                var infrastructure = investedById[id];
                if (infrastructure.CargoMicroAtReferenceLoad == ulong.MaxValue)
                    throw new InvalidOperationException(
                        "A coastal crossing must have finite capacity.");
                coasts.Add(id, new Crossing(id,
                    la ? a : b, la ? b : a));
            }
        }
        if (!coasts.TryGetValue(embarkEdge, out var embark) ||
            !coasts.TryGetValue(disembarkEdge, out var disembark))
            throw new ArgumentException(
                "Both selected edges must be real land-sea crossings.");

        if (embark.Sea == disembark.Sea)
            throw new ArgumentException(
                "This proof requires a positive mar-mar leg between distinct water tiles.");
        var visited = new HashSet<ulong> { embark.Sea };
        var pending = new Queue<ulong>();
        pending.Enqueue(embark.Sea);
        while (pending.TryDequeue(out ulong at) &&
               !visited.Contains(disembark.Sea))
        {
            if (!oceanGraph.TryGetValue(at, out var links))
                continue;
            foreach (ulong next in links)
                if (visited.Add(next))
                    pending.Enqueue(next);
        }
        if (!visited.Contains(disembark.Sea))
            throw new ArgumentException(
                "Selected coasts have no uninterrupted real sea-sea path.");

        ulong[] from = LandDistances(s, landGraph, count);
        ulong[] to = LandDistances(d, landGraph, count);
        static ulong? Finite(ulong value) =>
            value == ulong.MaxValue ? null : value;
        // A disconnected endpoint yields no provable finite upper
        // bound. It cannot exclude a maritime path via this contract.
        ulong? landUpper = Finite(from[checked((int)d)]);
        ulong? accessA = Finite(from[checked((int)embark.Land)]);
        ulong? accessB = Finite(to[checked((int)disembark.Land)]);
        if (!accessA.HasValue || !accessB.HasValue)
            throw new ArgumentException(
                "The selected coast is not accessible by land from its market.");

        var quote =
            FoundationPhysicalMaritimeMarginalFreightBound.Evaluate(
                byId[embark.Id], byId[disembark.Id], seller,
                accessA.Value, accessB.Value, landUpper);
        return new Evidence(quote, embark.Id, disembark.Id,
            embark.Land, disembark.Land, true, true);
    }

    private static ulong[] LandDistances(
        ulong origin, Dictionary<ulong, List<(ulong Tile, ulong Cost)>> graph,
        int count)
    {
        ulong[] distance =
            Enumerable.Repeat(ulong.MaxValue, count + 1).ToArray();
        var queue = new PriorityQueue<ulong, (ulong Cost, ulong Tile)>();
        distance[checked((int)origin)] = 0UL;
        queue.Enqueue(origin, (0UL, origin));
        while (queue.TryDequeue(out ulong at, out var priority))
        {
            if (priority.Cost != distance[checked((int)at)] ||
                !graph.TryGetValue(at, out var links))
                continue;
            foreach (var link in links)
            {
                ulong candidate = checked(priority.Cost + link.Cost);
                if (candidate >= distance[checked((int)link.Tile)])
                    continue;
                distance[checked((int)link.Tile)] = candidate;
                queue.Enqueue(link.Tile, (candidate, link.Tile));
            }
        }
        return distance;
    }
}
