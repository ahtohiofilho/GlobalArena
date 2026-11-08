namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// Snapshot of effective commercial impedance at one unit logical edge,
/// expressed as a dimensionless physical-economic index, NOT currency,
/// worker travel time, tariff or an actual freight transfer.
/// </summary>
public readonly record struct CongestedEdgeEvaluation(
    long LoadRateRaw,
    long CapacityRateRaw,
    long OccupancyRaw,
    int Exponent,
    long CongestionMultiplierRaw,
    long EffectiveImpedanceRaw);

/// <summary>
/// Pure F-084D / F-031 candidate for convex congestion of a single
/// LOGICAL edge. No prices, per-commodity calibration, settlement, stored
/// cargo, route state, graph geometry or iterative market clearing.
/// </summary>
public static class EdgeCongestionLaboratory
{
    public const long Scale = 1_000_000;

    /// <summary>
    /// Converts an equivalent volume counted in an economic period into a
    /// per-turn rate. Both period duration and its flow rate use the same
    /// fixed-point turn normalization; there is no artificial congestion
    /// when cycles change duration and flow scales proportionally.
    /// </summary>
    public static long RatePerTurn(long volumePerPeriodRaw, long periodDurationTurnsRaw)
    {
        if (volumePerPeriodRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(volumePerPeriodRaw));
        if (periodDurationTurnsRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(periodDurationTurnsRaw));

        return checked((long)((Int128)volumePerPeriodRaw * Scale / periodDurationTurnsRaw));
    }

    /// <summary>
    /// Candidate C(B,I) = C_free(I) [1 + (B/K(I))^p].
    /// Only global exponents 3/4/5 are admitted in THIS comparison lab.
    /// Congestion does not impose a hard capacity cutoff.
    /// Exact integer arithmetic, truncating at each fixed-point multiply.
    /// Too-large unrepresentable values fail rather than silently clamp,
    /// wrap or invent a physical capacity ceiling.
    /// </summary>
    public static CongestedEdgeEvaluation Evaluate(
        long freeFlowImpedanceRaw,
        long capacityRateRaw,
        long equivalentLoadRateRaw,
        int globalExponent)
    {
        if (freeFlowImpedanceRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(freeFlowImpedanceRaw));
        if (capacityRateRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityRateRaw));
        if (equivalentLoadRateRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(equivalentLoadRateRaw));
        if (globalExponent is not (3 or 4 or 5))
            throw new ArgumentOutOfRangeException(nameof(globalExponent),
                "Only p=3, p=4, p=5 may be compared in this laboratory.");

        long rhoRaw = checked((long)((Int128)equivalentLoadRateRaw * Scale / capacityRateRaw));
        Int128 poweredRaw = Scale;
        for (int i = 0; i < globalExponent; i++)
            poweredRaw = checked(poweredRaw * rhoRaw / Scale);

        long multiplierRaw = checked((long)(Scale + poweredRaw));
        long effectiveRaw = checked((long)((Int128)freeFlowImpedanceRaw
            * multiplierRaw / Scale));

        return new CongestedEdgeEvaluation(equivalentLoadRateRaw,
            capacityRateRaw, rhoRaw, globalExponent, multiplierRaw, effectiveRaw);
    }

    public static CongestedEdgeEvaluation Evaluate(
        FreeFlowInvestedEdge improved,
        long equivalentLoadRateRaw,
        int globalExponent)
    {
        return Evaluate(improved.FreeFlowImpedanceRaw,
            improved.CapacityRateRaw, equivalentLoadRateRaw, globalExponent);
    }

    public static CongestedEdgeEvaluation Evaluate(
        NaturalEdgeEvaluation natural,
        long equivalentLoadRateRaw,
        int globalExponent)
    {
        return Evaluate(natural.ImpedanceRaw,
            natural.NaturalCapacityRateRaw, equivalentLoadRateRaw, globalExponent);
    }
}
