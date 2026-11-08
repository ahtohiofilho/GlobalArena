namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// F-084E / F-073 candidate at the NATURAL reference load, never at the
/// current market clearing load. All terms are dimensionless fixed-point.
/// This does not set tariffs in production or move money.
/// </summary>
public readonly record struct EdgeTariffReference(
    long NaturalReferenceLoadRateRaw,
    long NaturalReferenceImpedanceRaw,
    long InvestedReferenceImpedanceRaw,
    long PhysicalGainRaw,
    long WeightRaw,
    long IncorporatedCapitalRaw,
    int GlobalExponent);

public static class EdgeTariffReferenceLaboratory
{
    public const long Scale = 1_000_000;

    /// <summary>
    /// c0 = C(B=Knatural, I=0)
    /// cI = C(B=Knatural, I), holding reference B FIXED.
    /// delta = max(0,c0-cI)
    /// w = delta/(1+delta/c0) = delta*c0/(c0+delta)
    ///
    /// For a natural edge with zero capital, w=0. Ownership, price,
    /// current traffic and path distance never enter the calculation.
    /// The formula is a candidate, not an approved tariff parameter.
    /// </summary>
    public static EdgeTariffReference Evaluate(
        NaturalEdgeEvaluation natural,
        FreeFlowInvestedEdge improved,
        int globalExponent)
    {
        if (natural.ImpedanceRaw <= 0 || natural.NaturalCapacityRateRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(natural));
        if (improved.TotalInvestedCapitalRaw < 0 ||
            improved.ReferenceCapitalRaw <= 0 ||
            improved.ImprovementFactorRaw < Scale ||
            improved.FreeFlowImpedanceRaw <= 0 ||
            improved.CapacityRateRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(improved));

        // This lab enforces the F-024/F-025 premise that an actual
        // improvement cannot worsen a previously usable physical edge.
        if (improved.FreeFlowImpedanceRaw > natural.ImpedanceRaw ||
            improved.CapacityRateRaw < natural.NaturalCapacityRateRaw)
            throw new ArgumentException(
                "An infrastructure improvement cannot worsen natural physical service.",
                nameof(improved));

        if (improved.TotalInvestedCapitalRaw == 0 &&
            (improved.FreeFlowImpedanceRaw != natural.ImpedanceRaw ||
             improved.CapacityRateRaw != natural.NaturalCapacityRateRaw ||
             improved.ImprovementFactorRaw != Scale))
            throw new ArgumentException(
                "An edge with zero capital must be identical to its natural service.",
                nameof(improved));

        long referenceRate = natural.NaturalCapacityRateRaw;
        long c0 = EdgeCongestionLaboratory.Evaluate(
            natural, referenceRate, globalExponent).EffectiveImpedanceRaw;
        long cI = EdgeCongestionLaboratory.Evaluate(
            improved, referenceRate, globalExponent).EffectiveImpedanceRaw;

        long delta = Math.Max(0, checked(c0 - cI));
        long weight = delta == 0 ? 0 : checked((long)(
            (Int128)delta * c0 / ((Int128)c0 + delta)));

        if (improved.TotalInvestedCapitalRaw == 0 && weight != 0)
            throw new InvalidOperationException("Zero-capital infrastructure cannot charge.");

        return new EdgeTariffReference(referenceRate, c0, cI, delta,
            weight, improved.TotalInvestedCapitalRaw, globalExponent);
    }
}

/// <summary>
/// Weight-only F-060/F-062 participation laboratory. Holdings refer to
/// capital ALREADY incorporated by each owner, not promised contributions.
/// This returns dimensionless factors only, never money or a paid ledger.
/// </summary>
public static class EdgeTariffOwnershipLaboratory
{
    public const long Scale = 1_000_000;

    /// <summary>
    /// sellerSlot=-1 represents a seller not present in the ownership
    /// vector. Otherwise the seller is the civilization at that slot.
    /// External weight is w * (Itotal - Hseller) / Itotal, floored once.
    /// </summary>
    public static long ExternalWeightRaw(
        EdgeTariffReference reference,
        ReadOnlySpan<long> incorporatedHoldingsRaw,
        int sellerSlot = -1)
    {
        long total = ValidateOwnership(reference, incorporatedHoldingsRaw, sellerSlot);
        if (total == 0)
            return 0;

        long self = sellerSlot == -1 ? 0 : incorporatedHoldingsRaw[sellerSlot];
        return checked((long)((Int128)reference.WeightRaw * (total - self) / total));
    }

    /// <summary>
    /// Each outsider's nominal component w * Howner / Itotal.
    /// Components truncate separately; a future financial ledger MUST
    /// specify deterministic residual allocation, not book these values
    /// directly as money. A seller's own component is exactly zero.
    /// </summary>
    public static long OwnerExternalComponentRaw(
        EdgeTariffReference reference,
        ReadOnlySpan<long> incorporatedHoldingsRaw,
        int sellerSlot,
        int ownerSlot)
    {
        long total = ValidateOwnership(reference, incorporatedHoldingsRaw, sellerSlot);
        if ((uint)ownerSlot >= (uint)incorporatedHoldingsRaw.Length)
            throw new ArgumentOutOfRangeException(nameof(ownerSlot));

        if (total == 0 || sellerSlot == ownerSlot)
            return 0;

        return checked((long)((Int128)reference.WeightRaw *
            incorporatedHoldingsRaw[ownerSlot] / total));
    }

    /// <summary>
    /// Sum contributions from distinct edges along a *transient*
    /// computational commercial flow. No route object is persisted.
    /// </summary>
    public static long SumExternalWeights(ReadOnlySpan<long> externalWeightsRaw)
    {
        long sum = 0;
        foreach (long contribution in externalWeightsRaw)
        {
            if (contribution < 0)
                throw new ArgumentOutOfRangeException(nameof(externalWeightsRaw));
            sum = checked(sum + contribution);
        }
        return sum;
    }

    /// <summary>
    /// F-060 indicative split fractions only:
    /// seller = 1/(1+W); all other owners together = W/(1+W).
    /// Neither price P nor quantity q nor treasury transfers are supplied.
    /// Returned fractions sum to exactly 1e6 after quantization.
    /// </summary>
    public static (long SellerRetainedRaw, long ExternalOwnersCombinedRaw)
        IndicativeRevenueFractions(long externalWeightRaw)
    {
        if (externalWeightRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(externalWeightRaw));

        long seller = checked((long)((Int128)Scale * Scale /
            ((Int128)Scale + externalWeightRaw)));
        return (seller, Scale - seller);
    }

    private static long ValidateOwnership(
        EdgeTariffReference reference,
        ReadOnlySpan<long> holdings, int sellerSlot)
    {
        if (reference.WeightRaw < 0 || reference.IncorporatedCapitalRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(reference));
        if (sellerSlot < -1 || sellerSlot >= holdings.Length)
            throw new ArgumentOutOfRangeException(nameof(sellerSlot));

        long total = EdgeInvestmentLaboratory.SumCapital(holdings);
        if (total != reference.IncorporatedCapitalRaw)
            throw new ArgumentException(
                "Capital holdings must match the same edge's physical incorporated capital.",
                nameof(holdings));
        if (total == 0 && reference.WeightRaw != 0)
            throw new ArgumentException(
                "An unimproved natural edge cannot charge a tariff.",
                nameof(reference));
        return total;
    }
}
