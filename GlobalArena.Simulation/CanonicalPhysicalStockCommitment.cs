using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// The two physically exclusive locks available in the new explicit market
/// commitment version. Neither represents a sale, price, wage or intake.
/// </summary>
public enum CanonicalPhysicalCommitmentKind : byte
{
    MarketOffer = 1,
    DeliberateRetention = 2
}

/// <summary>
/// A journal-backed physical custody transfer from local stock into a
/// segregated commitment. The source lots use deterministic FIFO attribution,
/// not a claim of authentic physical molecule identity.
/// </summary>
public sealed record CanonicalPhysicalStockCommitment(
    int ActionOrdinal, ulong FineTile, uint Commodity,
    CanonicalPhysicalCommitmentKind Kind, ulong RawQuantity,
    bool Released)
{
    public bool IsLive => !Released;
    public bool IsExecutedSale => false;
    public bool IsExecutedRetention => Kind ==
        CanonicalPhysicalCommitmentKind.DeliberateRetention && IsLive;
    public bool IsExecutedMarketOfferCommitment => Kind ==
        CanonicalPhysicalCommitmentKind.MarketOffer && IsLive;
    public bool CreatesMoneyOrPhysicalGoods => false;
}

/// <summary>
/// Real-sidecar commitment snapshot, reconstructed only from the canonical
/// journal and checked against the reserved lot custody after every action.
/// Pending goods are physically isolated from ordinary available stock.
/// </summary>
public sealed class CanonicalPhysicalStockCommitmentWitness
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly string _hash;
    private readonly int _events;
    private readonly CanonicalPhysicalStockCommitment[] _rows;

    public IReadOnlyList<CanonicalPhysicalStockCommitment> Commitments { get; }
    public BigInteger LiveMarketOffersRaw { get; }
    public BigInteger LiveRetentionsRaw { get; }
    public bool HasExecutedPhysicalStockLocks => true;
    public bool HasExecutedMarketSales => false;
    public bool IssuesCurrencyOrBuyerWallets => false;
    public bool AuthenticatesNOrPriceCurve => false;

    internal CanonicalPhysicalStockCommitmentWitness(
        CanonicalPhysicalTacticalSession session)
    {
        _session = session;
        _hash = session.CurrentHash.HexDigest;
        _events = session.Events.Count;
        _rows = session.PhysicalStockCommitments.ToArray();
        Commitments = Array.AsReadOnly(_rows);
        LiveMarketOffersRaw = _rows.Where(x =>
            x.IsExecutedMarketOfferCommitment).Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        LiveRetentionsRaw = _rows.Where(x =>
            x.IsExecutedRetention).Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        VerifyIndependent();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session) ||
            _events != session.Events.Count ||
            _hash != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "The physical commitment witness is stale or belongs to another session.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _session.VerifyPhysicalStockCommitments();
        if (!_rows.SequenceEqual(_session.PhysicalStockCommitments) ||
            LiveMarketOffersRaw != _rows.Where(x =>
                x.IsExecutedMarketOfferCommitment).Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.RawQuantity) ||
            LiveRetentionsRaw != _rows.Where(x =>
                x.IsExecutedRetention).Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.RawQuantity))
            throw new InvalidOperationException(
                "Physical commitment witness disagrees with canonical journal custody.");
    }
}
