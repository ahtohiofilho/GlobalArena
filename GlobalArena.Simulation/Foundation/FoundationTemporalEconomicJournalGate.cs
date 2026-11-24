using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// System-originated closed temporal sale. Its amounts are independently
/// recomputed from an ordered, replay-verified R167 physical event journal,
/// never accepted from the sender as financial authority.
/// </summary>
public sealed class FoundationTemporalSalesSettledEvent : ISimulationEvent
{
    public EventId Id { get; }
    public WorldStateHash BeginningHash { get; }
    public WorldStateHash PreSettlementHash { get; }
    public ulong PreSettlementRevision { get; }
    public ulong EconomicCycleOrdinal { get; }
    public string ScheduleDigest { get; }
    public string CargoUnitCode { get; }
    public ulong ProducedGoodsMicro { get; }
    public BigInteger GrossSalesScaled { get; }
    public BigInteger PhysicalFreightScaled { get; }
    public BigInteger SellerNetScaled { get; }
    public BigInteger TaxesScaled { get; }
    public BigInteger ExternalTollsScaled { get; }
    public BigInteger TotalCivilizationCreditsScaled { get; }
    public IReadOnlyList<FoundationCivilizationEconomicSettlementCredit> Credits { get; }

    public FoundationTemporalSalesSettledEvent(
        EventId id, WorldStateHash beginningHash, WorldStateHash preSettlementHash,
        ulong preSettlementRevision, ulong cycleOrdinal, string digest,
        string cargoUnitCode, ulong goods, BigInteger gross,
        BigInteger freight, BigInteger net, BigInteger taxes,
        BigInteger tolls, IEnumerable<FoundationCivilizationEconomicSettlementCredit> credits)
    {
        ArgumentNullException.ThrowIfNull(credits);
        var lines = credits.OrderBy(c => c.Civilization.Value).ToArray();
        if (id.OriginKind != EventOriginKind.System ||
            id.SystemPhaseId != FoundationTemporalEconomicJournalGate.StablePhaseId ||
            id.SystemOrdinal != 1UL || cycleOrdinal == 0UL ||
            string.IsNullOrWhiteSpace(digest) ||
            string.IsNullOrWhiteSpace(cargoUnitCode) ||
            lines.Length == 0 || lines.Any(c => c is null ||
                !c.Civilization.IsValid || c.CashCreditScaled < 0 ||
                c.CashCreditScaled != c.SellerNetScaled + c.SellerTaxScaled +
                    c.OwnerTollIncomeScaled) ||
            lines.Select(c => c.Civilization).Distinct().Count() != lines.Length ||
            gross < 0 || freight < 0 || net < 0 || taxes < 0 || tolls < 0 ||
            gross != freight + net + taxes + tolls)
            throw new ArgumentException("Invalid or imbalanced temporal economic journal.");
        Id = id;
        BeginningHash = beginningHash;
        PreSettlementHash = preSettlementHash;
        PreSettlementRevision = preSettlementRevision;
        EconomicCycleOrdinal = cycleOrdinal;
        ScheduleDigest = digest;
        CargoUnitCode = cargoUnitCode;
        ProducedGoodsMicro = goods;
        GrossSalesScaled = gross;
        PhysicalFreightScaled = freight;
        SellerNetScaled = net;
        TaxesScaled = taxes;
        ExternalTollsScaled = tolls;
        Credits = Array.AsReadOnly(lines);
        TotalCivilizationCreditsScaled = lines.Aggregate(BigInteger.Zero,
            (sum, line) => sum + line.CashCreditScaled);
        if (TotalCivilizationCreditsScaled + freight != gross)
            throw new ArgumentException("Physical freight plus financial credits must match sales.");
    }
}

/// <summary>
/// R170 explicit, restricted authoritative integration gate. It publishes one
/// actual treasury credit after R167 movements and independently reproduces
/// every event before accepting the resulting canonical state hash.
/// This initial gate does not accept same-turn capital construction or other
/// worker commands; those must be integrated into the collective journal
/// before host opt-in can be enabled.
/// </summary>
public static class FoundationTemporalEconomicJournalGate
{
    public const string StablePhaseId =
        "foundation.verified-temporal-sales-treasury-v17";
    private static readonly CanonicalWorldStateHasher Hasher = new();

    public sealed record Receipt(
        FoundationTemporalSalesSettledEvent EconomicEvent,
        SimulationEventLog Journal,
        WorldState Next,
        WorldStateHash Hash,
        bool ReplayMatched);

    public static Receipt CloseAndReplay(
        WorldState beginning,
        IReadOnlyList<FoundationEdgeTravelTurnEvent> physicalEvents,
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy movementPolicy,
        PhysicalTacticalIncidenceMap incidence,
        FoundationAuthoritativeEconomicSettlementPolicy economicPolicy,
        SimulationContext context,
        FoundationFineFirstPhysicalGeography? signedFine = null)
    {
        ArgumentNullException.ThrowIfNull(physicalEvents);
        return CloseAndReplayWithInvestments(beginning,
            physicalEvents.Cast<ISimulationEvent>().ToArray(),
            movement, movementPolicy, incidence, economicPolicy, context,
            investmentExecutor: null, signedFine: signedFine);
    }

    /// <summary>
    /// R170 same-turn verified capital investment extension. Incoming
    /// events must be replayable commands, with all physical travel before
    /// all investments; funds are debited BEFORE fresh sales are credited.
    /// </summary>
    public static Receipt CloseAndReplayWithInvestments(
        WorldState beginning,
        IReadOnlyList<ISimulationEvent> physicalAndInvestmentEvents,
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy movementPolicy,
        PhysicalTacticalIncidenceMap incidence,
        FoundationAuthoritativeEconomicSettlementPolicy economicPolicy,
        SimulationContext context,
        FoundationMonetaryInfrastructureEventExecutor? investmentExecutor,
        FoundationFineFirstPhysicalGeography? signedFine = null,
        FoundationConstructionCollectiveTurnPhase? constructionPhase = null)
    {
        ArgumentNullException.ThrowIfNull(beginning);
        ArgumentNullException.ThrowIfNull(physicalAndInvestmentEvents);
        if (!beginning.EdgeTravels.IsInitialized ||
            !beginning.Treasury.IsInitialized ||
            !beginning.Transport.HasMonetaryConstructionCosts)
            throw new InvalidOperationException(
                "Temporal cash cannot close without a bound opt-in world and funded treasury.");
        var executor = new ReplayExecutor(beginning, movement, movementPolicy,
            incidence, economicPolicy, signedFine, investmentExecutor, constructionPhase);
        var staged = beginning;
        foreach (var ev in physicalAndInvestmentEvents)
            staged = executor.Execute(staged, ev, context);
        var (settlement, expected) = Derive(beginning, staged,
            executor.VerifiedPhysicalEvents, executor.VerifiedLocalEvents,
            executor.VerifiedInvestmentCount,
            incidence, economicPolicy, context, signedFine);
        // Resolution solves once; the fresh journal executor below solves
        // independently once more. Avoid an unneeded third market pass.
        var actual = expected;
        var events = physicalAndInvestmentEvents
            .Append((ISimulationEvent)settlement).ToArray();
        var journal = new SimulationEventLog(events.Select((ev, index) =>
            new SimulationEventLogEntry(checked((ulong)index + 1UL), ev,
                wasEligible: true, wasExecuted: true)).ToArray());
        var independent = ReplayJournal(beginning, journal, movement,
            movementPolicy, incidence, economicPolicy, context, signedFine,
            investmentExecutor, constructionPhase);
        var hash = Hasher.Compute(actual);
        if (hash != Hasher.Compute(independent) ||
            hash != Hasher.Compute(expected))
            throw new InvalidOperationException(
                "Temporal monetary close failed independent canonical journal replay.");
        return new Receipt(settlement, journal, actual, hash, true);
    }

    /// <summary>
    /// Externally auditable, independent replay boundary. A log without an
    /// executed SYSTEM sale, or with a duplicate sale, is never accepted.
    /// </summary>
    public static WorldState ReplayJournal(
        WorldState beginning, SimulationEventLog journal,
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy movementPolicy,
        PhysicalTacticalIncidenceMap incidence,
        FoundationAuthoritativeEconomicSettlementPolicy economicPolicy,
        SimulationContext context,
        FoundationFineFirstPhysicalGeography? signedFine = null,
        FoundationMonetaryInfrastructureEventExecutor? investmentExecutor = null,
        FoundationConstructionCollectiveTurnPhase? constructionPhase = null)
    {
        ArgumentNullException.ThrowIfNull(beginning);
        ArgumentNullException.ThrowIfNull(journal);
        var executor = new ReplayExecutor(beginning, movement, movementPolicy,
            incidence, economicPolicy, signedFine, investmentExecutor, constructionPhase);
        var state = new SimulationEventLogReplayer(executor).Replay(
            new SimulationReplayInput(beginning, journal, context));
        if (!executor.IsClosed)
            throw new InvalidOperationException(
                "The physical journal did not contain a completed temporal economic sale.");
        return state;
    }

    private static (
        FoundationTemporalSalesSettledEvent Event, WorldState State) Derive(
        WorldState beginning, WorldState staged,
        IReadOnlyList<FoundationEdgeTravelTurnEvent> physicalEvents,
        IReadOnlyList<PhysicalTacticalWorkerMovedEvent> localEvents,
        int investmentCount,
        PhysicalTacticalIncidenceMap incidence,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        SimulationContext context,
        FoundationFineFirstPhysicalGeography? signedFine)
    {
        if (HashedBeginningInvalid(beginning, staged, investmentCount) ||
            physicalEvents.Any(ev => ev.Id.Turn != context.Turn) ||
            localEvents.Any(ev => ev.Id.Turn != context.Turn))
            throw new InvalidOperationException(
                "Temporal monetary evidence is not from one authoritative worker turn.");
        var calendar = FoundationTemporalEconomicTurnProjector.Project(
            beginning, staged, physicalEvents, localEvents, policy.Demand);
        var financial = FoundationTemporalFinancialReconciler.Evaluate(
            beginning, incidence, calendar, policy, signedFine, beginning.Constructions.IsInitialized ? beginning : staged);
        ulong ordinal = checked(staged.Treasury.LastSettledEconomicCycleOrdinal + 1UL);
        if (staged.Treasury.HasClosedEconomicCycle &&
            staged.Treasury.LastSettledCargoUnitCode != policy.Cargo.CargoUnitCode)
            throw new InvalidOperationException(
                "Temporal market cannot change historical cargo units.");
        var credits = financial.Accounting.Accounts
            .Select(row => new FoundationCivilizationEconomicSettlementCredit(
                row.Civilization, row.SellerNetScaled, row.RevenueTaxScaled,
                row.TollIncomeScaled, row.SellerNetScaled +
                    row.RevenueTaxScaled + row.TollIncomeScaled)).ToArray();
        var quotes = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(staged, incidence).Roads
            .ToDictionary(road => road.Edge,
                road => road.PhysicalCostMicroPerWholeCargoUnit);
        var nextFreight = staged.Transport.Edges.Select(edge =>
            new CivilizationSettledFreightQuote(edge.Edge,
                quotes[edge.Edge], edge.CargoMicroAtReferenceLoad, 0UL))
            .ToArray();
        var ev = new FoundationTemporalSalesSettledEvent(
            EventId.ForSystemPhase(context.Turn, StablePhaseId, 1UL),
            Hasher.Compute(beginning), Hasher.Compute(staged),
            staged.Revision, ordinal, calendar.Presence.ScheduleDigest,
            policy.Cargo.CargoUnitCode, calendar.TotalProductionMicro,
            financial.Accounting.GrossSalesScaled,
            financial.Accounting.PhysicalFreightScaled,
            financial.Accounting.NetSellerReceiptsScaled,
            financial.Accounting.RevenueTaxScaled,
            financial.Accounting.ExternalTollsScaled, credits);
        var amounts = ev.Credits.ToDictionary(c => c.Civilization,
            c => c.CashCreditScaled);
        var state = staged.RecordAuditedEconomicCycle(
            ordinal, policy.Cargo.CargoUnitCode, amounts, nextFreight)
            .AdvanceRevision();
        return (ev, state);
    }

    private static bool HashedBeginningInvalid(
        WorldState start, WorldState end, int investments) =>
        !start.EdgeTravels.IsInitialized ||
        start.WorldBinding != end.WorldBinding ||
        start.Civilizations != end.Civilizations ||
        (investments == 0 &&
            (start.Transport != end.Transport || start.Treasury != end.Treasury)) ||
        (investments > 0 &&
            (start.Treasury.LastSettledEconomicCycleOrdinal !=
                end.Treasury.LastSettledEconomicCycleOrdinal ||
             start.Treasury.LastSettledCargoUnitCode !=
                end.Treasury.LastSettledCargoUnitCode)) ||
        start.Workers.Workers.Count != end.Workers.Workers.Count;

    private static void RequireIdentical(
        FoundationTemporalSalesSettledEvent received,
        FoundationTemporalSalesSettledEvent expected)
    {
        if (received.Id != expected.Id ||
            received.BeginningHash != expected.BeginningHash ||
            received.PreSettlementHash != expected.PreSettlementHash ||
            received.PreSettlementRevision != expected.PreSettlementRevision ||
            received.EconomicCycleOrdinal != expected.EconomicCycleOrdinal ||
            received.ScheduleDigest != expected.ScheduleDigest ||
            received.CargoUnitCode != expected.CargoUnitCode ||
            received.ProducedGoodsMicro != expected.ProducedGoodsMicro ||
            received.GrossSalesScaled != expected.GrossSalesScaled ||
            received.PhysicalFreightScaled != expected.PhysicalFreightScaled ||
            received.SellerNetScaled != expected.SellerNetScaled ||
            received.TaxesScaled != expected.TaxesScaled ||
            received.ExternalTollsScaled != expected.ExternalTollsScaled ||
            received.TotalCivilizationCreditsScaled !=
                expected.TotalCivilizationCreditsScaled ||
            !received.Credits.SequenceEqual(expected.Credits))
            throw new InvalidOperationException(
                "Forgery, duplicate, or stale temporal sale/cash journal event.");
    }

    private sealed class ReplayExecutor : ISimulationEventExecutor
    {
        private readonly WorldState _beginning;
        private readonly FoundationEdgeTravelEventExecutor _travel;
        private readonly PhysicalTacticalWorkerMoveEventExecutor _local;
        private readonly PhysicalTacticalIncidenceMap _incidence;
        private readonly FoundationAuthoritativeEconomicSettlementPolicy _policy;
        private readonly FoundationFineFirstPhysicalGeography? _signedFine;
        private readonly List<FoundationEdgeTravelTurnEvent> _events = new();
        private readonly List<PhysicalTacticalWorkerMovedEvent> _localEvents = new();
        private readonly HashSet<PhysicalWorkerId> _orderedWorkers = new();
        private readonly FoundationMonetaryInfrastructureEventExecutor? _investment;
        private readonly ISimulationEventExecutor? _construction;
        private bool _constructionWorkSeen;
        private bool _travelsStarted;
        private bool _investmentsStarted;
        private int _investmentCount;
        private bool _closed;

        public IReadOnlyList<FoundationEdgeTravelTurnEvent> VerifiedPhysicalEvents => _events;
        public IReadOnlyList<PhysicalTacticalWorkerMovedEvent> VerifiedLocalEvents => _localEvents;
        public bool IsClosed => _closed;
        public int VerifiedInvestmentCount => _investmentCount;

        public ReplayExecutor(
            WorldState beginning, PhysicalTacticalWorkerMoveEventExecutor movement,
            FoundationGroundEdgeTravelTimePolicy movementPolicy,
            PhysicalTacticalIncidenceMap incidence,
            FoundationAuthoritativeEconomicSettlementPolicy policy,
            FoundationFineFirstPhysicalGeography? signedFine,
            FoundationMonetaryInfrastructureEventExecutor? investment,
            FoundationConstructionCollectiveTurnPhase? constructionPhase)
        {
            _beginning = beginning;
            _travel = new FoundationEdgeTravelEventExecutor(movement, movementPolicy);
            _local = movement;
            _incidence = incidence;
            _policy = policy;
            _signedFine = signedFine;
            _investment = investment;
            if (beginning.Constructions.IsInitialized != (constructionPhase is not null))
                throw new InvalidOperationException("Construction replay requires matching opt-in phase.");
            _construction = constructionPhase?.CreateReplayExecutor(movement);
        }

        public WorldState Execute(WorldState state, ISimulationEvent ev,
            SimulationContext context)
        {
            if (_closed)
                throw new InvalidOperationException("Cannot append events after economic close.");
            if (ev is PhysicalTacticalWorkerMovedEvent local)
            {
                if (_travelsStarted || _investmentsStarted ||
                    local.Id.Turn != context.Turn ||
                    local.Source != local.Destination ||
                    local.TraversedEdge.HasValue ||
                    !_orderedWorkers.Add(local.WorkerId))
                    throw new InvalidOperationException(
                        "Temporal local orders require one canonical in-place event per worker.");
                var next = _local.Execute(state, local, context);
                _localEvents.Add(local);
                return next;
            }
            if (ev is FoundationEdgeTravelTurnEvent travel)
            {
                if (_investmentsStarted || travel.Id.Turn != context.Turn ||
                    !_orderedWorkers.Add(travel.WorkerId))
                    throw new InvalidOperationException(
                        "Duplicate physical traveler or mixed economic turns.");
                var next = _travel.Execute(state, travel, context);
                _travelsStarted = true;
                _events.Add(travel);
                return next;
            }
            if (ev is FoundationConstructionFundedEvent || ev is FoundationConstructionWorkBatchEvent)
            {
                if (_construction is null || _constructionWorkSeen)
                    throw new InvalidOperationException("Unexpected construction event or duplicate work batch.");
                if (ev is FoundationConstructionWorkBatchEvent batch)
                {
                    if (batch.Commands.Any(c => !_orderedWorkers.Add(c.WorkerId)))
                        throw new InvalidOperationException("Construction worker has competing orders.");
                    _constructionWorkSeen = true;
                }
                _investmentsStarted = true;
                var next = _construction.Execute(state, ev, context);
                _investmentCount = checked(_investmentCount + 1);
                return next;
            }
            if (ev is FoundationMonetaryInfrastructureInvestedEvent capital)
            {
                if (_investment is null || capital.Id.Turn != context.Turn)
                    throw new InvalidOperationException(
                        "Capital investments require a verified world-bound event executor.");
                _investmentsStarted = true;
                var next = _investment.Execute(state, capital, context);
                _investmentCount = checked(_investmentCount + 1);
                return next;
            }
            if (ev is not FoundationTemporalSalesSettledEvent monetary ||
                monetary.Id.Turn != context.Turn)
                throw new InvalidOperationException(
                    "Only canonical physical movements and one temporal SYSTEM sale are allowed.");
            var (derived, after) = Derive(_beginning, state, _events,
                _localEvents, _investmentCount, _incidence, _policy,
                context, _signedFine);
            RequireIdentical(monetary, derived);
            _closed = true;
            return after;
        }
    }
}
