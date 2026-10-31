using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// VS25: local physical food balance for the NEXT real economic settlement.
/// The two unmet-demand components are disjoint, additive, and observational:
/// physical shortage versus unfulfilled demand despite available local food.
/// The latter is not automatically attributed to a single monetary cause.
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
                "Local food consumed exceeds actual population demand.");
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
