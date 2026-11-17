using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// An opt-in, uncommitted exact cost/load agreement between global SEGMENTED
/// seller-market allocation and real physical-crossing quartic congestion.
/// It neither establishes the continuous-utility optimum nor the Wardrop
/// / multipath congestion optimum, economic settlement, or monetary revenue.
/// </summary>
public sealed class FoundationPhysicalGlobalConcaveCongestionPlan
{
    public FoundationPhysicalGlobalConcaveFlowPlan Distribution { get; }
    public IReadOnlyList<FoundationPhysicalCongestedEdgeReading> Edges { get; }
    public IReadOnlyList<FoundationPhysicalRoutingEdgeCost> ConsistentEdgeCosts { get; }
    public int FeedbackIterations { get; }
    public int TotalFlowAugmentations { get; }
    public bool ExactCostAndLoadAgreement { get; }

    internal FoundationPhysicalGlobalConcaveCongestionPlan(
        FoundationPhysicalGlobalConcaveFlowPlan distribution,
        FoundationPhysicalCongestedEdgeReading[] edges,
        FoundationPhysicalRoutingEdgeCost[] costs,
        int iterations, int augmentations)
    {
        Distribution = distribution;
        Edges = Array.AsReadOnly(edges);
        ConsistentEdgeCosts = Array.AsReadOnly(costs);
        FeedbackIterations = iterations;
        TotalFlowAugmentations = augmentations;
        ExactCostAndLoadAgreement = true;
    }
}

/// <summary>
/// Bounded FIXED-POINT SEARCH for globally coordinated sellers with endogenous
/// freight rates, on SINGLE shortest owner-aware route per real delivery.
/// Every pass globally re-solves all sellers with current per-edge rates,
/// audits the physical crossings, and independently derives the quartic rate
/// on each shared crossing from the complete real freight. Success requires
/// exact equality of ALL quoted physical rates with those induced by the
/// SAME final audited loads; cycles, overflow and budget exhaustion fail
/// CLOSED without returning a market plan. No hidden damping or capacity cap.
///
/// Note that fixed-cost global optimality of the segmented buyer allocation
/// is conditional on rates held fixed during each solve. Coupled
/// quantity/path equilibrium and globally optimal multipath loading remain
/// separately gated by #395/#396 and future work.
/// </summary>
public static class FoundationPhysicalGlobalConcaveCongestionFeedback
{
    public static FoundationPhysicalGlobalConcaveCongestionPlan Coordinate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int segmentsPerAverageMarket,
        int maxFlowAugmentationsPerSolve,
        int maxFeedbackIterations)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (segmentsPerAverageMarket < 1)
            throw new ArgumentOutOfRangeException(nameof(segmentsPerAverageMarket));
        if (maxFlowAugmentationsPerSolve < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFlowAugmentationsPerSolve));
        if (maxFeedbackIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFeedbackIterations));

        // Freeze all external inputs before any feedback cycle. Topology,
        // ownership and the physical costs of the original free-flow roads
        // never change in the caller or the authoritative v11 snapshot.
        var quoteList = quotes.ToArray();
        var original = freeFlowEdgeCosts.OrderBy(
            edge => edge?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
            incidence, original);

        var scales = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var scale in carryingScales)
        {
            if (scale is null || !scale.Edge.IsValid ||
                scale.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                scale.CargoMicroAtReferenceLoad == 0UL ||
                !scales.TryAdd(scale.Edge, scale.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "Each carrying scale must uniquely cover a real physical " +
                    "crossing, with positive cargo micro-units.",
                    nameof(carryingScales));
        }
        if (scales.Count != original.Length ||
            original.Any(edge => !scales.ContainsKey(edge.Edge)))
            throw new ArgumentException(
                "Carrying scale must cover ALL and ONLY declared traversable " +
                "physical crossings; no road or capacity is inferred.",
                nameof(carryingScales));

        var current = original;
        var visited = new HashSet<string>();
        string Signature(IReadOnlyList<FoundationPhysicalRoutingEdgeCost> costs) =>
            string.Join(",", costs.Select(edge =>
                edge.PhysicalCostMicroPerWholeCargoUnit.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        visited.Add(Signature(current));
        int totalAugmentations = 0;

        for (int pass = 1; pass <= maxFeedbackIterations; pass++)
        {
            var distribution = FoundationPhysicalGlobalConcaveFlowPlanner
                .Coordinate(census, incidence, demandPolicy, conversionPolicy,
                    quoteList, current, segmentsPerAverageMarket,
                    maxFlowAugmentationsPerSolve);
            totalAugmentations = checked(totalAugmentations +
                distribution.FlowAugmentations);
            var freight = distribution.AllocationAudit.Freight;
            var loads = freight.UsedEdges.ToDictionary(
                used => used.Edge, used => used.TotalCargoMicro);

            var next = new FoundationPhysicalRoutingEdgeCost[original.Length];
            var readings = new FoundationPhysicalCongestedEdgeReading[
                original.Length];
            bool exact = true;
            for (int i = 0; i < original.Length; i++)
            {
                var road = original[i];
                ulong load = loads.GetValueOrDefault(road.Edge);
                ulong reference = scales[road.Edge];
                ulong rate =
                    FoundationPhysicalEndogenousCongestionFeedback
                        .CalculateCongestedRate(
                            road.PhysicalCostMicroPerWholeCargoUnit,
                            load, reference);
                next[i] = road with
                {
                    PhysicalCostMicroPerWholeCargoUnit = rate
                };
                readings[i] = new FoundationPhysicalCongestedEdgeReading(
                    road.Edge, load, reference,
                    road.PhysicalCostMicroPerWholeCargoUnit, rate,
                    road.OwnershipKind, road.InfrastructureOwner,
                    road.ExternalTariffMicroPerWholeCargoUnit);
                if (current[i].PhysicalCostMicroPerWholeCargoUnit != rate)
                    exact = false;
            }

            if (readings.Aggregate(BigInteger.Zero,
                    (sum, edge) => sum + edge.CargoMicro) !=
                freight.TotalEdgeTraversalCargoMicro)
                throw new InvalidOperationException(
                    "Projected congestion load disagrees with the " +
                    "independent real physical freight audit.");

            if (exact)
                return new FoundationPhysicalGlobalConcaveCongestionPlan(
                    distribution, readings, next, pass,
                    totalAugmentations);

            if (!visited.Add(Signature(next)))
                throw new InvalidOperationException(
                    "Global concave-flow congestion feedback revisited a " +
                    "physical cost vector and oscillated; no partial " +
                    "candidate delivery or economic cycle is published.");
            current = next;
        }

        throw new InvalidOperationException(
            "Global concave-flow congestion did not reach exact physical " +
            "cost/load consistency within the iteration budget; no " +
            "candidate allocation or economic settlement is published.");
    }
}
