using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Pure, opt-in evaluator of smooth shared-edge transport congestion costs
/// implied by already-selected food market trades. Reuses only actually
/// selected source/destination routes and current strategic edge access.
///
/// Existing market decisions are not retroactively changed. A later
/// multi-commodity market resolver can apply the exact marginal-cost
/// functions during optimization, not after settlement.
///
/// A food-only market is required: ignoring nonfood traffic with unknown
/// physical units would misrepresent shared corridor congestion.
/// </summary>
public sealed class FoodStrategicEdgeCongestionEvaluator
{
    public FoodStrategicEdgeCongestionResult Evaluate(
        WorldGenerationResult generatedWorld,
        WorldState worldState,
        StrategicEdgeAccessSnapshot edgeAccess,
        EconomicMarketAllocationResult market,
        FoodCommodityCatalog foodCatalog,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(edgeAccess);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(foodCatalog);
        ArgumentNullException.ThrowIfNull(profiles);

        if (worldState.WorldBinding is null ||
            worldState.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Congestion evaluation requires a WorldState bound to the supplied generated world.");

        var graphEdges = generatedWorld.StrategicTopology.Edges
            .Select(edge => edge.Id).ToHashSet();
        var snapshot = profiles.ToArray();
        if (snapshot.Any(profile => profile is null))
            throw new ArgumentException(
                "Congestion profiles cannot contain null records.",
                nameof(profiles));

        var byEdge = new Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile>();
        foreach (var profile in snapshot)
        {
            if (!graphEdges.Contains(profile.StrategicEdgeId))
                throw new InvalidOperationException(
                    $"Congestion profile references edge {profile.StrategicEdgeId.Value} outside the generated world.");
            if (!byEdge.TryAdd(profile.StrategicEdgeId, profile))
                throw new ArgumentException(
                    $"Duplicate congestion profile for edge {profile.StrategicEdgeId.Value}.",
                    nameof(profiles));
        }

        var router = new StrategicEconomicRouteResolver(
            generatedWorld.StrategicSurfaceGraph);
        var routes = new Dictionary<StrategicEconomicRouteKey, StrategicEconomicRoute>();
        var sharedLoad = new Dictionary<StrategicEdgeId, BigInteger>();

        foreach (var trade in market.Allocations)
        {
            if (!foodCatalog.TryGet(trade.CommodityId, out var food))
                throw new InvalidOperationException(
                    $"Shared physical congestion evaluation cannot omit an unregistered/nonfood " +
                    $"commodity {trade.CommodityId.Value}.");

            var routeKey = new StrategicEconomicRouteKey(
                trade.SourceEconomicPointId, trade.DestinationEconomicPointId);
            if (!routes.TryGetValue(routeKey, out var route))
            {
                var resolved = router.ResolveDetailed(
                    worldState.Economy, routeKey, edgeAccess);
                if (!resolved.IsReachable)
                    throw new InvalidOperationException(
                        $"Selected trade {trade.SourceEconomicPointId.Value} -> " +
                        $"{trade.DestinationEconomicPointId.Value} has no open strategic route.");

                route = resolved.Route!;
                routes.Add(routeKey, route);
            }

            var micrograms = (BigInteger)trade.RawQuantity * food!.GramsPerFlowUnit;
            foreach (var edgeId in route.EdgePath)
            {
                if (!byEdge.ContainsKey(edgeId))
                    throw new InvalidOperationException(
                        $"Missing caller-defined congestion profile for used edge {edgeId.Value}.");

                var prior = sharedLoad.TryGetValue(edgeId, out var load)
                    ? load : BigInteger.Zero;
                sharedLoad[edgeId] = prior + micrograms;
            }
        }

        return new FoodStrategicEdgeCongestionResult(
            sharedLoad.Select(row =>
                new FoodStrategicEdgeCongestionEntry(
                    byEdge[row.Key], row.Value)));
    }
}
