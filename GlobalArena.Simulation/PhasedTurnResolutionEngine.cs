using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A headless domain-owned phase. PlanningInput always contains the original
/// turn-start snapshot and the complete closed command batch. StagedState is
/// the private output of preceding phases, never a published game snapshot.
/// Implementations own their domain semantics and must emit an auditable
/// TurnResolutionResult for their effects.
/// </summary>
public interface ITurnResolutionPhase
{
    string PhaseId { get; }

    TurnResolutionResult Resolve(TurnResolutionInput planningInput, WorldState stagedState);
}

/// <summary>
/// Ordered, deterministic composition of externally supplied phases. This
/// infrastructure does not choose the definitive order of economic, movement,
/// combat, or reconciliation policies and does not itself execute domain rules.
/// Every phase is invoked even when no player commands exist. Only the complete
/// result may later pass through AuthoritativeTurnCommitGate to the session.
/// </summary>
public sealed class PhasedTurnResolutionEngine : ITurnResolutionEngine
{
    private readonly IReadOnlyList<ITurnResolutionPhase> _phases;

    public IReadOnlyList<string> PhaseIds { get; }

    public PhasedTurnResolutionEngine(IEnumerable<ITurnResolutionPhase> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        var snapshot = phases.ToArray();
        if (snapshot.Length == 0)
            throw new ArgumentException("A phased turn engine requires at least one explicit phase.", nameof(phases));

        var identities = new HashSet<string>(StringComparer.Ordinal);
        var phaseIds = new string[snapshot.Length];
        for (var index = 0; index < snapshot.Length; index++)
        {
            var phase = snapshot[index] ??
                throw new ArgumentException("A phase cannot be null.", nameof(phases));
            var id = phase.PhaseId;
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A phase must have a nonempty identity.", nameof(phases));
            if (!identities.Add(id))
                throw new ArgumentException("Duplicate phase identity: " + id, nameof(phases));
            phaseIds[index] = id;
        }

        _phases = Array.AsReadOnly(snapshot);
        PhaseIds = Array.AsReadOnly(phaseIds);
    }

    public TurnResolutionResult Resolve(TurnResolutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var staged = input.WorldState;
        var allEvents = new List<ISimulationEvent>();
        var allEntries = new List<SimulationEventLogEntry>();
        var observedEventIds = new HashSet<EventId>();

        for (var phaseIndex = 0; phaseIndex < _phases.Count; phaseIndex++)
        {
            var phase = _phases[phaseIndex];
            var id = PhaseIds[phaseIndex];
            if (!string.Equals(phase.PhaseId, id, StringComparison.Ordinal))
                throw new InvalidOperationException("Phase identity changed after registration: " + id);

            var result = phase.Resolve(input, staged) ??
                throw new InvalidOperationException("Phase returned null: " + id);
            var next = result.ResultingWorldState;
            if (next.WorldBinding != staged.WorldBinding)
                throw new InvalidOperationException("Phase changed the authoritative world binding: " + id);
            if (next.Revision < staged.Revision)
                throw new InvalidOperationException("Phase regressed the state revision: " + id);
            if (result.Events.Count != result.EventLog.Entries.Count)
                throw new InvalidOperationException("Phase event list and log do not agree: " + id);

            for (var index = 0; index < result.Events.Count; index++)
            {
                var simulationEvent = result.Events[index] ??
                    throw new InvalidOperationException("Phase returned a null event: " + id);
                var entry = result.EventLog.Entries[index];
                if (entry.EventId != simulationEvent.Id)
                    throw new InvalidOperationException("Phase event log identity differs from ordered events: " + id);
                if (simulationEvent.Id.OriginCommandId.Turn != input.Context.Turn)
                    throw new InvalidOperationException("Phase event belongs to a different turn: " + id);
                if (!observedEventIds.Add(simulationEvent.Id))
                    throw new InvalidOperationException("Repeated event identity across phases: " + id);
                allEvents.Add(simulationEvent);
                allEntries.Add(new SimulationEventLogEntry(
                    checked((ulong)allEntries.Count + 1UL), simulationEvent,
                    entry.WasEligible, entry.WasExecuted));
            }

            // A new immutable state needs traceable executed events; a no-op
            // phase can return the exact input object with an empty log.
            if (!ReferenceEquals(next, staged) &&
                !result.EventLog.Entries.Any(entry => entry.WasExecuted))
                throw new InvalidOperationException("Phase changed state without an executed event: " + id);

            staged = next;
        }

        return new TurnResolutionResult(
            staged, allEvents, new SimulationEventLog(allEntries));
    }
}
