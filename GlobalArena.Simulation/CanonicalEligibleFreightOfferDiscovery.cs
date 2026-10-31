using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One versioned technology/calibration context and one explicitly selected
/// primitive commodity/activity. Does not declare any source/destination,
/// service identity or route. Institutional rights must already be recorded
/// by the canonical session; there is no implicit public/free carrier.
/// </summary>
public sealed class CanonicalEligibleFreightDiscoveryPolicy
{
    public CommodityId Commodity { get; }
    public EconomicActivityKind Activity { get; }
    public ExperimentalFreightTechnologyPricing Pricing { get; }
    public ExperimentalLandInfrastructureKind Infrastructure { get; }
    public FoodCommodityCatalog Catalog { get; }
    public int MaximumRegisteredServices { get; }

    public CanonicalEligibleFreightDiscoveryPolicy(
        CommodityId commodity, EconomicActivityKind activity,
        ExperimentalFreightTechnologyPricing pricing,
        ExperimentalLandInfrastructureKind infrastructure,
        FoodCommodityCatalog catalog, int maximumRegisteredServices = 128)
    {
        Pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (!commodity.IsValid || !Catalog.Contains(commodity) ||
            activity is not (EconomicActivityKind.Agriculture or
                EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
            !Enum.IsDefined(infrastructure) ||
            maximumRegisteredServices is < 1 or > 128)
            throw new ArgumentException("Freight discovery needs a valid commodity, activity and bounded technology.");
        Commodity = commodity;
        Activity = activity;
        Infrastructure = infrastructure;
        MaximumRegisteredServices = maximumRegisteredServices;
    }
}

/// <summary>
/// One existing session right, one genuinely reachable constructed route and
/// the current pricing option. The routing projection is not a real shipment.
/// </summary>
public sealed record CanonicalEligibleFreightService(
    ExperimentalTransportServiceEntitlement Entitlement,
    EconomicTransportServiceOffer Offer,
    ExperimentalSharedFreightOption Option,
    ulong MaximumRawQuantity);

public sealed class CanonicalEligibleFreightOfferDiscovery
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalTransportProof? _proof;
    private readonly IReadOnlyList<ExperimentalBuiltFreightServiceEvidence> _evidence;
    public IReadOnlyList<CanonicalEligibleFreightService> Services { get; }
    public IReadOnlyList<CanonicalPrimitiveTradeLane> Lanes { get; }
    public int InspectedRegisteredServices { get; }
    public int EligibleEconomicIntents { get; }
    public int UnreachablePhysicalServices { get; }
    public int SourceTreeBuilds { get; }
    public long ExaminedArcs { get; }
    public bool CanAuthorizeRealSettlement => false;

    internal CanonicalEligibleFreightOfferDiscovery(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalTransportProof? proof,
        CanonicalEligibleFreightService[] services,
        CanonicalPrimitiveTradeLane[] lanes,
        ExperimentalBuiltFreightServiceEvidence[] evidence,
        int registered, int intents, int unreachable, int sourceTrees, long examinedArcs)
    {
        _session = session;
        _sites = sites;
        _proof = proof;
        Services = Array.AsReadOnly(services);
        Lanes = Array.AsReadOnly(lanes);
        _evidence = Array.AsReadOnly(evidence);
        InspectedRegisteredServices = registered;
        EligibleEconomicIntents = intents;
        UnreachablePhysicalServices = unreachable;
        SourceTreeBuilds = sourceTrees;
        ExaminedArcs = examinedArcs;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        _sites.RequireCurrent(session);
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException("Freight discovery belongs to a different session.");
        if (_proof is not null)
            session.TransportNetwork.Validate(_proof);
    }

    /// <summary>
    /// Use discovered lanes/evidence in the VS62-VS60 audit without caller
    /// entering endpoint lists or a route; policy, quote, H and price still
    /// remain explicit. No price or actual settlement is created here.
    /// </summary>
    public CanonicalLivePrimitiveTradePreview PreviewTrade(
        FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> quotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibration,
        string physicalFreightNominalUnitId,
        ExperimentalSharedFreightCongestionPolicy congestion,
        ulong rawQuantum, int maximumConfigurations = 4096)
    {
        RequireCurrent(_session);
        if (Services.Count == 0)
            throw new InvalidOperationException("No currently eligible constructed service to preview.");
        return _sites.PreviewTrade(catalog, postedPrices, purchasingPolicy,
            quotes, calibration, physicalFreightNominalUnitId, Lanes,
            _evidence, congestion, rawQuantum, maximumConfigurations);
    }

    /// <summary>
    /// O(R + route discovery) over the recorded service rights, NOT a
    /// Cartesian product of inhabited sites. VS41 independently finds the
    /// deterministic cheapest built path; VS42 FromBuiltOffer verifies its
    /// contiguous, operational and open resources; VS57 validates all
    /// recorded institutional grants and rights. Unreachable paths are
    /// omitted; absent legal grants fail closed instead of being invented.
    /// </summary>
    public static CanonicalEligibleFreightOfferDiscovery FromSession(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEligibleFreightDiscoveryPolicy policy,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnitId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(policy);
        sites.RequireCurrent(session);
        var hash = session.CurrentHash;
        var revision = session.TransportNetwork.Revision;
        var registered = session.TransportNetwork.RegisteredEntitlements;
        if (registered.Count > policy.MaximumRegisteredServices)
            throw new InvalidOperationException(
                "Registered service cap exceeded; no partial candidate inventory returned.");
        if (registered.Select(x => x.ServiceId).Distinct().Count() != registered.Count)
            throw new InvalidOperationException(
                "The VS41 reference requires globally distinct service IDs.");
        if (!policy.Catalog.TryGet(policy.Commodity, out var item) || item is null)
            throw new InvalidOperationException("Declared freight commodity has no mass conversion.");
        var supply = sites.Flows.SupplyFlows
            .Where(x => x.CommodityId == policy.Commodity && x.Activity == policy.Activity)
            .ToDictionary(x => x.EconomicPointId);
        var demand = sites.Flows.DemandFlows
            .Where(x => x.CommodityId == policy.Commodity)
            .ToDictionary(x => x.EconomicPointId);
        var anchorMap = sites.BuyerAnchors.ToDictionary(x => x.Buyer, x => x.Tile);
        var intents = new List<EconomicFineTransportServiceIntent>();
        var attached = new Dictionary<ulong, (ExperimentalTransportServiceEntitlement Right, ulong Maximum)>();
        foreach (var right in registered)
        {
            if (!supply.TryGetValue(right.Source, out var source) ||
                !demand.TryGetValue(right.Destination, out var buyer) ||
                right.Source == right.Destination)
                continue;
            var max = Math.Min(source.RawQuantity, buyer.RawQuantity);
            if (max == 0) continue;
            // RawQuantity (1e6 units/whole) * grams/whole gives micrograms.
            // The itinerary cost estimate remains explicitly hypothetical.
            var load = (BigInteger)max * item.GramsPerFlowUnit;
            intents.Add(new EconomicFineTransportServiceIntent(right.ServiceId,
                new StrategicEconomicRouteKey(right.Source, right.Destination),
                policy.Pricing.TechnologyTypeId,
                policy.Pricing.TechnologyCatalogVersion, load));
            attached.Add(right.ServiceId, (right, max));
        }
        if (intents.Count == 0)
        {
            sites.RequireCurrent(session);
            return new(session, sites, null, [], [], [],
                registered.Count, 0, 0, 0, 0L);
        }
        var assets = session.TransportNetwork.InstalledAssets;
        var access = session.TransportNetwork.Access;
        var facts = new CanonicalLandEdgeFactsSnapshot(session);
        var mapping = new Dictionary<(ulong TechnologyTypeId, ulong CatalogVersion),
            ExperimentalLandInfrastructureKind> {
            [(policy.Pricing.TechnologyTypeId, policy.Pricing.TechnologyCatalogVersion)] =
                policy.Infrastructure
        };
        var network = new ExperimentalLandInfrastructureSnapshot(
            session, facts, assets, mapping, access, revision);
        var diagnostics = new ExperimentalFreightRouteSearchDiagnostics();
        var offers = new ExperimentalCostOptimalFreightItineraryDiscovery()
            .Discover(session, facts, network, anchorMap, access,
                assets, intents, [policy.Pricing], diagnostics);
        var accepted = new List<CanonicalEligibleFreightService>();
        var evidence = new List<ExperimentalBuiltFreightServiceEvidence>();
        foreach (var offer in offers)
        {
            var option = ExperimentalSharedFreightOption.FromBuiltOffer(
                offer, session, anchorMap, assets, access);
            var record = attached[offer.ServiceId];
            accepted.Add(new(record.Right, offer, option, record.Maximum));
            evidence.Add(new ExperimentalBuiltFreightServiceEvidence(offer, option));
        }
        CanonicalTransportProof? proof = null;
        if (accepted.Count > 0)
        {
            // Capture + independent Validate: no missing institution/operator
            // entitlement or asset grant may become a deliverable service.
            proof = session.TransportNetwork.Capture(evidence,
                purchasingPolicyId, purchasingPolicyVersion,
                calibrationId, calibrationVersion, nominalUnitId);
            session.TransportNetwork.Validate(proof);
        }
        var lanes = accepted
            .GroupBy(x => (x.Entitlement.Source, x.Entitlement.Destination))
            .OrderBy(x => x.Key.Source.Value)
            .ThenBy(x => x.Key.Destination.Value)
            .Select(x => new CanonicalPrimitiveTradeLane(
                x.Key.Source, x.Key.Destination, policy.Commodity, policy.Activity,
                x.Select(row => row.Option))).ToArray();
        if (session.CurrentHash != hash ||
            session.TransportNetwork.Revision != revision)
            throw new InvalidOperationException("Session changed during institutional offer discovery.");
        var result = new CanonicalEligibleFreightOfferDiscovery(
            session, sites, proof, accepted.ToArray(), lanes,
            evidence.ToArray(), registered.Count, intents.Count,
            diagnostics.UnreachableServices, diagnostics.SourceTreeBuilds,
            diagnostics.ExaminedArcs);
        result.RequireCurrent(session);
        return result;
    }
}
