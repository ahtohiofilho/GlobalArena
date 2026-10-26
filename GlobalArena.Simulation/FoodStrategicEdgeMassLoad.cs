using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Aggregate claimed market-flow mass on an edge, across all declared food
/// commodities. RawMicrograms is exact because the existing commodity flow
/// is fixed at 1,000,000 raw units per whole commodity unit, with caller-
/// declared integer grams per whole unit. No rounding is performed.
/// </summary>
public sealed class FoodStrategicEdgeMassLoad
{
    public StrategicEdgeId StrategicEdgeId { get; }
    public UInt128 RawMicrograms { get; }
    public UInt128 RawCapacityMicrograms { get; }
    public UInt128 RawOverCapacityMicrograms =>
        RawMicrograms > RawCapacityMicrograms
            ? RawMicrograms - RawCapacityMicrograms
            : 0U;

    public bool IsOverCapacity => RawMicrograms > RawCapacityMicrograms;

    public FoodStrategicEdgeMassLoad(
        StrategicEdgeId strategicEdgeId,
        UInt128 rawMicrograms,
        UInt128 rawCapacityMicrograms)
    {
        if (!strategicEdgeId.IsValid)
            throw new ArgumentException(
                "Transport load requires a valid strategic edge.",
                nameof(strategicEdgeId));
        StrategicEdgeId = strategicEdgeId;
        RawMicrograms = rawMicrograms;
        RawCapacityMicrograms = rawCapacityMicrograms;
    }
}
