using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// How the CURRENT economic-input epoch obtained its initial audited
/// candidate. This is provenance, not an optimization/convergence claim.
/// </summary>
public enum FoundationPhysicalProgressiveSeedKind
{
    FullGlobalSolve = 0,
    AuditedQuoteRevaluation = 1,
    ReauditedFixedMarketRouting = 2
}

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
    /// <summary>
    /// Rebased plans retain historical segmented market metadata. Only
    /// BestPotentialScaled is current-epoch economic potential; a warm seed
    /// is NOT a newly optimized global market flow at changed quotes.
    /// </summary>
    public FoundationPhysicalProgressiveSeedKind SeedKind { get; }
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
        BigInteger[] acceptedHistory,
        FoundationPhysicalProgressiveSeedKind seedKind =
            FoundationPhysicalProgressiveSeedKind.FullGlobalSolve)
    {
        SeedKind = seedKind;
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
/// New snapshots must pass BeginOrResume. Identical input resumes exactly.
/// Changed quotes with unchanged demand-market identities can reuse a fully
/// REAUDITED fixed physical candidate; changed edge rates/scales on the same
/// physical network reroute fixed quantities under the NEW road ownership and
/// congestion before starting the new epoch. Neither path runs global flow.
/// Changed workers, topology, policies, market identities, or network edge
/// sets force a fresh full global solve. A warm start is NOT a cross-epoch
/// convergence certificate and NEVER publishes economic settlement.
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
        if (quoteList.Any(quote => quote is null ||
            quote.QuoteMicroPerWholeCommodityUnit == 0UL))
            throw new ArgumentException(
                "All real demand markets require positive, explicit quotes.",
                nameof(quotes));
        var roads = freeFlowEdgeCosts.OrderBy(
            x => x?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        var scales = carryingScales.ToArray();

        // Only a truly immutable worker/topology/policy snapshot may seed
        // this new epoch. Workers are physical individuals, not counts:
        // even one changed worker object forces a full new market solve.
        bool samePhysicalInputs = previous is not null &&
            ReferenceEquals(previous.Census, census) &&
            ReferenceEquals(previous.Incidence, incidence) &&
            ReferenceEquals(previous.DemandPolicy, demandPolicy) &&
            ReferenceEquals(previous.ConversionPolicy, conversionPolicy) &&
            previous.SegmentsPerMarket == segmentsPerAverageMarket &&
            previous.MaxAugmentations == maxFlowAugmentationsPerProposal &&
            previous.MaxRouteBatchesPerParent == maxRouteBatchesPerParent &&
            previous.MaxTotalRouteBatches == maxTotalRouteBatches &&
            previous.MinimumAcceptedGainScaled == minimumAcceptedGainScaled;

        // Repricing may change marginal utility but MUST NOT silently add,
        // remove, replace or reorder any real worker-backed demand market.
        bool sameMarkets = samePhysicalInputs &&
            previous!.FrozenQuotes.Length == quoteList.Length &&
            previous.FrozenQuotes.Zip(quoteList).All(pair =>
                pair.First is not null && pair.Second is not null &&
                pair.First.BuyerTile == pair.Second.BuyerTile &&
                pair.First.Buyer == pair.Second.Buyer &&
                pair.First.Commodity == pair.Second.Commodity);

        bool sameRoads = sameMarkets &&
            previous!.FrozenRoads.SequenceEqual(roads);
        bool sameScales = sameMarkets &&
            previous!.FrozenScales.SequenceEqual(scales);
        if (sameMarkets && sameRoads && sameScales &&
            previous!.FrozenQuotes.SequenceEqual(quoteList))
            return previous; // Same epoch: retain the unfinished block.

        FoundationPhysicalGlobalConcaveMultiPathPlan? warm = null;
        var seedKind = FoundationPhysicalProgressiveSeedKind.FullGlobalSolve;
        if (sameMarkets && sameRoads && sameScales)
        {
            // Prices/quotes changed, but workers, destinations, physical
            // network, tariffs and congestion scales are literally unchanged.
            // Reaudit the former best candidate, then value its SAME
            // quantities and freight under the new exact market quotes.
            // Do not preserve previous potential history or stability flag.
            warm = previous!.BestAuditedCandidate;
            ReauditUnchangedPhysicalCandidate(
                census, incidence, demandPolicy, conversionPolicy,
                roads, warm);
            seedKind = FoundationPhysicalProgressiveSeedKind
                .AuditedQuoteRevaluation;
        }
        else if (sameMarkets && previous!.FrozenRoads.Length == roads.Length &&
            previous.FrozenRoads.Zip(roads).All(pair =>
                pair.First is not null && pair.Second is not null &&
                pair.First.Edge == pair.Second.Edge))
        {
            // Same connected physical edge set, but rates/ownership/tariffs
            // or cargo scales changed. Recalculate ALL routes, quartic costs,
            // ownership charges and freight audit under the NEW inputs while
            // preserving previously allocated buyer goods. No global solve.
            // If the old fixed-market response is infeasible, fail closed:
            // never return shipments with an unaudited network.
            warm = FoundationPhysicalGlobalConcaveMultiPathPlanner
                .RouteFixedMarket(census, incidence, demandPolicy,
                    conversionPolicy,
                    previous.BestAuditedCandidate.MarketAllocation,
                    roads, scales, maxRouteBatchesPerParent,
                    maxTotalRouteBatches);
            seedKind = FoundationPhysicalProgressiveSeedKind
                .ReauditedFixedMarketRouting;
        }

        var initial = warm ?? FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(
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
            new[] { potential }, seedKind);
    }

    private static void ReauditUnchangedPhysicalCandidate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IReadOnlyList<FoundationPhysicalRoutingEdgeCost> roads,
        FoundationPhysicalGlobalConcaveMultiPathPlan candidate)
    {
        var (_, owners) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, roads);
        var audited = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            candidate.Parts.Select(part => part.Delivery), owners);
        if (audited.TotalAvailableGoodsMicro !=
                candidate.AllocationAudit.TotalAvailableGoodsMicro ||
            audited.TotalAllocatedGoodsMicro !=
                candidate.AllocationAudit.TotalAllocatedGoodsMicro ||
            audited.TotalAvailableGoodsMicro !=
                audited.TotalAllocatedGoodsMicro ||
            audited.Freight.TotalEdgeTraversalCargoMicro !=
                candidate.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro)
            throw new InvalidOperationException(
                "Quote-only rebase failed independent physical conservation.");

        var loaded = audited.Freight.UsedEdges.ToDictionary(
            edge => edge.Edge, edge => edge.TotalCargoMicro);
        var auditedOwnerEdges = owners.Select(owner => owner.Edge).ToHashSet();
        if (candidate.ProjectedEdges.Count != roads.Count ||
            candidate.ProjectedEdges.Any(edge =>
                !auditedOwnerEdges.Contains(edge.Edge) ||
                loaded.GetValueOrDefault(edge.Edge) != edge.CargoMicro))
            throw new InvalidOperationException(
                "Quote-only rebase mismatched the independently audited freight.");
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
            history.ToArray(), state.SeedKind);
    }
}
