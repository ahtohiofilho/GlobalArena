namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// EXPLICIT LAB parameters; the only canonical economic normalization is
/// LandImpedanceBaseRaw == 1e6. These settings are not ratified simulation law.
/// </summary>
public readonly record struct NaturalEdgeCalibration(
    long LandImpedanceBaseRaw,
    long WaterImpedanceBaseRaw,
    long CoastImpedanceBaseRaw,
    long RoughnessImpedanceWeightRaw,
    long GradeImpedanceWeightRaw,
    long LandCapacityRateRaw,
    long WaterCapacityRateRaw,
    long CoastCapacityRateRaw,
    long RoughnessCapacityPenaltyRaw,
    long GradeCapacityPenaltyRaw)
{
    public const long Scale = 1_000_000;

    public void Validate()
    {
        if (LandImpedanceBaseRaw != Scale)
            throw new ArgumentOutOfRangeException(nameof(LandImpedanceBaseRaw),
                "F-082 requires exactly one impedance unit for natural neutral land.");

        CheckPositive(WaterImpedanceBaseRaw, nameof(WaterImpedanceBaseRaw));
        CheckPositive(CoastImpedanceBaseRaw, nameof(CoastImpedanceBaseRaw));
        CheckPositive(LandCapacityRateRaw, nameof(LandCapacityRateRaw));
        CheckPositive(WaterCapacityRateRaw, nameof(WaterCapacityRateRaw));
        CheckPositive(CoastCapacityRateRaw, nameof(CoastCapacityRateRaw));
        CheckNonNegative(RoughnessImpedanceWeightRaw, nameof(RoughnessImpedanceWeightRaw));
        CheckNonNegative(GradeImpedanceWeightRaw, nameof(GradeImpedanceWeightRaw));
        CheckNonNegative(RoughnessCapacityPenaltyRaw, nameof(RoughnessCapacityPenaltyRaw));
        CheckNonNegative(GradeCapacityPenaltyRaw, nameof(GradeCapacityPenaltyRaw));
    }

    private static void CheckPositive(long value, string name)
    {
        if (value < 1 || value > 4 * Scale)
            throw new ArgumentOutOfRangeException(name, "Must be positive and <= 4e6.");
    }

    private static void CheckNonNegative(long value, string name)
    {
        if (value < 0 || value > 4 * Scale)
            throw new ArgumentOutOfRangeException(name, "Must be within [0,4e6].");
    }
}

/// <summary>
/// One reproducible benchmark candidate, NOT a canonical price/transport law.
/// 0.6 + 0.4 penalties make rough+steep land cost exactly 2 in F-083 lab.
/// Coast is an EDGE-local interface, never a separate port/node charge.
/// </summary>
public static class NaturalEdgeReferenceProfiles
{
    public static NaturalEdgeCalibration CandidateA => new(
        LandImpedanceBaseRaw: 1_000_000,
        WaterImpedanceBaseRaw: 800_000,
        CoastImpedanceBaseRaw: 1_500_000,
        RoughnessImpedanceWeightRaw: 600_000,
        GradeImpedanceWeightRaw: 400_000,
        LandCapacityRateRaw: 1_000_000,
        WaterCapacityRateRaw: 1_500_000,
        CoastCapacityRateRaw: 350_000,
        RoughnessCapacityPenaltyRaw: 600_000,
        GradeCapacityPenaltyRaw: 400_000);
}
