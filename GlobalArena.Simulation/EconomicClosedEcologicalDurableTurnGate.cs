using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Result of a policy-gated, commandless ecological turn. A projected event
/// is supplied ONLY when the sole durable GAFULL09 head accepted the turn.
/// Open turns neither project nor commit.
/// </summary>
public sealed class EconomicClosedEcologicalTurnResult
{
    public bool WasCommitted { get; }
    public EconomicEcologicalTurnProjectedEvent? AuditEvent { get; }
    public string? AcceptedCheckpointSha256Hex { get; }

    private EconomicClosedEcologicalTurnResult(
        bool committed, EconomicEcologicalTurnProjectedEvent? auditEvent,
        string? acceptedDigest)
    {
        WasCommitted = committed;
        AuditEvent = auditEvent;
        AcceptedCheckpointSha256Hex = acceptedDigest;
    }

    internal static EconomicClosedEcologicalTurnResult Open() =>
        new(false, null, null);

    internal static EconomicClosedEcologicalTurnResult Committed(
        EconomicEcologicalTurnProjectedEvent auditEvent, string digest) =>
        new(true, auditEvent, digest);
}

/// <summary>
/// G3-J3 bounded coordinator: run the EXISTING policy gate and phased engine
/// against a fresh accepted full-v9 snapshot, verify exactly one system-origin
/// ecological projection, and use the EXISTING durable host as the SOLE
/// publication owner. No second CanonicalGameSession.AcceptSnapshot,
/// AuthoritativeTurnCommitGate, or unsigned player-command execution.
/// The host's compare-and-swap protects the preflight/commit gap.
///
/// This is explicitly limited to ecological-only, commandless turns while
/// WorldState stays unchanged. General authenticated gameplay requires a
/// wider single-owner transaction and command/event journal.
/// </summary>
public sealed class EconomicClosedEcologicalDurableTurnGate
{
    private readonly WorldGenerationResult _generated;
    private readonly EconomicCompleteV9DurableSaveHost _durable;
    private readonly ITurnPolicy _policy;
    private readonly object _sync = new();

    public EconomicClosedEcologicalDurableTurnGate(
        WorldGenerationResult generatedWorld,
        EconomicCompleteV9DurableSaveHost durableHost,
        ITurnPolicy turnPolicy)
    {
        _generated = generatedWorld ??
            throw new ArgumentNullException(nameof(generatedWorld));
        _durable = durableHost ??
            throw new ArgumentNullException(nameof(durableHost));
        _policy = turnPolicy ??
            throw new ArgumentNullException(nameof(turnPolicy));
    }

    public EconomicClosedEcologicalTurnResult ResolveAndCommit(
        TurnPolicyInput closureInput,
        SimulationSeed simulationSeed,
        string expectedAcceptedCheckpointSha256Hex,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        ArgumentNullException.ThrowIfNull(closureInput);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);

        // This coordinator never accepts arbitrary ISimulationCommand inputs
        // or claims to authorize an external player's economic orders.
        lock (_sync)
        {
            var trusted = EconomicEcologicalSidecarArchive.NormalizeDigest(
                expectedAcceptedCheckpointSha256Hex);
            if (_durable.AcceptedSha256Hex != trusted)
                throw new InvalidOperationException(
                    "Turn was planned against a stale durable checkpoint.");
            if (_durable.LastAppliedTurn == ulong.MaxValue ||
                closureInput.Turn.Value != _durable.LastAppliedTurn + 1UL)
                throw new InvalidOperationException(
                    "Durable ecological gate requires the exact next turn.");

            // Snapshot the orders exactly once. The phase projection and
            // durable journal must consume identical immutable inputs.
            var demand = workerDemand.ToArray();
            var holds = originHolds.ToArray();
            if (demand.Any(x => x is null) || holds.Any(x => x is null))
                throw new ArgumentException("Closed turn inputs cannot contain null rows.");

            // No sidecar or head mutation occurs before closure policy passes.
            if (!_policy.ShouldClose(closureInput))
                return EconomicClosedEcologicalTurnResult.Open();

            // The durable snapshot is the only source of the accepted world,
            // including versioned physical transport facilities.
            var before = EconomicCompleteV9SaveBundleCodec.Decode(
                _generated, _durable.CaptureCompleteSave());
            before.RequireExpectedDigest(trusted);
            var phase = new EconomicEcologicalTurnProjectionPhase(
                _generated, before, trusted, demand, holds);
            var resolutionInput = new TurnResolutionInput(
                before.World, Array.Empty<ISimulationCommand>(),
                new SimulationContext(closureInput.Turn, simulationSeed));
            var gate = new TurnPolicyResolutionGate(
                _policy, new PhasedTurnResolutionEngine(
                    new ITurnResolutionPhase[] { phase }));
            var resolved = gate.Resolve(new TurnPolicyResolutionGateInput(
                closureInput, resolutionInput));

            // A policy with changing output within this single call fails
            // closed rather than letting a possibly open turn be committed.
            if (!resolved.WasResolved)
                throw new InvalidOperationException(
                    "Turn closure policy changed between preflight and resolution.");

            var result = resolved.ResolutionResult ??
                throw new InvalidOperationException(
                    "Closed phase produced no resolution result.");
            if (!ReferenceEquals(result.ResultingWorldState, before.World) ||
                result.Events.Count != 1 ||
                result.EventLog.Entries.Count != 1 ||
                result.Events[0] is not EconomicEcologicalTurnProjectedEvent projected)
                throw new InvalidOperationException(
                    "Closed ecological turn must have exactly one read-only projection event.");

            var log = result.EventLog.Entries[0];
            var id = projected.Id;
            if (log.EventId != id || !log.WasEligible || !log.WasExecuted ||
                id.OriginKind != EventOriginKind.System ||
                id.Turn != closureInput.Turn ||
                id.SystemPhaseId != EconomicProductionDemandTurnPhase.StablePhaseId ||
                id.SystemOrdinal != 1UL ||
                projected.PreviousCheckpointSha256Hex != trusted ||
                projected.Projection.ProposedNext.WorldHash != before.WorldHash)
                throw new InvalidOperationException(
                    "Phase audit event or log cannot attest to this turn.");

            // A separately simulated ecological turn is accepted only by
            // the one existing durable host. CAS detects any concurrent
            // acceptance after projection; its journal is replay-verified.
            var committed = _durable.CommitNext(
                trusted, closureInput.Turn.Value, demand, holds);
            if (committed.ProposedNext.Sha256Hex !=
                    projected.ProposedCheckpointSha256Hex)
                throw new InvalidOperationException(
                    "Durable ecological result diverged from the preflight phase.");

            return EconomicClosedEcologicalTurnResult.Committed(
                projected, committed.ProposedNext.Sha256Hex);
        }
    }
}
