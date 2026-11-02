using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Proof of a REAL local cash-food purchase is deliberately not generalized
/// to forestry/mining. An absence of evidence is UNKNOWN, not zero sales.
/// </summary>
public enum CanonicalCommodityExecutionEvidenceKind
{
    ExecutedLocalCashFoodWithPaidPrice,
    PhysicalFoodReceiptWithoutPositivePayment,
    NoExecutedLocalFoodReceipt,
    HypotheticalOnlyNoCanonicalExecutedMarket
}

/// <summary>
/// Potential/pressure is a current LIVE workforce-derived projection, not
/// a realized sale. Executed quantities/payments are nullable to prevent
/// inventing a zero market for wood/stone that has never been settled.
/// </summary>
public sealed record CanonicalCommodityExecutionEvidenceRow(
    CommodityId Commodity,
    EconomicActivityKind Activity,
    BigInteger ProjectedPrimitivePotentialRaw,
    BigInteger ProjectedResidentDemandPressureRaw,
    ulong ResidentProducerWorkers,
    BigInteger? ActualLocalPurchasedRaw,
    BigInteger? ActuallyPaidMicrocredits,
    BigInteger? PurchasedFoodRawWithPositivePayment,
    string? ExecutedNominalUnitId,
    CanonicalCommodityExecutionEvidenceKind EvidenceKind)
{
    public bool HasAuthenticatedExecutedCashPrice =>
        EvidenceKind ==
            CanonicalCommodityExecutionEvidenceKind.ExecutedLocalCashFoodWithPaidPrice;
    public bool HasProofOfActualExecutedSales =>
        ActualLocalPurchasedRaw.HasValue &&
        ActualLocalPurchasedRaw.Value > BigInteger.Zero;
    public bool IsExecutedCashEvidenceUnavailable =>
        ActualLocalPurchasedRaw is null;
}

internal sealed record CanonicalCommodityExecutionReconstruction(
    CanonicalCommodityExecutionEvidenceRow[] Rows,
    ulong CompletedEconomicCycleMovementTurn,
    ulong PhysicalWorkers,
    string WorldHashHex);

/// <summary>
/// VS93 multicommodity evidence barrier: primitive food/wood/stone projected
/// flows are NEVER themselves actual market receipts. Only V12 canonical
/// local food cash has authenticated executed payment and unit evidence.
/// No actual trade, pricing or production is created by this audit.
/// </summary>
public sealed class CanonicalPrimitiveCommodityExecutionBoundary
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalExecutedFoodSampleAudit _sample92;
    private readonly CanonicalCommodityExecutionEvidenceRow[] _rows;
    private readonly string _worldHashHex;

    public IReadOnlyList<CanonicalCommodityExecutionEvidenceRow> Commodities { get; }
    public ulong CompletedEconomicCycleMovementTurn { get; }
    public ulong PhysicalResidentWorkers { get; }
    public bool UsesOnlyCanonicallyExecutedFoodCashReceipts => true;
    public bool TreatsUnextractedPotentialAsExecutedQuantity => false;
    public bool UsesHypotheticalVs78PriceAsActualPayment => false;
    public bool EstablishesPaidPricesForWoodOrStone => false;
    public bool MeasuresAllConsumerDemandAsActualSales => false;
    public bool ImplementsAbstractPurchasingPower => false;
    public bool ChangesActualProductionOrWorkerAssignments => false;
    public bool ModifiesCanonicalWorldOrSave => false;
    public bool InventsStocksOrCredit => false;
    public bool CanAuthorizeSettlement => false;

    private CanonicalPrimitiveCommodityExecutionBoundary(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalExecutedFoodSampleAudit sample92,
        CanonicalCommodityExecutionReconstruction computed)
    {
        _session = session;
        _sites = sites;
        _sample92 = sample92;
        _worldHashHex = computed.WorldHashHex;
        CompletedEconomicCycleMovementTurn =
            computed.CompletedEconomicCycleMovementTurn;
        PhysicalResidentWorkers = computed.PhysicalWorkers;
        _rows = computed.Rows.ToArray();
        Commodities = Array.AsReadOnly(_rows);
    }

    private static CanonicalCommodityExecutionReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalExecutedFoodSampleAudit sample92)
    {
        sites.RequireCurrent(session);
        sample92.RequireCurrent(session);
        sample92.VerifyIndependent();
        if (session.State.Cycles.Count == 0 ||
            session.State.Cycles[^1].MovementTurn != session.State.MovementTurn ||
            session.State.MovementTurn != sites.MovementTurn)
            throw new InvalidOperationException(
                "Multicommodity observation requires the same actual completed economic cycle and current workforce sites.");

        var executedFood =
            CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement(session);
        executedFood.RequireCurrent(session);
        executedFood.VerifyIndependent();
        var mapping = sites.CommodityMapping;
        var mapped = new[] {
            (Commodity: mapping.Food, Activity: EconomicActivityKind.Agriculture),
            (Commodity: mapping.Wood, Activity: EconomicActivityKind.Forestry),
            (Commodity: mapping.Stone, Activity: EconomicActivityKind.Mining)
        };
        if (mapped.Select(x => x.Commodity).Distinct().Count() != 3)
            throw new InvalidOperationException(
                "Primitive food, wood and stone require distinct canonical commodity identities.");

        var supplies = sites.Flows.SupplyFlows;
        var demands = sites.Flows.DemandFlows;
        foreach (var flow in supplies)
        {
            if (!mapped.Any(x => x.Commodity == flow.CommodityId &&
                    x.Activity == flow.Activity))
                throw new InvalidOperationException(
                    "Physical worker-based supply has an unrecognized primitive commodity/activity mapping.");
        }

        var physicalWorkers = sites.Sites.Aggregate(0UL,
            (sum, site) => checked(sum + site.ResidentWorkers));
        if (physicalWorkers != sites.ResidentWorkers)
            throw new InvalidOperationException(
                "Economic potential uses a different real worker population.");

        var rows = new List<CanonicalCommodityExecutionEvidenceRow>();
        foreach (var (commodity, activity) in mapped)
        {
            BigInteger potential = BigInteger.Zero;
            foreach (var supply in supplies.Where(x =>
                x.CommodityId == commodity && x.Activity == activity))
                potential += supply.RawQuantity;
            BigInteger demand = BigInteger.Zero;
            foreach (var consumer in demands.Where(x =>
                x.CommodityId == commodity))
                demand += consumer.RawQuantity;
            ulong workers = 0;
            foreach (var site in sites.Sites)
                workers = checked(workers + (activity switch {
                    EconomicActivityKind.Agriculture => site.AgriculturalWorkers,
                    EconomicActivityKind.Forestry => site.ForestryWorkers,
                    EconomicActivityKind.Mining => site.MiningWorkers,
                    _ => 0UL
                }));
            if (activity == EconomicActivityKind.Agriculture)
            {
                var paidFoodRaw = executedFood.ExecutedReceipts.Aggregate(
                    BigInteger.Zero, (total, r) => total +
                        (r.ActuallyPaidMicrocredits > 0UL ? r.ExecutedFoodRaw : 0UL));
                var kind = executedFood.TotalActualLocalFoodRaw == 0
                    ? CanonicalCommodityExecutionEvidenceKind.NoExecutedLocalFoodReceipt
                    : paidFoodRaw == 0
                    ? CanonicalCommodityExecutionEvidenceKind.PhysicalFoodReceiptWithoutPositivePayment
                    : CanonicalCommodityExecutionEvidenceKind.ExecutedLocalCashFoodWithPaidPrice;
                rows.Add(new CanonicalCommodityExecutionEvidenceRow(
                    commodity, activity, potential, demand, workers,
                    executedFood.TotalActualLocalFoodRaw,
                    executedFood.TotalActualPaymentsMicrocredits,
                    paidFoodRaw,
                    kind == CanonicalCommodityExecutionEvidenceKind
                        .ExecutedLocalCashFoodWithPaidPrice
                        ? executedFood.ObservedNominalUnitId : null,
                    kind));
            }
            else
            {
                rows.Add(new CanonicalCommodityExecutionEvidenceRow(
                    commodity, activity, potential, demand, workers,
                    null, null, null, null,
                    CanonicalCommodityExecutionEvidenceKind
                        .HypotheticalOnlyNoCanonicalExecutedMarket));
            }
        }
        executedFood.RequireCurrent(session);
        sites.RequireCurrent(session);
        sample92.RequireCurrent(session);
        return new CanonicalCommodityExecutionReconstruction(
            rows.ToArray(), session.State.MovementTurn,
            physicalWorkers, session.CurrentHash.HexDigest);
    }

    public static CanonicalPrimitiveCommodityExecutionBoundary FromCertifiedEvidence(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery currentPrimitiveSites,
        CanonicalExecutedFoodSampleAudit certifiedSample92)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentPrimitiveSites);
        ArgumentNullException.ThrowIfNull(certifiedSample92);
        var computation = Reconstruct(
            session, currentPrimitiveSites, certifiedSample92);
        var report = new CanonicalPrimitiveCommodityExecutionBoundary(
            session, currentPrimitiveSites, certifiedSample92, computation);
        report.VerifyIndependent();
        return report;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session) ||
            session.CurrentHash.HexDigest != _worldHashHex ||
            session.State.MovementTurn != CompletedEconomicCycleMovementTurn)
            throw new InvalidOperationException(
                "Real execution evidence is foreign to or stale against the canonical physical session.");
        _sites.RequireCurrent(session);
        _sample92.RequireCurrent(session);
    }

    /// <summary>
    /// A VS94 physical batch must reuse the exact canonical site discovery
    /// certified by this witness, not a lookalike policy from the same world.
    /// </summary>
    internal void RequireCertifiedSiteDiscovery(
        CanonicalPrimitiveSiteDiscovery sites)
    {
        if (!ReferenceEquals(_sites, sites))
            throw new InvalidOperationException(
                "VS93 evidence does not certify this specific primitive-site discovery.");
        RequireCurrent(_session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var refreshed = Reconstruct(_session, _sites, _sample92);
        if (!_rows.SequenceEqual(refreshed.Rows) ||
            CompletedEconomicCycleMovementTurn !=
                refreshed.CompletedEconomicCycleMovementTurn ||
            PhysicalResidentWorkers != refreshed.PhysicalWorkers ||
            _worldHashHex != refreshed.WorldHashHex)
            throw new InvalidOperationException(
                "Real cash-food receipts and hypothetical primitive commodity flows failed independent evidence replay.");
        RequireCurrent(_session);
    }
}
