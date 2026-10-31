using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// One commanded journey to a nonadjacent final destination. Intermediate
/// tiles are passage only, never work sites or new population centres.
/// Every leg consumes the SAME two movement turns as an existing primitive
/// adjacent movement. Route is immutable and canonically ordered.
/// </summary>
public sealed record PhysicalLongDistanceTransit(
    ulong OrderId,
    PhysicalTacticalTileId Source,
    PhysicalTacticalTileId Destination,
    EconomicActivityKind FromJob,
    EconomicActivityKind ToJob,
    ulong Workers,
    ulong DepartureTurn,
    ulong ArrivalTurn,
    ulong NextLegCompletionTurn,
    int CompletedLegs,
    IReadOnlyList<PhysicalTacticalTileId> Route)
{
    public PhysicalTacticalTileId CurrentWaypoint => Route[CompletedLegs];
    public int TotalLegs => Route.Count - 1;
}
