namespace GlobalArena.Runtime;

/// <summary>
/// Legacy six-movement cadence is retained for VS07/VS10 V9 saves.
/// New games use progressive thirty-turn bands.
/// </summary>
public enum PhysicalEconomicCadenceMode : byte
{
    LegacyFixedSix = 0,
    AdaptiveThirty = 1
}

/// <summary>
/// Deterministic cadence: turns 1..30 settle 1/1, 31..60 settle 1/2,
/// 61..90 settle 1/3, etc. Elapsed time from the previous settlement is
/// carried across band boundaries, so an interval longer than thirty
/// movements never makes economic settlements stop altogether.
/// </summary>
public static class PhysicalEconomicCadence
{
    public const ulong AdaptiveWindowTurns = 30UL;
    public const ulong LegacyInterval = 6UL;

    public static ulong IntervalForTurn(ulong movementTurn, PhysicalEconomicCadenceMode mode)
    {
        if (movementTurn == 0UL)
            throw new ArgumentOutOfRangeException(nameof(movementTurn));
        return mode switch
        {
            PhysicalEconomicCadenceMode.LegacyFixedSix => LegacyInterval,
            PhysicalEconomicCadenceMode.AdaptiveThirty =>
                checked((movementTurn - 1UL) / AdaptiveWindowTurns + 1UL),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    public static bool IsSettlementTurn(
        ulong movementTurn, ulong lastSettlementTurn,
        PhysicalEconomicCadenceMode mode)
    {
        if (movementTurn == 0UL || movementTurn <= lastSettlementTurn)
            throw new ArgumentOutOfRangeException(nameof(movementTurn));
        return movementTurn - lastSettlementTurn >= IntervalForTurn(movementTurn, mode);
    }

    /// <summary>
    /// Validate every settlement with constant work per recorded cycle.
    /// Since the denominator grows by only one per 30 turns, the
    /// elapsed-minus-denominator threshold is monotonic, so testing the
    /// previous tick proves this was the FIRST eligible settlement.
    /// </summary>
    public static void ValidateTimeline(
        ulong movementTurn,
        IReadOnlyList<PhysicalEconomicCycle> cycles,
        PhysicalEconomicCadenceMode mode)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        _ = IntervalForTurn(1UL, mode); // reject unsupported policies even at turn zero
        ulong previous = 0UL;
        foreach (var cycle in cycles)
        {
            if (cycle is null || cycle.MovementTurn == 0UL ||
                cycle.MovementTurn <= previous || cycle.MovementTurn > movementTurn ||
                !IsSettlementTurn(cycle.MovementTurn, previous, mode) ||
                (cycle.MovementTurn > previous + 1UL &&
                 IsSettlementTurn(cycle.MovementTurn - 1UL, previous, mode)))
                throw new ArgumentException(
                    "Physical economic settlement history violates the selected cadence.");
            previous = cycle.MovementTurn;
        }
        if (movementTurn > previous && IsSettlementTurn(movementTurn, previous, mode))
            throw new ArgumentException(
                "Physical economic settlement is overdue for the recorded movement turn.");
    }
}
