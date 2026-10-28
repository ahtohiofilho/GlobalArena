using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Typed, system-origin audit evidence of ONE ecological turn PROPOSAL.
/// WasExecuted in the event log means that the evaluation ran, not that
/// ecological reserves were published or market transactions settled.
/// The proposed books remain outside the authoritative WorldState.
/// </summary>
public sealed class EconomicEcologicalTurnProjectedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public string PreviousCheckpointSha256Hex { get; }
    public EconomicEcologicalTurnCommitResult Projection { get; }
    public string ProposedCheckpointSha256Hex =>
        Projection.ProposedNext.Sha256Hex;

    public EconomicEcologicalTurnProjectedEvent(
        EventId id, string previousCheckpointSha256Hex,
        EconomicEcologicalTurnCommitResult projection)
    {
        if (id.OriginKind != EventOriginKind.System ||
            id.SystemPhaseId != EconomicProductionDemandTurnPhase.StablePhaseId ||
            id.SystemOrdinal != 1UL)
            throw new ArgumentException(
                "Ecological projection requires the stable economic system-phase identity.",
                nameof(id));
        ArgumentNullException.ThrowIfNull(projection);
        var previous = EconomicEcologicalSidecarArchive.NormalizeDigest(
            previousCheckpointSha256Hex);
        if (previous == projection.ProposedNext.Sha256Hex)
            throw new ArgumentException(
                "Projected ecological turn must create a new checkpoint.",
                nameof(projection));

        Id = id;
        PreviousCheckpointSha256Hex = previous;
        Projection = projection;
    }
}

/// <summary>
/// G3-J2 opt-in, strictly read-only bridge from G3 ecological production
/// to the EXISTING PhasedTurnResolutionEngine. Reuses the established
/// economy.production-demand phase ID to prevent duplicate workforce or
/// resident demand when composing an authoritative phase collection.
///
/// It does NOT call EconomicCompleteV9DurableSaveHost.CommitNext, mutate a
/// WorldState, or publish reserves. A later single-owner gate must link the
/// canonical event log to durable checkpoint acceptance as ONE transaction.
/// </summary>
public sealed class EconomicEcologicalTurnProjectionPhase : ITurnResolutionPhase
{
    private readonly WorldGenerationResult _generatedWorld;
    private readonly EconomicEcologicalCheckpoint _previous;
    private readonly string _expectedDigest;
    private readonly EconomicDemandProfile[] _demand;
    private readonly EconomicMaterialOriginHoldOrder[] _holds;
    private readonly EconomicEcologicalTurnCommitResolver _resolver = new();

    public string PhaseId => EconomicProductionDemandTurnPhase.StablePhaseId;

    public EconomicEcologicalTurnProjectionPhase(
        WorldGenerationResult generatedWorld,
        EconomicEcologicalCheckpoint acceptedPrevious,
        string expectedAcceptedCheckpointSha256Hex,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(acceptedPrevious);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        acceptedPrevious.RequireExpectedDigest(expectedAcceptedCheckpointSha256Hex);
        if (acceptedPrevious.World.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Phase ecological checkpoint is not bound to this generated world.");
        _generatedWorld = generatedWorld;
        _previous = acceptedPrevious;
        _expectedDigest = acceptedPrevious.Sha256Hex;
        _demand = workerDemand.ToArray();
        _holds = originHolds.ToArray();
        if (_demand.Any(x => x is null) || _holds.Any(x => x is null))
            throw new ArgumentException("Economic phase orders cannot contain null.");
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        // No hidden use of an older, parallel economic world. This bridge
        // runs only at the exact snapshot to which the accepted books bind.
        // An earlier phase changing WorldState must be reconciled with the
        // ecological books by a future atomic host before running this phase.
        if (!ReferenceEquals(planningInput.WorldState, _previous.World) ||
            !ReferenceEquals(stagedState, _previous.World))
            throw new InvalidOperationException(
                "Ecological phase requires its exact accepted turn-start and staged WorldState.");
        if (_previous.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
            planningInput.Context.Turn.Value !=
                _previous.NaturalCapital.LastAppliedTurn + 1UL)
            throw new InvalidOperationException(
                "Ecological phase requires the exact next uncommitted turn.");

        var projected = _resolver.Propose(
            _generatedWorld, _previous, _expectedDigest,
            planningInput.Context.Turn.Value, _demand, _holds);
        var evaluation = new EconomicEcologicalTurnProjectedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn, PhaseId, ordinal: 1UL),
            _expectedDigest, projected);

        // World is the same reference; event attests to evaluation only.
        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[] { evaluation },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL, evaluation, wasEligible: true, wasExecuted: true)
            }));
    }
}
