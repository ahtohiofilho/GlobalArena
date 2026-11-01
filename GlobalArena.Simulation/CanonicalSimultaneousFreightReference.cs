using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One specific real service/commodity/activity candidate from a session-
/// bound VS80 supply-only discovery. Multiple offers MAY refer to the same
/// source or corridor; none confers independent stock or hard throughput.
/// </summary>
public sealed record CanonicalSimultaneousFreightOffer(
    CommodityId Commodity, EconomicActivityKind Activity,
    ulong TechnologyTypeId, ulong TechnologyCatalogVersion,
    ulong ServiceId, EconomicPointId Source, EconomicPointId Buyer,
    ulong SourcePotentialRaw, ulong BuyerPriceDemandSignalRaw);

public sealed record CanonicalSimultaneousFreightLot(
    CanonicalSimultaneousFreightOffer Offer, ulong RawQuantity);

/// <summary>
/// Physical quantity intentionally withheld at the origin for this trial.
/// Does not create or persist an inventory in the canonical session.
/// </summary>
public sealed record CanonicalSimultaneousFreightSourceRetention(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong RawQuantity);

public sealed record CanonicalSimultaneousFreightSourceBalance(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong PotentialProductionRaw,
    ulong DeliberateRetentionRaw, BigInteger TotalDeclaredShipmentsRaw,
    BigInteger RemainingUncommittedPotentialRaw);

public sealed record CanonicalSimultaneousFreightMarketIndex(
    EconomicPointId Buyer, CommodityId Commodity,
    BigInteger TotalHypotheticalDeliveredRaw, BigInteger BuyerDemandPressureRaw,
    CanonicalEndogenousPriceIndex GentleAveragePriceIndex,
    CanonicalEndogenousPriceIndex SteepAveragePriceIndex,
    CanonicalEndogenousPriceIndex GentleGrossRevenueIndex,
    CanonicalEndogenousPriceIndex SteepGrossRevenueIndex);

public sealed class CanonicalSimultaneousFreightAudit
{
    private readonly CanonicalSimultaneousFreightReference _reference;
    private readonly CanonicalSimultaneousFreightLot[] _lots;
    private readonly CanonicalSimultaneousFreightSourceRetention[] _retentions;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;

    public IReadOnlyList<CanonicalSimultaneousFreightLot> Lots { get; }
    public IReadOnlyList<CanonicalSimultaneousFreightSourceRetention> Retentions { get; }
    public IReadOnlyList<CanonicalSimultaneousFreightSourceBalance> Sources { get; }
    public IReadOnlyList<CanonicalSimultaneousFreightMarketIndex> Markets { get; }
    public IReadOnlyDictionary<ExperimentalSharedFreightCorridorKey, BigInteger> CorridorLoads { get; }
    public FoodTransportCostFraction TotalAuthenticatedNominalFreightCost { get; }
    public bool UsesExogenousMonetaryBudget => false;
    public bool CapsShipmentsByConsumerDemand => false;
    public bool ProvesHardThroughput => false;
    public bool ProducesActualDeliveryOrInventory => false;
    public bool ComputesNetMonetaryRevenue => false;
    public bool CanAuthorizeRealSettlement => false;

    internal CanonicalSimultaneousFreightAudit(
        CanonicalSimultaneousFreightReference reference,
        CanonicalSimultaneousFreightLot[] lots,
        CanonicalSimultaneousFreightSourceRetention[] retentions,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalSimultaneousFreightSourceBalance[] sources,
        CanonicalSimultaneousFreightMarketIndex[] markets,
        ExperimentalSharedFreightBatchEvaluation freight)
    {
        _reference = reference;
        _lots = (CanonicalSimultaneousFreightLot[])lots.Clone();
        _retentions = (CanonicalSimultaneousFreightSourceRetention[])retentions.Clone();
        _congestion = congestion;
        Lots = Array.AsReadOnly(_lots);
        Retentions = Array.AsReadOnly(_retentions);
        Sources = Array.AsReadOnly(sources);
        Markets = Array.AsReadOnly(markets);
        CorridorLoads = freight.CorridorLoads;
        TotalAuthenticatedNominalFreightCost = freight.TotalCost;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session) =>
        _reference.RequireCurrent(session);

    private static bool EqualIndex(
        CanonicalEndogenousPriceIndex a,
        CanonicalEndogenousPriceIndex b) => a.CompareTo(b) == 0;

    public void VerifyIndependent()
    {
        _reference.RequireCurrent(_reference.Session);
        var other = _reference.Reconstruct(_lots, _retentions, _congestion);
        if (!Sources.SequenceEqual(other.Sources) ||
            Markets.Count != other.Markets.Length ||
            Markets.Where((x, i) =>
                x.Buyer != other.Markets[i].Buyer ||
                x.Commodity != other.Markets[i].Commodity ||
                x.TotalHypotheticalDeliveredRaw != other.Markets[i].TotalHypotheticalDeliveredRaw ||
                x.BuyerDemandPressureRaw != other.Markets[i].BuyerDemandPressureRaw ||
                !EqualIndex(x.GentleAveragePriceIndex, other.Markets[i].GentleAveragePriceIndex) ||
                !EqualIndex(x.SteepAveragePriceIndex, other.Markets[i].SteepAveragePriceIndex) ||
                !EqualIndex(x.GentleGrossRevenueIndex, other.Markets[i].GentleGrossRevenueIndex) ||
                !EqualIndex(x.SteepGrossRevenueIndex, other.Markets[i].SteepGrossRevenueIndex)).Any() ||
            TotalAuthenticatedNominalFreightCost.CompareTo(other.Freight.TotalCost) != 0 ||
            CorridorLoads.Count != other.Freight.CorridorLoads.Count ||
            CorridorLoads.Any(x => !other.Freight.CorridorLoads.TryGetValue(
                x.Key, out var q) || x.Value != q))
            throw new InvalidOperationException(
                "Independent reconstruction of simultaneous physical supply, market indices or corridor cost diverged.");
        _reference.RequireCurrent(_reference.Session);
    }
}

internal sealed record CanonicalSimultaneousFreightReconstruction(
    CanonicalSimultaneousFreightSourceBalance[] Sources,
    CanonicalSimultaneousFreightMarketIndex[] Markets,
    ExperimentalSharedFreightBatchEvaluation Freight);

/// <summary>
/// VS81 read-only multi-lot physical audit. All services are rederived from
/// existing rights, operational built assets and authenticated routes.
/// Demand is only a PRICE signal: no demand/raw quantity purchase cutoff,
/// private wallet, synthetic carrier, booked shipment or settlement.
/// </summary>
public sealed class CanonicalSimultaneousFreightReference
{
    public const int MaximumProfiles = 12;
    public const int MaximumLots = 12;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalEligibleFreightDiscoveryPolicy[] _policies;
    private readonly CanonicalEligibleFreightOfferDiscovery[] _discoveries;
    private readonly Dictionary<CanonicalSimultaneousFreightOffer,
        (CanonicalEligibleFreightService Service, ulong GramsPerUnit)> _authenticated;

    public CanonicalPhysicalTacticalSession Session => _session;
    public IReadOnlyList<CanonicalSimultaneousFreightOffer> Offers { get; }
    public bool UsesExogenousMonetaryBudget => false;
    public bool UsesBuyerDemandAsPhysicalCap => false;
    public bool UsesActualTransportEntitlements => true;
    public bool ProvesHardVehicleThroughput => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalSimultaneousFreightReference(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEligibleFreightDiscoveryPolicy[] policies,
        CanonicalEligibleFreightOfferDiscovery[] discoveries,
        Dictionary<CanonicalSimultaneousFreightOffer,
            (CanonicalEligibleFreightService Service, ulong GramsPerUnit)> authenticated,
        CanonicalSimultaneousFreightOffer[] offers)
    {
        _session = session;
        _sites = sites;
        _policies = policies;
        _discoveries = discoveries;
        _authenticated = authenticated;
        Offers = Array.AsReadOnly(offers);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "VS81 freight reference belongs to another physical session.");
        _sites.RequireCurrent(session);
        foreach (var discovery in _discoveries)
            discovery.RequireCurrent(session);
    }

    private static (EconomicPointId Source, CommodityId Commodity,
        EconomicActivityKind Activity) SourceKey(
            CanonicalSimultaneousFreightOffer o) =>
        (o.Source, o.Commodity, o.Activity);

    private static (EconomicPointId Source, CommodityId Commodity,
        EconomicActivityKind Activity) SourceKey(
            CanonicalSimultaneousFreightSourceRetention r) =>
        (r.Source, r.Commodity, r.Activity);

    internal CanonicalSimultaneousFreightReconstruction Reconstruct(
        IReadOnlyList<CanonicalSimultaneousFreightLot> inputLots,
        IReadOnlyList<CanonicalSimultaneousFreightSourceRetention> inputRetentions,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        RequireCurrent(_session);
        var supply = _sites.Flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity),
            x => x.RawQuantity);
        var demand = _sites.Flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId),
            x => x.RawQuantity);

        var used = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>();
        var retained = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), ulong>();
        foreach (var r in inputRetentions)
        {
            var key = SourceKey(r);
            if (!supply.TryGetValue(key, out var possible) ||
                r.RawQuantity == 0 || r.RawQuantity > possible ||
                !retained.TryAdd(key, r.RawQuantity))
                throw new ArgumentException(
                    "Intentional retention must refer to a unique real productive source and cannot exceed physical production.",
                    nameof(inputRetentions));
        }

        var selected = inputLots.OrderBy(x => x.Offer.Commodity.Value)
            .ThenBy(x => x.Offer.Activity)
            .ThenBy(x => x.Offer.Source.Value)
            .ThenBy(x => x.Offer.Buyer.Value)
            .ThenBy(x => x.Offer.ServiceId)
            .ThenBy(x => x.Offer.TechnologyTypeId)
            .ThenBy(x => x.Offer.TechnologyCatalogVersion).ToArray();

        var employed = new HashSet<CanonicalSimultaneousFreightOffer>(
            ReferenceEqualityComparer.Instance);
        var physicalLots = new List<ExperimentalSharedFreightLot>();
        var markets = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
        for (var i = 0; i < selected.Length; i++)
        {
            var lot = selected[i];
            var offer = lot.Offer;
            if (!_authenticated.TryGetValue(offer, out var binding) ||
                !Offers.Any(o => ReferenceEquals(o, offer)) ||
                !employed.Add(offer) || lot.RawQuantity == 0)
                throw new ArgumentException(
                    "Every lot needs one distinct, live authenticated offer and positive physical quantity.",
                    nameof(inputLots));
            var key = SourceKey(offer);
            if (!supply.TryGetValue(key, out var potential) ||
                !demand.ContainsKey((offer.Buyer, offer.Commodity)) ||
                _sites.At(offer.Source)?.ResidentWorkers is not > 0 ||
                _sites.At(offer.Buyer)?.ResidentWorkers is not > 0 ||
                binding.Service.MaximumRawQuantity != potential)
                throw new InvalidOperationException(
                    "Authenticated commercial offer must match current real source, buyer and source-only physical envelope.");
            retained.TryGetValue(key, out var reserved);
            used.TryGetValue(key, out var previous);
            var cumulative = previous + lot.RawQuantity;
            if (cumulative + reserved > potential)
                throw new ArgumentException(
                    "Two or more services cannot consume the same production; retention is deducted before shipment.",
                    nameof(inputLots));
            used[key] = cumulative;
            var mkey = (offer.Buyer, offer.Commodity);
            markets.TryGetValue(mkey, out var consumed);
            markets[mkey] = consumed + lot.RawQuantity;
            physicalLots.Add(new ExperimentalSharedFreightLot(
                checked((ulong)(i + 1)),
                (BigInteger)lot.RawQuantity * binding.GramsPerUnit,
                [binding.Service.Option]));
        }

        var balances = used.Keys.Union(retained.Keys)
            .OrderBy(x => x.Item1.Value)
            .ThenBy(x => x.Item2.Value)
            .ThenBy(x => x.Item3)
            .Select(key =>
            {
                var possible = supply[key];
                used.TryGetValue(key, out var quantity);
                retained.TryGetValue(key, out var keep);
                return new CanonicalSimultaneousFreightSourceBalance(
                    key.Item1, key.Item2, key.Item3, possible, keep,
                    quantity, (BigInteger)possible - keep - quantity);
            }).ToArray();

        var priceRows = markets.OrderBy(x => x.Key.Item1.Value)
            .ThenBy(x => x.Key.Item2.Value)
            .Select(x =>
            {
                var d = new BigInteger(demand[x.Key]);
                var mild = CanonicalEndogenousLocalPriceComparison.Calculate(
                    x.Value, d, 1);
                var steep = CanonicalEndogenousLocalPriceComparison.Calculate(
                    x.Value, d, 2);
                return new CanonicalSimultaneousFreightMarketIndex(
                    x.Key.Item1, x.Key.Item2, x.Value, d,
                    mild.Average, steep.Average, mild.Revenue, steep.Revenue);
            }).ToArray();
        var freight = ExperimentalSharedFreightBatchCost.Evaluate(
            physicalLots, new int[physicalLots.Count], congestion);
        RequireCurrent(_session);
        return new(balances, priceRows, freight);
    }

    public CanonicalSimultaneousFreightAudit Audit(
        IEnumerable<CanonicalSimultaneousFreightLot> proposedLots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        IEnumerable<CanonicalSimultaneousFreightSourceRetention>? explicitRetentions = null)
    {
        ArgumentNullException.ThrowIfNull(proposedLots);
        ArgumentNullException.ThrowIfNull(congestion);
        RequireCurrent(_session);
        var lots = proposedLots.ToArray();
        var retentions = explicitRetentions?.ToArray() ??
            Array.Empty<CanonicalSimultaneousFreightSourceRetention>();
        if (lots.Length is < 1 or > MaximumLots ||
            lots.Any(x => x is null || x.Offer is null || x.RawQuantity == 0) ||
            retentions.Length > MaximumLots ||
            retentions.Any(x => x is null || x.RawQuantity == 0))
            throw new ArgumentException(
                "Simultaneous trial requires 1..12 positive lots and at most 12 explicit source retentions.");
        var sorted = lots.OrderBy(x => x.Offer.Commodity.Value)
            .ThenBy(x => x.Offer.Activity)
            .ThenBy(x => x.Offer.Source.Value)
            .ThenBy(x => x.Offer.Buyer.Value)
            .ThenBy(x => x.Offer.ServiceId)
            .ThenBy(x => x.Offer.TechnologyTypeId)
            .ThenBy(x => x.Offer.TechnologyCatalogVersion).ToArray();
        var keep = retentions.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value).ThenBy(x => x.Activity).ToArray();
        var reconstructed = Reconstruct(sorted, keep, congestion);
        var audit = new CanonicalSimultaneousFreightAudit(
            this, sorted, keep, congestion,
            reconstructed.Sources, reconstructed.Markets,
            reconstructed.Freight);
        audit.VerifyIndependent();
        return audit;
    }

    public static CanonicalSimultaneousFreightReference FromSites(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalEligibleFreightDiscoveryPolicy> policies,
        string purchasingId, ulong purchasingVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnitId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(policies);
        sites.RequireCurrent(session);
        var profiles = policies.ToArray();
        if (profiles.Length is < 1 or > MaximumProfiles ||
            profiles.Any(x => x is null) ||
            profiles.Select(x => (x.Commodity, x.Activity,
                x.Pricing.TechnologyTypeId,
                x.Pricing.TechnologyCatalogVersion)).Distinct().Count() !=
                profiles.Length)
            throw new ArgumentException(
                "VS81 needs unique valid commodity/activity/technology profile identities.",
                nameof(policies));
        if (string.IsNullOrWhiteSpace(purchasingId) ||
            string.IsNullOrWhiteSpace(calibrationId) ||
            string.IsNullOrWhiteSpace(nominalUnitId))
            throw new ArgumentException(
                "Physical institutional proof requires explicit provenance.");
        var ordered = profiles.OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity)
            .ThenBy(x => x.Pricing.TechnologyTypeId)
            .ThenBy(x => x.Pricing.TechnologyCatalogVersion).ToArray();
        var discovered = new List<CanonicalEligibleFreightOfferDiscovery>();
        var offers = new List<CanonicalSimultaneousFreightOffer>();
        var bindings = new Dictionary<CanonicalSimultaneousFreightOffer,
            (CanonicalEligibleFreightService Service, ulong GramsPerUnit)>(
                ReferenceEqualityComparer.Instance);
        var massByCommodity = new Dictionary<CommodityId, ulong>();
        foreach (var p in ordered)
        {
            var mass = p.Catalog.Commodities.Single(x =>
                x.CommodityId == p.Commodity).GramsPerFlowUnit;
            if (massByCommodity.TryGetValue(p.Commodity, out var already) &&
                already != mass)
                throw new ArgumentException(
                    "One commodity cannot have two different physical mass conversions.",
                    nameof(policies));
            massByCommodity[p.Commodity] = mass;
            var d = CanonicalEligibleFreightOfferDiscovery.FromSessionSupplyBound(
                session, sites, p, purchasingId, purchasingVersion,
                calibrationId, calibrationVersion, nominalUnitId);
            if (d.UsesBuyerDemandQuantityCap)
                throw new InvalidOperationException(
                    "VS81 must not reimport legacy buyer demand as a shipment cutoff.");
            discovered.Add(d);
            foreach (var authenticated in d.Services)
            {
                var o = new CanonicalSimultaneousFreightOffer(
                    p.Commodity, p.Activity,
                    p.Pricing.TechnologyTypeId,
                    p.Pricing.TechnologyCatalogVersion,
                    authenticated.Offer.ServiceId,
                    authenticated.Entitlement.Source,
                    authenticated.Entitlement.Destination,
                    authenticated.MaximumRawQuantity,
                    sites.Flows.DemandFlows.Single(x =>
                        x.EconomicPointId == authenticated.Entitlement.Destination &&
                        x.CommodityId == p.Commodity).RawQuantity);
                if (offers.Any(x => x == o))
                    throw new InvalidOperationException(
                        "Identical authenticated service/commodity candidate was discovered twice.");
                offers.Add(o);
                bindings.Add(o, (authenticated, mass));
            }
        }
        var result = new CanonicalSimultaneousFreightReference(
            session, sites, ordered, discovered.ToArray(), bindings,
            offers.ToArray());
        result.RequireCurrent(session);
        return result;
    }
}
