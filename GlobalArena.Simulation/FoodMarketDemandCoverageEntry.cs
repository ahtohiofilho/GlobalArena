using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Demand for one registered food at one economic point and the fraction
/// allocated to it by the market in this economic cycle. This is a trade
/// coverage metric, not food delivered, accessible, eaten or nutritionally
/// sufficient. All quantities are in the food's raw commodity flow units.
/// </summary>
public sealed class FoodMarketDemandCoverageEntry
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }

    public ulong RawDemandQuantity { get; }
    public ulong RawTradeAllocatedQuantity { get; }
    public ulong RawUnallocatedQuantity =>
        RawDemandQuantity - RawTradeAllocatedQuantity;

    public bool IsFullyTradeAllocated =>
        RawUnallocatedQuantity == 0UL;

    public FoodMarketDemandCoverageEntry(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        ulong rawDemandQuantity,
        ulong rawTradeAllocatedQuantity)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Food coverage requires an economic point.",
                nameof(economicPointId));
        if (!commodityId.IsValid)
            throw new ArgumentException(
                "Food coverage requires a commodity.",
                nameof(commodityId));
        if (rawDemandQuantity == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(rawDemandQuantity),
                "Food coverage needs a positive existing demand flow.");
        if (rawTradeAllocatedQuantity > rawDemandQuantity)
            throw new ArgumentOutOfRangeException(
                nameof(rawTradeAllocatedQuantity),
                "Market cannot cover more than the corresponding food demand.");

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawDemandQuantity = rawDemandQuantity;
        RawTradeAllocatedQuantity = rawTradeAllocatedQuantity;
    }
}
