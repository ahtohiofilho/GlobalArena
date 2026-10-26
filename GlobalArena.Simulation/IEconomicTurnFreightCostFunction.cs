using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// Ruleset-provided, continuously defined freight cost per physical gram
/// evaluated from an expected pre-turn load (in micrograms).
/// Implementations must be pure and deterministic for a versioned ruleset.
/// This seam deliberately prescribes no technology or freight-price formula.
/// </summary>
public interface IEconomicTurnFreightCostFunction
{
    ulong RulesetVersion { get; }

    FoodTransportCostFraction EvaluateRawUnitCostPerGram(
        BigInteger expectedLoadMicrograms);
}
