using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// One location/commodity from the real physical ledger. Market eligibility
/// is a policy candidate, not a sale, delivery, or transfer of custody.
/// </summary>
public sealed record CanonicalDefaultMarketPoolRow(
    ulong FineTile, uint Commodity, BigInteger UncommittedLocalRaw,
    BigInteger AlreadyCommittedOfferRaw, BigInteger DeliberatelyRetainedRaw)
{
    public BigInteger EligibleByDefaultRaw =>
        UncommittedLocalRaw + AlreadyCommittedOfferRaw;
    public BigInteger TotalAccountedRaw =>
        EligibleByDefaultRaw + DeliberatelyRetainedRaw;
}

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01A: global read-only inventory classification
/// for the target offer-by-default policy. The current runtime DOES NOT
/// execute automatic sales; this preflight prevents treating historic
/// uncommitted stock as deliberate retention.
/// </summary>
public sealed class CanonicalDefaultMarketPoolPreflight
{
    private readonly CanonicalPhysicalTacticalSession _source;
    private readonly string _hash;
    private readonly int _eventCount;
    private readonly CanonicalDefaultMarketPoolRow[] _rows;

    public IReadOnlyList<CanonicalDefaultMarketPoolRow> Rows { get; }
    public BigInteger EligibleByDefaultRaw { get; }
    public BigInteger DeliberatelyRetainedRaw { get; }
    public BigInteger TotalAccountedRaw { get; }

    public bool CertifiesActualAutomaticOffers => false;
    public bool CertifiesExecutedSales => false;
    public bool AuthorizesSettlementOrRevenue => false;
    public bool ModifiesCanonicalState => false;

    private CanonicalDefaultMarketPoolPreflight(
        CanonicalPhysicalTacticalSession session)
    {
        _source = session;
        _hash = session.CurrentHash.HexDigest;
        _eventCount = session.Events.Count;

        // The existing witness authenticates the exclusive stock locks.
        session.CapturePhysicalStockCommitmentWitness().VerifyIndependent();

        var buckets = new Dictionary<(ulong Tile, uint Commodity),
            (BigInteger Free, BigInteger Offer, BigInteger Retain)>();

        foreach (var stock in session.State.Stocks)
        {
            var id = stock.Commodity.Value;
            if (id is not (1U or 2U or 3U))
                throw new InvalidOperationException(
                    "Unknown physical commodity cannot be silently marketed.");
            var key = (stock.TileId.FineStrategicCellId.Value, id);
            if (buckets.ContainsKey(key))
                throw new InvalidOperationException(
                    "Duplicate physical stock would double-count market supply.");
            buckets[key] = (new BigInteger(stock.RawQuantity),
                BigInteger.Zero, BigInteger.Zero);
        }

        foreach (var hold in session.PhysicalStockCommitments.Where(x => x.IsLive))
        {
            if (hold.Commodity is not (1U or 2U or 3U))
                throw new InvalidOperationException(
                    "Unknown committed commodity cannot be silently marketed.");
            var key = (hold.FineTile, hold.Commodity);
            buckets.TryGetValue(key, out var previous);
            buckets[key] = hold.Kind switch
            {
                CanonicalPhysicalCommitmentKind.MarketOffer =>
                    (previous.Free, previous.Offer + hold.RawQuantity, previous.Retain),
                CanonicalPhysicalCommitmentKind.DeliberateRetention =>
                    (previous.Free, previous.Offer, previous.Retain + hold.RawQuantity),
                _ => throw new InvalidOperationException(
                    "Unrecognized commitment cannot be assumed sellable.")
            };
        }

        _rows = buckets.OrderBy(x => x.Key.Tile)
            .ThenBy(x => x.Key.Commodity)
            .Select(x => new CanonicalDefaultMarketPoolRow(
                x.Key.Tile, x.Key.Commodity,
                x.Value.Free, x.Value.Offer, x.Value.Retain))
            .ToArray();
        Rows = Array.AsReadOnly(_rows);
        EligibleByDefaultRaw = _rows.Aggregate(BigInteger.Zero,
            (total, row) => total + row.EligibleByDefaultRaw);
        DeliberatelyRetainedRaw = _rows.Aggregate(BigInteger.Zero,
            (total, row) => total + row.DeliberatelyRetainedRaw);
        TotalAccountedRaw = EligibleByDefaultRaw + DeliberatelyRetainedRaw;
        VerifyCurrent();
    }

    public static CanonicalDefaultMarketPoolPreflight FromCurrent(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new(session);
    }

    public CanonicalDefaultMarketPoolRow ForTileCommodity(
        ulong fineTile, uint commodity)
    {
        if (commodity is not (1U or 2U or 3U))
            throw new ArgumentOutOfRangeException(nameof(commodity));
        VerifyCurrent();
        return _rows.FirstOrDefault(x =>
            x.FineTile == fineTile && x.Commodity == commodity)
            ?? new CanonicalDefaultMarketPoolRow(fineTile, commodity,
                BigInteger.Zero, BigInteger.Zero, BigInteger.Zero);
    }

    public void VerifyCurrent()
    {
        if (_source.CurrentHash.HexDigest != _hash ||
            _source.Events.Count != _eventCount)
            throw new InvalidOperationException(
                "Market pool preflight is stale after a canonical action.");
        _source.CapturePhysicalStockCommitmentWitness().VerifyIndependent();

        var free = _source.State.Stocks.Aggregate(BigInteger.Zero,
            (total, stock) => total + stock.RawQuantity);
        var locks = _source.PhysicalStockCommitments
            .Where(x => x.IsLive)
            .Aggregate(BigInteger.Zero,
                (total, hold) => total + hold.RawQuantity);

        if (free + locks != TotalAccountedRaw ||
            _rows.Any(x => x.UncommittedLocalRaw < 0 ||
                x.AlreadyCommittedOfferRaw < 0 ||
                x.DeliberatelyRetainedRaw < 0) ||
            _rows.Aggregate(BigInteger.Zero,
                (total, row) => total + row.EligibleByDefaultRaw) !=
                EligibleByDefaultRaw ||
            _rows.Aggregate(BigInteger.Zero,
                (total, row) => total + row.DeliberatelyRetainedRaw) !=
                DeliberatelyRetainedRaw)
            throw new InvalidOperationException(
                "Physical inventory and market classification do not conserve.");
    }
}
