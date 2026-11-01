using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// All decisions are OBSERVATIONAL DATA-QUALITY classifications, never
/// commercial authorizations or proof of a causal supply/demand response.
/// </summary>
public enum CanonicalFoodRobustnessDiagnostic
{
    NoComparableExecutedFoodSales,
    InsufficientPhysicalCoverage,
    ExcessiveObservedVolumeShift,
    InsufficientComparableHistory,
    InsufficientNonzeroDirectionalEvidence,
    ExcessiveObservedReversals,
    WithinDeclaredObservationalCriteria
}

/// <summary>
/// Explicit diagnostic thresholds. Trade-volume shift is a change in
/// EXECUTED sales volume, not an inferred supply or demand shock.
/// A gain must have already been explicitly enumerated by VS89/VS90.
/// </summary>
public sealed record CanonicalFoodRobustnessScenario(
    string ScenarioId,
    int CandidateStepPermille,
    int MinimumCommonPhysicalCoveragePermille,
    int MaximumExecutedVolumeShiftPermille,
    int MinimumEligibleWindows,
    int MinimumNonzeroWindows,
    int MaximumAdjacentNonzeroReversals);

public sealed record CanonicalFoodRobustnessObservation(
    string ScenarioId,
    int CandidateStepPermille,
    int ObservedWindows,
    int ActuallyComparableWindows,
    int WindowsMeetingPhysicalCoverage,
    int WindowsMeetingBothCoverageAndVolumeShift,
    int RejectedForCoverage,
    int RejectedForVolumeShift,
    int NonzeroQualifiedDirectionalWindows,
    int AdjacentQualifiedNonzeroReversals,
    int MaximumAbsoluteQualifiedDeltaPermille,
    CanonicalFoodRobustnessDiagnostic Diagnostic)
{
    public bool WithinDeclaredCriteria =>
        Diagnostic == CanonicalFoodRobustnessDiagnostic
            .WithinDeclaredObservationalCriteria;
}

/// <summary>
/// VS91 runs conservative/lenient diagnostic gates over already-executed
/// V12 local-food receipts and verified VS90 cycle evidence. No market
/// prices, purchases or output are synthesized. The PURE evaluator accepts
/// arbitrary windows only to test a threshold policy; only FromVerifiedHistory
/// binds an evaluation to the canonical session/journal.
/// </summary>
public sealed class CanonicalMultiCycleFoodRobustnessGate
{
    public const int MaximumScenarios = 8;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalMultiCycleFoodPriceStabilityReport _history;
    private readonly CanonicalFoodRobustnessScenario[] _policies;
    private readonly CanonicalFoodRobustnessObservation[] _observations;
    private readonly string _worldHashHex;

    public IReadOnlyList<CanonicalFoodRobustnessScenario> Scenarios { get; }
    public IReadOnlyList<CanonicalFoodRobustnessObservation> Observations { get; }
    public bool UsesOnlyActuallyExecutedLocalFoodSales => true;
    public bool ClaimsMeasuredCausalSupplyOrDemandShock => false;
    public bool ClaimsPredictiveAccuracy => false;
    public bool PromotesAnyGainAutomatically => false;
    public bool EstablishesNonFoodMarketPrices => false;
    public bool CanAuthorizeRealProductionOrSettlement => false;
    public bool ChangesWorldOrSave => false;
    public bool CreatesInventoryOrCredit => false;

    private CanonicalMultiCycleFoodRobustnessGate(
        CanonicalPhysicalTacticalSession session,
        CanonicalMultiCycleFoodPriceStabilityReport history,
        CanonicalFoodRobustnessScenario[] policies,
        CanonicalFoodRobustnessObservation[] observations)
    {
        _session = session;
        _history = history;
        _policies = (CanonicalFoodRobustnessScenario[])policies.Clone();
        _observations = (CanonicalFoodRobustnessObservation[])observations.Clone();
        Scenarios = Array.AsReadOnly(_policies);
        Observations = Array.AsReadOnly(_observations);
        _worldHashHex = session.CurrentHash.HexDigest;
    }

    private static void ValidateScenario(CanonicalFoodRobustnessScenario policy,
        int observedWindows)
    {
        if (policy is null ||
            string.IsNullOrEmpty(policy.ScenarioId) ||
            policy.ScenarioId.Length > 40 ||
            policy.ScenarioId.Any(x => !(char.IsAsciiLetterOrDigit(x) ||
                x is '-' or '_')) ||
            policy.CandidateStepPermille is < 1 or >
                CanonicalLaggedProductionResponse.MaximumResponseStepPermille ||
            policy.MinimumCommonPhysicalCoveragePermille is < 0 or > 1000 ||
            policy.MaximumExecutedVolumeShiftPermille is < 0 or > 1000 ||
            policy.MinimumEligibleWindows < 1 ||
            policy.MinimumEligibleWindows > observedWindows ||
            policy.MinimumNonzeroWindows < 0 ||
            policy.MinimumNonzeroWindows > policy.MinimumEligibleWindows ||
            policy.MaximumAdjacentNonzeroReversals < 0 ||
            policy.MaximumAdjacentNonzeroReversals >= observedWindows)
            throw new ArgumentOutOfRangeException(nameof(policy),
                "Invalid ID, gain, coverage, volume, sample or reversal threshold.");
    }

    /// <summary>
    /// Pure policy sensitivity evaluation, NOT an authorization or a
    /// historical proof. FromVerifiedHistory supplies certified windows.
    /// </summary>
    public static CanonicalFoodRobustnessObservation EvaluateHypotheticalCriteria(
        IReadOnlyList<CanonicalMultiCycleExecutedFoodWindow> windows,
        CanonicalFoodRobustnessScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(scenario);
        if (windows.Count is < 2 or > 7)
            throw new ArgumentOutOfRangeException(nameof(windows));
        ValidateScenario(scenario, windows.Count);

        var actuallyComparable = 0;
        var covered = 0;
        var eligible = 0;
        var excludedCoverage = 0;
        var excludedShift = 0;
        var maxDelta = 0;
        var nonzero = 0;
        var directions = new int?[windows.Count];
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i] ?? throw new ArgumentException(
                "Historical window is null.", nameof(windows));
            if (w.SourceExecutedFoodRaw < 0 ||
                w.TargetExecutedFoodRaw < 0 ||
                w.CommonExecutedFoodRaw < 0 ||
                w.CommonExecutedFoodRaw > w.SourceExecutedFoodRaw ||
                w.CommonExecutedFoodRaw > w.TargetExecutedFoodRaw ||
                w.MatchedTileCount < 0 ||
                w.SourceMatchedWeightedPriceRaw < 0 ||
                w.TargetMatchedWeightedPriceRaw < 0 ||
                (w.HasComparableExecutedSales &&
                    (w.CommonExecutedFoodRaw <= 0 ||
                     w.SourceMatchedWeightedPriceRaw <= 0 ||
                     w.TargetMatchedWeightedPriceRaw <= 0)) ||
                (!w.HasComparableExecutedSales &&
                    (w.CommonExecutedFoodRaw != 0 ||
                     w.SourceMatchedWeightedPriceRaw != 0 ||
                     w.TargetMatchedWeightedPriceRaw != 0)))
                throw new InvalidOperationException(
                    "Diagnostic criteria received inconsistent executed-price window evidence.");

            if (!w.HasComparableExecutedSales)
                continue; // an absent price is never a zero price.
            actuallyComparable++;
            if (w.CommonExecutedFoodRaw * 1000 <
                    (BigInteger)scenario.MinimumCommonPhysicalCoveragePermille *
                        w.SourceExecutedFoodRaw ||
                w.CommonExecutedFoodRaw * 1000 <
                    (BigInteger)scenario.MinimumCommonPhysicalCoveragePermille *
                        w.TargetExecutedFoodRaw)
            {
                excludedCoverage++;
                continue;
            }
            covered++;
            var larger = BigInteger.Max(
                w.SourceExecutedFoodRaw, w.TargetExecutedFoodRaw);
            var shift = BigInteger.Abs(
                w.TargetExecutedFoodRaw - w.SourceExecutedFoodRaw);
            if (larger == 0 ||
                shift * 1000 > (BigInteger)
                    scenario.MaximumExecutedVolumeShiftPermille * larger)
            {
                excludedShift++;
                continue;
            }

            eligible++;
            var a = w.SourceMatchedWeightedPriceRaw;
            var b = w.TargetMatchedWeightedPriceRaw;
            var delta = checked((int)((b - a) *
                scenario.CandidateStepPermille / (a + b)));
            if (Math.Abs(delta) > scenario.CandidateStepPermille)
                throw new InvalidOperationException(
                    "Executed nominal food-price trend exceeds bounded diagnostic gain.");
            directions[i] = delta;
            maxDelta = Math.Max(maxDelta, Math.Abs(delta));
            if (delta != 0)
                nonzero++;
        }
        var reversals =
            CanonicalMultiCycleFoodPriceStabilityReport
                .CountNonzeroAdjacentReversals(directions);
        var diagnostic = actuallyComparable == 0
            ? CanonicalFoodRobustnessDiagnostic.NoComparableExecutedFoodSales
            : covered == 0
                ? CanonicalFoodRobustnessDiagnostic.InsufficientPhysicalCoverage
            : eligible == 0
                ? CanonicalFoodRobustnessDiagnostic.ExcessiveObservedVolumeShift
            : eligible < scenario.MinimumEligibleWindows
                ? CanonicalFoodRobustnessDiagnostic.InsufficientComparableHistory
            : nonzero < scenario.MinimumNonzeroWindows
                ? CanonicalFoodRobustnessDiagnostic.InsufficientNonzeroDirectionalEvidence
            : reversals > scenario.MaximumAdjacentNonzeroReversals
                ? CanonicalFoodRobustnessDiagnostic.ExcessiveObservedReversals
                : CanonicalFoodRobustnessDiagnostic.WithinDeclaredObservationalCriteria;
        return new CanonicalFoodRobustnessObservation(
            scenario.ScenarioId, scenario.CandidateStepPermille,
            windows.Count, actuallyComparable, covered, eligible,
            excludedCoverage, excludedShift, nonzero, reversals, maxDelta,
            diagnostic);
    }

    private static CanonicalFoodRobustnessObservation[] Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalMultiCycleFoodPriceStabilityReport history,
        CanonicalFoodRobustnessScenario[] policies)
    {
        if (policies.Length is < 1 or > MaximumScenarios)
            throw new ArgumentOutOfRangeException(nameof(policies));
        history.RequireCurrent(session);
        history.VerifyIndependent();
        var observedWindows = history.Windows.Count;
        var supportedSteps = history.GainSensitivities.Select(
            x => x.CandidateStepPermille).ToHashSet();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var results = new CanonicalFoodRobustnessObservation[policies.Length];
        for (var i = 0; i < policies.Length; i++)
        {
            var policy = policies[i];
            ValidateScenario(policy, observedWindows);
            if (!names.Add(policy.ScenarioId) ||
                !supportedSteps.Contains(policy.CandidateStepPermille))
                throw new InvalidOperationException(
                    "Each scenario must have a unique ID and a VS89-certified gain.");
            results[i] = EvaluateHypotheticalCriteria(history.Windows, policy);
        }
        return results;
    }

    public static CanonicalMultiCycleFoodRobustnessGate FromVerifiedHistory(
        CanonicalPhysicalTacticalSession session,
        CanonicalMultiCycleFoodPriceStabilityReport history,
        IEnumerable<CanonicalFoodRobustnessScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(scenarios);
        var policies = scenarios.ToArray();
        var observations = Reconstruct(session, history, policies);
        var report = new CanonicalMultiCycleFoodRobustnessGate(
            session, history, policies, observations);
        report.VerifyIndependent();
        return report;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session) ||
            _worldHashHex != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "A robustness diagnosis cannot outlive its physical session.");
        _history.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Reconstruct(_session, _history, _policies);
        if (!_observations.SequenceEqual(rebuilt))
            throw new InvalidOperationException(
                "Independent VS91 policy stress classification diverged from certified actual cash-food history.");
        RequireCurrent(_session);
    }
}
