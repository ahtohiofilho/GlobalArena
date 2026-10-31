using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Fixed-point, pure physical accounting for an actual camp. The configured
/// geographical provider supplies the production amount; this class never
/// invents a floor, supplies food without work, or mutates commercial stock.
/// </summary>
public static class PhysicalJourneyCampFoodAccounting
{
    public static (PhysicalJourneyFoodLedger Food, PhysicalJourneyCampState Camp,
        ulong ProducedRaw) WaitOneTurn(
        PhysicalJourneyFoodLedger food, PhysicalJourneyCampState camp,
        ulong workers, ulong forageRawPerWorker, bool economicSettlement)
    {
        ArgumentNullException.ThrowIfNull(food);
        ArgumentNullException.ThrowIfNull(camp);
        if (camp.OrderId != food.OrderId || workers == 0UL)
            throw new InvalidOperationException("Camp work requires an active food-owning crew.");
        var gross = economicSettlement
            ? checked(forageRawPerWorker * workers) : 0UL;
        var need = checked(workers *
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker);
        if (economicSettlement && gross <= need)
            throw new InvalidOperationException(
                "This campsite cannot produce a physical food surplus.");
        var nextFood = food with
        {
            FoodRemainingRaw = checked(food.FoodRemainingRaw + gross)
        };
        var nextCamp = camp with
        {
            WaitingMovementTurns = checked(camp.WaitingMovementTurns + 1UL),
            GatheredFoodRaw = checked(camp.GatheredFoodRaw + gross),
            GatheringEconomicCycles = checked(camp.GatheringEconomicCycles +
                (economicSettlement ? 1UL : 0UL))
        };
        return (nextFood, nextCamp, gross);
    }

    public static (PhysicalJourneyFoodLedger Food, ulong ConsumedRaw,
        ulong UnmetRaw) ConsumeOneEconomicCycle(
        PhysicalJourneyFoodLedger food, ulong workers)
    {
        ArgumentNullException.ThrowIfNull(food);
        if (workers == 0UL)
            throw new ArgumentOutOfRangeException(nameof(workers));
        var demand = checked(workers *
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker);
        var consumed = Math.Min(food.FoodRemainingRaw, demand);
        return (food with
        {
            FoodRemainingRaw = food.FoodRemainingRaw - consumed,
            FoodConsumedRaw = checked(food.FoodConsumedRaw + consumed)
        }, consumed, demand - consumed);
    }
}
