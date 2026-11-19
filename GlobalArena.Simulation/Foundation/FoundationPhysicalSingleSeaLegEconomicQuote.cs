using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R72 RESEARCH-ONLY monetary contract for a single, explicitly verified
/// maritime leg. Quotes a specified path, not a global optimal route or sale.
/// It uses the EXACT frozen seller-specific R69 land/coast cost domain and
/// applies R54's paired terminal discount ONCE to a real sea-sea chain.
/// The existing market allocator and settlement remain unchanged.
/// </summary>
public static class FoundationPhysicalSingleSeaLegEconomicQuote
{
    public sealed record Evidence(
        FoundationPhysicalTreasuryBoundMaritimeProof.Evidence FrozenProof,
        ulong NaturalSeaLegMicroPerWholeCargoUnit,
        ulong DiscountedSeaLegMicroPerWholeCargoUnit,
        UInt128 TotalFreightMicroPerWholeCargoUnit,
        bool CanCompeteWithLandOnly,
        bool IsAnAuthoritativeSale);

    public static Evidence QuoteVerified(
        WorldState snapshot,
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        CommodityId commodity,
        ulong goodsMicro,
        CivilizationId seller,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        PhysicalTacticalEdgeId embarkEdge,
        PhysicalTacticalEdgeId disembarkEdge,
        ulong planningEconomicCycleOrdinal,
        IReadOnlyList<PhysicalTacticalTileId> oneSeaLegTiles)
    {
        ArgumentNullException.ThrowIfNull(oneSeaLegTiles);
        // This is the ONLY way to obtain the three terrestrial rates:
        // signed world -> authoritative treasury -> exact cargo policy ->
        // previous settlement -> same-seller frozen physical/toll costs.
        var bound = FoundationPhysicalTreasuryBoundMaritimeProof.Evaluate(
            snapshot, world, incidence, cargoPolicy, commodity, goodsMicro,
            seller, source, destination, embarkEdge, disembarkEdge,
            planningEconomicCycleOrdinal);
        if (oneSeaLegTiles.Count < 4)
            throw new ArgumentException(
                "Maritime legs require land-water-water-land with actual coast crossings.");

        var byId = snapshot.Transport.Edges.ToDictionary(e => e.Edge);
        var edgeGeometry = incidence.FineTopology.Edges.ToDictionary(
            e => e.IncidentCellIds.OrderBy(id => id.Value)
                .Select(id => id.Value).ToArray(),
            e => e.Id, new PairComparer());
        var routeEdges = new List<PhysicalTransportEdgeRuntimeRecord>();
        for (int i = 1; i < oneSeaLegTiles.Count; i++)
        {
            var a = oneSeaLegTiles[i - 1];
            var b = oneSeaLegTiles[i];
            if (!a.IsValid || !b.IsValid ||
                a.FineGoldbergParameters != incidence.Refinement.FineParameters ||
                b.FineGoldbergParameters != incidence.Refinement.FineParameters ||
                a == b)
                throw new ArgumentException(
                    "An explicit sea-leg route must use different real fine tiles.");
            var pair = new[] { a.FineStrategicCellId.Value,
                b.FineStrategicCellId.Value };
            Array.Sort(pair);
            if (!edgeGeometry.TryGetValue(pair, out var edgeId))
                throw new ArgumentException(
                    "Maritime leg contains a fabricated or non-adjacent crossing.");
            var id = new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, edgeId);
            routeEdges.Add(byId[id]);
        }
        if (routeEdges[0].Edge != embarkEdge ||
            routeEdges[^1].Edge != disembarkEdge ||
            oneSeaLegTiles[0].FineStrategicCellId.Value !=
                bound.Route.EmbarkLandTile ||
            oneSeaLegTiles[^1].FineStrategicCellId.Value !=
                bound.Route.DisembarkLandTile)
            throw new ArgumentException(
                "The actual sea-leg terminal edges must match the frozen freight proof.");

        ulong naturalSea = 0UL;
        for (int i = 1; i < routeEdges.Count - 1; i++)
            naturalSea = checked(naturalSea +
                routeEdges[i].FreePhysicalCostMicroPerWholeCargoUnit);
        ulong discountedSea =
            FoundationPhysicalPairedCoastalSeaLegQuote.QuoteVerifiedSeaLeg(
                world, incidence, oneSeaLegTiles, routeEdges);
        if (discountedSea == 0UL || discountedSea > naturalSea)
            throw new InvalidOperationException(
                "The whole-leg quote must stay positive and not exceed natural sea costs.");
        // R69's lower bound already contains exactly ONE micro-money for
        // the entire maritime leg. Replace that floor with the verified
        // complete R54 quote, do NOT discount every sea-sea edge again.
        UInt128 total =
            bound.Route.Bound.MaritimeLowerBoundMicroPerWholeCargoUnit -
            1UL + discountedSea;
        ulong? land = bound.Route.Bound
            .FeasibleLandFreightMicroPerWholeCargoUnit;
        return new Evidence(bound, naturalSea, discountedSea,
            total, !land.HasValue || total <= land.Value,
            IsAnAuthoritativeSale: false);
    }

    private sealed class PairComparer : IEqualityComparer<ulong[]>
    {
        public bool Equals(ulong[]? a, ulong[]? b) =>
            a is not null && b is not null && a.Length == 2 &&
            b.Length == 2 && a[0] == b[0] && a[1] == b[1];
        public int GetHashCode(ulong[] a) =>
            HashCode.Combine(a[0], a[1]);
    }
}
