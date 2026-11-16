using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A participant's *private* pending intention. This is not a published
/// event or a change to the canonical WorldState.
/// </summary>
public sealed record FoundationCivilizationWorkerOrder(
    CivilizationId Issuer,
    PhysicalWorkerId WorkerId,
    StrategicCellId Source,
    StrategicCellId Destination,
    PhysicalWorkerActivity Activity,
    bool IsMovement);

public sealed record FoundationCollectiveWorkerReceipt(
    CivilizationId Issuer,
    PhysicalWorkerId WorkerId,
    StrategicCellId Source,
    StrategicCellId Destination,
    PhysicalWorkerActivity Activity,
    StrategicEdgeId? TraversedEdge);

/// <summary>
/// Exactly one committed turn shared by every required civilization.
/// WorldState revision counts executed worker events; it does not count turns.
/// </summary>
public sealed class FoundationCollectiveTurnReceipt
{
    public TurnNumber Turn { get; }
    public WorldStateHash Hash { get; }
    public ulong Revision { get; }
    public IReadOnlyList<FoundationCollectiveWorkerReceipt> Events { get; }
    public int ExecutedEvents => Events.Count;
    public bool ReplayMatched { get; }

    internal FoundationCollectiveTurnReceipt(
        TurnNumber turn, WorldStateHash hash, ulong revision,
        FoundationCollectiveWorkerReceipt[] events)
    {
        Turn = turn;
        Hash = hash;
        Revision = revision;
        Events = Array.AsReadOnly(events);
        ReplayMatched = true;
    }
}

/// <summary>
/// Foundation-only collective turn owner. All participating civilizations
/// prepare orders against the SAME start-of-turn authoritative snapshot, with
/// private per-civilization queues. Nothing moves until ALL participants ready
/// and the game owner explicitly calls CloseReadyTurn.
///
/// This is NOT networking, multiplayer authentication, economic settlement,
/// combat, competitive conflict resolution or a simultaneous movement physics
/// solver. Independent physical worker orders are deterministically applied by
/// worker ID within one private resolution and published as one WorldState.
/// </summary>
public sealed class FoundationCollectiveTurnCoordinator
{
    private readonly CanonicalGameSession _session;
    private readonly AuthoritativeTurnCommitGate _publisher;
    private readonly PhysicalWorkerMoveEventExecutor _executor;
    private readonly CanonicalWorldStateHasher _hasher = new();
    private readonly CivilizationId[] _participants;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationCivilizationWorkerOrder>> _plans;
    private readonly HashSet<CivilizationId> _ready = new();
    private WorldState _knownSnapshot;

    public CanonicalGameSession Session => _session;
    public WorldState Snapshot => _session.Snapshot;
    public TurnNumber NextTurn => _publisher.NextTurn;
    public WorldStateHash StateHash => _hasher.Compute(Snapshot);
    public IReadOnlyList<CivilizationId> Participants =>
        Array.AsReadOnly(_participants);
    public IReadOnlyList<CivilizationId> ReadyCivilizations =>
        Array.AsReadOnly(_participants.Where(_ready.Contains).ToArray());
    public bool AllRequiredReady => _participants.All(_ready.Contains);
    public int PendingOrderCount => _plans.Values.Sum(x => x.Count);

    public FoundationCollectiveTurnCoordinator(CanonicalGameSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _knownSnapshot = session.Snapshot;
        if (!session.Snapshot.Workers.IsInitialized ||
            session.Snapshot.Workers.HasAuthoritativeTacticalLocations ||
            session.Snapshot.WorldBinding !=
                RuntimeWorldBinding.FromGeneratedWorld(session.GeneratedWorld))
            throw new InvalidOperationException(
                "Collective turns require canonical bound initialized physical workers.");

        _participants = session.Snapshot.Civilizations.Civilizations
            .OrderBy(id => id.Value).ToArray();
        if (_participants.Length == 0)
            throw new InvalidOperationException(
                "Collective turns require at least one registered civilization.");
        _plans = _participants.ToDictionary(
            id => id,
            _ => new SortedDictionary<ulong, FoundationCivilizationWorkerOrder>());
        _executor = new PhysicalWorkerMoveEventExecutor(session.GeneratedWorld);
        var engine = new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[]
            {
                new PhysicalWorkerMovementTurnPhase(session.GeneratedWorld)
            });
        _publisher = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), rulesetVersion: 1U);
    }

    /// <summary>Only the requesting civilization may read its own pending plan.</summary>
    public IReadOnlyList<FoundationCivilizationWorkerOrder> GetOwnOrders(
        CivilizationId requester)
    {
        RequireParticipant(requester);
        return Array.AsReadOnly(_plans[requester].Values.ToArray());
    }

    public FoundationCivilizationWorkerOrder QueueMove(
        CivilizationId issuer, PhysicalWorkerId workerId, StrategicCellId destination)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, workerId);
        if (!destination.IsValid || worker.CellId == destination ||
            _executor.CheckEdge(worker.CellId, destination) is null)
            throw new InvalidOperationException(
                "Collective worker move requires one real Goldberg adjacent tile.");

        var pending = new FoundationCivilizationWorkerOrder(
            issuer, workerId, worker.CellId, destination,
            PhysicalWorkerActivity.Traveling, IsMovement: true);
        _plans[issuer][workerId.Value] = pending;
        return pending;
    }

    public FoundationCivilizationWorkerOrder QueueActivity(
        CivilizationId issuer, PhysicalWorkerId workerId, PhysicalWorkerActivity activity)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, workerId);
        if (!Enum.IsDefined(activity) || worker.Activity == activity ||
            (activity == PhysicalWorkerActivity.Producing &&
                worker.OutputMicroPerTurn == 0UL))
            throw new InvalidOperationException(
                "Collective worker activity change is invalid.");
        var pending = new FoundationCivilizationWorkerOrder(
            issuer, workerId, worker.CellId, worker.CellId,
            activity, IsMovement: false);
        _plans[issuer][workerId.Value] = pending;
        return pending;
    }

    public bool CancelOwnOrder(CivilizationId issuer, PhysicalWorkerId workerId)
    {
        CheckWritable(issuer);
        return workerId.IsValid && _plans[issuer].Remove(workerId.Value);
    }

    public void SetReady(CivilizationId issuer)
    {
        CheckWritable(issuer);
        _ready.Add(issuer);
    }

    /// <summary>Allow a previously ready civilization to edit its own plan.</summary>
    public void Reopen(CivilizationId issuer)
    {
        CheckSnapshot();
        RequireParticipant(issuer);
        _ready.Remove(issuer);
    }

    /// <summary>
    /// Does not publish a turn while anyone remains unready, including a
    /// participant with zero workers and zero orders. Return null in that case.
    /// The caller can opt to wait; no implicit readiness or deadlines.
    /// </summary>
    public FoundationCollectiveTurnReceipt? TryCloseReadyTurn() =>
        AllRequiredReady ? CloseReadyTurn() : null;

    public FoundationCollectiveTurnReceipt CloseReadyTurn()
    {
        CheckSnapshot();
        if (!AllRequiredReady)
            throw new InvalidOperationException(
                "Cannot commit collective turn until every required civilization is ready.");

        var before = Snapshot;
        var turn = _publisher.NextTurn;
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var orders = _plans.Values.SelectMany(p => p.Values)
            .OrderBy(order => order.WorkerId.Value)
            .ToArray();

        if (orders.Select(x => x.WorkerId).Distinct().Count() != orders.Length)
            throw new InvalidOperationException(
                "A physical worker cannot be ordered by multiple civilizations.");

        // Revalidate against the very same pre-turn WorldState: no double work,
        // stale movements or changed owner may leak into a completed batch.
        foreach (var order in orders)
        {
            var worker = RequireOwnedWorker(order.Issuer, order.WorkerId);
            if (worker.CellId != order.Source ||
                (order.IsMovement &&
                    (_executor.CheckEdge(order.Source, order.Destination) is null ||
                     order.Activity != PhysicalWorkerActivity.Traveling)) ||
                (!order.IsMovement &&
                    (order.Source != order.Destination ||
                     order.Activity == worker.Activity ||
                     (order.Activity == PhysicalWorkerActivity.Producing &&
                        worker.OutputMicroPerTurn == 0UL))))
                throw new InvalidOperationException(
                    "A pending collective order does not match the canonical turn-start worker.");
        }

        var commands = orders.Select((order, index) =>
            (ISimulationCommand)new PhysicalWorkerMoveCommand(
                new CommandId(turn, checked((ulong)index + 1UL)),
                order.Issuer, order.WorkerId, order.Destination, order.Activity))
            .ToArray();

        var result = _publisher.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(turn, allRequiredParticipantsReady: true,
                    externalDeadlineReached: false),
                new TurnResolutionInput(before, commands, context)));
        if (!result.WasResolved || result.ResolutionResult is not { } resolved)
            throw new InvalidOperationException(
                "All-ready collective turn was not resolved.");
        var events = resolved.Events.Select(ev =>
            ev as PhysicalWorkerMovedEvent ??
                throw new InvalidOperationException(
                    "Collective turn contained a non-worker event."))
            .ToArray();
        if (events.Length != commands.Length ||
            resolved.EventLog.Entries.Count != events.Length ||
            resolved.EventLog.Entries.Any(e => !e.WasExecuted))
            throw new InvalidOperationException(
                "Collective worker journal does not match the ordered commands.");

        var replayed = new SimulationEventLogReplayer(_executor)
            .Replay(new SimulationReplayInput(before, resolved.EventLog, context));
        var hash = _hasher.Compute(Snapshot);
        if (_hasher.Compute(replayed) != hash)
            throw new InvalidOperationException(
                "Collective worker turn journal replay differs from committed state.");

        var receipt = new FoundationCollectiveTurnReceipt(
            turn, hash, Snapshot.Revision,
            events.Select(ev => new FoundationCollectiveWorkerReceipt(
                ev.Issuer, ev.WorkerId, ev.Source, ev.Destination,
                ev.NextActivity, ev.TraversedEdge)).ToArray());

        foreach (var plan in _plans.Values)
            plan.Clear();
        _ready.Clear();
        _knownSnapshot = Snapshot;
        return receipt;
    }

    private PhysicalWorkerRuntimeRecord RequireOwnedWorker(
        CivilizationId issuer, PhysicalWorkerId workerId)
    {
        if (!workerId.IsValid)
            throw new InvalidOperationException("Unknown physical worker.");
        var worker = Snapshot.Workers.Workers.FirstOrDefault(x => x.Id == workerId);
        if (worker is null || worker.Owner != issuer)
            throw new InvalidOperationException(
                "Civilization cannot order a non-owned physical worker.");
        return worker;
    }

    private void CheckWritable(CivilizationId issuer)
    {
        CheckSnapshot();
        RequireParticipant(issuer);
        if (_ready.Contains(issuer))
            throw new InvalidOperationException(
                "Ready civilization must reopen before changing any planned orders.");
    }

    private void RequireParticipant(CivilizationId issuer)
    {
        if (!issuer.IsValid || !_plans.ContainsKey(issuer))
            throw new InvalidOperationException(
                "Civilization is not a required participant in this collective turn.");
    }

    private void CheckSnapshot()
    {
        if (!ReferenceEquals(_knownSnapshot, Snapshot))
            throw new InvalidOperationException(
                "The canonical session changed outside the collective turn owner; explicit reconciliation required.");
    }
}
