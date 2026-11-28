using System.Numerics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Provisional linear member (alpha = 1) of the purchased demography price
/// family. No default calibration or living-census input. The caller supplies
/// the next one-based historical acquisition index; the future canonical
/// ledger owns the baseline convention and monotonically advances that index.
/// This pure quote never debits cash or creates a worker.
/// </summary>
public sealed class FoundationPurchasedDemographicPricePolicy
{
    public ulong InitialMoneyMicro { get; }
    public ulong ReferenceAcquisitions { get; }

    public FoundationPurchasedDemographicPricePolicy(ulong initialMoneyMicro, ulong referenceAcquisitions)
    {
        if (initialMoneyMicro == 0UL) throw new ArgumentOutOfRangeException(nameof(initialMoneyMicro));
        if (referenceAcquisitions == 0UL) throw new ArgumentOutOfRangeException(nameof(referenceAcquisitions));
        InitialMoneyMicro = initialMoneyMicro;
        ReferenceAcquisitions = referenceAcquisitions;
    }

    public ulong QuoteMarginalMoneyMicro(ulong historicalIndex) =>
        QuoteBatchMoneyMicro(historicalIndex, 1UL);

    // Sum individually rounded-up marginal micro-money prices. Splitting a
    // batch into successive purchases cannot change its total price.
    public ulong QuoteBatchMoneyMicro(ulong nextHistoricalIndex, ulong quantity)
    {
        if (nextHistoricalIndex == 0UL) throw new ArgumentOutOfRangeException(nameof(nextHistoricalIndex));
        if (quantity == 0UL) return 0UL;
        _ = checked(nextHistoricalIndex + (quantity - 1UL));
        BigInteger b = ReferenceAcquisitions;
        BigInteger c = InitialMoneyMicro;
        BigInteger offset = c * (b + nextHistoricalIndex - 1) + b - 1;
        return checked((ulong)SumFloors(quantity, b, c, offset));
    }

    // Euclidean floor sum: sum floor((a*i + b)/m), i in [0,n).
    // Nonnegative BigInteger intermediates and logarithmic reduction avoid
    // floating-point rounding, overflow in intermediates and per-worker loops.
    private static BigInteger SumFloors(BigInteger n, BigInteger m, BigInteger a, BigInteger b)
    {
        BigInteger result = BigInteger.Zero;
        while (true)
        {
            if (a >= m)
            {
                result += n * (n - 1) / 2 * (a / m);
                a %= m;
            }
            if (b >= m)
            {
                result += n * (b / m);
                b %= m;
            }
            BigInteger y = a * n + b;
            if (y < m) return result;
            n = y / m;
            b = y % m;
            (m, a) = (a, m);
        }
    }
}
