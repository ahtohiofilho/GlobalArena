namespace GlobalArena.Simulation;

/// <summary>
/// Caller-specified NUMERICAL search resolution for deterministic successive
/// marginal allocation. RawDecisionQuantum limits the size of one decision,
/// NOT the quantity that may traverse a route or edge. Repeated decisions may
/// move unlimited feasible supply at increasing cost. MaxDecisions is a
/// fail-closed computational budget (never silently truncates a market).
///
/// The greedy resolution is a deterministic marginal-price approximation,
/// not a proof of a globally optimal multi-commodity convex flow.
/// </summary>
public sealed class EconomicCongestionMarketSearchPolicy
{
    public ulong RawDecisionQuantum { get; }
    public int MaxDecisions { get; }

    public EconomicCongestionMarketSearchPolicy(
        ulong rawDecisionQuantum, int maxDecisions)
    {
        if (rawDecisionQuantum == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawDecisionQuantum));
        if (maxDecisions <= 0 || maxDecisions > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(maxDecisions));

        RawDecisionQuantum = rawDecisionQuantum;
        MaxDecisions = maxDecisions;
    }
}
