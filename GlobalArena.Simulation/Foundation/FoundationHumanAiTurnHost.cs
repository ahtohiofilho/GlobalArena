using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// One explicit AI proposal against the canonical, unchanged turn-start
/// WorldState. It is neither an executable event nor an economic decision.
/// </summary>
public sealed record FoundationAiWorkerDecision(
    PhysicalWorkerId WorkerId,
    StrategicCellId Destination,
    PhysicalWorkerActivity Activity,
    bool IsMovement);

/// <summary>
/// AI policy must return a finite, deterministic set of intents; the game
/// owner checks every proposal before accepting any. Policies cannot publish
/// WorldState and are not assumed to be strategic or economically optimal.
/// </summary>
public interface IFoundationAiTurnPolicy
{
    IReadOnlyList<FoundationAiWorkerDecision> Plan(
        CivilizationId civilization, WorldState snapshot,
        WorldGenerationResult generatedWorld, TurnNumber turn);
}

/// <summary>
/// Safe baseline for an AI with no strategic rules yet. It intentionally
/// submits NO orders and confirms readiness; no phantom travel, workforce,
/// demand or prices are generated.
/// </summary>
public sealed class FoundationAiHoldPositionPolicy : IFoundationAiTurnPolicy
{
    public IReadOnlyList<FoundationAiWorkerDecision> Plan(
        CivilizationId civilization, WorldState snapshot,
        WorldGenerationResult generatedWorld, TurnNumber turn)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        return Array.Empty<FoundationAiWorkerDecision>();
    }
}

/// <summary>
/// Opt-in, narrow AI activity policy: a traveling worker that already has a
/// positive productive capacity may return to Producing at its CURRENT tile.
/// This is not a crop-selection, route-finding, war or market strategy.
/// </summary>
public sealed class FoundationAiResumeProductionPolicy : IFoundationAiTurnPolicy
{
    public IReadOnlyList<FoundationAiWorkerDecision> Plan(
        CivilizationId civilization, WorldState snapshot,
        WorldGenerationResult generatedWorld, TurnNumber turn)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        return Array.AsReadOnly(snapshot.Workers.Workers
            .Where(worker => worker.Owner == civilization &&
                worker.Activity == PhysicalWorkerActivity.Traveling &&
                worker.OutputMicroPerTurn > 0UL)
            .OrderBy(worker => worker.Id.Value)
            .Select(worker => new FoundationAiWorkerDecision(
                worker.Id, worker.CellId,
                PhysicalWorkerActivity.Producing, IsMovement: false))
            .ToArray());
    }
}

/// <summary>
/// Explicit single-computer HUMAN+AI gameplay orchestration over the existing
/// collective readiness and journal gate. Every civilization receives exactly
/// one role; human decisions are manual, AI decisions are returned by an
/// injected policy. There is no network/user authentication, no implicit
/// deadline, and no race against the game's own authoritative snapshot.
///
/// Human + AI orders share ONE closed turn. AI policy output is prevalidated
/// before any AI plan is submitted; invalid policy output cannot partially
/// publish a turn or advance the clock. No economic clearing is executed.
/// </summary>
public sealed class FoundationHumanAiTurnHost
{
    private readonly FoundationCollectiveTurnCoordinator _collective;
    private readonly HashSet<CivilizationId> _humans;
    private readonly IReadOnlyDictionary<CivilizationId, IFoundationAiTurnPolicy> _ai;
    private readonly PhysicalWorkerMoveEventExecutor _edges;

    public CanonicalGameSession Session => _collective.Session;
    public WorldState Snapshot => _collective.Snapshot;
    public TurnNumber NextTurn => _collective.NextTurn;
    public WorldStateHash StateHash => _collective.StateHash;
    public IReadOnlyList<CivilizationId> HumanCivilizations =>
        Array.AsReadOnly(_collective.Participants
            .Where(_humans.Contains).ToArray());
    public IReadOnlyList<CivilizationId> AiCivilizations =>
        Array.AsReadOnly(_collective.Participants
            .Where(_ai.ContainsKey).ToArray());
    public IReadOnlyList<CivilizationId> ReadyCivilizations =>
        _collective.ReadyCivilizations;
    public bool AllRequiredReady => _collective.AllRequiredReady;

    public FoundationHumanAiTurnHost(
        CanonicalGameSession session,
        IEnumerable<CivilizationId> humanCivilizations,
        IReadOnlyDictionary<CivilizationId, IFoundationAiTurnPolicy> aiPolicies)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(humanCivilizations);
        ArgumentNullException.ThrowIfNull(aiPolicies);
        _collective = new FoundationCollectiveTurnCoordinator(session);
        var humans = humanCivilizations.ToArray();
        if (humans.Any(id => !id.IsValid) ||
            humans.Distinct().Count() != humans.Length)
            throw new ArgumentException(
                "Human civilization IDs must be valid and unique.",
                nameof(humanCivilizations));
        _humans = humans.ToHashSet();
        if (aiPolicies.Any(entry => !entry.Key.IsValid ||
                entry.Value is null))
            throw new ArgumentException(
                "AI policy IDs and instances must be non-null and valid.",
                nameof(aiPolicies));

        var participants = _collective.Participants.ToHashSet();
        var roles = humans.Concat(aiPolicies.Keys).ToArray();
        if (roles.Length != participants.Count ||
            !participants.SetEquals(roles) ||
            roles.Distinct().Count() != roles.Length)
            throw new ArgumentException(
                "Every registered civilization needs exactly one Human or AI role.");

        // Freeze the policy dictionary; callers may mutate their original
        // dictionary without replacing a role midway through a turn.
        _ai = new Dictionary<CivilizationId, IFoundationAiTurnPolicy>(aiPolicies);
        _edges = new PhysicalWorkerMoveEventExecutor(session.GeneratedWorld);
    }

    public IReadOnlyList<FoundationCivilizationWorkerOrder> GetHumanOrders(
        CivilizationId human)
    {
        RequireHuman(human);
        return _collective.GetOwnOrders(human);
    }

    public void QueueHumanMove(
        CivilizationId human, PhysicalWorkerId worker,
        StrategicCellId destination)
    {
        RequireHuman(human);
        _collective.QueueMove(human, worker, destination);
    }

    public void QueueHumanActivity(
        CivilizationId human, PhysicalWorkerId worker,
        PhysicalWorkerActivity activity)
    {
        RequireHuman(human);
        _collective.QueueActivity(human, worker, activity);
    }

    public bool CancelHumanOrder(
        CivilizationId human, PhysicalWorkerId worker)
    {
        RequireHuman(human);
        return _collective.CancelOwnOrder(human, worker);
    }

    public void SetHumanReady(CivilizationId human)
    {
        RequireHuman(human);
        _collective.SetReady(human);
    }

    public void ReopenHuman(CivilizationId human)
    {
        RequireHuman(human);
        _collective.Reopen(human);
    }

    /// <summary>
    /// Runs every not-yet-ready AI policy once, in ascending civilization ID.
    /// Repeated calls in the same turn are idempotent, with no extra events.
    /// All AI proposals are fully checked before enqueuing; the only effects
    /// are pending orders and readiness, not worker/state mutations.
    /// </summary>
    public void PrepareAiCivilizations()
    {
        var ready = _collective.ReadyCivilizations.ToHashSet();
        foreach (var civ in _collective.Participants.Where(_ai.ContainsKey))
        {
            if (ready.Contains(civ))
                continue;
            var before = _collective.Snapshot;
            var decisions = _ai[civ].Plan(
                civ, before, Session.GeneratedWorld, NextTurn)
                ?? throw new InvalidOperationException(
                    "AI policy returned a null plan.");
            if (!ReferenceEquals(before, _collective.Snapshot))
                throw new InvalidOperationException(
                    "AI policy changed the authoritative snapshot while planning.");
            var proposals = decisions.ToArray();
            ValidateAiPlan(civ, proposals);
            var enqueued = new List<PhysicalWorkerId>();
            try
            {
                foreach (var decision in proposals)
                {
                    if (decision.IsMovement)
                        _collective.QueueMove(civ, decision.WorkerId,
                            decision.Destination);
                    else
                        _collective.QueueActivity(civ, decision.WorkerId,
                            decision.Activity);
                    enqueued.Add(decision.WorkerId);
                }
                _collective.SetReady(civ);
            }
            catch
            {
                // Never leave a partially staged AI plan behind after failure.
                foreach (var worker in enqueued)
                    _collective.CancelOwnOrder(civ, worker);
                throw;
            }
        }
    }

    /// <summary>
    /// Prepares AI (unless already ready) and then attempts to close the
    /// collective turn. It stays open until EVERY human has confirmed. AI
    /// decisions are not re-generated on every click or polling request.
    /// </summary>
    public FoundationCollectiveTurnReceipt? TryCloseTurn()
    {
        PrepareAiCivilizations();
        return _collective.TryCloseReadyTurn();
    }

    private void ValidateAiPlan(
        CivilizationId civilization, FoundationAiWorkerDecision[] plan)
    {
        var seen = new HashSet<PhysicalWorkerId>();
        foreach (var decision in plan)
        {
            if (decision is null || !decision.WorkerId.IsValid ||
                !decision.Destination.IsValid ||
                !Enum.IsDefined(decision.Activity) ||
                !seen.Add(decision.WorkerId))
                throw new InvalidOperationException(
                    "AI policy returned a duplicate or invalid worker decision.");

            var worker = Snapshot.Workers.Workers.FirstOrDefault(w =>
                w.Id == decision.WorkerId && w.Owner == civilization);
            if (worker is null)
                throw new InvalidOperationException(
                    "AI policy attempted to order a foreign or nonexistent worker.");

            if (decision.IsMovement)
            {
                if (decision.Destination == worker.CellId ||
                    decision.Activity != PhysicalWorkerActivity.Traveling ||
                    _edges.CheckEdge(worker.CellId, decision.Destination) is null)
                    throw new InvalidOperationException(
                        "AI policy submitted an invalid Goldberg movement.");
            }
            else if (worker.CellId != decision.Destination ||
                worker.Activity == decision.Activity ||
                (decision.Activity == PhysicalWorkerActivity.Producing &&
                    worker.OutputMicroPerTurn == 0UL))
            {
                throw new InvalidOperationException(
                    "AI policy submitted an invalid local worker activity.");
            }
        }
    }

    private void RequireHuman(CivilizationId civilization)
    {
        if (!civilization.IsValid || !_humans.Contains(civilization))
            throw new InvalidOperationException(
                "Only an explicitly assigned human civilization can issue human orders.");
    }
}
