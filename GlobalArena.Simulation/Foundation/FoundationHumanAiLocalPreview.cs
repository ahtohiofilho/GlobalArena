using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Narrow bridge from the single-computer Godot worker-order UI to the
/// canonical all-ready Human+AI turn owner. No game rule is implemented in
/// Godot; this adapts the previously validated G/P/X/E UI contract only.
/// The existing FoundationLocalGameHost remains independently available for
/// backwards-compatible legacy foundation tests and tooling.
/// </summary>
public sealed class FoundationHumanAiLocalPreview
{
    private readonly FoundationHumanAiTurnHost _host;
    private readonly PhysicalWorkerMoveEventExecutor _edges;

    public CanonicalGameSession Session => _host.Session;
    public WorldState Snapshot => _host.Snapshot;
    public TurnNumber NextTurn => _host.NextTurn;
    public WorldStateHash StateHash => _host.StateHash;
    public CivilizationId PlayerCivilization { get; }
    public IReadOnlyList<CivilizationId> AiCivilizations => _host.AiCivilizations;
    public IReadOnlyList<CivilizationId> ReadyCivilizations => _host.ReadyCivilizations;
    public FoundationCollectiveTurnReceipt? LastTurnReceipt { get; private set; }
    public int LastAiEventCount => LastTurnReceipt?.Events.Count(
        ev => ev.Issuer != PlayerCivilization) ?? 0;
    public int PendingOrderCount => _host.GetHumanOrders(PlayerCivilization).Count;
    public IReadOnlyList<FoundationQueuedWorkerOrder> PendingOrders =>
        Array.AsReadOnly(_host.GetHumanOrders(PlayerCivilization)
            .Select(order => new FoundationQueuedWorkerOrder(
                order.WorkerId, order.Source, order.Destination,
                order.Activity, order.IsMovement)).ToArray());

    public IReadOnlyList<PhysicalWorkerRuntimeRecord> ControllableWorkers =>
        Array.AsReadOnly(Snapshot.Workers.Workers
            .Where(worker => worker.Owner == PlayerCivilization)
            .OrderBy(worker => worker.Id.Value).ToArray());

    private FoundationHumanAiLocalPreview(
        FoundationHumanAiTurnHost host, CivilizationId playerCivilization)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        PlayerCivilization = playerCivilization;
        if (!host.HumanCivilizations.Contains(playerCivilization))
            throw new ArgumentException(
                "Preview player must be an explicitly human civilization.",
                nameof(playerCivilization));
        _edges = new PhysicalWorkerMoveEventExecutor(Session.GeneratedWorld);
    }

    /// <summary>
    /// Concrete, LABLED TEST SCENARIO: reuses the exact 6 real physical workers
    /// from the foundation local preview, then seeds worker #4 as Traveling
    /// BEFORE building the collective host. AI Civ #2 will resume its existing
    /// productive capacity on its first ready turn without inventing a route,
    /// crop or worker. This is NOT a permanent worldgen/population policy.
    /// </summary>
    public static FoundationHumanAiLocalPreview CreateLocalPreview(
        WorldGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var materialized = FoundationLocalGameHost.CreateLocalPreview(request);
        var session = materialized.Session;
        var player = materialized.PlayerCivilization;
        var opponent = new CivilizationId(2UL);
        var workerFour = new PhysicalWorkerId(4UL);
        var current = session.Snapshot.Workers.Workers.Single(
            worker => worker.Id == workerFour && worker.Owner == opponent);
        if (current.Activity != PhysicalWorkerActivity.Producing ||
            current.OutputMicroPerTurn == 0UL)
            throw new InvalidOperationException(
                "Preview AI worker #4 must have real productive capacity.");
        var modified = new PhysicalWorkerRuntimeState(
            session.Snapshot.Workers.Workers.Select(worker =>
                worker.Id == workerFour
                    ? worker.WithLocationAndActivity(
                        worker.CellId, PhysicalWorkerActivity.Traveling)
                    : worker));
        session.AcceptSnapshot(session.Snapshot.WithWorkers(modified));
        var host = new FoundationHumanAiTurnHost(
            session, new[] { player },
            new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
            {
                [opponent] = new FoundationAiResumeProductionPolicy()
            });
        return new FoundationHumanAiLocalPreview(host, player);
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
            return _edges.CheckEdge(worker.CellId, destination).HasValue;
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
        return worker is not null &&
            worker.Activity != activity &&
            (activity != PhysicalWorkerActivity.Producing ||
                worker.OutputMicroPerTurn > 0UL);
    }

    public FoundationQueuedWorkerOrder QueueMove(
        PhysicalWorkerId workerId, StrategicCellId destination)
    {
        if (!CanMove(workerId, destination))
            throw new InvalidOperationException(
                "Invalid human worker move or Goldberg destination.");
        _host.QueueHumanMove(PlayerCivilization, workerId, destination);
        return PendingOrders.Single(x => x.WorkerId == workerId);
    }

    public FoundationQueuedWorkerOrder QueueActivity(
        PhysicalWorkerId workerId, PhysicalWorkerActivity activity)
    {
        if (!CanChangeActivity(workerId, activity))
            throw new InvalidOperationException(
                "Invalid human worker activity change.");
        _host.QueueHumanActivity(PlayerCivilization, workerId, activity);
        return PendingOrders.Single(x => x.WorkerId == workerId);
    }

    public bool CancelQueuedOrder(PhysicalWorkerId workerId) =>
        _host.CancelHumanOrder(PlayerCivilization, workerId);

    /// <summary>
    /// E closes the HUMAN turn and includes AI plans in the SAME authoritative
    /// collective resolution. The receipt exposes AI participation separately
    /// so the UI can report it without displaying an invented market result.
    /// No physical worker is moved while orders are being prepared.
    /// </summary>
    public FoundationCollectiveTurnReceipt EndTurn()
    {
        var initialTurn = NextTurn;
        _host.SetHumanReady(PlayerCivilization);
        try
        {
            var receipt = _host.TryCloseTurn()
                ?? throw new InvalidOperationException(
                    "Collective turn remained open despite the human and AI readiness.");
            LastTurnReceipt = receipt;
            return receipt;
        }
        catch
        {
            // If publication did not occur, let the human edit the pending
            // plan again. Never reopen a turn that was already published.
            if (NextTurn == initialTurn)
                _host.ReopenHuman(PlayerCivilization);
            throw;
        }
    }
}
