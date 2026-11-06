using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Authenticated disposition of a single historical physical-custody lock.
/// A released offer returns to free stock; a physical material use is a real
/// sink but NEVER evidence of an executed monetary sale.
/// </summary>
public enum CanonicalMarketCustodyDisposition : byte
{
    PendingOffer = 1,
    ReturnedToFreeStock = 2,
    ExecutedLocalMaterialUse = 3,
    ExecutedLocalWorkforceMaterialUse = 4,
    ExplicitRetention = 5,
    ReleasedRetention = 6,
    TransferredToRealLocalRecipient = 7,
    ReturnedFromLocalRecipientCustody = 8,
    ExecutedRecipientWorkforceMaterialUse = 9
}

public sealed record CanonicalMarketCustodyDispositionReceipt(
    int CommitmentActionOrdinal, ulong FineTile, uint Commodity,
    ulong QuantityRaw, CanonicalMarketCustodyDisposition Disposition,
    int? PhysicalUseActionOrdinal, EconomicActivityKind? BeneficiaryActivity,
    ulong BeneficiaryWorkers,
    int? PhysicalTransferActionOrdinal = null,
    int? RecipientDispositionActionOrdinal = null)
{
    public bool ProvesExecutedCommercialSale => false;
    public bool ProvesRealPhysicalMaterialSink =>
        Disposition is CanonicalMarketCustodyDisposition.ExecutedLocalMaterialUse or
            CanonicalMarketCustodyDisposition.ExecutedLocalWorkforceMaterialUse or
            CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse;
}

public sealed record CanonicalMarketPhysicalCoverageRow(
    ulong FineTile, uint Commodity,
    BigInteger HistoricallyOfferedRaw,
    BigInteger PendingOfferRaw,
    BigInteger ReleasedToFreeRaw,
    BigInteger ExecutedLocalMaterialRaw,
    BigInteger OfWhichRealWorkforceRaw,
    BigInteger UncommittedFreeRaw,
    BigInteger ExplicitRetentionRaw,
    BigInteger TransferredToRecipientRaw = default,
    BigInteger ReturnedFromRecipientRaw = default,
    BigInteger ExecutedRecipientMaterialRaw = default)
{
    /// <summary>
    /// Goods awaiting any authenticated economic destination, including
    /// freely available (but not yet committed) units. The same released lot
    /// cannot be added again here, because it is included in the free stock.
    /// </summary>
    public BigInteger EligibleAwaitingDestinationRaw =>
        PendingOfferRaw + UncommittedFreeRaw;

    public bool ProvesExecutedSale => false;
}

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01H: independently recomputed, journal-backed
/// conservation between offer creation, live custody, released custody and
/// executed physical material use. Neither a closing-quantity report nor a
/// real-workgroup sink is an ownership transfer or settlement receipt.
///
/// Historical totals and currently live balances are intentionally separate:
/// comparing a historical release with current free stock would double count
/// goods which were reoffered later.
/// </summary>
public sealed class CanonicalMarketExecutedPhysicalCoverage
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly string _hash;
    private readonly int _eventCount;
    private readonly CanonicalMarketCustodyDispositionReceipt[] _receipts;
    private readonly CanonicalMarketPhysicalCoverageRow[] _rows;

    public IReadOnlyList<CanonicalMarketCustodyDispositionReceipt> Dispositions { get; }
    public IReadOnlyList<CanonicalMarketPhysicalCoverageRow> Markets { get; }
    public BigInteger TotalHistoricallyOfferedRaw { get; }
    public BigInteger TotalPendingOfferRaw { get; }
    public BigInteger TotalExecutedPhysicalMaterialRaw { get; }
    public BigInteger TotalRealWorkforceMaterialRaw { get; }
    public BigInteger TotalTransferredToRecipientRaw { get; }
    public BigInteger TotalReturnedFromRecipientRaw { get; }
    public BigInteger TotalExecutedRecipientMaterialRaw { get; }
    public BigInteger TotalEligibleAwaitingDestinationRaw { get; }
    public BigInteger TotalDeliberatelyRetainedRaw { get; }

    public bool AuthenticatesRealOfferAndMaterialUseJournals => true;
    public bool AuthenticatesRemoteMarketDeliveries => false;
    public bool AuthenticatesResidentFoodIngestion => false;
    public bool ProvesCompletedMarketSales => false;
    public bool CanAuthorizeMonetaryRevenueOrTax => false;
    public bool MutatesCanonicalStateOrSave => false;

    private CanonicalMarketExecutedPhysicalCoverage(
        CanonicalPhysicalTacticalSession session)
    {
        _session = session;
        _hash = session.CurrentHash.HexDigest;
        _eventCount = session.Events.Count;
        (_receipts, _rows) = Rebuild(session);
        Dispositions = Array.AsReadOnly(_receipts);
        Markets = Array.AsReadOnly(_rows);
        TotalHistoricallyOfferedRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.HistoricallyOfferedRaw);
        TotalPendingOfferRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.PendingOfferRaw);
        TotalExecutedPhysicalMaterialRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ExecutedLocalMaterialRaw);
        TotalRealWorkforceMaterialRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.OfWhichRealWorkforceRaw);
        TotalTransferredToRecipientRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.TransferredToRecipientRaw);
        TotalReturnedFromRecipientRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ReturnedFromRecipientRaw);
        TotalExecutedRecipientMaterialRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ExecutedRecipientMaterialRaw);
        TotalEligibleAwaitingDestinationRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.EligibleAwaitingDestinationRaw);
        TotalDeliberatelyRetainedRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ExplicitRetentionRaw);
        VerifyIndependent();
    }

    public static CanonicalMarketExecutedPhysicalCoverage FromCurrent(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new(session);
    }

    private static (CanonicalMarketCustodyDispositionReceipt[],
        CanonicalMarketPhysicalCoverageRow[]) Rebuild(
            CanonicalPhysicalTacticalSession session)
    {
        var witness = session.CapturePhysicalStockCommitmentWitness();
        witness.VerifyIndependent();
        var pool = CanonicalDefaultMarketPoolPreflight.FromCurrent(session);

        var uses = session.ExecutedCommittedMaterialUses;
        var useByCommit = uses.ToDictionary(x =>
            x.SourceCommitmentActionOrdinal);
        var transfers = session.ExecutedLocalRecipientCustodyTransfers;
        var transferByCommit = transfers.ToDictionary(x =>
            x.SourceCommitmentActionOrdinal);
        var recipientDispositions = session.ExecutedLocalRecipientCustodyDispositions;
        var recipientDispositionsByCommit = recipientDispositions.ToDictionary(x =>
            x.SourceCommitmentActionOrdinal);
        if (uses.Select(x => x.ActionOrdinal).Distinct().Count() != uses.Count ||
            useByCommit.Count != uses.Count)
            throw new InvalidOperationException(
                "A material-use action or physical offer cannot be counted twice.");

        var receiptRows = new List<CanonicalMarketCustodyDispositionReceipt>();
        var seenUses = new HashSet<int>();
        var seenTransfers = new HashSet<int>();
        var seenDispositions = new HashSet<int>();
        foreach (var offer in witness.Commitments.OrderBy(x => x.ActionOrdinal))
        {
            if ((offer.Released ? 1 : 0) + (offer.Consumed ? 1 : 0) +
                (offer.Transferred ? 1 : 0) > 1)
                throw new InvalidOperationException(
                    "A single custody lock cannot be both used and released.");
            useByCommit.TryGetValue(offer.ActionOrdinal, out var used);
            transferByCommit.TryGetValue(offer.ActionOrdinal, out var transferred);
            recipientDispositionsByCommit.TryGetValue(offer.ActionOrdinal, out var closed);
            if (offer.Consumed != (used is not null) ||
                (offer.Consumed &&
                    offer.Kind != CanonicalPhysicalCommitmentKind.MarketOffer) ||
                offer.Transferred != (transferred is not null))
                throw new InvalidOperationException(
                    "An executed sink must match one consumed market offer.");

            if (used is not null)
            {
                if (!seenUses.Add(used.ActionOrdinal) ||
                    used.SourceCommitmentActionOrdinal != offer.ActionOrdinal ||
                    used.UsedRaw != offer.RawQuantity ||
                    used.Commodity != offer.Commodity ||
                    used.FineTile != offer.FineTile ||
                    used.SourceAttributions.Aggregate(
                        BigInteger.Zero, (sum, x) => sum + x.RawQuantity) !=
                        offer.RawQuantity ||
                    used.ProvesMonetarySale || used.HasExecutedTransport ||
                    used.ProvesDeliveryToFinalEconomicBuyer)
                    throw new InvalidOperationException(
                        "Market physical sink is not backed by unique real material provenance.");
            }

            if (transferred is not null &&
                (!seenTransfers.Add(transferred.ActionOrdinal) ||
                 transferred.SourceCommitmentActionOrdinal != offer.ActionOrdinal ||
                 transferred.RawQuantity != offer.RawQuantity ||
                 transferred.Commodity != offer.Commodity ||
                 transferred.FineTile != offer.FineTile ||
                 !transferred.ProvesPhysicalCustodyHandoff ||
                 transferred.ProvesExecutedCommercialSale ||
                 transferred.AuthorizesPriceOrRevenuePosting))
                throw new InvalidOperationException(
                    "Actual local recipient custody must match a unique source commitment.");

            if (closed is not null &&
                (!offer.Transferred || transferred is null ||
                 !seenDispositions.Add(closed.ActionOrdinal) ||
                 closed.OriginalTransferActionOrdinal != transferred.ActionOrdinal ||
                 closed.SourceCommitmentActionOrdinal != offer.ActionOrdinal ||
                 closed.RawQuantity != offer.RawQuantity ||
                 closed.FineTile != offer.FineTile ||
                 closed.Commodity != offer.Commodity ||
                 closed.ProvesCommercialSale ||
                 closed.AuthorizesMonetaryRevenueOrTax))
                throw new InvalidOperationException(
                    "Recipient custody disposition is not bound to authentic transferred stock.");

            var status = offer.Kind switch
            {
                CanonicalPhysicalCommitmentKind.DeliberateRetention when offer.Released =>
                    CanonicalMarketCustodyDisposition.ReleasedRetention,
                CanonicalPhysicalCommitmentKind.DeliberateRetention =>
                    CanonicalMarketCustodyDisposition.ExplicitRetention,
                CanonicalPhysicalCommitmentKind.MarketOffer when offer.Transferred &&
                    closed?.ProvesReturnOfSamePhysicalCustody == true =>
                    CanonicalMarketCustodyDisposition.ReturnedFromLocalRecipientCustody,
                CanonicalPhysicalCommitmentKind.MarketOffer when offer.Transferred &&
                    closed?.ProvesExecutedMaterialUse == true =>
                    CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse,
                CanonicalPhysicalCommitmentKind.MarketOffer when offer.Transferred =>
                    CanonicalMarketCustodyDisposition.TransferredToRealLocalRecipient,
                CanonicalPhysicalCommitmentKind.MarketOffer when offer.Released =>
                    CanonicalMarketCustodyDisposition.ReturnedToFreeStock,
                CanonicalPhysicalCommitmentKind.MarketOffer when used is not null &&
                    used.ProvesLocalWorkforceGroup =>
                    CanonicalMarketCustodyDisposition.ExecutedLocalWorkforceMaterialUse,
                CanonicalPhysicalCommitmentKind.MarketOffer when used is not null =>
                    CanonicalMarketCustodyDisposition.ExecutedLocalMaterialUse,
                CanonicalPhysicalCommitmentKind.MarketOffer =>
                    CanonicalMarketCustodyDisposition.PendingOffer,
                _ => throw new InvalidOperationException(
                    "Unknown physical custody disposition.")
            };

            receiptRows.Add(new CanonicalMarketCustodyDispositionReceipt(
                offer.ActionOrdinal, offer.FineTile, offer.Commodity,
                offer.RawQuantity, status,
                used?.ActionOrdinal ?? (closed?.ProvesExecutedMaterialUse == true
                    ? closed.ActionOrdinal : null),
                used?.BeneficiaryActivity ?? transferred?.RecipientActivity,
                used?.BeneficiaryWorkers ?? closed?.ActiveBeneficiaryWorkers ??
                    transferred?.RecipientWorkers ?? 0UL,
                transferred?.ActionOrdinal, closed?.ActionOrdinal));
        }
        if (seenUses.Count != uses.Count ||
            seenTransfers.Count != transfers.Count ||
            seenDispositions.Count != recipientDispositions.Count)
            throw new InvalidOperationException(
                "Orphan executed physical material use without a commitment.");

        var receipts = receiptRows.ToArray();
        var keys = pool.Rows.Select(x => (x.FineTile, x.Commodity))
            .Concat(receipts.Select(x => (x.FineTile, x.Commodity)))
            .Distinct()
            .OrderBy(x => x.FineTile)
            .ThenBy(x => x.Commodity);
        var rows = new List<CanonicalMarketPhysicalCoverageRow>();
        foreach (var key in keys)
        {
            var recorded = receipts.Where(x =>
                x.FineTile == key.FineTile && x.Commodity == key.Commodity)
                .ToArray();
            BigInteger Sum(params CanonicalMarketCustodyDisposition[] statuses) =>
                recorded.Where(x => statuses.Contains(x.Disposition))
                    .Aggregate(BigInteger.Zero, (sum, x) => sum + x.QuantityRaw);

            var gross = Sum(
                CanonicalMarketCustodyDisposition.PendingOffer,
                CanonicalMarketCustodyDisposition.ReturnedToFreeStock,
                CanonicalMarketCustodyDisposition.ExecutedLocalMaterialUse,
                CanonicalMarketCustodyDisposition.ExecutedLocalWorkforceMaterialUse,
                CanonicalMarketCustodyDisposition.TransferredToRealLocalRecipient,
                CanonicalMarketCustodyDisposition.ReturnedFromLocalRecipientCustody,
                CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse);
            var pending = Sum(CanonicalMarketCustodyDisposition.PendingOffer);
            var releasedFromRecipient = Sum(
                CanonicalMarketCustodyDisposition.ReturnedFromLocalRecipientCustody);
            var released = Sum(
                CanonicalMarketCustodyDisposition.ReturnedToFreeStock,
                CanonicalMarketCustodyDisposition.ReturnedFromLocalRecipientCustody);
            var executedAtRecipient = Sum(
                CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse);
            var transferred = Sum(
                CanonicalMarketCustodyDisposition.TransferredToRealLocalRecipient);
            var used = Sum(
                CanonicalMarketCustodyDisposition.ExecutedLocalMaterialUse,
                CanonicalMarketCustodyDisposition.ExecutedLocalWorkforceMaterialUse,
                CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse);
            var workforce = Sum(
                CanonicalMarketCustodyDisposition.ExecutedLocalWorkforceMaterialUse,
                CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse);
            var retention = Sum(CanonicalMarketCustodyDisposition.ExplicitRetention);
            var stock = pool.ForTileCommodity(key.FineTile, key.Commodity);
            if (gross != pending + released + used + transferred ||
                pending != stock.AlreadyCommittedOfferRaw ||
                retention != stock.DeliberatelyRetainedRaw ||
                workforce > used)
                throw new InvalidOperationException(
                    "Market offer physical disposition does not reconcile with live stocks.");

            rows.Add(new CanonicalMarketPhysicalCoverageRow(
                key.FineTile, key.Commodity, gross, pending, released,
                used, workforce, stock.UncommittedLocalRaw, retention,
                transferred, releasedFromRecipient, executedAtRecipient));
        }

        if (rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.EligibleAwaitingDestinationRaw) !=
            pool.EligibleByDefaultRaw ||
            rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ExplicitRetentionRaw) !=
            pool.DeliberatelyRetainedRaw ||
            rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.PendingOfferRaw) !=
            witness.LiveMarketOffersRaw)
            throw new InvalidOperationException(
                "Historical receipts cannot manufacture or erase the current market pool.");

        return (receipts, rows.ToArray());
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash.HexDigest != _hash ||
            session.Events.Count != _eventCount)
            throw new InvalidOperationException(
                "Physical market coverage is foreign or stale.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var (receipts, rows) = Rebuild(_session);
        if (!_receipts.SequenceEqual(receipts) ||
            !_rows.SequenceEqual(rows) ||
            TotalHistoricallyOfferedRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.HistoricallyOfferedRaw) ||
            TotalPendingOfferRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.PendingOfferRaw) ||
            TotalExecutedPhysicalMaterialRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ExecutedLocalMaterialRaw) ||
            TotalRealWorkforceMaterialRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.OfWhichRealWorkforceRaw) ||
            TotalEligibleAwaitingDestinationRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.EligibleAwaitingDestinationRaw) ||
            TotalTransferredToRecipientRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.TransferredToRecipientRaw) ||
            TotalReturnedFromRecipientRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ReturnedFromRecipientRaw) ||
            TotalExecutedRecipientMaterialRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ExecutedRecipientMaterialRaw) ||
            TotalDeliberatelyRetainedRaw != rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ExplicitRetentionRaw))
            throw new InvalidOperationException(
                "Physical destination coverage diverged from independent canonical replay.");
        RequireCurrent(_session);
    }
}
