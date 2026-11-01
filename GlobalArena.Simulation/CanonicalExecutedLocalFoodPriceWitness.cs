using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Receipt of a food purchase actually settled by the legacy V12 canonical
/// local cash solver, NOT a VS78/VS86 hypothetical relative price.
/// Units: physical fixed-point food; nominal microcredits per full food unit.
/// </summary>
public sealed record CanonicalExecutedLocalFoodPriceRow(
    ulong MovementTurn, ulong FineTile,
    ulong ExecutedFoodRaw, ulong ExecutedPriceMicrocreditsPerWholeUnit,
    ulong ActuallyPaidMicrocredits);

internal sealed record CanonicalExecutedFoodPriceComputation(
    CanonicalExecutedLocalFoodPriceRow[] Rows,
    ulong TotalActualLocalFoodRaw,
    BigInteger TotalActualPaymentsMicrocredits,
    int TotalHistoricalReceipts);

/// <summary>
/// VS87: read-only evidence extracted EXCLUSIVELY from the canonical
/// FoodCash.Receipts of an actually completed economic cycle. Neither VS86
/// sidecars nor VS84 quote indices can serve as receipt authority.
/// </summary>
public sealed class CanonicalExecutedLocalFoodPriceWitness
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalExecutedLocalFoodPriceRow[] _rows;
    private readonly string _worldHashHex;
    private readonly ulong _networkRevision;
    private readonly ulong _workerRevision;
    private readonly int _journalCount;
    private readonly int _cycleCount;
    private readonly int _historicalReceiptCount;

    public ulong CompletedEconomicCycleMovementTurn { get; }
    public ulong TotalActualLocalFoodRaw { get; }
    public BigInteger TotalActualPaymentsMicrocredits { get; }
    public IReadOnlyList<CanonicalExecutedLocalFoodPriceRow> ExecutedReceipts { get; }
    public string SourceCanonicalWorldHashHex => _worldHashHex;
    public bool HasExecutedPriceObservation => _rows.Length != 0;
    public bool HasEvidenceOfRealCashFoodPurchase => HasExecutedPriceObservation;
    public bool QuotesAbstractVs86Price => false;
    public bool EstablishesRealPricesForOtherCommodities => false;
    public bool ChangesEconomicSettlement => false;
    public bool CanAuthorizeFutureSettlement => false;
    public bool IsFromCanonicalFoodCashState => true;
    public string ObservedNominalUnitId => "GA-LEGACY-FOOD-CASH-MICROCREDITS";
    public uint ObservedCommodityId => 1U;

    private CanonicalExecutedLocalFoodPriceWitness(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedFoodPriceComputation read)
    {
        _session = session;
        _rows = read.Rows.ToArray();
        ExecutedReceipts = Array.AsReadOnly(_rows);
        TotalActualLocalFoodRaw = read.TotalActualLocalFoodRaw;
        TotalActualPaymentsMicrocredits = read.TotalActualPaymentsMicrocredits;
        _historicalReceiptCount = read.TotalHistoricalReceipts;
        _worldHashHex = session.CurrentHash.HexDigest;
        _networkRevision = session.TransportNetwork.Revision;
        _workerRevision = session.State.WorkerRevision;
        _journalCount = session.Events.Count;
        _cycleCount = session.State.Cycles.Count;
        CompletedEconomicCycleMovementTurn = session.State.MovementTurn;
    }

    internal static CanonicalExecutedFoodPriceComputation Reconstruct(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.MarketMode != PhysicalFoodMarketMode.LocalCashSettlement)
            throw new InvalidOperationException(
                "An actual local cash-food price witness requires the canonical V12 cash settlement mode.");
        var state = session.State;
        var ledger = state.FoodCash ??
            throw new InvalidOperationException("Canonical cash-food ledger is absent.");
        var events = session.Events;
        if (state.Cycles.Count == 0 ||
            state.Cycles[^1].MovementTurn != state.MovementTurn ||
            events.Count == 0 || events[^1].Kind != "tick" ||
            events[^1].AfterHashHex != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "The executed price must come directly from a real cycle-closing canonical tick.");

        var sorted = ledger.Receipts.ToArray();
        var latest = new List<CanonicalExecutedLocalFoodPriceRow>();
        var seen = new HashSet<(ulong Turn, ulong Tile)>();
        ulong delivered = 0UL;
        BigInteger money = BigInteger.Zero;
        ulong previousTurn = 0;
        foreach (var item in sorted)
        {
            var tile = item.Tile.FineStrategicCellId.Value;
            if (tile == 0UL || item.MovementTurn == 0UL ||
                item.MovementTurn > state.MovementTurn ||
                item.MovementTurn < previousTurn ||
                item.FoodRaw == 0UL ||
                !seen.Add((item.MovementTurn, tile)))
                throw new InvalidOperationException(
                    "Invalid, nonmonotone, duplicate or nonphysical executed cash-food receipt.");
            previousTurn = item.MovementTurn;

            var exactPayment = (BigInteger)item.FoodRaw *
                item.PriceMicrocreditsPerUnit /
                PhysicalFoodCashMarketState.MonetaryScale;
            if (exactPayment != item.PaidMicrocredits)
                throw new InvalidOperationException(
                    "Canonical price receipt does not reconcile paid microcredits with delivered physical food.");

            if (item.MovementTurn == state.MovementTurn)
            {
                latest.Add(new CanonicalExecutedLocalFoodPriceRow(
                    item.MovementTurn, tile, item.FoodRaw,
                    item.PriceMicrocreditsPerUnit, item.PaidMicrocredits));
                delivered = checked(delivered + item.FoodRaw);
                money += item.PaidMicrocredits;
            }
        }

        if (delivered > state.Cycles[^1].FoodConsumedRaw)
            throw new InvalidOperationException(
                "Local cash-food receipts exceed the real physical food consumption in this economic cycle.");
        return new CanonicalExecutedFoodPriceComputation(
            latest.OrderBy(x => x.FineTile).ToArray(),
            delivered, money, sorted.Length);
    }

    public static CanonicalExecutedLocalFoodPriceWitness FromLatestRealSettlement(
        CanonicalPhysicalTacticalSession session)
    {
        var read = Reconstruct(session);
        var answer = new CanonicalExecutedLocalFoodPriceWitness(session, read);
        answer.VerifyIndependent();
        return answer;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash.HexDigest != _worldHashHex ||
            session.TransportNetwork.Revision != _networkRevision ||
            session.State.WorkerRevision != _workerRevision ||
            session.State.MovementTurn != CompletedEconomicCycleMovementTurn ||
            session.State.Cycles.Count != _cycleCount ||
            session.Events.Count != _journalCount)
            throw new InvalidOperationException(
                "Executed local food-price witness belongs to another or stale canonical world state.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var current = Reconstruct(_session);
        if (current.TotalHistoricalReceipts != _historicalReceiptCount ||
            current.TotalActualLocalFoodRaw != TotalActualLocalFoodRaw ||
            current.TotalActualPaymentsMicrocredits !=
                TotalActualPaymentsMicrocredits ||
            !_rows.SequenceEqual(current.Rows))
            throw new InvalidOperationException(
                "Independent recomputation from executed canonical cash/food receipts diverged.");
        RequireCurrent(_session);
    }
}
