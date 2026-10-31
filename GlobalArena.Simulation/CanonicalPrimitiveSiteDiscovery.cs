using System.Globalization;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable rates and geographic commodity identities for discovering all
/// currently inhabited primitive sites. No declared site list, accounts,
/// demand balances, political founding or producer ownership.
/// </summary>
public sealed class CanonicalPrimitiveSiteDiscoveryPolicy
{
    public EconomicPrimitiveCommodityMapping Commodities { get; }
    public IReadOnlyList<CanonicalPrimitiveDemandRate> DemandRates { get; }
    public FoodTransportCostFraction PurchasingProductivityFactor { get; }
    internal string Signature { get; }

    public CanonicalPrimitiveSiteDiscoveryPolicy(
        EconomicPrimitiveCommodityMapping commodities,
        IEnumerable<CanonicalPrimitiveDemandRate> demandRates,
        FoodTransportCostFraction purchasingProductivityFactor)
    {
        Commodities = commodities ?? throw new ArgumentNullException(nameof(commodities));
        ArgumentNullException.ThrowIfNull(demandRates);
        PurchasingProductivityFactor = purchasingProductivityFactor ??
            throw new ArgumentNullException(nameof(purchasingProductivityFactor));
        var rates = demandRates.ToArray();
        if (rates.Length == 0 || rates.Any(x => x is null ||
                !x.Commodity.IsValid || x.RawPerResidentWorker <= 0 ||
                x.RawPerResidentWorker > StrategicScalarField.Denominator) ||
            rates.Select(x => x.Commodity).Distinct().Count() != rates.Length)
            throw new ArgumentException(
                "Discovery requires unique positive normalized commodity demand rates.",
                nameof(demandRates));
        DemandRates = Array.AsReadOnly(rates.OrderBy(x => x.Commodity.Value).ToArray());
        // This identity is for deterministic *comparison*, not authorization.
        Signature = string.Join(";", new[] {
            Commodities.Food.Value.ToString(CultureInfo.InvariantCulture),
            Commodities.Wood.Value.ToString(CultureInfo.InvariantCulture),
            Commodities.Stone.Value.ToString(CultureInfo.InvariantCulture),
            PurchasingProductivityFactor.Numerator.ToString(CultureInfo.InvariantCulture),
            PurchasingProductivityFactor.Denominator.ToString(CultureInfo.InvariantCulture)
        }.Concat(DemandRates.Select(x =>
            x.Commodity.Value.ToString(CultureInfo.InvariantCulture) + ":" +
            x.RawPerResidentWorker.ToString(CultureInfo.InvariantCulture))));
    }
}

/// <summary>
/// A stable *local-to-this-world/topology* economic ID is the inhabited fine
/// tile ID, without a city-founding event or sequential ID allocation.
/// Supply is primitive capacity per cycle, not accumulated stock.
/// </summary>
public sealed record CanonicalPrimitiveEconomicSite(
    EconomicPointId Point,
    PhysicalTacticalTileId Tile,
    ulong ResidentWorkers,
    ulong AgriculturalWorkers,
    ulong ForestryWorkers,
    ulong MiningWorkers,
    IReadOnlyList<EconomicSupplyFlow> Supply,
    IReadOnlyList<EconomicDemandFlow> Demand);

public sealed record CanonicalPrimitiveEconomicSiteChange(
    CanonicalPrimitiveEconomicSite Before, CanonicalPrimitiveEconomicSite After);

public sealed record CanonicalPrimitiveSiteDiscoveryDelta(
    WorldStateHash BeforeHash, WorldStateHash AfterHash,
    IReadOnlyList<CanonicalPrimitiveEconomicSite> Added,
    IReadOnlyList<CanonicalPrimitiveEconomicSite> Removed,
    IReadOnlyList<CanonicalPrimitiveEconomicSiteChange> Updated);

/// <summary>
/// Snapshot must come from the live session. It is read-only; old snapshots
/// may be compared with the latest snapshot, not reused as current authority.
/// Neither this inventory nor its aliases authorize real SETTLE.
/// </summary>
public sealed class CanonicalPrimitiveSiteDiscovery
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscoveryPolicy _policy;
    private readonly IReadOnlyDictionary<EconomicPointId, CanonicalPrimitiveEconomicSite> _byId;
    private readonly IReadOnlyDictionary<EconomicPointId, ExperimentalAbstractBuyerAnchor> _buyers;
    private readonly IReadOnlyDictionary<EconomicPointId, ExperimentalAbstractProducerAnchor> _producers;

    public WorldStateHash WorldHash { get; }
    public ulong NetworkRevision { get; }
    public ulong WorkerRevision { get; }
    public ulong MovementTurn { get; }
    public ulong ResidentWorkers { get; }
    public ulong TravellingWorkers { get; }
    public IReadOnlyList<CanonicalPrimitiveEconomicSite> Sites { get; }
    public EconomicProductionDemandResult Flows { get; }
    public IReadOnlyList<ExperimentalAbstractBuyerAnchor> BuyerAnchors { get; }
    public IReadOnlyList<ExperimentalAbstractProducerAnchor> ProducerAnchors { get; }
    public bool CanAuthorizeRealSettlement => false;

    internal CanonicalPrimitiveSiteDiscovery(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscoveryPolicy policy,
        EmergentSettlementProjection settlements,
        EconomicProductionDemandResult flows,
        ExperimentalAbstractBuyerAnchor[] buyers,
        ExperimentalAbstractProducerAnchor[] producers,
        WorldStateHash hash, ulong networkRevision)
    {
        _session = session;
        _policy = policy;
        WorldHash = hash;
        NetworkRevision = networkRevision;
        WorkerRevision = session.State.WorkerRevision;
        MovementTurn = settlements.MovementTurn;
        ResidentWorkers = settlements.TotalResidentWorkers;
        TravellingWorkers = settlements.TotalTravellers;
        Flows = flows;
        BuyerAnchors = Array.AsReadOnly(buyers);
        ProducerAnchors = Array.AsReadOnly(producers);
        _buyers = buyers.ToDictionary(x => x.Buyer);
        _producers = producers.ToDictionary(x => x.Producer);
        var supplies = flows.SupplyFlows.ToLookup(x => x.EconomicPointId);
        var demands = flows.DemandFlows.ToLookup(x => x.EconomicPointId);
        var ordered = settlements.Sites.Select(site =>
        {
            var point = SiteId(site.Tile);
            return new CanonicalPrimitiveEconomicSite(
                point, site.Tile, site.Workers, site.AgriculturalWorkers,
                site.ForestryWorkers, site.MiningWorkers,
                Array.AsReadOnly(supplies[point].ToArray()),
                Array.AsReadOnly(demands[point].ToArray()));
        }).ToArray();
        Sites = Array.AsReadOnly(ordered);
        _byId = ordered.ToDictionary(x => x.Point);
    }

    public static EconomicPointId SiteId(PhysicalTacticalTileId tile)
    {
        if (!tile.IsValid)
            throw new ArgumentException("Only a valid fine tactical tile has a site identity.", nameof(tile));
        return new EconomicPointId(tile.FineStrategicCellId.Value);
    }

    public CanonicalPrimitiveEconomicSite? At(EconomicPointId point) =>
        _byId.GetValueOrDefault(point);

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash != WorldHash ||
            session.TransportNetwork.Revision != NetworkRevision)
            throw new InvalidOperationException(
                "Discovered economic sites are foreign or stale; query the current session.");
    }

    private static bool SameFlows(
        CanonicalPrimitiveEconomicSite a, CanonicalPrimitiveEconomicSite b) =>
        a.ResidentWorkers == b.ResidentWorkers &&
        a.AgriculturalWorkers == b.AgriculturalWorkers &&
        a.ForestryWorkers == b.ForestryWorkers &&
        a.MiningWorkers == b.MiningWorkers &&
        a.Supply.Select(x => (x.CommodityId, x.Activity, x.RawQuantity))
            .SequenceEqual(b.Supply.Select(x => (x.CommodityId, x.Activity, x.RawQuantity))) &&
        a.Demand.Select(x => (x.CommodityId, x.RawQuantity))
            .SequenceEqual(b.Demand.Select(x => (x.CommodityId, x.RawQuantity)));

    public CanonicalPrimitiveSiteDiscoveryDelta ChangesSince(
        CanonicalPrimitiveSiteDiscovery previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        RequireCurrent(_session);
        if (!ReferenceEquals(previous._session, _session) ||
            previous._policy.Signature != _policy.Signature ||
            previous.MovementTurn > MovementTurn ||
            previous.WorkerRevision > WorkerRevision)
            throw new InvalidOperationException(
                "Site discovery deltas require chronological snapshots of one session and policy.");

        var added = Sites.Where(x => !previous._byId.ContainsKey(x.Point)).ToArray();
        var removed = previous.Sites.Where(x => !_byId.ContainsKey(x.Point)).ToArray();
        var changed = Sites.Where(x => previous._byId.TryGetValue(x.Point, out var before) &&
                !SameFlows(before, x))
            .Select(x => new CanonicalPrimitiveEconomicSiteChange(previous._byId[x.Point], x))
            .ToArray();
        return new CanonicalPrimitiveSiteDiscoveryDelta(
            previous.WorldHash, WorldHash, Array.AsReadOnly(added),
            Array.AsReadOnly(removed), Array.AsReadOnly(changed));
    }

    /// <summary>
    /// Discover endpoints automatically from live sites. The caller must still
    /// provide REAL authorized transport options, prices and institutionally
    /// declared services. No site list, invented path or fallback route.
    /// Only the endpoints used by these lanes enter this bounded preview.
    /// </summary>
    public CanonicalLivePrimitiveTradePreview PreviewTrade(
        FoodCommodityCatalog catalog,
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
        RequireCurrent(_session);
        ArgumentNullException.ThrowIfNull(lanes);
        var selected = lanes.ToArray();
        if (selected.Length == 0 || selected.Any(x => x is null))
            throw new ArgumentException("At least one existing physical trade lane is required.", nameof(lanes));
        var buyers = selected.Select(x => x.Buyer).Distinct().OrderBy(x => x.Value)
            .Select(id => _buyers.TryGetValue(id, out var anchor) ? anchor :
                throw new InvalidOperationException("Lane buyer is not a live discovered site."))
            .ToArray();
        var producers = selected.Select(x => x.Source).Distinct().OrderBy(x => x.Value)
            .Select(id => _producers.TryGetValue(id, out var anchor) ? anchor :
                throw new InvalidOperationException("Lane source has no current primitive supply."))
            .ToArray();
        var scenario = new CanonicalLivePrimitiveTradeScenario(
            catalog, _policy.Commodities, buyers, producers, _policy.DemandRates,
            postedPrices, purchasingPolicy, quotes, calibration,
            physicalFreightNominalUnitId, selected, services, congestion,
            rawQuantum, maximumConfigurations);
        return _session.PreviewLivePrimitiveTrade(scenario);
    }

    internal static CanonicalPrimitiveSiteDiscovery Create(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscoveryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(policy);
        var hash = session.CurrentHash;
        var revision = session.TransportNetwork.Revision;
        var settlements = EmergentSettlementProjection.FromSession(session);
        var buyers = settlements.Sites.Select(site =>
            new ExperimentalAbstractBuyerAnchor(SiteId(site.Tile),
                site.Tile, policy.PurchasingProductivityFactor)).ToArray();
        var possibleProducers = settlements.Sites.Select(site =>
            new ExperimentalAbstractProducerAnchor(SiteId(site.Tile), site.Tile)).ToArray();
        var flows = CanonicalLivePrimitiveTradeProjector.BuildFlows(
            session, policy.Commodities, buyers, possibleProducers,
            policy.DemandRates, settlements);
        var productive = flows.SupplyFlows.Select(x => x.EconomicPointId).ToHashSet();
        var producers = possibleProducers.Where(x => productive.Contains(x.Producer)).ToArray();
        if (session.CurrentHash != hash ||
            session.TransportNetwork.Revision != revision)
            throw new InvalidOperationException("Physical session changed during site discovery.");
        var result = new CanonicalPrimitiveSiteDiscovery(
            session, policy, settlements, flows, buyers, producers, hash, revision);
        result.RequireCurrent(session);
        return result;
    }
}
