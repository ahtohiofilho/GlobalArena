namespace GlobalArena.Simulation;

/// <summary>
/// Route-choice policy for the opt-in congestion-priced food market only.
/// Neither mode imposes a physical throughput limit.
/// </summary>
public enum StrategicCongestionRoutingMode : byte
{
    FixedShortestHop = 0,
    AdaptiveLowestMarginalCost = 1
}
