using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R71 RESEARCH-ONLY compatibility gate: detect price-domain drift between
/// the R55-R65 route comparator and the authoritatively projected economic
/// freight. This is diagnostic, NOT a new route choice or a settlement.
/// It deliberately refuses to certify a multimodal integration even if
/// one test world's free-flow edge rates happen to match.
/// </summary>
public static class FoundationPhysicalMultimodalEconomicParityAudit
{
    public sealed record Evidence(
        ulong? AuthoritativeSellerLandOnlyMicroPerWholeCargo,
        ulong? ExperimentalLandOnlyMicroPerWholeCargo,
        ulong? ExperimentalOneSeaLegMicroPerWholeCargo,
        bool ExperimentalSeaPreferred,
        long DifferentlyPricedPhysicalEdges,
        long SellerThirdPartyTariffEdges,
        long AuthoritativeCoastalCrossings,
        long ExperimentalPhysicalCostDrifts,
        bool CanSafelyReplaceEconomicRouting);

    public static Evidence Compare(
        WorldState snapshot,
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        CivilizationId seller,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        int maxEligibleEmbarkations = 20_000)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        if (!seller.IsValid || snapshot.WorldBinding !=
                RuntimeWorldBinding.FromGeneratedWorld(world))
            throw new ArgumentException(
                "Economic route audit needs a valid seller and signed world.");

        var (roads, _) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                snapshot, incidence);
        var costById = roads.ToDictionary(x => x.Edge);
        var originalById = snapshot.Transport.Edges.ToDictionary(x => x.Edge);
        var isLand = incidence.TileIncidences.Select(tile =>
        {
            long sum = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                sum = checked(sum +
                    world.StrategicPhysicalFields.Elevation.GetRawValue(coarse));
            return sum / tile.IncidentCoarseCellIds.Count >
                world.StrategicPhysicalFields.SeaLevel.RawValue;
        }).ToArray();
        int count = incidence.FineTopology.Cells.Count;
        ulong sourceId = source.FineStrategicCellId.Value;
        ulong targetId = destination.FineStrategicCellId.Value;
        if (!source.IsValid || !destination.IsValid || source == destination ||
            source.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            destination.FineGoldbergParameters != incidence.Refinement.FineParameters ||
            sourceId > (ulong)count || targetId > (ulong)count ||
            !isLand[checked((int)sourceId - 1)] ||
            !isLand[checked((int)targetId - 1)])
            throw new ArgumentException(
                "Comparisons require two actual terrestrial market tiles.");

        var land = new Dictionary<ulong, List<(ulong Tile, ulong Cost)>>();
        long mismatch = 0L, tariffs = 0L, coast = 0L;
        foreach (var geometric in incidence.FineTopology.Edges)
        {
            var edge = new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, geometric.Id);
            var quoted = costById[edge];
            var physical = originalById[edge];
            bool thirdParty = quoted.InfrastructureOwner.HasValue &&
                quoted.InfrastructureOwner.Value != seller &&
                quoted.ExternalTariffMicroPerWholeCargoUnit != 0UL;
            ulong paidTariff = thirdParty
                ? quoted.ExternalTariffMicroPerWholeCargoUnit : 0UL;
            if (thirdParty) tariffs++;
            ulong actual = checked(
                quoted.PhysicalCostMicroPerWholeCargoUnit + paidTariff);
            ulong experimental =
                FoundationPhysicalMultimodalRouteComparator.OperationalCost(
                    physical);
            if (actual != experimental) mismatch++;
            ulong a = geometric.IncidentCellIds[0].Value;
            ulong b = geometric.IncidentCellIds[1].Value;
            bool la = isLand[checked((int)a - 1)];
            bool lb = isLand[checked((int)b - 1)];
            if (la != lb) coast++;
            if (!la || !lb) continue;
            if (!land.TryGetValue(a, out var from))
                land[a] = from = new();
            if (!land.TryGetValue(b, out var to))
                land[b] = to = new();
            from.Add((b, actual));
            to.Add((a, actual));
        }

        ulong[] distances =
            Enumerable.Repeat(ulong.MaxValue, count + 1).ToArray();
        var queue = new PriorityQueue<ulong, (ulong Cost, ulong Tile)>();
        distances[checked((int)sourceId)] = 0UL;
        queue.Enqueue(sourceId, (0UL, sourceId));
        while (queue.TryDequeue(out ulong tile, out var priority))
        {
            if (priority.Cost != distances[checked((int)tile)] ||
                !land.TryGetValue(tile, out var links))
                continue;
            foreach (var link in links)
            {
                ulong next = checked(priority.Cost + link.Cost);
                if (next >= distances[checked((int)link.Tile)]) continue;
                distances[checked((int)link.Tile)] = next;
                queue.Enqueue(link.Tile, (next, link.Tile));
            }
        }
        var experimentalResult =
            FoundationPhysicalMultimodalRouteComparator.Compare(
                world, incidence, snapshot.Transport, source, destination,
                maxEligibleEmbarkations: maxEligibleEmbarkations,
                useProvenLandBound: true);
        ulong? actualLand = distances[checked((int)targetId)] ==
            ulong.MaxValue ? null : distances[checked((int)targetId)];
        long drift = roads.Zip(snapshot.Transport.Edges,
            (road, original) => (road, original))
            .Count(pair => pair.road.Edge == pair.original.Edge &&
                pair.road.PhysicalCostMicroPerWholeCargoUnit !=
                FoundationPhysicalMultimodalRouteComparator.OperationalCost(
                    pair.original));
        // Even if a test has no tariff/cost divergence, R55 discounts
        // the entire sea leg according to both coasts, whereas the
        // authoritative RouteFrom prices each sea edge individually.
        // It would be incorrect to assert settlement compatibility here.
        return new Evidence(actualLand, experimentalResult.LandOnlyRateMicro,
            experimentalResult.OneSeaLegRateMicro,
            experimentalResult.SeaRoutePreferred,
            mismatch, tariffs, coast, drift,
            CanSafelyReplaceEconomicRouting: false);
    }
}
