using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Food legitimately acquired by one long-distance expedition, segregated
/// from locally offered market stock. Upon arrival the remaining food is held
/// by that same group at its destination, not silently resold by a producer.
/// This is an individual-property ledger; it is not a bank, loan, or market order.
/// </summary>
public sealed record PhysicalJourneyFoodLedger(
    ulong OrderId,
    PhysicalTacticalTileId Source,
    PhysicalTacticalTileId Destination,
    ulong FoodPurchasedRaw,
    ulong FoodRemainingRaw,
    ulong FoodConsumedRaw,
    ulong UnitPriceMicrocredits,
    ulong PaidMicrocredits,
    bool Arrived)
{
    public static ulong PurchasePayment(ulong foodRaw, ulong unitPriceMicrocredits)
    {
        if (foodRaw == 0UL || unitPriceMicrocredits == 0UL)
            throw new ArgumentOutOfRangeException(nameof(foodRaw),
                "A physical purchase requires food and a positive quoted price.");
        // Round up positive fractional microcredits: no food transfer goes unpaid.
        var numerator = (UInt128)foodRaw * unitPriceMicrocredits;
        return checked((ulong)((numerator +
            PhysicalFoodCashMarketState.MonetaryScale - 1UL) /
            PhysicalFoodCashMarketState.MonetaryScale));
    }
}
