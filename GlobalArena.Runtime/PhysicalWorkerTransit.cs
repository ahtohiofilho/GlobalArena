using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// One commanded group on a single adjacent-tile route. During transit it
/// produces nothing and remains provisioned at its recorded departure tile.
/// Arrival is a deterministic economic movement turn, not an instant move.
/// </summary>
public sealed record PhysicalWorkerTransit(
    ulong OrderId, PhysicalTacticalTileId Source, PhysicalTacticalTileId Destination,
    EconomicActivityKind FromJob, EconomicActivityKind ToJob, ulong Workers,
    ulong DepartureTurn, ulong ArrivalTurn);

public enum PhysicalWorkerMovementMode : byte
{
    LegacyInstant = 0,
    TimedAdjacent = 1
}
