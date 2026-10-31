using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Read-only prediction of the next economic settlement under the CURRENT
/// save's existing cadence. This does not define or change the time scale.
/// </summary>
public sealed record NextPhysicalEconomicCycle(
    ulong CurrentMovementTurn,
    ulong SettlementTurn,
    ulong MovementTurnsToAdvance);

/// <summary>
/// VS22: convenience action composed strictly of the existing canonical
/// single-turn operations. Every intermediate tick remains in the event
/// journal; no new event kind, save field, market, or cadence is introduced.
/// </summary>

/// <summary>
/// A read-only window over the canonical economic cycle history. Totals
/// are physical counters, not GDP, incomes, or a macroeconomic valuation.
/// </summary>
public sealed record RecentPhysicalEconomicCycles(
    int CycleCount,
    ulong FirstSettlementTurn,
    ulong LastSettlementTurn,
    ulong GrossProducedRaw,
    ulong FoodConsumedRaw,
    ulong FoodUnmetRaw);

/// <summary>
/// Preview of a bounded multi-cycle action; every movement tick will still
/// be a regular canonical action and persist in the existing V9/V13/V14 save.
/// </summary>
public sealed record PhysicalEconomicBatchPlan(
    ulong StartingMovementTurn, ulong FinalSettlementTurn,
    int EconomicCycles, ulong MovementTurnsToAdvance);

public static class PhysicalEconomicCycleProgression
{
    public static NextPhysicalEconomicCycle Preview(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        var lastSettlement = state.Cycles.Count == 0
            ? 0UL : state.Cycles[^1].MovementTurn;
        var future = checked(state.MovementTurn + 1UL);
        while (!PhysicalEconomicCadence.IsSettlementTurn(
                   future, lastSettlement, session.CadenceMode))
            future = checked(future + 1UL);
        return new NextPhysicalEconomicCycle(state.MovementTurn, future,
            checked(future - state.MovementTurn));
    }

    public static PhysicalEconomicCycle AdvanceToNext(CanonicalPhysicalTacticalSession session)
    {
        var plan = Preview(session);
        // Prevent a foreseeable journal-capacity failure midway through a
        // multi-tick action. Other runtime errors may still interrupt after
        // already committed ticks; no fake transaction/rollback is claimed.
        if (plan.MovementTurnsToAdvance >
            (ulong)(CanonicalPhysicalTacticalSession.MaximumActions - session.Events.Count))
            throw new InvalidOperationException(
                "Not enough canonical journal capacity to finish the next economic cycle.");

        for (ulong tick = 1UL; tick <= plan.MovementTurnsToAdvance; tick++)
        {
            var cycle = session.AdvanceMovementTurn();
            if (tick == plan.MovementTurnsToAdvance)
            {
                if (cycle is null || cycle.MovementTurn != plan.SettlementTurn)
                    throw new InvalidOperationException(
                        "Next canonical economic cycle diverged from the cadence projection.");
                return cycle;
            }

            if (cycle is not null)
                throw new InvalidOperationException(
                    "A canonical economic cycle occurred before the projected turn.");
        }

        throw new InvalidOperationException("The projected economic cycle was not reached.");
    }

    /// <summary>
    /// VS23: read the most recent settled cycles without creating additional
    /// money, stock, employment or accounting entries.
    /// </summary>
    public static RecentPhysicalEconomicCycles SummarizeRecent(
        CanonicalPhysicalTacticalSession session, int maxCycles = 5)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (maxCycles is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(maxCycles));
        var history = session.State.Cycles;
        var count = Math.Min(history.Count, maxCycles);
        ulong produced = 0UL, consumed = 0UL, unmet = 0UL;
        for (var i = history.Count - count; i < history.Count; i++)
        {
            var cycle = history[i];
            produced = checked(produced + cycle.ProducedRaw);
            consumed = checked(consumed + cycle.FoodConsumedRaw);
            unmet = checked(unmet + cycle.FoodUnmetRaw);
        }
        return new RecentPhysicalEconomicCycles(
            count, count == 0 ? 0UL : history[history.Count - count].MovementTurn,
            count == 0 ? 0UL : history[^1].MovementTurn,
            produced, consumed, unmet);
    }

    /// <summary>
    /// Predict five or fewer economic settlements by scanning only the
    /// versioned cadence calendar. Does not advance the world or append events.
    /// </summary>
    public static PhysicalEconomicBatchPlan PreviewBatch(
        CanonicalPhysicalTacticalSession session, int cycleCount = 5)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (cycleCount is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(cycleCount));
        var state = session.State;
        var startingTurn = state.MovementTurn;
        var currentTurn = startingTurn;
        var lastSettlement = state.Cycles.Count == 0
            ? 0UL : state.Cycles[^1].MovementTurn;
        var predictedCycles = 0;
        while (predictedCycles < cycleCount)
        {
            currentTurn = checked(currentTurn + 1UL);
            if (!PhysicalEconomicCadence.IsSettlementTurn(
                    currentTurn, lastSettlement, session.CadenceMode))
                continue;
            lastSettlement = currentTurn;
            predictedCycles++;
        }
        return new PhysicalEconomicBatchPlan(startingTurn, currentTurn,
            predictedCycles, checked(currentTurn - startingTurn));
    }

    /// <summary>
    /// VS23: compose up to five normal economic-cycle commands. Journal
    /// capacity is checked before any tick is committed. Unexpected runtime
    /// failure may leave already-committed turns intact (no fake rollback).
    /// </summary>
    public static PhysicalEconomicBatchPlan AdvanceBatch(
        CanonicalPhysicalTacticalSession session, int cycleCount = 5)
    {
        var plan = PreviewBatch(session, cycleCount);
        var remaining = CanonicalPhysicalTacticalSession.MaximumActions -
            session.Events.Count;
        if (plan.MovementTurnsToAdvance > (ulong)remaining)
            throw new InvalidOperationException(
                "Not enough canonical journal capacity for the requested economic cycles.");
        for (var cycleIndex = 0; cycleIndex < cycleCount; cycleIndex++)
            AdvanceToNext(session);
        if (session.State.MovementTurn != plan.FinalSettlementTurn)
            throw new InvalidOperationException(
                "Multi-cycle progression diverged from its canonical cadence preview.");
        return plan;
    }
}
