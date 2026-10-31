using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>Explicit laboratory scenario. Collections are captured once; no cached purchasing receipt or authority callback.</summary>
public sealed class CanonicalTradeScenario(
    EconomicProductionDemandResult flows, FoodCommodityCatalog catalog,
    IEnumerable<ExperimentalAbstractBuyerAnchor> buyers,
    IEnumerable<ExperimentalAbstractProducerAnchor> producers,
    IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
    ExperimentalAbstractPurchasingPolicy purchasingPolicy,
    IEnumerable<ExperimentalAbstractNominalDeliveredQuote> quotes,
    ExperimentalAbstractPriceFreightCalibrationPolicy calibration,
    string physicalFreightNominalUnitId,
    IEnumerable<ExperimentalSimultaneousFreightLane> lanes,
    IEnumerable<ExperimentalBuiltFreightServiceEvidence> services,
    ExperimentalSharedFreightCongestionPolicy congestion,
    ulong rawQuantum, int maximumConfigurations = 4096)
{
    public EconomicProductionDemandResult Flows { get; } = flows ?? throw new ArgumentNullException(nameof(flows));
    public FoodCommodityCatalog Catalog { get; } = catalog ?? throw new ArgumentNullException(nameof(catalog));
    public IReadOnlyList<ExperimentalAbstractBuyerAnchor> Buyers { get; } = Array.AsReadOnly(buyers.ToArray());
    public IReadOnlyList<ExperimentalAbstractProducerAnchor> Producers { get; } = Array.AsReadOnly(producers.ToArray());
    public IReadOnlyList<ExperimentalBuyerCommodityPrice> PostedPrices { get; } = Array.AsReadOnly(postedPrices.ToArray());
    public ExperimentalAbstractPurchasingPolicy PurchasingPolicy { get; } = purchasingPolicy;
    public IReadOnlyList<ExperimentalAbstractNominalDeliveredQuote> Quotes { get; } = Array.AsReadOnly(quotes.ToArray());
    public ExperimentalAbstractPriceFreightCalibrationPolicy Calibration { get; } = calibration;
    public string PhysicalFreightNominalUnitId { get; } = physicalFreightNominalUnitId;
    public IReadOnlyList<ExperimentalSimultaneousFreightLane> Lanes { get; } = Array.AsReadOnly(lanes.ToArray());
    public IReadOnlyList<ExperimentalBuiltFreightServiceEvidence> Services { get; } = Array.AsReadOnly(services.ToArray());
    public ExperimentalSharedFreightCongestionPolicy Congestion { get; } = congestion;
    public ulong RawQuantum { get; } = rawQuantum;
    public int MaximumConfigurations { get; } = maximumConfigurations;
}

/// <summary>Only a successful live-session pipeline can create this read-only preview.</summary>
public sealed class CanonicalTradePreview
{
    public ExperimentalAbstractPurchasingProjection Purchasing { get; }
    public CanonicalTransportSelection VerifiedSelection { get; }
    public bool CanAuthorizeRealSettlement => false;
    internal CanonicalTradePreview(ExperimentalAbstractPurchasingProjection purchasing,
        CanonicalTransportSelection selection)
    { Purchasing = purchasing; VerifiedSelection = selection; }
    public void RequireCurrent(CanonicalPhysicalTacticalSession session) => VerifiedSelection.RequireCurrent(session);

    internal static CanonicalTradePreview Create(CanonicalPhysicalTacticalSession session, CanonicalTradeScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var initialHash = session.CurrentHash;
        var initialRevision = session.TransportNetwork.Revision;
        var s = scenario;
        // Derive capacity and H from live residents, never caller-certified balances.
        var purchasing = new ExperimentalAbstractPurchasingPowerProjector().Project(session,
            s.Flows, s.Buyers, s.Producers, s.Lanes, s.PostedPrices, s.PurchasingPolicy);
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>();
        foreach (var buyer in purchasing.Buyers) anchors.Add(buyer.Buyer, buyer.Tile);
        foreach (var producer in purchasing.Producers)
        {
            if (anchors.TryGetValue(producer.Producer, out var tile) && tile != producer.Tile)
                throw new InvalidOperationException("Conflicting producer/buyer physical identity.");
            anchors[producer.Producer] = producer.Tile;
        }
        var proof = session.TransportNetwork.Capture(s.Services,
            s.PurchasingPolicy.PolicyId, s.PurchasingPolicy.Version,
            s.Calibration.CalibrationId, s.Calibration.Version, s.Calibration.NominalQuoteUnitId);
        var selected = new CanonicalSimultaneousTransportSelector().Select(session, purchasing,
            s.Flows, s.Catalog, s.Quotes, s.Calibration, s.PhysicalFreightNominalUnitId,
            s.Lanes, s.Congestion, proof, anchors, s.RawQuantum, s.MaximumConfigurations);
        var verified = new CanonicalSimultaneousTransportAuditor().Verify(session, purchasing,
            s.Flows, s.Catalog, s.Quotes, s.Calibration, s.PhysicalFreightNominalUnitId,
            s.Lanes, s.Congestion, proof, anchors, s.RawQuantum, selected.Deliveries,
            selected.EvaluatedConfigurations, s.MaximumConfigurations);
        if (session.CurrentHash != initialHash || session.TransportNetwork.Revision != initialRevision)
            throw new InvalidOperationException("Session changed during trade preview.");
        verified.RequireCurrent(session);
        return new(purchasing, verified);
    }
}
