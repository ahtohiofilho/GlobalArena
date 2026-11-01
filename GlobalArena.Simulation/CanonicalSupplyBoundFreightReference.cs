using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// ONE existing, route/rights-proven transport service's hypothetical source
/// quantity envelope. This does NOT claim a hard carrier throughput limit:
/// VS42 congestion is a cost curve, not a certified maximum vehicle load.
/// </summary>
public sealed record CanonicalSupplyBoundFreightRow(
    ulong ServiceId,
    EconomicPointId Source,
    EconomicPointId Buyer,
    CommodityId Commodity,
    EconomicActivityKind Activity,
    ulong SourcePhysicalSupplyRaw,
    ulong BuyerDemandPriceSignalRaw,
    ulong LegacyBuyerDemandCappedRaw,
    ulong NewSupplyBoundRaw);

/// <summary>
/// A single hypothetical quantity evaluated on an actually authorized route.
/// Price remains a relative index and freight remains in its own nominal unit.
/// </summary>
public sealed record CanonicalSupplyBoundFreightPriceTrial(
    ulong ServiceId,
    ulong RawQuantity,
    ulong BuyerDemandPriceSignalRaw,
    CanonicalEndogenousPriceIndex GentleAveragePriceIndex,
    CanonicalEndogenousPriceIndex SteepAveragePriceIndex,
    CanonicalEndogenousPriceIndex GentleGrossRevenueIndex,
    CanonicalEndogenousPriceIndex SteepGrossRevenueIndex,
    FoodTransportCostFraction AuthenticatedNominalFreightCost);

/// <summary>
/// VS80. Reuses VS63's exact legal freight proof and route discovery, but
/// removes buyer demand magnitude from the quantity envelope WITHOUT touching
/// historical VS63/64 references. No source-quantity exceeds authenticated
/// physical production. One service trial is not a combined congestion or
/// settlement authorization, nor an executable remote market.
/// </summary>
public sealed class CanonicalSupplyBoundFreightReference
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalEligibleFreightDiscoveryPolicy _policy;
    private readonly CanonicalEligibleFreightOfferDiscovery _discovery;
    private readonly string _purchasingId;
    private readonly ulong _purchasingVersion;
    private readonly string _calibrationId;
    private readonly ulong _calibrationVersion;
    private readonly string _nominalUnitId;

    public IReadOnlyList<CanonicalSupplyBoundFreightRow> Services { get; }
    public int RegisteredServicesInspected => _discovery.InspectedRegisteredServices;
    public int EligiblePhysicalServiceIntents => _discovery.EligibleEconomicIntents;
    public int UnreachablePhysicalServices => _discovery.UnreachablePhysicalServices;
    public bool UsesExogenousMoneyBudget => false;
    public bool CapsQuantityByBuyerDemand => false;
    public bool EnforcesSourcePhysicalQuantity => true;
    public bool UsesRealInstitutionalProof => true;
    public bool ProvesHardVehicleThroughput => false;
    public bool CreatesActualRemoteConsumption => false;
    public bool ComputesNetMonetaryRevenue => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalSupplyBoundFreightReference(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEligibleFreightDiscoveryPolicy policy,
        CanonicalEligibleFreightOfferDiscovery discovery,
        string purchasingId, ulong purchasingVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnitId,
        CanonicalSupplyBoundFreightRow[] rows)
    {
        _session = session;
        _sites = sites;
        _policy = policy;
        _discovery = discovery;
        _purchasingId = purchasingId;
        _purchasingVersion = purchasingVersion;
        _calibrationId = calibrationId;
        _calibrationVersion = calibrationVersion;
        _nominalUnitId = nominalUnitId;
        Services = Array.AsReadOnly(rows);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Supply-bound freight reference belongs to another physical session.");
        _sites.RequireCurrent(session);
        _discovery.RequireCurrent(session);
    }

    private static CanonicalSupplyBoundFreightRow[] Rows(
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEligibleFreightOfferDiscovery discovered,
        CanonicalEligibleFreightDiscoveryPolicy policy)
    {
        if (discovered.UsesBuyerDemandQuantityCap)
            throw new InvalidOperationException(
                "Cannot replace buyer demand cap using historically demand-capped services.");
        var supply = sites.Flows.SupplyFlows
            .Where(x => x.CommodityId == policy.Commodity &&
                x.Activity == policy.Activity)
            .ToDictionary(x => x.EconomicPointId, x => x.RawQuantity);
        var demand = sites.Flows.DemandFlows
            .Where(x => x.CommodityId == policy.Commodity)
            .ToDictionary(x => x.EconomicPointId, x => x.RawQuantity);
        var rows = new List<CanonicalSupplyBoundFreightRow>();
        foreach (var service in discovered.Services)
        {
            var right = service.Entitlement;
            if (!supply.TryGetValue(right.Source, out var production) ||
                !demand.TryGetValue(right.Destination, out var pressure) ||
                right.Source == right.Destination ||
                production == 0 ||
                service.MaximumRawQuantity != production ||
                sites.At(right.Source)?.ResidentWorkers is not > 0 ||
                sites.At(right.Destination)?.ResidentWorkers is not > 0)
                throw new InvalidOperationException(
                    "Supply envelope must belong to the current real producer, resident buyer and authenticated right.");
            rows.Add(new(service.Offer.ServiceId, right.Source,
                right.Destination, policy.Commodity, policy.Activity,
                production, pressure, Math.Min(production, pressure),
                service.MaximumRawQuantity));
        }
        return rows.OrderBy(x => x.ServiceId)
            .ThenBy(x => x.Source.Value)
            .ThenBy(x => x.Buyer.Value).ToArray();
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var repeated = CanonicalEligibleFreightOfferDiscovery.FromSessionSupplyBound(
            _session, _sites, _policy, _purchasingId, _purchasingVersion,
            _calibrationId, _calibrationVersion, _nominalUnitId);
        var rows = Rows(_sites, repeated, _policy);
        if (!Services.SequenceEqual(rows) ||
            repeated.InspectedRegisteredServices != RegisteredServicesInspected ||
            repeated.EligibleEconomicIntents != EligiblePhysicalServiceIntents ||
            repeated.UnreachablePhysicalServices != UnreachablePhysicalServices)
            throw new InvalidOperationException(
                "Supply-bound freight reference failed independently authenticated replay.");
        RequireCurrent(_session);
    }

    /// <summary>
    /// Preview a physically source-bounded lot, even when q exceeds the
    /// buyer's old physical demand. The buyer demand remains a price signal.
    /// No hard vehicle throughput or multiple-lot market clearing is claimed.
    /// </summary>
    public CanonicalSupplyBoundFreightPriceTrial PreviewOneLot(
        ulong serviceId, ulong rawQuantity,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        ArgumentNullException.ThrowIfNull(congestion);
        RequireCurrent(_session);
        var row = Services.Single(x => x.ServiceId == serviceId);
        var service = _discovery.Services.Single(x =>
            x.Offer.ServiceId == serviceId);
        if (rawQuantity == 0 || rawQuantity > row.SourcePhysicalSupplyRaw ||
            rawQuantity > row.NewSupplyBoundRaw)
            throw new ArgumentOutOfRangeException(nameof(rawQuantity),
                "Trial quantity exceeds actual source production or authenticated source envelope.");
        if (!_policy.Catalog.TryGet(_policy.Commodity, out var item) ||
            item is null)
            throw new InvalidOperationException("Current commodity has no authenticated mass.");
        var mass = new BigInteger(rawQuantity) * item.GramsPerFlowUnit;
        var freight = ExperimentalSharedFreightBatchCost.Evaluate(
            [new ExperimentalSharedFreightLot(
                1UL, mass, [service.Option])],
            [0], congestion);
        var gentle = CanonicalEndogenousLocalPriceComparison.Calculate(
            rawQuantity, row.BuyerDemandPriceSignalRaw, 1);
        var steep = CanonicalEndogenousLocalPriceComparison.Calculate(
            rawQuantity, row.BuyerDemandPriceSignalRaw, 2);
        RequireCurrent(_session);
        return new(serviceId, rawQuantity, row.BuyerDemandPriceSignalRaw,
            gentle.Average, steep.Average, gentle.Revenue, steep.Revenue,
            freight.TotalCost);
    }

    public static CanonicalSupplyBoundFreightReference FromSites(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEligibleFreightDiscoveryPolicy policy,
        string purchasingId, ulong purchasingVersion,
        string calibrationId, ulong calibrationVersion, string nominalUnitId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(purchasingId) ||
            string.IsNullOrWhiteSpace(calibrationId) ||
            string.IsNullOrWhiteSpace(nominalUnitId))
            throw new ArgumentException(
                "Institutional transport proof requires explicit versioned provenance.");
        sites.RequireCurrent(session);
        var discovery = CanonicalEligibleFreightOfferDiscovery.FromSessionSupplyBound(
            session, sites, policy, purchasingId, purchasingVersion,
            calibrationId, calibrationVersion, nominalUnitId);
        var rows = Rows(sites, discovery, policy);
        var reference = new CanonicalSupplyBoundFreightReference(
            session, sites, policy, discovery, purchasingId,
            purchasingVersion, calibrationId, calibrationVersion,
            nominalUnitId, rows);
        reference.VerifyIndependent();
        return reference;
    }
}
