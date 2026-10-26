using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One reference-market trade. Commodity value is integrated from its
/// destination's changing marginal value curve. Freight is charged once
/// using its immutable pre-turn quote; operational congestion is not added.
/// This is a commercial allocation, not a physical-delivery attestation.
/// </summary>
public sealed class EconomicTurnLockedFreightTrade
{
    public ulong DecisionOrdinal { get; }
    public ulong TurnOrdinal { get; }
    public EconomicPointId SourceEconomicPointId { get; }
    public EconomicActivityKind SourceActivity { get; }
    public EconomicPointId DestinationEconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawQuantity { get; }
    public FoodTransportCostFraction RawMarginalCommodityUnitValueAtDecision { get; }
    public EconomicTurnFreightQuote FreightQuote { get; }
    public FoodTransportCostFraction RawCommodityValue { get; }
    public FoodTransportCostFraction RawFreightCharge { get; }
    public FoodTransportCostFraction RawProducerReservationCost { get; }
    public FoodTransportCostFraction RawNetReturn { get; }

    internal EconomicTurnLockedFreightTrade(
        ulong decisionOrdinal,
        ulong turnOrdinal,
        EconomicSupplyFlow source,
        EconomicDemandFlow destination,
        ulong quantity,
        FoodTransportCostFraction marginalCommodityUnitValue,
        EconomicTurnFreightQuote freightQuote,
        FoodTransportCostFraction commodityValue,
        FoodTransportCostFraction freightCharge,
        FoodTransportCostFraction reservationCost,
        FoodTransportCostFraction netReturn)
    {
        DecisionOrdinal = decisionOrdinal;
        TurnOrdinal = turnOrdinal;
        SourceEconomicPointId = source.EconomicPointId;
        SourceActivity = source.Activity;
        DestinationEconomicPointId = destination.EconomicPointId;
        CommodityId = source.CommodityId;
        RawQuantity = quantity;
        RawMarginalCommodityUnitValueAtDecision = marginalCommodityUnitValue;
        FreightQuote = freightQuote;
        RawCommodityValue = commodityValue;
        RawFreightCharge = freightCharge;
        RawProducerReservationCost = reservationCost;
        RawNetReturn = netReturn;
    }
}
