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
}
