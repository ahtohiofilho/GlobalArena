using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact shared mass load and continuous marginal/total congestion cost
/// for one used strategic edge. It is never marked 'over capacity'.
/// </summary>
public sealed class FoodStrategicEdgeCongestionEntry
{
    public StrategicEdgeId StrategicEdgeId { get; }
    public BigInteger LoadMicrograms { get; }
    public FoodTransportCostFraction MarginalCostPerGram { get; }
    public FoodTransportCostFraction TotalCost { get; }

    public FoodStrategicEdgeCongestionEntry(
        FoodStrategicEdgeCongestionProfile profile,
        BigInteger loadMicrograms)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (loadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(loadMicrograms));

        StrategicEdgeId = profile.StrategicEdgeId;
        LoadMicrograms = loadMicrograms;
        MarginalCostPerGram = profile.EvaluateMarginalCostPerGram(loadMicrograms);
        TotalCost = profile.EvaluateTotalCost(loadMicrograms);
    }
}
