using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01A: one physical local custody identity, not a sale.
/// Free stock is eligible by default, existing market commitments remain eligible
/// once (not twice), and deliberate retention is excluded.
/// </summary>
public sealed record CanonicalDefaultMarketAdmissionRow(
    ulong FineTile, uint Commodity,
    BigInteger FreeLocalRaw, BigInteger ExistingMarketCommitmentRaw,
    BigInteger DeliberatelyRetainedRaw)
{
    public BigInteger EligibleForMarketRaw =>
        FreeLocalRaw + ExistingMarketCommitmentRaw;
    public BigInteger AccountedPhysicalRaw =>
        EligibleForMarketRaw + DeliberatelyRetainedRaw;
    public bool IsExecutedSale => false;
}

/// <summary>
/// A session-bound, independently verified eligibility plan for EVERY real
/// local (tile, commodity) stock/custody. Never issues sale actions, income,
/// buyers, deliveries, or a nutritional intake. No imaginary market demand
/// quota caps the eligible physical amount.
///
/// This is the deterministic admission boundary for a future *versioned*
/// automatic-market execution engine; historical saves and ticks are untouched.
/// </summary>
public sealed class CanonicalDefaultMarketAdmissionPlan
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly string _hash;
    private readonly int _eventCount;
    private readonly CanonicalDefaultMarketAdmissionRow[] _rows;

    public IReadOnlyList<CanonicalDefaultMarketAdmissionRow> Rows { get; }
    public ulong MovementTurn { get; }
    public int ClosedEconomicCycles { get; }
    public BigInteger TotalFreeLocalRaw { get; }
    public BigInteger TotalAlreadyOfferedRaw { get; }
    public BigInteger TotalDeliberatelyRetainedRaw { get; }
    public BigInteger TotalEligibleForMarketRaw =>
        TotalFreeLocalRaw + TotalAlreadyOfferedRaw;

    public bool ExcludesExpeditionCargo => true;
    public bool ComputesWithoutDemandCaps => true;
    public bool ProvesActualSaleOrRevenue => false;
    public bool ProvesNutritionalQuality => false;
    public bool ChangesWorldOrSave => false;

    private CanonicalDefaultMarketAdmissionPlan(CanonicalPhysicalTacticalSession session)
    {
        _session = session;
        _hash = session.CurrentHash.HexDigest;
        _eventCount = session.Events.Count;
        MovementTurn = session.State.MovementTurn;
        ClosedEconomicCycles = session.State.Cycles.Count;
        _rows = BuildRows(session);
        Rows = Array.AsReadOnly(_rows);
        TotalFreeLocalRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.FreeLocalRaw);
        TotalAlreadyOfferedRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.ExistingMarketCommitmentRaw);
        TotalDeliberatelyRetainedRaw = _rows.Aggregate(
            BigInteger.Zero, (sum, x) => sum + x.DeliberatelyRetainedRaw);
        VerifyIndependent();
    }

    public static CanonicalDefaultMarketAdmissionPlan Capture(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new(session);
    }

    private static CanonicalDefaultMarketAdmissionRow[] BuildRows(
        CanonicalPhysicalTacticalSession session)
    {
        // The commitment witness verifies exact journal-backed market/retained
        // custody versus physical lots and current free local inventory.
        session.CapturePhysicalStockCommitmentWitness().VerifyIndependent();

        var map = new Dictionary<(ulong Tile, uint Commodity),
            (BigInteger Free, BigInteger Offer, BigInteger Retained)>();

        foreach (var stock in session.State.Stocks)
        {
            if (stock.RawQuantity == 0UL) continue;
            var key = (stock.TileId.FineStrategicCellId.Value, stock.Commodity.Value);
            if (key.Item1 == 0UL || key.Item2 == 0U)
                throw new InvalidOperationException("Invalid local physical identity.");
            if (map.ContainsKey(key))
                throw new InvalidOperationException("Duplicate local physical stock.");
            map.Add(key, (new BigInteger(stock.RawQuantity),
                BigInteger.Zero, BigInteger.Zero));
        }

        foreach (var item in session.PhysicalStockCommitments.Where(x => x.IsLive))
        {
            if (item.FineTile == 0UL || item.Commodity == 0U ||
                item.RawQuantity == 0UL)
                throw new InvalidOperationException("Invalid live custody identity.");
            var key = (item.FineTile, item.Commodity);
            map.TryGetValue(key, out var current);
            var amount = new BigInteger(item.RawQuantity);
            map[key] = item.Kind switch
            {
                CanonicalPhysicalCommitmentKind.MarketOffer =>
                    (current.Free, current.Offer + amount, current.Retained),
                CanonicalPhysicalCommitmentKind.DeliberateRetention =>
                    (current.Free, current.Offer, current.Retained + amount),
                _ => throw new InvalidOperationException("Unknown live custody kind.")
            };
        }

        return map
            .OrderBy(x => x.Key.Tile)
            .ThenBy(x => x.Key.Commodity)
            .Select(x => new CanonicalDefaultMarketAdmissionRow(
                x.Key.Tile, x.Key.Commodity,
                x.Value.Free, x.Value.Offer, x.Value.Retained))
            .ToArray();
    }

    public BigInteger EligibleAt(ulong fineTile, uint commodity)
    {
        if (fineTile == 0UL || commodity == 0U)
            throw new ArgumentOutOfRangeException(
                nameof(fineTile), "A local physical identity must be valid.");
        RequireCurrent(_session);
        return _rows.FirstOrDefault(x =>
            x.FineTile == fineTile && x.Commodity == commodity)?
            .EligibleForMarketRaw ?? BigInteger.Zero;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash.HexDigest != _hash ||
            session.Events.Count != _eventCount ||
            session.State.MovementTurn != MovementTurn ||
            session.State.Cycles.Count != ClosedEconomicCycles)
            throw new InvalidOperationException(
                "Default market admission is stale or belongs to another session.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var actual = BuildRows(_session);
        if (!_rows.SequenceEqual(actual) ||
            _rows.Any(x => x.FreeLocalRaw < 0 ||
                x.ExistingMarketCommitmentRaw < 0 ||
                x.DeliberatelyRetainedRaw < 0) ||
            TotalFreeLocalRaw != actual.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.FreeLocalRaw) ||
            TotalAlreadyOfferedRaw != actual.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.ExistingMarketCommitmentRaw) ||
            TotalDeliberatelyRetainedRaw != actual.Aggregate(
                BigInteger.Zero, (sum, x) => sum + x.DeliberatelyRetainedRaw))
            throw new InvalidOperationException(
                "Default market admission diverged from live physical custody.");
        RequireCurrent(_session);
    }
}
