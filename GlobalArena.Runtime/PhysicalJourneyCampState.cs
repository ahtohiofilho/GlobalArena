namespace GlobalArena.Runtime;

/// <summary>
/// A versioned automatic-camp itinerary. Delay is physical movement time
/// already spent gathering; it shifts all remaining leg deadlines, but never
/// changes the speed per leg. The food itself belongs in the V16 lot.
/// </summary>
public sealed record PhysicalJourneyCampState(
    ulong OrderId,
    ulong WaitingMovementTurns,
    ulong GatheredFoodRaw,
    ulong GatheringEconomicCycles);
