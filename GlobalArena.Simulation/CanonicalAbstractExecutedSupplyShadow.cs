using System.Numerics;
using GlobalArena.Economy;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Source of genuinely executed output in the last completed physical cycle.
/// This is not proof that the output was offered to, sold in, or finally
/// consumed by any market. Expedition camp food remains expedition custody.
/// </summary>
public readonly record struct CanonicalAbstractSupplySource(
    ulong SourceFineTile, uint Commodity,
    CanonicalPhysicalProductionChannel Channel,
    EconomicActivityKind? Activity, ulong JourneyOrderId, ulong ProducedRaw);

/// <summary>
/// GA-ECON-ABSTRACT-SUPPLY-01: a read-only candidate market quote grounded in
/// executed physical PRODUCTION, as distinct from hypothetical output capacity.
/// Retention, N and the price curve are explicit CANDIDATE inputs, never inferred
/// from legacy cash balances, food demand, sales, or historical defaults.
/// It does not certify that produced goods were available to market (they may
/// already have been consumed), nor that a real offer or sale took place.
/// </summary>
public sealed class CanonicalAbstractExecutedSupplyShadow
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalExecutedPhysicalCycleProvenance _evidence;
    private readonly CanonicalAbstractSupplySource[] _sources;

    public ulong CycleOrdinal => _evidence.CycleOrdinal;
    public ulong ClosingMovementTurn => _evidence.ClosingMovementTurn;
    public string ClosingWorldHashHex => _evidence.ClosingWorldHashHex;
    public uint Commodity { get; }
    public ulong RealPopulation => _evidence.TotalRealPopulation;
    public BigInteger ActuallyProducedRaw { get; }
    public ulong CandidateRetentionRaw { get; }
    public BigInteger CandidateOfferedRaw => ActuallyProducedRaw - CandidateRetentionRaw;
    public ulong RawUnitsPerEconomicUnit { get; }
    public IReadOnlyList<CanonicalAbstractSupplySource> ExecutedProductionSources { get; }
    public PopulationPriceQuote CandidateQuote { get; }

    public bool HasAuthenticatedExecutedProduction => true;
    public bool AuthenticatesActualMarketOffer => false;
    public bool AuthenticatesFinalUseOrCrossCycleStockVintage => false;
    public bool AuthenticatesPurchasingPowerN => false;
    public bool HasApprovedPriceCurve => false;
    public bool CapsPhysicalSupplyByEffectiveDemand => false;
    public bool UsesLegacyCashBalances => false;
    public bool ChangesPhysicalStocksOrCurrency => false;
    public bool MutatesCanonicalSaveOrHash => false;
    public bool CanAuthorizeExecutedSettlement => false;

    private CanonicalAbstractExecutedSupplyShadow(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedPhysicalCycleProvenance evidence,
        uint commodity, ulong retainedRaw, ulong rawUnitsPerEconomicUnit,
        AbstractPurchasingPowerIndex candidatePurchasingPower,
        PopulationPriceFunction experimentalPriceCurve)
    {
        _session = session;
        _evidence = evidence;
        Commodity = commodity;
        CandidateRetentionRaw = retainedRaw;
        RawUnitsPerEconomicUnit = rawUnitsPerEconomicUnit;
        _evidence.VerifyIndependent();
        _evidence.RequireCurrent(session);

        _sources = evidence.Production
            .Where(x => x.Commodity == commodity)
            .Select(x => new CanonicalAbstractSupplySource(
                x.SourceFineTile, x.Commodity, x.Channel,
                x.Activity, x.JourneyOrderId, x.ProducedRaw))
            .OrderBy(x => x.SourceFineTile)
            .ThenBy(x => x.Channel).ThenBy(x => x.Activity)
            .ThenBy(x => x.JourneyOrderId)
            .ToArray();
        ExecutedProductionSources = Array.AsReadOnly(_sources);
        ActuallyProducedRaw = _sources.Aggregate(
            BigInteger.Zero, (sum, row) => sum + row.ProducedRaw);

        // A zero-output commodity must already belong to the real cycle's
        // explicit physical stock universe; no imaginary extra product ID.
        if (_sources.Length == 0 &&
            !evidence.LocalStock.Any(x => x.Commodity == commodity))
            throw new ArgumentException(
                "No physical evidence of this commodity in the executed cycle.",
                nameof(commodity));

        if (new BigInteger(retainedRaw) > ActuallyProducedRaw)
            throw new ArgumentOutOfRangeException(nameof(retainedRaw),
                "Hypothetical retention cannot exceed genuinely produced output.");

        // Decimal bounds are deliberately explicit; never round or clamp an
        // overlarge real flow into a fictional offer.
        var available = checked((decimal)ActuallyProducedRaw) /
            rawUnitsPerEconomicUnit;
        var retained = (decimal)retainedRaw / rawUnitsPerEconomicUnit;
        var candidateOffer = PhysicalOffer.FromAvailable(available, retained);

        CandidateQuote = PopulationPriceKernel.Quote(
            evidence.TotalRealPopulation, candidateOffer,
            candidatePurchasingPower, experimentalPriceCurve);

        // A caller-supplied experimental price function must not silently
        // mutate the actual canonical snapshot while computing a quote.
        _evidence.RequireCurrent(session);
        VerifyIndependent();
    }

    public static CanonicalAbstractExecutedSupplyShadow FromLatestRealCycle(
        CanonicalPhysicalTacticalSession session,
        uint commodity, ulong explicitlyRetainedOfProductionRaw,
        ulong rawUnitsPerEconomicUnit,
        AbstractPurchasingPowerIndex candidatePurchasingPower,
        PopulationPriceFunction experimentalPriceCurve)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(experimentalPriceCurve);
        if (commodity == 0U)
            throw new ArgumentOutOfRangeException(nameof(commodity));
        if (rawUnitsPerEconomicUnit == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawUnitsPerEconomicUnit));

        var evidence = session.CaptureLatestExecutedPhysicalCycleProvenance();
        return new(session, evidence, commodity, explicitlyRetainedOfProductionRaw,
            rawUnitsPerEconomicUnit, candidatePurchasingPower,
            experimentalPriceCurve);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "The real physical production receipt belongs to another session.");
        _evidence.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _evidence.VerifyIndependent();
        var sources = _evidence.Production
            .Where(x => x.Commodity == Commodity)
            .Select(x => new CanonicalAbstractSupplySource(
                x.SourceFineTile, x.Commodity, x.Channel,
                x.Activity, x.JourneyOrderId, x.ProducedRaw))
            .OrderBy(x => x.SourceFineTile).ThenBy(x => x.Channel).ThenBy(x => x.Activity)
            .ThenBy(x => x.JourneyOrderId).ToArray();

        if (!_sources.SequenceEqual(sources) ||
            _sources.Aggregate(BigInteger.Zero, (sum, x) =>
                sum + x.ProducedRaw) != ActuallyProducedRaw ||
            CandidateOfferedRaw < 0 ||
            ((decimal)ActuallyProducedRaw / RawUnitsPerEconomicUnit -
                (decimal)CandidateRetentionRaw / RawUnitsPerEconomicUnit) !=
                CandidateQuote.Offered ||
            CandidateQuote.Population != RealPopulation ||
            CandidateQuote.EffectiveDemand !=
                checked(RealPopulation * CandidateQuote.RelativePurchasingPower) ||
            CandidateQuote.HasOffer != (CandidateOfferedRaw > 0))
            throw new InvalidOperationException(
                "Abstract production-side shadow quote lost its physical identity or arithmetic.");
        RequireCurrent(_session);
    }
}
