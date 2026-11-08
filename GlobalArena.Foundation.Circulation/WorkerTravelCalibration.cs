namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// Experimental physical effort required to cross ONE logical edge.
/// A "turn" is represented here as 1e6 units of universal action time.
/// NONE of these values, including neutral hop time, are approved game pacing.
/// Values are independent of economic impedance and natural freight capacity.
/// </summary>
public readonly record struct WorkerTravelCalibration(
    long NeutralLandEffortRaw,
    long WaterEffortRaw,
    long CoastEffortRaw,
    long RoughnessEffortWeightRaw,
    long GradeEffortWeightRaw)
{
    public const long Scale = 1_000_000;

    public void Validate()
    {
        CheckPositive(NeutralLandEffortRaw, nameof(NeutralLandEffortRaw));
        CheckPositive(WaterEffortRaw, nameof(WaterEffortRaw));
        CheckPositive(CoastEffortRaw, nameof(CoastEffortRaw));
        CheckNonNegative(RoughnessEffortWeightRaw, nameof(RoughnessEffortWeightRaw));
        CheckNonNegative(GradeEffortWeightRaw, nameof(GradeEffortWeightRaw));
    }

    private static void CheckPositive(long value, string field)
    {
        if (value <= 0 || value > 4 * Scale)
            throw new ArgumentOutOfRangeException(field, "Laboratory effort must be within (0,4e6].");
    }

    private static void CheckNonNegative(long value, string field)
    {
        if (value < 0 || value > 4 * Scale)
            throw new ArgumentOutOfRangeException(field, "Laboratory weight must be within [0,4e6].");
    }
}

public static class WorkerTravelReferenceProfiles
{
    // Candidate B is a test scenario. DO NOT canonize 1 hop per turn.
    public static WorkerTravelCalibration CandidateB => new(
        NeutralLandEffortRaw: 1_000_000,
        WaterEffortRaw: 650_000,
        CoastEffortRaw: 1_400_000,
        RoughnessEffortWeightRaw: 700_000,
        GradeEffortWeightRaw: 500_000);
}
