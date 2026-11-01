using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Agreement of advisory DIRECTIONS only. Neither series is a causal
/// prediction of the other, and their numerical units must never be mixed.
/// </summary>
public enum CanonicalFoodCalibrationDirection
{
    IndeterminateNoComparableSales,
    IndeterminateBothZero,
    IndeterminateHypotheticalZero,
    IndeterminateExecutedZero,
    Concordant,
    Opposed
}

public sealed record CanonicalFoodCalibrationCandidate(
    int CandidateStepPermille,
    int Vs84HypotheticalAgricultureDeltaPermille,
    int Vs88ExecutedFoodPriceTrendDeltaPermille,
    CanonicalFoodCalibrationDirection Direction,
    int AbsoluteAdvisoryDeltaGapPermille);

internal sealed record CanonicalFoodCalibrationReconstruction(
    CanonicalFoodCalibrationCandidate[] Candidates,
    BigInteger FrozenHypotheticalIndexNumerator,
    BigInteger FrozenHypotheticalIndexDenominator,
    BigInteger SourceExecutedFoodRaw,
    BigInteger TargetExecutedFoodRaw,
    BigInteger MatchedCommonFoodRaw,
    BigInteger SourceMatchedWeightedPriceRaw,
    BigInteger TargetMatchedWeightedPriceRaw);

/// <summary>
/// VS89 DIAGNOSTIC matrix. Compare bounded, dimensionless response
/// DIRECTIONS from (1) VS84 hypothetical prior local price index and
/// (2) VS88 two actual V12 food prices. No optimal gain is selected,
/// no false nominal conversion, no automatic production/settlement.
/// </summary>
public sealed class CanonicalFoodPriceCalibrationMatrix
{
    public const int MaximumCandidateCount = 8;

    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalExecutedFoodPriceTrendBridge _trend;
    private readonly int[] _steps;
    private readonly CanonicalFoodCalibrationCandidate[] _candidates;
    private readonly string _worldHashHex;

    public IReadOnlyList<int> CandidateStepPermilles { get; }
    public IReadOnlyList<CanonicalFoodCalibrationCandidate> Candidates { get; }
    public BigInteger FrozenHypotheticalIndexNumerator { get; }
    public BigInteger FrozenHypotheticalIndexDenominator { get; }
    public BigInteger SourceExecutedFoodRaw { get; }
    public BigInteger TargetExecutedFoodRaw { get; }
    public BigInteger MatchedCommonFoodRaw { get; }
    public BigInteger SourceMatchedWeightedNominalPriceRaw { get; }
    public BigInteger TargetMatchedWeightedNominalPriceRaw { get; }
    public bool HasComparableExecutedFoodPrice =>
        _trend.HasComparableExecutedFoodPrice;
    public bool CoverageIsComplete =>
        HasComparableExecutedFoodPrice &&
        MatchedCommonFoodRaw == SourceExecutedFoodRaw &&
        MatchedCommonFoodRaw == TargetExecutedFoodRaw;
    public ulong SourceCycleMovementTurn => _trend.SourceCycleMovementTurn;
    public ulong TargetCycleMovementTurn => _trend.TargetCycleMovementTurn;

    public bool ComparesOnlyDirectionsNotPriceUnits => true;
    public bool ProvesEconomicCausalityOrPredictionAccuracy => false;
    public bool SelectsOptimalGainAutomatically => false;
    public bool TreatsVs84IndexAsRealPayment => false;
    public bool ClaimsNonFoodExecutedPrices => false;
    public bool ModifiesWorkerProductionOrWorldSave => false;
    public bool CreatesCashInventoryOrCredit => false;
    public bool AuthorizesSettlement => false;

    private CanonicalFoodPriceCalibrationMatrix(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedFoodPriceTrendBridge trend,
        int[] steps,
        CanonicalFoodCalibrationReconstruction reconstruction)
    {
        _session = session;
        _trend = trend;
        _worldHashHex = session.CurrentHash.HexDigest;
        _steps = (int[])steps.Clone();
        CandidateStepPermilles = Array.AsReadOnly(_steps);
        _candidates = reconstruction.Candidates.ToArray();
        Candidates = Array.AsReadOnly(_candidates);
        FrozenHypotheticalIndexNumerator =
            reconstruction.FrozenHypotheticalIndexNumerator;
        FrozenHypotheticalIndexDenominator =
            reconstruction.FrozenHypotheticalIndexDenominator;
        SourceExecutedFoodRaw = reconstruction.SourceExecutedFoodRaw;
        TargetExecutedFoodRaw = reconstruction.TargetExecutedFoodRaw;
        MatchedCommonFoodRaw = reconstruction.MatchedCommonFoodRaw;
        SourceMatchedWeightedNominalPriceRaw =
            reconstruction.SourceMatchedWeightedPriceRaw;
        TargetMatchedWeightedNominalPriceRaw =
            reconstruction.TargetMatchedWeightedPriceRaw;
    }

    public static CanonicalFoodCalibrationDirection ClassifyDirections(
        bool hasComparableExecutedSales, int hypotheticalDelta,
        int executedDelta)
    {
        if (!hasComparableExecutedSales)
            return CanonicalFoodCalibrationDirection.IndeterminateNoComparableSales;
        if (hypotheticalDelta == 0 && executedDelta == 0)
            return CanonicalFoodCalibrationDirection.IndeterminateBothZero;
        if (hypotheticalDelta == 0)
            return CanonicalFoodCalibrationDirection.IndeterminateHypotheticalZero;
        if (executedDelta == 0)
            return CanonicalFoodCalibrationDirection.IndeterminateExecutedZero;
        return Math.Sign(hypotheticalDelta) == Math.Sign(executedDelta)
            ? CanonicalFoodCalibrationDirection.Concordant
            : CanonicalFoodCalibrationDirection.Opposed;
    }

    private static CanonicalFoodCalibrationReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedFoodPriceTrendBridge trend,
        int[] steps)
    {
        if (steps.Length is < 1 or > MaximumCandidateCount)
            throw new ArgumentOutOfRangeException(nameof(steps),
                "Calibration requires a finite explicit grid of 1..8 gains.");
        var priorStep = 0;
        foreach (var s in steps)
        {
            if (s <= priorStep ||
                s > CanonicalLaggedProductionResponse.MaximumResponseStepPermille)
                throw new ArgumentOutOfRangeException(nameof(steps),
                    "Candidate gains must be strictly increasing, unique, in 1..250.");
            priorStep = s;
        }

        trend.RequireCurrent(session);
        trend.VerifyIndependent();
        var receipt = trend.SourceCycleProof.SourcePriceReceipt;
        var food = receipt.HypotheticalPriorLocalPriceSignals.SingleOrDefault(
            x => x.Activity == EconomicActivityKind.Agriculture) ??
            throw new InvalidOperationException(
                "The VS84 frozen hypothetical agricultural price is missing.");
        if (food.FrozenWeightedLocalPriceIndex.Denominator <= 0 ||
            food.FrozenWeightedLocalPriceIndex.Numerator < 0 ||
            food.FrozenWeightedLocalPriceIndex.Numerator >
                food.FrozenWeightedLocalPriceIndex.Denominator)
            throw new InvalidOperationException(
                "Invalid certified VS84 relative food index.");

        // Both totals are actual V12 food quantities, not VS83 potential
        // production and never a monetary budget. They are used only for
        // exact matching COVERAGE fractions, not to scale a price index.
        var ledger = session.State.FoodCash ??
            throw new InvalidOperationException(
                "No authenticated local cash-food ledger exists.");
        var sourceFood = ledger.Receipts
            .Where(x => x.MovementTurn == trend.SourceCycleMovementTurn)
            .Aggregate(BigInteger.Zero, (acc, x) => acc + x.FoodRaw);
        var targetFood = (BigInteger)trend.TargetExecutedWitness.TotalActualLocalFoodRaw;
        var common = trend.MatchedTiles.Aggregate(
            BigInteger.Zero, (acc, x) => acc + x.CommonObservedFoodRaw);
        if (common > sourceFood || common > targetFood ||
            (trend.HasComparableExecutedFoodPrice && common <= 0) ||
            (!trend.HasComparableExecutedFoodPrice && common != 0))
            throw new InvalidOperationException(
                "Matched food sample cannot exceed either actual physical trade volume.");

        var a = trend.SourceCommonWeightedNominalPriceRaw;
        var b = trend.TargetCommonWeightedNominalPriceRaw;
        if (trend.HasComparableExecutedFoodPrice && (a <= 0 || b <= 0))
            throw new InvalidOperationException(
                "A comparable actual food cohort must have positive paid prices.");
        if (!trend.HasComparableExecutedFoodPrice && (a != 0 || b != 0))
            throw new InvalidOperationException(
                "No matching executed sales must not invent nominal prices.");
        var rows = new List<CanonicalFoodCalibrationCandidate>();
        foreach (var s in steps)
        {
            var hypothetical =
                CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                    food.FrozenWeightedLocalPriceIndex, s);
            var observed = a + b == 0
                ? 0
                : checked((int)((b - a) * s / (a + b)));
            var classification = ClassifyDirections(
                trend.HasComparableExecutedFoodPrice, hypothetical, observed);
            rows.Add(new CanonicalFoodCalibrationCandidate(
                s, hypothetical, observed, classification,
                Math.Abs(hypothetical - observed)));
        }
        return new CanonicalFoodCalibrationReconstruction(
            rows.ToArray(),
            food.FrozenWeightedLocalPriceIndex.Numerator,
            food.FrozenWeightedLocalPriceIndex.Denominator,
            sourceFood, targetFood, common, a, b);
    }

    public static CanonicalFoodPriceCalibrationMatrix FromVerifiedSignals(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedFoodPriceTrendBridge trend,
        IEnumerable<int> candidateStepPermilles)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(trend);
        ArgumentNullException.ThrowIfNull(candidateStepPermilles);
        var steps = candidateStepPermilles.ToArray();
        var built = Reconstruct(session, trend, steps);
        var report = new CanonicalFoodPriceCalibrationMatrix(
            session, trend, steps, built);
        report.VerifyIndependent();
        return report;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash.HexDigest != _worldHashHex)
            throw new InvalidOperationException(
                "Calibration matrix cannot outlive the canonical world/session.");
        _trend.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Reconstruct(_session, _trend, _steps);
        if (!_candidates.SequenceEqual(rebuilt.Candidates) ||
            FrozenHypotheticalIndexNumerator !=
                rebuilt.FrozenHypotheticalIndexNumerator ||
            FrozenHypotheticalIndexDenominator !=
                rebuilt.FrozenHypotheticalIndexDenominator ||
            SourceExecutedFoodRaw != rebuilt.SourceExecutedFoodRaw ||
            TargetExecutedFoodRaw != rebuilt.TargetExecutedFoodRaw ||
            MatchedCommonFoodRaw != rebuilt.MatchedCommonFoodRaw ||
            SourceMatchedWeightedNominalPriceRaw !=
                rebuilt.SourceMatchedWeightedPriceRaw ||
            TargetMatchedWeightedNominalPriceRaw !=
                rebuilt.TargetMatchedWeightedPriceRaw)
            throw new InvalidOperationException(
                "Independent VS89 calibration diagnostics diverged from certified VS84 and actual VS88 price signals.");
        RequireCurrent(_session);
    }
}
