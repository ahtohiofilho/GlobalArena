using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable, purely derived marker data. No additional world, population
/// entity, movement time or save state: every marker is a canonical allocation
/// or an existing V13 transit order.
/// </summary>
public sealed record TacticalWorkerGroupMarker(
    PhysicalTacticalTileId Tile, EconomicActivityKind Activity, ulong Workers);

public sealed record TacticalWorkerTravelMarker(
    ulong OrderId, PhysicalTacticalTileId Source, PhysicalTacticalTileId Destination,
    ulong Workers, ulong DepartureTurn, ulong ArrivalTurn, ulong CurrentTurn)
{
    public ulong Elapsed => CurrentTurn - DepartureTurn;
    public ulong Duration => ArrivalTurn - DepartureTurn;
    public ulong Remaining => ArrivalTurn - CurrentTurn;
    public double Fraction => Elapsed / (double)Duration;
}

public sealed class TacticalWorkerMarkerProjection
{
    public IReadOnlyList<TacticalWorkerGroupMarker> Groups { get; }
    public IReadOnlyList<TacticalWorkerTravelMarker> Journeys { get; }
    public ulong Turn { get; }

    private TacticalWorkerMarkerProjection(
        ulong turn,
        TacticalWorkerGroupMarker[] groups,
        TacticalWorkerTravelMarker[] journeys)
    {
        Turn = turn;
        Groups = Array.AsReadOnly(groups);
        Journeys = Array.AsReadOnly(journeys);
    }

    public static TacticalWorkerMarkerProjection FromPhysicalState(
        PhysicalTacticalEconomyRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var groups = state.Allocations
            .Select(x => new TacticalWorkerGroupMarker(x.TileId, x.Activity, x.Workers))
            .OrderBy(x => x.Tile.FineStrategicCellId.Value)
            .ThenBy(x => (byte)x.Activity).ToArray();
        var journeys = state.Transits.Select(x => new TacticalWorkerTravelMarker(
                x.OrderId, x.Source, x.Destination, x.Workers, x.DepartureTurn,
                x.ArrivalTurn, state.MovementTurn))
            .Concat(state.LongTrips.Select(x => new TacticalWorkerTravelMarker(
                x.OrderId, x.Route[x.CompletedLegs],
                x.Route[x.CompletedLegs + 1], x.Workers,
                PhysicalMovementTimelines.ExistingPrimitive.LegStartTurn(
                    x.DepartureTurn, x.CompletedLegs), x.NextLegCompletionTurn,
                state.MovementTurn)))
            .OrderBy(x => x.OrderId).ToArray();
        if (groups.Aggregate(0UL, (sum, x) => checked(sum + x.Workers)) +
            journeys.Aggregate(0UL, (sum, x) => checked(sum + x.Workers))
            != state.TotalWorkers)
            throw new InvalidOperationException("Worker marker projection would duplicate people.");
        if (journeys.Any(x => x.Duration == 0UL || x.Remaining == 0UL ||
            x.CurrentTurn < x.DepartureTurn || x.CurrentTurn >= x.ArrivalTurn))
            throw new InvalidOperationException("Marker references an expired worker journey.");
        return new TacticalWorkerMarkerProjection(state.MovementTurn, groups, journeys);
    }
}
