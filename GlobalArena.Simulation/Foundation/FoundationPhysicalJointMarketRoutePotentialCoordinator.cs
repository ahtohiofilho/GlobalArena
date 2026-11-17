using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Monotone candidate-only block-search of global buyer quantities AND real
/// multipath freight on one shared integrated economic potential. A complete
/// pass with no strictly improving proposal establishes ONLY stability under
/// the explicitly declared block proposals, not continuous global optimality,
/// Wardrop equilibrium, exact price clearing or monetary settlement.
/// </summary>
public sealed class FoundationPhysicalJointMarketRoutePotentialPlan
{
    public FoundationPhysicalGlobalConcaveMultiPathPlan Allocation { get; }
    public int CompletePasses { get; }
    public int AcceptedMarketRouteProposals { get; }
    public int AcceptedRouteOnlyProposals { get; }
    public int RejectedNonImprovingProposals { get; }
    public IReadOnlyList<BigInteger> AcceptedPotentialHistoryScaled { get; }
    public bool StableUnderOneCompleteBlockPass { get; }
    public BigInteger FinalIntegratedNetPotentialScaled =>
        AcceptedPotentialHistoryScaled[^1];

    internal FoundationPhysicalJointMarketRoutePotentialPlan(
        FoundationPhysicalGlobalConcaveMultiPathPlan allocation,
        int passes, int marketAccepts, int routeAccepts,
        int rejected, BigInteger[] history)
    {
        Allocation = allocation;
        CompletePasses = passes;
        AcceptedMarketRouteProposals = marketAccepts;
        AcceptedRouteOnlyProposals = routeAccepts;
        RejectedNonImprovingProposals = rejected;
        AcceptedPotentialHistoryScaled = Array.AsReadOnly(history);
        StableUnderOneCompleteBlockPass = true;
    }
}

/// <summary>
/// An exhausted budget is NOT a zero-gain fixed point. Partial route/market
/// candidates are deliberately not returned to the caller.
/// </summary>
public sealed class FoundationPhysicalJointMarketRouteBudgetException
    : InvalidOperationException
{
    public int CompletePassBudget { get; }
    public int AcceptedProposals { get; }
    /// <summary>
    /// Immutable diagnostic scores of accepted improvements only. These
    /// contain no worker locations, shipments, plan or settled sales.
    /// </summary>
    public IReadOnlyList<BigInteger> AcceptedPotentialHistoryScaled { get; }

    internal FoundationPhysicalJointMarketRouteBudgetException(
        int budget, int accepted, BigInteger[] acceptedPotentialHistory)
        : base("Joint market/route improvement did not complete a " +
            "non-improving full block pass within the declared pass budget; " +
            "no partial candidate or settled economic cycle was returned.")
    {
        CompletePassBudget = budget;
        AcceptedProposals = accepted;
        ArgumentNullException.ThrowIfNull(acceptedPotentialHistory);
        if (acceptedPotentialHistory.Length != checked(accepted + 1))
            throw new InvalidOperationException(
                "Budget diagnostic must contain the initial potential and " +
                "each accepted strict improvement only.");
        AcceptedPotentialHistoryScaled =
            Array.AsReadOnly((BigInteger[])acceptedPotentialHistory.Clone());
    }
}

/// <summary>
/// Joint potential: sum of the REAL market integrated marginal benefit
/// 2*quote*sqrt(demand*Q), minus integrated quartic physical freight across
/// each single shared crossing and seller-specific third-party tariffs.
///
/// Both terms use the exact same integer comparison units: buyer benefit
/// from #397/#398 is fixed-point with 64 fractional bits, and physical freight
/// from #396 is multiplied by 2^64. The selected state is ALWAYS independently
/// audited and 100% of each worker lot must have a buyer.
///
/// Block A: recompute globally competing producer allocations at the current
/// *audited* congested edge rates, then plan/validate multipath under ORIGINAL
/// free-flow physical rates. Block B: reroute the incumbent market quantities
/// under the same immutable free-flow physical network. Accept either only
/// if the complete potential strictly increases. A rejected proposal never
/// changes the incumbent. Therefore accepted potentials are strictly monotone.
/// Unlike #399, a changed congestion-rate vector is not required to equal
/// the next cost vector; this is a potential ascent policy, not Picard.
/// </summary>
public static class FoundationPhysicalJointMarketRoutePotentialCoordinator
{
    private const int FractionalBits = 64;
    private const ulong Micro = FoundationPhysicalMarginalMarketDistributor.Micro;

    internal static BigInteger IntegratedNetPotentialScaled(
        FoundationPhysicalGlobalConcaveMultiPathPlan plan,
        IReadOnlyList<FoundationCommodityMarketPlanningQuote> quotes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(quotes);
        var quoteMap = quotes.ToDictionary(
            x => (x.BuyerTile, x.Buyer, x.Commodity),
            x => x.QuoteMicroPerWholeCommodityUnit);
        if (quoteMap.Count != plan.MarketAllocation.MarketTotals.Count)
            throw new InvalidOperationException(
                "Every real physical demand market must have one quote.");
        BigInteger benefit = BigInteger.Zero;
        foreach (var market in plan.MarketAllocation.MarketTotals)
        {
            if (!quoteMap.TryGetValue(
                    (market.Tile, market.Buyer, market.Commodity),
                    out ulong quote))
                throw new InvalidOperationException(
                    "Integrated objective is missing a real market quote.");
            benefit += FoundationPhysicalMarginalIntegratedUtility
                .MarketBenefitScaled(quote, market.DemandMicro,
                    market.AllocatedGoodsMicro);
        }
        // MarketBenefitScaled includes the existing 1e6 comparison factor.
        // The fixed-cost producer planner subtracts cargoPerWhole * rate
        // per goods-micro, whereas the integrated freight audit sums
        // actual cargoMicro * rate. Multiply that physical potential by
        // Micro so BOTH terms share the same legacy canonical units.
        // This is not money charged or settled.
        return benefit - ((plan.FinalIntegratedFreightCost * Micro) <<
            FractionalBits);
    }

    public static FoundationPhysicalJointMarketRoutePotentialPlan Coordinate(
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
        int maxCompletePasses)
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
            maxTotalRouteBatches < 1 || maxCompletePasses < 1)
            throw new ArgumentOutOfRangeException(
                "All joint market and route proposal budgets must be positive.");

        // Freeze all policies exactly once; never let a non-repeatable
        // IEnumerable change market prices or the physical topology mid-pass.
        var quoteList = quotes.ToArray();
        var baseRoads = freeFlowEdgeCosts.OrderBy(x =>
            x?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        var scales = carryingScales.ToArray();

        var incumbent = FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(
            census, incidence, demandPolicy, conversionPolicy, quoteList,
            baseRoads, scales, segmentsPerAverageMarket,
            maxFlowAugmentationsPerProposal, maxRouteBatchesPerParent,
            maxTotalRouteBatches);
        var history = new List<BigInteger>
        {
            IntegratedNetPotentialScaled(incumbent, quoteList)
        };
        int acceptedMarket = 0;
        int acceptedRoutes = 0;
        int rejected = 0;

        for (int pass = 1; pass <= maxCompletePasses; pass++)
        {
            bool increased = false;

            // Already audited physical cargo on every edge is authoritative
            // for the prospective rates used to solve this block, but not
            // published as a settled price or exact congestion equilibrium.
            var loadMap = incumbent.ProjectedEdges.ToDictionary(
                edge => edge.Edge);
            var marginalRoads = baseRoads.Select(road =>
            {
                if (!loadMap.TryGetValue(road.Edge, out var loaded))
                    throw new InvalidOperationException(
                        "Joint proposal is missing an audited physical crossing.");
                return road with
                {
                    PhysicalCostMicroPerWholeCargoUnit =
                        loaded.CongestedPhysicalCostMicroPerWholeCargoUnit
                };
            }).ToArray();

            var proposedMarkets = FoundationPhysicalGlobalConcaveFlowPlanner
                .Coordinate(census, incidence, demandPolicy, conversionPolicy,
                    quoteList, marginalRoads, segmentsPerAverageMarket,
                    maxFlowAugmentationsPerProposal);
            var marketAndRoutes =
                FoundationPhysicalGlobalConcaveMultiPathPlanner.RouteFixedMarket(
                    census, incidence, demandPolicy, conversionPolicy,
                    proposedMarkets, baseRoads, scales,
                    maxRouteBatchesPerParent, maxTotalRouteBatches);
            var proposedScore = IntegratedNetPotentialScaled(
                marketAndRoutes, quoteList);
            if (proposedScore > history[^1])
            {
                incumbent = marketAndRoutes;
                history.Add(proposedScore);
                acceptedMarket = checked(acceptedMarket + 1);
                increased = true;
            }
            else
                rejected = checked(rejected + 1);

            // Independent improvement proposal for the CURRENT fixed buyers.
            // A same-market reroute never changes supply, quotes or ownership.
            var routeOnly =
                FoundationPhysicalGlobalConcaveMultiPathPlanner.RouteFixedMarket(
                    census, incidence, demandPolicy, conversionPolicy,
                    incumbent.MarketAllocation, baseRoads, scales,
                    maxRouteBatchesPerParent, maxTotalRouteBatches);
            var routeScore = IntegratedNetPotentialScaled(routeOnly, quoteList);
            if (routeScore > history[^1])
            {
                incumbent = routeOnly;
                history.Add(routeScore);
                acceptedRoutes = checked(acceptedRoutes + 1);
                increased = true;
            }
            else
                rejected = checked(rejected + 1);

            if (!increased)
            {
                if (history.Zip(history.Skip(1))
                    .Any(pair => pair.Second <= pair.First))
                    throw new InvalidOperationException(
                        "Joint potential accepted a non-improving proposal.");
                if (incumbent.AllocationAudit.TotalAvailableGoodsMicro !=
                    incumbent.AllocationAudit.TotalAllocatedGoodsMicro)
                    throw new InvalidOperationException(
                        "Joint proposal failed to conserve all real worker supply.");
                return new FoundationPhysicalJointMarketRoutePotentialPlan(
                    incumbent, pass, acceptedMarket, acceptedRoutes,
                    rejected, history.ToArray());
            }
        }

        throw new FoundationPhysicalJointMarketRouteBudgetException(
            maxCompletePasses, checked(acceptedMarket + acceptedRoutes),
            history.ToArray());
    }
}
