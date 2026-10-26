using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit transport capacity for one strategic edge, measured in grams
/// per economic cycle. Zero is an explicit no-throughput declaration; there
/// is no inferred capacity from geometry or market transfer signals.
/// </summary>
public sealed class FoodStrategicEdgeMassCapacity
{
    public StrategicEdgeId StrategicEdgeId { get; }
    public ulong GramsPerEconomicCycle { get; }

    public FoodStrategicEdgeMassCapacity(
        StrategicEdgeId strategicEdgeId,
        ulong gramsPerEconomicCycle)
    {
        if (!strategicEdgeId.IsValid)
            throw new ArgumentException(
                "Food transport capacity requires a valid strategic edge.",
                nameof(strategicEdgeId));

        StrategicEdgeId = strategicEdgeId;
        GramsPerEconomicCycle = gramsPerEconomicCycle;
    }
}
