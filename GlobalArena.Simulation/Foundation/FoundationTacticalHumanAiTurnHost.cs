using System.Numerics;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
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
                !snapshot.EdgeTravels.Pending.Any(p => p.Worker == worker.Id) &&
                worker.Occupation is not PhysicalWorkerOccupation.LandTransport and
                    not PhysicalWorkerOccupation.PortOperations &&
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
    public IReadOnlyList<FoundationMonetaryInfrastructureInvestedEvent>
        InfrastructureInvestments { get; }
    public FoundationEconomicSalesSettledEvent? EconomicSettlement { get; }
    public IReadOnlyList<FoundationEdgeTravelTurnEvent> EdgeTravels { get; }
    public FoundationTemporalSalesSettledEvent? TemporalEconomicSettlement { get; }
    public int ExecutedEvents => (BirthBatch is null ? 0 : 1) + PersistentEdits.Count + PersistentFunding.Count + (PersistentTimeline is null ? 0 : 1) + Events.Count + EdgeTravels.Count + InfrastructureInvestments.Count + ConstructionFunding.Count + ConstructionWork.Count +
        (EconomicSettlement is null ? 0 : 1) +
        (TemporalEconomicSettlement is null ? 0 : 1);
    public IReadOnlyList<FoundationConstructionFundedEvent> ConstructionFunding { get; }
    public IReadOnlyList<FoundationConstructionWorkBatchEvent> ConstructionWork { get; }
    public bool ReplayMatched { get; }
    public IReadOnlyList<FoundationPersistentOrderEditEvent> PersistentEdits { get; }
    public IReadOnlyList<FoundationFundedWorkerIntentEvent> PersistentFunding { get; }
    public FoundationPersistentWorkerTurnEvent? PersistentTimeline { get; }
    public FoundationPhysicalBirthBatchEvent? BirthBatch { get; }

    internal FoundationTacticalCollectiveReceipt(
        TurnNumber turn, WorldStateHash hash, ulong revision,
        PhysicalTacticalWorkerMovedEvent[] events,
        FoundationMonetaryInfrastructureInvestedEvent[]? investments = null,
        FoundationEconomicSalesSettledEvent? economicSettlement = null,
        FoundationEdgeTravelTurnEvent[]? edgeTravels = null,
        FoundationTemporalSalesSettledEvent? temporalSettlement = null,
        FoundationConstructionFundedEvent[]? constructionFunding = null,
        FoundationConstructionWorkBatchEvent[]? constructionWork = null,
        FoundationPersistentOrderEditEvent[]? persistentEdits = null,
        FoundationFundedWorkerIntentEvent[]? persistentFunding = null,
        FoundationPersistentWorkerTurnEvent? persistentTimeline = null,
        FoundationPhysicalBirthBatchEvent? birthBatch = null)
    {
        Turn = turn;
        Hash = hash;
        Revision = revision;
        Events = Array.AsReadOnly(events);
        InfrastructureInvestments = Array.AsReadOnly(investments ??
            Array.Empty<FoundationMonetaryInfrastructureInvestedEvent>());
        EconomicSettlement = economicSettlement;
        TemporalEconomicSettlement = temporalSettlement;
        EdgeTravels = Array.AsReadOnly(edgeTravels ?? Array.Empty<FoundationEdgeTravelTurnEvent>());
        ConstructionFunding = Array.AsReadOnly(constructionFunding ?? Array.Empty<FoundationConstructionFundedEvent>());
        ConstructionWork = Array.AsReadOnly(constructionWork ?? Array.Empty<FoundationConstructionWorkBatchEvent>());
        PersistentEdits = Array.AsReadOnly(persistentEdits ?? Array.Empty<FoundationPersistentOrderEditEvent>());
        PersistentFunding = Array.AsReadOnly(persistentFunding ?? Array.Empty<FoundationFundedWorkerIntentEvent>());
        PersistentTimeline = persistentTimeline;
        BirthBatch = birthBatch;
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
    private readonly FoundationMonetaryInfrastructureEventExecutor?
        _monetaryExecutor;
    private readonly FoundationEconomicSalesSettlementEventExecutor?
        _economicExecutor;
    private readonly FoundationEdgeTravelEventExecutor? _travelExecutor;
    private readonly FoundationGroundEdgeTravelTimePolicy? _travelPolicy;
    private readonly FoundationAuthoritativeEconomicSettlementPolicy? _temporalPolicy;
    private readonly FoundationFineFirstPhysicalGeography? _signedFine;
    private readonly FoundationConstructionCollectiveTurnPhase? _constructionPhase;
    private readonly Dictionary<PhysicalWorkerId, (CivilizationId Owner, ulong Project)> _constructionPlans = new();
    private readonly AuthoritativeTurnCommitGate _publisher;
    private readonly FoundationTemporalCollectiveTurnPhase? _persistentPhase;
    private readonly FoundationFundedWorkerIntentJournal? _persistentFunding;
    private readonly Dictionary<CivilizationId, List<ISimulationCommand>> _persistentPlans = new();
    public bool PersistentOrdersEnabled => _persistentPhase is not null;
    public bool ExperimentalLocalEconomyEnabled { get; }
    public bool DefaultAutomaticBaseEnabled { get; }
    private readonly CanonicalWorldStateHasher _hasher = new();
    private readonly HashSet<CivilizationId> _humans;
    private readonly IReadOnlyDictionary<CivilizationId,
        IFoundationTacticalAiTurnPolicy> _ai;
    private readonly CivilizationId[] _participants;
    private readonly IReadOnlyDictionary<CivilizationId, FoundationCivilizationKnowledgeScope>? _knowledgeScopes;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationTacticalWorkerOrder>> _plans;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationMonetaryInfrastructureCommand>>
        _investmentPlans;
    private readonly HashSet<CivilizationId> _ready = new();
    private WorldState _knownSnapshot;
    private readonly FoundationEconomicTradeCycleReadBoundary _closedTrade;
    private FoundationTacticalCollectiveReceipt? _lastTemporalClose;

    /// <summary>Own local economic evidence from the last committed temporal close.
    /// Current own physical presence is required. No foreign worker or seller rows,
    /// no previous-price fallback and no hypothetical market evaluation.</summary>
    public FoundationViewerLocalEconomicClose? GetLastClosedLocalEconomics(CivilizationId viewer, PhysicalTacticalTileId node)
    {
        CheckSnapshot();
        RequireHuman(viewer);
        if (!Snapshot.Workers.Workers.Any(w => w.Owner == viewer && w.TacticalTileId == node))
            throw new InvalidOperationException("Local economics require own current physical presence.");
        return ReadCommittedOwnNodeEconomics(viewer, node);
    }

    // Callers must establish current own presence before this shared read.
    // AI uses a distinct set of its own committed nodes; public access still
    // requires a human participant and the original presence check.
    private FoundationViewerLocalEconomicClose? ReadCommittedOwnNodeEconomics(
        CivilizationId viewer, PhysicalTacticalTileId node)
    {
        var receipt = _lastTemporalClose;
        var sale = receipt?.TemporalEconomicSettlement;
        if (receipt is null || sale?.AuditedLocalEconomics is not { } evidence ||
            !receipt.ReplayMatched || receipt.Revision != Snapshot.Revision ||
            sale.EconomicCycleOrdinal != Snapshot.Treasury.LastSettledEconomicCycleOrdinal)
            return null;
        var rows = evidence.Read(viewer, node);
        return rows is null ? null : new(viewer, node, evidence.CycleOrdinal, receipt.Hash, rows);
    }

    // Internal diagnostic/authorization foundation only. Do not expose
    // foreign workforce totals to player or AI policy views.
    internal FoundationClosedNodeWorkEvidence? ReadLastClosedNodeWork(
        PhysicalTacticalTileId node, CivilizationId requester,
        IReadOnlySet<CivilizationId> eligibleOwners)
    {
        CheckSnapshot();
        var receipt = _lastTemporalClose;
        if (receipt is null || receipt.PersistentTimeline is not { } timeline ||
            receipt.TemporalEconomicSettlement is not { } sale ||
            !receipt.ReplayMatched || receipt.Revision != Snapshot.Revision ||
            sale.EconomicCycleOrdinal != Snapshot.Treasury.LastSettledEconomicCycleOrdinal)
            return null;
        if (receipt.Turn.Value != NextTurn.Value - 1UL)
            return null;
        // Current persistent model retains worker identities and ownership;
        // births after the calendar add no slices. Future conquest/death
        // requires preserving historical owner evidence at calendar close.
        var owners = Snapshot.Workers.Workers.ToDictionary(w => w.Id, w => w.Owner);
        return FoundationClosedNodeWorkProjector.Read(timeline.Slices, owners,
            node, requester, eligibleOwners);
    }

    // Trusted read-side gate for the next naval executor. A successful
    // decision is not yet authority to move a particular unit.
    internal FoundationNavalBoardingDecision ReadNavalBoarding(
        CivilizationId requester, PhysicalTacticalTileId origin, PhysicalTacticalTileId water)
    {
        CheckSnapshot();
        if (!_participants.Contains(requester))
            throw new InvalidOperationException("Unknown boarding participant.");
        if (_signedFine is null || _persistentPhase is null)
            throw new InvalidOperationException("Boarding requires signed persistent scheduling.");
        if (_knowledgeScopes is not null && !_knowledgeScopes[requester].KnowsLink(origin, water))
            return new(FoundationNavalBoardingStatus.UnknownConnection);
        return FoundationNavalBoardingPolicy.Evaluate(Snapshot, Incidence, _signedFine,
            requester, origin, water, eligible => ReadLastClosedNodeWork(origin, requester, eligible));
    }

    // Only the human owner's last independently verified cycle; no pending-plan data.
    public IReadOnlyList<FoundationWorkerFoodIntake>? ReadWorkerFoodIntake(
        CivilizationId viewer, PhysicalWorkerId worker)
    {
        CheckSnapshot();
        RequireHuman(viewer);
        _ = RequireOwnedWorker(viewer, worker);
        var sale = _lastTemporalClose?.TemporalEconomicSettlement;
        if (sale?.FoodIntake is null ||
            sale.EconomicCycleOrdinal != Snapshot.Treasury.LastSettledEconomicCycleOrdinal)
            return null;
        var rows = sale.FoodIntake.Where(r => r.Worker == worker).ToArray();
        return rows.Length == 0 ? null : Array.AsReadOnly(rows);
    }

    public IReadOnlyList<FoundationOperationalFreightShadowEdge>? ReadOperationalQuotes(CivilizationId viewer, PhysicalTacticalTileId node)
    {
        CheckSnapshot();
        RequireHuman(viewer);
        if (!Snapshot.Workers.Workers.Any(w => w.Owner == viewer && w.TacticalTileId == node))
            throw new InvalidOperationException("Operational readout requires own local presence.");
        var sale = _lastTemporalClose?.TemporalEconomicSettlement;
        if (sale?.OperationalQuotes is null || sale.EconomicCycleOrdinal != Snapshot.Treasury.LastSettledEconomicCycleOrdinal)
            return null;
        var incident = Incidence.FineTopology.Cells[(int)node.FineStrategicCellId.Value - 1].AdjacentCellIds.ToHashSet();
        var edges = Incidence.FineTopology.Edges.Where(e => e.IncidentCellIds.Contains(node.FineStrategicCellId) &&
            e.IncidentCellIds.Any(incident.Contains)).Select(e => new PhysicalTacticalEdgeId(Incidence.Refinement.FineParameters, e.Id)).ToHashSet();
        return Array.AsReadOnly(sale.OperationalQuotes.Where(q => edges.Contains(q.Edge)).ToArray());
    }

    public CanonicalGameSession Session => _session;
    public WorldState Snapshot => _session.Snapshot;
    public WorldStateHash StateHash => _hasher.Compute(Snapshot);
    public TurnNumber NextTurn => _publisher.NextTurn;
    public PhysicalTacticalIncidenceMap Incidence => _incidence;
    public IReadOnlyList<CivilizationId> HumanCivilizations =>
        Array.AsReadOnly(_participants.Where(_humans.Contains).ToArray());
    public IReadOnlyList<CivilizationId> AiCivilizations =>
        Array.AsReadOnly(_participants.Where(_ai.ContainsKey).ToArray());
    // Trusted bootstrap only: preserve AI behavior byte-for-byte when
    // enabling a declared economic policy before the first turn.
    internal IReadOnlyDictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
        SnapshotAiPoliciesForBootstrap() =>
        new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(_ai);
    public IReadOnlyList<CivilizationId> ReadyCivilizations =>
        Array.AsReadOnly(_participants.Where(_ready.Contains).ToArray());
    public int PendingOrderCount => _persistentPlans.Values.Sum(plan => plan.Count) + _plans.Values.Sum(plan => plan.Count) +
        _investmentPlans.Values.Sum(plan => plan.Count);
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
    /// R171: returns ONLY the requesting human civilization's monetary
    /// credit from the last independently replayed temporal economic close.
    /// Pending turns and AI participants are never exposed. No market
    /// calculation, world mutation or alternate economic ledger.
    /// Returns null when no temporal cycle has yet been committed.
    /// </summary>
    public FoundationViewerTemporalEconomicClose? GetLastClosedTemporalEconomics(
        CivilizationId viewer)
    {
        CheckSnapshot();
        RequireHuman(viewer);
        if (_lastTemporalClose is null)
            return null;
        return FoundationViewerTemporalEconomicCloseProjector.Read(
            _lastTemporalClose, Snapshot, viewer);
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

    public FoundationAgriculturalProductionPolicy? AgriculturalProduction { get; }
    public FoundationOperationalFreightShadowPolicy? OperationalFreight { get; }

    public bool AllRequiredReady => _participants.All(_ready.Contains);

    public FoundationTacticalHumanAiTurnHost(
        CanonicalGameSession session, PhysicalTacticalIncidenceMap incidence,
        IEnumerable<CivilizationId> humanCivilizations,
        IReadOnlyDictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            aiPolicies,
        bool enableMonetaryInfrastructure = false,
        FoundationAuthoritativeEconomicSettlementPolicy?
            settlementPolicy = null,
        FoundationFineFirstPhysicalGeography? fineGeography = null,
        FoundationFineFirstWorldgenIdentity? fineSignature = null,
        FoundationGroundEdgeTravelTimePolicy? physicalTravelPolicy = null,
        ulong? constructionMoneyMicroPerWholeWork = null, bool enableDemographicGrowth = false, FoundationAgriculturalProductionPolicy? agriculture = null, FoundationOperationalFreightShadowPolicy? operationalPolicy = null,
        bool enableExperimentalLocalEconomy = false, bool enableDefaultAutomaticBase = false,
        IReadOnlyDictionary<CivilizationId, FoundationCivilizationKnowledgeScope>? knowledgeScopes = null, bool enableExclusiveNodeLabor = false)
    {
        if (enableExclusiveNodeLabor && (!enableDefaultAutomaticBase || !session.Snapshot.WorkerOrders.IsInitialized))
            throw new InvalidOperationException("Exclusive node labor requires persistent default automatic base.");
        AgriculturalProduction = agriculture;
        OperationalFreight = operationalPolicy;
        if (operationalPolicy is not null && (!session.Snapshot.WorkerOrders.IsInitialized || fineGeography is null)) throw new InvalidOperationException("Operational freight requires signed persistent scheduling.");
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        ArgumentNullException.ThrowIfNull(humanCivilizations);
        ArgumentNullException.ThrowIfNull(aiPolicies);

        if ((fineGeography is null) != (fineSignature is null))
            throw new InvalidOperationException(
                "G18 host needs its complete signed geography, never an " +
                "unverified fine-world identity.");
        var expectedBinding = fineGeography is null
            ? RuntimeWorldBinding.FromGeneratedWorld(session.GeneratedWorld)
            : RuntimeWorldBinding.FromFineFirst(
                session.GeneratedWorld, incidence, fineGeography,
                fineSignature!);
        if (!Snapshot.Workers.IsInitialized ||
            !Snapshot.Workers.HasAuthoritativeTacticalLocations ||
            Snapshot.WorldBinding != expectedBinding ||
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

        // Null retains the historical fully revealed QA profile. When a
        // restricted profile is supplied, missing participants fail closed.
        if (knowledgeScopes is not null)
        {
            if (!_participants.ToHashSet().SetEquals(knowledgeScopes.Keys) ||
                knowledgeScopes.Values.Any(scope => scope is null))
                throw new ArgumentException("Restricted geography requires a scope for every participant.", nameof(knowledgeScopes));
            _knowledgeScopes = new Dictionary<CivilizationId, FoundationCivilizationKnowledgeScope>(knowledgeScopes);
        }
        _humans = humanArray.ToHashSet();
        _ai = new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(
            aiPolicies);
        _plans = _participants.ToDictionary(civ => civ,
            _ => new SortedDictionary<ulong, FoundationTacticalWorkerOrder>());
        _investmentPlans = _participants.ToDictionary(civ => civ,
            _ => new SortedDictionary<ulong, FoundationMonetaryInfrastructureCommand>());
        // Opt-in temporal mode uses a single R170 phase, never the
        // legacy census-based seller settlement.
        _temporalPolicy = settlementPolicy is not null &&
            physicalTravelPolicy is not null ? settlementPolicy : null;
        _signedFine = fineGeography;
        bool persistent = Snapshot.WorkerOrders.IsInitialized;
        if (enableExperimentalLocalEconomy != (settlementPolicy?.UseTemporalProgressiveDistribution == true) ||
            (enableExperimentalLocalEconomy && (!persistent || _temporalPolicy is null || agriculture is null || fineGeography is null)))
            throw new InvalidOperationException("Experimental local economy requires explicit progressive temporal policy and signed persistent crops.");
        ExperimentalLocalEconomyEnabled = enableExperimentalLocalEconomy;
        if (enableDefaultAutomaticBase && (!enableExperimentalLocalEconomy || agriculture?.BasicResources is null))
            throw new InvalidOperationException("Default automatic base requires explicit experimental local economy and basic resources.");
        DefaultAutomaticBaseEnabled = enableDefaultAutomaticBase;
        if (persistent && (_temporalPolicy is null || fineGeography is null || !constructionMoneyMicroPerWholeWork.HasValue || !enableMonetaryInfrastructure))
            throw new InvalidOperationException("Persistent host needs complete signed temporal construction world.");
        foreach (var civ in _participants) _persistentPlans[civ] = new();
        if (Snapshot.Constructions.IsInitialized != constructionMoneyMicroPerWholeWork.HasValue ||
            (constructionMoneyMicroPerWholeWork.HasValue && (!enableMonetaryInfrastructure || fineGeography is null || (settlementPolicy is not null && physicalTravelPolicy is null))))
            throw new InvalidOperationException("Construction host requires signed geography, explicit rate and temporal travel for economic settlement.");
        if (settlementPolicy is not null && !enableMonetaryInfrastructure)
            throw new InvalidOperationException(
                "Authoritative sales need the same monetary treasury and " +
                "physical world as the existing investment journal.");
        if (enableMonetaryInfrastructure &&
            (!Snapshot.Treasury.IsInitialized ||
             !Snapshot.Transport.HasMonetaryConstructionCosts))
            throw new InvalidOperationException(
                "Monetary infrastructure orders require a complete " +
                "real treasury and v13 capital construction costs.");
        if (Snapshot.EdgeTravels.IsInitialized != (physicalTravelPolicy is not null))
            throw new InvalidOperationException(
                "Opt-in persisted physical travel requires an explicit natural-time policy.");
        physicalTravelPolicy?.RequireValid();
        _travelPolicy = physicalTravelPolicy;
        _knownSnapshot = Snapshot;
        _closedTrade = new FoundationEconomicTradeCycleReadBoundary(
            Snapshot.WorldBinding ?? throw new InvalidOperationException(
                "Tactical world lacks its world binding."),
            incidence.Refinement.FineParameters, _participants);
        _executor = fineGeography is null
            ? new PhysicalTacticalWorkerMoveEventExecutor(
                session.GeneratedWorld, incidence)
            : new PhysicalTacticalWorkerMoveEventExecutor(
                session.GeneratedWorld, incidence, fineGeography,
                fineSignature!);
        var phases = new List<ITurnResolutionPhase>
        {
            fineGeography is null
                ? new PhysicalTacticalWorkerMovementTurnPhase(
                    session.GeneratedWorld, incidence)
                : new PhysicalTacticalWorkerMovementTurnPhase(
                    session.GeneratedWorld, incidence, fineGeography,
                    fineSignature!)
        };
        if (_travelPolicy is not null)
        {
            _travelExecutor = new FoundationEdgeTravelEventExecutor(_executor, _travelPolicy);
            phases.Add(new FoundationEdgeTravelTurnPhase(_executor, _travelPolicy));
        }
        if (enableMonetaryInfrastructure)
        {
            _monetaryExecutor = fineGeography is null
                ? new FoundationMonetaryInfrastructureEventExecutor(
                    session.GeneratedWorld, incidence)
                : new FoundationMonetaryInfrastructureEventExecutor(
                    session.GeneratedWorld, incidence, fineGeography,
                    fineSignature!);
            if (constructionMoneyMicroPerWholeWork is { } rate && !persistent)
            {
                _constructionPhase = new FoundationConstructionCollectiveTurnPhase(
                    new FoundationConstructionFundingExecutor(_monetaryExecutor, rate), incidence, fineGeography!, _temporalPolicy is not null);
                phases.Add(_constructionPhase);
            }
            else phases.Add(fineGeography is null
                ? new FoundationMonetaryInfrastructureTurnPhase(
                    session.GeneratedWorld, incidence)
                : new FoundationMonetaryInfrastructureTurnPhase(
                    session.GeneratedWorld, incidence, fineGeography,
                    fineSignature!));
        }
        if (settlementPolicy is not null && _temporalPolicy is null)
        {
            _economicExecutor = fineGeography is null
                ? new FoundationEconomicSalesSettlementEventExecutor(
                    session.GeneratedWorld, incidence, settlementPolicy)
                : new FoundationEconomicSalesSettlementEventExecutor(
                    session.GeneratedWorld, incidence, settlementPolicy,
                    fineGeography, fineSignature!);
            // Money available for THIS turn's investment must already exist:
            // the current sales close AFTER its orders and funds NEXT turn.
            phases.Add(fineGeography is null
                ? new FoundationAuthoritativeEconomicSalesSettlementPhase(
                    session.GeneratedWorld, incidence, settlementPolicy)
                : new FoundationAuthoritativeEconomicSalesSettlementPhase(
                    session.GeneratedWorld, incidence, settlementPolicy,
                    fineGeography, fineSignature!));
        }
        if (_temporalPolicy is not null)
        {
            // No staged partial state is published: resolution and monetary
            // journal replay occur inside one canonical atomic phase.
            phases.Clear();
            var temporal = new FoundationTemporalCollectiveTurnPhase(
                session.GeneratedWorld, incidence, _travelPolicy!,
                _temporalPolicy, fineGeography, fineSignature, _constructionPhase,
                persistent ? constructionMoneyMicroPerWholeWork : null, enableDemographicGrowth, agriculture, operationalPolicy,
                enableExperimentalAutomaticBase: ExperimentalLocalEconomyEnabled,
                enableLocalPriceAllocation: ExperimentalLocalEconomyEnabled,
                enableDefaultAutomaticBase: DefaultAutomaticBaseEnabled, enableExclusiveNodeLabor: enableExclusiveNodeLabor);
            phases.Add(temporal);
            if (persistent)
            {
                _persistentPhase = temporal;
                _persistentFunding = new FoundationFundedWorkerIntentJournal(new FoundationConstructionFundingExecutor(
                    _monetaryExecutor!, constructionMoneyMicroPerWholeWork!.Value));
            }
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

    /// <summary>
    /// Read-only pending monetary intentions for the requesting human civilization.
    /// No treasury debit or physical construction occurs until the all-ready turn.
    /// </summary>
    public IReadOnlyList<FoundationMonetaryInfrastructureCommand>
        GetHumanMonetaryInvestments(CivilizationId requester)
    {
        RequireHuman(requester);
        return Array.AsReadOnly(_investmentPlans[requester].Values.ToArray());
    }

    /// <summary>
    /// An edge investment is an order of the CIVILIZATION, not a
    /// construction order of a physical worker. It does not create any
    /// resource/stock/material consumption. Money remains unchanged
    /// until the all-ready authoritative turn is accepted.
    /// </summary>
    public void QueueHumanMonetaryInvestment(
        CivilizationId human, PhysicalTacticalEdgeId edge, ulong moneyMicro)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        CheckWritable(human);
        if (_monetaryExecutor is null || !edge.IsValid || moneyMicro == 0UL)
            throw new InvalidOperationException(
                "Monetary investment is disabled or has invalid identity/amount.");

        if (_constructionPhase is not null && (Snapshot.Constructions.Projects.Any(p => p.Edge == edge && !p.IsActivated) ||
            _investmentPlans.Values.SelectMany(p => p.Values).Any(c => c.Edge == edge)))
            throw new InvalidOperationException("Construction edge already reserved.");
        if (_investmentPlans[human].ContainsKey(edge.FineStrategicEdgeId.Value))
            throw new InvalidOperationException(
                "A civilization may submit at most one investment per " +
                "physical edge during the same collective turn.");
        var physical = Snapshot.Transport.Edges.FirstOrDefault(r =>
            r.Edge == edge) ??
            throw new InvalidOperationException(
                "Investment cannot target an absent fine physical edge.");
        if (physical.CargoMicroAtReferenceLoad == ulong.MaxValue)
            throw new InvalidOperationException(
                "Open sea has no constructed infrastructure or financeable bottleneck.");
        if (physical.NaturalConstructionCostMoneyMicro is not > 0UL ||
            physical.OwnershipKind == PhysicalTransportOwnershipKind.UnownedInfrastructure ||
            (physical.OwnershipKind == PhysicalTransportOwnershipKind.Owned &&
             physical.InfrastructureOwner != human))
            throw new InvalidOperationException(
                "Financing requires a natural edge or the current owner of " +
                "one existing infrastructure; takeover is a military action.");

        ulong already = physical.CommittedInvestmentMoneyMicro ?? 0UL;
        var pendingEdge = _investmentPlans.Values
            .SelectMany(p => p.Values)
            .Where(c => c.Edge == edge)
            .Aggregate(System.Numerics.BigInteger.Zero,
                (sum, c) => sum + c.MoneyMicro);
        if ((System.Numerics.BigInteger)already + pendingEdge + moneyMicro >
                ulong.MaxValue)
            throw new InvalidOperationException(
                "Cumulative edge financing exceeds the canonical money range.");

        var pendingFromCivilization = _investmentPlans[human].Values
            .Aggregate(System.Numerics.BigInteger.Zero,
                (sum, c) => sum + GlobalArenaMoneyScale.FromMoneyMicro(c.MoneyMicro));
        var thisSpend = GlobalArenaMoneyScale.FromMoneyMicro(moneyMicro);
        if (pendingFromCivilization + thisSpend >
            Snapshot.Treasury.Get(human).CashScaled)
            throw new InvalidOperationException(
                "Insufficient authoritative cash for all pending investments.");

        _investmentPlans[human].Add(edge.FineStrategicEdgeId.Value,
            new FoundationMonetaryInfrastructureCommand(
                new CommandId(NextTurn, checked(
                    (ulong)_investmentPlans[human].Count + 1UL)),
                human, edge, moneyMicro));
    }

    public IReadOnlyList<FoundationConstructionWorkerAssignment> GetHumanConstructionOrders(CivilizationId human)
    {
        RequireHuman(human);
        return Array.AsReadOnly(_constructionPlans.Where(p => p.Value.Owner == human)
            .OrderBy(p => p.Key.Value).Select(p => new FoundationConstructionWorkerAssignment(p.Key, p.Value.Project)).ToArray());
    }

    public void QueueHumanConstructionWork(CivilizationId human, PhysicalWorkerId worker, ulong projectId)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        CheckWritable(human);
        if (_constructionPhase is null || projectId == 0UL || _plans[human].ContainsKey(worker.Value))
            throw new InvalidOperationException("Construction is disabled or worker has a competing order.");
        var person = RequireOwnedWorker(human, worker);
        var project = Snapshot.Constructions.Projects.SingleOrDefault(p => p.Id == projectId);
        if (project is null || project.Investor != human || project.IsComplete ||
            person.Activity != PhysicalWorkerActivity.Other ||
            person.Occupation != PhysicalWorkerOccupation.Unassigned)
            throw new InvalidOperationException("Construction needs an eligible idle worker and existing owned project.");
        _ = FoundationConstructionWorkerWork.Apply(Snapshot, Incidence, _signedFine!,
            new[] { new FoundationConstructionWorkerAssignment(worker, projectId) });
        _constructionPlans[worker] = (human, projectId);
    }

    public bool CancelHumanConstructionWork(CivilizationId human, PhysicalWorkerId worker)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        CheckWritable(human);
        return _constructionPlans.TryGetValue(worker, out var plan) &&
            plan.Owner == human && _constructionPlans.Remove(worker);
    }

    public bool CancelHumanMonetaryInvestment(
        CivilizationId human, PhysicalTacticalEdgeId edge)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        CheckWritable(human);
        return edge.IsValid &&
            _investmentPlans[human].Remove(edge.FineStrategicEdgeId.Value);
    }

    public void QueueHumanMove(CivilizationId human, PhysicalWorkerId id,
        PhysicalTacticalTileId destination)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        QueueMove(human, id, destination);
    }

    public void QueueHumanActivity(CivilizationId human, PhysicalWorkerId id,
        PhysicalWorkerActivity activity)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
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
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        QueueOccupation(human, id, occupation);
    }

    /// <summary>
    /// R148: one queued command to change a real producer directly into a
    /// land or port operator. The worker remains at their exact fine tile,
    /// and nothing becomes effective before the collective turn is closed.
    /// Existing Other operators may switch between operational roles.
    /// </summary>
    public void QueueHumanOperationalRole(CivilizationId human,
        PhysicalWorkerId id, FoundationOperationalWorkKind kind)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        var occupation = kind switch
        {
            FoundationOperationalWorkKind.Land =>
                PhysicalWorkerOccupation.LandTransport,
            FoundationOperationalWorkKind.Port =>
                PhysicalWorkerOccupation.PortOperations,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        QueueActivityAndOccupation(human, id, PhysicalWorkerActivity.Other,
            occupation);
    }

    /// <summary>
    /// R148: direct return from logistics to a productive occupation using
    /// ONE local, replayable event. Ordinary occupation-only changes remain
    /// available through QueueHumanOccupation.
    /// </summary>
    public void QueueHumanProductiveRole(CivilizationId human,
        PhysicalWorkerId id, PhysicalWorkerOccupation occupation)
    {
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        if (!FoundationTacticalWorkerReassignmentPolicy.IsProductive(occupation))
            throw new ArgumentOutOfRangeException(nameof(occupation));
        QueueActivityAndOccupation(human, id,
            PhysicalWorkerActivity.Producing, occupation);
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
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
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
            if (staged.ContainsKey(change.WorkerId.Value))
                throw new InvalidOperationException(
                    "A physical worker cannot appear twice in the same batch.");
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
        if (PersistentOrdersEnabled) throw new InvalidOperationException("Use persistent queue commands in this host.");
        RequireHuman(human);
        CheckWritable(human);
        return id.IsValid && _plans[human].Remove(id.Value);
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
            if (PersistentOrdersEnabled && _ai[civ] is FoundationPersistentEconomicAiPolicy persistentAi)
            {
                PreparePersistentAi(civ, persistentAi);
                continue;
            }
            var before = Snapshot;
            var proposed = _ai[civ].Plan(
                civ, before, _incidence, NextTurn)
                ?? throw new InvalidOperationException("AI returned a null plan.");
            if (!ReferenceEquals(before, Snapshot))
                throw new InvalidOperationException(
                    "AI mutated canonical WorldState while planning.");
            var decisions = proposed.ToArray();
            if (PersistentOrdersEnabled)
            {
                if (decisions.Length != 0)
                    throw new InvalidOperationException("Persistent host currently requires an AI hold plan; legacy AI decisions are not silently converted.");
                _ready.Add(civ);
                continue;
            }
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

    // Only committed own-worker data crosses the new policy boundary.
    // Stage the whole batch privately before making any edit visible.
    private void PreparePersistentAi(CivilizationId civ, FoundationPersistentEconomicAiPolicy policy)
    {
        var before = Snapshot;
        var queues = before.WorkerOrders.Queues.Where(q => q.Owner == civ).ToDictionary(q => q.Worker);
        var crossings = before.EdgeTravels.Pending.Select(p => p.Worker).ToHashSet();
        var workers = before.Workers.Workers.Where(w => w.Owner == civ)
            .OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPersistentAiWorker(w.Id, w.TacticalTileId!.Value,
                w.Activity, crossings.Contains(w.Id), queues[w.Id])).ToArray();
        var local = workers.Select(w => w.Node).Distinct()
            .OrderBy(node => node.FineStrategicCellId.Value)
            .Select(node => ReadCommittedOwnNodeEconomics(civ, node))
            .OfType<FoundationViewerLocalEconomicClose>().ToArray();
        var view = new FoundationPersistentAiView(civ, NextTurn, Array.AsReadOnly(workers))
        {
            LocalEconomics = Array.AsReadOnly(local),
            AutomaticBaseAvailable = ExperimentalLocalEconomyEnabled,
            DefaultAutomaticBase = DefaultAutomaticBaseEnabled,
            OwnCashScaled = before.Treasury.IsInitialized ? before.Treasury.Get(civ).CashScaled : null,
            FundingAvailable = _persistentFunding is not null,
            InvestmentOpportunities = policy.RequiresInvestmentCandidates
                ? ReadAiInvestmentOpportunities(civ, workers)
                : Array.Empty<FoundationAiInvestmentOpportunity>(),
            AdjacentOpportunities = policy.RequiresExpansionCandidates
                ? ReadAdjacentAiOpportunities(civ, workers, local)
                : Array.Empty<FoundationAiAdjacentOpportunity>()
        };
        var (plannedEdits, funding) = policy.PlanTurn(view);
        var edits = (plannedEdits
            ?? throw new InvalidOperationException("Persistent AI returned a null plan.")).ToArray();
        if (funding is not null && edits.Length != 0)
            throw new InvalidOperationException("Experimental AI funding must be an exclusive plan.");
        CheckSnapshot();
        if (!ReferenceEquals(before, Snapshot))
            throw new InvalidOperationException("Persistent AI mutated canonical WorldState while planning.");
        var list = _persistentPlans[civ];
        if (list.Count != 0)
            throw new InvalidOperationException("Unexpected pending persistent AI plan.");
        var staged = new List<ISimulationCommand>(edits.Length);
        foreach (var edit in edits)
        {
            ArgumentNullException.ThrowIfNull(edit);
            if (_knowledgeScopes is not null && edit.Kind == FoundationPersistentOrderEditKind.Insert &&
                edit.OrderKind == FoundationWorkerOrderKind.Move)
            {
                var worker = workers.FirstOrDefault(w => w.Id == edit.Worker);
                if (worker is null || !edit.Destination.HasValue ||
                    !_knowledgeScopes[civ].KnowsLink(worker.Node, edit.Destination.Value) ||
                    !_executor.CheckEdge(worker.Node, edit.Destination.Value).HasValue)
                    throw new InvalidOperationException("Restricted AI move requires a known adjacent link.");
            }
            staged.Add(CreatePersistentEdit(civ, edit, staged.Count));
        }
        if (funding is not null)
        {
            if (_knowledgeScopes is not null)
            {
                var scope = _knowledgeScopes[civ];
                var endpoints = Incidence.FineTopology.Edges
                    .Where(e => e.Id == funding.Edge.FineStrategicEdgeId &&
                        funding.Edge.FineGoldbergParameters == Incidence.Refinement.FineParameters)
                    .Select(e => e.IncidentCellIds).SingleOrDefault();
                if (endpoints is null || !scope.KnowsLink(
                        new PhysicalTacticalTileId(Incidence.Refinement.FineParameters, endpoints[0]),
                        new PhysicalTacticalTileId(Incidence.Refinement.FineParameters, endpoints[1])))
                    throw new InvalidOperationException("Restricted AI funding requires a known physical link.");
            }
            staged.Add(CreatePersistentFunding(civ, funding.Edge, funding.MoneyMicro, funding.Assignments, 0));
        }
        _ = ProjectPersistentPlan(civ, staged);
        list.AddRange(staged);
        _ready.Add(civ);
    }

    // Restrict candidate reads before evaluating terrain or productivity.
    // A fixed scope is a QA input; persistent discovery and restricted route
    // execution still need their own authoritative contract.
    private IReadOnlyList<FoundationAiAdjacentOpportunity> ReadAdjacentAiOpportunities(
        CivilizationId owner, FoundationPersistentAiWorker[] workers, FoundationViewerLocalEconomicClose[] local)
    {
        if (!DefaultAutomaticBaseEnabled || _signedFine is not { } geography || _travelPolicy is null ||
            AgriculturalProduction is not { BasicResources: { } basic } agriculture)
            return Array.Empty<FoundationAiAdjacentOpportunity>();
        var scope = _knowledgeScopes is null ? null : _knowledgeScopes[owner];
        var ownNodes = workers.Select(w => w.Node).ToHashSet();
        var pricesByNode = local.ToDictionary(row => row.Node,
            row => row.Commodities.ToDictionary(c => c.Commodity, c => c.LocalPriceMicro));
        var result = new List<FoundationAiAdjacentOpportunity>();
        var transport = Snapshot.Transport.Edges.ToDictionary(e => e.Edge);
        foreach (var group in workers.GroupBy(w => w.Node).OrderBy(g => g.Key.FineStrategicCellId.Value))
        {
            if (group.Count(w => !w.Crossing && w.Activity != PhysicalWorkerActivity.Traveling &&
                w.Queue.Orders.Count == 0 && w.Queue.BaseActivity?.Mode != FoundationWorkerBaseMode.Manual) < 3)
                continue;
            var source = group.Key;
            if (scope is not null && !scope.KnowsNode(source)) continue;
            int index = checked((int)source.FineStrategicCellId.Value - 1);
            if (geography.SurfaceAtIndex(index) != FoundationFineSurfaceKind.Land ||
                !geography.CanTraverseOnFoot(index)) continue;
            pricesByNode.TryGetValue(source, out var prices);
            BigInteger Value(CommodityId item, ulong output) =>
                new BigInteger(output) * (prices?.GetValueOrDefault(item) ??
                    FoundationSettledLocalPriceSnapshot.InitialReferencePriceMicro);
            BigInteger Potential(PhysicalTacticalTileId node)
            {
                var best = BigInteger.Max(Value(basic.Wood, basic.WoodWholeTurnOutput),
                    Value(basic.Stone, basic.StoneWholeTurnOutput));
                foreach (var crop in CropCatalog.All)
                {
                    var output = agriculture.Resolve(node, crop.Id);
                    best = BigInteger.Max(best, Value(output.Commodity, output.WholeTurnOutput));
                }
                return best;
            }
            var originPotential = Potential(source);
            foreach (var adjacent in Incidence.FineTopology.Cells[index].AdjacentCellIds.OrderBy(n => n.Value))
            {
                var destination = new PhysicalTacticalTileId(Incidence.Refinement.FineParameters, adjacent);
                if (scope is not null && !scope.KnowsLink(source, destination)) continue;
                int next = checked((int)adjacent.Value - 1);
                if (ownNodes.Contains(destination) || geography.SurfaceAtIndex(next) != FoundationFineSurfaceKind.Land ||
                    !geography.CanTraverseOnFoot(next) ||
                    geography.CrossingAtIndices(index, next) != FoundationFineTransportCrossingKind.Ground ||
                    _executor.CheckEdge(source, destination) is not { } physicalEdge)
                    continue;
                var duration = FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(transport[physicalEdge], _travelPolicy).ImprovedCrossingTimeMicro;
                result.Add(new(source, destination, originPotential, Potential(destination), duration));
            }
        }
        return Array.AsReadOnly(result.ToArray());
    }

    // Read only the owner's verified participant cargo, never global load.
    // Keep this first estimate to known, locally occupied ground links.
    private IReadOnlyList<FoundationAiInvestmentOpportunity> ReadAiInvestmentOpportunities(
        CivilizationId owner, FoundationPersistentAiWorker[] workers)
    {
        if (!DefaultAutomaticBaseEnabled || _persistentFunding is null ||
            _signedFine is not { } geography || !Snapshot.Treasury.IsInitialized ||
            !Snapshot.Transport.HasMonetaryConstructionCosts)
            return Array.Empty<FoundationAiInvestmentOpportunity>();
        var sale = _lastTemporalClose?.TemporalEconomicSettlement;
        if (sale?.AuditedParticipantEdges is not { } audit ||
            sale.EconomicCycleOrdinal != Snapshot.Treasury.LastSettledEconomicCycleOrdinal ||
            !audit.TryGetValue(owner, out var used))
            return Array.Empty<FoundationAiInvestmentOpportunity>();
        var budget = Snapshot.Treasury.Get(owner).CashScaled /
            (10 * GlobalArenaMoneyScale.ExactScaledPerMoneyMicro);
        if (budget <= 0 || used.Count == 0)
            return Array.Empty<FoundationAiInvestmentOpportunity>();
        var sources = workers.Where(w => !w.Crossing && w.Activity != PhysicalWorkerActivity.Traveling &&
            w.Queue.Orders.Count == 0 && w.Queue.BaseActivity?.Mode != FoundationWorkerBaseMode.Manual)
            .GroupBy(w => w.Node).Where(g => g.Count() >= 3).Select(g => g.Key).ToHashSet();
        if (sources.Count == 0) return Array.Empty<FoundationAiInvestmentOpportunity>();
        var scope = _knowledgeScopes is null ? null : _knowledgeScopes[owner];
        var active = Snapshot.Constructions.Projects.Where(p => !p.IsActivated).Select(p => p.Edge).ToHashSet();
        var cargo = used.Where(e => e.TotalCargoMicro > 0 &&
            (sources.Contains(e.FirstTile) || sources.Contains(e.SecondTile)) &&
            (scope is null || scope.KnowsLink(e.FirstTile, e.SecondTile)) &&
            !active.Contains(e.Edge))
            .ToDictionary(e => e.Edge);
        if (cargo.Count == 0) return Array.Empty<FoundationAiInvestmentOpportunity>();
        var result = new List<FoundationAiInvestmentOpportunity>();
        foreach (var edge in Snapshot.Transport.Edges)
        {
            if (!cargo.TryGetValue(edge.Edge, out var load) ||
                (edge.InfrastructureOwner.HasValue && edge.InfrastructureOwner != owner) ||
                edge.NaturalConstructionCostMoneyMicro is not { } reference || reference == 0UL ||
                geography.CrossingAtIndices((int)load.FirstTile.FineStrategicCellId.Value - 1,
                    (int)load.SecondTile.FineStrategicCellId.Value - 1) != FoundationFineTransportCrossingKind.Ground)
                continue;
            ulong invested = edge.CommittedInvestmentMoneyMicro.GetValueOrDefault();
            ulong spend = (ulong)BigInteger.Min(budget, BigInteger.Min(reference, ulong.MaxValue - invested));
            if (spend == 0UL) continue;
            ulong current = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(edge);
            ulong projected = FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(edge, spend);
            if (projected >= current) continue;
            foreach (var source in new[] { load.FirstTile, load.SecondTile }.Where(sources.Contains))
                result.Add(new(edge.Edge, source, load.TotalCargoMicro, spend, current, projected));
        }
        return Array.AsReadOnly(result.OrderBy(c => c.Edge.FineStrategicEdgeId.Value)
            .ThenBy(c => c.Source.FineStrategicCellId.Value).ToArray());
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

        if (PersistentOrdersEnabled) return ClosePersistentTurn();
        var before = Snapshot;
        var turn = NextTurn;
        // R141: after a completed fine-edge travel, a capable idle human
        // worker resumes productive activity on the NEXT collective turn.
        // A real human order (move, occupation, activity) always takes priority.
        // This is a journaled command, never an implicit WorldState mutation.
        var automaticResumptions = Snapshot.Workers.Workers
            .Where(worker => _humans.Contains(worker.Owner) &&
                worker.Activity == PhysicalWorkerActivity.Traveling &&
                !Snapshot.EdgeTravels.Pending.Any(p => p.Worker == worker.Id) &&
                worker.Occupation is not PhysicalWorkerOccupation.LandTransport and
                    not PhysicalWorkerOccupation.PortOperations &&
                worker.OutputMicroPerTurn > 0UL &&
                worker.TacticalTileId.HasValue &&
                !_plans[worker.Owner].ContainsKey(worker.Id.Value))
            .OrderBy(worker => worker.Id.Value)
            .Select(worker => new FoundationTacticalWorkerOrder(
                worker.Owner, worker.Id,
                worker.TacticalTileId!.Value, worker.TacticalTileId.Value,
                PhysicalWorkerActivity.Producing, IsMovement: false))
            .ToArray();
        var orders = _plans.Values.SelectMany(plan => plan.Values)
            .Concat(automaticResumptions)
            .OrderBy(order => order.WorkerId.Value).ToArray();
        // Monetary orders do not occupy or teleport any worker. Sort by
        // civilization and real edge for deterministic multi-investor turns.
        var investments = _investmentPlans.Values
            .SelectMany(plan => plan.Values)
            .OrderBy(c => c.Issuer.Value)
            .ThenBy(c => c.Edge.FineStrategicEdgeId.Value)
            .ToArray();
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

        var continuations = Snapshot.EdgeTravels.Pending
            .OrderBy(p => p.Worker.Value).ToArray();
        if (orders.Any(o => continuations.Any(p => p.Worker == o.WorkerId)))
            throw new InvalidOperationException(
                "Unfinished physical crossing cannot receive competing orders.");
        var constructionOrders = _constructionPlans.OrderBy(p => p.Key.Value).ToArray();
        if (orders.Any(o => _constructionPlans.ContainsKey(o.WorkerId)) ||
            continuations.Any(p => _constructionPlans.ContainsKey(p.Worker)))
            throw new InvalidOperationException("Construction worker has competing turn orders.");
        var commands = orders.Select((order, index) =>
            _travelExecutor is not null && order.IsMovement
                ? (ISimulationCommand)new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, checked((ulong)index + 1UL)),
                    order.Issuer, order.WorkerId, order.Destination, isStart: true)
                : new PhysicalTacticalWorkerMoveCommand(
                    new CommandId(turn, checked((ulong)index + 1UL)),
                    order.Issuer, order.WorkerId, order.Destination,
                    order.Activity, order.NextOccupation))
            .Concat(continuations.Select((pending, index) =>
                (ISimulationCommand)new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn,
                        checked((ulong)(orders.Length + index) + 1UL)),
                    pending.Owner, pending.Worker, pending.Destination,
                    isStart: false)))
            .Concat(investments.Select((order, index) =>
                (ISimulationCommand)new FoundationMonetaryInfrastructureCommand(
                    new CommandId(turn,
                        checked((ulong)(orders.Length + continuations.Length + index) + 1UL)),
                    order.Issuer, order.Edge, order.MoneyMicro)))
            .Concat(constructionOrders.Select((order, index) => (ISimulationCommand)new FoundationConstructionWorkCommand(
                new CommandId(turn, checked((ulong)(orders.Length + continuations.Length + investments.Length + index) + 1UL)),
                order.Value.Owner, order.Key, order.Value.Project)))
            .ToArray();
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
        var events = resolved.Events
            .OfType<PhysicalTacticalWorkerMovedEvent>().ToArray();
        var travelEvents = resolved.Events
            .OfType<FoundationEdgeTravelTurnEvent>().ToArray();
        var moneyEvents = resolved.Events
            .OfType<FoundationMonetaryInfrastructureInvestedEvent>().ToArray();
        var settled = resolved.Events
            .OfType<FoundationEconomicSalesSettledEvent>().ToArray();
        var temporalSettled = resolved.Events
            .OfType<FoundationTemporalSalesSettledEvent>().ToArray();
        var fundedEvents = resolved.Events.OfType<FoundationConstructionFundedEvent>().ToArray();
        var workEvents = resolved.Events.OfType<FoundationConstructionWorkBatchEvent>().ToArray();
        int expectedEventCount = commands.Length - constructionOrders.Length + (constructionOrders.Length > 0 ? 1 : 0);
        int expectedSettlements = _economicExecutor is null &&
            _temporalPolicy is null ? 0 : 1;
        if (events.Length != orders.Count(o => !o.IsMovement || _travelExecutor is null) ||
            travelEvents.Length != orders.Count(o => o.IsMovement && _travelExecutor is not null) +
                continuations.Length ||
            moneyEvents.Length + fundedEvents.Length != investments.Length ||
            workEvents.Length != (constructionOrders.Length > 0 ? 1 : 0) ||
            settled.Length + temporalSettled.Length != expectedSettlements ||
            (_temporalPolicy is not null && temporalSettled.Length != 1) ||
            resolved.Events.Count != expectedEventCount + expectedSettlements ||
            resolved.EventLog.Entries.Count !=
                expectedEventCount + expectedSettlements ||
            resolved.EventLog.Entries.Any(entry => !entry.WasExecuted))
            throw new InvalidOperationException(
                "Committed journal differs from the complete worker, " +
                "investment and real production/consumption settlement.");

        ISimulationEventExecutor replayExecutor =
            _monetaryExecutor is null
                ? _executor
                : new FoundationMonetaryCollectiveReplayExecutor(
                    _executor, _monetaryExecutor, _economicExecutor);
        if (_travelExecutor is not null)
            replayExecutor = new FoundationEdgeTravelCompositeReplayExecutor(
                replayExecutor, _travelExecutor);
        if (_constructionPhase is not null)
            replayExecutor = _constructionPhase.CreateReplayExecutor(replayExecutor);
        var replay = _temporalPolicy is not null
            ? FoundationTemporalEconomicJournalGate.ReplayJournal(
                before, resolved.EventLog, _executor, _travelPolicy!,
                Incidence, _temporalPolicy, context, _signedFine,
                _monetaryExecutor, _constructionPhase)
            : new SimulationEventLogReplayer(replayExecutor)
                .Replay(new SimulationReplayInput(
                    before, resolved.EventLog, context));
        var hash = _hasher.Compute(Snapshot);
        var previousFormat = _hasher.Compute(before).FormatVersion;
        if ((hash.FormatVersion != previousFormat &&
             !(previousFormat == 13U &&
               (hash.FormatVersion == 14U || hash.FormatVersion == 15U)) &&
             !(previousFormat == 14U && hash.FormatVersion == 15U) &&
             !(previousFormat == 18U && hash.FormatVersion == 19U)) ||
            (hash.FormatVersion != 10U && hash.FormatVersion != 11U &&
             hash.FormatVersion != 13U && hash.FormatVersion != 14U &&
             hash.FormatVersion != 15U && hash.FormatVersion != 17U && hash.FormatVersion != 18U && hash.FormatVersion != 19U) ||
            _hasher.Compute(replay) != hash)
            throw new InvalidOperationException(
                "Worker, infrastructure and closed market journal replay " +
                "does not match the authoritative world hash.");
        // Financial/cargo evidence can become visible ONLY after its
        // entire all-ready journal was committed and independently
        // reproduced from real workers and canonical transport. The
        // read boundary publishes participant-specific volumes, never
        // global trade totals or a fabricated preview overlay.
        if (settled.SingleOrDefault() is { } verifiedCycle)
            _closedTrade.PublishVerifiedParticipantSettlement(
                verifiedCycle, Snapshot, NextTurn);
        if (temporalSettled.SingleOrDefault() is { } verifiedTemporalCycle)
            _closedTrade.PublishVerifiedTemporalSettlement(
                verifiedTemporalCycle, Snapshot, NextTurn);

        var receipt = new FoundationTacticalCollectiveReceipt(
            turn, hash, Snapshot.Revision, events, moneyEvents,
            settled.SingleOrDefault(), travelEvents,
            temporalSettled.SingleOrDefault(), fundedEvents, workEvents);
        // Do not expose the receipt until the entire all-ready turn, journal
        // replay and authoritative hash checks have succeeded. A subsequent
        // non-temporal cycle invalidates the old view (no stale presentation).
        _lastTemporalClose = receipt.TemporalEconomicSettlement is null
            ? null : receipt;
        foreach (var plan in _plans.Values) plan.Clear();
        foreach (var plan in _investmentPlans.Values) plan.Clear();
        _constructionPlans.Clear();
        _ready.Clear();
        _knownSnapshot = Snapshot;
        return receipt;
    }

    // Private plans are validated against an immutable projection of the
    // issuer's own earlier commands; no treasury or queue is published.
    private WorldState ProjectPersistentPlan(CivilizationId owner, IEnumerable<ISimulationCommand> commands)
    {
        if (!PersistentOrdersEnabled) throw new InvalidOperationException("Persistent orders disabled.");
        var state = Snapshot;
        var context = new SimulationContext(NextTurn, new SimulationSeed(712UL));
        foreach (var command in commands)
        {
            var id = new EventId(command.Id, 1UL);
            if (command is FoundationPersistentOrderEditCommand edit && edit.Issuer == owner)
                state = FoundationPersistentOrderEditJournal.Record(state, id, edit.Edit, context).Next;
            else if (command is FoundationFundedWorkerIntentCommand fund && fund.Issuer == owner)
                state = _persistentFunding!.Record(state, id, owner, fund.Edge, fund.MoneyMicro, fund.Assignments, context).Next;
            else throw new InvalidOperationException("Foreign persistent plan.");
        }
        return state;
    }

    public IReadOnlyList<FoundationWorkerOrderQueue> GetHumanPersistentQueues(CivilizationId human)
    {
        RequireHuman(human);
        CheckSnapshot();
        return Array.AsReadOnly(ProjectPersistentPlan(human, _persistentPlans[human])
            .WorkerOrders.Queues.Where(q => q.Owner == human).ToArray());
    }

    public void QueueHumanPersistentEdit(CivilizationId human, FoundationPersistentOrderEdit edit)
    {
        RequireHuman(human); CheckWritable(human);
        var list = _persistentPlans[human];
        var command = CreatePersistentEdit(human, edit, list.Count);
        _ = ProjectPersistentPlan(human, list.Append(command));
        list.Add(command);
    }

    private FoundationPersistentOrderEditCommand CreatePersistentEdit(
        CivilizationId owner, FoundationPersistentOrderEdit edit, int priorCount)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.Issuer != owner) throw new InvalidOperationException("Foreign order edit.");
        if (!PersistentOrdersEnabled) throw new InvalidOperationException("Persistent orders not configured.");
        if ((edit.Crop.HasValue || edit.BaseActivity?.Crop is not null) && AgriculturalProduction is null)
            throw new InvalidOperationException("Crop preview not configured.");
        if (edit.BaseActivity?.Mode == FoundationWorkerBaseMode.Automatic && !ExperimentalLocalEconomyEnabled)
            throw new InvalidOperationException("Automatic base allocation is not activated.");
        return new FoundationPersistentOrderEditCommand(new CommandId(NextTurn, (ulong)priorCount + 1UL), edit);
    }

    // Same private plan and collective publication as every other persistent edit.
    public void QueueHumanBaseActivity(CivilizationId human, PhysicalWorkerId worker,
        FoundationWorkerBaseActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        QueueHumanPersistentEdit(human, new FoundationPersistentOrderEdit(human, worker,
            FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: activity));
    }

    public void QueueHumanFundedConstruction(CivilizationId human, PhysicalTacticalEdgeId edge,
        ulong moneyMicro, IEnumerable<FoundationFundedWorkerInsertion> assignments)
    {
        RequireHuman(human); CheckWritable(human);
        // Admission is private for every participant, regardless of controller.
        var list = _persistentPlans[human];
        if (list.OfType<FoundationFundedWorkerIntentCommand>().Any(c => c.Edge == edge))
            throw new InvalidOperationException("Construction edge already reserved in own plan.");
        var command = CreatePersistentFunding(human, edge, moneyMicro, assignments, list.Count);
        _ = ProjectPersistentPlan(human, list.Append(command));
        list.Add(command);
    }

    private FoundationFundedWorkerIntentCommand CreatePersistentFunding(
        CivilizationId owner, PhysicalTacticalEdgeId edge, ulong moneyMicro,
        IEnumerable<FoundationFundedWorkerInsertion> assignments, int priorCount)
    {
        if (!PersistentOrdersEnabled || _persistentFunding is null)
            throw new InvalidOperationException("Persistent construction funding not configured.");
        ArgumentNullException.ThrowIfNull(assignments);
        return new FoundationFundedWorkerIntentCommand(new CommandId(NextTurn, (ulong)priorCount + 1UL),
            owner, edge, moneyMicro, Array.AsReadOnly(assignments.ToArray()));
    }

    public IReadOnlyList<FoundationFundedWorkerIntentCommand> GetHumanPersistentFunding(CivilizationId human)
    {
        RequireHuman(human); CheckSnapshot();
        return Array.AsReadOnly(_persistentPlans[human].OfType<FoundationFundedWorkerIntentCommand>().ToArray());
    }

    public bool CancelHumanPersistentFunding(CivilizationId human, PhysicalTacticalEdgeId edge)
    {
        RequireHuman(human); CheckWritable(human);
        var list = _persistentPlans[human];
        var fund = list.OfType<FoundationFundedWorkerIntentCommand>().SingleOrDefault(c => c.Edge == edge);
        if (fund is null) return false;
        // Reject cancellation if subsequent commands depend on the reservation.
        var remaining = list.Where(c => c.Id != fund.Id).ToArray();
        if (list.Last().Id != fund.Id)
            throw new InvalidOperationException("Cancel subsequent plan edits first, or clear the pending plan.");
        _ = ProjectPersistentPlan(human, remaining);
        list.Remove(fund);
        return true;
    }

    public void ClearHumanPersistentPlan(CivilizationId human)
    {
        RequireHuman(human); CheckWritable(human);
        if (!PersistentOrdersEnabled) throw new InvalidOperationException("Persistent orders disabled.");
        _persistentPlans[human].Clear();
    }

    private FoundationTacticalCollectiveReceipt ClosePersistentTurn()
    {
        using var profile = FoundationTurnProfiler.Measure("persistent_host_close");
        var turn = NextTurn;
        var before = Snapshot;
        var commands = new List<ISimulationCommand>();
        // Funding allocates global IDs. Translate local provisional project IDs
        // by replaying each participant's earlier fund operations in final order.
        var state = before;
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        // Resolve private funding claims before publishing any commands.
        // Rotating canonical participant order is independent of controller,
        // arrival, readiness and bid size. Preserve the canonical journal order.
        // A losing fund truncates its plan: later edits may depend on provisional
        // project IDs or queue positions, even without explicit ProjectId.
        var reservedFunding = new HashSet<PhysicalTacticalEdgeId>();
        var acceptedPrefixes = new Dictionary<CivilizationId, int>();
        var first = (int)((turn.Value - 1UL) % (ulong)_participants.Length);
        for (int offset = 0; offset < _participants.Length; offset++)
        {
            var civ = _participants[(first + offset) % _participants.Length];
            int count = 0;
            foreach (var pending in _persistentPlans[civ])
            {
                if (pending is FoundationFundedWorkerIntentCommand fund && !reservedFunding.Add(fund.Edge))
                    break;
                count++;
            }
            acceptedPrefixes.Add(civ, count);
        }
        foreach (var civ in _participants)
        {
            var local = before;
            var projects = new Dictionary<ulong, ulong>();
            foreach (var pending in _persistentPlans[civ].Take(acceptedPrefixes[civ]))
            {
                var id = new CommandId(turn, (ulong)commands.Count + 1UL);
                if (pending is FoundationFundedWorkerIntentCommand fund)
                {
                    var actual = fund with { Id = id };
                    var provisional = _persistentFunding!.Record(local, new EventId(fund.Id, 1UL),
                        civ, fund.Edge, fund.MoneyMicro, fund.Assignments, context);
                    var committed = _persistentFunding.Record(state, new EventId(id, 1UL),
                        civ, fund.Edge, fund.MoneyMicro, fund.Assignments, context);
                    projects.Add(provisional.Event.ProjectId, committed.Event.ProjectId);
                    local = provisional.Next; state = committed.Next; commands.Add(actual);
                }
                else if (pending is FoundationPersistentOrderEditCommand edit)
                {
                    var input = edit.Edit;
                    var mapped = input.ProjectId is { } p && projects.TryGetValue(p, out var real)
                        ? input with { ProjectId = real } : input;
                    local = FoundationPersistentOrderEditJournal.Record(local, new EventId(edit.Id, 1UL), input, context).Next;
                    state = FoundationPersistentOrderEditJournal.Record(state, new EventId(id, 1UL), mapped, context).Next;
                    commands.Add(new FoundationPersistentOrderEditCommand(id, mapped));
                }
            }
        }
        var committedTurn = _publisher.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(turn, allRequiredParticipantsReady: true, externalDeadlineReached: false),
            new TurnResolutionInput(before, commands.ToArray(), context)));
        if (!committedTurn.WasResolved || committedTurn.ResolutionResult is not { } resolved)
            throw new InvalidOperationException("Persistent collective turn failed.");
        // R211 already independently verifies the complete journal before
        // the publisher accepts it; no third market solve is required here.
        var sale = resolved.Events.OfType<FoundationTemporalSalesSettledEvent>().Single();
        var timeline = resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>().Single();
        using (FoundationTurnProfiler.Measure("trade_publication"))
            _closedTrade.PublishVerifiedTemporalSettlement(sale, Snapshot, NextTurn);
        var receipt = new FoundationTacticalCollectiveReceipt(turn, StateHash, Snapshot.Revision,
            Array.Empty<PhysicalTacticalWorkerMovedEvent>(), temporalSettlement: sale,
            persistentEdits: resolved.Events.OfType<FoundationPersistentOrderEditEvent>().ToArray(),
            persistentFunding: resolved.Events.OfType<FoundationFundedWorkerIntentEvent>().ToArray(),
            persistentTimeline: timeline, birthBatch: resolved.Events.OfType<FoundationPhysicalBirthBatchEvent>().SingleOrDefault());
        _lastTemporalClose = receipt;
        foreach (var plan in _persistentPlans.Values) plan.Clear();
        _ready.Clear(); _knownSnapshot = Snapshot;
        return receipt;
    }

    private void QueueMove(CivilizationId issuer, PhysicalWorkerId id,
        PhysicalTacticalTileId destination)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, id);
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
        var tile = worker.TacticalTileId!.Value;
        ValidateOrder(issuer, id, tile, worker.Activity,
            movement: false, nextOccupation: occupation);
        _plans[issuer][id.Value] = new FoundationTacticalWorkerOrder(
            issuer, id, tile, tile, worker.Activity,
            IsMovement: false, NextOccupation: occupation);
    }

    private void QueueActivityAndOccupation(
        CivilizationId issuer, PhysicalWorkerId id,
        PhysicalWorkerActivity activity, PhysicalWorkerOccupation occupation)
    {
        CheckWritable(issuer);
        var worker = RequireOwnedWorker(issuer, id);
        var tile = worker.TacticalTileId!.Value;
        // A worker already in the target occupation needs an activity-only
        // command; never fabricate a no-op occupational journal event.
        PhysicalWorkerOccupation? change = worker.Occupation == occupation
            ? null : occupation;
        ValidateOrder(issuer, id, tile, activity,
            movement: false, nextOccupation: change);
        _plans[issuer][id.Value] = new FoundationTacticalWorkerOrder(
            issuer, id, tile, tile, activity,
            IsMovement: false, NextOccupation: change);
    }

    private void ValidateOccupationTarget(
        PhysicalWorkerRuntimeRecord worker, PhysicalWorkerOccupation occupation)
    {
        if (!Snapshot.Workers.HasAuthoritativeOccupations ||
            !worker.Occupation.HasValue ||
            !(occupation switch
            {
                PhysicalWorkerOccupation.Agriculture or
                PhysicalWorkerOccupation.Forestry or
                PhysicalWorkerOccupation.Mining =>
                    (worker.Activity == PhysicalWorkerActivity.Producing ||
                     worker.Activity == PhysicalWorkerActivity.Other),
                PhysicalWorkerOccupation.LandTransport or
                PhysicalWorkerOccupation.PortOperations =>
                    worker.Activity == PhysicalWorkerActivity.Other,
                _ => false
            }) ||
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
        if (Snapshot.EdgeTravels.Pending.Any(p => p.Worker == workerId))
            throw new InvalidOperationException(
                "Cannot replace an unfinished physical crossing with another order.");
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
            if (!Snapshot.Workers.HasAuthoritativeOccupations ||
                !FoundationTacticalWorkerReassignmentPolicy
                    .IsValidOccupationTransition(worker, activity,
                        nextOccupation.Value, movement || edge.HasValue))
                throw new InvalidOperationException(
                    "Occupational reassignment must be a valid local " +
                    "production or operational transition.");
            return worker;
        }
        if (activity == PhysicalWorkerActivity.Producing &&
            worker.Occupation is PhysicalWorkerOccupation.LandTransport or
                PhysicalWorkerOccupation.PortOperations)
            throw new InvalidOperationException(
                "Logistics workers must change to a productive occupation before producing.");
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
