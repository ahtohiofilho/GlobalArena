using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Access comparison for one existing food demand row. Null access metrics
/// mean no caller observation was supplied. An explicit zero observation is
/// different and permits a zero-access comparison. This is not hunger or
/// nutritional sufficiency.
/// </summary>
public sealed class FoodDemandAccessCoverageEntry
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawDemandQuantity { get; }
    public ulong RawTradeAllocatedQuantity { get; }

    public bool HasAccessObservation { get; }
    public ulong? RawAccessibleQuantity { get; }
    public ulong? RawDemandMatchedByAccess { get; }
    public ulong? RawDemandNotMatchedByAccess { get; }
    public ulong? RawAccessibleBeyondDemand { get; }
    public string? EvidenceSourceKey { get; }

    public FoodDemandAccessCoverageEntry(
        FoodMarketDemandCoverageEntry tradeCoverage,
        FoodPointAccessObservation? observation)
    {
        ArgumentNullException.ThrowIfNull(tradeCoverage);

        EconomicPointId = tradeCoverage.EconomicPointId;
        CommodityId = tradeCoverage.CommodityId;
        RawDemandQuantity = tradeCoverage.RawDemandQuantity;
        RawTradeAllocatedQuantity = tradeCoverage.RawTradeAllocatedQuantity;

        if (observation is null)
            return;

        if (observation.EconomicPointId != EconomicPointId ||
            observation.CommodityId != CommodityId)
            throw new ArgumentException(
                "Access observation must match the food demand's point/commodity key.",
                nameof(observation));

        HasAccessObservation = true;
        RawAccessibleQuantity = observation.RawAccessibleQuantity;
        RawDemandMatchedByAccess = Math.Min(
            RawDemandQuantity, observation.RawAccessibleQuantity);
        RawDemandNotMatchedByAccess =
            RawDemandQuantity - RawDemandMatchedByAccess.Value;
        RawAccessibleBeyondDemand =
            observation.RawAccessibleQuantity - RawDemandMatchedByAccess.Value;
        EvidenceSourceKey = observation.EvidenceSourceKey;
    }
}
