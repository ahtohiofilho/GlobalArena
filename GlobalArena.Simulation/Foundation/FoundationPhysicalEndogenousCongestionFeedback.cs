using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit physical carrying scale for ONE real tactical crossing. It uses
/// the exact same cargo micro-unit defined by the conversion policy. This is
/// NOT a hard shipment cap and cannot be inferred from a tile or its coast.
/// </summary>
public sealed record FoundationPhysicalEdgeCarryingScale(
    PhysicalTacticalEdgeId Edge, ulong CargoMicroAtReferenceLoad);

/// <summary>
/// Physical cost and utilization for one declared infrastructure crossing.
/// A zero-load edge is included so no price or owner must be inferred by UI.
/// These are candidate-planning facts, NOT a settled economic-cycle snapshot.
/// </summary>
public sealed record FoundationPhysicalCongestedEdgeReading(
    PhysicalTacticalEdgeId Edge,
    ulong CargoMicro,
    ulong CargoMicroAtReferenceLoad,
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong CongestedPhysicalCostMicroPerWholeCargoUnit,
    FoundationInfrastructureOwnershipKind OwnershipKind,
    CivilizationId? InfrastructureOwner,
    ulong ExternalTariffMicroPerWholeCargoUnit);

/// <summary>
/// Exact physical delivery candidates after cost-volume feedback stabilizes.
/// CongestionIterations counts complete re-solves of the coupled producer
/// planner. It is NOT a market-equilibrium or monetary-settlement receipt.
/// </summary>
public sealed class FoundationPhysicalEndogenousCongestionPlan
{
    public FoundationPhysicalGlobalMarginalCoordinationPlan Distribution { get; }
    public IReadOnlyList<FoundationPhysicalCongestedEdgeReading> Edges { get; }
    public int CongestionIterations { get; }
    public bool ConsistentWithFinalEdgeLoads { get; }

    internal FoundationPhysicalEndogenousCongestionPlan(
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution,
        FoundationPhysicalCongestedEdgeReading[] edges, int iterations)
    {
        Distribution = distribution;
        Edges = Array.AsReadOnly(edges);
        CongestionIterations = iterations;
        ConsistentWithFinalEdgeLoads = true;
    }
}

/// <summary>
/// Bounded feedback between real freight load by physical edge (#386) and
/// shortest route / multi-producer marginal distribution (#391). Uses the
/// same quartic congestion SHAPE as the isolated foundation laboratory:
///   physical_rate = free_rate * (1 + (total_load / reference_load)^4)
/// with a conservative ceiling to the integer micro-money representation.
/// Any positive cargo adds to the SAME physical crossing irrespective of
/// commodity, direction or civilization; toll remains a separate rate.
/// There is NO arbitrary congestion ceiling or invisible free edge.
///
/// Re-solves until the calculated rates EXACTLY match those used to produce
/// the very same audited final loads. Rejects cycles, overflow and exhausted
/// budgets, rather than publishing a transient result as final trade.
/// Even success is only a fixed point of candidate planning, not actual
/// market clearing, realization of revenue or an economic cycle publication.
/// </summary>
public static class FoundationPhysicalEndogenousCongestionFeedback
{
    internal static ulong CalculateCongestedRate(
        ulong freePhysicalCostMicro, ulong cargoLoadMicro,
        ulong referenceLoadMicro)
    {
        if (freePhysicalCostMicro == 0UL || referenceLoadMicro == 0UL)
            throw new ArgumentOutOfRangeException(nameof(referenceLoadMicro),
                "Both base physical rate and reference carrying scale must be positive.");
        if (cargoLoadMicro == 0UL)
            return freePhysicalCostMicro;

        // BigInteger avoids unsigned intermediate overflow for large worlds,
        // congestion remains unbounded (fail closed above representable rate).
        BigInteger load4 = BigInteger.Pow(new BigInteger(cargoLoadMicro), 4);
        BigInteger cap4 = BigInteger.Pow(new BigInteger(referenceLoadMicro), 4);
        BigInteger surcharge = (new BigInteger(freePhysicalCostMicro) * load4
            + cap4 - BigInteger.One) / cap4;
        BigInteger total = new BigInteger(freePhysicalCostMicro) + surcharge;
        if (total > ulong.MaxValue)
            throw new OverflowException(
                "Physical congestion rate exceeds the micro-unit domain; " +
                "do not clamp an overloaded infrastructure corridor.");
        return checked((ulong)total);
    }

    public static FoundationPhysicalEndogenousCongestionPlan Coordinate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int maxProducerPasses,
        int maxCongestionIterations)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (maxProducerPasses <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxProducerPasses));
        if (maxCongestionIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCongestionIterations));

        // Materialize all external policies ONCE; repeated feedback must not
        // change due to a mutable IEnumerable or noncanonical iteration.
        var quoteList = quotes.ToArray();
        var baseEdges = freeFlowEdgeCosts
            .OrderBy(x => x?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        // Reuse the validated topological graph and historical ownership
        // rules before accepting a carrying scale.
        FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
            incidence, baseEdges);

        var scales = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var line in carryingScales)
        {
            if (line is null || !line.Edge.IsValid ||
                line.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                line.CargoMicroAtReferenceLoad == 0UL ||
                !scales.TryAdd(line.Edge, line.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "Every carrying scale needs one real unique positive " +
                    "physical crossing and compatible common cargo units.",
                    nameof(carryingScales));
        }
        if (scales.Count != baseEdges.Length ||
            baseEdges.Any(edge => !scales.ContainsKey(edge.Edge)))
            throw new ArgumentException(
                "The congestion scale policy must cover ALL and ONLY " +
                "explicitly traversable physical edges; do not infer capacities.",
                nameof(carryingScales));

        var current = baseEdges;
        // Detect deterministic two-cycles and longer route/load feedback
        // oscillations without claiming a converged snapshot.
        var observed = new HashSet<string>();
        string Signature(FoundationPhysicalRoutingEdgeCost[] edges) =>
            string.Join(",", edges.Select(edge =>
                edge.PhysicalCostMicroPerWholeCargoUnit.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        observed.Add(Signature(current));

        for (int pass = 1; pass <= maxCongestionIterations; pass++)
        {
            var distribution = FoundationPhysicalGlobalMarginalCoordinator
                .Coordinate(census, incidence, demandPolicy, conversionPolicy,
                    quoteList, current, maxProducerPasses);
            var freight = distribution.AllocationAudit.Freight;
            var loads = freight.UsedEdges.ToDictionary(
                edge => edge.Edge, edge => edge.TotalCargoMicro);

            var next = new FoundationPhysicalRoutingEdgeCost[baseEdges.Length];
            var readings = new FoundationPhysicalCongestedEdgeReading[baseEdges.Length];
            bool unchanged = true;
            for (int i = 0; i < baseEdges.Length; i++)
            {
                var edge = baseEdges[i];
                ulong load = loads.GetValueOrDefault(edge.Edge);
                ulong scale = scales[edge.Edge];
                ulong rate = CalculateCongestedRate(
                    edge.PhysicalCostMicroPerWholeCargoUnit, load, scale);
                next[i] = edge with
                {
                    PhysicalCostMicroPerWholeCargoUnit = rate
                };
                readings[i] = new FoundationPhysicalCongestedEdgeReading(
                    edge.Edge, load, scale,
                    edge.PhysicalCostMicroPerWholeCargoUnit, rate,
                    edge.OwnershipKind, edge.InfrastructureOwner,
                    edge.ExternalTariffMicroPerWholeCargoUnit);
                if (rate != current[i].PhysicalCostMicroPerWholeCargoUnit)
                    unchanged = false;
            }
            if (unchanged)
            {
                // All goods on a crossing count ONCE each crossing. No
                // duplicate coast edges or commodity-specific capacities.
                if (readings.Aggregate(BigInteger.Zero,
                        (sum, edge) => sum + edge.CargoMicro) !=
                    freight.TotalEdgeTraversalCargoMicro)
                    throw new InvalidOperationException(
                        "Congestion loads disagree with physical freight audit.");
                return new FoundationPhysicalEndogenousCongestionPlan(
                    distribution, readings, pass);
            }
            if (!observed.Add(Signature(next)))
                throw new InvalidOperationException(
                    "Congestion and physical route choices oscillated; " +
                    "do not mislabel a feedback cycle as an equilibrium.");
            current = next;
        }
        throw new InvalidOperationException(
            "Congestion cost/volume feedback did not stabilize within the " +
            "explicit iteration budget; no economic cycle was published.");
    }
}
