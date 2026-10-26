using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit kilogram demand per resident per economic cycle, measured in
/// fixed-point kilograms. The current M4.3 resident-demand resolver restricts
/// a rate to (0, 1] whole commodity units per resident per cycle; reject larger
/// rates rather than silently truncate or reinterpret them.
/// </summary>
public sealed class FoodResidentConsumptionRequirement
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public long RawKilogramsPerResidentPerCycle { get; }

    public FoodResidentConsumptionRequirement(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        long rawKilogramsPerResidentPerCycle)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Food requirement needs a valid economic point.",
                nameof(economicPointId));
        if (!commodityId.IsValid)
            throw new ArgumentException(
                "Food requirement needs a valid commodity.",
                nameof(commodityId));
        if (rawKilogramsPerResidentPerCycle <= 0L ||
            rawKilogramsPerResidentPerCycle > StrategicScalarField.Denominator)
            throw new ArgumentOutOfRangeException(
                nameof(rawKilogramsPerResidentPerCycle),
                "Food rate must fit the current positive fixed-point resident demand range.");

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawKilogramsPerResidentPerCycle = rawKilogramsPerResidentPerCycle;
    }
}
