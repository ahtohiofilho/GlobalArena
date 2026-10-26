using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit recurring demand rate per resident, distinct from the historical
/// workforce-based EconomicDemandProfile. No demographic or commodity rate
/// is inferred from generated geography.
/// </summary>
public sealed class EconomicResidentDemandProfile
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public long RawDemandRatePerResident { get; }

    public EconomicResidentDemandProfile(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        long rawDemandRatePerResident)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Resident demand requires a valid economic point identity.",
                nameof(economicPointId));
        if (!commodityId.IsValid)
            throw new ArgumentException(
                "Resident demand requires a valid commodity identity.",
                nameof(commodityId));
        if (rawDemandRatePerResident <= 0L
            || rawDemandRatePerResident > StrategicScalarField.Denominator)
            throw new ArgumentOutOfRangeException(
                nameof(rawDemandRatePerResident),
                "Resident demand rate must be in the positive normalized fixed-point range.");

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawDemandRatePerResident = rawDemandRatePerResident;
    }
}
