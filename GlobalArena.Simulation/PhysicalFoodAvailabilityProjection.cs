using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// VS25 historical V9-V20 quota reconciliation for the NEXT settlement.
/// FoodDemandRaw, PhysicalShortageRaw and UnconsumedDespiteSupplyRaw are old
/// quota-accounting fields, NOT real economic demand as a consumption ceiling,
/// hunger or nutrition/efficiency measurements. Keep their names for V9-V20
/// callers and tests; a future unbounded intake policy is a separate ruleset.
/// </summary>
public sealed record PhysicalFoodAvailabilityForecast(
    PhysicalTacticalTileId Tile,
    ulong StartingMovementTurn,
    ulong SettlementMovementTurn,
    ulong InitialFoodStockRaw,
    ulong ProducedFoodRaw,
    ulong TotalAvailableFoodRaw,
    ulong FoodDemandRaw,
    ulong ActuallyConsumedRaw,
    ulong PhysicalShortageRaw,
    ulong UnconsumedDespiteSupplyRaw,
    ulong FinalFoodStockRaw)
{
    public ulong TotalUnmetRaw =>
        checked(PhysicalShortageRaw + UnconsumedDespiteSupplyRaw);
}

/// <summary>
/// Read-only diagnostic, not a second food market. Exactly one upcoming
/// canonical economic settlement is executed in a disposable replay of
/// the current WorldState. No additional rules, journal event types,
/// prices, wage payments or changes to the actual game are introduced.
/// </summary>
public static class PhysicalFoodAvailabilityProjection
{
    private static readonly CommodityId Food = new(1U);

    public static PhysicalFoodAvailabilityForecast PreviewNextCycle(
        CanonicalPhysicalTacticalSession live, PhysicalTacticalTileId tile)
    {
        ArgumentNullException.ThrowIfNull(live);
        // Legacy direct consumption has no tile-level receipt. Do not invent
        // a monetary/physical split or misrepresent a global aggregate.
        if (live.FoodCash is null)
            throw new NotSupportedException(
                "Tile-level food accounting is unavailable in legacy direct-consumption saves.");

        var originalFoodStock = live.StockAt(tile, Food); // also validates tile
        var startingTurn = live.State.MovementTurn;
        var scenario = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            live.ExportSaveJson());
        var cycle = PhysicalEconomicCycleProgression.AdvanceToNext(scenario);
        var finalFoodStock = scenario.StockAt(tile, Food);
        var demand = scenario.FoodDemandAt(tile);
        var consumed = scenario.FoodCash!.Receipts
            .Where(x => x.Tile == tile && x.MovementTurn == cycle.MovementTurn)
            .Aggregate(0UL, (sum, row) => checked(sum + row.FoodRaw));
        var physicalAvailable = checked(finalFoodStock + consumed);
        if (physicalAvailable < originalFoodStock)
            throw new InvalidOperationException(
                "Local food balance would require negative production.");
        if (consumed > demand)
            throw new InvalidOperationException(
                "Legacy food consumed exceeds its V9-V20 intake quota.");
        var physicallyCoverable = Math.Min(physicalAvailable, demand);
        if (consumed > physicallyCoverable)
            throw new InvalidOperationException(
                "Food was consumed without adequate local physical supply.");
        var physicalShortage = demand - physicallyCoverable;
        var unmatchedDespiteSupply = physicallyCoverable - consumed;

        return new PhysicalFoodAvailabilityForecast(
            tile, startingTurn, cycle.MovementTurn,
            originalFoodStock, physicalAvailable - originalFoodStock,
            physicalAvailable, demand, consumed,
            physicalShortage, unmatchedDespiteSupply, finalFoodStock);
    }
}
