using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class EconomicDemandFlow
{
    public EconomicPointId EconomicPointId { get; }

    public CommodityId CommodityId { get; }

    public ulong RawQuantity { get; }

    public EconomicDemandFlow(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        ulong rawQuantity)
    {
        if (!economicPointId.IsValid)
        {
            throw new ArgumentException(
                "Demand flow requires a valid economic point identity.",
                nameof(economicPointId));
        }

        if (!commodityId.IsValid)
        {
            throw new ArgumentException(
                "Demand flow requires a valid commodity identity.",
                nameof(commodityId));
        }

        if (rawQuantity == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawQuantity),
                "Demand flow quantity must be positive.");
        }

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawQuantity = rawQuantity;
    }
}
