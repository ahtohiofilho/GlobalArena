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

    private readonly List<CanonicalExecutedCommittedMaterialUse>
        _executedCommittedMaterialUses = new();

    public IReadOnlyList<CanonicalExecutedCommittedMaterialUse>
        ExecutedCommittedMaterialUses =>
        Array.AsReadOnly(_executedCommittedMaterialUses.ToArray());

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
            var uses = _journal.Count(x =>
                (x.Kind is "use_committed_material" or
                    "use_committed_material_for_workers") &&
                x.CommitmentOrdinal == record.ActionOrdinal);
            if (record.Released && record.Consumed ||
                releases != (record.Released ? 1 : 0) ||
                uses != (record.Consumed ? 1 : 0) ||
                (record.Consumed &&
                 record.Kind != CanonicalPhysicalCommitmentKind.MarketOffer))
                throw new InvalidOperationException(
                    "Closing market custody disagrees with journal release/use receipts.");
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
            _physicalStockCommitments.Count ||
            _journal.Count(x => x.Kind is
                "use_committed_material" or "use_committed_material_for_workers") !=
            _executedCommittedMaterialUses.Count)
            throw new InvalidOperationException(
                "Orphaned or missing physical commitment / use receipt.");

        foreach (var executed in _executedCommittedMaterialUses)
        {
            if (executed.ActionOrdinal < 1 ||
                executed.ActionOrdinal > _journal.Count)
                throw new InvalidOperationException("Invalid executed material-use action ordinal.");
            var action = _journal[executed.ActionOrdinal - 1];
            var workforce = executed.ProvesLocalWorkforceGroup;
            if (action.Kind != (workforce
                    ? "use_committed_material_for_workers"
                    : "use_committed_material") ||
                action.ToJob != (workforce
                    ? (byte)executed.BeneficiaryActivity!.Value : (byte)0) ||
                action.Workers != executed.BeneficiaryWorkers ||
                action.FromJob != 0 ||
                action.CommitmentOrdinal != executed.SourceCommitmentActionOrdinal ||
                action.CommitmentCommodity != executed.Commodity ||
                action.CommitmentRaw != executed.UsedRaw ||
                action.MaterialUseKind != (byte)executed.Kind ||
                action.Source != executed.FineTile ||
                action.Destination != executed.FineTile ||
                action.BeforeHashHex != executed.BeforeWorldHashHex ||
                action.AfterHashHex != executed.AfterWorldHashHex)
                throw new InvalidOperationException(
                    "Executed material use is not tied to its exact journal action.");
            var origin = _physicalStockCommitments.SingleOrDefault(x =>
                x.ActionOrdinal == executed.SourceCommitmentActionOrdinal);
            if (origin is null || !origin.Consumed ||
                origin.FineTile != executed.FineTile ||
                origin.Commodity != executed.Commodity ||
                origin.RawQuantity != executed.UsedRaw ||
                origin.Kind != CanonicalPhysicalCommitmentKind.MarketOffer)
                throw new InvalidOperationException(
                    "Executed material destination lacks the authentic consumed market commitment.");
        }
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
    /// Execute the whole committed MARKET quantity as an actual, classified
    /// local wood/stone physical use. No buyer, transport, monetary settlement
    /// or produced finished product is inferred. Retention cannot be consumed
    /// through this market-only path.
    /// </summary>
    public CanonicalExecutedCommittedMaterialUse UseCommittedLocalMaterial(
        int commitmentActionOrdinal, CanonicalMaterialDispositionKind kind) =>
        UseCommittedLocalMaterialCore(commitmentActionOrdinal, kind, null);

    /// <summary>
    /// Execute the SAME real material sink with a directly authenticated
    /// local workgroup (tile + activity + people assigned at the action).
    /// This is not a named person, a legal buyer or a market payment.
    /// Remote recipients must use a separate physical transport contract.
    /// </summary>
    public CanonicalExecutedCommittedMaterialUse UseCommittedLocalMaterialForWorkforce(
        int commitmentActionOrdinal, EconomicActivityKind beneficiaryActivity,
        CanonicalMaterialDispositionKind kind)
    {
        ValidateJob(beneficiaryActivity);
        return UseCommittedLocalMaterialCore(
            commitmentActionOrdinal, kind, beneficiaryActivity);
    }

    private CanonicalExecutedCommittedMaterialUse UseCommittedLocalMaterialCore(
        int commitmentActionOrdinal, CanonicalMaterialDispositionKind kind,
        EconomicActivityKind? beneficiaryActivity)
    {
        EnsureJournalCapacity();
        VerifyPhysicalStockCommitments();
        if (kind is not (CanonicalMaterialDispositionKind.FinalPhysicalUse or
            CanonicalMaterialDispositionKind.IntermediateInputUse))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var index = _physicalStockCommitments.FindIndex(x =>
            x.ActionOrdinal == commitmentActionOrdinal);
        if (index < 0 || !_physicalStockCommitments[index].IsLive ||
            _physicalStockCommitments[index].Kind !=
                CanonicalPhysicalCommitmentKind.MarketOffer)
            throw new InvalidOperationException(
                "Only an active market commitment may be physically used.");
        var source = _physicalStockCommitments[index];
        if (source.Commodity is not (2U or 3U))
            throw new InvalidOperationException(
                "Food has its own real nutritional consumption path; this use only accepts wood and stone.");
        var tile = new PhysicalTacticalTileId(
            _fine, new StrategicCellId(source.FineTile));
        ValidateTile(tile);
        if (WorkersAt(tile) == 0UL)
            throw new InvalidOperationException(
                "Executed local material use requires a physically inhabited site.");
        var beneficiaryWorkers = beneficiaryActivity is EconomicActivityKind activity
            ? State.Allocations.FirstOrDefault(x =>
                x.TileId == tile && x.Activity == activity)?.Workers ?? 0UL
            : 0UL;
        if (beneficiaryActivity.HasValue && beneficiaryWorkers == 0UL)
            throw new InvalidOperationException(
                "No active real workers with that activity are present at the committed material's tile.");
        var staged = _materialLots.Clone();
        var attributions = staged.ConsumeCommittedMarketMaterial(
            source.ActionOrdinal, source.Commodity, source.RawQuantity);
        staged.VerifyInventories(State);
        // The actual stock debit happened at the earlier commitment.
        // This execution removes the reserved custody, not another copy
        // of the local stock. The canonical action still advances the hash.
        Commit(State, beneficiaryActivity.HasValue
                ? "use_committed_material_for_workers"
                : "use_committed_material",
            source.FineTile, source.FineTile, 0,
            beneficiaryActivity.HasValue ? (byte)beneficiaryActivity.Value : (byte)0,
            beneficiaryWorkers,
            materialUseKind: (byte)kind,
            commitmentCommodity: source.Commodity,
            commitmentRaw: source.RawQuantity,
            commitmentKind: (byte)source.Kind,
            commitmentOrdinal: source.ActionOrdinal);
        _materialLots = staged;
        _physicalStockCommitments[index] = source with { Consumed = true };
        var action = _journal[^1];
        var executed = new CanonicalExecutedCommittedMaterialUse(
            _journal.Count, source.ActionOrdinal, State.MovementTurn,
            source.FineTile, source.Commodity, kind, source.RawQuantity,
            action.BeforeHashHex, action.AfterHashHex, attributions,
            beneficiaryActivity, beneficiaryWorkers);
        _executedCommittedMaterialUses.Add(executed);
        VerifyPhysicalStockCommitments();
        return executed;
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
        if (index < 0 || !_physicalStockCommitments[index].IsLive)
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
