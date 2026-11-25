using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R169 opt-in, read-only path into the EXISTING global water-filling solver.
/// Uses temporal R168 production/demand and canonical physical freight, but
/// does not publish prices, revenue, taxes or an economic journal event.
/// </summary>
public static class FoundationTemporalNativeMarginalMarketPlanner
{
    public static FoundationPhysicalGlobalMarginalCoordinationPlan Coordinate(
        WorldState baseline, PhysicalTacticalIncidenceMap incidence,
        FoundationTemporalEconomicTurnInputs temporal,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        int maxProducerPasses = 64,
        FoundationFineFirstPhysicalGeography? signedFine = null,
        WorldState? freightSnapshot = null) => CoordinateVerified(baseline, incidence, temporal, demandPolicy, cargoPolicy, maxProducerPasses, signedFine, freightSnapshot, null);

    internal static FoundationPhysicalGlobalMarginalCoordinationPlan CoordinateVerified(
        WorldState baseline, PhysicalTacticalIncidenceMap incidence,
        FoundationTemporalEconomicTurnInputs temporal, FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy cargoPolicy, int maxProducerPasses,
        FoundationFineFirstPhysicalGeography? signedFine, WorldState? freightSnapshot,
        IReadOnlyList<FoundationPhysicalRoutingEdgeCost>? verifiedRoads,
        bool useProgressiveDistribution = false,
        IReadOnlyList<FoundationPhysicalEconomicTaxRate>? safeProgressiveRouteTaxes = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(temporal);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(cargoPolicy);
        var transportWorld = freightSnapshot ?? baseline;
        if (baseline.WorldBinding != transportWorld.WorldBinding ||
            !transportWorld.Transport.IsInitialized ||
            !baseline.Workers.HasAuthoritativeOccupations ||
            !demandPolicy.Commodities.SequenceEqual(cargoPolicy.Commodities) ||
            (baseline.WorldBinding?.IsFineFirstWorld == true &&
                signedFine is null))
            throw new InvalidOperationException(
                "Temporal clearing needs the bound real physical market, signed geography and complete commodity policies.");
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            baseline, temporal, demandPolicy);
        var roads = verifiedRoads ?? FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(transportWorld, incidence).Roads;
        var blocked = new HashSet<PhysicalTacticalEdgeId>();
        if (signedFine is not null)
        {
            if (signedFine.FineParameters != incidence.Refinement.FineParameters)
                throw new InvalidOperationException("Foreign signed fine geography.");
            foreach (var edge in incidence.FineTopology.Edges)
            {
                int a = checked((int)edge.IncidentCellIds[0].Value - 1);
                int b = checked((int)edge.IncidentCellIds[1].Value - 1);
                if (signedFine.CrossingAtIndices(a, b) ==
                    FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                    blocked.Add(new PhysicalTacticalEdgeId(
                        incidence.Refinement.FineParameters, edge.Id));
            }
        }
        var quotes = market.DemandByCivilization
            .Select(line => new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                FoundationPhysicalUniversalMarketPricing
                    .UnitQuoteMicroPerWholeCommodityUnit))
            .ToArray();
        FoundationPhysicalUniversalMarketPricing.RequireUniversal(quotes);
        var allowed = roads.Where(edge => !blocked.Contains(edge.Edge)).ToArray();
        FoundationPhysicalGlobalMarginalCoordinationPlan plan;
        if (useProgressiveDistribution)
        {
            // Complete audited progressive allocation, NOT a truncated
            // failed global solve. Never certify a fixed point here.
            var progressive = FoundationPhysicalMarginalMarketDistributor.Distribute(
                market, incidence, cargoPolicy, quotes, allowed, safeProgressiveRouteTaxes);
            plan = new FoundationPhysicalGlobalMarginalCoordinationPlan(
                progressive.Candidates.ToArray(), progressive.MarketTotals.ToArray(),
                progressive.MarketTotals.ToArray(), progressive.AllocationAudit,
                progressive.ProductiveLotCount, 0, 0UL, false, Array.Empty<ulong>(),
                false, 0, System.Numerics.BigInteger.Zero, stableUnderOneFullCanonicalPass: false);
        }
        else
            plan = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
                market, incidence, cargoPolicy, quotes, allowed, maxProducerPasses);
        if (plan.AllocationAudit.TotalAvailableGoodsMicro !=
                temporal.TotalProductionMicro ||
            plan.AllocationAudit.TotalAllocatedGoodsMicro !=
                temporal.TotalProductionMicro)
            throw new InvalidOperationException(
                "Existing marginal solver lost or duplicated journaled temporal goods.");
        return plan;
    }
}
