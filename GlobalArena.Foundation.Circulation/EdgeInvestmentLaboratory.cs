namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// F-084C pure hypothetical investment lens. Capital is ALREADY INCORPORATED
/// for the supplied snapshot; this class never spends a treasury, invents a
/// contractor, pays a toll, or grants ownership. It does not mutate an edge.
/// </summary>
public readonly record struct FreeFlowInvestedEdge(
    long TotalInvestedCapitalRaw,
    long ReferenceCapitalRaw,
    long ImprovementFactorRaw,
    long FreeFlowImpedanceRaw,
    long CapacityRateRaw);

public static class EdgeInvestmentLaboratory
{
    public const long Scale = 1_000_000;

    /// <summary>
    /// Add hypothetical shares from multiple civilizations. This is an
    /// order-invariant physical aggregate only, not ownership settlement.
    /// </summary>
    public static long SumCapital(ReadOnlySpan<long> incorporatedHoldingsRaw)
    {
        long total = 0;
        foreach (var holding in incorporatedHoldingsRaw)
        {
            if (holding < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(incorporatedHoldingsRaw), "Incorporated capital cannot be negative.");
            total = checked(total + holding);
        }
        return total;
    }

    /// <summary>
    /// Candidate F-025/F-031 efficiency:
    /// U(I) = 1 + log2(1 + I/I0), approximating log2 by deterministic
    /// dyadic linear interpolation to avoid authoritative floating point.
    ///
    /// C_free(I) = C_natural/U(I); K(I) = K_natural*U(I).
    ///
    /// Capital I and I0 have identical units, but C and K are NOT money.
    /// Zero invested capital reproduces natural C and K exactly.
    /// </summary>
    public static FreeFlowInvestedEdge Evaluate(
        NaturalEdgeEvaluation natural,
        long incorporatedCapitalRaw,
        long referenceCapitalRaw)
    {
        if (natural.ImpedanceRaw <= 0 || natural.NaturalCapacityRateRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(natural),
                "Natural impedance and capacity must be positive.");
        if (incorporatedCapitalRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(incorporatedCapitalRaw));
        if (referenceCapitalRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceCapitalRaw));

        // x = I0 + I. For dyadic landmarks x/I0 = 1, 2, 4, 8...,
        // log2 is exactly 0, 1, 2, 3... in fixed-point representation.
        // Between landmarks we interpolate linearly and monotonically,
        // with progressively smaller slopes: NO hard investment ceiling.
        Int128 x = (Int128)referenceCapitalRaw + incorporatedCapitalRaw;
        Int128 lower = referenceCapitalRaw;
        int level = 0;
        while (lower * 2 <= x)
        {
            lower *= 2;
            level++;
        }

        var fractionalLevelRaw = (long)((x - lower) * Scale / lower);
        long factorRaw = checked(Scale + (long)level * Scale + fractionalLevelRaw);
        long freeFlow = checked((long)Int128.Max(1,
            (Int128)natural.ImpedanceRaw * Scale / factorRaw));
        long capacity = checked((long)((Int128)natural.NaturalCapacityRateRaw * factorRaw / Scale));

        if (capacity <= 0)
            throw new InvalidOperationException("Investment candidate produced invalid capacity.");

        return new FreeFlowInvestedEdge(
            incorporatedCapitalRaw, referenceCapitalRaw, factorRaw, freeFlow, capacity);
    }
}
