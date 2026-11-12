// Global Arena Gate 09 R18 REV4 - numerical reference only (not production).
// Convert a positive exact rational to correctly rounded IEEE-754 binary64.
// For the F-072 tariff contract, 0 <= numerator <= long.MaxValue * 1_000_000
// and denominator = 1_000_000_000_000, so all positive results are normal.
using System;
using System.Numerics;

internal static class ExactRatioBinary64
{
    internal static double Convert(BigInteger numerator, long denominator)
    {
        if (numerator.Sign < 0 || denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (numerator.IsZero) return 0.0;

        var divisor = new BigInteger(denominator);
        // Exact floor(log2(numerator / divisor)), with integer comparisons.
        int exponent = checked((int)(numerator.GetBitLength() - divisor.GetBitLength()));
        bool below = exponent >= 0
            ? numerator < (divisor << exponent)
            : (numerator << (-exponent)) < divisor;
        if (below) exponent--;
        // Input domain guarantees normals. Reject rather than silently mishandle subnormals.
        if (exponent < -1022 || exponent > 1023)
            throw new ArithmeticException("Ratio outside supported normal binary64 domain");

        int shift = 52 - exponent;
        BigInteger scaledN = shift >= 0 ? numerator << shift : numerator;
        BigInteger scaledD = shift >= 0 ? divisor : divisor << (-shift);
        BigInteger significand = BigInteger.DivRem(scaledN, scaledD, out BigInteger remainder);

        // Nearest even; exact tie detection, no intermediate float or decimal rounding.
        int comparison = (remainder << 1).CompareTo(scaledD);
        if (comparison > 0 || (comparison == 0 && !significand.IsEven))
            significand++;

        if (significand == (BigInteger.One << 53))
        {
            significand >>= 1;
            exponent++;
        }
        if (exponent > 1023 || significand < (BigInteger.One << 52) ||
            significand >= (BigInteger.One << 53))
            throw new ArithmeticException("Unexpected binary64 rounding overflow");

        long fraction = checked((long)(significand - (BigInteger.One << 52)));
        long binary64 = ((long)(exponent + 1023) << 52) | fraction;
        return BitConverter.Int64BitsToDouble(binary64);
    }
}
