using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Pure comparison between explicitly declared food demand and independent
/// caller-provided access observations. Does not derive access from profitable
/// trade, local production, inventories or resident workforce.
/// </summary>
public sealed class FoodDemandAccessCoverageResolver
{
    public FoodDemandAccessCoverageResult Resolve(
        FoodMarketDemandCoverageResult marketCoverage,
        FoodCommodityCatalog catalog,
        FoodAccessObservationSnapshot observations,
        GlobalArena.Kernel.TurnNumber currentTurn)
    {
        ArgumentNullException.ThrowIfNull(marketCoverage);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(observations);

        if (currentTurn.Value == 0UL || observations.Turn != currentTurn)
            throw new InvalidOperationException(
                "Food access observations must belong to the resolved turn.");

        var foodDemands = marketCoverage.Entries
            .ToDictionary(row => (row.EconomicPointId, row.CommodityId));

        foreach (var row in marketCoverage.Entries)
        {
            if (!catalog.Contains(row.CommodityId))
                throw new InvalidOperationException(
                    $"Access coverage includes food commodity {row.CommodityId.Value} " +
                    "that is absent from the supplied food catalog.");
        }

        var indexed = new Dictionary<
            (EconomicPointId PointId, CommodityId CommodityId),
            FoodPointAccessObservation>();

        foreach (var observation in observations.Observations)
        {
            if (!catalog.Contains(observation.CommodityId))
                throw new InvalidOperationException(
                    $"Access observation references a nonregistered food commodity " +
                    $"{observation.CommodityId.Value}.");

            var key = (observation.EconomicPointId, observation.CommodityId);
            if (!foodDemands.ContainsKey(key))
                throw new InvalidOperationException(
                    $"Access observation has no corresponding food demand at " +
                    $"point {observation.EconomicPointId.Value}, commodity {observation.CommodityId.Value}.");

            // Snapshot already rejects duplicates and keeps canonical order.
            indexed.Add(key, observation);
        }

        return new FoodDemandAccessCoverageResult(
            marketCoverage.Entries.Select(row =>
            {
                indexed.TryGetValue(
                    (row.EconomicPointId, row.CommodityId),
                    out var observation);
                return new FoodDemandAccessCoverageEntry(row, observation);
            }));
    }
}
