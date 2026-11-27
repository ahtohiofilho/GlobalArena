using System.Numerics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Generic dimensionless improvement ratio sqrt(1 + I/B), where I is
/// activated capital and B is a positive monetary reference in the same
/// units. No activity, commodity, technology age or market dependency.
/// </summary>
public static class FoundationInfrastructureCapitalResponse
{
    /// <summary>
    /// Productive dual of the canonical logistics cost response.
    /// floor(P0 * sqrt((B + I) / B)), rounded once in output micro-units.
    /// Zero natural output stays zero. Pending construction capital must
    /// not be passed as activated capital by an authoritative caller.
    /// </summary>
    public static ulong ProjectProductiveOutput(
        ulong naturalOutputMicro, ulong activatedCapitalMoneyMicro,
        ulong referenceConstructionMoneyMicro)
    {
        RequireReference(referenceConstructionMoneyMicro);
        if (naturalOutputMicro == 0UL || activatedCapitalMoneyMicro == 0UL)
            return naturalOutputMicro;
        BigInteger reference = referenceConstructionMoneyMicro;
        BigInteger capital = activatedCapitalMoneyMicro;
        BigInteger natural = naturalOutputMicro;
        return checked((ulong)IntegerSquareRoot(
            natural * natural * (reference + capital) / reference));
    }

    /// <summary>
    /// Canonical inverse improvement: floor(C0 / sqrt((B + I) / B)),
    /// with a minimum positive cost of one micro-unit as in R112.
    /// </summary>
    public static ulong ProjectUnitCost(
        ulong naturalCostMicro, ulong activatedCapitalMoneyMicro,
        ulong referenceConstructionMoneyMicro)
    {
        RequireReference(referenceConstructionMoneyMicro);
        if (naturalCostMicro == 0UL)
            throw new ArgumentOutOfRangeException(nameof(naturalCostMicro));
        if (activatedCapitalMoneyMicro == 0UL) return naturalCostMicro;
        BigInteger reference = referenceConstructionMoneyMicro;
        BigInteger capital = activatedCapitalMoneyMicro;
        BigInteger natural = naturalCostMicro;
        return checked((ulong)BigInteger.Max(BigInteger.One,
            IntegerSquareRoot(natural * natural * reference / (reference + capital))));
    }

    private static void RequireReference(ulong reference)
    {
        if (reference == 0UL)
            throw new ArgumentOutOfRangeException(nameof(reference),
                "Infrastructure requires a positive construction reference.");
    }

    // floor(sqrt(value)); exact for nonnegative inputs, without floating
    // point, platform rounding modes or overflow in intermediate products.
    internal static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value.IsZero) return BigInteger.Zero;
        int bits = checked((int)value.GetBitLength());
        BigInteger x = BigInteger.One << ((bits + 1) / 2);
        while (true)
        {
            BigInteger next = (x + value / x) >> 1;
            if (next >= x) return x;
            x = next;
        }
    }
}
