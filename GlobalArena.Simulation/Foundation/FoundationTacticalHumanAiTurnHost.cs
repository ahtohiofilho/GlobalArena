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
    public int ExecutedEvents => Events.Count + EdgeTravels.Count + InfrastructureInvestments.Count + ConstructionFunding.Count + ConstructionWork.Count +
        (EconomicSettlement is null ? 0 : 1) +
        (TemporalEconomicSettlement is null ? 0 : 1);
    public IReadOnlyList<FoundationConstructionFundedEvent> ConstructionFunding { get; }
    public IReadOnlyList<FoundationConstructionWorkBatchEvent> ConstructionWork { get; }
    public bool ReplayMatched { get; }

    internal FoundationTacticalCollectiveReceipt(
        TurnNumber turn, WorldStateHash hash, ulong revision,
        PhysicalTacticalWorkerMovedEvent[] events,
        FoundationMonetaryInfrastructureInvestedEvent[]? investments = null,
        FoundationEconomicSalesSettledEvent? economicSettlement = null,
        FoundationEdgeTravelTurnEvent[]? edgeTravels = null,
        FoundationTemporalSalesSettledEvent? temporalSettlement = null,
        FoundationConstructionFundedEvent[]? constructionFunding = null,
        FoundationConstructionWorkBatchEvent[]? constructionWork = null)
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
    private readonly CanonicalWorldStateHasher _hasher = new();
    private readonly HashSet<CivilizationId> _humans;
    private readonly IReadOnlyDictionary<CivilizationId,
        IFoundationTacticalAiTurnPolicy> _ai;
    private readonly CivilizationId[] _participants;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationTacticalWorkerOrder>> _plans;
    private readonly Dictionary<CivilizationId,
        SortedDictionary<ulong, FoundationMonetaryInfrastructureCommand>>
        _investmentPlans;
    private readonly HashSet<CivilizationId> _ready = new();
    private WorldState _knownSnapshot;
    private readonly FoundationEconomicTradeCycleReadBoundary _closedTrade;
    private FoundationTacticalCollectiveReceipt? _lastTemporalClose;

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
    public int PendingOrderCount => _plans.Values.Sum(plan => plan.Count) +
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
        ulong? constructionMoneyMicroPerWholeWork = null)
    {
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
            if (constructionMoneyMicroPerWholeWork is { } rate)
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
            phases.Add(new FoundationTemporalCollectiveTurnPhase(
                session.GeneratedWorld, incidence, _travelPolicy!,
                _temporalPolicy, fineGeography, fineSignature, _constructionPhase));
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

    public void QueueHumanConstructionWork(CivilizationId human, PhysicalWorkerId worker, ulong projectId)
    {
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
        _constructionPlans[worker] = (human, projectId);
    }

    public bool CancelHumanConstructionWork(CivilizationId human, PhysicalWorkerId worker)
    {
        RequireHuman(human);
        CheckWritable(human);
        return _constructionPlans.TryGetValue(worker, out var plan) &&
            plan.Owner == human && _constructionPlans.Remove(worker);
    }

    public bool CancelHumanMonetaryInvestment(
        CivilizationId human, PhysicalTacticalEdgeId edge)
    {
        RequireHuman(human);
        CheckWritable(human);
        return edge.IsValid &&
            _investmentPlans[human].Remove(edge.FineStrategicEdgeId.Value);
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
    /// R148: one queued command to change a real producer directly into a
    /// land or port operator. The worker remains at their exact fine tile,
    /// and nothing becomes effective before the collective turn is closed.
    /// Existing Other operators may switch between operational roles.
    /// </summary>
    public void QueueHumanOperationalRole(CivilizationId human,
        PhysicalWorkerId id, FoundationOperationalWorkKind kind)
    {
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
