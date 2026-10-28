using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-supplied producer reservation cost per whole commodity flow unit.
/// This is separate from demand-side market valuation and freight pricing.
/// It is a baseline reference input, not a game-wide producer price model.
/// </summary>
public sealed class EconomicTurnSourceReservation
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public EconomicActivityKind Activity { get; }
    public ulong RawUnitReservationCost { get; }

    public EconomicTurnSourceReservation(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        EconomicActivityKind activity,
        ulong rawUnitReservationCost)
    {
        if (!economicPointId.IsValid || !commodityId.IsValid)
            throw new ArgumentException("Reservation requires valid producer and commodity.");
        if (activity != EconomicActivityKind.Agriculture &&
            activity != EconomicActivityKind.Mining &&
            activity != EconomicActivityKind.Forestry)
            throw new ArgumentOutOfRangeException(nameof(activity));
        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        Activity = activity;
        RawUnitReservationCost = rawUnitReservationCost;
    }
}
