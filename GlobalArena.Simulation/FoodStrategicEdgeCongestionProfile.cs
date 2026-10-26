using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-specified smooth, unbounded congestion cost function for a
/// strategic edge, rather than a physical maximum/overload threshold.
///
/// Marginal raw cost per gram at load L (micrograms) is:
///   b + a * (L / S)^2
/// where S is a characteristic, nonbinding throughput scale in micrograms.
/// Its derivative is continuous and nonnegative for L >= 0. S is NOT a
/// technical capacity: passing S or any other load is always permitted.
///
/// Integrated cost for total L is:
///   [ b*L + a*L^3/(3*S^2) ] / micrograms-per-gram.
/// Both quantities use exact rational BigInteger arithmetic.
/// </summary>
public sealed class FoodStrategicEdgeCongestionProfile
{
    private static readonly BigInteger MicrogramsPerGram =
        StrategicScalarField.Denominator;

    public StrategicEdgeId StrategicEdgeId { get; }
    public ulong CharacteristicGramsPerEconomicCycle { get; }
    public ulong RawBaseMarginalCostPerGram { get; }
    public ulong RawCongestionCostScalePerGram { get; }

    public FoodStrategicEdgeCongestionProfile(
        StrategicEdgeId strategicEdgeId,
        ulong characteristicGramsPerEconomicCycle,
        ulong rawBaseMarginalCostPerGram,
        ulong rawCongestionCostScalePerGram)
    {
        if (!strategicEdgeId.IsValid)
            throw new ArgumentException(
                "Congestion profile needs a valid strategic edge.",
                nameof(strategicEdgeId));
        if (characteristicGramsPerEconomicCycle == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(characteristicGramsPerEconomicCycle),
                "The characteristic throughput scale must be positive; it is not a capacity limit.");
        if (rawCongestionCostScalePerGram == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(rawCongestionCostScalePerGram),
                "Positive congestion sensitivity is needed for a rising marginal cost.");

        StrategicEdgeId = strategicEdgeId;
        CharacteristicGramsPerEconomicCycle = characteristicGramsPerEconomicCycle;
        RawBaseMarginalCostPerGram = rawBaseMarginalCostPerGram;
        RawCongestionCostScalePerGram = rawCongestionCostScalePerGram;
    }

    public FoodTransportCostFraction EvaluateMarginalCostPerGram(
        BigInteger loadMicrograms)
    {
        ValidateLoad(loadMicrograms);
        var reference = (BigInteger)CharacteristicGramsPerEconomicCycle
            * MicrogramsPerGram;
        var referenceSquared = reference * reference;
        var numerator =
            (BigInteger)RawBaseMarginalCostPerGram * referenceSquared
            + (BigInteger)RawCongestionCostScalePerGram
                * loadMicrograms * loadMicrograms;

        return new FoodTransportCostFraction(numerator, referenceSquared);
    }

    public FoodTransportCostFraction EvaluateTotalCost(
        BigInteger loadMicrograms)
    {
        ValidateLoad(loadMicrograms);
        var reference = (BigInteger)CharacteristicGramsPerEconomicCycle
            * MicrogramsPerGram;
        var referenceSquared = reference * reference;

        var numerator =
            3 * (BigInteger)RawBaseMarginalCostPerGram
                * loadMicrograms * referenceSquared
            + (BigInteger)RawCongestionCostScalePerGram
                * BigInteger.Pow(loadMicrograms, 3);
        var denominator = 3 * MicrogramsPerGram * referenceSquared;

        return new FoodTransportCostFraction(numerator, denominator);
    }

    private static void ValidateLoad(BigInteger loadMicrograms)
    {
        if (loadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(loadMicrograms));
    }
}
