using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A validated candidate-only fixed point of the multi-buyer allocator
/// coupled to multi-path freight loads. This does NOT imply a simultaneous
/// global freight optimum, marginal-price equilibrium or financial clearing.
/// </summary>
public sealed class FoundationPhysicalCoupledMultiPathFeedbackPlan
{
    public FoundationPhysicalMultiPathCandidatePlan Freight { get; }
    public IReadOnlyList<FoundationPhysicalRoutingEdgeCost> ConsistentEdgeCosts { get; }
    public int FeedbackIterations { get; }
    public bool ExactCostAndLoadAgreement { get; }

    internal FoundationPhysicalCoupledMultiPathFeedbackPlan(
        FoundationPhysicalMultiPathCandidatePlan freight,
        FoundationPhysicalRoutingEdgeCost[] costs, int iterations)
    {
        Freight = freight;
        ConsistentEdgeCosts = Array.AsReadOnly(costs);
        FeedbackIterations = iterations;
        ExactCostAndLoadAgreement = true;
    }
}

/// <summary>
/// Bounded candidate-only coordination between REAL physical buyers and
/// multi-path loads. Each full feedback pass:
///  (1) coordinates all producers with current marginal freight rates;
///  (2) splits their real shipments across alternative physical routes,
///      using base-edge prospective quartic congestion and tariffs;
///  (3) computes final shared per-edge cargo from the independent auditor;
///  (4) derives next marginal freight rates from those exact audited loads.
/// A result is accepted only if the rates used for (1) equal all the rates
/// derived in (4). Cycles and an exhausted iteration budget fail CLOSED.
///
/// Critical limitation: route splitting in (2) is greedy and always uses
/// base physical rates plus the prospective batch load. Thus this fixed
/// point represents agreement between buyer quantities and the audited
/// multi-path congestion, NOT a proof of optimal paths, Wardrop equilibrium
/// or globally maximal financial returns.
/// </summary>
public static class FoundationPhysicalCoupledMultiPathMarketFeedback
{
    public static FoundationPhysicalCoupledMultiPathFeedbackPlan Coordinate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int maxProducerPasses,
        int maxBatchesPerDelivery,
        int maxFeedbackIterations)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (maxProducerPasses < 1)
            throw new ArgumentOutOfRangeException(nameof(maxProducerPasses));
        if (maxBatchesPerDelivery < 1)
            throw new ArgumentOutOfRangeException(nameof(maxBatchesPerDelivery));
        if (maxFeedbackIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFeedbackIterations));

        var quoteList = quotes.ToArray();
        var roads = freeFlowEdgeCosts.OrderBy(
            e => e?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        var scales = carryingScales.ToArray();
        // Validate all topology and ownership entries up front; each
        // physical crossing has one capacity across both directions and
        // all commodities. Split below also validates the complete scales.
        FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
            incidence, roads);
        var current = roads;
        var observed = new HashSet<string>();

        string Signature(FoundationPhysicalRoutingEdgeCost[] costs) =>
            string.Join(",", costs.Select(c =>
                c.PhysicalCostMicroPerWholeCargoUnit.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        observed.Add(Signature(current));

        for (int pass = 1; pass <= maxFeedbackIterations; pass++)
        {
            var markets = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
                census, incidence, demandPolicy, conversionPolicy,
                quoteList, current, maxProducerPasses);
            // Keep market quantities exactly as selected with 'current'
            // costs; use BASE costs to construct the multi-path routes
            // without accidentally applying the congestion penalty twice.
            var freight = FoundationPhysicalMultiPathFreightPlanner.Split(
                census, incidence, demandPolicy, conversionPolicy,
                markets, roads, scales, maxBatchesPerDelivery);
            if (freight.AllocationAudit.TotalAllocatedGoodsMicro !=
                    markets.AllocationAudit.TotalAllocatedGoodsMicro ||
                freight.AllocationAudit.TotalAvailableGoodsMicro !=
                    markets.AllocationAudit.TotalAvailableGoodsMicro)
                throw new InvalidOperationException(
                    "Market and multi-path supply differ after redistribution.");

            var projected = freight.ProjectedEdgeCosts.ToDictionary(
                line => line.Edge);
            if (projected.Count != roads.Length)
                throw new InvalidOperationException(
                    "The final audited freight projection omitted a physical edge.");
            var next = new FoundationPhysicalRoutingEdgeCost[roads.Length];
            bool unchanged = true;
            for (int i = 0; i < roads.Length; i++)
            {
                var original = roads[i];
                var actual = projected[original.Edge];
                if (actual.FreePhysicalCostMicroPerWholeCargoUnit !=
                        original.PhysicalCostMicroPerWholeCargoUnit ||
                    actual.ExternalTariffMicroPerWholeCargoUnit !=
                        original.ExternalTariffMicroPerWholeCargoUnit ||
                    actual.InfrastructureOwner != original.InfrastructureOwner ||
                    actual.OwnershipKind != original.OwnershipKind)
                    throw new InvalidOperationException(
                        "Multi-path feedback changed a base cost, owner or tariff.");
                next[i] = original with
                {
                    PhysicalCostMicroPerWholeCargoUnit =
                        actual.CongestedPhysicalCostMicroPerWholeCargoUnit
                };
                if (current[i].PhysicalCostMicroPerWholeCargoUnit !=
                    next[i].PhysicalCostMicroPerWholeCargoUnit)
                    unchanged = false;
            }
            if (freight.ProjectedEdgeCosts.Aggregate(BigInteger.Zero,
                    (sum, edge) => sum + edge.CargoMicro) !=
                freight.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro)
                throw new InvalidOperationException(
                    "Congestion projection differs from audited actual crossings.");

            if (unchanged)
                return new FoundationPhysicalCoupledMultiPathFeedbackPlan(
                    freight, next, pass);
            if (!observed.Add(Signature(next)))
                throw new InvalidOperationException(
                    "Multi-path market/freight congestion feedback oscillated. " +
                    "Do not publish a non-stationary plan as settled trade.");
            current = next;
        }

        throw new InvalidOperationException(
            "Coupled buyers, multi-path freight and congestion could not " +
            "reach exact cost/load agreement within the iteration budget. " +
            "No economic cycle was published.");
    }
}
