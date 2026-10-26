namespace GlobalArena.Simulation;

/// <summary>
/// Canonical access report with separate aggregates for reported and unknown
/// access. Raw demand for unknown entries is not added to a zero-access gap.
/// Trade allocation totals are independent of access observations.
/// </summary>
public sealed class FoodDemandAccessCoverageResult
{
    private readonly IReadOnlyList<FoodDemandAccessCoverageEntry> _entries;

    public IReadOnlyList<FoodDemandAccessCoverageEntry> Entries => _entries;

    public ulong TotalRawFoodDemand { get; }
    public ulong TotalRawTradeAllocated { get; }
    public ulong RawDemandWithKnownAccess { get; }
    public ulong RawDemandWithUnknownAccess { get; }
    public ulong RawDemandMatchedByKnownAccess { get; }
    public ulong RawDemandNotMatchedByKnownAccess { get; }

    public FoodDemandAccessCoverageResult(
        IEnumerable<FoodDemandAccessCoverageEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var rows = entries.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Food access coverage cannot contain null rows.",
                nameof(entries));
        rows = rows
            .OrderBy(row => row.EconomicPointId.Value)
            .ThenBy(row => row.CommodityId.Value)
            .ToArray();

        UInt128 totalDemand = 0U;
        UInt128 totalTrade = 0U;
        UInt128 knownDemand = 0U;
        UInt128 unknownDemand = 0U;
        UInt128 matched = 0U;
        UInt128 notMatched = 0U;

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (i > 0 &&
                rows[i - 1].EconomicPointId == row.EconomicPointId &&
                rows[i - 1].CommodityId == row.CommodityId)
                throw new ArgumentException(
                    "Food access coverage cannot duplicate point/food keys.",
                    nameof(entries));

            totalDemand += row.RawDemandQuantity;
            totalTrade += row.RawTradeAllocatedQuantity;
            if (row.HasAccessObservation)
            {
                knownDemand += row.RawDemandQuantity;
                matched += row.RawDemandMatchedByAccess!.Value;
                notMatched += row.RawDemandNotMatchedByAccess!.Value;
            }
            else
            {
                unknownDemand += row.RawDemandQuantity;
            }
        }

        if (totalDemand > ulong.MaxValue ||
            totalTrade > ulong.MaxValue ||
            knownDemand > ulong.MaxValue ||
            unknownDemand > ulong.MaxValue ||
            matched > ulong.MaxValue ||
            notMatched > ulong.MaxValue)
            throw new OverflowException(
                "Food access coverage totals exceed UInt64 capacity.");

        _entries = Array.AsReadOnly(rows);
        TotalRawFoodDemand = (ulong)totalDemand;
        TotalRawTradeAllocated = (ulong)totalTrade;
        RawDemandWithKnownAccess = (ulong)knownDemand;
        RawDemandWithUnknownAccess = (ulong)unknownDemand;
        RawDemandMatchedByKnownAccess = (ulong)matched;
        RawDemandNotMatchedByKnownAccess = (ulong)notMatched;
    }
}
