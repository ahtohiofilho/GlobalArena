using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One immutable decision: before beginning an adjacent leg, determine
/// whether carried food can cover every economic settlement on that leg.
/// Never stop mid-edge. On the final leg arrival itself is NOT a traveller
/// settlement, since arrival is resolved before that cycle.
/// </summary>
public static class PhysicalJourneyAutomaticCampPolicy
{
    public static bool NeedsCampBeforeNextLeg(
        CanonicalPhysicalTacticalSession game,
        PhysicalLongDistanceTransit trip,
        PhysicalJourneyFoodLedger food)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(food);
        var current = game.State;
        var timeline = PhysicalMovementTimelines.ForExistingMode(game.MovementMode);
        // A leg already in progress cannot be interrupted or slowed.
        if (trip.NextLegCompletionTurn !=
            timeline.AdjacentArrivalTurn(current.MovementTurn))
            return false;
        var last = current.Cycles.Count == 0
            ? 0UL : current.Cycles[^1].MovementTurn;
        var needed = 0UL;
        var demand = checked(trip.Workers *
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker);
        var finalLeg = trip.CompletedLegs == trip.TotalLegs - 1;
        for (var tick = checked(current.MovementTurn + 1UL);
             tick <= trip.NextLegCompletionTurn; tick++)
        {
            if (!PhysicalEconomicCadence.IsSettlementTurn(tick, last,
                    game.CadenceMode))
                continue;
            last = tick;
            if (finalLeg && tick == trip.ArrivalTurn) continue;
            needed = checked(needed + demand);
        }
        return food.FoodRemainingRaw < needed;
    }
}
