using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact physical coverage in the ACTUALLY EXECUTED V12 local cash-food
/// market, not a statistical population or all-commodity market sample.
/// </summary>
public sealed record CanonicalFoodSampleWindow(
    ulong SourceCycleMovementTurn,
    ulong TargetCycleMovementTurn,
    int SourcePositivePaymentTiles,
    int TargetPositivePaymentTiles,
    int MatchedPositivePaymentTiles,
    BigInteger SourceExecutedFoodRaw,
    BigInteger TargetExecutedFoodRaw,
    BigInteger MatchedCommonFoodRaw,
    BigInteger LargestSingleTileCommonFoodRaw)
{
    public bool HasPairedPaidFoodPrices => MatchedPositivePaymentTiles > 0;
}

public enum CanonicalFoodSampleDiagnostic
{
    NoPairedPaidFoodPrices,
    InsufficientMatchedTileDiversity,
    InsufficientPairedPhysicalCoverage,
    ExcessiveSingleTileConcentration,
    InsufficientEligibleWindows,
    InsufficientPersistentTiles,
    WithinDeclaredFoodSampleCriteria
}

public sealed record CanonicalFoodSampleScenario(
    string ScenarioId,
    int MinimumMatchedTilesPerWindow,
    int MinimumPairedCoveragePermille,
    int MaximumLargestTileSharePermille,
    int MinimumEligibleWindows,
    int MinimumPersistentMatchedTiles);

public sealed record CanonicalFoodSampleAssessment(
    string ScenarioId,
    int TotalWindows,
    int PairedWindows,
    int WindowsMeetingTileDiversity,
    int WindowsMeetingPhysicalCoverage,
    int WindowsMeetingConcentration,
    int FullyEligibleWindows,
    int PersistentMatchedTileCount,
    CanonicalFoodSampleDiagnostic Diagnostic)
{
    public bool WithinDeclaredCriteria =>
        Diagnostic == CanonicalFoodSampleDiagnostic.WithinDeclaredFoodSampleCriteria;
}

internal sealed record CanonicalFoodSampleReconstruction(
    CanonicalFoodSampleWindow[] Windows,
    int PersistentMatchedTileCount,
    CanonicalFoodSampleAssessment[] Assessments);

/// <summary>
/// VS92: tests how narrow or concentrated the PAID FOOD sample is across
/// certified real VS90 cycles and VS91 audit. Meeting thresholds NEVER
/// demonstrates representativeness of wider food demand, population, other
/// commodities or purchasing power. No prices or transactions invented.
/// </summary>
public sealed class CanonicalExecutedFoodSampleAudit
{
    public const int MaximumScenarios = 8;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalMultiCycleFoodRobustnessGate _gate91;
    private readonly CanonicalFoodSampleScenario[] _scenarios;
    private readonly CanonicalFoodSampleWindow[] _windows;
    private readonly CanonicalFoodSampleAssessment[] _assessments;
    private readonly string _worldHashHex;

    public IReadOnlyList<CanonicalFoodSampleScenario> Scenarios { get; }
    public IReadOnlyList<CanonicalFoodSampleWindow> Windows { get; }
    public IReadOnlyList<CanonicalFoodSampleAssessment> Assessments { get; }
    public int PersistentMatchedTileCount { get; }
    public bool MeasuresOnlyExecutedLocalFoodSample => true;
    public bool ProvesWholeEconomyRepresentativeness => false;
    public bool ProvesCompleteFoodDemandCoverage => false;
    public bool EstablishesPricesForOtherGoods => false;
    public bool ProvesCausalSupplyDemandEffects => false;
    public bool PromotesEconomicParameterAutomatically => false;
    public bool ChangesCanonicalWorldOrSave => false;
    public bool CanAuthorizeSettlement => false;
    public bool CreatesMoneyStocksOrCredit => false;

    private CanonicalExecutedFoodSampleAudit(
        CanonicalPhysicalTacticalSession session,
        CanonicalMultiCycleFoodRobustnessGate gate91,
        CanonicalFoodSampleScenario[] scenarios,
        CanonicalFoodSampleReconstruction built)
    {
        _session = session;
        _gate91 = gate91;
        _scenarios = (CanonicalFoodSampleScenario[])scenarios.Clone();
        _windows = (CanonicalFoodSampleWindow[])built.Windows.Clone();
        _assessments = (CanonicalFoodSampleAssessment[])built.Assessments.Clone();
        Scenarios = Array.AsReadOnly(_scenarios);
        Windows = Array.AsReadOnly(_windows);
        Assessments = Array.AsReadOnly(_assessments);
        PersistentMatchedTileCount = built.PersistentMatchedTileCount;
        _worldHashHex = session.CurrentHash.HexDigest;
    }

    private static void ValidateScenario(CanonicalFoodSampleScenario s,
        int windows)
    {
        if (s is null ||
            string.IsNullOrEmpty(s.ScenarioId) || s.ScenarioId.Length > 40 ||
            s.ScenarioId.Any(c => !(char.IsAsciiLetterOrDigit(c) ||
                c is '_' or '-')) ||
            s.MinimumMatchedTilesPerWindow is < 1 or > 512 ||
            s.MinimumPairedCoveragePermille is < 0 or > 1000 ||
            s.MaximumLargestTileSharePermille is < 0 or > 1000 ||
            s.MinimumEligibleWindows < 1 || s.MinimumEligibleWindows > windows ||
            s.MinimumPersistentMatchedTiles is < 0 or > 512)
            throw new ArgumentOutOfRangeException(nameof(s),
                "Invalid or unbounded food-sample diagnostic thresholds.");
    }

    /// <summary>
    /// Pure criteria evaluator for stress tests. It does NOT certify input
    /// provenance; only FromVerifiedHistory does so from canonical receipts.
    /// </summary>
    public static CanonicalFoodSampleAssessment EvaluateHypotheticalCriteria(
        IReadOnlyList<CanonicalFoodSampleWindow> windows,
        int persistentMatchedTileCount,
        CanonicalFoodSampleScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(scenario);
        if (windows.Count is < 2 or > 7)
            throw new ArgumentOutOfRangeException(nameof(windows));
        ValidateScenario(scenario, windows.Count);
        if (persistentMatchedTileCount < 0 ||
            persistentMatchedTileCount > windows.Min(w => w.MatchedPositivePaymentTiles))
            throw new ArgumentOutOfRangeException(nameof(persistentMatchedTileCount));

        var paired = 0;
        var diverse = 0;
        var covered = 0;
        var concentratedEnough = 0;
        var eligible = 0;
        foreach (var w in windows)
        {
            if (w is null || w.SourceExecutedFoodRaw < 0 ||
                w.TargetExecutedFoodRaw < 0 ||
                w.MatchedCommonFoodRaw < 0 ||
                w.SourcePositivePaymentTiles < 0 ||
                w.TargetPositivePaymentTiles < 0 ||
                w.MatchedPositivePaymentTiles < 0 ||
                w.MatchedPositivePaymentTiles > w.SourcePositivePaymentTiles ||
                w.MatchedPositivePaymentTiles > w.TargetPositivePaymentTiles ||
                w.MatchedCommonFoodRaw > w.SourceExecutedFoodRaw ||
                w.MatchedCommonFoodRaw > w.TargetExecutedFoodRaw ||
                w.LargestSingleTileCommonFoodRaw < 0 ||
                w.LargestSingleTileCommonFoodRaw > w.MatchedCommonFoodRaw ||
                (w.MatchedPositivePaymentTiles == 0 &&
                    (w.MatchedCommonFoodRaw != 0 ||
                     w.LargestSingleTileCommonFoodRaw != 0)) ||
                (w.MatchedPositivePaymentTiles > 0 &&
                    (w.MatchedCommonFoodRaw <= 0 ||
                     w.LargestSingleTileCommonFoodRaw <= 0 ||
                     w.MatchedCommonFoodRaw <
                         w.LargestSingleTileCommonFoodRaw ||
                     w.MatchedCommonFoodRaw >
                         w.LargestSingleTileCommonFoodRaw *
                             w.MatchedPositivePaymentTiles)))
                throw new InvalidOperationException(
                    "Impossible food-sample volume, tile diversity or physical concentration.");
            if (!w.HasPairedPaidFoodPrices)
                continue; // zero observed prices is not a price of zero
            paired++;
            if (w.MatchedPositivePaymentTiles < scenario.MinimumMatchedTilesPerWindow)
                continue;
            diverse++;
            if (w.MatchedCommonFoodRaw * 1000 <
                    (BigInteger)scenario.MinimumPairedCoveragePermille *
                        w.SourceExecutedFoodRaw ||
                w.MatchedCommonFoodRaw * 1000 <
                    (BigInteger)scenario.MinimumPairedCoveragePermille *
                        w.TargetExecutedFoodRaw)
                continue;
            covered++;
            if (w.LargestSingleTileCommonFoodRaw * 1000 >
                    (BigInteger)scenario.MaximumLargestTileSharePermille *
                        w.MatchedCommonFoodRaw)
                continue;
            concentratedEnough++;
            eligible++;
        }

        var diagnostic = paired == 0
            ? CanonicalFoodSampleDiagnostic.NoPairedPaidFoodPrices
            : diverse == 0
                ? CanonicalFoodSampleDiagnostic.InsufficientMatchedTileDiversity
            : covered == 0
                ? CanonicalFoodSampleDiagnostic.InsufficientPairedPhysicalCoverage
            : concentratedEnough == 0
                ? CanonicalFoodSampleDiagnostic.ExcessiveSingleTileConcentration
            : eligible < scenario.MinimumEligibleWindows
                ? CanonicalFoodSampleDiagnostic.InsufficientEligibleWindows
            : persistentMatchedTileCount < scenario.MinimumPersistentMatchedTiles
                ? CanonicalFoodSampleDiagnostic.InsufficientPersistentTiles
                : CanonicalFoodSampleDiagnostic.WithinDeclaredFoodSampleCriteria;
        return new CanonicalFoodSampleAssessment(
            scenario.ScenarioId, windows.Count, paired, diverse, covered,
            concentratedEnough, eligible, persistentMatchedTileCount,
            diagnostic);
    }

    private static CanonicalFoodSampleReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalMultiCycleFoodRobustnessGate gate91,
        CanonicalFoodSampleScenario[] scenarios)
    {
        if (scenarios.Length is < 1 or > MaximumScenarios)
            throw new ArgumentOutOfRangeException(nameof(scenarios));
        gate91.RequireCurrent(session);
        gate91.VerifyIndependent();
        var history = gate91.SourceHistory;
        var cash = session.State.FoodCash ??
            throw new InvalidOperationException(
                "V12 actual local food cash receipts are not available.");
        var known = cash.Receipts
            .GroupBy(x => x.MovementTurn)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var rows = new List<CanonicalFoodSampleWindow>();
        HashSet<ulong>? persistent = null;
        foreach (var existing in history.Windows)
        {
            var before = (known.GetValueOrDefault(
                existing.SourceCycleMovementTurn) ??
                Array.Empty<PhysicalFoodCashReceipt>())
                .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
            var after = (known.GetValueOrDefault(
                existing.TargetCycleMovementTurn) ??
                Array.Empty<PhysicalFoodCashReceipt>())
                .ToDictionary(x => x.Tile.FineStrategicCellId.Value);
            BigInteger srcFood = 0, dstFood = 0;
            var srcPaid = 0;
            var dstPaid = 0;
            foreach (var r in before.Values)
            {
                srcFood += r.FoodRaw;
                if (r.PaidMicrocredits > 0 && r.PriceMicrocreditsPerUnit > 0)
                    srcPaid++;
            }
            foreach (var r in after.Values)
            {
                dstFood += r.FoodRaw;
                if (r.PaidMicrocredits > 0 && r.PriceMicrocreditsPerUnit > 0)
                    dstPaid++;
            }
            var pairedTiles = new HashSet<ulong>();
            BigInteger matched = 0, largest = 0, srcWeighted = 0, dstWeighted = 0;
            foreach (var (tile, r) in before)
            {
                if (!after.TryGetValue(tile, out var t) ||
                    r.FoodRaw == 0 || t.FoodRaw == 0 ||
                    r.PaidMicrocredits == 0 || t.PaidMicrocredits == 0 ||
                    r.PriceMicrocreditsPerUnit == 0 || t.PriceMicrocreditsPerUnit == 0)
                    continue;
                var common = Math.Min(r.FoodRaw, t.FoodRaw);
                pairedTiles.Add(tile);
                matched += common;
                largest = BigInteger.Max(largest, common);
                srcWeighted += (BigInteger)common * r.PriceMicrocreditsPerUnit;
                dstWeighted += (BigInteger)common * t.PriceMicrocreditsPerUnit;
            }
            persistent ??= new HashSet<ulong>(pairedTiles);
            persistent.IntersectWith(pairedTiles);
            if (srcFood != existing.SourceExecutedFoodRaw ||
                dstFood != existing.TargetExecutedFoodRaw ||
                matched != existing.CommonExecutedFoodRaw ||
                srcWeighted != existing.SourceMatchedWeightedPriceRaw ||
                dstWeighted != existing.TargetMatchedWeightedPriceRaw ||
                pairedTiles.Count != existing.MatchedTileCount)
                throw new InvalidOperationException(
                    "Independent tile-level food sample disagrees with the authenticated VS90 real economic interval.");

            rows.Add(new CanonicalFoodSampleWindow(
                existing.SourceCycleMovementTurn,
                existing.TargetCycleMovementTurn,
                srcPaid, dstPaid, pairedTiles.Count, srcFood, dstFood,
                matched, largest));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var policies = new CanonicalFoodSampleAssessment[scenarios.Length];
        for (var i = 0; i < scenarios.Length; i++)
        {
            ValidateScenario(scenarios[i], rows.Count);
            if (!names.Add(scenarios[i].ScenarioId))
                throw new InvalidOperationException(
                    "Food-sample criterion identifiers must be unique.");
            policies[i] = EvaluateHypotheticalCriteria(
                rows, persistent?.Count ?? 0, scenarios[i]);
        }
        return new CanonicalFoodSampleReconstruction(
            rows.ToArray(), persistent?.Count ?? 0, policies);
    }

    public static CanonicalExecutedFoodSampleAudit FromVerifiedRobustness(
        CanonicalPhysicalTacticalSession session,
        CanonicalMultiCycleFoodRobustnessGate certifiedGate91,
        IEnumerable<CanonicalFoodSampleScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(certifiedGate91);
        ArgumentNullException.ThrowIfNull(scenarios);
        var items = scenarios.ToArray();
        var built = Reconstruct(session, certifiedGate91, items);
        var result = new CanonicalExecutedFoodSampleAudit(
            session, certifiedGate91, items, built);
        result.VerifyIndependent();
        return result;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session) ||
            _worldHashHex != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "Food sample diagnostics are stale or belong to another real session.");
        _gate91.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Reconstruct(_session, _gate91, _scenarios);
        if (!_windows.SequenceEqual(rebuilt.Windows) ||
            !_assessments.SequenceEqual(rebuilt.Assessments) ||
            PersistentMatchedTileCount != rebuilt.PersistentMatchedTileCount)
            throw new InvalidOperationException(
                "Independent VS92 food sample tile/coverage audit diverged from real paid receipts.");
        RequireCurrent(_session);
    }
}
