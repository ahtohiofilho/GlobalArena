using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// Dimensionless monetary-free valuation of ACTUAL journal-backed market
/// custody. The gross-revenue quantities are relative INDEXES, not settled
/// payments, unit prices in currency, taxes, wages, or real buyer budgets.
/// </summary>
public sealed record CanonicalCommittedOfferMarginalReferenceRow(
    ulong FineTile, uint Commodity, ulong RealResidents,
    BigInteger LiveMarketOfferRaw, BigInteger UncommittedLocalRaw,
    BigInteger DeliberatelyRetainedRaw, BigInteger NeutralDemandPressureRaw,
    CanonicalEndogenousPriceIndex GentleAveragePriceIndex,
    CanonicalEndogenousPriceIndex GentleLastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex GentleRevenueIndex,
    CanonicalEndogenousPriceIndex SteepAveragePriceIndex,
    CanonicalEndogenousPriceIndex SteepLastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex SteepRevenueIndex);

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01G: certified actual *offer custody* feeding the
/// previously implemented dyadic marginal value function (VS78).
///
/// No supply is assumed sold, transferred or consumed. Population is real;
/// N=1 is an explicit uncalibrated neutral price-reference baseline, not a
/// buyer wallet or an authorization of nominal cash. Effective pressure is
/// expressed in the caller's explicit commodity raw-unit scale.
/// </summary>
public sealed class CanonicalCommittedOfferMarginalReference
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly ulong _rawUnitsPerEconomicUnit;
    private readonly string _worldHash;
    private readonly int _events;
    private readonly CanonicalCommittedOfferMarginalReferenceRow[] _rows;

    public IReadOnlyList<CanonicalCommittedOfferMarginalReferenceRow> Markets { get; }
    public ulong RawUnitsPerEconomicUnit => _rawUnitsPerEconomicUnit;
    public BigInteger TotalActuallyCommittedOfferRaw { get; }
    public CanonicalEndogenousPriceIndex GentleTotalRevenueIndex { get; }
    public CanonicalEndogenousPriceIndex SteepTotalRevenueIndex { get; }

    public bool UsesRealJournaledMarketOfferCustody => true;
    public bool TreatsUncommittedStockAsExecutedOffer => false;
    public bool TreatsDemandPressureAsPhysicalSaleCap => false;
    public bool UsesNeutralUncalibratedPurchasingPower => true;
    public bool HasAuthenticatedPurchasingPowerN => false;
    public bool HasCalibratedNominalUnitPrices => false;
    public bool ClaimsAllOffersHavePhysicalDestinations => false;
    public bool ProvesExecutedSaleOrPayment => false;
    public bool AuthorizesRevenueOrTaxPosting => false;
    public bool MutatesCanonicalWorldOrSave => false;

    private CanonicalCommittedOfferMarginalReference(
        CanonicalPhysicalTacticalSession session, ulong rawUnitScale)
    {
        _session = session;
        _rawUnitsPerEconomicUnit = rawUnitScale;
        _worldHash = session.CurrentHash.HexDigest;
        _events = session.Events.Count;
        _rows = Rebuild(session, rawUnitScale);
        Markets = Array.AsReadOnly(_rows);
        TotalActuallyCommittedOfferRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.LiveMarketOfferRaw);
        GentleTotalRevenueIndex = _rows.Aggregate(
            CanonicalEndogenousPriceIndex.Zero,
            (sum, x) => CanonicalEndogenousPriceIndex.Add(
                sum, x.GentleRevenueIndex));
        SteepTotalRevenueIndex = _rows.Aggregate(
            CanonicalEndogenousPriceIndex.Zero,
            (sum, x) => CanonicalEndogenousPriceIndex.Add(
                sum, x.SteepRevenueIndex));
        VerifyIndependent();
    }

    public static CanonicalCommittedOfferMarginalReference FromCurrent(
        CanonicalPhysicalTacticalSession session,
        ulong rawUnitsPerEconomicUnit)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (rawUnitsPerEconomicUnit == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(rawUnitsPerEconomicUnit),
                "The price pressure must use an explicit nonzero raw-unit scale.");
        return new(session, rawUnitsPerEconomicUnit);
    }

    private static CanonicalCommittedOfferMarginalReferenceRow[] Rebuild(
        CanonicalPhysicalTacticalSession session, ulong scale)
    {
        var pool = CanonicalDefaultMarketPoolPreflight.FromCurrent(session);
        var rows = new List<CanonicalCommittedOfferMarginalReferenceRow>();
        foreach (var physical in pool.Rows.Where(x =>
            x.AlreadyCommittedOfferRaw > BigInteger.Zero))
        {
            if (physical.Commodity is not (1U or 2U or 3U))
                throw new InvalidOperationException(
                    "Unknown physical market offer cannot be priced.");

            // Use the real allocation ledger, not a synthetic buyer list or
            // the legacy cash account / historical food-demand quantity.
            ulong residents = 0;
            foreach (var worker in session.State.Allocations.Where(x =>
                x.TileId.FineStrategicCellId.Value == physical.FineTile))
                residents = checked(residents + worker.Workers);
            if (residents == 0UL)
                throw new InvalidOperationException(
                    "A price reference requires real residents at the receiving market.");

            // Neutral N=1 only. This is P*scale as a PRICE PRESSURE signal,
            // never a maximal physical intake or buyer-wallet allowance.
            var pressure = (BigInteger)residents * scale;
            var gentle = CanonicalEndogenousLocalPriceComparison.Calculate(
                physical.AlreadyCommittedOfferRaw, pressure, 1);
            var steep = CanonicalEndogenousLocalPriceComparison.Calculate(
                physical.AlreadyCommittedOfferRaw, pressure, 2);
            if (gentle.Revenue.CompareTo(
                    CanonicalEndogenousPriceIndex.Zero) <= 0 ||
                steep.Revenue.CompareTo(
                    CanonicalEndogenousPriceIndex.Zero) <= 0)
                throw new InvalidOperationException(
                    "A positive real offer at an inhabited market needs a positive reference index.");

            rows.Add(new CanonicalCommittedOfferMarginalReferenceRow(
                physical.FineTile, physical.Commodity, residents,
                physical.AlreadyCommittedOfferRaw, physical.UncommittedLocalRaw,
                physical.DeliberatelyRetainedRaw, pressure,
                gentle.Average, gentle.Last, gentle.Revenue,
                steep.Average, steep.Last, steep.Revenue));
        }
        return rows.OrderBy(x => x.FineTile)
            .ThenBy(x => x.Commodity).ToArray();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session) ||
            session.CurrentHash.HexDigest != _worldHash ||
            session.Events.Count != _events)
            throw new InvalidOperationException(
                "Market price reference belongs to another or stale game session.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var recalculated = Rebuild(_session, _rawUnitsPerEconomicUnit);
        if (!_rows.SequenceEqual(recalculated) ||
            TotalActuallyCommittedOfferRaw != recalculated.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.LiveMarketOfferRaw) ||
            GentleTotalRevenueIndex.CompareTo(recalculated.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.GentleRevenueIndex))) != 0 ||
            SteepTotalRevenueIndex.CompareTo(recalculated.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.SteepRevenueIndex))) != 0)
            throw new InvalidOperationException(
                "Physical offers and marginal reference indices diverge from their journal.");
        RequireCurrent(_session);
    }
}
