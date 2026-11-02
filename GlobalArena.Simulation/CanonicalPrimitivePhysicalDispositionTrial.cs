using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One global, non-worker-specific experiment per primitive activity.
/// It is a physical fraction of VS94 FEASIBLE quantity, not a wallet,
/// price, demand cap, extraction order, or household transaction.
/// </summary>
public sealed record CanonicalPrimitiveLocalPhysicalUsePolicy(
    EconomicActivityKind Activity, int LocalUsePermille);

public sealed record CanonicalPrimitivePhysicalDispositionSource(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity,
    ulong PotentialRaw, ulong FeasibleRaw,
    ulong ProposedLocalUseRaw, BigInteger ProposedRemoteDeliveryRaw,
    ulong RemainingFeasibleNotExtractedRaw,
    ulong InfeasibleNotExtractedRaw, ulong TotalNotExtractedRaw);

public sealed record CanonicalPrimitivePhysicalDispositionCommodity(
    CommodityId Commodity, EconomicActivityKind Activity,
    BigInteger TotalPotentialRaw, BigInteger TotalFeasibleRaw,
    BigInteger ProposedLocalUseRaw, BigInteger ProposedRemoteDeliveryRaw,
    BigInteger TotalNotExtractedRaw, BigInteger ProjectedDemandPressureRaw,
    BigInteger? IndependentlyExecutedLocalFoodPurchaseRaw,
    BigInteger? IndependentlyExecutedFoodPaymentMicrocredits);

public sealed record CanonicalPrimitivePhysicalDispositionBuyer(
    EconomicPointId Buyer, CommodityId Commodity,
    BigInteger ProposedRemoteArrivalRaw, BigInteger ProjectedDemandPressureRaw);

internal sealed record CanonicalPrimitivePhysicalDispositionComputation(
    CanonicalPrimitivePhysicalDispositionSource[] Sources,
    CanonicalPrimitivePhysicalDispositionCommodity[] Commodities,
    CanonicalPrimitivePhysicalDispositionBuyer[] Buyers,
    CanonicalSimultaneousFreightAudit? FreightAudit);

/// <summary>
/// VS95: one read-only local/remote use hypothesis for each canonical source.
/// The *only* planned extraction is local use + authenticated remote lots.
/// All unassigned physical capacity remains UNEXTRACTED, never stock.
/// VS81 verifies real route/service rights; VS94 bounds each source quantity.
/// No actual production, sale, buyer affordability or financial settlement.
/// </summary>
public sealed class CanonicalPrimitivePhysicalDispositionTrial
{
    public const int PermilleScale = 1000;

    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitivePhysicalMulticommodityBatch _physical94;
    private readonly CanonicalSimultaneousFreightReference _freight;
    private readonly CanonicalPrimitiveLocalPhysicalUsePolicy[] _policies;
    private readonly CanonicalSimultaneousFreightLot[] _lots;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;
    private readonly CanonicalPrimitivePhysicalDispositionSource[] _sources;
    private readonly CanonicalPrimitivePhysicalDispositionCommodity[] _commodities;
    private readonly CanonicalPrimitivePhysicalDispositionBuyer[] _buyers;

    public IReadOnlyList<CanonicalPrimitiveLocalPhysicalUsePolicy> LocalPolicies { get; }
    public IReadOnlyList<CanonicalSimultaneousFreightLot> ProposedRemoteLots { get; }
    public IReadOnlyList<CanonicalPrimitivePhysicalDispositionSource> Sources { get; }
    public IReadOnlyList<CanonicalPrimitivePhysicalDispositionCommodity> Commodities { get; }
    public IReadOnlyList<CanonicalPrimitivePhysicalDispositionBuyer> Buyers { get; }
    public CanonicalSimultaneousFreightAudit? InstitutionalFreightPreflight { get; }
    public ulong CompletedEconomicCycleMovementTurn =>
        _physical94.CompletedEconomicCycleMovementTurn;

    public bool UsesOnlyLiveWorkforceCapacity => true;
    public bool VerifiesInstitutionalFreightRights => true;
    public bool AllowsPlannedPhysicalUseAboveDemandPressure => true;
    public bool RecordsUnallocatedMaterialsAsInventory => false;
    public bool CreatesActualExtractionOrDelivery => false;
    public bool ChargesActualFreightPayment => false;
    public bool ComputesWoodOrStoneNominalPrices => false;
    public bool ImplementsAbstractPurchasingPower => false;
    public bool CanAuthorizeSettlement => false;

    private CanonicalPrimitivePhysicalDispositionTrial(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitivePhysicalMulticommodityBatch physical94,
        CanonicalSimultaneousFreightReference freight,
        CanonicalPrimitiveLocalPhysicalUsePolicy[] policies,
        CanonicalSimultaneousFreightLot[] lots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalPrimitivePhysicalDispositionComputation computed)
    {
        _session = session;
        _sites = sites;
        _physical94 = physical94;
        _freight = freight;
        _policies = (CanonicalPrimitiveLocalPhysicalUsePolicy[])policies.Clone();
        _lots = (CanonicalSimultaneousFreightLot[])lots.Clone();
        _congestion = congestion;
        _sources = computed.Sources;
        _commodities = computed.Commodities;
        _buyers = computed.Buyers;
        LocalPolicies = Array.AsReadOnly(_policies);
        ProposedRemoteLots = Array.AsReadOnly(_lots);
        Sources = Array.AsReadOnly(_sources);
        Commodities = Array.AsReadOnly(_commodities);
        Buyers = Array.AsReadOnly(_buyers);
        InstitutionalFreightPreflight = computed.FreightAudit;
    }

    internal void RequireSameSites(CanonicalPrimitiveSiteDiscovery sites)
    {
        if (!ReferenceEquals(_sites, sites))
            throw new InvalidOperationException("VS95 trial requires its original live site discovery.");
        RequireCurrent(_session);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "VS95 physical trial is foreign to the live tactical session.");
        _sites.RequireCurrent(session);
        _physical94.RequireCurrent(session);
        _freight.RequireCurrent(session);
        InstitutionalFreightPreflight?.RequireCurrent(session);
    }

    private static CanonicalPrimitivePhysicalDispositionComputation Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitivePhysicalMulticommodityBatch physical94,
        CanonicalSimultaneousFreightReference freight,
        CanonicalPrimitiveLocalPhysicalUsePolicy[] policies,
        CanonicalSimultaneousFreightLot[] lots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        sites.RequireCurrent(session);
        physical94.RequireCurrent(session);
        freight.RequireCurrent(session);
        physical94.VerifyIndependent();
        var mapping = sites.CommodityMapping;
        var expected = new Dictionary<EconomicActivityKind, CommodityId> {
            [EconomicActivityKind.Agriculture] = mapping.Food,
            [EconomicActivityKind.Forestry] = mapping.Wood,
            [EconomicActivityKind.Mining] = mapping.Stone
        };
        var policyByActivity = policies.ToDictionary(x => x.Activity, x => x.LocalUsePermille);
        var supply = sites.Flows.SupplyFlows.ToDictionary(
            x => (Source: x.EconomicPointId, Commodity: x.CommodityId, x.Activity),
            x => x.RawQuantity);
        var demand = sites.Flows.DemandFlows.ToDictionary(
            x => (Buyer: x.EconomicPointId, Commodity: x.CommodityId),
            x => x.RawQuantity);
        var physical = physical94.Sources.ToDictionary(
            x => (x.Source, x.Commodity, x.Activity));

        // Reconciliation against the policy and quantities authenticated in
        // VS94/VS93, even if the caller supplies a different current discovery.
        if (physical94.Sources.Count != checked(sites.Sites.Count * 3) ||
            physical94.Commodities.Count != 3 ||
            physical94.Commodities.Any(x =>
                !expected.TryGetValue(x.Activity, out var id) ||
                x.Commodity != id ||
                x.ProjectedDemandPressureRaw != sites.Flows.DemandFlows
                    .Where(d => d.CommodityId == x.Commodity)
                    .Aggregate(BigInteger.Zero, (sum, d) => sum + d.RawQuantity)) ||
            physical.Count != physical94.Sources.Count ||
            supply.Count != physical94.Sources.Count(x => x.PotentialRaw > 0))
            throw new InvalidOperationException(
                "VS95 cannot combine VS94 potential with a different current worker/economic-site policy.");

        foreach (var row in physical94.Sources)
        {
            var matching = sites.At(row.Source);
            supply.TryGetValue((row.Source, row.Commodity, row.Activity), out var capacity);
            var workers = matching is null ? 0UL : row.Activity switch {
                EconomicActivityKind.Agriculture => matching.AgriculturalWorkers,
                EconomicActivityKind.Forestry => matching.ForestryWorkers,
                EconomicActivityKind.Mining => matching.MiningWorkers,
                _ => 0UL
            };
            if (matching is null || expected[row.Activity] != row.Commodity ||
                matching.ResidentWorkers != row.ResidentWorkers ||
                row.PotentialRaw != capacity ||
                workers != row.AssignedActivityWorkers ||
                !demand.ContainsKey((row.Source, row.Commodity)))
                throw new InvalidOperationException(
                    "VS95 physical capacity and workers must match the same current inhabited source.");
        }
        // The VS81 reference has already audited actual service/route rights.
        // Independently cross-check its offer against THIS exact site policy.
        foreach (var o in freight.Offers)
        {
            if (!physical.TryGetValue((o.Source, o.Commodity, o.Activity),
                    out var source) ||
                o.SourcePotentialRaw != source.PotentialRaw ||
                !demand.TryGetValue((o.Buyer, o.Commodity), out var pressure) ||
                pressure != o.BuyerPriceDemandSignalRaw)
                throw new InvalidOperationException(
                    "VS81 freight service belongs to a different source or buyer snapshot.");
        }

        var proposedRemote = new Dictionary<
            (EconomicPointId Source, CommodityId Commodity,
             EconomicActivityKind Activity), BigInteger>();
        var buyerRemote = new Dictionary<
            (EconomicPointId Buyer, CommodityId Commodity), BigInteger>();
        foreach (var lot in lots)
        {
            var key = (lot.Offer.Source, lot.Offer.Commodity, lot.Offer.Activity);
            var buyer = (lot.Offer.Buyer, lot.Offer.Commodity);
            if (!physical.ContainsKey(key) || !demand.ContainsKey(buyer))
                throw new ArgumentException(
                    "Remote lot must use a certified source and an inhabited real buyer.",
                    nameof(lots));
            proposedRemote.TryGetValue(key, out var previous);
            proposedRemote[key] = previous + lot.RawQuantity;
            buyerRemote.TryGetValue(buyer, out var already);
            buyerRemote[buyer] = already + lot.RawQuantity;
        }

        var sourceRows = new List<CanonicalPrimitivePhysicalDispositionSource>();
        foreach (var row in physical94.Sources)
        {
            var key = (row.Source, row.Commodity, row.Activity);
            var local = checked((ulong)((BigInteger)row.ExperimentallyFeasibleRaw *
                policyByActivity[row.Activity] / PermilleScale));
            proposedRemote.TryGetValue(key, out var remote);
            var selected = (BigInteger)local + remote;
            if (selected > row.ExperimentallyFeasibleRaw)
                throw new ArgumentException(
                    "Local and remote proposals compete for ONE physically feasible source pool; no double allocation.",
                    nameof(lots));
            var feasibleRemainder = checked((ulong)(
                row.ExperimentallyFeasibleRaw - selected));
            var notExtracted = checked((ulong)(
                (BigInteger)row.PotentialRaw - selected));
            sourceRows.Add(new(row.Source, row.Commodity, row.Activity,
                row.PotentialRaw, row.ExperimentallyFeasibleRaw, local, remote,
                feasibleRemainder, row.CapacityNotExtractedRaw, notExtracted));
        }

        // VS81 is called after narrower VS94 physical conservation,
        // authenticates distinct offers and shared corridor cost/rights.
        CanonicalSimultaneousFreightAudit? audit = lots.Length == 0 ? null :
            freight.Audit(lots, congestion);
        audit?.VerifyIndependent();

        var commodityRows = physical94.Commodities.Select(c =>
        {
            var rows = sourceRows.Where(x => x.Commodity == c.Commodity).ToArray();
            var local = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ProposedLocalUseRaw);
            var remote = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ProposedRemoteDeliveryRaw);
            var notExtracted = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.TotalNotExtractedRaw);
            if (local + remote + notExtracted != c.PotentialRaw)
                throw new InvalidOperationException(
                    "Global commodity conservation did not reconcile back to VS94 potential.");
            return new CanonicalPrimitivePhysicalDispositionCommodity(
                c.Commodity, c.Activity, c.PotentialRaw,
                c.ExperimentallyFeasibleRaw, local, remote, notExtracted,
                c.ProjectedDemandPressureRaw,
                c.IndependentlyExecutedLocalPurchaseRaw,
                c.IndependentlyPaidMicrocredits);
        }).OrderBy(x => x.Commodity.Value).ToArray();

        var buyerRows = buyerRemote.OrderBy(x => x.Key.Buyer.Value)
            .ThenBy(x => x.Key.Commodity.Value)
            .Select(x => new CanonicalPrimitivePhysicalDispositionBuyer(
                x.Key.Buyer, x.Key.Commodity, x.Value, demand[x.Key]))
            .ToArray();
        if (buyerRows.Aggregate(BigInteger.Zero,
                (sum, b) => sum + b.ProposedRemoteArrivalRaw) !=
            commodityRows.Aggregate(BigInteger.Zero,
                (sum, c) => sum + c.ProposedRemoteDeliveryRaw))
            throw new InvalidOperationException(
                "Outbound quantity must equal hypothetical inbound quantity for every declared lot.");
        sites.RequireCurrent(session);
        physical94.RequireCurrent(session);
        freight.RequireCurrent(session);
        return new(sourceRows.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value).ThenBy(x => x.Activity).ToArray(),
            commodityRows, buyerRows, audit);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Reconstruct(_session, _sites, _physical94,
            _freight, _policies, _lots, _congestion);
        if (!_sources.SequenceEqual(rebuilt.Sources) ||
            !_commodities.SequenceEqual(rebuilt.Commodities) ||
            !_buyers.SequenceEqual(rebuilt.Buyers) ||
            (InstitutionalFreightPreflight is null) != (rebuilt.FreightAudit is null) ||
            (InstitutionalFreightPreflight is not null &&
                (InstitutionalFreightPreflight
                    .TotalAuthenticatedNominalFreightCost.CompareTo(
                        rebuilt.FreightAudit!.TotalAuthenticatedNominalFreightCost) != 0 ||
                 !InstitutionalFreightPreflight.Sources.SequenceEqual(
                     rebuilt.FreightAudit.Sources) ||
                 InstitutionalFreightPreflight.CorridorLoads.Count !=
                     rebuilt.FreightAudit.CorridorLoads.Count ||
                 InstitutionalFreightPreflight.CorridorLoads.Any(x =>
                     !rebuilt.FreightAudit.CorridorLoads.TryGetValue(
                         x.Key, out var q) || x.Value != q))))
            throw new InvalidOperationException(
                "VS95 independent local/remote conservation or authenticated freight replay diverged.");
        InstitutionalFreightPreflight?.VerifyIndependent();
        RequireCurrent(_session);
    }

    public static CanonicalPrimitivePhysicalDispositionTrial FromCertifiedPlans(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery currentSites,
        CanonicalPrimitivePhysicalMulticommodityBatch physical94,
        CanonicalSimultaneousFreightReference currentFreight,
        IEnumerable<CanonicalPrimitiveLocalPhysicalUsePolicy> localPolicies,
        IEnumerable<CanonicalSimultaneousFreightLot> proposedRemoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentSites);
        ArgumentNullException.ThrowIfNull(physical94);
        ArgumentNullException.ThrowIfNull(currentFreight);
        ArgumentNullException.ThrowIfNull(localPolicies);
        ArgumentNullException.ThrowIfNull(proposedRemoteLots);
        ArgumentNullException.ThrowIfNull(congestion);
        var policies = localPolicies.ToArray();
        var lots = proposedRemoteLots.ToArray();
        if (policies.Length != 3 ||
            policies.Any(x => x is null ||
                x.Activity is not (
                    EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or
                    EconomicActivityKind.Mining) ||
                x.LocalUsePermille is < 0 or > PermilleScale) ||
            policies.Select(x => x.Activity).Distinct().Count() != 3 ||
            lots.Length > CanonicalSimultaneousFreightReference.MaximumLots ||
            lots.Any(x => x is null || x.Offer is null || x.RawQuantity == 0UL))
            throw new ArgumentException(
                "VS95 requires 3 unique primitive activity policies and at most 12 positive authenticated freight lots.");

        var orderedPolicies = policies.OrderBy(x => x.Activity).ToArray();
        var orderedLots = lots.OrderBy(x => x.Offer.Commodity.Value)
            .ThenBy(x => x.Offer.Activity)
            .ThenBy(x => x.Offer.Source.Value)
            .ThenBy(x => x.Offer.Buyer.Value)
            .ThenBy(x => x.Offer.ServiceId)
            .ThenBy(x => x.Offer.TechnologyTypeId)
            .ThenBy(x => x.Offer.TechnologyCatalogVersion).ToArray();
        var computed = Reconstruct(session, currentSites, physical94,
            currentFreight, orderedPolicies, orderedLots, congestion);
        var report = new CanonicalPrimitivePhysicalDispositionTrial(
            session, currentSites, physical94, currentFreight,
            orderedPolicies, orderedLots, congestion, computed);
        report.VerifyIndependent();
        return report;
    }
}
