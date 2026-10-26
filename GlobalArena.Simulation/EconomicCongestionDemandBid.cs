using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit value per whole commodity unit (raw monetary fixed-point) for
/// one existing demand point and food commodity. No prices are inferred.
/// </summary>
public sealed class EconomicCongestionDemandBid
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawUnitValue { get; }

    public EconomicCongestionDemandBid(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        ulong rawUnitValue)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException("Demand bid requires a valid economic point.", nameof(economicPointId));
        if (!commodityId.IsValid)
            throw new ArgumentException("Demand bid requires a valid commodity.", nameof(commodityId));
        if (rawUnitValue == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawUnitValue));

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawUnitValue = rawUnitValue;
    }
}
