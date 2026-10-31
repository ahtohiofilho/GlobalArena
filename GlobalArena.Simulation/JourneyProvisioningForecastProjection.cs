using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A food source is an optional service available at a waypoint, not a
/// requirement imposed by a historical era. This pure quote does NOT buy,
/// gather or transfer food. Future market/transport providers must implement
/// ownership and settlement separately before their offers can be executed.
/// </summary>
public interface ITravelFoodSource
{
    string Kind { get; }
    ulong RawFoodPerWorkerPerSettlement(
        CanonicalPhysicalTacticalSession session, PhysicalTacticalTileId tile);
}

/// <summary>
/// Experimental temporary forage: 75% of actual locally-possible primitive
/// agriculture, rounded down. A separate geography-derived wild-food yield
/// can replace this approximation later. There is no colony and no market
/// transaction at a stop.
/// </summary>
public sealed class PrimitiveForageFoodSource : ITravelFoodSource
{
    public const uint InitialEfficiencyPercent = 75U;
    public string Kind => "temporary-forage";

    public ulong RawFoodPerWorkerPerSettlement(
        CanonicalPhysicalTacticalSession session, PhysicalTacticalTileId tile)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsPhysicalWorkableLand(tile.FineStrategicCellId))
            return 0UL;
        var local = session.PrimitiveYieldAt(tile, EconomicActivityKind.Agriculture);
        if (local <= 0L) return 0UL;
        return checked((ulong)((UInt128)(ulong)local * InitialEfficiencyPercent / 100U));
    }
}

public enum JourneyProvisioningStatus : byte
{
    ArrivalPossible = 0,
    InsufficientFoodAndNoProductiveStop = 1
}

/// <summary>
/// Proposed stops have ZERO effect on the world. Cargo is a user-supplied
/// hypothetical quantity, not a claim that source stocks are owned, purchased
/// or physically loaded by the travellers. Departure, movement, trading and
/// persistent portable stocks are NOT implemented by this projection.
/// </summary>
public sealed record JourneyProvisioningStop(
    PhysicalTacticalTileId Tile,
    string SourceKind,
    ulong EconomicSettlements,
    ulong FoodGatheredRaw,
    ulong FoodConsumedRaw);

public sealed record JourneyProvisioningForecast(
    PhysicalTacticalTileId Origin,
    PhysicalTacticalTileId Destination,
    ulong Workers,
    ulong HypotheticalInitialCargoRaw,
    JourneyProvisioningStatus Status,
    ulong? ProjectedArrivalMovementTurn,
    ulong TravelMovementTurns,
    ulong WaitingMovementTurns,
    ulong GatheredFoodRaw,
    ulong ConsumedFoodRaw,
    ulong? RemainingCargoRaw,
    IReadOnlyList<JourneyProvisioningStop> Stops)
{
    public bool CanArriveUnderAssumptions =>
        Status == JourneyProvisioningStatus.ArrivalPossible;
}

/// <summary>
/// Single deterministic read-only logistics forecast for primitive walking.
/// An alternate food provider can be passed without changing route timing,
/// consumers or the economy. Camps are optional: adequate carried food
/// means no stops. At a camp, actual economic time MUST pass before food is
/// gathered; every waiting settlement also consumes traveller food.
/// </summary>
public static class JourneyProvisioningForecastProjection
{
    private static readonly ITravelFoodSource DefaultSource = new PrimitiveForageFoodSource();

    public static JourneyProvisioningForecast PreviewPlanned(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId origin, PhysicalTacticalTileId destination,
        EconomicActivityKind sourceJob, EconomicActivityKind destinationJob,
        ulong workers, ulong hypotheticalCarriedFoodRaw,
        ITravelFoodSource? optionalSource = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var route = PhysicalRemoteDestinationRouteProjection.Preview(
            session, origin, destination, sourceJob, destinationJob, workers);
        if (!route.HasFootpath || route.MovementLegs < 2UL)
            throw new InvalidOperationException(
                "Primitive expedition forecast requires a nonadjacent land route.");
        return Simulate(session, route, hypotheticalCarriedFoodRaw,
            optionalSource ?? DefaultSource);
    }

    private static JourneyProvisioningForecast Simulate(
        CanonicalPhysicalTacticalSession session,
        PhysicalRemoteDestinationRoute route, ulong initialFoodRaw,
        ITravelFoodSource provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var state = session.State;
        var currentTurn = state.MovementTurn;
        var lastSettlement = state.Cycles.Count == 0
            ? 0UL : state.Cycles[^1].MovementTurn;
        var departureTurn = currentTurn;
        var cargo = initialFoodRaw;
        var gathered = 0UL;
        var consumed = 0UL;
        var waiting = 0UL;
        var stops = new List<JourneyProvisioningStop>();
        var perSettlementDemand = checked(
            route.Workers * CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker);
        var timeline = PhysicalMovementTimelines.ForExistingMode(session.MovementMode);

        for (var leg = 0; leg < route.Tiles.Count - 1; leg++)
        {
            var at = route.Tiles[leg];
            var finalLeg = leg == route.Tiles.Count - 2;
            var plannedLegArrival = timeline.AdjacentArrivalTurn(currentTurn);
            ulong RequiredFoodBeforeArrival(ulong start, ulong last)
            {
                ulong requirement = 0UL;
                var target = timeline.AdjacentArrivalTurn(start);
                for (var tick = checked(start + 1UL); tick <= target; tick++)
                {
                    if (!PhysicalEconomicCadence.IsSettlementTurn(
                            tick, last, session.CadenceMode))
                        continue;
                    last = tick;
                    if (finalLeg && tick == target) continue;
                    requirement = checked(requirement + perSettlementDemand);
                }
                return requirement;
            }

            while (cargo < RequiredFoodBeforeArrival(currentTurn, lastSettlement))
            {
                var foodPerWorker = provider.RawFoodPerWorkerPerSettlement(session, at);
                var gross = checked(foodPerWorker * route.Workers);
                // If forage cannot even cover camp consumption, no number
                // of extra waiting cycles can fill the deficit. The route is
                // not forbidden: it requires another supply source or cargo.
                if (gross <= perSettlementDemand)
                    return Infeasible();

                var campFood = 0UL;
                var campConsumption = 0UL;
                ulong stopCycles = 0UL;
                do
                {
                    // No instantaneous food, and no requirement to found a
                    // settlement. The same economic clock handles all eras.
                    do
                    {
                        currentTurn = checked(currentTurn + 1UL);
                        waiting = checked(waiting + 1UL);
                    }
                    while (!PhysicalEconomicCadence.IsSettlementTurn(
                        currentTurn, lastSettlement, session.CadenceMode));
                    lastSettlement = currentTurn;
                    stopCycles = checked(stopCycles + 1UL);
                    cargo = checked(cargo + gross - perSettlementDemand);
                    gathered = checked(gathered + gross);
                    consumed = checked(consumed + perSettlementDemand);
                    campFood = checked(campFood + gross);
                    campConsumption = checked(campConsumption + perSettlementDemand);
                } while (cargo < RequiredFoodBeforeArrival(currentTurn, lastSettlement));
                stops.Add(new JourneyProvisioningStop(at, provider.Kind,
                    stopCycles, campFood, campConsumption));
            }

            var endTurn = timeline.AdjacentArrivalTurn(currentTurn);
            for (var tick = checked(currentTurn + 1UL); tick <= endTurn; tick++)
            {
                if (!PhysicalEconomicCadence.IsSettlementTurn(
                        tick, lastSettlement, session.CadenceMode))
                    continue;
                lastSettlement = tick;
                if (finalLeg && tick == endTurn) continue;
                cargo -= perSettlementDemand; // checked by pre-leg planning
                consumed = checked(consumed + perSettlementDemand);
            }
            currentTurn = endTurn;
        }

        return new JourneyProvisioningForecast(route.Origin, route.Destination,
            route.Workers, initialFoodRaw, JourneyProvisioningStatus.ArrivalPossible,
            currentTurn, checked(currentTurn - departureTurn - waiting), waiting,
            gathered, consumed, cargo, Array.AsReadOnly(stops.ToArray()));

        JourneyProvisioningForecast Infeasible() =>
            new(route.Origin, route.Destination, route.Workers, initialFoodRaw,
                JourneyProvisioningStatus.InsufficientFoodAndNoProductiveStop,
                null, checked(currentTurn - departureTurn - waiting), waiting,
                gathered, consumed, null, Array.AsReadOnly(stops.ToArray()));
    }
}
