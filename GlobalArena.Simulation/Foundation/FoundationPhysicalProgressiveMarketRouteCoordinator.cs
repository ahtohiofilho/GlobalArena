using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Immutable, uncommitted continuation of the two-block market/route search.
/// The best candidate is ALWAYS fully audited and conserves real worker
/// production, but is NOT a settled market or a proof of exact equilibrium.
/// </summary>
public sealed class FoundationPhysicalProgressiveMarketRouteState
{
    internal PhysicalWorkerRuntimeState Census { get; }
    internal PhysicalTacticalIncidenceMap Incidence { get; }
    internal FoundationActivityCommodityDemandPolicy DemandPolicy { get; }
    internal FoundationCommodityCargoConversionPolicy ConversionPolicy { get; }
    internal FoundationCommodityMarketPlanningQuote[] FrozenQuotes { get; }
    internal FoundationPhysicalRoutingEdgeCost[] FrozenRoads { get; }
    internal FoundationPhysicalEdgeCarryingScale[] FrozenScales { get; }
    internal int SegmentsPerMarket { get; }
    internal int MaxAugmentations { get; }
    internal int MaxRouteBatchesPerParent { get; }
    internal int MaxTotalRouteBatches { get; }

    public FoundationPhysicalGlobalConcaveMultiPathPlan BestAuditedCandidate { get; }
    public BigInteger BestPotentialScaled { get; }
    public BigInteger MinimumAcceptedGainScaled { get; }
    public int ProposalsEvaluated { get; }
    public int AcceptedMarketProposals { get; }
    public int AcceptedRouteProposals { get; }
    public int RejectedProposals { get; }
    public int CompletedPasses { get; }
    public int NextProposalIndex { get; }
    public bool ImprovedInCurrentPass { get; }
    public bool StableUnderDeclaredGainFloor { get; }
    public IReadOnlyList<BigInteger> AcceptedPotentialHistoryScaled { get; }

    internal FoundationPhysicalProgressiveMarketRouteState(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        FoundationCommodityMarketPlanningQuote[] quotes,
        FoundationPhysicalRoutingEdgeCost[] roads,
        FoundationPhysicalEdgeCarryingScale[] scales,
        int segments, int augmentations, int batchesPerParent,
        int totalBatches, BigInteger minimumGain,
        FoundationPhysicalGlobalConcaveMultiPathPlan candidate,
        BigInteger currentPotential, int proposals,
        int acceptedMarkets, int acceptedRoutes, int rejected,
        int completedPasses, int nextProposalIndex,
        bool improvedInPass, bool stable,
        BigInteger[] acceptedHistory)
    {
        Census = census;
        Incidence = incidence;
        DemandPolicy = demandPolicy;
        ConversionPolicy = conversionPolicy;
        FrozenQuotes = quotes;
        FrozenRoads = roads;
        FrozenScales = scales;
        SegmentsPerMarket = segments;
        MaxAugmentations = augmentations;
        MaxRouteBatchesPerParent = batchesPerParent;
        MaxTotalRouteBatches = totalBatches;
        MinimumAcceptedGainScaled = minimumGain;
        BestAuditedCandidate = candidate;
        BestPotentialScaled = currentPotential;
        ProposalsEvaluated = proposals;
        AcceptedMarketProposals = acceptedMarkets;
        AcceptedRouteProposals = acceptedRoutes;
        RejectedProposals = rejected;
        CompletedPasses = completedPasses;
        NextProposalIndex = nextProposalIndex;
        ImprovedInCurrentPass = improvedInPass;
        StableUnderDeclaredGainFloor = stable;
        AcceptedPotentialHistoryScaled =
            Array.AsReadOnly((BigInteger[])acceptedHistory.Clone());
    }
}

/// <summary>
/// Research-only deterministic anytime optimizer. A call expends at most the
/// declared NUMBER OF COMPLETE PROPOSAL EVALUATIONS (not a wall-clock
/// budget). Each proposal may itself execute a bounded but expensive global
/// flow and physical routing calculation. All arithmetic and audits remain
/// exact, while the search can stop and continue between planning invocations.
/// 
/// New snapshots must pass BeginOrResume: the previous state is reused ONLY
/// if immutable input objects and all frozen economic parameters still agree.
/// Changes to workers, quotes, costs, policies, or topology require a fresh
/// full audited baseline. A future gate can add a rigorously validated
/// cross-world warm start; this one must never reuse stale deliveries.
/// </summary>
public static class FoundationPhysicalProgressiveMarketRouteCoordinator
{
    public static FoundationPhysicalProgressiveMarketRouteState BeginOrResume(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int segmentsPerAverageMarket,
        int maxFlowAugmentationsPerProposal,
        int maxRouteBatchesPerParent,
        int maxTotalRouteBatches,
        BigInteger minimumAcceptedGainScaled,
        FoundationPhysicalProgressiveMarketRouteState? previous = null)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (segmentsPerAverageMarket < 1 ||
            maxFlowAugmentationsPerProposal < 1 ||
            maxRouteBatchesPerParent < 1 ||
            maxTotalRouteBatches < 1 ||
            minimumAcceptedGainScaled < BigInteger.Zero)
            throw new ArgumentOutOfRangeException(
                "All proposal budgets must be positive and the economic " +
                "minimum improvement must be nonnegative.");

        // Freeze enumerables once; no hidden change to policy/market prices
        // is allowed between proposals of the SAME search continuation.
        var quoteList = quotes.ToArray();
        var roads = freeFlowEdgeCosts.OrderBy(
            x => x?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        var scales = carryingScales.ToArray();

        if (previous is not null &&
            ReferenceEquals(previous.Census, census) &&
            ReferenceEquals(previous.Incidence, incidence) &&
            ReferenceEquals(previous.DemandPolicy, demandPolicy) &&
            ReferenceEquals(previous.ConversionPolicy, conversionPolicy) &&
            previous.SegmentsPerMarket == segmentsPerAverageMarket &&
            previous.MaxAugmentations == maxFlowAugmentationsPerProposal &&
            previous.MaxRouteBatchesPerParent == maxRouteBatchesPerParent &&
            previous.MaxTotalRouteBatches == maxTotalRouteBatches &&
            previous.MinimumAcceptedGainScaled == minimumAcceptedGainScaled &&
            previous.FrozenQuotes.SequenceEqual(quoteList) &&
            previous.FrozenRoads.SequenceEqual(roads) &&
            previous.FrozenScales.SequenceEqual(scales))
        {
            return previous;
        }

        var initial = FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(
            census, incidence, demandPolicy, conversionPolicy, quoteList,
            roads, scales, segmentsPerAverageMarket,
            maxFlowAugmentationsPerProposal, maxRouteBatchesPerParent,
            maxTotalRouteBatches);
        if (initial.AllocationAudit.TotalAvailableGoodsMicro !=
            initial.AllocationAudit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "Progressive search cannot initialize an unaccounted worker lot.");
        var potential = FoundationPhysicalJointMarketRoutePotentialCoordinator
            .IntegratedNetPotentialScaled(initial, quoteList);
        return new FoundationPhysicalProgressiveMarketRouteState(
            census, incidence, demandPolicy, conversionPolicy,
            quoteList, roads, scales, segmentsPerAverageMarket,
            maxFlowAugmentationsPerProposal, maxRouteBatchesPerParent,
            maxTotalRouteBatches, minimumAcceptedGainScaled, initial,
            potential, 0, 0, 0, 0, 0, 0, false, false,
            new[] { potential });
    }

    public static FoundationPhysicalProgressiveMarketRouteState Advance(
        FoundationPhysicalProgressiveMarketRouteState state,
        int maxProposalEvaluations)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (maxProposalEvaluations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxProposalEvaluations));
        if (state.StableUnderDeclaredGainFloor)
            return state; // No more paid work on an unchanged stable input.

        var incumbent = state.BestAuditedCandidate;
        BigInteger score = state.BestPotentialScaled;
        var history = state.AcceptedPotentialHistoryScaled.ToList();
        int attempts = state.ProposalsEvaluated;
        int acceptedMarket = state.AcceptedMarketProposals;
        int acceptedRoutes = state.AcceptedRouteProposals;
        int rejected = state.RejectedProposals;
        int completePasses = state.CompletedPasses;
        int nextBlock = state.NextProposalIndex;
        bool improved = state.ImprovedInCurrentPass;
        bool stable = false;

        for (int i = 0; i < maxProposalEvaluations; i++)
        {
            FoundationPhysicalGlobalConcaveMultiPathPlan proposed;
            if (nextBlock == 0)
            {
                // Propose changes in buyers/quantities from the last
                // ACCEPTED, already audited congestion loads.
                var loaded = incumbent.ProjectedEdges.ToDictionary(
                    edge => edge.Edge);
                var congestedRoads = state.FrozenRoads.Select(road =>
                {
                    if (!loaded.TryGetValue(road.Edge, out var reading))
                        throw new InvalidOperationException(
                            "Progressive search is missing a physical edge load.");
                    return road with
                    {
                        PhysicalCostMicroPerWholeCargoUnit =
                            reading.CongestedPhysicalCostMicroPerWholeCargoUnit
                    };
                }).ToArray();
                var global = FoundationPhysicalGlobalConcaveFlowPlanner
                    .Coordinate(state.Census, state.Incidence,
                        state.DemandPolicy, state.ConversionPolicy,
                        state.FrozenQuotes, congestedRoads,
                        state.SegmentsPerMarket, state.MaxAugmentations);
                proposed = FoundationPhysicalGlobalConcaveMultiPathPlanner
                    .RouteFixedMarket(state.Census, state.Incidence,
                        state.DemandPolicy, state.ConversionPolicy,
                        global, state.FrozenRoads, state.FrozenScales,
                        state.MaxRouteBatchesPerParent,
                        state.MaxTotalRouteBatches);
            }
            else if (nextBlock == 1)
            {
                // Independent route proposal with CURRENT accepted buyers.
                proposed = FoundationPhysicalGlobalConcaveMultiPathPlanner
                    .RouteFixedMarket(state.Census, state.Incidence,
                        state.DemandPolicy, state.ConversionPolicy,
                        incumbent.MarketAllocation, state.FrozenRoads,
                        state.FrozenScales, state.MaxRouteBatchesPerParent,
                        state.MaxTotalRouteBatches);
            }
            else
                throw new InvalidOperationException(
                    "Invalid progressive proposal continuation marker.");

            if (proposed.AllocationAudit.TotalAvailableGoodsMicro !=
                    proposed.AllocationAudit.TotalAllocatedGoodsMicro ||
                proposed.AllocationAudit.TotalAllocatedGoodsMicro !=
                    incumbent.AllocationAudit.TotalAllocatedGoodsMicro)
                throw new InvalidOperationException(
                    "A progressive economic proposal broke physical conservation.");

            var proposedScore = FoundationPhysicalJointMarketRoutePotentialCoordinator
                .IntegratedNetPotentialScaled(proposed, state.FrozenQuotes);
            // Finite economic significance: below this explicit threshold,
            // postpone the improvement. No rounding of prices or quantities.
            bool accept = proposedScore > score &&
                proposedScore - score >= state.MinimumAcceptedGainScaled;
            if (accept)
            {
                incumbent = proposed;
                score = proposedScore;
                history.Add(score);
                improved = true;
                if (nextBlock == 0)
                    acceptedMarket = checked(acceptedMarket + 1);
                else
                    acceptedRoutes = checked(acceptedRoutes + 1);
            }
            else
                rejected = checked(rejected + 1);

            attempts = checked(attempts + 1);
            nextBlock++;
            if (nextBlock == 2)
            {
                nextBlock = 0;
                completePasses = checked(completePasses + 1);
                if (!improved)
                {
                    stable = true;
                    break;
                }
                improved = false;
            }
        }
        if (history.Zip(history.Skip(1))
            .Any(pair => pair.Second <= pair.First))
            throw new InvalidOperationException(
                "Progressive search accepted non-increasing economic potential.");
        return new FoundationPhysicalProgressiveMarketRouteState(
            state.Census, state.Incidence, state.DemandPolicy,
            state.ConversionPolicy, state.FrozenQuotes, state.FrozenRoads,
            state.FrozenScales, state.SegmentsPerMarket,
            state.MaxAugmentations, state.MaxRouteBatchesPerParent,
            state.MaxTotalRouteBatches, state.MinimumAcceptedGainScaled,
            incumbent, score, attempts, acceptedMarket, acceptedRoutes,
            rejected, completePasses, nextBlock, improved, stable,
            history.ToArray());
    }
}
