namespace GlobalArena.Simulation;

/// <summary>
/// Read-only, canonical per-food-point market coverage for the current cycle.
/// An empty result says no *registered food demand flows* were evaluated; it
/// does not mean that all residents received food or have zero biological need.
/// </summary>
public sealed class FoodMarketDemandCoverageResult
{
    private readonly IReadOnlyList<FoodMarketDemandCoverageEntry> _entries;

    public IReadOnlyList<FoodMarketDemandCoverageEntry> Entries => _entries;

    public ulong TotalRawDemandQuantity { get; }
    public ulong TotalRawTradeAllocatedQuantity { get; }
    public ulong TotalRawUnallocatedQuantity { get; }

    public FoodMarketDemandCoverageResult(
        IEnumerable<FoodMarketDemandCoverageEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var snapshot = entries.ToArray();
        if (snapshot.Any(entry => entry is null))
            throw new ArgumentException(
                "Food coverage cannot contain null entries.",
                nameof(entries));

        snapshot = snapshot
            .OrderBy(entry => entry.EconomicPointId.Value)
            .ThenBy(entry => entry.CommodityId.Value)
            .ToArray();

        UInt128 totalDemand = 0U;
        UInt128 totalAllocated = 0U;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var current = snapshot[index];
            if (index > 0)
            {
                var previous = snapshot[index - 1];
                if (previous.EconomicPointId == current.EconomicPointId
                    && previous.CommodityId == current.CommodityId)
                    throw new ArgumentException(
                        "Food coverage cannot duplicate a point/commodity key.",
                        nameof(entries));
            }

            totalDemand += current.RawDemandQuantity;
            totalAllocated += current.RawTradeAllocatedQuantity;
        }

        if (totalDemand > ulong.MaxValue || totalAllocated > ulong.MaxValue)
            throw new OverflowException(
                "Total food demand or trade allocation exceeds UInt64.");

        _entries = Array.AsReadOnly(snapshot);
        TotalRawDemandQuantity = checked((ulong)totalDemand);
        TotalRawTradeAllocatedQuantity = checked((ulong)totalAllocated);
        TotalRawUnallocatedQuantity =
            TotalRawDemandQuantity - TotalRawTradeAllocatedQuantity;
    }
}
