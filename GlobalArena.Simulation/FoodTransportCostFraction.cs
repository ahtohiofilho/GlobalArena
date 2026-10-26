using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact nonnegative rational transport cost in caller-defined raw economic
/// cost units. Keeping numerator/denominator avoids rounding away congestion
/// increments or introducing an arbitrary congestion cut-off.
/// </summary>
public sealed class FoodTransportCostFraction : IComparable<FoodTransportCostFraction>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public FoodTransportCostFraction(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator));

        // Canonicalize rational representations for deterministic results.
        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public int CompareTo(FoodTransportCostFraction? other)
    {
        if (other is null)
            return 1;
        return (Numerator * other.Denominator)
            .CompareTo(other.Numerator * Denominator);
    }
}
