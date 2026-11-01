using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Macroscopic experimental rates for one already staffed primitive activity.
/// One policy applies to all productive sites; no per-worker microcommands
/// or private money. Permille is a physical fraction, NOT a price/credit.
/// </summary>
public sealed record CanonicalWorkforceActivityRealizationPolicy(
    EconomicActivityKind Activity,
    int UtilizationPermille,
    int RetentionPermille);

public sealed record CanonicalWorkforceRealizationSource(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong ActualActivityWorkers,
    ulong PotentialRaw, int UtilizationPermille,
    int RetentionPermille, ulong DerivedProducedRaw,
    ulong DeliberatelyRetainedRaw, ulong PotentialNotExtractedRaw);

internal sealed record CanonicalWorkforceRealizationComputation(
    CanonicalWorkforceRealizationSource[] Rows,
    CanonicalRealizedProductionAllocation? Allocation);

/// <summary>
/// VS83: use only ACTUAL assigned workers and their current VS62 economic
/// physical capacity. A shared activity-level utilization hypothesis
/// yields the explicit quantities consumed by the validated VS82 audit.
/// Never creates a unit, production event, inventory, credit or SETTLE.
/// </summary>
public sealed class CanonicalWorkforceRealizationProjection
{
    public const int Permille = 1000;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalSimultaneousFreightReference _freightReference;
    private readonly CanonicalWorkforceActivityRealizationPolicy[] _policies;
    private readonly CanonicalSimultaneousFreightLot[] _remoteLots;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;

    public IReadOnlyList<CanonicalWorkforceActivityRealizationPolicy> Policies { get; }
    public IReadOnlyList<CanonicalWorkforceRealizationSource> Sources { get; }
    public CanonicalRealizedProductionAllocation? Allocation { get; }
    public bool CreatesWorkersOrCities => false;
    public bool UsesPhysicalResidentWorkers => true;
    public bool AllocatesWorkPerIndividualWorker => false;
    public bool UsesArtificialBuyerBudget => false;
    public bool CreatesInvoluntaryStock => false;
    public bool CapsTradeByBuyerDemand => false;
    public bool ExecutesActualProductionOrSale => false;
    public bool ComputesMoneyProfit => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalWorkforceRealizationProjection(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freightReference,
        CanonicalWorkforceActivityRealizationPolicy[] policies,
        CanonicalSimultaneousFreightLot[] remoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalWorkforceRealizationComputation computed)
    {
        _session = session;
        _sites = sites;
        _freightReference = freightReference;
        _policies = (CanonicalWorkforceActivityRealizationPolicy[])policies.Clone();
        _remoteLots = (CanonicalSimultaneousFreightLot[])remoteLots.Clone();
        _congestion = congestion;
        Policies = Array.AsReadOnly(_policies);
        Sources = Array.AsReadOnly(computed.Rows);
        Allocation = computed.Allocation;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Workforce realization belongs to another physical session.");
        _sites.RequireCurrent(session);
        _freightReference.RequireCurrent(session);
        Allocation?.RequireCurrent(session);
    }

    private static ulong ActivityWorkers(
        CanonicalPrimitiveEconomicSite site,
        EconomicActivityKind activity) => activity switch
    {
        EconomicActivityKind.Agriculture => site.AgriculturalWorkers,
        EconomicActivityKind.Forestry => site.ForestryWorkers,
        EconomicActivityKind.Mining => site.MiningWorkers,
        _ => throw new ArgumentException(
            "Only primitive productive activities have a certified worker population.",
            nameof(activity))
    };

    private static CanonicalWorkforceRealizationComputation Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freightReference,
        CanonicalWorkforceActivityRealizationPolicy[] policies,
        CanonicalSimultaneousFreightLot[] remoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        sites.RequireCurrent(session);
        freightReference.RequireCurrent(session);
        var activityPolicies = policies.ToDictionary(x => x.Activity);
        var produced = new List<CanonicalRealizedProductionDeclaration>();
        var rows = new List<CanonicalWorkforceRealizationSource>();
        var keyset = new HashSet<
            (EconomicPointId, CommodityId, EconomicActivityKind)>();
        foreach (var flow in sites.Flows.SupplyFlows.OrderBy(x => x.EconomicPointId.Value)
            .ThenBy(x => x.CommodityId.Value).ThenBy(x => x.Activity))
        {
            if (flow.RawQuantity == 0)
                continue;
            if (!activityPolicies.TryGetValue(flow.Activity, out var policy))
                throw new ArgumentException(
                    "A productive activity cannot be omitted from the macroscopic utilization policy.",
                    nameof(policies));
            var key = (flow.EconomicPointId, flow.CommodityId, flow.Activity);
            var site = sites.At(flow.EconomicPointId);
            if (!keyset.Add(key) || site is null ||
                site.ResidentWorkers == 0 || ActivityWorkers(site, flow.Activity) == 0)
                throw new InvalidOperationException(
                    "Physical supply must be backed by unique real resident workers assigned to the exact productive activity.");
            var workers = ActivityWorkers(site, flow.Activity);
            var raw = (BigInteger)flow.RawQuantity *
                policy.UtilizationPermille / Permille;
            var derived = checked((ulong)raw);
            var keep = checked((ulong)(
                (BigInteger)derived * policy.RetentionPermille / Permille));
            rows.Add(new(flow.EconomicPointId, flow.CommodityId, flow.Activity,
                workers, flow.RawQuantity,
                policy.UtilizationPermille, policy.RetentionPermille,
                derived, keep, flow.RawQuantity - derived));
            if (derived > 0)
                produced.Add(new(flow.EconomicPointId, flow.CommodityId,
                    flow.Activity, derived, keep));
        }
        if (rows.Count > CanonicalRealizedProductionAllocation.MaximumDeclarations)
            throw new InvalidOperationException(
                "VS83 reference is limited to 12 authenticated physical source pools; never silently truncate the world.");
        if (activityPolicies.Keys.Except(rows.Select(x => x.Activity)).Any())
            throw new ArgumentException(
                "Macroscopic realization policy must not invent an unstaffed productive activity.",
                nameof(policies));
        CanonicalRealizedProductionAllocation? allocation = null;
        if (produced.Count > 0)
        {
            allocation = CanonicalRealizedProductionAllocation.FromSites(
                session, sites, freightReference, produced, remoteLots, congestion);
        }
        else if (remoteLots.Length != 0)
        {
            throw new ArgumentException(
                "Zero realized physical production cannot ship remote freight.",
                nameof(remoteLots));
        }
        sites.RequireCurrent(session);
        freightReference.RequireCurrent(session);
        return new(rows.ToArray(), allocation);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Reconstruct(_session, _sites, _freightReference,
            _policies, _remoteLots, _congestion);
        if (!Sources.SequenceEqual(replay.Rows) ||
            (Allocation is null) != (replay.Allocation is null) ||
            (Allocation is not null &&
                (!Allocation.Sources.SequenceEqual(replay.Allocation!.Sources) ||
                 !Allocation.LocalMarkets.SequenceEqual(replay.Allocation.LocalMarkets) ||
                 Allocation.AuthenticatedNominalFreightCost.CompareTo(
                     replay.Allocation.AuthenticatedNominalFreightCost) != 0)))
            throw new InvalidOperationException(
                "Independent real-worker-derived production, local market or physical freight quantities diverged.");
        Allocation?.VerifyIndependent();
        RequireCurrent(_session);
    }

    public static CanonicalWorkforceRealizationProjection FromSites(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freightReference,
        IEnumerable<CanonicalWorkforceActivityRealizationPolicy> policies,
        IEnumerable<CanonicalSimultaneousFreightLot> remoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(freightReference);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(remoteLots);
        ArgumentNullException.ThrowIfNull(congestion);
        var rates = policies.ToArray();
        var lots = remoteLots.ToArray();
        if (rates.Length is < 1 or > 3 ||
            rates.Any(x => x is null || x.Activity is not (
                    EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
                x.UtilizationPermille is < 0 or > Permille ||
                x.RetentionPermille is < 0 or > Permille) ||
            rates.Select(x => x.Activity).Distinct().Count() != rates.Length ||
            lots.Length > CanonicalSimultaneousFreightReference.MaximumLots ||
            lots.Any(x => x is null || x.Offer is null || x.RawQuantity == 0))
            throw new ArgumentException(
                "VS83 requires unique primitive activity policies with physical utilization and retention between 0 and 1000; no forged or zero remote lots.");
        var sorted = rates.OrderBy(x => x.Activity).ToArray();
        var orderedLots = lots.OrderBy(x => x.Offer.Commodity.Value)
            .ThenBy(x => x.Offer.Activity)
            .ThenBy(x => x.Offer.Source.Value)
            .ThenBy(x => x.Offer.Buyer.Value)
            .ThenBy(x => x.Offer.ServiceId)
            .ThenBy(x => x.Offer.TechnologyTypeId)
            .ThenBy(x => x.Offer.TechnologyCatalogVersion).ToArray();
        var computed = Reconstruct(session, sites, freightReference,
            sorted, orderedLots, congestion);
        var reference = new CanonicalWorkforceRealizationProjection(
            session, sites, freightReference, sorted, orderedLots,
            congestion, computed);
        reference.VerifyIndependent();
        return reference;
    }
}
