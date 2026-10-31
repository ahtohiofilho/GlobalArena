using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A physical resident generates recurring demand for each declared primitive
/// commodity. This is a *rate*, never an externally supplied demand balance.
/// </summary>
public sealed record CanonicalPrimitiveDemandRate(
    CommodityId Commodity, long RawPerResidentWorker);

/// <summary>
/// Route identity and attested whole-service options, without cached flow
/// quantities. The physical quantities are reconstructed at query time.
/// </summary>
public sealed class CanonicalPrimitiveTradeLane
{
    public EconomicPointId Source { get; }
    public EconomicPointId Buyer { get; }
    public CommodityId Commodity { get; }
    public EconomicActivityKind Activity { get; }
    public IReadOnlyList<ExperimentalSharedFreightOption> Services { get; }

    public CanonicalPrimitiveTradeLane(EconomicPointId source, EconomicPointId buyer,
        CommodityId commodity, EconomicActivityKind activity,
        IEnumerable<ExperimentalSharedFreightOption> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!source.IsValid || !buyer.IsValid || !commodity.IsValid ||
            activity is not (EconomicActivityKind.Agriculture or
                EconomicActivityKind.Forestry or EconomicActivityKind.Mining))
            throw new ArgumentException("Primitive lane requires valid canonical identities.");
        var items = services.ToArray();
        if (items.Length == 0 || items.Any(x => x is null) ||
            items.Select(x => x.ServiceId).Distinct().Count() != items.Length ||
            items.Any(x => x.RouteKey.SourceEconomicPointId != source ||
                x.RouteKey.DestinationEconomicPointId != buyer))
            throw new ArgumentException("Primitive lane services must be unique and route-bound.");
        Source = source;
        Buyer = buyer;
        Commodity = commodity;
        Activity = activity;
        Services = Array.AsReadOnly(items.OrderBy(x => x.ServiceId).ToArray());
    }
}

/// <summary>
/// Explicit laboratory identity bindings, prices and physical services.
/// No host-supplied EconomicProductionDemandResult or flow quantities.
/// All collections are copied before the session is queried.
/// </summary>
public sealed class CanonicalLivePrimitiveTradeScenario
{
    public FoodCommodityCatalog Catalog { get; }
    public EconomicPrimitiveCommodityMapping Commodities { get; }
    public IReadOnlyList<ExperimentalAbstractBuyerAnchor> Buyers { get; }
    public IReadOnlyList<ExperimentalAbstractProducerAnchor> Producers { get; }
    public IReadOnlyList<CanonicalPrimitiveDemandRate> DemandRates { get; }
    public IReadOnlyList<ExperimentalBuyerCommodityPrice> PostedPrices { get; }
    public ExperimentalAbstractPurchasingPolicy PurchasingPolicy { get; }
    public IReadOnlyList<ExperimentalAbstractNominalDeliveredQuote> Quotes { get; }
    public ExperimentalAbstractPriceFreightCalibrationPolicy Calibration { get; }
    public string PhysicalFreightNominalUnitId { get; }
    public IReadOnlyList<CanonicalPrimitiveTradeLane> Lanes { get; }
    public IReadOnlyList<ExperimentalBuiltFreightServiceEvidence> Services { get; }
    public ExperimentalSharedFreightCongestionPolicy Congestion { get; }
    public ulong RawQuantum { get; }
    public int MaximumConfigurations { get; }

    public CanonicalLivePrimitiveTradeScenario(
        FoodCommodityCatalog catalog, EconomicPrimitiveCommodityMapping commodities,
        IEnumerable<ExperimentalAbstractBuyerAnchor> buyers,
        IEnumerable<ExperimentalAbstractProducerAnchor> producers,
        IEnumerable<CanonicalPrimitiveDemandRate> demandRates,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> quotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibration,
        string physicalFreightNominalUnitId,
        IEnumerable<CanonicalPrimitiveTradeLane> lanes,
        IEnumerable<ExperimentalBuiltFreightServiceEvidence> services,
        ExperimentalSharedFreightCongestionPolicy congestion,
        ulong rawQuantum, int maximumConfigurations = 4096)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Commodities = commodities ?? throw new ArgumentNullException(nameof(commodities));
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(producers);
        ArgumentNullException.ThrowIfNull(demandRates);
        ArgumentNullException.ThrowIfNull(postedPrices);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(services);
        PurchasingPolicy = purchasingPolicy ?? throw new ArgumentNullException(nameof(purchasingPolicy));
        Calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
        Congestion = congestion ?? throw new ArgumentNullException(nameof(congestion));
        Buyers = Array.AsReadOnly(buyers.ToArray());
        Producers = Array.AsReadOnly(producers.ToArray());
        DemandRates = Array.AsReadOnly(demandRates.ToArray());
        PostedPrices = Array.AsReadOnly(postedPrices.ToArray());
        Quotes = Array.AsReadOnly(quotes.ToArray());
        Lanes = Array.AsReadOnly(lanes.ToArray());
        Services = Array.AsReadOnly(services.ToArray());
        PhysicalFreightNominalUnitId = physicalFreightNominalUnitId;
        RawQuantum = rawQuantum;
        MaximumConfigurations = maximumConfigurations;

        if (!Catalog.Contains(Commodities.Food) ||
            !Catalog.Contains(Commodities.Wood) ||
            !Catalog.Contains(Commodities.Stone))
            throw new ArgumentException("The three primitive commodities need physical catalog definitions.");
        if (Buyers.Count == 0 || Producers.Count == 0 || Lanes.Count == 0 ||
            DemandRates.Count == 0 || Buyers.Any(x => x is null) ||
            Producers.Any(x => x is null) || Lanes.Any(x => x is null) ||
            Services.Any(x => x is null) || PostedPrices.Any(x => x is null) ||
            Quotes.Any(x => x is null) ||
            DemandRates.Any(x => x is null || !x.Commodity.IsValid ||
                x.RawPerResidentWorker <= 0 ||
                x.RawPerResidentWorker > StrategicScalarField.Denominator) ||
            DemandRates.Select(x => x.Commodity).Distinct().Count() != DemandRates.Count)
            throw new ArgumentException("Invalid primitive trade scenario.");
        if (Buyers.Select(x => x.Buyer).Distinct().Count() != Buyers.Count ||
            Buyers.Select(x => x.Tile).Distinct().Count() != Buyers.Count ||
            Producers.Select(x => x.Producer).Distinct().Count() != Producers.Count ||
            Producers.Select(x => x.Tile).Distinct().Count() != Producers.Count)
            throw new ArgumentException("Physical site identities cannot be duplicated.");
        if (Lanes.Select(x => (x.Source, x.Buyer, x.Commodity, x.Activity))
                .Distinct().Count() != Lanes.Count)
            throw new ArgumentException("Primitive lanes cannot be duplicated.");
        foreach (var buyer in Buyers)
            foreach (var producer in Producers)
                if ((buyer.Buyer == producer.Producer && buyer.Tile != producer.Tile) ||
                    (buyer.Tile == producer.Tile && buyer.Buyer != producer.Producer))
                    throw new ArgumentException("One inhabited site cannot use conflicting economic identities.");
        if (RawQuantum == 0 || MaximumConfigurations is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(rawQuantum));
    }
}

/// <summary>
/// Read-only primitive physical flow result plus the independently audited
/// VS60 trade preview. This object never authorizes settlement.
/// </summary>
public sealed class CanonicalLivePrimitiveTradePreview
{
    public EconomicProductionDemandResult Flows { get; }
    public CanonicalTradePreview Trade { get; }
    public bool CanAuthorizeRealSettlement => false;

    internal CanonicalLivePrimitiveTradePreview(
        EconomicProductionDemandResult flows, CanonicalTradePreview trade)
    {
        Flows = flows;
        Trade = trade;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session) =>
        Trade.RequireCurrent(session);
}

/// <summary>
/// Rebind each declared lane to *live* production and resident demand.
/// The geographic yield is the same PrimitiveYieldAt used by the physical
/// economic cycle; travellers never produce or inflate resident demand.
/// No site is founded and no commerce or cash operation is committed.
/// </summary>
internal static class CanonicalLivePrimitiveTradeProjector
{
    // One canonical physical flow kernel shared by VS61 directed routes and VS62 global discovery.
    // Demand uses resident workers, not travellers or another population counter.
    internal static EconomicProductionDemandResult BuildFlows(
        CanonicalPhysicalTacticalSession session,
        EconomicPrimitiveCommodityMapping commodities,
        IReadOnlyList<ExperimentalAbstractBuyerAnchor> buyers,
        IReadOnlyList<ExperimentalAbstractProducerAnchor> producers,
        IReadOnlyList<CanonicalPrimitiveDemandRate> demandRates,
        EmergentSettlementProjection? settlements = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(commodities);
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(producers);
        ArgumentNullException.ThrowIfNull(demandRates);
        var places = (settlements ?? EmergentSettlementProjection.FromSession(session)).Sites
            .ToDictionary(x => x.Tile);
        var supply = new List<EconomicSupplyFlow>();
        var demand = new List<EconomicDemandFlow>();

        foreach (var producer in producers)
        {
            if (!places.ContainsKey(producer.Tile))
                throw new InvalidOperationException("An offered producer is not currently inhabited.");
            foreach (var allocation in session.State.Allocations
                .Where(x => x.TileId == producer.Tile)
                .OrderBy(x => (byte)x.Activity))
            {
                var commodity = allocation.Activity switch
                {
                    EconomicActivityKind.Agriculture => commodities.Food,
                    EconomicActivityKind.Forestry => commodities.Wood,
                    EconomicActivityKind.Mining => commodities.Stone,
                    _ => throw new InvalidOperationException("Unknown physical activity.")
                };
                var rate = session.PrimitiveYieldAt(producer.Tile, allocation.Activity);
                if (rate <= 0 || allocation.Workers == 0)
                    continue;
                supply.Add(new EconomicSupplyFlow(producer.Producer, commodity,
                    allocation.Activity, checked(allocation.Workers * (ulong)rate)));
            }
        }

        foreach (var buyer in buyers)
        {
            if (!places.TryGetValue(buyer.Tile, out var site))
                throw new InvalidOperationException("An offered buyer is not currently inhabited.");
            foreach (var rate in demandRates)
                demand.Add(new EconomicDemandFlow(buyer.Buyer, rate.Commodity,
                    checked(site.Workers * (ulong)rate.RawPerResidentWorker)));
        }

        return new EconomicProductionDemandResult(supply, demand);
    }

    internal static CanonicalLivePrimitiveTradePreview Create(
        CanonicalPhysicalTacticalSession session, CanonicalLivePrimitiveTradeScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(scenario);
        var hash = session.CurrentHash;
        var revision = session.TransportNetwork.Revision;
        var flows = BuildFlows(session, scenario.Commodities,
            scenario.Buyers, scenario.Producers, scenario.DemandRates);
        var supplyByKey = flows.SupplyFlows.ToDictionary(x =>
            (x.EconomicPointId, x.CommodityId, x.Activity));
        var demandByKey = flows.DemandFlows.ToDictionary(x =>
            (x.EconomicPointId, x.CommodityId));
        var lanes = scenario.Lanes.Select(lane =>
        {
            if (!supplyByKey.TryGetValue((lane.Source, lane.Commodity, lane.Activity),
                    out var offered) ||
                !demandByKey.TryGetValue((lane.Buyer, lane.Commodity), out var requested))
                throw new InvalidOperationException(
                    "Offered lane has no current physical production or resident demand.");
            return new ExperimentalSimultaneousFreightLane(
                offered, requested, lane.Services);
        }).ToArray();

        var wrapped = new CanonicalTradeScenario(flows, scenario.Catalog,
            scenario.Buyers, scenario.Producers, scenario.PostedPrices,
            scenario.PurchasingPolicy, scenario.Quotes, scenario.Calibration,
            scenario.PhysicalFreightNominalUnitId, lanes, scenario.Services,
            scenario.Congestion, scenario.RawQuantum, scenario.MaximumConfigurations);
        if (session.CurrentHash != hash || session.TransportNetwork.Revision != revision)
            throw new InvalidOperationException("Session changed during primitive flow projection.");
        var trade = session.PreviewTrade(wrapped); // VS52 -> VS57 -> VS58 -> VS59 -> VS60
        if (session.CurrentHash != hash || session.TransportNetwork.Revision != revision)
            throw new InvalidOperationException("Session changed during primitive trade preview.");
        trade.RequireCurrent(session);
        return new(flows, trade);
    }
}
