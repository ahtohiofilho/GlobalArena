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
        FoundationFineFirstPhysicalGeography? signedFine = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(temporal);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(cargoPolicy);
        if (!baseline.Transport.IsInitialized ||
            !baseline.Workers.HasAuthoritativeOccupations ||
            !demandPolicy.Commodities.SequenceEqual(cargoPolicy.Commodities) ||
            (baseline.WorldBinding?.IsFineFirstWorld == true &&
                signedFine is null))
            throw new InvalidOperationException(
                "Temporal clearing needs the bound real physical market, signed geography and complete commodity policies.");
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            baseline, temporal, demandPolicy);
        var (roads, _) = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(baseline, incidence);
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
        var plan = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            market, incidence, cargoPolicy, quotes,
            roads.Where(edge => !blocked.Contains(edge.Edge)),
            maxProducerPasses);
        if (plan.AllocationAudit.TotalAvailableGoodsMicro !=
                temporal.TotalProductionMicro ||
            plan.AllocationAudit.TotalAllocatedGoodsMicro !=
                temporal.TotalProductionMicro)
            throw new InvalidOperationException(
                "Existing marginal solver lost or duplicated journaled temporal goods.");
        return plan;
    }
}
