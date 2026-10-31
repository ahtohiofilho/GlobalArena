using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A purely physical, nonbinding diagnosis. These quantities are NOT a
/// freight tariff, a ration inventory, an assigned purchase, or an additional
/// food obligation. Travellers already generate food demand in V15.
/// </summary>
public sealed record PhysicalJourneySupplyAssessment(
    PhysicalTacticalTileId Source,
    PhysicalTacticalTileId FinalDestination,
    EconomicActivityKind OriginalActivity,
    ulong Travellers,
    ulong AssessedAtMovementTurn,
    ulong ExpectedArrivalTurn,
    ulong RemainingMovementTurns,
    ulong EconomicSettlementsBeforeArrival,
    ulong TravellersBaselineFoodDemandRaw,
    ulong ForegoneSourceProductionRaw,
    ulong ExistingOriginFoodStockRaw,
    ulong StockOnlyUpperBoundRaw,
    ulong StockOnlyUncoveredRaw,
    bool IsExistingJourney)
{
    public bool OriginStockAloneCoversBaseline =>
        StockOnlyUncoveredRaw == 0UL;
}

/// <summary>
/// GA-VS30: supply and opportunity-cost reconnaissance for arbitrary-distance
/// walking, evaluated using the REAL cadence, the canonical half-food-unit
/// demand per worker, and the ACTUAL primitive yield for the source activity.
/// Existing stocks are reported as a stock-only upper bound, NOT as food
/// reserved or paid for by travellers: other residents, future local
/// production and market purchasing can change actual availability.
/// </summary>
public static class PhysicalJourneySupplyProjection
{
    private static readonly CommodityId Food = new(1U);

    public static PhysicalJourneySupplyAssessment PreviewPlanned(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId source, PhysicalTacticalTileId destination,
        EconomicActivityKind fromJob, EconomicActivityKind toJob, ulong workers)
    {
        ArgumentNullException.ThrowIfNull(session);
        var route = PhysicalRemoteDestinationRouteProjection.Preview(
            session, source, destination, fromJob, toJob, workers);
        if (!route.HasFootpath || route.MovementLegs < 2UL ||
            route.EarliestArrivalTurn is null)
            throw new InvalidOperationException(
                "A remote supply diagnosis requires a walkable multi-leg route.");
        return Assess(session, source, destination, fromJob, workers,
            route.EarliestArrivalTurn.Value, false);
    }

    public static PhysicalJourneySupplyAssessment PreviewActive(
        CanonicalPhysicalTacticalSession session, ulong orderId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var journey = session.State.LongTrips.SingleOrDefault(x => x.OrderId == orderId)
            ?? throw new InvalidOperationException(
                "No currently travelling long-distance group has that order ID.");
        return Assess(session, journey.Source, journey.Destination,
            journey.FromJob, journey.Workers, journey.ArrivalTurn, true);
    }

    private static PhysicalJourneySupplyAssessment Assess(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId source, PhysicalTacticalTileId destination,
        EconomicActivityKind fromJob, ulong workers, ulong arrivalTurn,
        bool active)
    {
        var state = session.State;
        if (arrivalTurn <= state.MovementTurn || workers == 0UL)
            throw new InvalidOperationException("A supply window needs future travel.");
        var lastSettlement = state.Cycles.Count == 0
            ? 0UL : state.Cycles[^1].MovementTurn;
        ulong cycles = 0UL;
        // Arrival is resolved BEFORE production/consumption in its own turn:
        // only settlements on turns strictly earlier than arrival represent
        // food demand and lost production while the group is still travelling.
        for (var turn = checked(state.MovementTurn + 1UL);
             turn < arrivalTurn; turn = checked(turn + 1UL))
        {
            if (!PhysicalEconomicCadence.IsSettlementTurn(
                    turn, lastSettlement, session.CadenceMode))
                continue;
            lastSettlement = turn;
            cycles = checked(cycles + 1UL);
        }

        var foodDemand = checked(checked(cycles * workers) *
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker);
        var outputRate = session.PrimitiveYieldAt(source, fromJob);
        if (outputRate < 0L)
            throw new InvalidOperationException("Negative primitive output potential.");
        var forgone = checked(checked(cycles * workers) * (ulong)outputRate);
        var sourceStock = session.StockAt(source, Food);
        var coverage = Math.Min(foodDemand, sourceStock);

        return new PhysicalJourneySupplyAssessment(
            source, destination, fromJob, workers, state.MovementTurn,
            arrivalTurn, checked(arrivalTurn - state.MovementTurn), cycles,
            foodDemand, forgone, sourceStock, coverage,
            foodDemand - coverage, active);
    }
}
