using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit physical freight equivalence. The client/renderer does not choose
/// units or infer their masses. Catalog is supplied by economic policy.
/// </summary>
public sealed record FoundationCommodityCargoConversion(
    CommodityId Commodity, ulong CargoMicroPerWholeCommodityUnit);

/// <summary>
/// One declared, complete comparable cargo unit and one positive conversion
/// for EACH policy commodity. No implicit "all goods weigh one" rule.
/// </summary>
public sealed class FoundationCommodityCargoConversionPolicy
{
    private readonly Dictionary<CommodityId, ulong> _conversions;
    public string CargoUnitCode { get; }
    public IReadOnlyList<CommodityId> Commodities { get; }

    public FoundationCommodityCargoConversionPolicy(
        string cargoUnitCode, IEnumerable<FoundationCommodityCargoConversion> conversions)
    {
        if (string.IsNullOrWhiteSpace(cargoUnitCode) ||
            cargoUnitCode != cargoUnitCode.Trim())
            throw new ArgumentException(
                "A precise common cargo unit must be supplied.", nameof(cargoUnitCode));
        ArgumentNullException.ThrowIfNull(conversions);
        _conversions = new();
        foreach (var conversion in conversions)
        {
            if (conversion is null || !conversion.Commodity.IsValid ||
                conversion.CargoMicroPerWholeCommodityUnit == 0UL ||
                !_conversions.TryAdd(conversion.Commodity,
                    conversion.CargoMicroPerWholeCommodityUnit))
                throw new ArgumentException(
                    "Conversion entries must be distinct known commodities with " +
                    "strictly positive comparable cargo mass.", nameof(conversions));
        }
        if (_conversions.Count == 0)
            throw new ArgumentException("Cargo conversion catalogue is empty.",
                nameof(conversions));
        CargoUnitCode = cargoUnitCode;
        Commodities = Array.AsReadOnly(_conversions.Keys
            .OrderBy(id => id.Value).ToArray());
    }

    internal ulong CargoMicro(CommodityId commodity, ulong goodsMicro)
    {
        if (!_conversions.TryGetValue(commodity, out var cargoUnitMicro))
            throw new ArgumentException("Unknown cargo commodity.");
        var exact = (UInt128)goodsMicro * cargoUnitMicro;
        // Never lose mass by rounding each split delivery differently.
        if (exact % 1_000_000U != 0U)
            throw new InvalidOperationException(
                "Commodity quantity cannot be converted to exact cargo micro-units.");
        var converted = exact / 1_000_000U;
        if (converted == 0U || converted > ulong.MaxValue)
            throw new OverflowException("Cargo is zero or outside ulong micro-units.");
        return (ulong)converted;
    }
}

/// <summary>
/// Candidate delivery with ACTUAL source activity, declared physical buyer
/// and a fully supplied route. It is not a wallet, purchase order, market
/// price, final accepted sale or an authoritative economic journal event.
/// </summary>
public sealed record FoundationPhysicalCommodityDeliveryCandidate(
    ulong DeliveryOrdinal,
    CivilizationId Seller, PhysicalWorkerOccupation SourceOccupation,
    PhysicalTacticalTileId SourceTile,
    CivilizationId Buyer, PhysicalTacticalTileId BuyerTile,
    CommodityId Commodity, ulong GoodsMicro,
    IReadOnlyList<PhysicalTacticalTileId> RouteTiles);

/// <summary>
/// Read-only validation that proposed allocations cover exactly the physical
/// productive capacity, use actual demand locations, and reconcile quantities
/// to audited physical edge crossings. No price/settlement is claimed.
/// </summary>
public sealed class FoundationPhysicalDeliveryAllocationAudit
{
    public int DeliveryCount { get; }
    public ulong TotalAllocatedGoodsMicro { get; }
    public ulong TotalAvailableGoodsMicro { get; }
    public FoundationPhysicalCommodityMarketInputs MarketInputs { get; }
    public FoundationPhysicalTradeEdgeLoadAudit Freight { get; }

    internal FoundationPhysicalDeliveryAllocationAudit(
        int count, ulong allocated, FoundationPhysicalCommodityMarketInputs market,
        FoundationPhysicalTradeEdgeLoadAudit freight)
    {
        DeliveryCount = count;
        TotalAllocatedGoodsMicro = allocated;
        TotalAvailableGoodsMicro = market.TotalActiveCapacityMicroPerTurn;
        MarketInputs = market;
        Freight = freight;
    }
}

/// <summary>
/// A missing physical buyer or a single unallocated unit rejects the proposed
/// allocation. Shipment selection remains the responsibility of the future
/// physical-market clearing algorithm, not this validation layer.
/// </summary>
public static class FoundationPhysicalDeliveryAllocationAuditor
{
    public static FoundationPhysicalDeliveryAllocationAudit Audit(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationPhysicalCommodityDeliveryCandidate> candidates,
        IEnumerable<FoundationTransportEdgeOwnershipEvidence> ownership)
    {
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(ownership);

        // Real v11 worker census and complete positive demand policy are
        // validated by the existing projector; none are reconstructed here.
        var market = FoundationPhysicalCommodityDemandProjector.Project(
            census, demandPolicy);
        if (!market.Commodities.SequenceEqual(conversionPolicy.Commodities))
            throw new ArgumentException(
                "Demand and freight policies must declare the SAME complete " +
                "commodity catalogue.", nameof(conversionPolicy));

        var fine = incidence.Refinement.FineParameters;
        var supply = market.SupplyLots.ToDictionary(
            lot => (lot.Tile, lot.Owner, lot.Occupation, lot.Commodity),
            lot => lot.CapacityMicroPerTurn);
        var demand = market.DemandByCivilization
            .Select(line => (line.Tile, line.Owner, line.Commodity))
            .ToHashSet();
        var supplied = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Seller,
             PhysicalWorkerOccupation Occupation, CommodityId Commodity), ulong>();
        var deliveries = new List<FoundationFreightDeliveryRouteEvidence>();
        var ordinals = new HashSet<ulong>();
        ulong goods = 0UL;

        foreach (var candidate in candidates)
        {
            if (candidate is null || candidate.DeliveryOrdinal == 0UL ||
                !candidate.Seller.IsValid || !candidate.Buyer.IsValid ||
                !candidate.Commodity.IsValid ||
                !Enum.IsDefined(candidate.SourceOccupation) ||
                candidate.SourceOccupation == PhysicalWorkerOccupation.Unassigned ||
                candidate.GoodsMicro == 0UL || candidate.RouteTiles is null ||
                !ordinals.Add(candidate.DeliveryOrdinal))
                throw new ArgumentException(
                    "A candidate needs a positive unique trade ID, real parties, " +
                    "productive role and positive commodity quantity.",
                    nameof(candidates));
            var source = (candidate.SourceTile, candidate.Seller,
                candidate.SourceOccupation, candidate.Commodity);
            if (!supply.TryGetValue(source, out var available) ||
                !demand.Contains((candidate.BuyerTile, candidate.Buyer,
                    candidate.Commodity)))
                throw new InvalidOperationException(
                    "Candidate has no productive physical seller or real " +
                    "positive-demand buyer at its declared fine tile.");
            if (candidate.SourceTile.FineGoldbergParameters != fine ||
                candidate.BuyerTile.FineGoldbergParameters != fine ||
                candidate.RouteTiles.Count == 0 ||
                candidate.RouteTiles[0] != candidate.SourceTile ||
                candidate.RouteTiles[^1] != candidate.BuyerTile)
                throw new ArgumentException(
                    "Candidate physical route must start at its actual " +
                    "producer and finish at its actual demand tile.",
                    nameof(candidates));

            var accumulated = checked(supplied.GetValueOrDefault(source) +
                candidate.GoodsMicro);
            if (accumulated > available)
                throw new InvalidOperationException(
                    "Allocated quantity exceeds real productive capacity.");
            supplied[source] = accumulated;
            goods = checked(goods + candidate.GoodsMicro);
            var cargo = conversionPolicy.CargoMicro(
                candidate.Commodity, candidate.GoodsMicro);
            deliveries.Add(new FoundationFreightDeliveryRouteEvidence(
                candidate.DeliveryOrdinal, candidate.Commodity, cargo,
                conversionPolicy.CargoUnitCode,
                Array.AsReadOnly(candidate.RouteTiles.ToArray())));
        }

        if (supplied.Count != supply.Count ||
            supply.Any(lot =>
                !supplied.TryGetValue(lot.Key, out var sold) ||
                sold != lot.Value) ||
            goods != market.TotalActiveCapacityMicroPerTurn)
            throw new InvalidOperationException(
                "Every productive worker's available capacity must be " +
                "allocated exactly once; no phantom inventory or lost goods.");

        var freight = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            incidence, conversionPolicy.CargoUnitCode, deliveries, ownership);
        if (freight.DeliveryCount != deliveries.Count ||
            freight.TotalDeliveredCargoMicro == 0UL && deliveries.Count > 0)
            throw new InvalidOperationException(
                "Audited freight differs from the supplied physical deliveries.");

        return new FoundationPhysicalDeliveryAllocationAudit(
            deliveries.Count, goods, market, freight);
    }
}
