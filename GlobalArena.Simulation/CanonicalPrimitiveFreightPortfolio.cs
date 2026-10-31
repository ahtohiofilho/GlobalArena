using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One independently audited VS63 commodity/activity/technology query.
/// Its freight option is a candidate, not allocated route capacity or an
/// independent share of the buyer's purchasing budget.
/// </summary>
public sealed record CanonicalPrimitiveFreightPortfolioSlice(
    CanonicalEligibleFreightDiscoveryPolicy Policy,
    CanonicalEligibleFreightOfferDiscovery Discovery);

/// <summary>
/// An existing service's possible commercial use by a single commodity.
/// An identical carrier/asset can appear in many candidate uses. These
/// alternatives must NOT be summed or each independently settled.
/// </summary>
public sealed record CanonicalPrimitiveFreightCandidateUse(
    CommodityId Commodity, EconomicActivityKind Activity,
    ulong TechnologyTypeId, ulong TechnologyCatalogVersion,
    ulong ServiceId, EconomicPointId Source, EconomicPointId Buyer,
    ulong MaximumRawQuantity);

public sealed record CanonicalPrimitiveFreightSharedResource(
    ulong ResourceId,
    IReadOnlyList<CanonicalPrimitiveFreightCandidateUse> CompetingUses);

/// <summary>
/// Session-bound, read-only portfolio of separately authorized physical
/// commercial candidates. This is a discovery INVENTORY, deliberately not
/// a combined simultaneous market clearing or dispatch/settlement result.
/// In particular, all slices compete for a shared purchasing budget H,
/// regardless of whether their asset/service overlap sets are empty.
/// </summary>
public sealed class CanonicalPrimitiveFreightPortfolio
{
    public const int MaximumProfiles = 12;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    public IReadOnlyList<CanonicalPrimitiveFreightPortfolioSlice> Slices { get; }
    public IReadOnlyList<CanonicalPrimitiveFreightCandidateUse> CandidateUses { get; }
    public IReadOnlyList<CanonicalPrimitiveFreightSharedResource> SharedServices { get; }
    public IReadOnlyList<CanonicalPrimitiveFreightSharedResource> SharedAssets { get; }
    public string PurchasingPolicyId { get; }
    public ulong PurchasingPolicyVersion { get; }
    public string CalibrationId { get; }
    public ulong CalibrationVersion { get; }
    public string NominalUnitId { get; }
    internal CanonicalPrimitiveSiteDiscovery CurrentSites => _sites;

    public CanonicalPrimitiveJointFreightBudgetPreview PreviewJointBudget(
        IEnumerable<CanonicalPrimitiveJointFreightLot> lots,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy) =>
        CanonicalPrimitiveJointFreightBudgetPreview.FromPortfolio(
            this, _session, lots, postedPrices, purchasingPolicy, congestionPolicy);

    public int RegisteredRightsInspectedPerSlice { get; }
    public int TotalEligibleIntents { get; }
    public int TotalUnreachablePhysicalServices { get; }
    public int TotalSourceTreeBuilds { get; }
    public long TotalExaminedArcs { get; }
    public bool CanAuthorizeRealSettlement => false;
    public bool CanCombineIndependentTradePreviews => false;

    private CanonicalPrimitiveFreightPortfolio(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveFreightPortfolioSlice[] slices,
        CanonicalPrimitiveFreightCandidateUse[] uses,
        CanonicalPrimitiveFreightSharedResource[] sharedServices,
        CanonicalPrimitiveFreightSharedResource[] sharedAssets,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnitId)
    {
        PurchasingPolicyId = purchasingPolicyId;
        PurchasingPolicyVersion = purchasingPolicyVersion;
        CalibrationId = calibrationId;
        CalibrationVersion = calibrationVersion;
        NominalUnitId = nominalUnitId;
        _session = session;
        _sites = sites;
        Slices = Array.AsReadOnly(slices);
        CandidateUses = Array.AsReadOnly(uses);
        SharedServices = Array.AsReadOnly(sharedServices);
        SharedAssets = Array.AsReadOnly(sharedAssets);
        RegisteredRightsInspectedPerSlice =
            slices[0].Discovery.InspectedRegisteredServices;
        TotalEligibleIntents = slices.Sum(x => x.Discovery.EligibleEconomicIntents);
        TotalUnreachablePhysicalServices =
            slices.Sum(x => x.Discovery.UnreachablePhysicalServices);
        TotalSourceTreeBuilds = slices.Sum(x => x.Discovery.SourceTreeBuilds);
        TotalExaminedArcs = slices.Sum(x => x.Discovery.ExaminedArcs);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Freight portfolio belongs to another physical session.");
        _sites.RequireCurrent(session);
        foreach (var slice in Slices)
            slice.Discovery.RequireCurrent(session);
    }

    private static CanonicalPrimitiveFreightSharedResource[] FindShared(
        IEnumerable<(ulong Id, CanonicalPrimitiveFreightCandidateUse Use)> resources) =>
        resources.GroupBy(x => x.Id)
            .Where(x => x.Count() > 1)
            .OrderBy(x => x.Key)
            .Select(x => new CanonicalPrimitiveFreightSharedResource(
                x.Key, Array.AsReadOnly(x.Select(y => y.Use).ToArray())))
            .ToArray();

    /// <summary>
    /// Bounded multi-product inventory, not a cartesian all-city-pairs
    /// expansion. Each profile invokes VS63's legal proof and VS41/42 route
    /// checks, with identical session/snapshot provenance. Profiles are
    /// evaluated in stable order independently to avoid falsely allocating
    /// one carrier, asset or household budget multiple times.
    /// </summary>
    public static CanonicalPrimitiveFreightPortfolio FromSession(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalEligibleFreightDiscoveryPolicy> profiles,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnitId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(profiles);
        sites.RequireCurrent(session);
        if (string.IsNullOrWhiteSpace(purchasingPolicyId) ||
            purchasingPolicyVersion == 0 ||
            string.IsNullOrWhiteSpace(calibrationId) ||
            calibrationVersion == 0 ||
            string.IsNullOrWhiteSpace(nominalUnitId))
            throw new ArgumentException("Portfolio needs explicit versioned purchasing and quote provenance.");

        // Copy first: lazy caller sequences cannot mutate session during
        // discovery and leave a partially accepted multi-product result.
        var selected = profiles.ToArray();
        if (selected.Length is < 1 or > MaximumProfiles ||
            selected.Any(x => x is null))
            throw new ArgumentException(
                "Freight portfolio requires 1..12 valid profiles.", nameof(profiles));
        if (selected.Any(x => x.Pricing.RequiredInfrastructure != x.Infrastructure))
            throw new ArgumentException(
                "Freight technology's required infrastructure must match its declared physical mapping.",
                nameof(profiles));
        var keys = selected.Select(x => (
            x.Commodity, x.Activity, x.Pricing.TechnologyTypeId,
            x.Pricing.TechnologyCatalogVersion)).ToArray();
        if (keys.Distinct().Count() != keys.Length)
            throw new ArgumentException(
                "A commodity/activity/technology may be queried only once per portfolio.",
                nameof(profiles));
        // A unit cannot silently change its grams between two profiles,
        // even if their quoted technology differs.
        var masses = new Dictionary<CommodityId, ulong>();
        foreach (var profile in selected)
        {
            var commodity = profile.Catalog.Commodities.Single(x =>
                x.CommodityId == profile.Commodity);
            if (masses.TryGetValue(profile.Commodity, out var oldGrams) &&
                oldGrams != commodity.GramsPerFlowUnit)
                throw new ArgumentException(
                    "Conflicting physical mass units for the same commodity.",
                    nameof(profiles));
            masses[profile.Commodity] = commodity.GramsPerFlowUnit;
        }
        var ordered = selected
            .OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity)
            .ThenBy(x => x.Pricing.TechnologyTypeId)
            .ThenBy(x => x.Pricing.TechnologyCatalogVersion)
            .ToArray();
        var slices = new List<CanonicalPrimitiveFreightPortfolioSlice>();
        var uses = new List<CanonicalPrimitiveFreightCandidateUse>();
        var assetUses = new List<(ulong Id, CanonicalPrimitiveFreightCandidateUse Use)>();
        foreach (var profile in ordered)
        {
            var discovery = CanonicalEligibleFreightOfferDiscovery.FromSession(
                session, sites, profile, purchasingPolicyId,
                purchasingPolicyVersion, calibrationId, calibrationVersion,
                nominalUnitId);
            slices.Add(new(profile, discovery));
            foreach (var candidate in discovery.Services)
            {
                var use = new CanonicalPrimitiveFreightCandidateUse(
                    profile.Commodity, profile.Activity,
                    profile.Pricing.TechnologyTypeId,
                    profile.Pricing.TechnologyCatalogVersion,
                    candidate.Offer.ServiceId, candidate.Entitlement.Source,
                    candidate.Entitlement.Destination, candidate.MaximumRawQuantity);
                uses.Add(use);
                foreach (var asset in candidate.Offer.AssetPath.Distinct())
                    assetUses.Add((asset, use));
            }
        }
        var result = new CanonicalPrimitiveFreightPortfolio(
            session, sites, slices.ToArray(), uses.ToArray(),
            FindShared(uses.Select(x => (x.ServiceId, x))),
            FindShared(assetUses), purchasingPolicyId, purchasingPolicyVersion,
            calibrationId, calibrationVersion, nominalUnitId);
        result.RequireCurrent(session);
        return result;
    }
}
