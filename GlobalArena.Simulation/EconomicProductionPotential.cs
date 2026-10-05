using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class EconomicProductionPotential
{
    public EconomicPointId EconomicPointId { get; }

    public CommodityId CommodityId { get; }

    public EconomicActivityKind Activity { get; }

    public long RawSupplyRatePerWorker { get; }

    public EconomicProductionPotential(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        EconomicActivityKind activity,
        long rawSupplyRatePerWorker)
    {
        if (!economicPointId.IsValid)
        {
            throw new ArgumentException(
                "Production potential requires a valid economic point identity.",
                nameof(economicPointId));
        }

        if (!commodityId.IsValid)
        {
            throw new ArgumentException(
                "Production potential requires a valid commodity identity.",
                nameof(commodityId));
        }

        if (activity != EconomicActivityKind.Agriculture
            && activity != EconomicActivityKind.Mining)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activity),
                "Production potential must target Agriculture or Mining.");
        }

        if (rawSupplyRatePerWorker <= 0L
            || rawSupplyRatePerWorker > StrategicScalarField.Denominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawSupplyRatePerWorker),
                "Production rate must be inside the positive normalized fixed-point range.");
        }

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        Activity = activity;
        RawSupplyRatePerWorker = rawSupplyRatePerWorker;
    }
}
