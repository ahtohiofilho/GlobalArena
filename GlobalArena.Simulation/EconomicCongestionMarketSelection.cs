using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// One deterministic marginal-allocation decision. Exact congestion cost is
/// the change in integrated shared-edge cost caused by this tranche, based
/// on preceding selections. It is NOT the legacy per-unit flat transfer
/// charge and must not be serialized as an EconomicTradeAllocation.
/// </summary>
public sealed class EconomicCongestionMarketSelection
{
    public ulong DecisionOrdinal { get; }
    public EconomicPointId SourceEconomicPointId { get; }
    public EconomicActivityKind SourceActivity { get; }
    public EconomicPointId DestinationEconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawQuantity { get; }
    public ulong RawUnitValue { get; }
    public ulong RawBaseUnitTransferCost { get; }
    public FoodTransportCostFraction RawGrossTradeValue { get; }
    public FoodTransportCostFraction RawBaselineTransportCost { get; }
    public FoodTransportCostFraction RawAdditionalCongestionCost { get; }
    public FoodTransportCostFraction RawNetTradeReturn { get; }
    public IReadOnlyList<StrategicEdgeId> RoutedEdgePath { get; }

    public EconomicCongestionMarketSelection(
        ulong decisionOrdinal,
        EconomicPointId source,
        EconomicActivityKind activity,
        EconomicPointId destination,
        CommodityId commodity,
        ulong quantity,
        ulong rawUnitValue,
        ulong rawBaseUnitTransferCost,
        FoodTransportCostFraction rawAdditionalCongestionCost,
        IEnumerable<StrategicEdgeId>? routedEdgePath = null)
    {
        if (decisionOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(decisionOrdinal));
        if (!source.IsValid || !destination.IsValid || !commodity.IsValid)
            throw new ArgumentException("Selection requires valid source, destination and commodity.");
        if (activity != EconomicActivityKind.Agriculture && activity != EconomicActivityKind.Mining)
            throw new ArgumentOutOfRangeException(nameof(activity));
        if (quantity == 0UL)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (rawUnitValue <= rawBaseUnitTransferCost)
            throw new ArgumentOutOfRangeException(nameof(rawUnitValue));
        ArgumentNullException.ThrowIfNull(rawAdditionalCongestionCost);
        var chosenEdges = routedEdgePath?.ToArray() ?? Array.Empty<StrategicEdgeId>();
        if (chosenEdges.Any(edge => !edge.IsValid) ||
            chosenEdges.Distinct().Count() != chosenEdges.Length)
            throw new ArgumentException(
                "Selected trade route must have valid, non-repeated strategic edges.",
                nameof(routedEdgePath));

        var denominator = (ulong)EconomicTransferPolicy.FixedPointDenominator;
        var gross = new FoodTransportCostFraction(
            (System.Numerics.BigInteger)quantity * rawUnitValue, denominator);
        var baseline = new FoodTransportCostFraction(
            (System.Numerics.BigInteger)quantity * rawBaseUnitTransferCost, denominator);

        // Exact raw economic monetary units, including fractional units.
        var numerator = gross.Numerator * baseline.Denominator
            * rawAdditionalCongestionCost.Denominator
            - baseline.Numerator * gross.Denominator
                * rawAdditionalCongestionCost.Denominator
            - rawAdditionalCongestionCost.Numerator * gross.Denominator
                * baseline.Denominator;
        var common = gross.Denominator * baseline.Denominator
            * rawAdditionalCongestionCost.Denominator;
        if (numerator <= 0)
            throw new InvalidOperationException(
                "Selected trade must have strictly positive exact net return.");

        DecisionOrdinal = decisionOrdinal;
        SourceEconomicPointId = source;
        SourceActivity = activity;
        DestinationEconomicPointId = destination;
        CommodityId = commodity;
        RawQuantity = quantity;
        RawUnitValue = rawUnitValue;
        RawBaseUnitTransferCost = rawBaseUnitTransferCost;
        RawGrossTradeValue = gross;
        RawBaselineTransportCost = baseline;
        RawAdditionalCongestionCost = rawAdditionalCongestionCost;
        RawNetTradeReturn = new FoodTransportCostFraction(numerator, common);
        RoutedEdgePath = Array.AsReadOnly(chosenEdges);
    }
}
