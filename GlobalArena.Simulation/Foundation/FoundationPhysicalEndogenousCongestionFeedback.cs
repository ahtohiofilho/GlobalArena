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
/// <summary>
/// Explicit numerical strategy for fixed-point SEARCH, not an economic rule.
/// Denominator 1 is the original exact Picard iteration; allowing larger
/// denominators enables controlled under-relaxation after an observed cycle.
/// This cannot guarantee a fixed point when a discrete route split cannot
/// represent one; such cycles must still fail closed.
/// </summary>
public sealed record FoundationPhysicalCongestionRelaxationPolicy(
    uint InitialDenominator, uint MaximumDenominator);

public sealed class FoundationPhysicalEndogenousCongestionPlan
{
    public FoundationPhysicalGlobalMarginalCoordinationPlan Distribution { get; }
    public IReadOnlyList<FoundationPhysicalCongestedEdgeReading> Edges { get; }
    public int CongestionIterations { get; }
    public bool ConsistentWithFinalEdgeLoads { get; }
    public uint FinalRelaxationDenominator { get; }
    public int AdaptiveRestarts { get; }

    internal FoundationPhysicalEndogenousCongestionPlan(
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution,
        FoundationPhysicalCongestedEdgeReading[] edges, int iterations,
        uint relaxationDenominator, int adaptiveRestarts)
    {
        Distribution = distribution;
        Edges = Array.AsReadOnly(edges);
        CongestionIterations = iterations;
        FinalRelaxationDenominator = relaxationDenominator;
        AdaptiveRestarts = adaptiveRestarts;
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
        // Deterministic nearest-integer quantization. The old ceiling
        // charged one micro-unit for EVERY positive load, even if the true
        // quartic cost was less than one trillionth of a micro-unit.
        // This is a numerical rule, not a minimum congestion toll.
        BigInteger surcharge = (new BigInteger(freePhysicalCostMicro) * load4
            + cap4 / 2) / cap4;
        BigInteger total = new BigInteger(freePhysicalCostMicro) + surcharge;
        if (total > ulong.MaxValue)
            throw new OverflowException(
                "Physical congestion rate exceeds the micro-unit domain; " +
                "do not clamp an overloaded infrastructure corridor.");
        return checked((ulong)total);
    }

    /// <summary>
    /// Relax the PHYSICAL COST iteration by an explicitly chosen positive
    /// denominator. Each changed cost advances at least one micro-unit, so
    /// a stationary update cannot be mistaken for exact convergence.
    /// </summary>
    internal static ulong RelaxCostTowards(
        ulong current, ulong target, uint denominator)
    {
        if (denominator == 0U)
            throw new ArgumentOutOfRangeException(nameof(denominator));
        if (current == target)
            return current;
        var delta = current < target ? target - current : current - target;
        var step = delta / denominator +
            (delta % denominator == 0UL ? 0UL : 1UL);
        return current < target ? checked(current + step) :
            checked(current - step);
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
        int maxCongestionIterations,
        FoundationPhysicalCongestionRelaxationPolicy? relaxation = null)
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
        relaxation ??= new FoundationPhysicalCongestionRelaxationPolicy(1U, 1U);
        if (relaxation.InitialDenominator == 0U ||
            relaxation.MaximumDenominator < relaxation.InitialDenominator)
            throw new ArgumentOutOfRangeException(nameof(relaxation),
                "Require 1 <= initial denominator <= maximum denominator.");

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
        uint denominator = relaxation.InitialDenominator;
        int adaptiveRestarts = 0;
        // Detect exact repeats of the physical cost vector only; when a
        // higher damping level is authorized, restart deterministically from
        // the original free-flow policy rather than carrying a partial cycle.
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
                    PhysicalCostMicroPerWholeCargoUnit =
                        RelaxCostTowards(current[i].PhysicalCostMicroPerWholeCargoUnit,
                            rate, denominator)
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
                    distribution, readings, pass,
                    denominator, adaptiveRestarts);
            }
            if (!observed.Add(Signature(next)))
            {
                if (denominator == relaxation.MaximumDenominator)
                    throw new InvalidOperationException(
                        "Congestion routes oscillated even at the explicitly " +
                        "allowed relaxation level. No economic settlement.");
                denominator = checked((uint)Math.Min(
                    (ulong)relaxation.MaximumDenominator,
                    (ulong)denominator * 2UL));
                adaptiveRestarts = checked(adaptiveRestarts + 1);
                current = baseEdges;
                observed.Clear();
                observed.Add(Signature(current));
                continue;
            }
            current = next;
        }
        throw new InvalidOperationException(
            "Congestion cost/volume feedback did not stabilize within the " +
            "explicit iteration budget; no economic cycle was published.");
    }
}
