using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Status of the evidence boundary, NOT a physiological diagnosis. The
/// existing physical engine closes food as one aggregate CommodityId(1).
/// An exhaustive six-food ledger and calibrated normalization are required
/// before nutritional quality can be measured.
/// </summary>
public enum CanonicalNutritionEvidenceStatus : byte
{
    NoFoodPhysicallyConsumed = 1,
    LegacyAggregateFoodOnly = 2,
    IncompleteFoodVarietyAccounting = 3
}

/// <summary>
/// A genuine, executed destination use. These are real stock/cargo debits,
/// not economic demand, a purchase quote, a stock position, or FIFO proof
/// of which historic producer supplied the consumed material.
/// </summary>
public readonly record struct CanonicalNutritionExecutedUse(
    ulong DestinationFineTile,
    uint Commodity,
    CanonicalPhysicalFinalUseChannel Channel,
    ulong JourneyOrderId,
    ulong ConsumedRaw);

/// <summary>
/// GA-NUTR-INTEGRATION-01: zero-mutation boundary between an authenticated
/// closed physical cycle and future nutritional evaluation. It preserves
/// every actual local/expedition consumption identity and separately verifies
/// the deterministic FIFO accounting witness. It deliberately NEVER computes
/// Q, A(f), f or D for the legacy aggregate food commodity.
/// </summary>
public sealed class CanonicalNutritionExecutedConsumptionProjection
{
    private readonly CanonicalPhysicalTacticalSession _source;
    private readonly CanonicalExecutedPhysicalCycleProvenance _physical;
    private readonly CanonicalPhysicalLotCycleWitness _lots;
    private readonly CanonicalNutritionExecutedUse[] _uses;

    public ulong CycleOrdinal => _physical.CycleOrdinal;
    public ulong ClosingMovementTurn => _physical.ClosingMovementTurn;
    public string ClosingWorldHashHex => _physical.ClosingWorldHashHex;
    public ulong TotalRealPopulation => _physical.TotalRealPopulation;
    public ulong TotalConsumedRaw => _physical.ActuallyConsumedRaw;
    public ulong TotalUnmetReferenceRaw => _physical.ActuallyUnmetFoodRaw;
    public ulong ResidentLocalConsumedRaw { get; }
    public ulong ExpeditionRationsConsumedRaw { get; }
    public int ObservedPositiveFoodCommodityCount { get; }
    public CanonicalNutritionEvidenceStatus Status { get; }
    public IReadOnlyList<CanonicalNutritionExecutedUse> ExecutedUses { get; }

    public bool ContainsOnlyExecutedFinalUse => true;
    public bool HasReconciledPhysicalLotAccounting => true;
    public bool TreatsFifoLotOriginsAsAuthenticatedIdentity => false;
    public bool DistinguishesLocalFromExpeditionConsumption => true;
    public bool HasPerPersonFoodVarietyConsumption => false;
    public bool HasCalibratedNutritionalConversions => false;
    public bool CanComputeNutritionalQuality => false;
    public double? NutritionalQuality => null;
    public bool CanAuthorizeProductivityEffect => false;
    public bool CanAuthorizeRealPurchasingPower => false;
    public bool ChangesCanonicalWorldOrSave => false;

    private CanonicalNutritionExecutedConsumptionProjection(
        CanonicalPhysicalTacticalSession source,
        CanonicalExecutedPhysicalCycleProvenance physical,
        CanonicalPhysicalLotCycleWitness lots)
    {
        _source = source;
        _physical = physical;
        _lots = lots;
        _physical.RequireCurrent(source);
        _lots.RequireCurrent(source);
        _physical.VerifyIndependent();
        _lots.VerifyIndependent();

        if (_physical.CycleOrdinal != _lots.CycleOrdinal ||
            _physical.ClosingMovementTurn != _lots.ClosingMovementTurn ||
            _physical.ClosingWorldHashHex != _lots.ClosingWorldHashHex)
            throw new InvalidOperationException(
                "Physical final-use and lot evidence disagree on the closed cycle.");

        _uses = _physical.FinalUse.Select(x => new CanonicalNutritionExecutedUse(
                x.DestinationFineTile, x.Commodity, x.Channel,
                x.JourneyOrderId, x.ConsumedRaw))
            .OrderBy(x => x.DestinationFineTile)
            .ThenBy(x => x.Commodity)
            .ThenBy(x => x.Channel)
            .ThenBy(x => x.JourneyOrderId)
            .ToArray();

        var attestedUse = _lots.FinalUseAttributions
            .GroupBy(x => (x.DestinationFineTile, x.Commodity,
                Channel: x.UseChannel, x.JourneyOrderId))
            .ToDictionary(g => g.Key, g =>
                g.Aggregate(BigInteger.Zero, (a, x) => a + x.RawQuantity));
        var physicalUse = _uses
            .GroupBy(x => (x.DestinationFineTile, x.Commodity,
                x.Channel, x.JourneyOrderId))
            .ToDictionary(g => g.Key, g =>
                g.Aggregate(BigInteger.Zero, (a, x) => a + x.ConsumedRaw));
        if (physicalUse.Count != attestedUse.Count ||
            physicalUse.Any(x => !attestedUse.TryGetValue(x.Key, out var quantity) ||
                quantity != x.Value))
            throw new InvalidOperationException(
                "Executed final use must reconcile with conserved FIFO consumption.");

        var allConsumed = _uses.Aggregate(BigInteger.Zero, (a, x) =>
            a + x.ConsumedRaw);
        var local = _uses.Where(x =>
                x.Channel == CanonicalPhysicalFinalUseChannel.ResidentLocal)
            .Aggregate(BigInteger.Zero, (a, x) => a + x.ConsumedRaw);
        var expedition = _uses.Where(x =>
                x.Channel == CanonicalPhysicalFinalUseChannel.ExpeditionRations)
            .Aggregate(BigInteger.Zero, (a, x) => a + x.ConsumedRaw);

        if (allConsumed != new BigInteger(_physical.ActuallyConsumedRaw) ||
            local + expedition != allConsumed ||
            _physical.TotalRealPopulation != source.State.TotalWorkers)
            throw new InvalidOperationException(
                "Recorded nutrition evidence does not match actual physical consumption.");

        ResidentLocalConsumedRaw = checked((ulong)local);
        ExpeditionRationsConsumedRaw = checked((ulong)expedition);
        ObservedPositiveFoodCommodityCount = _uses
            .Select(x => x.Commodity).Distinct().Count();
        Status = allConsumed == BigInteger.Zero
            ? CanonicalNutritionEvidenceStatus.NoFoodPhysicallyConsumed
            : ObservedPositiveFoodCommodityCount == 1 &&
              _uses.All(x => x.Commodity == 1U)
                ? CanonicalNutritionEvidenceStatus.LegacyAggregateFoodOnly
                : CanonicalNutritionEvidenceStatus.IncompleteFoodVarietyAccounting;
        ExecutedUses = Array.AsReadOnly(_uses);
        RequireCurrent(source);
    }

    /// <summary>
    /// Returns false for no completed cycle or a non-closing/action-modified
    /// state; does not reuse a stale prior receipt as current nutrition.
    /// Corrupt or contradictory evidence still throws in the strict path.
    /// </summary>
    public static bool TryCaptureCurrent(
        CanonicalPhysicalTacticalSession session,
        out CanonicalNutritionExecutedConsumptionProjection? projection)
    {
        ArgumentNullException.ThrowIfNull(session);
        projection = null;
        if (session.State.Cycles.Count == 0 ||
            session.State.MovementTurn != session.State.Cycles[^1].MovementTurn ||
            session.Events.Count == 0 ||
            session.Events[^1].Kind != "tick" ||
            session.Events[^1].AfterHashHex != session.CurrentHash.HexDigest)
            return false;

        projection = FromCurrentExecutedCycle(session);
        return true;
    }

    public static CanonicalNutritionExecutedConsumptionProjection
        FromCurrentExecutedCycle(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // These two witnesses independently verify the live canonical
        // closing hash, physical balance and FIFO custody before extraction.
        var physical = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var lots = session.CaptureLatestPhysicalLotWitness();
        return new(session, physical, lots);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(session, _source))
            throw new InvalidOperationException(
                "Nutrition consumption projection belongs to a foreign canonical session.");
        _physical.RequireCurrent(session);
        _lots.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_source);
        _physical.VerifyIndependent();
        _lots.VerifyIndependent();
        if (_uses.Aggregate(BigInteger.Zero, (a, x) =>
                a + x.ConsumedRaw) != new BigInteger(TotalConsumedRaw) ||
            (BigInteger)ResidentLocalConsumedRaw +
                ExpeditionRationsConsumedRaw != TotalConsumedRaw)
            throw new InvalidOperationException(
                "Nutrition consumption projection lost its physical conservation.");
        RequireCurrent(_source);
    }
}
