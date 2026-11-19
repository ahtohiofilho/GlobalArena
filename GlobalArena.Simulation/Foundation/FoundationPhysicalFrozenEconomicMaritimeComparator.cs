using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R73 research-only global ONE-sea-leg economic quote comparator.
/// Unlike R55, land and coast use authoritative seller-specific frozen
/// freight and the current treasury's closed prior-cycle congestion.
/// Sea quotes use the complete actual coastline-pair R54 formula ONCE.
/// No resource allocation, revenue, settlement, market-price or cache
/// mutation occurs. This is NOT yet the authoritative buyer pathfinder.
/// </summary>
public static class FoundationPhysicalFrozenEconomicMaritimeComparator
{
    public sealed record Result(
        ulong? LandOnlyRateMicro,
        UInt128? BestSeaRateMicro,
        PhysicalTacticalEdgeId? EmbarkEdge,
        PhysicalTacticalEdgeId? DisembarkEdge,
        long PotentialPairs,
        long ExcludedPairs,
        long QuotedPairs)
    {
        public bool SeaPreferred => BestSeaRateMicro.HasValue &&
            (!LandOnlyRateMicro.HasValue ||
             BestSeaRateMicro.Value < LandOnlyRateMicro.Value);
    }

    private sealed record Coast(
        PhysicalTacticalEdgeId Edge, ulong Land, ulong Sea,
        ulong SellerCrossingRate,
        PhysicalTransportEdgeRuntimeRecord Inventory);

    public static Result Compare(
        WorldState snapshot,
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy conversion,
        CommodityId commodity, ulong goodsMicro, CivilizationId seller,
        PhysicalTacticalTileId source, PhysicalTacticalTileId destination,
        ulong planningEconomicCycleOrdinal, bool proveBound)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(conversion);
        if (!seller.IsValid || !commodity.IsValid || goodsMicro == 0UL ||
            conversion.CargoMicro(commodity, goodsMicro) == 0UL ||
            snapshot.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(world) ||
            !snapshot.Treasury.IsInitialized ||
            planningEconomicCycleOrdinal != checked(
                snapshot.Treasury.LastSettledEconomicCycleOrdinal + 1UL) ||
            (snapshot.Treasury.HasClosedEconomicCycle &&
             snapshot.Treasury.LastSettledCargoUnitCode != conversion.CargoUnitCode))
            throw new ArgumentException(
                "Only a matching real world, seller, exact commodity cargo " +
                "and immediately following economic cycle may be quoted.");

        var (freeRoads, scales) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                snapshot, incidence);
        var byId = freeRoads.ToDictionary(e => e.Edge);
        var capacity = scales.ToDictionary(e => e.Edge,
            e => e.CargoMicroAtReferenceLoad);
        var inventory = snapshot.Transport.Edges.ToDictionary(e => e.Edge);
        var historic = snapshot.Treasury.LastSettledFreightQuotes
            .ToDictionary(e => e.Edge);
        if (snapshot.Treasury.HasClosedEconomicCycle &&
            (historic.Count != byId.Count ||
             byId.Keys.Any(key => !historic.ContainsKey(key))))
            throw new InvalidOperationException(
                "A frozen economic quote needs every real settled edge.");
        var isLand = incidence.TileIncidences.Select(tile =>
        {
            long total = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                total = checked(total + world.StrategicPhysicalFields
                    .Elevation.GetRawValue(coarse));
            return total / tile.IncidentCoarseCellIds.Count >
                world.StrategicPhysicalFields.SeaLevel.RawValue;
        }).ToArray();
        int count = incidence.FineTopology.Cells.Count;
        ulong s = source.FineStrategicCellId.Value;
        ulong d = destination.FineStrategicCellId.Value;
        if (!source.IsValid || !destination.IsValid || s == d ||
            source.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            destination.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            s > (ulong)count || d > (ulong)count ||
            !isLand[checked((int)s - 1)] ||
            !isLand[checked((int)d - 1)])
            throw new ArgumentException(
                "A research maritime market needs two distinct real land tiles.");

        var land = new Dictionary<ulong, List<(ulong Tile, ulong Cost)>>();
        var sea = new Dictionary<ulong, List<(ulong Tile, ulong Cost)>>();
        var coasts = new List<Coast>();
        static void Add(
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

        foreach (var edge in incidence.FineTopology.Edges)
        {
            var id = new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, edge.Id);
            var row = byId[id];
            var actual = inventory[id];
            ulong surcharge = 0UL;
            if (snapshot.Treasury.HasClosedEconomicCycle &&
                capacity[id] != ulong.MaxValue)
            {
                var previous = historic[id];
                if (previous.FreePhysicalCostMicroPerWholeCargoUnit ==
                    row.PhysicalCostMicroPerWholeCargoUnit &&
                    previous.ReferenceCargoMicro == capacity[id])
                    surcharge =
                        previous.QuotedCongestionSurchargeMicroPerWholeCargoUnit;
            }
            ulong physical = checked(
                row.PhysicalCostMicroPerWholeCargoUnit + surcharge);
            ulong thirdParty = row.InfrastructureOwner.HasValue &&
                row.InfrastructureOwner.Value != seller
                ? row.ExternalTariffMicroPerWholeCargoUnit : 0UL;
            ulong sellerCost = checked(physical + thirdParty);
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            bool la = isLand[checked((int)a - 1)];
            bool lb = isLand[checked((int)b - 1)];
            if (la && lb)
            {
                if (capacity[id] == ulong.MaxValue)
                    throw new InvalidOperationException("Land has finite capacity.");
                Add(land, a, b, sellerCost);
            }
            else if (!la && !lb)
            {
                if (capacity[id] != ulong.MaxValue ||
                    row.InfrastructureOwner.HasValue ||
                    row.ExternalTariffMicroPerWholeCargoUnit != 0UL ||
                    surcharge != 0UL)
                    throw new InvalidOperationException(
                        "Open sea is naturally unowned, unbounded and without congestion.");
                Add(sea, a, b, physical);
            }
            else
            {
                if (capacity[id] == ulong.MaxValue)
                    throw new InvalidOperationException(
                        "Coastal transfer requires finite infrastructure.");
                coasts.Add(new Coast(id, la ? a : b,
                    la ? b : a, sellerCost, actual));
            }
        }

        ulong[] from = Distances(s, land, count);
        ulong[] to = Distances(d, land, count);
        ulong? landUpper = from[checked((int)d)] == ulong.MaxValue
            ? null : from[checked((int)d)];
        var entrances = coasts.Where(x =>
            from[checked((int)x.Land)] != ulong.MaxValue)
            .OrderBy(x => x.Edge.FineStrategicEdgeId.Value).ToArray();
        var exits = coasts.Where(x =>
            to[checked((int)x.Land)] != ulong.MaxValue)
            .OrderBy(x => x.Edge.FineStrategicEdgeId.Value).ToArray();

        long potential = checked((long)entrances.Length * exits.Length);
        long excluded = 0L, quotes = 0L;
        UInt128? best = null;
        PhysicalTacticalEdgeId? bestA = null, bestB = null;
        foreach (var a in entrances)
        {
            // One local, per-query sea search per real embarkation, with
            // NO cross-cycle/shared mutable cache or approximation.
            ulong[] water = Distances(a.Sea, sea, count);
            foreach (var b in exits)
            {
                if (a.Edge == b.Edge || a.Sea == b.Sea ||
                    water[checked((int)b.Sea)] == ulong.MaxValue)
                    continue;
                UInt128 floor =
                    (UInt128)from[checked((int)a.Land)] +
                    a.SellerCrossingRate + 1UL +
                    b.SellerCrossingRate +
                    to[checked((int)b.Land)];
                if (proveBound && landUpper.HasValue &&
                    floor > landUpper.Value)
                {
                    excluded++;
                    continue;
                }
                // The sea length is the exact natural sum of REAL
                // mar-mar crossing costs. Apply terminal-paired R54
                // once, independent of seller and cargo mass.
                ulong quoted = FoundationPhysicalPairedCoastalSeaLegQuote
                    .Quote(water[checked((int)b.Sea)],
                        a.Inventory, b.Inventory);
                UInt128 total = floor - 1UL + quoted;
                quotes++;
                // On equal-cost maritime routes, use canonical numeric
                // terminal identities; land beats an equal-cost sea route.
                bool better = !best.HasValue || total < best.Value ||
                    total == best.Value &&
                    (a.Edge.FineStrategicEdgeId.Value <
                        bestA!.Value.FineStrategicEdgeId.Value ||
                     a.Edge == bestA.Value &&
                     b.Edge.FineStrategicEdgeId.Value <
                        bestB!.Value.FineStrategicEdgeId.Value);
                if (!better) continue;
                best = total;
                bestA = a.Edge;
                bestB = b.Edge;
            }
        }
        return new Result(landUpper, best, bestA, bestB,
            potential, excluded, quotes);
    }

    private static ulong[] Distances(
        ulong source, Dictionary<ulong, List<(ulong Tile, ulong Cost)>> graph,
        int count)
    {
        var distances = Enumerable.Repeat(ulong.MaxValue, count + 1).ToArray();
        var queue = new PriorityQueue<ulong, (ulong Cost, ulong Tile)>();
        distances[checked((int)source)] = 0UL;
        queue.Enqueue(source, (0UL, source));
        while (queue.TryDequeue(out ulong current, out var priority))
        {
            if (distances[checked((int)current)] != priority.Cost ||
                !graph.TryGetValue(current, out var links))
                continue;
            foreach (var (next, cost) in links)
            {
                ulong value = checked(priority.Cost + cost);
                if (value >= distances[checked((int)next)]) continue;
                distances[checked((int)next)] = value;
                queue.Enqueue(next, (value, next));
            }
        }
        return distances;
    }
}
