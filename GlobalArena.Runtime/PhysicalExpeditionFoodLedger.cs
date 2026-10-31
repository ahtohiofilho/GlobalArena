using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// V16 historical cash purchase: the local producer's commodity is sold
/// to the expedition's household, strictly at sight. This receipt is
/// separate from the V12 one-receipt-per-tile-per-cycle retail ledger.
/// </summary>
public sealed record PhysicalExpeditionFoodPurchase(
    ulong OrderId, PhysicalTacticalTileId Source, ulong PurchasedRaw,
    ulong PriceMicrocreditsPerUnit, ulong PaidMicrocredits);

/// <summary>
/// V16 food physically owned by one moving workforce. The initial amount
/// is the exact forecasted travel need. There is no camp, resupply or
/// post-arrival excess in this first execution scope.
/// </summary>
public sealed record PhysicalExpeditionFoodParcel(
    ulong OrderId, ulong RemainingRaw);
