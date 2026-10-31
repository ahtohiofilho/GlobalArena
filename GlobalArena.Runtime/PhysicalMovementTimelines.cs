using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// The movement subsystem owns WHEN a tile-to-tile traversal completes.
/// Economics, logistics, map markers and save validation must read this
/// schedule, not independently impose a duration on each Goldberg edge.
///
/// This contract describes the already-shipped V13-V15 schedules only.
/// A future integer movement-point model will need its own canonical state,
/// versioned policy and per-unit progress (not a reinterpretation of old saves).
/// </summary>
public interface IPhysicalMovementTimeline
{
    ulong AdjacentArrivalTurn(ulong departureTurn);
    ulong RouteArrivalTurn(ulong departureTurn, ulong legCount);
    ulong LegCompletionTurn(ulong departureTurn, int completedLegs);
    ulong LegStartTurn(ulong departureTurn, int completedLegs);
}

/// <summary>
/// Immutable timing policy for the primitive movement defined in V13-V15:
/// exactly two movement turns per adjacent edge, independent of food,
/// terrain class and other economic cycle parameters. The policy is an
/// implementation detail of that saved movement ruleset, NOT a statement
/// that all historical eras must use multiples/powers of two.
/// </summary>
public sealed class PrimitiveFixedLegMovementTimeline : IPhysicalMovementTimeline
{
    public const ulong TurnsPerLeg = 2UL;

    internal PrimitiveFixedLegMovementTimeline() { }

    public ulong AdjacentArrivalTurn(ulong departureTurn) =>
        checked(departureTurn + TurnsPerLeg);

    public ulong RouteArrivalTurn(ulong departureTurn, ulong legCount)
    {
        if (legCount == 0UL)
            throw new ArgumentOutOfRangeException(nameof(legCount));
        return checked(departureTurn + checked(legCount * TurnsPerLeg));
    }

    public ulong LegCompletionTurn(ulong departureTurn, int completedLegs)
    {
        if (completedLegs < 0)
            throw new ArgumentOutOfRangeException(nameof(completedLegs));
        return RouteArrivalTurn(departureTurn, checked((ulong)completedLegs + 1UL));
    }

    public ulong LegStartTurn(ulong departureTurn, int completedLegs)
    {
        if (completedLegs < 0)
            throw new ArgumentOutOfRangeException(nameof(completedLegs));
        return checked(departureTurn +
            checked((ulong)completedLegs * TurnsPerLeg));
    }
}

/// <summary>
/// Single selection point for movement timing. No new rule is activated.
/// Old canonical V13/V14/V15 files always retain this adapter; flexible
/// scalar movement points, terrain and transport services will require an
/// explicitly versioned implementation and progress representation.
/// </summary>
public static class PhysicalMovementTimelines
{
    public static IPhysicalMovementTimeline ExistingPrimitive { get; } =
        new PrimitiveFixedLegMovementTimeline();

    public static IPhysicalMovementTimeline ForExistingMode(
        PhysicalWorkerMovementMode mode) =>
        mode == PhysicalWorkerMovementMode.TimedAdjacent
            ? ExistingPrimitive
            : throw new InvalidOperationException(
                "There is no timed movement schedule in legacy instant mode.");
}
