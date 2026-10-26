using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Opt-in, read-only audit of strategic-edge physical mass throughput
/// implied by already-selected food market allocations. Reuses the world-
/// bound graph router and current closed-edge access snapshot. Two foods on
/// the same edge share one capacity, even if the market allocated them in
/// separate per-commodity passes. No default capacities, stocks, shipments,
/// delivery outcomes, traffic modes, or repricing are manufactured.
///
/// This food-only audit deliberately rejects markets containing nonfood
/// allocations: otherwise "within shared capacity" would misleadingly
/// exclude real traffic whose physical unit conversion is unknown.
/// </summary>
public sealed class FoodStrategicEdgeCapacityAuditor
{
    public FoodStrategicEdgeCapacityAudit Audit(
        WorldGenerationResult generatedWorld,
        WorldState worldState,
        StrategicEdgeAccessSnapshot edgeAccess,
        EconomicMarketAllocationResult market,
        FoodCommodityCatalog foodCatalog,
        IEnumerable<FoodStrategicEdgeMassCapacity> capacities)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(edgeAccess);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(foodCatalog);
        ArgumentNullException.ThrowIfNull(capacities);

        if (worldState.WorldBinding is null ||
            worldState.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Transport capacity auditing requires a WorldState bound to the supplied generated world.");

        var graphEdges = generatedWorld.StrategicTopology.Edges
            .Select(edge => edge.Id).ToHashSet();

        var snapshot = capacities.ToArray();
        if (snapshot.Any(capacity => capacity is null))
            throw new ArgumentException(
                "Transport capacities cannot contain null rows.",
                nameof(capacities));

        var byEdge = new Dictionary<StrategicEdgeId, ulong>();
        foreach (var capacity in snapshot)
        {
            if (!graphEdges.Contains(capacity.StrategicEdgeId))
                throw new InvalidOperationException(
                    $"Transport capacity references edge {capacity.StrategicEdgeId.Value} outside the generated world.");
            if (!byEdge.TryAdd(capacity.StrategicEdgeId,
                capacity.GramsPerEconomicCycle))
                throw new ArgumentException(
                    $"Duplicate capacity for edge {capacity.StrategicEdgeId.Value}.",
                    nameof(capacities));
        }

        var router = new StrategicEconomicRouteResolver(
            generatedWorld.StrategicSurfaceGraph);
        var routes = new Dictionary<StrategicEconomicRouteKey, StrategicEconomicRoute>();
        var massLoad = new Dictionary<StrategicEdgeId, UInt128>();

        foreach (var trade in market.Allocations)
        {
            if (!foodCatalog.TryGet(trade.CommodityId, out var food))
                throw new InvalidOperationException(
                    $"Shared food-mass audit cannot certify a market containing unregistered/nonfood commodity {trade.CommodityId.Value}.");

            var routeKey = new StrategicEconomicRouteKey(
                trade.SourceEconomicPointId,
                trade.DestinationEconomicPointId);

            if (!routes.TryGetValue(routeKey, out var route))
            {
                var resolved = router.ResolveDetailed(
                    worldState.Economy, routeKey, edgeAccess);

                if (!resolved.IsReachable)
                    throw new InvalidOperationException(
                        $"Selected market trade {trade.SourceEconomicPointId.Value} -> " +
                        $"{trade.DestinationEconomicPointId.Value} has no open strategic route.");

                route = resolved.Route!;
                routes.Add(routeKey, route);
            }

            // quantity is in micro-units of a commodity; multiplying by
            // explicit grams/whole-unit yields exact micrograms of load.
            var rawMicrograms = checked((UInt128)trade.RawQuantity
                * food!.GramsPerFlowUnit);

            foreach (var edgeId in route.EdgePath)
            {
                if (!byEdge.ContainsKey(edgeId))
                    throw new InvalidOperationException(
                        $"Missing explicit food-mass throughput capacity for used edge {edgeId.Value}.");

                var existing = massLoad.TryGetValue(edgeId, out var value)
                    ? value
                    : 0U;
                massLoad[edgeId] = checked(existing + rawMicrograms);
            }
        }

        return new FoodStrategicEdgeCapacityAudit(
            massLoad.Select(row =>
                new FoodStrategicEdgeMassLoad(
                    row.Key,
                    row.Value,
                    checked((UInt128)byEdge[row.Key]
                        * (ulong)StrategicScalarField.Denominator))));
    }
}
