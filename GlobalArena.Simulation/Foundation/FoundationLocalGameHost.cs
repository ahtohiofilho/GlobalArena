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
/// Pending UI intent; NOT an event, commerce, real worker motion or a snapshot.
/// A worker can have only one effective order per turn. Editing a pending
/// order replaces that worker's previous intent without changing WorldState.
/// </summary>
public sealed record FoundationQueuedWorkerOrder(
    PhysicalWorkerId WorkerId,
    StrategicCellId Source,
    StrategicCellId Destination,
    PhysicalWorkerActivity NextActivity,
    bool IsMovement);

/// <summary>
/// One authoritative publication after all worker orders for this turn.
/// Revision is event-count based, NOT a claim that there was only one event.
/// The journal is replayed from the pre-turn WorldState and compared by hash.
/// </summary>
public sealed class FoundationBatchTurnReceipt
{
    public TurnNumber Turn { get; }
    public ulong ResultingRevision { get; }
    public WorldStateHash ResultingHash { get; }
    public bool ReplayMatched { get; }
    public IReadOnlyList<FoundationLocalTurnReceipt> WorkerEvents { get; }
    public int ExecutedEvents => WorkerEvents.Count;

    internal FoundationBatchTurnReceipt(
        TurnNumber turn, ulong revision, WorldStateHash stateHash,
        FoundationLocalTurnReceipt[] events)
    {
        Turn = turn;
        ResultingRevision = revision;
        ResultingHash = stateHash;
        ReplayMatched = true;
        WorkerEvents = Array.AsReadOnly(events);
    }
}

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
    private readonly SortedDictionary<ulong, FoundationQueuedWorkerOrder> _pending = new();
    private WorldState _lastPublishedSnapshot;

    public CanonicalGameSession Session => _session;
    public CivilizationId PlayerCivilization { get; }
    public TurnNumber NextTurn => _publisher.NextTurn;
    public WorldState Snapshot => _session.Snapshot;
    public WorldStateHash StateHash => _hasher.Compute(Snapshot);
    public bool IsPreviewScenario { get; }
    public IReadOnlyList<FoundationQueuedWorkerOrder> PendingOrders =>
        Array.AsReadOnly(_pending.Values.ToArray());
    public int PendingOrderCount => _pending.Count;
    public IReadOnlyList<PhysicalWorkerRuntimeRecord> ControllableWorkers =>
        Array.AsReadOnly(Snapshot.Workers.Workers
            .Where(worker => worker.Owner == PlayerCivilization)
            .OrderBy(worker => worker.Id.Value).ToArray());

    private FoundationLocalGameHost(
        CanonicalGameSession session, CivilizationId playerCivilization,
        bool previewScenario)
    {
        _session = session;
        _lastPublishedSnapshot = session.Snapshot;
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
    /// Stages a worker move without advancing time or touching authoritative
    /// data. Replacing one worker's pending order is allowed; competing workers
    /// may converge on the same node (combat/capacity policies are NOT here).
    /// </summary>
    public FoundationQueuedWorkerOrder QueueMove(
        PhysicalWorkerId workerId, StrategicCellId destination)
    {
        CheckAuthoritativeOwner();
        if (!CanMove(workerId, destination))
            throw new InvalidOperationException(
                "Worker is foreign, does not exist, or destination is not an adjacent Goldberg tile.");
        var worker = ControllableWorkers.Single(w => w.Id == workerId);
        var planned = new FoundationQueuedWorkerOrder(
            workerId, worker.CellId, destination,
            PhysicalWorkerActivity.Traveling, IsMovement: true);
        _pending[workerId.Value] = planned;
        return planned;
    }

    public FoundationQueuedWorkerOrder QueueActivity(
        PhysicalWorkerId workerId, PhysicalWorkerActivity activity)
    {
        CheckAuthoritativeOwner();
        if (!CanChangeActivity(workerId, activity))
            throw new InvalidOperationException(
                "Worker is foreign, absent, or its activity cannot be changed.");
        var worker = ControllableWorkers.Single(w => w.Id == workerId);
        var planned = new FoundationQueuedWorkerOrder(
            workerId, worker.CellId, worker.CellId,
            activity, IsMovement: false);
        _pending[workerId.Value] = planned;
        return planned;
    }

    public bool CancelQueuedOrder(PhysicalWorkerId workerId)
    {
        CheckAuthoritativeOwner();
        return workerId.IsValid && _pending.Remove(workerId.Value);
    }

    public void ClearQueuedOrders()
    {
        CheckAuthoritativeOwner();
        _pending.Clear();
    }

    /// <summary>
    /// Close ONE turn, regardless of the number of physical worker orders.
    /// All commands receive the same turn ID; worker identity fixes their
    /// canonical order and independent command sequence numbers. The resolver
    /// stages complete effects privately; a malformed command never partially
    /// publishes. After successful commit the entire journal is replayed.
    ///
    /// An empty turn is valid: it changes NextTurn, but must not invent
    /// events, movement, production or a new worker-state revision.
    /// </summary>
    public FoundationBatchTurnReceipt EndTurn()
    {
        CheckAuthoritativeOwner();
        var original = Snapshot;
        var context = new SimulationContext(
            _publisher.NextTurn, new SimulationSeed(712UL));
        var commands = _pending.Values
            .Select((order, index) => (ISimulationCommand)
                new PhysicalWorkerMoveCommand(
                    new CommandId(context.Turn, checked((ulong)index + 1UL)),
                    PlayerCivilization, order.WorkerId,
                    order.Destination, order.NextActivity))
            .ToArray();
        var gate = _publisher.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(context.Turn, true, false),
                new TurnResolutionInput(original, commands, context)));
        if (!gate.WasResolved || gate.ResolutionResult is not { } result)
            throw new InvalidOperationException("Foundation turn was not closed.");

        var events = result.Events
            .Select(ev => ev as PhysicalWorkerMovedEvent ??
                throw new InvalidOperationException(
                    "Unrecognized authoritative worker event."))
            .ToArray();
        if (events.Length != commands.Length ||
            result.EventLog.Entries.Count != events.Length ||
            result.EventLog.Entries.Any(x => !x.WasExecuted))
            throw new InvalidOperationException(
                "Committed turn journal does not match queued worker orders.");

        var replayed = new SimulationEventLogReplayer(_eventExecutor)
            .Replay(new SimulationReplayInput(original, result.EventLog, context));
        var hash = _hasher.Compute(Snapshot);
        if (_hasher.Compute(replayed) != hash)
            throw new InvalidOperationException(
                "Committed worker batch diverges from deterministic journal replay.");

        var receipts = events.Select(ev => new FoundationLocalTurnReceipt(
            context.Turn, ev.WorkerId, ev.Source, ev.Destination,
            ev.NextActivity, Snapshot.Revision, hash,
            ReplayMatched: true, ExecutedEvents: events.Length)).ToArray();

        _pending.Clear();
        _lastPublishedSnapshot = Snapshot;
        return new FoundationBatchTurnReceipt(
            context.Turn, Snapshot.Revision, hash, receipts);
    }

    private void CheckAuthoritativeOwner()
    {
        if (!ReferenceEquals(_lastPublishedSnapshot, _session.Snapshot))
            throw new InvalidOperationException(
                "Canonical snapshot changed outside the local turn owner; pending orders require explicit reconciliation.");
    }

    /// <summary>
    /// Legacy convenience APIs retained for the existing single-order tests,
    /// console and non-UI callers. New Godot gameplay uses Queue* + EndTurn.
    /// A queued batch must NEVER be silently committed by a legacy click.
    /// </summary>
    private FoundationLocalTurnReceipt CommitSingle(
        PhysicalWorkerId workerId, StrategicCellId destination,
        PhysicalWorkerActivity? nextActivity)
    {
        CheckAuthoritativeOwner();
        if (_pending.Count != 0)
            throw new InvalidOperationException(
                "Cannot bypass a pending multi-worker turn using the legacy single-order command.");
        if (nextActivity.HasValue)
            QueueActivity(workerId, nextActivity.Value);
        else
            QueueMove(workerId, destination);
        var result = EndTurn();
        return result.WorkerEvents.Single();
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
        return CommitSingle(workerId, destination, nextActivity: null);
    }

    public FoundationLocalTurnReceipt ChangeWorkerActivity(
        PhysicalWorkerId workerId, PhysicalWorkerActivity activity)
    {
        if (!CanChangeActivity(workerId, activity))
            throw new InvalidOperationException(
                "Worker activity change is invalid for this player's unit.");
        var worker = ControllableWorkers.Single(w => w.Id == workerId);
        return CommitSingle(workerId, worker.CellId, activity);
    }

}
