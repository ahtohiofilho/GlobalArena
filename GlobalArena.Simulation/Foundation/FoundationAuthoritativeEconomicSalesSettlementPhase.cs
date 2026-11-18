using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit immutable authoritative settlement parameters. These are the
/// existing demand, physical cargo and post-revenue tax rules, NOT a user
/// supplied list of sales, prices or money credits. No default taxes.
/// </summary>
public sealed class FoundationAuthoritativeEconomicSettlementPolicy
{
    public FoundationActivityCommodityDemandPolicy Demand { get; }
    public FoundationCommodityCargoConversionPolicy Cargo { get; }
    public IReadOnlyList<FoundationPhysicalEconomicTaxRate> TaxRates { get; }
    public int MaxProducerPasses { get; }

    public FoundationAuthoritativeEconomicSettlementPolicy(
        FoundationActivityCommodityDemandPolicy demand,
        FoundationCommodityCargoConversionPolicy cargo,
        IEnumerable<FoundationPhysicalEconomicTaxRate> taxRates,
        int maxProducerPasses = 64)
    {
        Demand = demand ?? throw new ArgumentNullException(nameof(demand));
        Cargo = cargo ?? throw new ArgumentNullException(nameof(cargo));
        ArgumentNullException.ThrowIfNull(taxRates);
        var rates = taxRates.ToArray();
        if (maxProducerPasses < 1 ||
            rates.Length == 0 ||
            rates.Any(r => r is null || !r.Civilization.IsValid ||
                r.GrossRevenueBasisPoints > 10_000U) ||
            rates.Select(r => r.Civilization).Distinct().Count() != rates.Length ||
            !Demand.Commodities.SequenceEqual(Cargo.Commodities))
            throw new ArgumentException(
                "Closed-market policy requires explicit complete demand, " +
                "cargo, valid unique post-revenue taxes, and a positive " +
                "producer resolution budget.");
        MaxProducerPasses = maxProducerPasses;
        TaxRates = Array.AsReadOnly(rates.OrderBy(r =>
            r.Civilization.Value).ToArray());
    }
}

/// <summary>
/// Exactly credited monetary proceeds of one real civilization:
/// seller net + its own post-revenue tax + toll income received from
/// other sellers. Physical freight is an operational cost, not an
/// invented payable resource or buyer wallet.
/// </summary>
public sealed record FoundationCivilizationEconomicSettlementCredit(
    CivilizationId Civilization,
    BigInteger SellerNetScaled,
    BigInteger SellerTaxScaled,
    BigInteger OwnerTollIncomeScaled,
    BigInteger CashCreditScaled);

/// <summary>
/// Immutable SYSTEM-originated journal receipt, never a player command.
/// It cites the exact pre-settlement source hash and the algorithm's
/// independently audited real production / sale / consumption totals.
/// </summary>
public sealed class FoundationEconomicSalesSettledEvent : ISimulationEvent
{
    public EventId Id { get; }
    public WorldStateHash SourceWorldHash { get; }
    public ulong ExpectedWorldRevision { get; }
    public ulong EconomicCycleOrdinal { get; }
    public ulong ProducedGoodsMicro { get; }
    public ulong SoldAndConsumedGoodsMicro { get; }
    public BigInteger GrossSalesScaled { get; }
    public BigInteger PhysicalFreightScaled { get; }
    public BigInteger SellerTaxesScaled { get; }
    public BigInteger ExternalTollsScaled { get; }
    public BigInteger SellerNetScaled { get; }
    public BigInteger CreditedToTreasuriesScaled { get; }
    public string CargoUnitCode { get; }
    public IReadOnlyList<FoundationCivilizationEconomicSettlementCredit> Credits { get; }

    // Server-only, NON-monetary evidence. Never expose this dictionary to
    // clients: a caller must use a participant-filtered read boundary.
    // Only Derive can attach it and only exact independent replay can
    // certify it for publication.
    internal IReadOnlyDictionary<CivilizationId,
        IReadOnlyList<FoundationPhysicalTradeEdgeLoad>>?
        AuditedParticipantEdges { get; private set; }

    internal void AttachAuditedParticipantEdges(
        IReadOnlyDictionary<CivilizationId,
            IReadOnlyList<FoundationPhysicalTradeEdgeLoad>> exact)
    {
        if (AuditedParticipantEdges is not null)
            throw new InvalidOperationException(
                "Verified trade evidence cannot be overwritten.");
        AuditedParticipantEdges = exact ??
            throw new ArgumentNullException(nameof(exact));
    }

    public FoundationEconomicSalesSettledEvent(
        EventId id, WorldStateHash sourceWorldHash,
        ulong expectedWorldRevision, ulong economicCycleOrdinal,
        ulong producedGoodsMicro, ulong soldAndConsumedGoodsMicro,
        BigInteger grossSalesScaled, BigInteger physicalFreightScaled,
        BigInteger sellerTaxesScaled, BigInteger externalTollsScaled,
        BigInteger sellerNetScaled, BigInteger creditedToTreasuriesScaled,
        string cargoUnitCode,
        IEnumerable<FoundationCivilizationEconomicSettlementCredit> credits)
    {
        ArgumentNullException.ThrowIfNull(credits);
        if (id.OriginKind != EventOriginKind.System ||
            id.SystemPhaseId != FoundationAuthoritativeEconomicSalesSettlementPhase.StablePhaseId ||
            id.SystemOrdinal != 1UL || id.Sequence != 1UL ||
            economicCycleOrdinal == 0UL ||
            string.IsNullOrWhiteSpace(cargoUnitCode) ||
            grossSalesScaled.Sign < 0 ||
            physicalFreightScaled.Sign < 0 ||
            sellerTaxesScaled.Sign < 0 ||
            externalTollsScaled.Sign < 0 ||
            sellerNetScaled.Sign < 0 ||
            creditedToTreasuriesScaled.Sign < 0)
            throw new ArgumentException(
                "A closed sale requires an actual system turn, positive " +
                "economic chronology and nonnegative exact-money totals.");
        var rows = credits.ToArray();
        if (rows.Length == 0 ||
            rows.Any(c => c is null || !c.Civilization.IsValid ||
                c.CashCreditScaled.Sign < 0 ||
                c.CashCreditScaled != c.SellerNetScaled +
                    c.SellerTaxScaled + c.OwnerTollIncomeScaled) ||
            rows.Select(c => c.Civilization).Distinct().Count() != rows.Length)
            throw new ArgumentException(
                "Economic receipts require one consistent real account " +
                "per civilization, not invented unbalanced credits.");

        Id = id;
        SourceWorldHash = sourceWorldHash;
        ExpectedWorldRevision = expectedWorldRevision;
        EconomicCycleOrdinal = economicCycleOrdinal;
        ProducedGoodsMicro = producedGoodsMicro;
        SoldAndConsumedGoodsMicro = soldAndConsumedGoodsMicro;
        GrossSalesScaled = grossSalesScaled;
        PhysicalFreightScaled = physicalFreightScaled;
        SellerTaxesScaled = sellerTaxesScaled;
        ExternalTollsScaled = externalTollsScaled;
        SellerNetScaled = sellerNetScaled;
        CreditedToTreasuriesScaled = creditedToTreasuriesScaled;
        CargoUnitCode = cargoUnitCode;
        Credits = Array.AsReadOnly(
            rows.OrderBy(c => c.Civilization.Value).ToArray());
    }
}

/// <summary>
/// Server-authoritative economic kernel, independently replayable from only
/// the trusted WorldState, world generation, complete policy and fixed
/// freight history. No client can propose revenue, prices or sales.
/// </summary>
public sealed class FoundationEconomicSalesSettlementEventExecutor
    : ISimulationEventExecutor
{
    private readonly RuntimeWorldBinding _binding;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly FoundationAuthoritativeEconomicSettlementPolicy _policy;
    private readonly CanonicalWorldStateHasher _hasher = new();

    public FoundationEconomicSalesSettlementEventExecutor(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence,
        FoundationAuthoritativeEconomicSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(world);
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (incidence.Refinement.CoarseParameters !=
            world.Request.StrategicParameters ||
            incidence.CoarseTopology.Cells.Count !=
                world.StrategicTopology.Cells.Count)
            throw new InvalidOperationException(
                "Economic close must use the actual signed physical world.");
        _binding = RuntimeWorldBinding.FromGeneratedWorld(world);
    }

    private sealed record Expected(
        FoundationEconomicSalesSettledEvent Event,
        IReadOnlyDictionary<CivilizationId, BigInteger> Credits,
        CivilizationSettledFreightQuote[] NextFreight);

    private Expected Derive(WorldState snapshot, SimulationContext context)
    {
        if (snapshot.WorldBinding != _binding ||
            !snapshot.Treasury.IsInitialized ||
            !snapshot.Transport.HasMonetaryConstructionCosts ||
            !snapshot.Workers.HasAuthoritativeOccupations)
            throw new InvalidOperationException(
                "Sales cannot be closed from a preview, unbound world or " +
                "an unfunded, missing authoritative civilization treasury.");

        var history = snapshot.Treasury;
        ulong ordinal = checked(history.LastSettledEconomicCycleOrdinal + 1UL);
        FoundationPhysicalLaggedFreightQuoteState? previous = null;
        if (history.HasClosedEconomicCycle)
        {
            if (history.LastSettledCargoUnitCode != _policy.Cargo.CargoUnitCode)
                throw new InvalidOperationException(
                    "Historical freight requires the same real cargo unit.");
            previous = new FoundationPhysicalLaggedFreightQuoteState(
                history.LastSettledEconomicCycleOrdinal,
                history.LastSettledFreightQuotes.Select(q =>
                    new FoundationPhysicalLaggedFreightEdgeState(
                        q.Edge, q.FreePhysicalCostMicroPerWholeCargoUnit,
                        q.ReferenceCargoMicro,
                        q.QuotedCongestionSurchargeMicroPerWholeCargoUnit))
                    .ToArray(), hasSettledProvenance: true);
        }
        var plan = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(
                snapshot, _incidence, _policy.Demand, _policy.Cargo,
                previous, _policy.MaxProducerPasses);
        var audit = plan.Distribution.AllocationAudit;
        if (audit.TotalAvailableGoodsMicro != audit.TotalAllocatedGoodsMicro ||
            plan.RepricedWithinCycle ||
            plan.NextCycleQuotes.HasSettledProvenance ||
            plan.NextCycleQuotes.AuditedPreviewCycles != ordinal ||
            audit.Freight.CargoUnitCode != _policy.Cargo.CargoUnitCode)
            throw new InvalidOperationException(
                "Only a complete, fixed-freight, independently audited " +
                "real sale-and-consumption cycle can issue cash.");

        var accounts = FoundationPhysicalEconomicAccountingReconciler
            .EvaluateCandidate(
                snapshot.Workers, _incidence, _policy.Demand, _policy.Cargo,
                plan, _policy.TaxRates);
        var civilizations = snapshot.Civilizations.Civilizations;
        if (accounts.Accounts.Count != civilizations.Count ||
            !accounts.Accounts.Select(a => a.Civilization)
                .SequenceEqual(civilizations))
            throw new InvalidOperationException(
                "Closed sales cannot mint money for an unknown civilization.");
        var credits = accounts.Accounts.Select(a =>
            new FoundationCivilizationEconomicSettlementCredit(
                a.Civilization,
                a.SellerNetScaled, a.RevenueTaxScaled,
                a.TollIncomeScaled,
                a.SellerNetScaled + a.RevenueTaxScaled +
                    a.TollIncomeScaled)).ToArray();
        BigInteger totalCredit = credits.Aggregate(BigInteger.Zero,
            (sum, c) => sum + c.CashCreditScaled);
        if (totalCredit + accounts.PhysicalFreightScaled !=
                accounts.GrossSalesScaled ||
            accounts.ExternalTollsScaled != credits.Aggregate(
                BigInteger.Zero, (sum, c) => sum + c.OwnerTollIncomeScaled) ||
            accounts.GrossSalesScaled != accounts.PhysicalFreightScaled +
                accounts.ExternalTollsScaled + accounts.RevenueTaxScaled +
                accounts.NetSellerReceiptsScaled)
            throw new InvalidOperationException(
                "Authoritative monetary reconciliation is not exact: " +
                "sales must equal physical freight plus all actual credits.");

        var next = plan.NextCycleQuotes.Edges.Select(q =>
            new CivilizationSettledFreightQuote(
                q.Edge, q.FreePhysicalCostMicroPerWholeCargoUnit,
                q.ReferenceCargoMicro,
                q.QuotedCongestionSurchargeMicroPerWholeCargoUnit)).ToArray();
        if (next.Length != snapshot.Transport.Edges.Count)
            throw new InvalidOperationException(
                "Market settlement omitted authoritative freight crossings.");

        var ev = new FoundationEconomicSalesSettledEvent(
            EventId.ForSystemPhase(context.Turn,
                FoundationAuthoritativeEconomicSalesSettlementPhase.StablePhaseId,
                1UL), _hasher.Compute(snapshot), snapshot.Revision,
            ordinal, audit.TotalAvailableGoodsMicro,
            audit.TotalAllocatedGoodsMicro, accounts.GrossSalesScaled,
            accounts.PhysicalFreightScaled, accounts.RevenueTaxScaled,
            accounts.ExternalTollsScaled, accounts.NetSellerReceiptsScaled,
            totalCredit, _policy.Cargo.CargoUnitCode, credits);
        ev.AttachAuditedParticipantEdges(
            FoundationSettledTradeParticipantProjection.FromAuditedPlan(
                snapshot, _incidence, _policy.Cargo, plan));
        return new Expected(ev, credits.ToDictionary(c => c.Civilization,
            c => c.CashCreditScaled), next);
    }

    private static void RequireExact(
        FoundationEconomicSalesSettledEvent received,
        FoundationEconomicSalesSettledEvent derived)
    {
        if (received.Id != derived.Id ||
            received.SourceWorldHash != derived.SourceWorldHash ||
            received.ExpectedWorldRevision != derived.ExpectedWorldRevision ||
            received.EconomicCycleOrdinal != derived.EconomicCycleOrdinal ||
            received.ProducedGoodsMicro != derived.ProducedGoodsMicro ||
            received.SoldAndConsumedGoodsMicro != derived.SoldAndConsumedGoodsMicro ||
            received.GrossSalesScaled != derived.GrossSalesScaled ||
            received.PhysicalFreightScaled != derived.PhysicalFreightScaled ||
            received.SellerTaxesScaled != derived.SellerTaxesScaled ||
            received.ExternalTollsScaled != derived.ExternalTollsScaled ||
            received.SellerNetScaled != derived.SellerNetScaled ||
            received.CreditedToTreasuriesScaled !=
                derived.CreditedToTreasuriesScaled ||
            received.CargoUnitCode != derived.CargoUnitCode ||
            !received.Credits.SequenceEqual(derived.Credits) ||
            !FoundationSettledTradeParticipantProjection.Identical(
                received.AuditedParticipantEdges,
                derived.AuditedParticipantEdges))
            throw new InvalidOperationException(
                "Forged, stale or duplicated sale / cash / tax / toll " +
                "journal event; only the recomputed physical market may settle.");
    }

    private static WorldState Apply(WorldState snapshot, Expected expected) =>
        snapshot.RecordAuditedEconomicCycle(
            expected.Event.EconomicCycleOrdinal,
            expected.Event.CargoUnitCode, expected.Credits,
            expected.NextFreight).AdvanceRevision();

    /// <summary>
    /// The resolution phase solves once; independent replay deliberately
    /// solves AGAIN from the original snapshot to reject forged receipts.
    /// </summary>
    internal (FoundationEconomicSalesSettledEvent Event, WorldState Next)
        CloseActualEconomicCycle(
            WorldState state, SimulationContext context)
    {
        var expected = Derive(state, context);
        return (expected.Event, Apply(state, expected));
    }

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (simulationEvent is not FoundationEconomicSalesSettledEvent ev ||
            ev.Id.Turn != context.Turn ||
            ev.ExpectedWorldRevision != state.Revision ||
            ev.SourceWorldHash != _hasher.Compute(state))
            throw new InvalidOperationException(
                "Economic journal event did not match its original " +
                "real WorldState and closed turn.");
        var actual = Derive(state, context);
        RequireExact(ev, actual.Event);
        return Apply(state, actual);
    }
}

/// <summary>
/// A system phase, with NO player sale command and NO buyer wallet.
/// It always closes exactly one audited economic cycle in each explicitly
/// enabled all-ready turn, AFTER investment orders of that turn.
/// </summary>
public sealed class FoundationAuthoritativeEconomicSalesSettlementPhase
    : ITurnResolutionPhase
{
    public const string StablePhaseId =
        "foundation.verified-sales-consumption-treasury-v15";

    private readonly FoundationEconomicSalesSettlementEventExecutor _executor;
    public string PhaseId => StablePhaseId;

    public FoundationAuthoritativeEconomicSalesSettlementPhase(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence,
        FoundationAuthoritativeEconomicSettlementPolicy policy)
    {
        _executor = new FoundationEconomicSalesSettlementEventExecutor(
            world, incidence, policy);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput input, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stagedState);
        var (ev, next) = _executor.CloseActualEconomicCycle(
            stagedState, input.Context);
        return new TurnResolutionResult(next, new ISimulationEvent[] { ev },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(1UL, ev,
                    wasEligible: true, wasExecuted: true)
            }));
    }
}
