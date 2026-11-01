using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A HYPOTHETICAL physically realized quantity, not unextracted potential
/// and not a command to produce, debit inventory or settle a sale.
/// </summary>
public sealed record CanonicalRealizedProductionDeclaration(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong ProducedRaw,
    ulong DeliberatelyRetainedRaw);

public sealed record CanonicalRealizedProductionBalance(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity,
    ulong PotentialRaw, ulong HypotheticallyProducedRaw,
    ulong PotentialNotExtractedRaw, ulong DeliberatelyRetainedRaw,
    BigInteger HypotheticallyRemotelyShippedRaw,
    BigInteger OfferedAndLocallyAbsorbedRaw);

public sealed record CanonicalRealizedLocalPriceMarket(
    EconomicPointId LocalMarket, CommodityId Commodity,
    BigInteger TotalLocallyAbsorbedRaw, BigInteger ResidentDemandPressureRaw,
    CanonicalEndogenousPriceIndex GentleAveragePriceIndex,
    CanonicalEndogenousPriceIndex SteepAveragePriceIndex,
    CanonicalEndogenousPriceIndex GentleGrossRevenueIndex,
    CanonicalEndogenousPriceIndex SteepGrossRevenueIndex);

internal sealed record CanonicalRealizedProductionReconstruction(
    CanonicalRealizedProductionBalance[] Balances,
    CanonicalRealizedLocalPriceMarket[] LocalMarkets,
    CanonicalSimultaneousFreightAudit? Freight);

/// <summary>
/// VS82 read-only: potential -> explicitly realized -> deliberately retained
/// + actual trial freight offers + local macro absorption. No latent material
/// appears from unused production capacity and no inventory is committed.
/// Local and remote price outputs are dimensionless experimental indices,
/// while authenticated shared freight retains its own nominal cost unit.
/// </summary>
public sealed class CanonicalRealizedProductionAllocation
{
    public const int MaximumDeclarations = 12;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalSimultaneousFreightReference _freightReference;
    private readonly CanonicalRealizedProductionDeclaration[] _declarations;
    private readonly CanonicalSimultaneousFreightLot[] _lots;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;
    public IReadOnlyList<CanonicalRealizedProductionDeclaration> Declarations { get; }
    public IReadOnlyList<CanonicalRealizedProductionBalance> Sources { get; }
    public IReadOnlyList<CanonicalRealizedLocalPriceMarket> LocalMarkets { get; }
    public CanonicalSimultaneousFreightAudit? FreightAudit { get; }
    public IReadOnlyList<CanonicalSimultaneousFreightMarketIndex> RemoteMarkets =>
        FreightAudit?.Markets ?? Array.Empty<CanonicalSimultaneousFreightMarketIndex>();
    public FoodTransportCostFraction AuthenticatedNominalFreightCost =>
        FreightAudit?.TotalAuthenticatedNominalFreightCost ??
            new FoodTransportCostFraction(0, 1);
    public bool TreatsUnextractedPotentialAsInventory => false;
    public bool CreatesInvoluntaryStock => false;
    public bool CreatesActualConsumption => false;
    public bool UsesExogenousBuyerMoney => false;
    public bool CapsConsumptionByDemandRaw => false;
    public bool ProvesHardFreightThroughput => false;
    public bool ComputesMonetaryNetRevenue => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalRealizedProductionAllocation(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freightReference,
        CanonicalRealizedProductionDeclaration[] declarations,
        CanonicalSimultaneousFreightLot[] lots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalRealizedProductionReconstruction reconstructed)
    {
        _session = session;
        _sites = sites;
        _freightReference = freightReference;
        _declarations = (CanonicalRealizedProductionDeclaration[])declarations.Clone();
        _lots = (CanonicalSimultaneousFreightLot[])lots.Clone();
        _congestion = congestion;
        Declarations = Array.AsReadOnly(_declarations);
        Sources = Array.AsReadOnly(reconstructed.Balances);
        LocalMarkets = Array.AsReadOnly(reconstructed.LocalMarkets);
        FreightAudit = reconstructed.Freight;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "VS82 physical production report cannot be reused in another session.");
        _sites.RequireCurrent(session);
        _freightReference.RequireCurrent(session);
        FreightAudit?.RequireCurrent(session);
    }

    private static CanonicalRealizedProductionReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freightReference,
        IReadOnlyList<CanonicalRealizedProductionDeclaration> declarations,
        IReadOnlyList<CanonicalSimultaneousFreightLot> lots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        sites.RequireCurrent(session);
        freightReference.RequireCurrent(session);
        // A freight reference from a different sites policy cannot silently
        // substitute a different demand pressure or source quantity.
        var sourceFlows = sites.Flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity),
            x => x.RawQuantity);
        var demands = sites.Flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        foreach (var offered in freightReference.Offers)
        {
            if (!sourceFlows.TryGetValue((offered.Source, offered.Commodity,
                    offered.Activity), out var cap) ||
                cap != offered.SourcePotentialRaw ||
                !demands.TryGetValue((offered.Buyer, offered.Commodity),
                    out var pressure) ||
                pressure != offered.BuyerPriceDemandSignalRaw)
                throw new ArgumentException(
                    "Remote freight reference belongs to a different physical site or demand snapshot.",
                    nameof(freightReference));
        }
        var unique = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind),
            CanonicalRealizedProductionDeclaration>();
        foreach (var declaration in declarations)
        {
            var key = (declaration.Source, declaration.Commodity,
                declaration.Activity);
            if (!sourceFlows.TryGetValue(key, out var potential) ||
                declaration.ProducedRaw == 0 ||
                declaration.ProducedRaw > potential ||
                declaration.DeliberatelyRetainedRaw > declaration.ProducedRaw ||
                sites.At(declaration.Source)?.ResidentWorkers is not > 0 ||
                !demands.ContainsKey((declaration.Source, declaration.Commodity)) ||
                !unique.TryAdd(key, declaration))
                throw new ArgumentException(
                    "Every realized production must be a unique, live, physically possible source with explicit retention within actual production.",
                    nameof(declarations));
        }

        var remoteTotals = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind),
            BigInteger>();
        foreach (var lot in lots)
        {
            var offer = lot.Offer;
            var key = (offer.Source, offer.Commodity, offer.Activity);
            if (!unique.ContainsKey(key))
                throw new ArgumentException(
                    "A remote lot cannot consume source production not explicitly realized.",
                    nameof(lots));
            remoteTotals.TryGetValue(key, out var prior);
            remoteTotals[key] = prior + lot.RawQuantity;
        }
        var sourceRows = unique.OrderBy(x => x.Key.Item1.Value)
            .ThenBy(x => x.Key.Item2.Value).ThenBy(x => x.Key.Item3)
            .Select(kv =>
            {
                var d = kv.Value;
                var potential = sourceFlows[kv.Key];
                remoteTotals.TryGetValue(kv.Key, out var shipped);
                var available = (BigInteger)d.ProducedRaw -
                    d.DeliberatelyRetainedRaw;
                if (shipped > available)
                    throw new ArgumentException(
                        "Remote shipments and explicit retention cannot exceed realized production.",
                        nameof(lots));
                return new CanonicalRealizedProductionBalance(
                    d.Source, d.Commodity, d.Activity, potential,
                    d.ProducedRaw, potential - d.ProducedRaw,
                    d.DeliberatelyRetainedRaw, shipped,
                    available - shipped);
            }).ToArray();
        // Exactly one local price calculation per (market, commodity),
        // even if several production activities contribute to the commodity.
        var localMarkets = sourceRows.GroupBy(x => (x.Source, x.Commodity))
            .OrderBy(x => x.Key.Source.Value)
            .ThenBy(x => x.Key.Commodity.Value)
            .Select(group =>
            {
                var q = group.Aggregate(BigInteger.Zero, (v, x) =>
                    v + x.OfferedAndLocallyAbsorbedRaw);
                var pressure = new BigInteger(demands[group.Key]);
                var gentle = CanonicalEndogenousLocalPriceComparison.Calculate(
                    q, pressure, 1);
                var steep = CanonicalEndogenousLocalPriceComparison.Calculate(
                    q, pressure, 2);
                return new CanonicalRealizedLocalPriceMarket(
                    group.Key.Source, group.Key.Commodity, q, pressure,
                    gentle.Average, steep.Average,
                    gentle.Revenue, steep.Revenue);
            }).ToArray();

        CanonicalSimultaneousFreightAudit? freight = null;
        if (lots.Count > 0)
        {
            var retention = declarations
                .Where(x => x.DeliberatelyRetainedRaw > 0)
                .Select(x => new CanonicalSimultaneousFreightSourceRetention(
                    x.Source, x.Commodity, x.Activity,
                    x.DeliberatelyRetainedRaw)).ToArray();
            // One batch evaluation, shared physical corridors and service
            // entitlements, never a fictitious local shipment or buyer.
            freight = freightReference.Audit(lots, congestion, retention);
            freight.VerifyIndependent();
        }
        sites.RequireCurrent(session);
        freightReference.RequireCurrent(session);
        return new(sourceRows, localMarkets, freight);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var x = Reconstruct(_session, _sites, _freightReference,
            _declarations, _lots, _congestion);
        if (!Sources.SequenceEqual(x.Balances) ||
            !LocalMarkets.SequenceEqual(x.LocalMarkets) ||
            (FreightAudit is null) != (x.Freight is null) ||
            (FreightAudit is not null &&
                (FreightAudit.TotalAuthenticatedNominalFreightCost.CompareTo(
                    x.Freight!.TotalAuthenticatedNominalFreightCost) != 0 ||
                 !FreightAudit.Sources.SequenceEqual(x.Freight.Sources) ||
                 FreightAudit.CorridorLoads.Count != x.Freight.CorridorLoads.Count ||
                 FreightAudit.CorridorLoads.Any(kv =>
                     !x.Freight.CorridorLoads.TryGetValue(kv.Key, out var q) ||
                     q != kv.Value))))
            throw new InvalidOperationException(
                "Independent VS82 reconstruction diverged in production, local prices or authenticated freight.");
        FreightAudit?.VerifyIndependent();
        RequireCurrent(_session);
    }

    public static CanonicalRealizedProductionAllocation FromSites(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freightReference,
        IEnumerable<CanonicalRealizedProductionDeclaration> productions,
        IEnumerable<CanonicalSimultaneousFreightLot> remoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(freightReference);
        ArgumentNullException.ThrowIfNull(productions);
        ArgumentNullException.ThrowIfNull(remoteLots);
        ArgumentNullException.ThrowIfNull(congestion);
        var d = productions.ToArray();
        var lots = remoteLots.ToArray();
        if (d.Length is < 1 or > MaximumDeclarations ||
            d.Any(x => x is null) ||
            lots.Length > CanonicalSimultaneousFreightReference.MaximumLots ||
            lots.Any(x => x is null || x.Offer is null || x.RawQuantity == 0))
            throw new ArgumentException(
                "VS82 requires 1..12 positive realized production declarations and at most 12 positive remote lots.");
        var ordered = d.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity).ToArray();
        var orderedLots = lots.OrderBy(x => x.Offer.Commodity.Value)
            .ThenBy(x => x.Offer.Activity)
            .ThenBy(x => x.Offer.Source.Value)
            .ThenBy(x => x.Offer.Buyer.Value)
            .ThenBy(x => x.Offer.ServiceId)
            .ThenBy(x => x.Offer.TechnologyTypeId)
            .ThenBy(x => x.Offer.TechnologyCatalogVersion).ToArray();
        var rebuilt = Reconstruct(session, sites, freightReference,
            ordered, orderedLots, congestion);
        var result = new CanonicalRealizedProductionAllocation(
            session, sites, freightReference, ordered, orderedLots,
            congestion, rebuilt);
        result.VerifyIndependent();
        return result;
    }
}
