using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class EconomicDemandProfile
{
    public EconomicPointId EconomicPointId { get; }

    public CommodityId CommodityId { get; }

    public long RawDemandRatePerWorker { get; }

    public EconomicDemandProfile(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        long rawDemandRatePerWorker)
    {
        if (!economicPointId.IsValid)
        {
            throw new ArgumentException(
                "Demand profile requires a valid economic point identity.",
                nameof(economicPointId));
        }

        if (!commodityId.IsValid)
        {
            throw new ArgumentException(
                "Demand profile requires a valid commodity identity.",
                nameof(commodityId));
        }

        if (rawDemandRatePerWorker <= 0L
            || rawDemandRatePerWorker > StrategicScalarField.Denominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawDemandRatePerWorker),
                "Demand rate must be inside the positive normalized fixed-point range.");
        }

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawDemandRatePerWorker = rawDemandRatePerWorker;
    }
}
