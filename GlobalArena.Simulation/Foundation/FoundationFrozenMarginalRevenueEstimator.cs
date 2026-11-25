using System.Numerics;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationFrozenMarginalScore(
    PhysicalWorkerOccupation Occupation, byte? Crop, uint EffortMicro,
    BigInteger EstimatedCreditDeltaScaled);

public sealed record FoundationFrozenMarginalIntervalResult(
    FoundationBaseMarginalInterval Interval, IReadOnlyList<FoundationFrozenMarginalScore> Candidates);

public sealed record FoundationFrozenMarginalEstimate(
    IReadOnlyList<FoundationFrozenMarginalIntervalResult> Intervals,
    int FinancialEvaluations, int RouteTreesBuilt)
{
    public bool IsCommitted => false;
}

/// <summary>
/// Diagnostic alternative to full market clearing for every candidate.
/// One canonical baseline, no candidate market solves. Production considers
/// putting all incremental output into one reachable demand node at a time,
/// with fixed other deliveries and demand; it includes the resulting price
/// change on the owner's incumbent sales at that node. Logistics uses the
/// canonical service/quote projection on frozen baseline routes and credits
/// only freight saved by the owner's own sellers. Neither estimate models
/// general rerouting, changes in other markets or induced demand, and neither
/// is an authoritative marginal credit, settlement or automatic assignment.
/// </summary>
public static class FoundationFrozenMarginalRevenueEstimator
{
    private const ulong Micro = FoundationPhysicalEconomicAccountingProposal.CommodityMicro;
    private const uint Basis = FoundationPhysicalEconomicAccountingProposal.BasisPoints;

    private static BigInteger Price(ulong demand, ulong delivered) =>
        delivered == 0UL ? BigInteger.Zero :
            FoundationPhysicalMarginalIntegratedUtility.IntegerSqrt(
                new BigInteger(demand) * Micro * Micro / delivered);

    public static FoundationFrozenMarginalEstimate Evaluate(
        WorldState opening, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        IReadOnlyList<FoundationBaseMarginalInterval> intervals,
        FoundationAgriculturalProductionPolicy agriculture,
        FoundationOperationalFreightShadowPolicy? operationalPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(agriculture);
        operationalPolicy?.RequireValid();
        if (!ReferenceEquals(agriculture.Geography, geography) ||
            geography.FineParameters != incidence.Refinement.FineParameters ||
            (operationalPolicy is not null && !opening.WorkerOrders.IsInitialized))
            throw new InvalidOperationException("Frozen estimation requires matching signed opening geography.");
        if (intervals.Count == 0 || intervals.Any(x => x is null) || intervals.Distinct().Count() != intervals.Count)
            throw new ArgumentException("Unique available intervals required.");
        var ordered = calendar.OrderBy(s => s.Worker.Value).ThenBy(s => s.StartMicro).ToArray();
        var indices = ordered.Select((s, i) => (s, i)).ToDictionary(x => (x.s.Worker, x.s.StartMicro), x => x.i);
        var workers = opening.Workers.Workers.ToDictionary(w => w.Id);
        var requested = intervals.OrderBy(x => x.Worker.Value).ThenBy(x => x.StartMicro).ToArray();
        foreach (var request in requested)
        {
            if (!workers.ContainsKey(request.Worker) ||
                !indices.TryGetValue((request.Worker, request.StartMicro), out var index))
                throw new ArgumentException("Unknown available interval.");
            var idle = ordered[index];
            if (idle.Activity != PhysicalWorkerActivity.Other || idle.Occupation != PhysicalWorkerOccupation.Unassigned ||
                idle.ProjectId.HasValue || idle.Crop.HasValue || idle.DurationMicro == 0)
                throw new InvalidOperationException("Only unallocated effort is eligible.");
        }
        var temporal = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, ordered, policy.Demand, agriculture);
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(opening, temporal, policy.Demand);
        var baseline = FoundationTemporalFinancialReconciler.Evaluate(opening, incidence, temporal, policy, geography, opening, operationalPolicy);
        var baseRoads = FoundationPhysicalAuthoritativeTransportProjector.FromWorld(opening, incidence).Roads;
        var shadow = (baseline.OperationalQuotes ?? Array.Empty<FoundationOperationalFreightShadowEdge>())
            .ToDictionary(x => x.Edge);
        var blocked = incidence.FineTopology.Edges.Where(e =>
            geography.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1, (int)e.IncidentCellIds[1].Value - 1) ==
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
            .Select(e => new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, e.Id)).ToHashSet();
        var roads = baseRoads.Where(x => !blocked.Contains(x.Edge)).Select(x =>
            shadow.TryGetValue(x.Edge, out var quote) ? x with
            { PhysicalCostMicroPerWholeCargoUnit = quote.ShadowPhysicalCostMicroPerWholeCargoUnit } : x).ToArray();
        var rates = roads.ToDictionary(x => x.Edge, x => x.PhysicalCostMicroPerWholeCargoUnit);
        var (graph, _) = FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(incidence, roads);
        var topology = incidence.FineTopology.Edges.ToDictionary(e =>
            (Math.Min(e.IncidentCellIds[0].Value, e.IncidentCellIds[1].Value),
             Math.Max(e.IncidentCellIds[0].Value, e.IncidentCellIds[1].Value)),
            e => new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, e.Id));
        var deliveries = baseline.Distribution.Candidates;
        var delivered = deliveries.GroupBy(x => (x.BuyerTile, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL, (sum, x) => checked(sum + x.GoodsMicro)));
        var ownDelivered = deliveries.GroupBy(x => (x.Seller, x.BuyerTile, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL, (sum, x) => checked(sum + x.GoodsMicro)));
        var ownEdgeCargo = new Dictionary<(CivilizationId Owner, PhysicalTacticalEdgeId Edge), BigInteger>();
        foreach (var delivery in deliveries)
        {
            var cargo = policy.Cargo.CargoMicro(delivery.Commodity, delivery.GoodsMicro);
            for (int i = 1; i < delivery.RouteTiles.Count; i++)
            {
                ulong a = delivery.RouteTiles[i - 1].FineStrategicCellId.Value;
                ulong b = delivery.RouteTiles[i].FineStrategicCellId.Value;
                var edge = topology[(Math.Min(a, b), Math.Max(a, b))];
                var key = (delivery.Seller, edge);
                ownEdgeCargo[key] = ownEdgeCargo.GetValueOrDefault(key) + cargo;
            }
        }
        var routeCache = new Dictionary<(CivilizationId, PhysicalTacticalTileId), FoundationPhysicalTradeRouteCandidatePlanner.RouteTree>();
        var result = new List<FoundationFrozenMarginalIntervalResult>();
        foreach (var request in requested)
        {
            int index = indices[(request.Worker, request.StartMicro)];
            var idle = ordered[index];
            var worker = workers[request.Worker];
            var scores = new List<FoundationFrozenMarginalScore>();
            if (geography.SurfaceAtIndex((int)idle.Node.FineStrategicCellId.Value - 1) == FoundationFineSurfaceKind.Land)
            {
                foreach (var choice in FoundationBaseMarginalRevenueEvaluator.EligibleActivities(
                    worker, idle, incidence, geography, policy, agriculture, operationalPolicy))
                {
                    var modified = (FoundationPersistentWorkerSlice[])ordered.Clone();
                    modified[index] = idle with { Activity = choice.Activity, Occupation = choice.Role, Crop = choice.Crop };
                    var candidate = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, modified, policy.Demand, agriculture);
                    BigInteger score = BigInteger.Zero;
                    if (choice.Activity == PhysicalWorkerActivity.Producing)
                    {
                        ulong goods = checked(candidate.TotalProductionMicro - temporal.TotalProductionMicro);
                        if (goods > 0UL)
                        {
                            var commodity = choice.Crop is { } crop ? agriculture.Resolve(idle.Node, (CropId)crop).Commodity : worker.Commodity;
                            ulong cargo = policy.Cargo.CargoMicro(commodity, goods);
                            var routeKey = (worker.Owner, idle.Node);
                            if (!routeCache.TryGetValue(routeKey, out var tree))
                            {
                                tree = FoundationPhysicalTradeRouteCandidatePlanner.RouteFrom(idle.Node.FineStrategicCellId.Value,
                                    worker.Owner, graph, incidence.FineTopology.Cells.Count);
                                routeCache.Add(routeKey, tree);
                            }
                            BigInteger? best = null;
                            foreach (var target in market.MarketDemand.Where(m => m.Commodity == commodity))
                            {
                                int tile = checked((int)target.Tile.FineStrategicCellId.Value);
                                if (tree.TotalRate[tile] == ulong.MaxValue) continue;
                                ulong q = delivered.GetValueOrDefault((target.Tile, commodity));
                                ulong own = ownDelivered.GetValueOrDefault((worker.Owner, target.Tile, commodity));
                                // Cold start uses a finite D/(Q+new goods), not an
                                // invented stock, infinity or arbitrary price.
                                var priorPrice = Price(target.DemandMicro, q);
                                var nextPrice = Price(target.DemandMicro, checked(q + goods));
                                var delta = ((new BigInteger(own) + goods) * nextPrice -
                                    new BigInteger(own) * priorPrice -
                                    new BigInteger(cargo) * tree.TotalRate[tile]) * Basis;
                                if (best is null || delta > best.Value) best = delta;
                            }
                            // No reachable demand is a broken physical market
                            // contract, not a zero-value fabricated candidate.
                            score = best ?? throw new InvalidOperationException("No reachable commodity demand for eligible output.");
                        }
                    }
                    else
                    {
                        // Frozen routes: no change in seller-specific tolls,
                        // no demand-induced redistribution or owner-toll windfall.
                        var proposed = FoundationTemporalFinancialReconciler.ProjectOperationalQuotes(
                            opening, incidence, geography, candidate, baseRoads, operationalPolicy!);
                        foreach (var road in proposed.CounterfactualRoads)
                            if (rates.TryGetValue(road.Edge, out var before))
                                score += ownEdgeCargo.GetValueOrDefault((worker.Owner, road.Edge)) *
                                    (new BigInteger(before) - road.PhysicalCostMicroPerWholeCargoUnit) * Basis;
                    }
                    scores.Add(new(choice.Role, choice.Crop, idle.DurationMicro, score));
                }
            }
            result.Add(new(request, Array.AsReadOnly(scores.OrderByDescending(x => x.EstimatedCreditDeltaScaled)
                .ThenBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray())));
        }
        return new(Array.AsReadOnly(result.ToArray()), 1, routeCache.Count);
    }
}
