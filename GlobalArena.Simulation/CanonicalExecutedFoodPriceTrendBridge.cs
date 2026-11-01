using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// An actual matched-tile observation of TWO executed V12 cash-food receipts.
/// Nominal units never mix with the dimensionless VS78 local price index.
/// </summary>
public sealed record CanonicalExecutedFoodPriceMatchedTile(
    ulong FineTile, ulong SourceExecutedFoodRaw, ulong TargetExecutedFoodRaw,
    ulong SourcePriceMicrocreditsPerWholeUnit,
    ulong TargetPriceMicrocreditsPerWholeUnit,
    ulong CommonObservedFoodRaw);

internal sealed record CanonicalExecutedFoodPriceTrendComputation(
    CanonicalExecutedFoodPriceMatchedTile[] MatchedTiles,
    BigInteger SourceCommonWeightedNominalPriceRaw,
    BigInteger TargetCommonWeightedNominalPriceRaw,
    int DirectionalAdvisoryDeltaPermille,
    int SuggestedAgricultureUtilizationPermille,
    int PriorHypotheticalAgricultureUtilizationPermille,
    int UnchangedAgricultureRetentionPermille);

/// <summary>
/// VS88: same-cohort executed FOOD-only nominal price trend from actual
/// consecutive physical economic cycles. An optional agriculture utilization
/// response is an untrusted experimental ADVISORY, never executed policy.
/// </summary>
public sealed class CanonicalExecutedFoodPriceTrendBridge
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalCycleStampedProductionProjection _cycleProof;
    private readonly CanonicalExecutedLocalFoodPriceWitness _currentExecuted;
    private readonly CanonicalExecutedFoodPriceMatchedTile[] _matched;
    private readonly string _worldHashHex;
    private readonly int _maximumAdvisoryStepPermille;

    public ulong SourceCycleMovementTurn { get; }
    public ulong TargetCycleMovementTurn { get; }
    public IReadOnlyList<CanonicalExecutedFoodPriceMatchedTile> MatchedTiles { get; }
    public BigInteger SourceCommonWeightedNominalPriceRaw { get; }
    public BigInteger TargetCommonWeightedNominalPriceRaw { get; }
    public bool HasComparableExecutedFoodPrice => _matched.Length > 0;
    public int MaximumAdvisoryStepPermille => _maximumAdvisoryStepPermille;
    public int PriorHypotheticalAgricultureUtilizationPermille { get; }
    public int UnchangedAgricultureRetentionPermille { get; }
    public int DirectionalAdvisoryDeltaPermille { get; }
    public int SuggestedAgricultureUtilizationPermille { get; }

    public bool UsesActualExecutedFoodCashReceipts => true;
    public bool UsesVs78HypotheticalPricesAsPayments => false;
    public bool EstablishesActualOtherCommodityPrices => false;
    public bool IsEconomicCausalityProof => false;
    public bool ChangesActualWorkforceOrProduction => false;
    public bool CreatesInventoryOrCredit => false;
    public bool AuthorizesRealSettlement => false;

    private CanonicalExecutedFoodPriceTrendBridge(
        CanonicalPhysicalTacticalSession session,
        CanonicalCycleStampedProductionProjection cycleProof,
        CanonicalExecutedLocalFoodPriceWitness currentExecuted,
        int step,
        CanonicalExecutedFoodPriceTrendComputation computed)
    {
        _session = session;
        _cycleProof = cycleProof;
        _currentExecuted = currentExecuted;
        _maximumAdvisoryStepPermille = step;
        _worldHashHex = session.CurrentHash.HexDigest;
        SourceCycleMovementTurn = cycleProof.SourcePriceReceipt.SourceMovementTurn;
        TargetCycleMovementTurn = cycleProof.NextRealCompletedEconomicCycleTurn;
        _matched = computed.MatchedTiles.ToArray();
        MatchedTiles = Array.AsReadOnly(_matched);
        SourceCommonWeightedNominalPriceRaw =
            computed.SourceCommonWeightedNominalPriceRaw;
        TargetCommonWeightedNominalPriceRaw =
            computed.TargetCommonWeightedNominalPriceRaw;
        PriorHypotheticalAgricultureUtilizationPermille =
            computed.PriorHypotheticalAgricultureUtilizationPermille;
        UnchangedAgricultureRetentionPermille =
            computed.UnchangedAgricultureRetentionPermille;
        DirectionalAdvisoryDeltaPermille =
            computed.DirectionalAdvisoryDeltaPermille;
        SuggestedAgricultureUtilizationPermille =
            computed.SuggestedAgricultureUtilizationPermille;
    }

    private static CanonicalExecutedFoodPriceTrendComputation Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalCycleStampedProductionProjection cycleProof,
        CanonicalExecutedLocalFoodPriceWitness currentExecuted,
        int step)
    {
        if (step is < 1 or > CanonicalLaggedProductionResponse.MaximumResponseStepPermille)
            throw new ArgumentOutOfRangeException(nameof(step));
        cycleProof.RequireCurrent(session);
        cycleProof.VerifyIndependent();
        currentExecuted.RequireCurrent(session);
        currentExecuted.VerifyIndependent();
        // No receipts is a valid zero-observation case: never make up a
        // price or use hypothetical VS78 indices to fill this gap.

        var previous = cycleProof.SourcePriceReceipt;
        var currentState = session.State;
        var sourceTurn = previous.SourceMovementTurn;
        var targetTurn = cycleProof.NextRealCompletedEconomicCycleTurn;
        if (currentExecuted.CompletedEconomicCycleMovementTurn != targetTurn ||
            currentExecuted.SourceCanonicalWorldHashHex !=
                cycleProof.NextRealCompletedEconomicCycleWorldHashHex ||
            targetTurn != currentState.MovementTurn)
            throw new InvalidOperationException(
                "Only the same authentic next canonical settlement may join the real cash witness and hypothetical VS85 production report.");
        var sourceCycle = currentState.Cycles.SingleOrDefault(
            x => x.MovementTurn == sourceTurn) ??
            throw new InvalidOperationException(
                "Source settlement is not recorded in the canonical cycle register.");

        // V12 cash receipts are actual recorded transactions, but only
        // for local food. The source must be an actual completed cycle.
        var ledger = currentState.FoodCash ??
            throw new InvalidOperationException(
                "Executed prices require the authentic V12 local food cash ledger.");
        var sources = ledger.Receipts
            .Where(x => x.MovementTurn == sourceTurn).ToArray();
        if (sources.GroupBy(x => x.Tile).Any(x => x.Count() != 1))
            throw new InvalidOperationException(
                "A source cycle has duplicate local food payments.");
        var sourceFood = sources.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.FoodRaw);
        if (sourceFood > sourceCycle.FoodConsumedRaw)
            throw new InvalidOperationException(
                "Historical executed local food is larger than the physical food consumed.");

        var targetByTile = currentExecuted.ExecutedReceipts
            .ToDictionary(x => x.FineTile);
        var matched = new List<CanonicalExecutedFoodPriceMatchedTile>();
        BigInteger beforePriceWeighted = BigInteger.Zero;
        BigInteger afterPriceWeighted = BigInteger.Zero;
        foreach (var s in sources.OrderBy(x => x.Tile.FineStrategicCellId.Value))
        {
            var id = s.Tile.FineStrategicCellId.Value;
            if (!targetByTile.TryGetValue(id, out var t) ||
                s.FoodRaw == 0UL || t.ExecutedFoodRaw == 0UL ||
                s.PaidMicrocredits == 0UL || t.ActuallyPaidMicrocredits == 0UL ||
                s.PriceMicrocreditsPerUnit == 0UL ||
                t.ExecutedPriceMicrocreditsPerWholeUnit == 0UL)
                continue;
            var common = Math.Min(s.FoodRaw, t.ExecutedFoodRaw);
            beforePriceWeighted += (BigInteger)common *
                s.PriceMicrocreditsPerUnit;
            afterPriceWeighted += (BigInteger)common *
                t.ExecutedPriceMicrocreditsPerWholeUnit;
            matched.Add(new CanonicalExecutedFoodPriceMatchedTile(
                id, s.FoodRaw, t.ExecutedFoodRaw,
                s.PriceMicrocreditsPerUnit,
                t.ExecutedPriceMicrocreditsPerWholeUnit, common));
        }

        // This trial is explicitly only an advisory for agriculture, based
        // on the hypothetical VS85 utilization level, not actual production.
        var agri = cycleProof.HypotheticalNextProduction.Policies.SingleOrDefault(
            x => x.Activity == EconomicActivityKind.Agriculture);
        if (agri is null)
            throw new InvalidOperationException(
                "The primitive agricultural workforce has no VS85 experiment to advise.");
        var delta = 0;
        if (matched.Count != 0)
        {
            var denom = beforePriceWeighted + afterPriceWeighted;
            if (denom == 0)
                throw new InvalidOperationException("Matched cash prices cannot both be zero.");
            var proposed = (afterPriceWeighted - beforePriceWeighted) * step / denom;
            if (proposed < -step || proposed > step)
                throw new InvalidOperationException("Actual food price trend was not bounded.");
            delta = checked((int)proposed);
        }
        return new CanonicalExecutedFoodPriceTrendComputation(
            matched.ToArray(), beforePriceWeighted, afterPriceWeighted,
            delta, Math.Clamp(agri.UtilizationPermille + delta, 0, 1000),
            agri.UtilizationPermille, agri.RetentionPermille);
    }

    public static CanonicalExecutedFoodPriceTrendBridge FromConsecutiveSettlements(
        CanonicalPhysicalTacticalSession session,
        CanonicalCycleStampedProductionProjection cycleProof,
        CanonicalExecutedLocalFoodPriceWitness currentExecuted,
        int maximumAdvisoryStepPermille)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(cycleProof);
        ArgumentNullException.ThrowIfNull(currentExecuted);
        var result = Reconstruct(session, cycleProof, currentExecuted,
            maximumAdvisoryStepPermille);
        var answer = new CanonicalExecutedFoodPriceTrendBridge(
            session, cycleProof, currentExecuted, maximumAdvisoryStepPermille,
            result);
        answer.VerifyIndependent();
        return answer;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session) ||
            session.CurrentHash.HexDigest != _worldHashHex)
            throw new InvalidOperationException(
                "Executed food trend is foreign to or stale against the live canonical world.");
        _cycleProof.RequireCurrent(session);
        _currentExecuted.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Reconstruct(_session, _cycleProof, _currentExecuted,
            _maximumAdvisoryStepPermille);
        if (!_matched.SequenceEqual(replay.MatchedTiles) ||
            SourceCommonWeightedNominalPriceRaw !=
                replay.SourceCommonWeightedNominalPriceRaw ||
            TargetCommonWeightedNominalPriceRaw !=
                replay.TargetCommonWeightedNominalPriceRaw ||
            DirectionalAdvisoryDeltaPermille !=
                replay.DirectionalAdvisoryDeltaPermille ||
            SuggestedAgricultureUtilizationPermille !=
                replay.SuggestedAgricultureUtilizationPermille ||
            PriorHypotheticalAgricultureUtilizationPermille !=
                replay.PriorHypotheticalAgricultureUtilizationPermille ||
            UnchangedAgricultureRetentionPermille !=
                replay.UnchangedAgricultureRetentionPermille)
            throw new InvalidOperationException(
                "Reconstructed actual food trend and bounded hypothetical agriculture advisory diverged.");
        RequireCurrent(_session);
    }
}
