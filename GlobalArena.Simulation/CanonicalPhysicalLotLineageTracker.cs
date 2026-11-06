using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Origin vintage is a deterministic accounting allocation, not proof that
/// indistinguishable physical atoms came from this particular source.
/// </summary>
public enum CanonicalPhysicalLotOriginKind : byte
{
    UnattributedOpeningStock = 0,
    ExecutedWorkerProduction = 1,
    ExecutedExpeditionForage = 2
}

public enum CanonicalPhysicalLotCustodyKind : byte
{
    LocalStock = 1,
    ExpeditionCargo = 2,
    MarketCommitment = 3,
    DeliberateRetention = 4
}

/// <summary>
/// A physically conserved quantity and its accounting source/vintage.
/// CustodyId is a fine tile for local stock, an order ID for expedition food.
/// The synthetic opening category must never be treated as new production.
/// </summary>
public sealed record CanonicalPhysicalLotHolding(
    ulong OriginCycleOrdinal, ulong OriginMovementTurn,
    ulong OriginFineTile, uint Commodity,
    CanonicalPhysicalLotOriginKind OriginKind,
    CanonicalPhysicalLotCustodyKind Custody, ulong CustodyId,
    ulong RawQuantity);

/// <summary>
/// Deterministic FIFO attribution of executed consumption. An executed
/// consumption is real; the particular lot-to-consumer link is a convention
/// while the canonical inventory remains fungible.
/// </summary>
public sealed record CanonicalPhysicalLotConsumption(
    ulong ConsumingCycleOrdinal, ulong DestinationFineTile,
    CanonicalPhysicalFinalUseChannel UseChannel, ulong JourneyOrderId,
    uint Commodity, ulong OriginCycleOrdinal, ulong OriginMovementTurn,
    ulong OriginFineTile, CanonicalPhysicalLotOriginKind OriginKind,
    ulong RawQuantity);

/// <summary>
/// GA-ECON-REBASE-06: an in-memory, replay-reproducible lot attribution book.
/// It tracks legacy opening stock as unattributed, real worker/camp additions,
/// local usage and provisioned-expedition custody, without changing gameplay.
/// </summary>
internal sealed class CanonicalPhysicalLotLineageTracker
{
    private readonly List<CanonicalPhysicalLotHolding> _holdings;

    private CanonicalPhysicalLotLineageTracker(List<CanonicalPhysicalLotHolding> rows)
    {
        _holdings = rows;
    }

    internal static CanonicalPhysicalLotLineageTracker FromInitialState(
        PhysicalTacticalEconomyRuntimeState state)
    {
        var rows = state.Stocks.Where(x => x.RawQuantity > 0UL)
            .OrderBy(x => x.TileId.FineStrategicCellId.Value)
            .ThenBy(x => x.Commodity.Value)
            .Select(x => new CanonicalPhysicalLotHolding(
                0UL, 0UL, 0UL, x.Commodity.Value,
                CanonicalPhysicalLotOriginKind.UnattributedOpeningStock,
                CanonicalPhysicalLotCustodyKind.LocalStock,
                x.TileId.FineStrategicCellId.Value, x.RawQuantity)).ToList();
        var book = new CanonicalPhysicalLotLineageTracker(rows);
        book.VerifyInventories(state);
        return book;
    }

    internal CanonicalPhysicalLotLineageTracker Clone() =>
        new(new List<CanonicalPhysicalLotHolding>(_holdings));

    internal IReadOnlyList<CanonicalPhysicalLotHolding> Snapshot() =>
        Array.AsReadOnly(_holdings.ToArray());

    private static bool SameCustody(CanonicalPhysicalLotHolding lot,
        CanonicalPhysicalLotCustodyKind custody, ulong id, uint commodity) =>
        lot.Custody == custody && lot.CustodyId == id && lot.Commodity == commodity;

    private List<CanonicalPhysicalLotHolding> Take(
        CanonicalPhysicalLotCustodyKind custody, ulong id, uint commodity,
        ulong quantity)
    {
        if (id == 0UL || commodity == 0U || quantity == 0UL)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        var remaining = quantity;
        var taken = new List<CanonicalPhysicalLotHolding>();
        for (var i = 0; i < _holdings.Count && remaining != 0UL; i++)
        {
            var existing = _holdings[i];
            if (!SameCustody(existing, custody, id, commodity)) continue;
            var amount = Math.Min(existing.RawQuantity, remaining);
            taken.Add(existing with { RawQuantity = amount });
            remaining -= amount;
            if (amount == existing.RawQuantity)
            {
                _holdings.RemoveAt(i);
                i--;
            }
            else
                _holdings[i] = existing with
                {
                    RawQuantity = existing.RawQuantity - amount
                };
        }
        if (remaining != 0UL)
            throw new InvalidOperationException(
                "Physical custody has insufficient accounted lots.");
        return taken;
    }

    /// <summary>
    /// Executed local use of physical wood or stone. FIFO attribution is
    /// deterministic; only the stock debit and classified use are canonical.
    /// </summary>
    internal IReadOnlyList<CanonicalPhysicalLotHolding> ConsumeLocalMaterial(
        ulong fineTile, uint commodity, ulong raw)
    {
        if (commodity is not (2U or 3U))
            throw new ArgumentOutOfRangeException(nameof(commodity),
                "Only actual stone and wood stock can be disposed here.");
        return Array.AsReadOnly(Take(
            CanonicalPhysicalLotCustodyKind.LocalStock,
            fineTile, commodity, raw).ToArray());
    }

    internal void TransferProvisionedFood(ulong sourceFineTile,
        ulong journeyOrderId, ulong physicalFoodRaw)
    {
        if (journeyOrderId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(journeyOrderId));
        if (_holdings.Any(x => x.Custody ==
                CanonicalPhysicalLotCustodyKind.ExpeditionCargo &&
                x.CustodyId == journeyOrderId))
            throw new InvalidOperationException("Duplicate expedition lot custody.");
        var moved = Take(CanonicalPhysicalLotCustodyKind.LocalStock,
            sourceFineTile, 1U, physicalFoodRaw);
        _holdings.AddRange(moved.Select(x => x with
        {
            Custody = CanonicalPhysicalLotCustodyKind.ExpeditionCargo,
            CustodyId = journeyOrderId
        }));
    }

    /// <summary>
    /// Transfer real local stock into a distinct custody ledger at its
    /// canonical action ordinal. No quantity is created or consumed.
    /// </summary>
    internal void CommitLocalStock(ulong fineTile, uint commodity,
        ulong raw, int ordinal, CanonicalPhysicalCommitmentKind kind)
    {
        if (ordinal <= 0 ||
            kind is not (CanonicalPhysicalCommitmentKind.MarketOffer or
                CanonicalPhysicalCommitmentKind.DeliberateRetention))
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var custody = kind == CanonicalPhysicalCommitmentKind.MarketOffer
            ? CanonicalPhysicalLotCustodyKind.MarketCommitment
            : CanonicalPhysicalLotCustodyKind.DeliberateRetention;
        if (_holdings.Any(x => x.Custody == custody &&
            x.CustodyId == (ulong)ordinal))
            throw new InvalidOperationException("Duplicate physical commitment custody.");
        var moved = Take(CanonicalPhysicalLotCustodyKind.LocalStock,
            fineTile, commodity, raw);
        _holdings.AddRange(moved.Select(x => x with
        {
            Custody = custody,
            CustodyId = (ulong)ordinal
        }));
    }

    /// <summary>Release the identical preserved lots to local stock.</summary>
    internal void ReleaseCommittedStock(ulong fineTile, uint commodity,
        ulong raw, int ordinal, CanonicalPhysicalCommitmentKind kind)
    {
        if (ordinal <= 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var custody = kind switch
        {
            CanonicalPhysicalCommitmentKind.MarketOffer =>
                CanonicalPhysicalLotCustodyKind.MarketCommitment,
            CanonicalPhysicalCommitmentKind.DeliberateRetention =>
                CanonicalPhysicalLotCustodyKind.DeliberateRetention,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var moved = Take(custody, (ulong)ordinal, commodity, raw);
        if (_holdings.Any(x => x.Custody == custody &&
            x.CustodyId == (ulong)ordinal))
            throw new InvalidOperationException(
                "Released stock commitment still owns physical cargo.");
        _holdings.AddRange(moved.Select(x => x with
        {
            Custody = CanonicalPhysicalLotCustodyKind.LocalStock,
            CustodyId = fineTile
        }));
    }

    /// <summary>
    /// Actually use the whole, previously committed local MARKET custody
    /// for a specified physical wood/stone use. There is no buyer payment
    /// and no material return to free stock.
    /// </summary>
    internal IReadOnlyList<CanonicalPhysicalLotHolding>
        ConsumeCommittedMarketMaterial(int actionOrdinal, uint commodity,
            ulong raw)
    {
        if (actionOrdinal <= 0 || commodity is not (2U or 3U))
            throw new ArgumentOutOfRangeException(nameof(commodity));
        var consumed = Take(
            CanonicalPhysicalLotCustodyKind.MarketCommitment,
            (ulong)actionOrdinal, commodity, raw);
        if (_holdings.Any(x =>
            x.Custody == CanonicalPhysicalLotCustodyKind.MarketCommitment &&
            x.CustodyId == (ulong)actionOrdinal))
            throw new InvalidOperationException(
                "A complete local physical disposition left orphaned market cargo.");
        return Array.AsReadOnly(consumed.ToArray());
    }

    private void RecordExecutedProduction(
        ulong cycleOrdinal, ulong movementTurn,
        CanonicalExecutedPhysicalProductionRow row)
    {
        if (row.ProducedRaw == 0UL || row.Commodity == 0U ||
            row.SourceFineTile == 0UL)
            throw new InvalidOperationException("Invalid recorded production source.");
        var isCamp = row.Channel == CanonicalPhysicalProductionChannel.ExpeditionCamp;
        if (isCamp ? row.JourneyOrderId == 0UL : row.JourneyOrderId != 0UL)
            throw new InvalidOperationException("Production channel lacks its custody identity.");
        _holdings.Add(new CanonicalPhysicalLotHolding(
            cycleOrdinal, movementTurn, row.SourceFineTile, row.Commodity,
            isCamp ? CanonicalPhysicalLotOriginKind.ExecutedExpeditionForage :
                CanonicalPhysicalLotOriginKind.ExecutedWorkerProduction,
            isCamp ? CanonicalPhysicalLotCustodyKind.ExpeditionCargo :
                CanonicalPhysicalLotCustodyKind.LocalStock,
            isCamp ? row.JourneyOrderId : row.SourceFineTile,
            row.ProducedRaw));
    }

    internal IReadOnlyList<CanonicalPhysicalLotConsumption> RecordClosingCycle(
        ulong cycleOrdinal, ulong movementTurn,
        IReadOnlyList<CanonicalExecutedPhysicalProductionRow> production,
        IReadOnlyList<CanonicalExecutedPhysicalFinalUseRow> finalUse,
        PhysicalTacticalEconomyRuntimeState after)
    {
        if (cycleOrdinal == 0UL || movementTurn == 0UL)
            throw new ArgumentOutOfRangeException(nameof(cycleOrdinal));

        foreach (var source in production)
            RecordExecutedProduction(cycleOrdinal, movementTurn, source);

        var uses = new List<CanonicalPhysicalLotConsumption>();
        foreach (var use in finalUse)
        {
            var isJourney = use.Channel ==
                CanonicalPhysicalFinalUseChannel.ExpeditionRations;
            var custody = isJourney ? CanonicalPhysicalLotCustodyKind.ExpeditionCargo :
                CanonicalPhysicalLotCustodyKind.LocalStock;
            var custodyId = isJourney ? use.JourneyOrderId : use.DestinationFineTile;
            foreach (var piece in Take(custody, custodyId, use.Commodity, use.ConsumedRaw))
                uses.Add(new CanonicalPhysicalLotConsumption(
                    cycleOrdinal, use.DestinationFineTile, use.Channel,
                    use.JourneyOrderId, piece.Commodity,
                    piece.OriginCycleOrdinal, piece.OriginMovementTurn,
                    piece.OriginFineTile, piece.OriginKind, piece.RawQuantity));
        }
        VerifyInventories(after);
        return Array.AsReadOnly(uses.ToArray());
    }

    internal void VerifyInventories(PhysicalTacticalEconomyRuntimeState state)
    {
        if (_holdings.Any(x => x.RawQuantity == 0UL || x.CustodyId == 0UL ||
            x.Commodity == 0U ||
            x.OriginKind is not (
                CanonicalPhysicalLotOriginKind.UnattributedOpeningStock or
                CanonicalPhysicalLotOriginKind.ExecutedWorkerProduction or
                CanonicalPhysicalLotOriginKind.ExecutedExpeditionForage) ||
            x.Custody is not (
                CanonicalPhysicalLotCustodyKind.LocalStock or
                CanonicalPhysicalLotCustodyKind.ExpeditionCargo or
                CanonicalPhysicalLotCustodyKind.MarketCommitment or
                CanonicalPhysicalLotCustodyKind.DeliberateRetention)))
            throw new InvalidOperationException("Invalid physical lot record.");

        var local = _holdings
            .Where(x => x.Custody == CanonicalPhysicalLotCustodyKind.LocalStock)
            .GroupBy(x => (x.CustodyId, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(
                BigInteger.Zero, (v, x) => v + x.RawQuantity));
        var expectedLocal = state.Stocks
            .Where(x => x.RawQuantity != 0UL)
            .ToDictionary(x => (
                CustodyId: x.TileId.FineStrategicCellId.Value,
                Commodity: x.Commodity.Value), x => new BigInteger(x.RawQuantity));
        if (local.Count != expectedLocal.Count || local.Any(x =>
            !expectedLocal.TryGetValue(x.Key, out var q) || q != x.Value))
            throw new InvalidOperationException(
                "Origin/vintage local lots do not reconcile with canonical physical stock.");

        var cargo = _holdings
            .Where(x => x.Custody == CanonicalPhysicalLotCustodyKind.ExpeditionCargo)
            .GroupBy(x => x.CustodyId)
            .ToDictionary(g => g.Key, g => g.Aggregate(
                BigInteger.Zero, (v, x) => v + x.RawQuantity));
        var expectedCargo = (state.TravelFoodLedgers ??
                Array.Empty<PhysicalJourneyFoodLedger>())
            .Where(x => x.FoodRemainingRaw != 0UL)
            .ToDictionary(x => x.OrderId, x => new BigInteger(x.FoodRemainingRaw));
        if (cargo.Count != expectedCargo.Count || cargo.Any(x =>
            !expectedCargo.TryGetValue(x.Key, out var q) || q != x.Value))
            throw new InvalidOperationException(
                "Origin/vintage expedition lots do not reconcile with real carried rations.");
    }
}

/// <summary>
/// Immutable per-cycle snapshot of a deterministic physical lot allocation
/// convention. It is not a proof that stock identity survived physical mixing.
/// </summary>
public sealed class CanonicalPhysicalLotCycleWitness
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalExecutedPhysicalCycleProvenance _physical;
    private readonly CanonicalPhysicalLotHolding[] _holdings;
    private readonly CanonicalPhysicalLotConsumption[] _consumption;

    public ulong CycleOrdinal => _physical.CycleOrdinal;
    public ulong ClosingMovementTurn => _physical.ClosingMovementTurn;
    public string ClosingWorldHashHex => _physical.ClosingWorldHashHex;
    public IReadOnlyList<CanonicalPhysicalLotHolding> ClosingLots { get; }
    public IReadOnlyList<CanonicalPhysicalLotConsumption> FinalUseAttributions { get; }
    public bool ReconstructedByCanonicalActionReplay => true;
    public bool TracksStockAndExpeditionCustodyAcrossCycles => true;
    public bool PhysicalOriginIdentityIsDirectlyAuthenticated => false;
    public bool UsesDeterministicFifoAccountingConvention => true;
    public bool CanAuthorizeRealPurchasingPower => false;
    public bool WritesNewCanonicalSaveFields => false;

    internal CanonicalPhysicalLotCycleWitness(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedPhysicalCycleProvenance physical,
        CanonicalPhysicalLotLineageTracker tracker,
        IReadOnlyList<CanonicalPhysicalLotConsumption> consumed)
    {
        _session = session;
        _physical = physical;
        _holdings = tracker.Snapshot().ToArray();
        _consumption = consumed.ToArray();
        ClosingLots = Array.AsReadOnly(_holdings);
        FinalUseAttributions = Array.AsReadOnly(_consumption);
        VerifyIndependent();
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        _physical.RequireCurrent(session);
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException("Foreign canonical lot witness.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _session.MaterialLotTracker.VerifyInventories(_session.State);
        if (!_holdings.SequenceEqual(_session.MaterialLotTracker.Snapshot()))
            throw new InvalidOperationException("Cross-cycle lots diverged.");
        if (_consumption.Any(x =>
                x.ConsumingCycleOrdinal != CycleOrdinal ||
                x.RawQuantity == 0UL) ||
            _consumption.Aggregate(BigInteger.Zero,
                (total, x) => total + x.RawQuantity) !=
            new BigInteger(_physical.ActuallyConsumedRaw))
            throw new InvalidOperationException("Attributed final uses diverge from real consumption.");
        var canonicalUses = _physical.FinalUse
            .GroupBy(x => (x.DestinationFineTile, x.Commodity, x.Channel, x.JourneyOrderId))
            .ToDictionary(g => g.Key,
                g => g.Aggregate(BigInteger.Zero, (a, x) => a + x.ConsumedRaw));
        var attributed = _consumption
            .GroupBy(x => (x.DestinationFineTile, x.Commodity, x.UseChannel, x.JourneyOrderId))
            .ToDictionary(g => g.Key,
                g => g.Aggregate(BigInteger.Zero, (a, x) => a + x.RawQuantity));
        if (canonicalUses.Count != attributed.Count ||
            canonicalUses.Any(x =>
                !attributed.TryGetValue(x.Key, out var q) || q != x.Value))
            throw new InvalidOperationException("Real destination-use quantities diverge from FIFO attribution.");
        RequireCurrent(_session);
    }
}
