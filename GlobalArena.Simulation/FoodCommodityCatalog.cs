using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Optional immutable food registry. Both IDs and mass conversion scales are
/// explicit data, never inferred from declaration order.
/// </summary>
public sealed class FoodCommodityCatalog
{
    private readonly IReadOnlyList<FoodCommodityDefinition> _commodities;
    private readonly Dictionary<CommodityId, FoodCommodityDefinition> _byId;

    public IReadOnlyList<FoodCommodityDefinition> Commodities => _commodities;

    public FoodCommodityCatalog(IEnumerable<FoodCommodityDefinition> commodities)
    {
        ArgumentNullException.ThrowIfNull(commodities);

        var snapshot = commodities.ToArray();
        if (snapshot.Any(commodity => commodity is null))
            throw new ArgumentException(
                "Food catalog cannot contain null definitions.",
                nameof(commodities));

        snapshot = snapshot.OrderBy(x => x.CommodityId.Value).ToArray();

        _byId = new Dictionary<CommodityId, FoodCommodityDefinition>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var commodity in snapshot)
        {
            if (!_byId.TryAdd(commodity.CommodityId, commodity))
                throw new ArgumentException(
                    $"Duplicate food commodity identity {commodity.CommodityId.Value}.",
                    nameof(commodities));

            if (!keys.Add(commodity.StableKey))
                throw new ArgumentException(
                    $"Duplicate food stable key {commodity.StableKey}.",
                    nameof(commodities));
        }

        _commodities = Array.AsReadOnly(snapshot);
    }

    public bool TryGet(
        CommodityId commodityId,
        out FoodCommodityDefinition? definition)
    {
        if (!commodityId.IsValid)
        {
            definition = null;
            return false;
        }

        return _byId.TryGetValue(commodityId, out definition);
    }

    public bool Contains(CommodityId commodityId) =>
        TryGet(commodityId, out _);
}
