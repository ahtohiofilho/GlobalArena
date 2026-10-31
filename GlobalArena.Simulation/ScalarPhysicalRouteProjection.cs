using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Supplies the prospective integer price of traversing one ADJACENT
/// walkable edge. The caller determines terrain, workforce and equipment.
/// The VS36 preview does not invent or activate any modal, speed or terrain
/// calibration, nor can a provider make an ocean edge walkable.
/// </summary>
public interface IScalarPhysicalEdgeCostProvider
{
    ulong CostPoints(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to);
}

/// <summary>
/// Opt-in permission for an edge in an explicit mobility modality.
/// Legacy scalar providers implicitly allow all canonical walking edges.
/// A non-traversable edge is skipped, never charged an infinite price.
/// </summary>
public interface IConstrainedScalarPhysicalEdgeCostProvider :
    IScalarPhysicalEdgeCostProvider
{
    bool CanTraverse(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to);
}

/// <summary>
/// A read-only, optional route quote. Availability matches the legacy land
/// topology, but the preferred path minimizes scalar points instead of hops.
/// No actual departure, food, cash, population or save changes are made.
/// </summary>
public sealed record ScalarPhysicalRoutePreview(
    PhysicalPrimitiveRouteAvailability Availability,
    IReadOnlyList<PhysicalTacticalTileId> Tiles,
    IReadOnlyList<ulong> EdgeCosts,
    ScalarMovementRouteForecast? Timing)
{
    public bool HasRoute =>
        Availability == PhysicalPrimitiveRouteAvailability.Walkable;
}

/// <summary>
/// Deterministic Dijkstra on the existing canonical LAND graph.
/// Equal-cost alternatives prefer fewer legs, then canonical predecessor
/// IDs. A supplied provider is pure policy: not a player-facing order.
/// The active V13-V17 BFS remains the authority for already-issued journeys.
/// </summary>
public static class ScalarPhysicalRouteProjection
{
    public static ScalarPhysicalRoutePreview Preview(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId origin,
        PhysicalTacticalTileId destination,
        EconomicActivityKind sourceJob,
        EconomicActivityKind destinationJob,
        ulong workers,
        ulong pointsPerTurn,
        IScalarPhysicalEdgeCostProvider provider)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(provider);
        if (pointsPerTurn == 0UL)
            throw new ArgumentOutOfRangeException(nameof(pointsPerTurn));

        // Reuse existing domain validation and connectivity without changing
        // the legacy BFS policy, order or persisted state.
        var primitive = PhysicalRemoteDestinationRouteProjection.Preview(
            session, origin, destination, sourceJob, destinationJob, workers);
        if (!primitive.HasFootpath)
            return new ScalarPhysicalRoutePreview(primitive.Availability,
                Array.AsReadOnly(Array.Empty<PhysicalTacticalTileId>()),
                Array.AsReadOnly(Array.Empty<ulong>()), null);

        var best = new Dictionary<PhysicalTacticalTileId, (ulong Cost, int Hops)>
        {
            [origin] = (0UL, 0)
        };
        var parent = new Dictionary<PhysicalTacticalTileId,
            (PhysicalTacticalTileId From, ulong EdgeCost)>();
        var queue = new PriorityQueue<PhysicalTacticalTileId,
            (ulong Cost, int Hops, ulong TileId)>();
        queue.Enqueue(origin, (0UL, 0, origin.FineStrategicCellId.Value));

        var overflowed = false;
        while (queue.TryDequeue(out var current, out var priority))
        {
            if (!best.TryGetValue(current, out var known) ||
                known.Cost != priority.Cost || known.Hops != priority.Hops)
                continue;
            if (current == destination)
                break;

            foreach (var next in session.AdjacentWorkable(current)
                         .OrderBy(x => x.FineStrategicCellId.Value))
            {
                if (provider is IConstrainedScalarPhysicalEdgeCostProvider access &&
                    !access.CanTraverse(session, current, next))
                    continue;
                var edgeCost = provider.CostPoints(session, current, next);
                if (edgeCost == 0UL)
                    throw new InvalidOperationException(
                        "A walkable physical edge requires a positive point cost.");
                if (ulong.MaxValue - known.Cost < edgeCost)
                {
                    overflowed = true;
                    continue; // unrepresentable path cannot win the quote
                }
                var cost = known.Cost + edgeCost;
                var hops = checked(known.Hops + 1);
                if (best.TryGetValue(next, out var old) &&
                    (cost > old.Cost ||
                     (cost == old.Cost && hops > old.Hops) ||
                     (cost == old.Cost && hops == old.Hops &&
                      current.FineStrategicCellId.Value >=
                      parent[next].From.FineStrategicCellId.Value)))
                    continue;

                best[next] = (cost, hops);
                parent[next] = (current, edgeCost);
                queue.Enqueue(next, (cost, hops, next.FineStrategicCellId.Value));
            }
        }

        if (!best.ContainsKey(destination))
        {
            if (overflowed)
                throw new OverflowException(
                    "The available scalar corridor exceeds the point range.");
            return new ScalarPhysicalRoutePreview(
                PhysicalPrimitiveRouteAvailability.NoCompatibleLandTransportMode,
                Array.AsReadOnly(Array.Empty<PhysicalTacticalTileId>()),
                Array.AsReadOnly(Array.Empty<ulong>()), null);
        }

        var reverseTiles = new List<PhysicalTacticalTileId> { destination };
        var reverseCosts = new List<ulong>();
        while (reverseTiles[^1] != origin)
        {
            var step = parent[reverseTiles[^1]];
            reverseCosts.Add(step.EdgeCost);
            reverseTiles.Add(step.From);
        }
        reverseTiles.Reverse();
        reverseCosts.Reverse();

        var timing = ScalarMovementPointForecast.Preview(
            session.State.MovementTurn, pointsPerTurn, reverseCosts);
        return new ScalarPhysicalRoutePreview(
            PhysicalPrimitiveRouteAvailability.Walkable,
            Array.AsReadOnly(reverseTiles.ToArray()),
            Array.AsReadOnly(reverseCosts.ToArray()),
            timing);
    }
}
