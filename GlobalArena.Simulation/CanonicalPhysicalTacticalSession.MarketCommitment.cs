using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit versioned stock custody: goods moved into a commitment cannot be
/// used by legacy consumers, builders or expeditions until deliberately released.
/// The future abstract clearing engine will require a separate authenticated
/// delivery action. No sale, wallet, new stock or N is created here.
/// </summary>
public sealed partial class CanonicalPhysicalTacticalSession
{
    private readonly List<CanonicalPhysicalStockCommitment>
        _physicalStockCommitments = new();

    public IReadOnlyList<CanonicalPhysicalStockCommitment>
        PhysicalStockCommitments =>
        Array.AsReadOnly(_physicalStockCommitments.ToArray());

    public CanonicalPhysicalStockCommitmentWitness
        CapturePhysicalStockCommitmentWitness()
    {
        VerifyPhysicalStockCommitments();
        return new CanonicalPhysicalStockCommitmentWitness(this);
    }

    private static CanonicalPhysicalLotCustodyKind CommitmentCustody(
        CanonicalPhysicalCommitmentKind kind) => kind switch
        {
            CanonicalPhysicalCommitmentKind.MarketOffer =>
                CanonicalPhysicalLotCustodyKind.MarketCommitment,
            CanonicalPhysicalCommitmentKind.DeliberateRetention =>
                CanonicalPhysicalLotCustodyKind.DeliberateRetention,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    /// <summary>
    /// Reconcile the journal's actual commitments to the exclusive physical
    /// custody of fungible lots. No hidden or invented escrow balances.
    /// </summary>
    internal void VerifyPhysicalStockCommitments()
    {
        _materialLots.VerifyInventories(State);
        if (_physicalStockCommitments.GroupBy(x => x.ActionOrdinal)
            .Any(g => g.Count() != 1))
            throw new InvalidOperationException(
                "Duplicate physical stock commitment ordinal.");

        foreach (var record in _physicalStockCommitments)
        {
            if (record.ActionOrdinal < 1 ||
                record.ActionOrdinal > _journal.Count ||
                record.FineTile == 0UL || record.Commodity == 0U ||
                record.RawQuantity == 0UL ||
                record.Kind is not (
                    CanonicalPhysicalCommitmentKind.MarketOffer or
                    CanonicalPhysicalCommitmentKind.DeliberateRetention))
                throw new InvalidOperationException(
                    "Invalid physical stock commitment receipt.");

            var original = _journal[record.ActionOrdinal - 1];
            if (original.Kind != "commit_physical_stock" ||
                original.Source != record.FineTile ||
                original.Destination != record.FineTile ||
                original.CommitmentCommodity != record.Commodity ||
                original.CommitmentRaw != record.RawQuantity ||
                original.CommitmentKind != (byte)record.Kind ||
                original.CommitmentOrdinal != 0)
                throw new InvalidOperationException(
                    "Stock commitment is not backed by its canonical original action.");

            var releases = _journal.Count(x =>
                x.Kind == "release_physical_stock" &&
                x.CommitmentOrdinal == record.ActionOrdinal);
            if (releases != (record.Released ? 1 : 0))
                throw new InvalidOperationException(
                    "Stock release status differs from canonical journal.");
        }

        var holdings = _materialLots.Snapshot().Where(x =>
            x.Custody is CanonicalPhysicalLotCustodyKind.MarketCommitment or
                CanonicalPhysicalLotCustodyKind.DeliberateRetention)
            .GroupBy(x => (x.Custody, x.CustodyId, x.Commodity))
            .ToDictionary(g => g.Key,
                g => g.Aggregate(BigInteger.Zero,
                    (sum, x) => sum + x.RawQuantity));
        var commitments = _physicalStockCommitments
            .Where(x => x.IsLive)
            .ToDictionary(
                x => (Custody: CommitmentCustody(x.Kind),
                      CustodyId: (ulong)x.ActionOrdinal,
                      x.Commodity),
                x => new BigInteger(x.RawQuantity));

        if (holdings.Count != commitments.Count ||
            holdings.Any(x =>
                !commitments.TryGetValue(x.Key, out var expected) ||
                expected != x.Value))
            throw new InvalidOperationException(
                "Market and retained custody must equal journal-backed physical commitments.");

        if (_journal.Count(x => x.Kind == "commit_physical_stock") !=
            _physicalStockCommitments.Count)
            throw new InvalidOperationException(
                "Orphaned or missing physical commitment receipt.");
    }

    /// <summary>
    /// Canonically lock a physical local stock quantity in escrow. The
    /// commitment's action ordinal is its stable replay identity.
    /// MarketOffer = supply dedicated to future clearing, not a sale.
    /// DeliberateRetention = physical stock withheld from market.
    /// </summary>
    public CanonicalPhysicalStockCommitment CommitPhysicalStock(
        PhysicalTacticalTileId tile, CommodityId commodity,
        CanonicalPhysicalCommitmentKind kind, ulong raw)
    {
        EnsureJournalCapacity();
        ValidateTile(tile);
        if (!commodity.IsValid ||
            kind is not (
                CanonicalPhysicalCommitmentKind.MarketOffer or
                CanonicalPhysicalCommitmentKind.DeliberateRetention) ||
            raw == 0UL)
            throw new ArgumentException(
                "Commitment requires an existing commodity, positive raw amount and valid custody kind.");
        if (WorkersAt(tile) == 0UL)
            throw new InvalidOperationException(
                "A physical stock commitment needs residents at the source tile.");
        if (StockAt(tile, commodity) < raw)
            throw new InvalidOperationException(
                "Committed quantity exceeds physically free local stock.");

        VerifyPhysicalStockCommitments();
        var current = State;
        var ordinal = checked(_journal.Count + 1);
        var next = new PhysicalTacticalEconomyRuntimeState(
            current.CoarseWorldBinding, _coarse, _fine,
            current.MovementTurn, current.WorkerRevision,
            current.Allocations,
            current.Stocks.Select(x => x.TileId == tile &&
                x.Commodity == commodity
                ? x with { RawQuantity = x.RawQuantity - raw } : x),
            current.Cycles, _cadenceMode, _geographyMode,
            current.FoodCash, _movementMode, current.Transits,
            current.LongTrips, current.RemoteRoutingEnabled,
            current.TravelFoodLedgers, current.JourneyCampStates,
            current.PrimitiveFacilities);
        var lots = _materialLots.Clone();
        lots.CommitLocalStock(tile.FineStrategicCellId.Value,
            commodity.Value, raw, ordinal, kind);
        lots.VerifyInventories(next);
        Commit(next, "commit_physical_stock",
            tile.FineStrategicCellId.Value,
            tile.FineStrategicCellId.Value, 0, 0, 0UL,
            commitmentCommodity: commodity.Value,
            commitmentRaw: raw, commitmentKind: (byte)kind);
        _materialLots = lots;
        var row = new CanonicalPhysicalStockCommitment(
            ordinal, tile.FineStrategicCellId.Value, commodity.Value,
            kind, raw, Released: false);
        _physicalStockCommitments.Add(row);
        VerifyPhysicalStockCommitments();
        return row;
    }

    /// <summary>
    /// Cancel a prior commitment atomically. Returns exactly its original
    /// physical quantity and attributed lots to the same local stock tile.
    /// A cancelled record remains in the audit journal forever.
    /// </summary>
    public CanonicalPhysicalStockCommitment ReleasePhysicalStockCommitment(
        int commitmentActionOrdinal)
    {
        EnsureJournalCapacity();
        VerifyPhysicalStockCommitments();
        var index = _physicalStockCommitments.FindIndex(
            x => x.ActionOrdinal == commitmentActionOrdinal);
        if (index < 0 || _physicalStockCommitments[index].Released)
            throw new InvalidOperationException(
                "Only one outstanding canonical physical commitment may be released.");
        var row = _physicalStockCommitments[index];
        var tile = new PhysicalTacticalTileId(
            _fine, new StrategicCellId(row.FineTile));
        var commodity = new CommodityId(row.Commodity);
        ValidateTile(tile);
        var current = State;
        if (!current.Stocks.Any(x =>
            x.TileId == tile && x.Commodity == commodity))
            throw new InvalidOperationException(
                "A committed commodity needs an existing stock identity.");
        var next = new PhysicalTacticalEconomyRuntimeState(
            current.CoarseWorldBinding, _coarse, _fine,
            current.MovementTurn, current.WorkerRevision,
            current.Allocations,
            current.Stocks.Select(x => x.TileId == tile &&
                x.Commodity == commodity
                ? x with { RawQuantity = checked(x.RawQuantity + row.RawQuantity) }
                : x),
            current.Cycles, _cadenceMode, _geographyMode,
            current.FoodCash, _movementMode, current.Transits,
            current.LongTrips, current.RemoteRoutingEnabled,
            current.TravelFoodLedgers, current.JourneyCampStates,
            current.PrimitiveFacilities);
        var lots = _materialLots.Clone();
        lots.ReleaseCommittedStock(row.FineTile, row.Commodity,
            row.RawQuantity, row.ActionOrdinal, row.Kind);
        lots.VerifyInventories(next);
        Commit(next, "release_physical_stock",
            row.FineTile, row.FineTile, 0, 0, 0UL,
            commitmentCommodity: row.Commodity,
            commitmentRaw: row.RawQuantity,
            commitmentKind: (byte)row.Kind,
            commitmentOrdinal: row.ActionOrdinal);
        _materialLots = lots;
        var released = row with { Released = true };
        _physicalStockCommitments[index] = released;
        VerifyPhysicalStockCommitments();
        return released;
    }
}
