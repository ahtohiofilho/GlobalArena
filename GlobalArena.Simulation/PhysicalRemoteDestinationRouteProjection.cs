using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Availability of a route with the PRESENT primitive walking technology.
/// This is never a declaration that a remote tile cannot be colonized by
/// another technology, infrastructure or future mode of transport.
/// </summary>
public enum PhysicalPrimitiveRouteAvailability
{
    Walkable = 0,
    DestinationNeedsOtherTechnology = 1,
    NoContinuousLandFootpath = 2,
    // Optional scalar quote only. No compatible constructed corridor for
    // this requested modal policy; old walking saves never emit this value.
    NoCompatibleLandTransportMode = 3
}

/// <summary>
/// Read-only path for an explicitly selected destination anywhere on the
/// physical world. All steps follow real Goldberg adjacencies; no teleport,
/// colonization event, scheduled orders or invented financial travel cost.
/// </summary>
public sealed record PhysicalRemoteDestinationRoute(
    PhysicalTacticalTileId Origin,
    PhysicalTacticalTileId Destination,
    EconomicActivityKind FromJob,
    EconomicActivityKind DestinationJob,
    ulong Workers,
    PhysicalPrimitiveRouteAvailability Availability,
    IReadOnlyList<PhysicalTacticalTileId> Tiles,
    ulong MovementLegs,
    ulong? PrimitiveTravelTurns,
    ulong? EarliestArrivalTurn)
{
    public bool HasFootpath => Availability == PhysicalPrimitiveRouteAvailability.Walkable;
}

/// <summary>
/// World-scale route preview. No arbitrary maximum colonization distance:
/// only the actual physical topology and available travel mode determine
/// reachability. The current primitive stage walks on land; future sea,
/// air and infrastructure route planners can provide alternatives.
/// </summary>
public static class PhysicalRemoteDestinationRouteProjection
{
    public static PhysicalRemoteDestinationRoute Preview(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId origin, PhysicalTacticalTileId destination,
        EconomicActivityKind fromJob, EconomicActivityKind destinationJob,
        ulong workers)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.MovementMode != PhysicalWorkerMovementMode.TimedAdjacent)
            throw new InvalidOperationException(
                "The primitive physical movement calendar is unavailable in this save.");
        // Validate both IDs and professions against the canonical physical world.
        session.PrimitiveYieldAt(origin, fromJob);
        session.PrimitiveYieldAt(origin, destinationJob);
        session.WorkersAt(destination);
        if (destination == origin)
            throw new InvalidOperationException(
                "Origin and remote destination must be distinct.");
        if (workers == 0UL)
            throw new InvalidOperationException("Select at least one resident worker.");
        var available = session.State.Allocations.Where(x =>
                x.TileId == origin && x.Activity == fromJob)
            .Aggregate(0UL, (sum, x) => checked(sum + x.Workers));
        if (workers > available)
            throw new InvalidOperationException(
                "Not enough residents in the selected originating activity.");

        PhysicalRemoteDestinationRoute Unavailable(PhysicalPrimitiveRouteAvailability status) =>
            new(origin, destination, fromJob, destinationJob, workers, status,
                Array.AsReadOnly(Array.Empty<PhysicalTacticalTileId>()),
                0UL, null, null);

        if (!session.IsPhysicalWorkableLand(destination.FineStrategicCellId))
            return Unavailable(PhysicalPrimitiveRouteAvailability.DestinationNeedsOtherTechnology);

        // BFS gives the minimum number of primitive walking legs because the
        // existing movement timeline assigns an identical cost per edge. Adjacency is
        // enumerated in canonical tile-ID order, making equal-length paths
        // fully deterministic across platform and replay.
        var visited = new HashSet<PhysicalTacticalTileId> { origin };
        var previous = new Dictionary<PhysicalTacticalTileId, PhysicalTacticalTileId>();
        var frontier = new Queue<PhysicalTacticalTileId>();
        frontier.Enqueue(origin);
        while (frontier.Count != 0 && !visited.Contains(destination))
        {
            var current = frontier.Dequeue();
            foreach (var next in session.AdjacentWorkable(current))
            {
                if (!visited.Add(next))
                    continue;
                previous.Add(next, current);
                if (next == destination)
                    break;
                frontier.Enqueue(next);
            }
        }

        if (!visited.Contains(destination))
            return Unavailable(PhysicalPrimitiveRouteAvailability.NoContinuousLandFootpath);

        var reverse = new List<PhysicalTacticalTileId> { destination };
        while (reverse[^1] != origin)
            reverse.Add(previous[reverse[^1]]);
        reverse.Reverse();
        var legs = checked((ulong)reverse.Count - 1UL);
        var timeline = PhysicalMovementTimelines.ForExistingMode(session.MovementMode);
        var arrival = timeline.RouteArrivalTurn(session.State.MovementTurn, legs);
        var movementTurns = checked(arrival - session.State.MovementTurn);
        return new PhysicalRemoteDestinationRoute(
            origin, destination, fromJob, destinationJob, workers,
            PhysicalPrimitiveRouteAvailability.Walkable,
            Array.AsReadOnly(reverse.ToArray()), legs, movementTurns,
            arrival);
    }
}
