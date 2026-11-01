using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One pair of REAL consecutive canonical cash-food settlements, aggregated
/// only over the same tiles that actually paid for local food in both cycles.
/// The nominal price unit is V12 microcredits per WHOLE food unit.
/// </summary>
public sealed record CanonicalMultiCycleExecutedFoodWindow(
    ulong SourceCycleMovementTurn,
    ulong TargetCycleMovementTurn,
    BigInteger SourceExecutedFoodRaw,
    BigInteger TargetExecutedFoodRaw,
    BigInteger CommonExecutedFoodRaw,
    BigInteger SourceMatchedWeightedPriceRaw,
    BigInteger TargetMatchedWeightedPriceRaw,
    int MatchedTileCount)
{
    public bool HasComparableExecutedSales => MatchedTileCount > 0;
    public bool HasCompletePhysicalCoverage =>
        HasComparableExecutedSales &&
        CommonExecutedFoodRaw == SourceExecutedFoodRaw &&
        CommonExecutedFoodRaw == TargetExecutedFoodRaw;
}

/// <summary>
/// Count flips ONLY between adjacent comparable windows having nonzero
/// bounded advisory responses. Missing windows and integer zero responses
/// break continuity; never interpolate historical prices.
/// </summary>
public sealed record CanonicalMultiCycleFoodGainSensitivity(
    int CandidateStepPermille,
    int ComparableWindowCount,
    int NonzeroWindowCount,
    int AdjacentNonzeroDirectionalReversals,
    int MaximumAbsoluteAdvisoryDeltaPermille,
    int LastExecutedAdvisoryDeltaPermille);

internal sealed record CanonicalMultiCycleFoodStabilityReconstruction(
    CanonicalMultiCycleExecutedFoodWindow[] Windows,
    CanonicalMultiCycleFoodGainSensitivity[] Sensitivities);

/// <summary>
/// VS90: the historical series is rebuilt from actual V12 cash receipts
/// already present in the canonical WorldState. Only the LAST pair has an
/// authenticated VS89 hypothetical-price comparison. Earlier VS84 quotes
/// were not saved and must NOT be inferred or invented for prior cycles.
/// </summary>
public sealed class CanonicalMultiCycleFoodPriceStabilityReport
{
    public const int MinimumEconomicCycles = 3;
    public const int MaximumEconomicCycles = 8;

    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalFoodPriceCalibrationMatrix _currentMatrix;
    private readonly CanonicalMultiCycleExecutedFoodWindow[] _windows;
    private readonly CanonicalMultiCycleFoodGainSensitivity[] _sensitivities;
    private readonly int _economicCycleCount;
    private readonly string _worldHashHex;

    public IReadOnlyList<CanonicalMultiCycleExecutedFoodWindow> Windows { get; }
    public IReadOnlyList<CanonicalMultiCycleFoodGainSensitivity> GainSensitivities { get; }
    public int ObservedEconomicCycles => _economicCycleCount;
    public int ComparableWindowCount => _windows.Count(x => x.HasComparableExecutedSales);
    public int MissingComparableWindowCount => _windows.Length - ComparableWindowCount;
    public bool HasCompleteComparableHistory => ComparableWindowCount == _windows.Length;
    public bool UsesActualCanonicalFoodCashHistory => true;
    public bool ReconstructsHistoricalHypotheticalVs84Prices => false;
    public bool ClaimsHistoricalPredictionAccuracy => false;
    public bool ProvesCausality => false;
    public bool AutomaticallyChoosesGain => false;
    public bool ModifiesPricesProductionOrSave => false;
    public bool CanAuthorizeActualSettlement => false;
    public bool IntroducesStocksOrCredit => false;

    private CanonicalMultiCycleFoodPriceStabilityReport(
        CanonicalPhysicalTacticalSession session,
        CanonicalFoodPriceCalibrationMatrix matrix,
        int cycleCount, CanonicalMultiCycleFoodStabilityReconstruction built)
    {
        _session = session;
        _currentMatrix = matrix;
        _economicCycleCount = cycleCount;
        _worldHashHex = session.CurrentHash.HexDigest;
        _windows = (CanonicalMultiCycleExecutedFoodWindow[])built.Windows.Clone();
        _sensitivities = (CanonicalMultiCycleFoodGainSensitivity[])built.Sensitivities.Clone();
        Windows = Array.AsReadOnly(_windows);
        GainSensitivities = Array.AsReadOnly(_sensitivities);
    }

    public static int CountNonzeroAdjacentReversals(
        IEnumerable<int?> windowDeltas)
    {
        ArgumentNullException.ThrowIfNull(windowDeltas);
        var reversals = 0;
        var prior = 0;
        foreach (var v in windowDeltas)
        {
            if (v is null || v.Value == 0)
            {
                prior = 0;
                continue;
            }
            var sign = Math.Sign(v.Value);
            if (prior != 0 && sign != prior)
                reversals++;
            prior = sign;
        }
        return reversals;
    }

    private static int Response(
        CanonicalMultiCycleExecutedFoodWindow window, int step)
    {
        if (!window.HasComparableExecutedSales)
            return 0; // diagnostic absence, NEVER a price of zero.
        var before = window.SourceMatchedWeightedPriceRaw;
        var after = window.TargetMatchedWeightedPriceRaw;
        var sum = before + after;
        if (before <= 0 || after <= 0 || sum <= 0)
            throw new InvalidOperationException(
                "A comparable paid-food market window has invalid executed nominal price aggregates.");
        var raw = (after - before) * step / sum;
        if (raw > step || raw < -step)
            throw new InvalidOperationException("Food trend cannot exceed its bounded diagnostic gain.");
        return checked((int)raw);
    }

    private static CanonicalMultiCycleFoodStabilityReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalFoodPriceCalibrationMatrix matrix, int count)
    {
        if (count is < MinimumEconomicCycles or > MaximumEconomicCycles)
            throw new ArgumentOutOfRangeException(nameof(count),
                "Historical stability requires 3..8 actually closed economic cycles.");
        matrix.RequireCurrent(session);
        matrix.VerifyIndependent();
        var state = session.State;
        if (state.Cycles.Count < count ||
            state.Cycles[^1].MovementTurn != state.MovementTurn ||
            matrix.TargetCycleMovementTurn != state.Cycles[^1].MovementTurn ||
            matrix.SourceCycleMovementTurn != state.Cycles[^2].MovementTurn ||
            session.Events.Count == 0 ||
            session.Events[^1].Kind != "tick" ||
            session.Events[^1].AfterHashHex != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "The most recent historical interval must be the real VS89 closing cycle, with enough completed prior cycles.");

        var cycles = state.Cycles.Skip(state.Cycles.Count - count).ToArray();
        for (var i = 1; i < cycles.Length; i++)
        {
            if (cycles[i].MovementTurn <= cycles[i - 1].MovementTurn)
                throw new InvalidOperationException(
                    "Canonical historical economic cycles are not strictly monotone.");
        }

        // This ledger is the canonical V12 EXECUTED food-cash settlement
        // history. VS87 validates all receipt payment arithmetic, duplication,
        // positive volume and chronological ordering for the latest witness.
        var receipts = state.FoodCash?.Receipts ??
            throw new InvalidOperationException(
                "Canonical V12 cash-food history is unavailable.");
        var cycleTurns = cycles.Select(x => x.MovementTurn).ToHashSet();
        var groups = receipts
            .Where(x => cycleTurns.Contains(x.MovementTurn))
            .GroupBy(x => x.MovementTurn)
            .ToDictionary(x => x.Key, x => x.ToArray());

        var byCycle = new Dictionary<ulong, Dictionary<ulong, (ulong Food, ulong Price, ulong Paid)>>();
        foreach (var c in cycles)
        {
            var observed = groups.GetValueOrDefault(c.MovementTurn) ??
                Array.Empty<PhysicalFoodCashReceipt>();
            var map = new Dictionary<ulong, (ulong Food, ulong Price, ulong Paid)>();
            BigInteger consumed = BigInteger.Zero;
            foreach (var receipt in observed)
            {
                var tile = receipt.Tile.FineStrategicCellId.Value;
                if (tile == 0UL || receipt.FoodRaw == 0UL ||
                    !map.TryAdd(tile, (receipt.FoodRaw,
                        receipt.PriceMicrocreditsPerUnit,
                        receipt.PaidMicrocredits)) ||
                    (BigInteger)receipt.FoodRaw * receipt.PriceMicrocreditsPerUnit /
                        PhysicalFoodCashMarketState.MonetaryScale !=
                        receipt.PaidMicrocredits)
                    throw new InvalidOperationException(
                        "Historical cash-food receipt is duplicated, nonphysical or financially inconsistent.");
                consumed += receipt.FoodRaw;
            }
            if (consumed > c.FoodConsumedRaw)
                throw new InvalidOperationException(
                    "Historical paid food exceeded canonical physically consumed food.");
            byCycle.Add(c.MovementTurn, map);
        }

        var windows = new List<CanonicalMultiCycleExecutedFoodWindow>();
        for (var i = 1; i < cycles.Length; i++)
        {
            var earlier = byCycle[cycles[i - 1].MovementTurn];
            var later = byCycle[cycles[i].MovementTurn];
            BigInteger sourceVolume = BigInteger.Zero, targetVolume = BigInteger.Zero;
            BigInteger matched = BigInteger.Zero, left = BigInteger.Zero, right = BigInteger.Zero;
            var tiles = 0;
            foreach (var entry in earlier)
                sourceVolume += entry.Value.Food;
            foreach (var entry in later)
                targetVolume += entry.Value.Food;
            foreach (var entry in earlier.OrderBy(x => x.Key))
            {
                if (!later.TryGetValue(entry.Key, out var target) ||
                    entry.Value.Paid == 0 || target.Paid == 0 ||
                    entry.Value.Price == 0 || target.Price == 0)
                    continue;
                var qty = Math.Min(entry.Value.Food, target.Food);
                matched += qty;
                left += (BigInteger)qty * entry.Value.Price;
                right += (BigInteger)qty * target.Price;
                tiles++;
            }
            if (matched > sourceVolume || matched > targetVolume ||
                (tiles == 0 && (matched != 0 || left != 0 || right != 0)))
                throw new InvalidOperationException(
                    "Historical common-food sampling exceeds physically executed local purchases.");
            windows.Add(new CanonicalMultiCycleExecutedFoodWindow(
                cycles[i - 1].MovementTurn, cycles[i].MovementTurn,
                sourceVolume, targetVolume, matched, left, right, tiles));
        }

        // At the only currently certified VS89 source/target pair, ensure
        // the independent historical ledger precisely reconciles to VS89.
        var last = windows[^1];
        if (last.SourceExecutedFoodRaw != matrix.SourceExecutedFoodRaw ||
            last.TargetExecutedFoodRaw != matrix.TargetExecutedFoodRaw ||
            last.CommonExecutedFoodRaw != matrix.MatchedCommonFoodRaw ||
            last.SourceMatchedWeightedPriceRaw !=
                matrix.SourceMatchedWeightedNominalPriceRaw ||
            last.TargetMatchedWeightedPriceRaw !=
                matrix.TargetMatchedWeightedNominalPriceRaw)
            throw new InvalidOperationException(
                "Latest historical executed-food interval differs from certified VS89 benchmark.");

        var sensitivities = new List<CanonicalMultiCycleFoodGainSensitivity>();
        foreach (var candidate in matrix.Candidates)
        {
            var step = candidate.CandidateStepPermille;
            var signals = windows.Select(w => w.HasComparableExecutedSales
                ? (int?)Response(w, step) : null).ToArray();
            if (signals[^1] != candidate.Vs88ExecutedFoodPriceTrendDeltaPermille)
                throw new InvalidOperationException(
                    "Last actual historical response differs from certified VS89 price observation.");
            sensitivities.Add(new CanonicalMultiCycleFoodGainSensitivity(
                step,
                signals.Count(x => x.HasValue),
                signals.Count(x => x.HasValue && x.Value != 0),
                CountNonzeroAdjacentReversals(signals),
                signals.Where(x => x.HasValue).Select(x => Math.Abs(x!.Value))
                    .DefaultIfEmpty(0).Max(),
                signals[^1] ?? 0));
        }
        return new CanonicalMultiCycleFoodStabilityReconstruction(
            windows.ToArray(), sensitivities.ToArray());
    }

    public static CanonicalMultiCycleFoodPriceStabilityReport FromVerifiedMatrix(
        CanonicalPhysicalTacticalSession session,
        CanonicalFoodPriceCalibrationMatrix currentMatrix,
        int observedEconomicCycles)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentMatrix);
        var data = Reconstruct(session, currentMatrix, observedEconomicCycles);
        var result = new CanonicalMultiCycleFoodPriceStabilityReport(
            session, currentMatrix, observedEconomicCycles, data);
        result.VerifyIndependent();
        return result;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash.HexDigest != _worldHashHex)
            throw new InvalidOperationException(
                "Historical food stability belongs to another or stale physical session.");
        _currentMatrix.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var computed = Reconstruct(_session, _currentMatrix, _economicCycleCount);
        if (!_windows.SequenceEqual(computed.Windows) ||
            !_sensitivities.SequenceEqual(computed.Sensitivities))
            throw new InvalidOperationException(
                "Independent historical food-price audit diverged from canonical executed receipts.");
        RequireCurrent(_session);
    }
}
