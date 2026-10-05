using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class EconomicSupplyFlow
{
    public EconomicPointId EconomicPointId { get; }

    public CommodityId CommodityId { get; }

    public EconomicActivityKind Activity { get; }

    public ulong RawQuantity { get; }

    public EconomicSupplyFlow(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        EconomicActivityKind activity,
        ulong rawQuantity)
    {
        if (!economicPointId.IsValid)
        {
            throw new ArgumentException(
                "Supply flow requires a valid economic point identity.",
                nameof(economicPointId));
        }

        if (!commodityId.IsValid)
        {
            throw new ArgumentException(
                "Supply flow requires a valid commodity identity.",
                nameof(commodityId));
        }

        if (activity != EconomicActivityKind.Agriculture
            && activity != EconomicActivityKind.Mining)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activity),
                "Supply flow must originate from Agriculture or Mining.");
        }

        if (rawQuantity == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawQuantity),
                "Supply flow quantity must be positive.");
        }

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        Activity = activity;
        RawQuantity = rawQuantity;
    }
}
