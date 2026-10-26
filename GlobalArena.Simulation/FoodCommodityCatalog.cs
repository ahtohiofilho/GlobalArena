using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Optional immutable food registry. No commodity ID is preallocated or
/// inferred from its position in this collection.
/// </summary>
public sealed class FoodCommodityCatalog
{
    private readonly IReadOnlyList<FoodCommodityDefinition> _commodities;
    private readonly HashSet<CommodityId> _ids;

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

        _ids = new HashSet<CommodityId>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var commodity in snapshot)
        {
            if (!_ids.Add(commodity.CommodityId))
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

    public bool Contains(CommodityId commodityId) =>
        commodityId.IsValid && _ids.Contains(commodityId);
}
