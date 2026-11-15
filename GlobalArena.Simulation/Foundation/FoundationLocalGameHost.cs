using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Immutable evidence of a locally committed one-command foundation turn.
/// The snapshot is owned by CanonicalGameSession, not the UI or a mock store.
/// </summary>
public sealed record FoundationLocalTurnReceipt(
    TurnNumber Turn,
    PhysicalWorkerId WorkerId,
    StrategicCellId Source,
    StrategicCellId Destination,
    PhysicalWorkerActivity Activity,
    ulong ResultingRevision,
    WorldStateHash ResultingHash,
    bool ReplayMatched,
    int ExecutedEvents);

/// <summary>
/// Small *gameplay owner* for the new physical-worker foundation. Owns real
/// CanonicalGameSession / PhasedTurnResolutionEngine / journal / commit gate.
/// Godot only sends intention (worker ID, target cell, activity) and reads
/// already-accepted snapshots. No market-clearing or financial settlement.
/// </summary>
public sealed class FoundationLocalGameHost
{
    private readonly CanonicalGameSession _session;
    private readonly AuthoritativeTurnCommitGate _publisher;
    private readonly PhysicalWorkerMoveEventExecutor _eventExecutor;
    private readonly CanonicalWorldStateHasher _hasher = new();

    public CanonicalGameSession Session => _session;
    public CivilizationId PlayerCivilization { get; }
    public TurnNumber NextTurn => _publisher.NextTurn;
    public WorldState Snapshot => _session.Snapshot;
    public WorldStateHash StateHash => _hasher.Compute(Snapshot);
    public bool IsPreviewScenario { get; }
    public IReadOnlyList<PhysicalWorkerRuntimeRecord> ControllableWorkers =>
        Array.AsReadOnly(Snapshot.Workers.Workers
            .Where(worker => worker.Owner == PlayerCivilization)
            .OrderBy(worker => worker.Id.Value).ToArray());

    private FoundationLocalGameHost(
        CanonicalGameSession session, CivilizationId playerCivilization,
        bool previewScenario)
    {
        _session = session;
        PlayerCivilization = playerCivilization;
        IsPreviewScenario = previewScenario;
        _eventExecutor = new PhysicalWorkerMoveEventExecutor(session.GeneratedWorld);
        var resolver = new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[]
            {
                new PhysicalWorkerMovementTurnPhase(session.GeneratedWorld)
            });
        _publisher = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(), resolver),
            new TurnNumber(1UL), rulesetVersion: 1U);
    }

    /// <summary>
    /// General explicit setup: a game owner supplies actual civilizations and
    /// individually located workers. No implicit workers or city classes.
    /// Validates the entire setup before publishing the first canonical state.
    /// </summary>
    public static FoundationLocalGameHost StartNew(
        WorldGenerationRequest worldRequest,
        IEnumerable<CivilizationRuntimeRecord> civilizations,
        IEnumerable<PhysicalWorkerRuntimeRecord> workers,
        CivilizationId playerCivilization)
    {
        ArgumentNullException.ThrowIfNull(worldRequest);
        ArgumentNullException.ThrowIfNull(civilizations);
        ArgumentNullException.ThrowIfNull(workers);
        if (!playerCivilization.IsValid)
            throw new ArgumentException("Player civilization must be valid.",
                nameof(playerCivilization));
        var session = CanonicalGameSession.Start(worldRequest);
        var civilizationState = new CivilizationRuntimeState(civilizations);
        if (!civilizationState.Civilizations.Contains(playerCivilization))
            throw new ArgumentException(
                "Player civilization must exist in the initial roster.",
                nameof(playerCivilization));
        var census = new PhysicalWorkerRuntimeState(workers);
        // The exact immutable WorldState enforces world/civilization membership.
        var initial = session.Snapshot
            .WithCivilizations(civilizationState)
            .WithWorkers(census);
        session.AcceptSnapshot(initial);
        return new FoundationLocalGameHost(session, playerCivilization,
            previewScenario: false);
    }

    /// <summary>
    /// Explicitly labeled local preview, NOT canonical world population policy.
    /// Two civilizations and six physical workers on actual Goldberg cells.
    /// Chosen indices are deterministic scenario spawn points only; all cells
    /// obey identical economic/movement laws. Other start configurations are
    /// supported by StartNew without changing a single tile rule.
    /// </summary>
    public static FoundationLocalGameHost CreateLocalPreview(
        WorldGenerationRequest worldRequest)
    {
        ArgumentNullException.ThrowIfNull(worldRequest);
        var session = CanonicalGameSession.Start(worldRequest);
        var graph = session.GeneratedWorld.StrategicSurfaceGraph;
        if (graph.NodeCount < 4)
            throw new ArgumentException(
                "Preview scenario needs at least four Goldberg cells.",
                nameof(worldRequest));

        var a = graph.GetCellId(0);
        var aNear = graph.GetCellId(graph.GetNeighborIndexes(0)[0]);
        var opposite = graph.NodeCount / 2;
        var b = graph.GetCellId(opposite);
        var bNear = graph.GetCellId(graph.GetNeighborIndexes(opposite)[0]);

        var civilizationOne = new CivilizationId(1UL);
        var civilizationTwo = new CivilizationId(2UL);
        var civilizationState = new CivilizationRuntimeState(
            new[]
            {
                new CivilizationRuntimeRecord(civilizationOne, a),
                new CivilizationRuntimeRecord(civilizationTwo, b)
            });
        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), civilizationOne, a,
                new CommodityId(1U), 2_000_000UL,
                PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), civilizationOne, a,
                new CommodityId(2U), 1_000_000UL,
                PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(3UL), civilizationOne, aNear,
                new CommodityId(1U), 0UL,
                PhysicalWorkerActivity.Military),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(4UL), civilizationTwo, b,
                new CommodityId(2U), 2_000_000UL,
                PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(5UL), civilizationTwo, b,
                new CommodityId(1U), 1_000_000UL,
                PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(6UL), civilizationTwo, bNear,
                new CommodityId(1U), 0UL,
                PhysicalWorkerActivity.Military)
        });
        session.AcceptSnapshot(session.Snapshot
            .WithCivilizations(civilizationState)
            .WithWorkers(census));
        return new FoundationLocalGameHost(session, civilizationOne,
            previewScenario: true);
    }

    public bool CanMove(PhysicalWorkerId workerId, StrategicCellId destination)
    {
        if (!workerId.IsValid || !destination.IsValid)
            return false;
        var worker = ControllableWorkers.FirstOrDefault(w => w.Id == workerId);
        if (worker is null || worker.CellId == destination)
            return false;
        try
        {
            return _eventExecutor.CheckEdge(worker.CellId, destination)
                .HasValue;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool CanChangeActivity(
        PhysicalWorkerId workerId, PhysicalWorkerActivity activity)
    {
        if (!workerId.IsValid || !Enum.IsDefined(activity))
            return false;
        var worker = ControllableWorkers.FirstOrDefault(w => w.Id == workerId);
        return worker is not null && worker.Activity != activity &&
            (activity != PhysicalWorkerActivity.Producing ||
                worker.OutputMicroPerTurn > 0UL);
    }

    /// <summary>
    /// A click is a complete locally closed turn in this preliminary sandbox.
    /// There is no cross-player simultaneous scheduling or economic turn here.
    /// </summary>
    public FoundationLocalTurnReceipt MoveWorker(
        PhysicalWorkerId workerId, StrategicCellId destination)
    {
        if (!CanMove(workerId, destination))
            throw new InvalidOperationException(
                "Worker is not controlled by this player or target is not adjacent.");
        return Commit(workerId, destination, nextActivity: null);
    }

    public FoundationLocalTurnReceipt ChangeWorkerActivity(
        PhysicalWorkerId workerId, PhysicalWorkerActivity activity)
    {
        if (!CanChangeActivity(workerId, activity))
            throw new InvalidOperationException(
                "Worker activity change is invalid for this player's unit.");
        var worker = ControllableWorkers.Single(w => w.Id == workerId);
        return Commit(workerId, worker.CellId, activity);
    }

    private FoundationLocalTurnReceipt Commit(
        PhysicalWorkerId workerId, StrategicCellId destination,
        PhysicalWorkerActivity? nextActivity)
    {
        var initial = Snapshot;
        var context = new SimulationContext(
            _publisher.NextTurn, new SimulationSeed(712UL));
        var command = new PhysicalWorkerMoveCommand(
            new CommandId(context.Turn, 1UL),
            PlayerCivilization, workerId, destination, nextActivity);
        var resolution = _publisher.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(context.Turn, true, false),
                new TurnResolutionInput(initial,
                    new ISimulationCommand[] { command }, context)));
        if (!resolution.WasResolved ||
            resolution.ResolutionResult is not { } resolved)
            throw new InvalidOperationException(
                "Local physical-worker turn was not committed.");
        var ev = resolved.Events.Single() as PhysicalWorkerMovedEvent
            ?? throw new InvalidOperationException(
                "Local physical-worker turn did not produce its journal event.");

        // Defense-in-depth; journal replay must reconstruct the committed hash.
        var replayed = new SimulationEventLogReplayer(_eventExecutor)
            .Replay(new SimulationReplayInput(initial, resolved.EventLog, context));
        var resultHash = _hasher.Compute(Snapshot);
        if (_hasher.Compute(replayed) != resultHash)
            throw new InvalidOperationException(
                "Committed worker state diverges from journal replay.");
        return new FoundationLocalTurnReceipt(
            context.Turn, workerId, ev.Source, ev.Destination,
            ev.NextActivity, Snapshot.Revision, resultHash,
            ReplayMatched: true, resolved.EventLog.Entries.Count);
    }
}
