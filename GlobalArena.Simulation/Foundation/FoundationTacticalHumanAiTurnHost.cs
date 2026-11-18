using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Typed pending tactical intent, private to its issuer until an all-ready
/// collective publication. A fine tile is not a coarse strategic ID.
/// </summary>
public sealed record FoundationTacticalWorkerOrder(
    CivilizationId Issuer, PhysicalWorkerId WorkerId,
    PhysicalTacticalTileId Source, PhysicalTacticalTileId Destination,
    PhysicalWorkerActivity Activity, bool IsMovement,
    PhysicalWorkerOccupation? NextOccupation = null);

/// <summary>
/// An explicit per-worker occupational choice in an atomic local batch.
/// These are requests; they are not a new physical census or market event.
/// </summary>
public sealed record FoundationTacticalOccupationChange(
    PhysicalWorkerId WorkerId, PhysicalWorkerOccupation Occupation);

public sealed record FoundationTacticalAiDecision(
    PhysicalWorkerId WorkerId, PhysicalTacticalTileId Destination,
    PhysicalWorkerActivity Activity, bool IsMovement);

public interface IFoundationTacticalAiTurnPolicy
{
    IReadOnlyList<FoundationTacticalAiDecision> Plan(
        CivilizationId civilization, WorldState snapshot,
        PhysicalTacticalIncidenceMap incidence, TurnNumber turn);
}

/// <summary>
/// An AI without a gameplay policy may simply hold. It generates no phantom
/// movement, extra worker or economic demand.
/// </summary>
public sealed class FoundationTacticalAiHoldPolicy : IFoundationTacticalAiTurnPolicy
{
    public IReadOnlyList<FoundationTacticalAiDecision> Plan(
        CivilizationId civilization, WorldState snapshot,
        PhysicalTacticalIncidenceMap incidence, TurnNumber turn) =>
        Array.Empty<FoundationTacticalAiDecision>();
}

/// <summary>
/// Minimal observable AI policy: existing productive capacity may resume
/// production at the very same *fine* tile after a travel phase.
/// </summary>
public sealed class FoundationTacticalAiResumeProductionPolicy
    : IFoundationTacticalAiTurnPolicy
{
    public IReadOnlyList<FoundationTacticalAiDecision> Plan(
        CivilizationId civilization, WorldState snapshot,
        PhysicalTacticalIncidenceMap incidence, TurnNumber turn)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(incidence);
        return Array.AsReadOnly(snapshot.Workers.Workers
            .Where(worker => worker.Owner == civilization &&
                worker.Activity == PhysicalWorkerActivity.Traveling &&
                worker.OutputMicroPerTurn > 0UL &&
                worker.TacticalTileId.HasValue)
            .OrderBy(worker => worker.Id.Value)
            .Select(worker => new FoundationTacticalAiDecision(
                worker.Id, worker.TacticalTileId!.Value,
                PhysicalWorkerActivity.Producing, IsMovement: false))
            .ToArray());
    }
}

/// <summary>One committed v10 collective turn, with canonical replay equality
/// and actual qualified fine edge receipts. Revisions count events.</summary>
public sealed class FoundationTacticalCollectiveReceipt
{
    public TurnNumber Turn { get; }
    public WorldStateHash Hash { get; }
    public ulong Revision { get; }
    public IReadOnlyList<PhysicalTacticalWorkerMovedEvent> Events { get; }
    public IReadOnlyList<PhysicalTransportConstructionWorkedEvent>
        ConstructionEvents { get; }
    public int ExecutedEvents => Events.Count + ConstructionEvents.Count;
    public bool ReplayMatched { get; }

    internal FoundationTacticalCollectiveReceipt(
        TurnNumber turn, WorldStateHash hash, ulong revision,
        PhysicalTacticalWorkerMovedEvent[] events,
        PhysicalTransportConstructionWorkedEvent[]? constructionEvents = null)
    {
        Turn = turn;
        Hash = hash;
        Revision = revision;
        Events = Array.AsReadOnly(events);
        ConstructionEvents = Array.AsReadOnly(constructionEvents ??
            Array.Empty<PhysicalTransportConstructionWorkedEvent>());
        ReplayMatched = true;
    }
}

/// <summary>
/// Opt-in HUMAN+AI host over the *real* v10 fine-worker authority.
/// A single private plan is built per civilization, AI is evaluated once
/// while its participant is unready, and nothing is published before
/// EVERY participant is ready. The v9 host and its hashes remain untouched.
///
/// This is neither a transport/economic solver nor network multiplayer.
/// It rejects state divergence, foreign worker orders, tactical teleports,
/// and duplicate worker orders before invoking the canonical commit gate.
/// </summary>
public sealed class FoundationTacticalHumanAiTurnHost
{
    private readonly CanonicalGameSession _session;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly PhysicalTacticalWorkerMoveEventExecutor _executor;
    private readonly PhysicalTransportConstructionEventExecutor?
        _constructionExecutor;
    private readonly FoundationPhysicalConstructionLaborPolicy?
        _constructionPolicy;
    private readonly AuthoritativeTurnCommitGate _publisher;
    private readonly CanonicalWorldStateHasher _hasher = new();
    private readonly HashSet<CivilizationId> _humans;
    private readonly IReadOnlyDictionary<CivilizationId,
        IFoundationTacticalAiTurnPolicy> _ai;
    private readonly CivilizationId[] _participants;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationTacticalWorkerOrder>> _plans;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationPhysicalConstructionWorkAssignment>>
        _constructionPlans;
    private readonly HashSet<CivilizationId> _ready = new();
    private WorldState _knownSnapshot;
    private readonly FoundationEconomicTradeCycleReadBoundary _closedTrade;

    public CanonicalGameSession Session => _session;
    public WorldState Snapshot => _session.Snapshot;
    public WorldStateHash StateHash => _hasher.Compute(Snapshot);
    public TurnNumber NextTurn => _publisher.NextTurn;
    public PhysicalTacticalIncidenceMap Incidence => _incidence;
    public IReadOnlyList<CivilizationId> HumanCivilizations =>
        Array.AsReadOnly(_participants.Where(_humans.Contains).ToArray());
    public IReadOnlyList<CivilizationId> AiCivilizations =>
        Array.AsReadOnly(_participants.Where(_ai.ContainsKey).ToArray());
    public IReadOnlyList<CivilizationId> ReadyCivilizations =>
        Array.AsReadOnly(_participants.Where(_ready.Contains).ToArray());
    public int PendingOrderCount => _plans.Values.Sum(plan => plan.Count) +
        _constructionPlans.Values.Sum(plan => plan.Count);
    /// <summary>
    /// Read-only v11 inputs for a future physical market, derived only from
    /// the last COMMITTED worker census. Pending orders cannot change them.
    /// No economic settlement or extra WorldState publication occurs here.
    /// </summary>
    public FoundationPhysicalWorkforceEconomicInputs GetPhysicalEconomicInputs() =>
        FoundationPhysicalWorkforceEconomicProjector.Project(Snapshot.Workers);

    /// <summary>
    /// Read-only activity-profile demand per commodity from the committed v11
    /// census and a COMPLETE caller-supplied policy. There is intentionally no
    /// default commodity list or demand preference inside the host.
    /// </summary>
    public FoundationPhysicalCommodityMarketInputs GetCommodityMarketInputs(
        FoundationActivityCommodityDemandPolicy policy) =>
        FoundationPhysicalCommodityDemandProjector.Project(Snapshot.Workers, policy);
    /// <summary>
    /// Read-only transport overlay for the last AUTHORITATIVELY settled
    /// economic cycle. The current worker-only host correctly returns
    /// NoClosedCycle, even after multiple E confirmations; it cannot
    /// turn movement receipts or experimental allocator results into trade.
    /// </summary>
    public FoundationLastClosedEconomicTradeView GetLastClosedEconomicTradeView(
        CivilizationId viewer)
    {
        CheckSnapshot();
        return _closedTrade.Read(viewer, Snapshot);
    }

    /// <summary>
    /// Trusted server-side candidate audit against ONLY the last committed
    /// physical worker census. It cannot publish trades, money, a closed
    /// economic cycle or a client-visible overlay. It is internal because
    /// every candidate may disclose trading data from other civilizations.
    /// </summary>
    internal FoundationPhysicalDeliveryAllocationAudit AuditDeliveryCandidates(
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationPhysicalCommodityDeliveryCandidate> candidates,
        IEnumerable<FoundationTransportEdgeOwnershipEvidence> ownership)
    {
        CheckSnapshot();
        return FoundationPhysicalDeliveryAllocationAuditor.Audit(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            candidates, ownership);
    }

    /// <summary>
    /// Trusted server-side route/market candidate planning against the
    /// committed census. External quotes and freight costs MUST be supplied.
    /// Never a client-visible price, real trade, or economic-cycle closure.
    /// </summary>
    internal FoundationPhysicalTradeRouteCandidatePlan PlanTradeCandidates(
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts)
    {
        CheckSnapshot();
        return FoundationPhysicalTradeRouteCandidatePlanner.Plan(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            quotes, edgeCosts);
    }

    /// <summary>
    /// Trusted read-only progressive multi-buyer planning, derived only
    /// from the confirmed v11 census. No financial settlement, inventory,
    /// price publication or last-closed-cycle artifact is produced here.
    /// </summary>
    internal FoundationPhysicalMarginalMarketDistributionPlan
        DistributeMarginalMarketCandidates(
            FoundationActivityCommodityDemandPolicy demandPolicy,
            FoundationCommodityCargoConversionPolicy conversionPolicy,
            IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
            IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts)
    {
        CheckSnapshot();
        return FoundationPhysicalMarginalMarketDistributor.Distribute(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            quotes, edgeCosts);
    }

    /// <summary>
    /// Trusted simulation-only global marginal CANDIDATE rebalancing across
    /// physical producers. Throws if the declared iteration budget cannot
    /// stabilize discrete seller best responses. Never writes a receipt,
    /// worker turn, treasury, trade ledger or last closed economic cycle.
    /// </summary>
    internal FoundationPhysicalGlobalMarginalCoordinationPlan
        CoordinateGlobalMarginalMarketCandidates(
            FoundationActivityCommodityDemandPolicy demandPolicy,
            FoundationCommodityCargoConversionPolicy conversionPolicy,
            IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
            IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts,
            int maxPasses,
            bool preferIncumbentThresholdTies = false,
            bool acceptOnlyStrictIntegratedGain = false)
    {
        CheckSnapshot();
        return FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            quotes, edgeCosts, maxPasses, preferIncumbentThresholdTies,
            acceptOnlyStrictIntegratedGain);
    }

    /// <summary>
    /// INTERNAL CANDIDATE ONLY: reprice physical freight from the actual
    /// aggregate cargo of repeated, fully audited candidate allocations.
    /// Neither worker turns nor economic/financial cycles are committed.
    /// </summary>
    internal FoundationPhysicalEndogenousCongestionPlan
        CoordinateCongestedMarketCandidates(
            FoundationActivityCommodityDemandPolicy demandPolicy,
            FoundationCommodityCargoConversionPolicy conversionPolicy,
            IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
            IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
            IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
            int maxProducerPasses,
            int maxCongestionIterations,
            FoundationPhysicalCongestionRelaxationPolicy? relaxation = null)
    {
        CheckSnapshot();
        return FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            quotes, freeFlowEdgeCosts, carryingScales,
            maxProducerPasses, maxCongestionIterations, relaxation);
    }

    /// <summary>
    /// Trusted candidate-only route splitting for the committed worker
    /// snapshot. A child route is a real audited crossing; neither this nor
    /// the price planner can publish a closed economic settlement.
    /// </summary>
    internal FoundationPhysicalMultiPathCandidatePlan PlanMultiPathTradeCandidates(
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int maxProducerPasses,
        int maxBatchesPerDelivery,
        bool preferIncumbentThresholdTies = false,
        bool acceptOnlyStrictIntegratedGain = false)
    {
        CheckSnapshot();
        return FoundationPhysicalMultiPathFreightPlanner.Plan(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            quotes, freeFlowEdgeCosts, carryingScales,
            maxProducerPasses, maxBatchesPerDelivery,
            preferIncumbentThresholdTies, acceptOnlyStrictIntegratedGain);
    }

    /// <summary>
    /// Internal-only coupled multi-market/multi-path cargo feedback for
    /// the last confirmed physical worker census. Candidate plans do not
    /// publish economic cycles, prices, toll collection or treasure events.
    /// </summary>
    internal FoundationPhysicalCoupledMultiPathFeedbackPlan
        CoordinateCoupledMultiPathMarketCandidates(
            FoundationActivityCommodityDemandPolicy demandPolicy,
            FoundationCommodityCargoConversionPolicy conversionPolicy,
            IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
            IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
            IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
            int maxProducerPasses,
            int maxBatchesPerDelivery,
            int maxFeedbackIterations,
            int maxMarginalPathPasses = 0,
            int maxFragmentsPerPathPart = 1,
            bool preferIncumbentThresholdTies = false,
            bool acceptOnlyStrictIntegratedGain = false)
    {
        CheckSnapshot();
        return FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            Snapshot.Workers, Incidence, demandPolicy, conversionPolicy,
            quotes, freeFlowEdgeCosts, carryingScales,
            maxProducerPasses, maxBatchesPerDelivery,
            maxFeedbackIterations, maxMarginalPathPasses,
            maxFragmentsPerPathPart, preferIncumbentThresholdTies,
            acceptOnlyStrictIntegratedGain);
    }

    public bool AllRequiredReady => _participants.All(_ready.Contains);

    public FoundationTacticalHumanAiTurnHost(
        CanonicalGameSession session, PhysicalTacticalIncidenceMap incidence,
        IEnumerable<CivilizationId> humanCivilizations,
        IReadOnlyDictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            aiPolicies,
        FoundationPhysicalConstructionLaborPolicy? constructionPolicy = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        ArgumentNullException.ThrowIfNull(humanCivilizations);
        ArgumentNullException.ThrowIfNull(aiPolicies);

        if (!Snapshot.Workers.IsInitialized ||
            !Snapshot.Workers.HasAuthoritativeTacticalLocations ||
            Snapshot.WorldBinding !=
                RuntimeWorldBinding.FromGeneratedWorld(session.GeneratedWorld) ||
            incidence.Refinement.CoarseParameters !=
                session.GeneratedWorld.Request.StrategicParameters ||
            Snapshot.Workers.Workers.Any(worker =>
                !worker.TacticalTileId.HasValue ||
                worker.TacticalTileId.Value.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters))
            throw new InvalidOperationException(
                "Tactical collective host requires authoritative v10/v11 workers " +
                "in the exact fine topology of the bound generated world.");

        _participants = Snapshot.Civilizations.Civilizations
            .OrderBy(civilization => civilization.Value).ToArray();
        if (_participants.Length == 0)
            throw new InvalidOperationException(
                "Collective turns require at least one civilization.");
        var humanArray = humanCivilizations.ToArray();
        if (humanArray.Any(civ => !civ.IsValid) ||
            humanArray.Distinct().Count() != humanArray.Length ||
            aiPolicies.Any(item => !item.Key.IsValid || item.Value is null))
            throw new ArgumentException("Invalid duplicate human or AI roles.");

        var roles = humanArray.Concat(aiPolicies.Keys).ToArray();
        if (roles.Length != _participants.Length ||
            roles.Distinct().Count() != roles.Length ||
            !_participants.ToHashSet().SetEquals(roles))
            throw new ArgumentException(
                "Every registered civilization needs exactly one human or AI role.");

        _humans = humanArray.ToHashSet();
        _ai = new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(
            aiPolicies);
        _plans = _participants.ToDictionary(civ => civ,
            _ => new SortedDictionary<ulong, FoundationTacticalWorkerOrder>());
        _constructionPlans = _participants.ToDictionary(civ => civ,
            _ => new SortedDictionary<ulong,
                FoundationPhysicalConstructionWorkAssignment>());
        if (constructionPolicy is not null &&
            (!Snapshot.Transport.HasNaturalConstructionLabor ||
             !Snapshot.Workers.HasAuthoritativeOccupations))
            throw new InvalidOperationException(
                "Construction-enabled collective turns require a complete " +
                "v13/v14 physical transport inventory and worker occupations.");
        _constructionPolicy = constructionPolicy;
        _knownSnapshot = Snapshot;
        _closedTrade = new FoundationEconomicTradeCycleReadBoundary(
            Snapshot.WorldBinding ?? throw new InvalidOperationException(
                "Tactical world lacks its world binding."),
            incidence.Refinement.FineParameters, _participants);
        _executor = new PhysicalTacticalWorkerMoveEventExecutor(
            session.GeneratedWorld, incidence);
        var phases = new List<ITurnResolutionPhase>
        {
            new PhysicalTacticalWorkerMovementTurnPhase(
                session.GeneratedWorld, incidence)
        };
        if (constructionPolicy is not null)
        {
            _constructionExecutor = new PhysicalTransportConstructionEventExecutor(
                session.GeneratedWorld, incidence, constructionPolicy);
            phases.Add(new PhysicalTransportConstructionTurnPhase(
                session.GeneratedWorld, incidence, constructionPolicy));
        }
        var engine = new PhasedTurnResolutionEngine(phases);
        _publisher = new AuthoritativeTurnCommitGate(
            session, new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), rulesetVersion: 2U);
    }

    public IReadOnlyList<FoundationTacticalWorkerOrder> GetHumanOrders(
        CivilizationId requester)
    {
        RequireHuman(requester);
        return Array.AsReadOnly(_plans[requester].Values.ToArray());
    }

    public IReadOnlyList<FoundationPhysicalConstructionWorkAssignment>
        GetHumanConstructionOrders(CivilizationId requester)
    {
        RequireHuman(requester);
        return Array.AsReadOnly(
            _constructionPlans[requester].Values.ToArray());
    }

    /// <summary>
    /// Private per-worker construction intent. No work is committed until
    /// ALL participants close the collective turn and journal replay agrees.
    /// Cannot steal an already queued move, occupation or activity command.
    /// </summary>
    public void QueueHumanConstruction(CivilizationId human,
        PhysicalWorkerId workerId, PhysicalTacticalEdgeId edge)
    {
        QueueHumanConstructionBatch(human,
            new[] { new FoundationPhysicalConstructionWorkAssignment(
                human, workerId, edge) });
    }

    /// <summary>
    /// Atomic bulk validation prevents accidental partial construction
    /// orders or duplicate worker assignment on large multi-worker projects.
    /// </summary>
    public int QueueHumanConstructionBatch(CivilizationId human,
        IReadOnlyList<FoundationPhysicalConstructionWorkAssignment> orders)
    {
        RequireHuman(human);
        ArgumentNullException.ThrowIfNull(orders);
        CheckWritable(human);
        if (_constructionPolicy is null)
            throw new InvalidOperationException(
                "This host has not enabled authoritative construction.");
        if (orders.Count == 0)
            throw new ArgumentException(
                "Construction batch must be nonempty.", nameof(orders));
        var staged = new SortedDictionary<ulong,
            FoundationPhysicalConstructionWorkAssignment>();
        foreach (var order in orders)
        {
            if (order is null || order.Issuer != human ||
                !order.WorkerId.IsValid ||
                _plans[human].ContainsKey(order.WorkerId.Value) ||
                _constructionPlans[human].ContainsKey(order.WorkerId.Value) ||
                !staged.TryAdd(order.WorkerId.Value, order))
                throw new InvalidOperationException(
                    "Construction cannot overlap another order or forge " +
                    "a worker's participant identity.");
        }

        var combined = _constructionPlans.Values
            .SelectMany(plan => plan.Values)
            .Concat(staged.Values)
            .ToArray();
        var preflight = FoundationPhysicalConstructionWorkPlanner.PlanCandidate(
            Snapshot, Session.GeneratedWorld, _incidence,
            _constructionPolicy, combined);
        if (preflight.Workers.Any(w => w.ContributedLaborMicro == 0UL))
            throw new InvalidOperationException(
                "A work assignment with zero usable labor cannot be " +
                "submitted as a real construction order.");

        foreach (var (id, order) in staged)
            _constructionPlans[human].Add(id, order);
        return staged.Count;
    }

    public void QueueHumanMove(CivilizationId human, PhysicalWorkerId id,
        PhysicalTacticalTileId destination)
    {
        RequireHuman(human);
        QueueMove(human, id, destination);
    }

    public void QueueHumanActivity(CivilizationId human, PhysicalWorkerId id,
        PhysicalWorkerActivity activity)
    {
        RequireHuman(human);
        QueueActivity(human, id, activity);
    }

    /// <summary>
    /// Queues a single human worker's productive reassignment at the SAME
    /// physical tile. Replaces its pending move/activity order for this
    /// collective turn; no color, worker state or market fact changes yet.
    /// </summary>
    public void QueueHumanOccupation(CivilizationId human,
        PhysicalWorkerId id, PhysicalWorkerOccupation occupation)
    {
        RequireHuman(human);
        QueueOccupation(human, id, occupation);
    }

    /// <summary>
    /// Validates the ENTIRE occupational batch against the same turn-start
    /// authoritative census before touching the participant's pending orders.
    /// Existing pending orders for mentioned workers are replaced only after
    /// validation succeeds; other workers' intentions remain unchanged.
    /// One physical person contributes at most one command to the turn.
    /// This is an application operation, not a second simulation phase.
    /// </summary>
    public int QueueHumanOccupationBatch(CivilizationId human,
        IReadOnlyList<FoundationTacticalOccupationChange> changes)
    {
        RequireHuman(human);
        ArgumentNullException.ThrowIfNull(changes);
        CheckWritable(human);
        if (changes.Count == 0)
            throw new ArgumentException("Occupation batch must contain at least one worker.",
                nameof(changes));

        // Build an index once: unlike validating N singles using a linear
        // worker lookup each, this keeps large workforces O(workers + K log K).
        var owned = Snapshot.Workers.Workers
            .Where(worker => worker.Owner == human)
            .ToDictionary(worker => worker.Id.Value);
        var staged = new SortedDictionary<ulong, FoundationTacticalWorkerOrder>();
        foreach (var change in changes)
        {
            if (change is null || !change.WorkerId.IsValid ||
                !owned.TryGetValue(change.WorkerId.Value, out var worker))
                throw new InvalidOperationException(
                    "Batch contains a missing, invalid or non-owned physical worker.");
            if (staged.ContainsKey(change.WorkerId.Value) ||
                _constructionPlans[human].ContainsKey(change.WorkerId.Value))
                throw new InvalidOperationException(
                    "A physical worker cannot be assigned to both " +
                    "occupation and construction in the same turn.");
            ValidateOccupationTarget(worker, change.Occupation);
            var source = worker.TacticalTileId ??
                throw new InvalidOperationException("Worker has no fine position.");
            staged.Add(change.WorkerId.Value, new FoundationTacticalWorkerOrder(
                human, worker.Id, source, source, worker.Activity,
                IsMovement: false, NextOccupation: change.Occupation));
        }

        // No mutations, including pre-existing queued moves, occur above.
        foreach (var (workerId, order) in staged)
            _plans[human][workerId] = order;
        return staged.Count;
    }

    public bool CancelHumanOrder(CivilizationId human, PhysicalWorkerId id)
    {
        RequireHuman(human);
        CheckWritable(human);
        if (!id.IsValid) return false;
        bool movement = _plans[human].Remove(id.Value);
        bool construction = _constructionPlans[human].Remove(id.Value);
        return movement || construction;
    }

    public void SetHumanReady(CivilizationId human)
    {
        RequireHuman(human);
        CheckWritable(human);
        _ready.Add(human);
    }

    public void ReopenHuman(CivilizationId human)
    {
        RequireHuman(human);
        CheckSnapshot();
        _ready.Remove(human);
    }

    /// <summary>AI policies are invoked at most once per ready turn. Validate
    /// the entire plan before queuing anything and rollback staged proposals
    /// if a later queue operation rejects them.</summary>
    public void PrepareAiCivilizations()
    {
        CheckSnapshot();
        foreach (var civ in _participants.Where(_ai.ContainsKey))
        {
            if (_ready.Contains(civ)) continue;
            var before = Snapshot;
            var proposed = _ai[civ].Plan(
                civ, before, _incidence, NextTurn)
                ?? throw new InvalidOperationException("AI returned a null plan.");
            if (!ReferenceEquals(before, Snapshot))
                throw new InvalidOperationException(
                    "AI mutated canonical WorldState while planning.");
            var decisions = proposed.ToArray();
            var seen = new HashSet<PhysicalWorkerId>();
            foreach (var decision in decisions)
            {
                if (decision is null || !decision.WorkerId.IsValid ||
                    !decision.Destination.IsValid ||
                    !Enum.IsDefined(decision.Activity) ||
                    !seen.Add(decision.WorkerId))
                    throw new InvalidOperationException(
                        "AI supplied an invalid or duplicate fine worker order.");
                ValidateOrder(civ, decision.WorkerId,
                    decision.Destination, decision.Activity, decision.IsMovement);
            }

            var queued = new List<ulong>();
            try
            {
                foreach (var decision in decisions)
                {
                    if (decision.IsMovement)
                        QueueMove(civ, decision.WorkerId, decision.Destination);
                    else
                        QueueActivity(civ, decision.WorkerId, decision.Activity);
                    queued.Add(decision.WorkerId.Value);
                }
                _ready.Add(civ);
            }
            catch
            {
                foreach (var id in queued)
                    _plans[civ].Remove(id);
                throw;
            }
        }
    }

    public FoundationTacticalCollectiveReceipt? TryCloseTurn()
    {
        CheckSnapshot();
        PrepareAiCivilizations();
        return AllRequiredReady ? CloseReadyTurn() : null;
    }

    private FoundationTacticalCollectiveReceipt CloseReadyTurn()
    {
        CheckSnapshot();
        if (!AllRequiredReady)
            throw new InvalidOperationException(
                "Cannot publish before all civilizations are ready.");

        var before = Snapshot;
        var turn = NextTurn;
        var orders = _plans.Values.SelectMany(plan => plan.Values)
            .OrderBy(order => order.WorkerId.Value).ToArray();
        if (orders.Select(order => order.WorkerId).Distinct().Count() != orders.Length)
            throw new InvalidOperationException("Duplicate inter-civilization worker order.");

        // A stale plan or forged origin must never be committed. Orders are
        // revalidated against the same immutable turn-start snapshot.
        foreach (var order in orders)
        {
            var worker = ValidateOrder(
                order.Issuer, order.WorkerId, order.Destination,
                order.Activity, order.IsMovement, order.NextOccupation);
            if (worker.TacticalTileId != order.Source)
                throw new InvalidOperationException(
                    "Pending tactical source no longer matches canonical worker.");
        }

        var commands = orders.Select((order, index) =>
            (ISimulationCommand)new PhysicalTacticalWorkerMoveCommand(
                new CommandId(turn, checked((ulong)index + 1UL)),
                order.Issuer, order.WorkerId, order.Destination,
                order.Activity, order.NextOccupation)).ToArray();
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var committed = _publisher.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(turn, allRequiredParticipantsReady: true,
                    externalDeadlineReached: false),
                new TurnResolutionInput(before, commands, context)));
        if (!committed.WasResolved ||
            committed.ResolutionResult is not { } resolved)
            throw new InvalidOperationException(
                "Canonical tactical turn gate failed to commit.");
        var events = resolved.Events.Select(ev =>
            ev as PhysicalTacticalWorkerMovedEvent ??
                throw new InvalidOperationException(
                    "Unexpected event in v10 tactical journal."))
            .ToArray();
        if (events.Length != commands.Length ||
            resolved.EventLog.Entries.Count != events.Length ||
            resolved.EventLog.Entries.Any(entry => !entry.WasExecuted))
            throw new InvalidOperationException(
                "v10 committed event log differs from submitted worker orders.");

        var replay = new SimulationEventLogReplayer(_executor)
            .Replay(new SimulationReplayInput(
                before, resolved.EventLog, context));
        var hash = _hasher.Compute(Snapshot);
        if (hash.FormatVersion != _hasher.Compute(before).FormatVersion ||
            (hash.FormatVersion != 10U && hash.FormatVersion != 11U) ||
            _hasher.Compute(replay) != hash)
            throw new InvalidOperationException(
                "Tactical collective journal replay does not match v10/v11 authority.");
        var receipt = new FoundationTacticalCollectiveReceipt(
            turn, hash, Snapshot.Revision, events);
        foreach (var plan in _plans.Values) plan.Clear();
        _ready.Clear();
        _knownSnapshot = Snapshot;
        return receipt;
    }

    private void QueueMove(CivilizationId issuer, PhysicalWorkerId id,
        PhysicalTacticalTileId destination)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, id);
        if (_constructionPlans[issuer].ContainsKey(id.Value))
            throw new InvalidOperationException(
                "Cancel construction before issuing a move for this worker.");
        ValidateOrder(issuer, id, destination,
            PhysicalWorkerActivity.Traveling, movement: true);
        _plans[issuer][id.Value] = new FoundationTacticalWorkerOrder(
            issuer, id, worker.TacticalTileId!.Value, destination,
            PhysicalWorkerActivity.Traveling, IsMovement: true);
    }

    private void QueueActivity(CivilizationId issuer, PhysicalWorkerId id,
        PhysicalWorkerActivity activity)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, id);
        if (_constructionPlans[issuer].ContainsKey(id.Value))
            throw new InvalidOperationException(
                "Cancel construction before changing this worker's activity.");
        var source = worker.TacticalTileId!.Value;
        ValidateOrder(issuer, id, source, activity, movement: false);
        _plans[issuer][id.Value] = new FoundationTacticalWorkerOrder(
            issuer, id, source, source, activity, IsMovement: false);
    }

    private void QueueOccupation(CivilizationId issuer,
        PhysicalWorkerId id, PhysicalWorkerOccupation occupation)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, id);
        if (_constructionPlans[issuer].ContainsKey(id.Value))
            throw new InvalidOperationException(
                "Cancel construction before changing this worker's occupation.");
        var tile = worker.TacticalTileId!.Value;
        ValidateOrder(issuer, id, tile, worker.Activity,
            movement: false, nextOccupation: occupation);
        _plans[issuer][id.Value] = new FoundationTacticalWorkerOrder(
            issuer, id, tile, tile, worker.Activity,
            IsMovement: false, NextOccupation: occupation);
    }

    private void ValidateOccupationTarget(
        PhysicalWorkerRuntimeRecord worker, PhysicalWorkerOccupation occupation)
    {
        if (!Snapshot.Workers.HasAuthoritativeOccupations ||
            !worker.Occupation.HasValue ||
            (occupation != PhysicalWorkerOccupation.Agriculture &&
             occupation != PhysicalWorkerOccupation.Forestry &&
             occupation != PhysicalWorkerOccupation.Mining) ||
            worker.Occupation.Value == occupation)
            throw new InvalidOperationException(
                "Reallocation requires an existing owned v11 worker and " +
                "a different productive occupation at the same fine tile.");
    }

    private PhysicalWorkerRuntimeRecord ValidateOrder(
        CivilizationId issuer, PhysicalWorkerId workerId,
        PhysicalTacticalTileId destination, PhysicalWorkerActivity activity,
        bool movement, PhysicalWorkerOccupation? nextOccupation = null)
    {
        var worker = RequireOwnedWorker(issuer, workerId);
        if (!destination.IsValid || !Enum.IsDefined(activity))
            throw new InvalidOperationException("Invalid fine destination or worker activity.");
        var source = worker.TacticalTileId ??
            throw new InvalidOperationException("Worker has no fine position.");
        PhysicalTacticalEdgeId? edge;
        try { edge = _executor.CheckEdge(source, destination); }
        catch (ArgumentException ex)
        { throw new InvalidOperationException("Foreign fine topology.", ex); }
        if (nextOccupation.HasValue)
        {
            ValidateOccupationTarget(worker, nextOccupation.Value);
            if (movement || edge.HasValue || activity != worker.Activity)
                throw new InvalidOperationException(
                    "Reallocation requires an existing owned v11 worker and " +
                    "a different productive occupation at the same fine tile.");
            return worker;
        }
        if (movement)
        {
            if (edge is null || activity != PhysicalWorkerActivity.Traveling)
                throw new InvalidOperationException(
                    "Tactical move must cross one real fine edge and enter Traveling.");
        }
        else if (edge.HasValue || activity == worker.Activity ||
                 (activity == PhysicalWorkerActivity.Producing &&
                    worker.OutputMicroPerTurn == 0UL))
            throw new InvalidOperationException("Invalid in-place worker activity change.");
        return worker;
    }

    private PhysicalWorkerRuntimeRecord RequireOwnedWorker(
        CivilizationId issuer, PhysicalWorkerId workerId)
    {
        if (!workerId.IsValid)
            throw new InvalidOperationException("Invalid worker identity.");
        var worker = Snapshot.Workers.Workers.FirstOrDefault(w => w.Id == workerId);
        if (worker is null || worker.Owner != issuer)
            throw new InvalidOperationException(
                "Civilization cannot order a non-owned worker.");
        return worker;
    }

    private void RequireHuman(CivilizationId requester)
    {
        if (!requester.IsValid || !_humans.Contains(requester))
            throw new InvalidOperationException(
                "Only the explicit human player can issue manual worker orders.");
    }

    private void CheckWritable(CivilizationId issuer)
    {
        CheckSnapshot();
        if (!_plans.ContainsKey(issuer) || _ready.Contains(issuer))
            throw new InvalidOperationException(
                "Unrecognized or already-ready civilization may not change plans.");
    }

    private void CheckSnapshot()
    {
        if (!ReferenceEquals(_knownSnapshot, Snapshot))
            throw new InvalidOperationException(
                "A foreign authority changed WorldState during the collective plan.");
    }
}
