using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-owned, deterministic commodity MARKET value (not freight).
/// Input is the raw quantity already sold to one destination/commodity this
/// turn. Both outputs use exact raw monetary rational units.
/// The integrated value is from zero to the supplied quantity. Its
/// mathematical derivative is the marginal value per WHOLE flow unit
/// divided by EconomicTransferPolicy.FixedPointDenominator.
/// In this reference solver marginal value must be nonnegative and
/// nonincreasing over the traded interval (caller-enforced contract).
/// </summary>
public interface IEconomicTurnCommodityValueCurve
{
    FoodTransportCostFraction EvaluateMarginalRawUnitValue(
        ulong alreadyAllocatedRawQuantity);

    FoodTransportCostFraction EvaluateIntegratedRawValue(
        ulong allocatedRawQuantity);
}

/// <summary>
/// One independently priced demand market; source supplies share this curve
/// at the destination. The offer is transient, not WorldState.
/// </summary>
public sealed class EconomicTurnCommodityValueOffer
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public IEconomicTurnCommodityValueCurve Curve { get; }

    public EconomicTurnCommodityValueOffer(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        IEconomicTurnCommodityValueCurve curve)
    {
        if (!economicPointId.IsValid || !commodityId.IsValid)
            throw new ArgumentException("Commodity value requires valid destination and commodity.");
        ArgumentNullException.ThrowIfNull(curve);
        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        Curve = curve;
    }
}
