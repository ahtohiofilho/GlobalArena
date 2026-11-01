using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A hypothetical declaration of output actually PLACED FOR SALE, as
/// distinguished from physically available primitive production potential.
/// This is not inventory, a supply mutation, sale or extraction command.
/// </summary>
public sealed record CanonicalPrimitiveExplicitSourceOffer(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong OfferedRawQuantity);

public sealed record CanonicalPrimitiveOfferSourceGap(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong PhysicalPotentialRaw,
    ulong HypotheticallyOfferedRaw, ulong Vs76AllocatedRaw,
    ulong OfferedButUnallocatedRaw, ulong PotentialNotOfferedRaw);

public sealed record CanonicalPrimitiveOfferBuyerGap(
    EconomicPointId Buyer, CommodityId Commodity,
    ulong PhysicalDemandRaw, ulong Vs76AllocatedRaw,
    ulong UnmetPhysicalDemandRaw);

/// <summary>
/// All aggregates refer to ONE commodity, never sums across different goods.
/// Missing demand is a NECESSARY lower bound, not sufficient evidence that
/// freight, posted price, H or institutional access could settle the remainder.
/// </summary>
public sealed record CanonicalPrimitiveOfferCommodityGap(
    CommodityId Commodity, BigInteger OfferedButUnallocatedRaw,
    BigInteger UnmetPhysicalDemandRaw,
    BigInteger NecessaryAdditionalPhysicalDemandRaw);

public sealed record CanonicalPrimitiveOfferAbsorptionSnapshot(
    IReadOnlyList<CanonicalPrimitiveOfferSourceGap> Sources,
    IReadOnlyList<CanonicalPrimitiveOfferBuyerGap> Buyers,
    IReadOnlyList<CanonicalPrimitiveOfferCommodityGap> Commodities);

/// <summary>
/// VS77: read-only reconciliation of one authenticated, previously selected
/// VS76 outcome against a caller's explicitly DECLARED hypothetical offers.
/// The selector is not changed or re-solved. In particular, the existence
/// of unsold offers NEVER authorizes fabricated buyers, lower prices,
/// bank balances, physical inventories or actual consumption.
/// </summary>
public sealed class CanonicalPrimitiveOfferAbsorptionDiagnostics
{
    public const int MaximumSourceOffers = 12;

    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPrimitiveIntegratedSelection _selection;
    private readonly CanonicalPrimitiveExplicitSourceOffer[] _offers;

    public IReadOnlyList<CanonicalPrimitiveExplicitSourceOffer> DeclaredOffers { get; }
    public CanonicalPrimitiveOfferAbsorptionSnapshot Snapshot { get; }
    public bool AllDeclaredOffersHypotheticallyAllocated =>
        Snapshot.Sources.All(x => x.OfferedButUnallocatedRaw == 0UL);
    public bool HasNecessaryPhysicalDemandShortfall =>
        Snapshot.Commodities.Any(x => x.NecessaryAdditionalPhysicalDemandRaw > 0);
    public bool CanAuthorizeRealSettlement => false;
    public bool IsActualSale => false;
    public bool IsActualInventory => false;
    public bool IsMarginalPriceCurveCalibrated => false;
    public bool IsCommitted => false;

    private CanonicalPrimitiveOfferAbsorptionDiagnostics(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveIntegratedSelection selection,
        CanonicalPrimitiveExplicitSourceOffer[] offers,
        CanonicalPrimitiveOfferAbsorptionSnapshot snapshot)
    {
        _session = session;
        _portfolio = portfolio;
        _selection = selection;
        _offers = (CanonicalPrimitiveExplicitSourceOffer[])offers.Clone();
        DeclaredOffers = Array.AsReadOnly(_offers);
        Snapshot = snapshot;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Offer/absorption audit belongs to another physical session.");
        _portfolio.RequireCurrent(session);
        _selection.RequireCurrent(session);
    }

    private static CanonicalPrimitiveOfferAbsorptionSnapshot Rebuild(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveIntegratedSelection selection,
        IReadOnlyList<CanonicalPrimitiveExplicitSourceOffer> offers)
    {
        var flows = portfolio.CurrentSites.Flows;
        var sourceCaps = flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity),
            x => x.RawQuantity);
        var buyerCaps = flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);

        var offered = offers.ToDictionary(
            x => (x.Source, x.Commodity, x.Activity),
            x => x.OfferedRawQuantity);
        var selectedSources = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>();
        var selectedBuyers = new Dictionary<
            (EconomicPointId, CommodityId), BigInteger>();

        foreach (var lot in selection.Lots)
        {
            var sourceKey = (lot.Option.Source, lot.Option.Commodity,
                lot.Option.Activity);
            var buyerKey = (lot.Option.Buyer, lot.Option.Commodity);
            if (!sourceCaps.ContainsKey(sourceKey) ||
                !buyerCaps.ContainsKey(buyerKey))
                throw new InvalidOperationException(
                    "Selected output has no authenticated physical source or demand.");
            selectedSources.TryGetValue(sourceKey, out var usedSource);
            selectedBuyers.TryGetValue(buyerKey, out var usedBuyer);
            selectedSources[sourceKey] = usedSource + lot.RawQuantity;
            selectedBuyers[buyerKey] = usedBuyer + lot.RawQuantity;
        }

        var sourceRows = new List<CanonicalPrimitiveOfferSourceGap>();
        foreach (var (key, potential) in sourceCaps
                     .OrderBy(x => x.Key.EconomicPointId.Value)
                     .ThenBy(x => x.Key.CommodityId.Value)
                     .ThenBy(x => x.Key.Activity))
        {
            offered.TryGetValue(key, out var declared);
            selectedSources.TryGetValue(key, out var allocated);
            if (declared > potential || allocated > declared)
                throw new ArgumentException(
                    "Every selected lot must be covered by a declared offer, and no offer may exceed physical potential.",
                    nameof(offers));
            sourceRows.Add(new(key.EconomicPointId, key.CommodityId,
                key.Activity, potential, declared, checked((ulong)allocated),
                checked(declared - (ulong)allocated),
                checked(potential - declared)));
        }

        if (offered.Keys.Any(x => !sourceCaps.ContainsKey(x)) ||
            selectedSources.Any(x => !sourceCaps.ContainsKey(x.Key)))
            throw new ArgumentException(
                "An offer refers to a nonphysical or unauthenticated source.",
                nameof(offers));

        var buyerRows = new List<CanonicalPrimitiveOfferBuyerGap>();
        foreach (var (key, demand) in buyerCaps
                     .OrderBy(x => x.Key.EconomicPointId.Value)
                     .ThenBy(x => x.Key.CommodityId.Value))
        {
            selectedBuyers.TryGetValue(key, out var allocated);
            if (allocated > demand)
                throw new InvalidOperationException(
                    "The VS76 selection oversubscribes physical buyer demand.");
            buyerRows.Add(new(key.EconomicPointId, key.CommodityId,
                demand, checked((ulong)allocated),
                checked(demand - (ulong)allocated)));
        }

        var goods = sourceRows.Select(x => x.Commodity)
            .Concat(buyerRows.Select(x => x.Commodity))
            .Distinct().OrderBy(x => x.Value);
        var commodityRows = goods.Select(commodity =>
        {
            var unallocated = sourceRows.Where(x => x.Commodity == commodity)
                .Aggregate(BigInteger.Zero,
                    (sum, x) => sum + x.OfferedButUnallocatedRaw);
            var unmet = buyerRows.Where(x => x.Commodity == commodity)
                .Aggregate(BigInteger.Zero,
                    (sum, x) => sum + x.UnmetPhysicalDemandRaw);
            return new CanonicalPrimitiveOfferCommodityGap(commodity,
                unallocated, unmet, BigInteger.Max(BigInteger.Zero,
                    unallocated - unmet));
        }).ToArray();

        return new CanonicalPrimitiveOfferAbsorptionSnapshot(
            Array.AsReadOnly(sourceRows.ToArray()),
            Array.AsReadOnly(buyerRows.ToArray()),
            Array.AsReadOnly(commodityRows));
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _selection.VerifyIndependent();
        var replay = Rebuild(_portfolio, _selection, _offers);
        if (!Snapshot.Sources.SequenceEqual(replay.Sources) ||
            !Snapshot.Buyers.SequenceEqual(replay.Buyers) ||
            !Snapshot.Commodities.SequenceEqual(replay.Commodities))
            throw new InvalidOperationException(
                "Explicit offer and residual absorption gap failed independent reconciliation.");
        RequireCurrent(_session);
    }

    internal static CanonicalPrimitiveOfferAbsorptionDiagnostics FromSelection(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveIntegratedSelection selection,
        IEnumerable<CanonicalPrimitiveExplicitSourceOffer> input)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(input);
        portfolio.RequireCurrent(session);
        selection.RequireCurrent(session);
        selection.VerifyIndependent();
        var offers = input.ToArray();
        if (offers.Length > MaximumSourceOffers ||
            offers.Any(x => x is null || x.OfferedRawQuantity == 0UL ||
                !x.Source.IsValid || !x.Commodity.IsValid) ||
            offers.Select(x => (x.Source, x.Commodity, x.Activity))
                .Distinct().Count() != offers.Length)
            throw new ArgumentException(
                "At most 12 unique positive and authentic source offers may be declared.",
                nameof(input));
        var ordered = offers.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity).ToArray();
        var snapshot = Rebuild(portfolio, selection, ordered);
        var result = new CanonicalPrimitiveOfferAbsorptionDiagnostics(
            session, portfolio, selection, ordered, snapshot);
        result.VerifyIndependent();
        return result;
    }
}
