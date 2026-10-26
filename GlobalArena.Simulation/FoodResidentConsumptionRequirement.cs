using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit integer grams of a named food per resident per economic cycle.
/// This physical requirement is not itself an EconomicDemandFlow. The
/// materializer must convert it exactly using the commodity's caller-supplied
/// grams-per-whole-flow-unit scale, and must reject rates outside the existing
/// M4.3 positive normalized resident-demand range.
/// </summary>
public sealed class FoodResidentConsumptionRequirement
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong GramsPerResidentPerCycle { get; }

    public FoodResidentConsumptionRequirement(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        ulong gramsPerResidentPerCycle)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Food requirement needs a valid economic point.",
                nameof(economicPointId));
        if (!commodityId.IsValid)
            throw new ArgumentException(
                "Food requirement needs a valid commodity.",
                nameof(commodityId));
        if (gramsPerResidentPerCycle == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(gramsPerResidentPerCycle),
                "Food requirement must specify positive physical mass.");

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        GramsPerResidentPerCycle = gramsPerResidentPerCycle;
    }
}
