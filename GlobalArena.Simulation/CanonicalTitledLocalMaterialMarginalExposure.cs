using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// Current snapshot of already journaled local wood/stone transfers of
/// SIMULATED economic title. Each title is counted once; reversed titles
/// are excluded from price exposure, while historical return is reported.
/// Values are exact, dimensionless marginal-price/revenue INDEXES, not
/// monetary proceeds or proof of a commercial sale.
/// </summary>
public sealed record CanonicalTitledLocalMaterialMarginalRow(
    ulong FineTile,
    uint Commodity,
    ulong RealResidents,
    BigInteger HistoricallyRegisteredTitleRaw,
    BigInteger ReversedTitleRaw,
    BigInteger CurrentlyTitledRaw,
    BigInteger StillInRecipientCustodyRaw,
    BigInteger PhysicallyUsedByRecipientRaw,
    BigInteger NeutralDemandPressureRaw,
    CanonicalEndogenousPriceIndex GentleAveragePriceIndex,
    CanonicalEndogenousPriceIndex GentleLastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex GentleHypotheticalGrossValueIndex,
    CanonicalEndogenousPriceIndex SteepAveragePriceIndex,
    CanonicalEndogenousPriceIndex SteepLastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex SteepHypotheticalGrossValueIndex)
{
    public bool ReportsActualMonetaryRevenue => false;
    public bool ProvesExecutedCommercialSale => false;
}

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01P: a reproducible local-only valuation
/// OBSERVATION of real delivered and *currently* titled material lots.
/// The source is the physical journal 01J/01K and title/reversal journal
/// 01N/01O; the price curve is the existing VS78 marginal function.
/// Neutral N=1 is UNCALIBRATED and never grants money, an artificial
/// household budget, title or settlement rights.
/// </summary>
public sealed class CanonicalTitledLocalMaterialMarginalExposure
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly ulong _scale;
    private readonly string _hash;
    private readonly int _eventCount;
    private readonly CanonicalTitledLocalMaterialMarginalRow[] _rows;

    public IReadOnlyList<CanonicalTitledLocalMaterialMarginalRow> Markets { get; }
    public ulong RawUnitsPerEconomicUnit => _scale;
    public BigInteger TotalHistoricallyRegisteredTitleRaw { get; }
    public BigInteger TotalReversedTitleRaw { get; }
    public BigInteger TotalCurrentlyTitledRaw { get; }
    public BigInteger TotalStillInRecipientCustodyRaw { get; }
    public BigInteger TotalPhysicallyUsedByRecipientRaw { get; }
    public CanonicalEndogenousPriceIndex TotalGentleHypotheticalGrossValueIndex { get; }
    public CanonicalEndogenousPriceIndex TotalSteepHypotheticalGrossValueIndex { get; }

    public bool UsesOnlyActualPhysicallyDeliveredJournaledLots => true;
    public bool UsesOnlyExecutedInGameEconomicTitleRegistrations => true;
    public bool IgnoresUnregisteredAndReversedTitleAsCurrentValuation => true;
    public bool UsesRealResidentAllocationsForNeutralDemandPressure => true;
    public bool IsPurchasingPowerCalibrationRatified => false;
    public bool CanCapPhysicalAbsorptionUsingBuyerDemand => false;
    public bool InventsBuyerBalancesOrLoans => false;
    public bool ProvesAnExecutedCommercialSale => false;
    public bool PostsProducerRevenueOrStateTaxes => false;
    public bool ChangesWorldSaveOrPhysicalLots => false;
    public bool ExecutesUnprovenRemoteTransport => false;

    private CanonicalTitledLocalMaterialMarginalExposure(
        CanonicalPhysicalTacticalSession session, ulong scale)
    {
        _session = session;
        _scale = scale;
        _hash = session.CurrentHash.HexDigest;
        _eventCount = session.Events.Count;
        _rows = Rebuild(session, scale);
        Markets = Array.AsReadOnly(_rows);
        TotalHistoricallyRegisteredTitleRaw = Sum(x =>
            x.HistoricallyRegisteredTitleRaw);
        TotalReversedTitleRaw = Sum(x => x.ReversedTitleRaw);
        TotalCurrentlyTitledRaw = Sum(x => x.CurrentlyTitledRaw);
        TotalStillInRecipientCustodyRaw = Sum(x =>
            x.StillInRecipientCustodyRaw);
        TotalPhysicallyUsedByRecipientRaw = Sum(x =>
            x.PhysicallyUsedByRecipientRaw);
        TotalGentleHypotheticalGrossValueIndex = SumIndex(x =>
            x.GentleHypotheticalGrossValueIndex);
        TotalSteepHypotheticalGrossValueIndex = SumIndex(x =>
            x.SteepHypotheticalGrossValueIndex);
        VerifyIndependent();
    }

    private BigInteger Sum(
        Func<CanonicalTitledLocalMaterialMarginalRow, BigInteger> projection) =>
        _rows.Aggregate(BigInteger.Zero, (sum, row) => sum + projection(row));

    private CanonicalEndogenousPriceIndex SumIndex(
        Func<CanonicalTitledLocalMaterialMarginalRow,
            CanonicalEndogenousPriceIndex> projection) =>
        _rows.Aggregate(CanonicalEndogenousPriceIndex.Zero,
            (sum, row) => CanonicalEndogenousPriceIndex.Add(
                sum, projection(row)));

    public static CanonicalTitledLocalMaterialMarginalExposure FromCurrent(
        CanonicalPhysicalTacticalSession session,
        ulong rawUnitsPerEconomicUnit)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (rawUnitsPerEconomicUnit == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(rawUnitsPerEconomicUnit),
                "Demand pressure requires an explicit positive raw-unit scale.");
        return new(session, rawUnitsPerEconomicUnit);
    }

    private static CanonicalTitledLocalMaterialMarginalRow[] Rebuild(
        CanonicalPhysicalTacticalSession session, ulong scale)
    {
        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(session);
        coverage.VerifyIndependent();
        var dispositionByCommit = coverage.Dispositions.ToDictionary(x =>
            x.CommitmentActionOrdinal);
        var positions = session.LocalSimulatedTitlePositions;
        if (positions.Select(x => x.Registration.Evidence.SourceCommitmentActionOrdinal)
                .Distinct().Count() != positions.Count ||
            positions.Count != session.RegisteredLocalSimulatedEconomicTitles.Count)
            throw new InvalidOperationException(
                "Repeated or omitted titles cannot enter marginal pricing.");
        var rows = new List<CanonicalTitledLocalMaterialMarginalRow>();
        foreach (var byMarket in positions.GroupBy(x =>
            (x.Registration.Evidence.FineTile,
                x.Registration.Evidence.Commodity))
            .OrderBy(x => x.Key.FineTile).ThenBy(x => x.Key.Commodity))
        {
            var key = byMarket.Key;
            if (key.FineTile == 0UL || key.Commodity is not (2U or 3U))
                throw new InvalidOperationException(
                    "Only evidenced local wood/stone can enter titled marginal reference.");

            ulong residents = 0UL;
            foreach (var allocation in session.State.Allocations.Where(x =>
                x.TileId.FineStrategicCellId.Value == key.FineTile))
                residents = checked(residents + allocation.Workers);

            BigInteger historical = BigInteger.Zero;
            BigInteger reversed = BigInteger.Zero;
            BigInteger active = BigInteger.Zero;
            BigInteger held = BigInteger.Zero;
            BigInteger used = BigInteger.Zero;
            foreach (var position in byMarket)
            {
                var title = position.Registration;
                var amount = new BigInteger(title.Evidence.RawQuantity);
                if (amount <= 0 ||
                    !dispositionByCommit.TryGetValue(
                        title.Evidence.SourceCommitmentActionOrdinal,
                        out var disposition) ||
                    disposition.QuantityRaw != title.Evidence.RawQuantity ||
                    disposition.FineTile != key.FineTile ||
                    disposition.Commodity != key.Commodity ||
                    disposition.PhysicalTransferActionOrdinal !=
                        title.Evidence.PhysicalTransferActionOrdinal)
                    throw new InvalidOperationException(
                        "A titled market cannot quote undocumented material deliveries.");
                historical += amount;
                if (position.IsReversed)
                {
                    if (disposition.Disposition is not (
                        CanonicalMarketCustodyDisposition.TransferredToRealLocalRecipient or
                        CanonicalMarketCustodyDisposition.ReturnedFromLocalRecipientCustody))
                        throw new InvalidOperationException(
                            "A reverted title cannot have been consumed or sold by the former holder.");
                    reversed += amount;
                    continue;
                }
                active += amount;
                switch (disposition.Disposition)
                {
                    case CanonicalMarketCustodyDisposition.TransferredToRealLocalRecipient:
                        if (!position.IsInRecipientCustody)
                            throw new InvalidOperationException(
                                "Physical custody status diverged from the titled lot.");
                        held += amount;
                        break;
                    case CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse:
                        if (position.IsInRecipientCustody ||
                            position.PhysicalDisposition !=
                                CanonicalLocalRecipientCustodyDispositionKind.ExecuteWorkforceMaterialUse)
                            throw new InvalidOperationException(
                                "Only actual material use is a physical sink.");
                        used += amount;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Nonreversed title cannot imply unproven return or consumption.");
                }
            }

            if (historical != reversed + active || active != held + used)
                throw new InvalidOperationException(
                    "Titled market quantity is not conserved.");
            if (active > BigInteger.Zero && residents == 0UL)
                throw new InvalidOperationException(
                    "A live titled local market cannot be priced without actual residents.");

            // The demand signal is NOT a resident cash budget, consumption
            // cap or nominal price. N=1 remains explicitly uncalibrated.
            var pressure = (BigInteger)residents * scale;
            var gentle = CanonicalEndogenousLocalPriceComparison.Calculate(
                active, pressure, 1);
            var steep = CanonicalEndogenousLocalPriceComparison.Calculate(
                active, pressure, 2);
            rows.Add(new CanonicalTitledLocalMaterialMarginalRow(
                key.FineTile, key.Commodity, residents,
                historical, reversed, active, held, used, pressure,
                gentle.Average, gentle.Last, gentle.Revenue,
                steep.Average, steep.Last, steep.Revenue));
        }
        return rows.ToArray();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session) ||
            _session.CurrentHash.HexDigest != _hash ||
            _session.Events.Count != _eventCount)
            throw new InvalidOperationException(
                "Marginal title reference is stale or belongs to a different game.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Rebuild(_session, _scale);
        if (!_rows.SequenceEqual(rebuilt) ||
            TotalHistoricallyRegisteredTitleRaw != rebuilt.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.HistoricallyRegisteredTitleRaw) ||
            TotalReversedTitleRaw != rebuilt.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.ReversedTitleRaw) ||
            TotalCurrentlyTitledRaw != rebuilt.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.CurrentlyTitledRaw) ||
            TotalStillInRecipientCustodyRaw != rebuilt.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.StillInRecipientCustodyRaw) ||
            TotalPhysicallyUsedByRecipientRaw != rebuilt.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.PhysicallyUsedByRecipientRaw) ||
            TotalGentleHypotheticalGrossValueIndex.CompareTo(
                rebuilt.Aggregate(CanonicalEndogenousPriceIndex.Zero,
                    (sum, x) => CanonicalEndogenousPriceIndex.Add(
                        sum, x.GentleHypotheticalGrossValueIndex))) != 0 ||
            TotalSteepHypotheticalGrossValueIndex.CompareTo(
                rebuilt.Aggregate(CanonicalEndogenousPriceIndex.Zero,
                    (sum, x) => CanonicalEndogenousPriceIndex.Add(
                        sum, x.SteepHypotheticalGrossValueIndex))) != 0)
            throw new InvalidOperationException(
                "The relative value curve diverged from live registered title lots.");
        RequireCurrent(_session);
    }
}
