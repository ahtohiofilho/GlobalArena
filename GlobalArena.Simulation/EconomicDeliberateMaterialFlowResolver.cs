using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit decision to withhold a portion of this turn's production from
/// the commercial market at its origin. This is a physical quantity, NOT
/// EconomicTurnSourceReservation (which is a producer unit-price input).
/// </summary>
public sealed class EconomicMaterialOriginHoldOrder
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public EconomicActivityKind Activity { get; }
    public ulong RawQuantity { get; }

    public EconomicMaterialOriginHoldOrder(
        EconomicPointId economicPointId, CommodityId commodityId,
        EconomicActivityKind activity, ulong rawQuantity)
    {
        if (!economicPointId.IsValid || !commodityId.IsValid)
            throw new ArgumentException("Origin hold requires valid point and commodity IDs.");
        if (rawQuantity == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawQuantity));
        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        Activity = activity;
        RawQuantity = rawQuantity;
    }
}

/// <summary>
/// Additional demand intended to acquire material for a destination reserve.
/// Merely requesting it is NOT evidence of market allocation, payment,
/// physical delivery or a persistent inventory credit.
/// </summary>
public sealed class EconomicMaterialDestinationAcquisitionOrder
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawQuantity { get; }

    public EconomicMaterialDestinationAcquisitionOrder(
        EconomicPointId economicPointId, CommodityId commodityId, ulong rawQuantity)
    {
        if (!economicPointId.IsValid || !commodityId.IsValid)
            throw new ArgumentException("Destination acquisition requires valid IDs.");
        if (rawQuantity == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawQuantity));
        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawQuantity = rawQuantity;
    }
}

/// <summary>
/// A pure pre-market projection. The original flows and WorldState are
/// untouched; selected hold/acquisition intents are retained for later
/// authoritative storage/delivery reconciliation.
/// </summary>
public sealed class EconomicDeliberateMaterialFlowResult
{
    public EconomicProductionDemandResult MarketFlows { get; }
    public IReadOnlyList<EconomicMaterialOriginHoldOrder> OriginHolds { get; }
    public IReadOnlyList<EconomicMaterialDestinationAcquisitionOrder> DestinationAcquisitions { get; }

    internal EconomicDeliberateMaterialFlowResult(
        EconomicProductionDemandResult marketFlows,
        EconomicMaterialOriginHoldOrder[] originHolds,
        EconomicMaterialDestinationAcquisitionOrder[] destinationAcquisitions)
    {
        MarketFlows = marketFlows;
        OriginHolds = Array.AsReadOnly(originHolds);
        DestinationAcquisitions = Array.AsReadOnly(destinationAcquisitions);
    }
}

/// <summary>
/// G3-A: deterministic, owner-scoped adaptation of already resolved physical
/// supply and consumer demand. No new commodities, warehouse simulation,
/// settlement, persistent stock, command, or production are invented.
/// </summary>
public sealed class EconomicDeliberateMaterialFlowResolver
{
    public EconomicDeliberateMaterialFlowResult Resolve(
        WorldState worldState,
        CivilizationId issuingCivilization,
        EconomicProductionDemandResult productionDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds,
        IEnumerable<EconomicMaterialDestinationAcquisitionOrder> destinationAcquisitions)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(productionDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        ArgumentNullException.ThrowIfNull(destinationAcquisitions);
        if (!issuingCivilization.IsValid ||
            !worldState.Civilizations.Civilizations.Contains(issuingCivilization))
            throw new ArgumentException("Issuer must be a recorded civilization.",
                nameof(issuingCivilization));

        var points = worldState.Economy.EconomicPoints
            .ToDictionary(point => point.Id);
        var supplies = productionDemand.SupplyFlows
            .ToDictionary(row => (row.EconomicPointId, row.CommodityId, row.Activity));
        var demands = productionDemand.DemandFlows
            .ToDictionary(row => (row.EconomicPointId, row.CommodityId), row => row.RawQuantity);

        var holdRows = originHolds.ToArray();
        var acquireRows = destinationAcquisitions.ToArray();
        if (holdRows.Any(row => row is null) ||
            acquireRows.Any(row => row is null))
            throw new ArgumentException("Orders cannot contain null entries.");

        holdRows = holdRows
            .OrderBy(row => row.EconomicPointId.Value)
            .ThenBy(row => row.CommodityId.Value)
            .ThenBy(row => (byte)row.Activity)
            .ToArray();
        acquireRows = acquireRows
            .OrderBy(row => row.EconomicPointId.Value)
            .ThenBy(row => row.CommodityId.Value)
            .ToArray();

        var heldBySupply = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), ulong>();
        foreach (var order in holdRows)
        {
            var key = (order.EconomicPointId, order.CommodityId, order.Activity);
            if (!supplies.TryGetValue(key, out var supply))
                throw new ArgumentException(
                    "Origin hold must match a real supply point, commodity and activity.",
                    nameof(originHolds));
            RequireOwner(order.EconomicPointId);
            if (!heldBySupply.TryAdd(key, order.RawQuantity))
                throw new ArgumentException("Duplicate origin hold key.", nameof(originHolds));
            if (order.RawQuantity > supply.RawQuantity)
                throw new ArgumentOutOfRangeException(
                    nameof(originHolds), "Cannot hold more than produced.");
        }

        var acquiredByPoint = new HashSet<(EconomicPointId, CommodityId)>();
        foreach (var order in acquireRows)
        {
            RequireOwner(order.EconomicPointId);
            var key = (order.EconomicPointId, order.CommodityId);
            if (!acquiredByPoint.Add(key))
                throw new ArgumentException(
                    "Duplicate destination acquisition key.", nameof(destinationAcquisitions));
            demands.TryGetValue(key, out var existingDemand);
            demands[key] = checked(existingDemand + order.RawQuantity);
        }

        var marketSupplies = new List<EconomicSupplyFlow>();
        foreach (var supply in productionDemand.SupplyFlows)
        {
            heldBySupply.TryGetValue(
                (supply.EconomicPointId, supply.CommodityId, supply.Activity),
                out var hold);
            var offered = supply.RawQuantity - hold;
            if (offered != 0UL)
                marketSupplies.Add(new EconomicSupplyFlow(
                    supply.EconomicPointId, supply.CommodityId, supply.Activity, offered));
        }

        var marketDemands = demands
            .OrderBy(row => row.Key.EconomicPointId.Value)
            .ThenBy(row => row.Key.CommodityId.Value)
            .Select(row => new EconomicDemandFlow(
                row.Key.EconomicPointId, row.Key.CommodityId, row.Value))
            .ToArray();

        return new EconomicDeliberateMaterialFlowResult(
            new EconomicProductionDemandResult(marketSupplies, marketDemands),
            holdRows, acquireRows);

        void RequireOwner(EconomicPointId pointId)
        {
            if (!points.TryGetValue(pointId, out var point) ||
                point.Owner != issuingCivilization)
                throw new InvalidOperationException(
                    "Material order requires an economic point owned by its issuer.");
        }
    }
}
