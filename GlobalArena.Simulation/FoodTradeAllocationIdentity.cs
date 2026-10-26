using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Internal contract key matching the accepted market allocation identity.
/// Quantity is separately checked against the actual selected allocation.
/// </summary>
internal readonly record struct FoodTradeAllocationIdentity(
    EconomicPointId Source,
    EconomicActivityKind Activity,
    EconomicPointId Destination,
    CommodityId Commodity,
    ulong RawUnitValue,
    ulong RawUnitTransferCost)
{
    public static FoodTradeAllocationIdentity From(EconomicTradeAllocation trade)
    {
        ArgumentNullException.ThrowIfNull(trade);
        return new FoodTradeAllocationIdentity(
            trade.SourceEconomicPointId, trade.SourceActivity,
            trade.DestinationEconomicPointId, trade.CommodityId,
            trade.RawUnitValue, trade.RawUnitTransferCost);
    }
}
