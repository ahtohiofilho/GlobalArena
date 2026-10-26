using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Pure measurement of the amount of *registered food demand* matched by
/// the market's trade allocations. It cannot establish physical delivery,
/// local availability, ingestion, calorie sufficiency or health outcomes.
/// No implicit hunger or population-status state is generated.
/// </summary>
public sealed class FoodMarketDemandCoverageResolver
{
    public FoodMarketDemandCoverageResult Resolve(
        EconomicProductionDemandResult flows,
        EconomicMarketAllocationResult market,
        FoodCommodityCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(catalog);

        // The canonical M4.3 result has at most one demand flow for each
        // point/commodity; exclude other commodities from this *food* view.
        var foodDemands = flows.DemandFlows
            .Where(demand => catalog.Contains(demand.CommodityId))
            .ToDictionary(
                demand => (demand.EconomicPointId, demand.CommodityId),
                demand => demand.RawQuantity);

        var allocations = new Dictionary<
            (EconomicPointId PointId, CommodityId CommodityId), UInt128>();

        foreach (var trade in market.Allocations)
        {
            if (!catalog.Contains(trade.CommodityId))
                continue;

            var key = (trade.DestinationEconomicPointId, trade.CommodityId);
            if (!foodDemands.TryGetValue(key, out var demand))
                throw new InvalidOperationException(
                    $"Food trade allocation targets a point/commodity without an explicit food demand: " +
                    $"{trade.DestinationEconomicPointId.Value}/{trade.CommodityId.Value}.");

            var previous = allocations.TryGetValue(key, out var total)
                ? total : 0U;
            var next = previous + trade.RawQuantity;

            if (next > demand)
                throw new InvalidOperationException(
                    $"Food trade allocation exceeds demand for point {trade.DestinationEconomicPointId.Value}, " +
                    $"commodity {trade.CommodityId.Value}.");

            allocations[key] = next;
        }

        return new FoodMarketDemandCoverageResult(
            foodDemands.Select(row =>
                new FoodMarketDemandCoverageEntry(
                    row.Key.EconomicPointId,
                    row.Key.CommodityId,
                    row.Value,
                    allocations.TryGetValue(row.Key, out var raw)
                        ? checked((ulong)raw)
                        : 0UL)));
    }
}
