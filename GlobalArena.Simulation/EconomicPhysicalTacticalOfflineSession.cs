using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A physically local store of a primitive commodity, expressed in the
/// same raw micro-units as EconomicPhysicalTacticalWorkerSession.Preview.
/// </summary>
public sealed record TacticalPrimitiveStock(
    PhysicalTacticalTileId TileId, CommodityId Commodity, ulong RawQuantity);

public sealed record TacticalPrimitiveEconomicCycle(
    ulong MovementTurn, ulong ProducedRaw, ulong FoodConsumedRaw,
    ulong FoodUnmetRaw);

public sealed record TacticalOfflineAction(
    string Kind, ulong Source, ulong Destination, byte FromJob, byte ToJob, ulong Workers);

public sealed record TacticalOfflinePayload(
    int Version, ulong Seed, int CoarseM, int CoarseN, int FineM, int FineN,
    ulong MovementTurn, ulong WorkerRevision, ulong TotalWorkers,
    TacticalOfflineAction[] Actions, TacticalPrimitiveStockRow[] Stocks,
    TacticalPrimitiveEconomicCycle[] Cycles);

public sealed record TacticalPrimitiveStockRow(ulong Tile, uint Commodity, ulong Raw);

public sealed record TacticalOfflineEnvelope(int Version, string PayloadJson, string PayloadSha256Hex);

/// <summary>
/// An explicitly ISOLATED offline progression loop over the existing physical
/// worker model. This does not mutate CanonicalGameSession.WorldState and is
/// not yet the definitive market, logistics or movement policy.
///
/// Six player-facing movement ticks trigger one primitive local economic
/// settlement. Supply is added to stock before local food consumption.
/// There is no teleported trade between physical tiles; unmet food demand is
/// observable but does not cause population changes yet.
/// Versioned save uses replay + exact physical stock verification.
/// </summary>
public sealed class EconomicPhysicalTacticalOfflineSession
{
    public const int SaveFormat = 1;
    public const ulong EconomicTickInterval = 6UL;
    public const int MaximumActions = 100_000;
    private const int MaximumSaveBytes = 4_000_000;

    private readonly ulong _seed;
    private readonly GoldbergParameters _coarse;
    private readonly GoldbergParameters _fine;
    private readonly List<TacticalOfflineAction> _history = new();
    private readonly List<TacticalPrimitiveEconomicCycle> _cycles = new();
    private Dictionary<(PhysicalTacticalTileId Tile, CommodityId Commodity), ulong> _stocks = new();

    public EconomicPhysicalTacticalWorkerSession Workers { get; }
    public ulong MovementTurn { get; private set; }
    public ulong EconomicCycles => (ulong)_cycles.Count;
    public IReadOnlyList<TacticalPrimitiveEconomicCycle> Cycles =>
        Array.AsReadOnly(_cycles.ToArray());

    public IReadOnlyList<TacticalPrimitiveStock> Stocks => Array.AsReadOnly(
        _stocks.OrderBy(x => x.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(x => x.Key.Commodity.Value)
            .Select(x => new TacticalPrimitiveStock(
                x.Key.Tile, x.Key.Commodity, x.Value)).ToArray());

    public EconomicPhysicalTacticalOfflineSession(
        ulong seed = 20261012UL,
        GoldbergParameters? coarse = null,
        GoldbergParameters? fine = null)
    {
        _seed = seed;
        _coarse = coarse ?? new GoldbergParameters(3, 0);
        _fine = fine ?? new GoldbergParameters(18, 0);
        Workers = new EconomicPhysicalTacticalWorkerSession(seed, _coarse, _fine);
    }

    public void MoveWorkers(PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        EconomicActivityKind fromJob, EconomicActivityKind toJob, ulong amount)
    {
        EnsureJournalCapacity();
        // The existing worker implementation validates physical adjacency,
        // worker conservation and job compatibility BEFORE changing state.
        Workers.MoveWorkers(source, destination, fromJob, toJob, amount);
        _history.Add(new TacticalOfflineAction("move",
            source.FineStrategicCellId.Value, destination.FineStrategicCellId.Value,
            checked((byte)fromJob), checked((byte)toJob), amount));
    }

    /// <summary>
    /// Advance a time tick. Production and food consumption settle together
    /// every six movement ticks. This is a real stock mutation, unlike Preview,
    /// but travel duration/AI/markets are not yet implemented.
    /// </summary>
    public TacticalPrimitiveEconomicCycle? AdvanceMovementTurn()
    {
        EnsureJournalCapacity();
        var next = checked(MovementTurn + 1UL);
        TacticalPrimitiveEconomicCycle? result = null;
        Dictionary<(PhysicalTacticalTileId Tile, CommodityId Commodity), ulong>? staged = null;
        if (next % EconomicTickInterval == 0)
        {
            staged = new(_stocks);
            var flows = Workers.Preview();
            ulong produced = 0;
            ulong consumed = 0;
            ulong unmet = 0;

            foreach (var supply in flows.Supply)
            {
                var key = (supply.TileId, supply.CommodityId);
                staged.TryGetValue(key, out var current);
                staged[key] = checked(current + supply.RawQuantity);
                produced = checked(produced + supply.RawQuantity);
            }

            foreach (var demand in flows.Demand)
            {
                var key = (demand.TileId, demand.CommodityId);
                staged.TryGetValue(key, out var available);
                var delivered = Math.Min(available, demand.RawQuantity);
                if (delivered != 0)
                    staged[key] = available - delivered;
                consumed = checked(consumed + delivered);
                unmet = checked(unmet + (demand.RawQuantity - delivered));
            }

            result = new(next, produced, consumed, unmet);
        }
        // No externally visible state changes before all checked arithmetic.
        if (staged is not null)
        {
            _stocks = staged;
            _cycles.Add(result!);
        }
        MovementTurn = next;
        _history.Add(new TacticalOfflineAction("tick", 0, 0, 0, 0, 0));
        return result;
    }

    public ulong StockAt(PhysicalTacticalTileId tile, CommodityId commodity)
    {
        _ = Workers.DisplayBucket(tile); // reject wrong-world identities
        return _stocks.TryGetValue((tile, commodity), out var value) ? value : 0UL;
    }

    /// <summary>
    /// A defensive, immutable projection for the canonical WorldState domain.
    /// VS07's offline journal remains the driver until a subsequent migration
    /// transfers event, save and transition authority. Do not treat this as
    /// a second independently mutable workforce.
    /// </summary>
    public PhysicalTacticalEconomyRuntimeState BuildPhysicalRuntimeSnapshot() => new(
        RuntimeWorldBinding.FromGeneratedWorld(Workers.CoarseWorld),
        _coarse, _fine, MovementTurn, Workers.Revision,
        Workers.Assignments.Select(x =>
            new PhysicalWorkerAllocation(x.TileId, x.Activity, x.Workers)),
        Stocks.Select(x =>
            new PhysicalCommodityStock(x.TileId, x.Commodity, x.RawQuantity)),
        _cycles.Select(x =>
            new PhysicalEconomicCycle(x.MovementTurn, x.ProducedRaw,
                x.FoodConsumedRaw, x.FoodUnmetRaw)));

    public string ExportSaveJson()
    {
        var payload = SnapshotPayload();
        var content = JsonSerializer.Serialize(payload);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return JsonSerializer.Serialize(new TacticalOfflineEnvelope(
            SaveFormat, content, hash));
    }

    public static EconomicPhysicalTacticalOfflineSession RestoreSaveJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumSaveBytes)
            throw new InvalidOperationException("Offline save exceeds its bounded size.");
        TacticalOfflineEnvelope envelope;
        TacticalOfflinePayload payload;
        try
        {
            envelope = JsonSerializer.Deserialize<TacticalOfflineEnvelope>(json)
                ?? throw new InvalidOperationException("Offline envelope missing.");
            if (envelope.Version != SaveFormat ||
                string.IsNullOrEmpty(envelope.PayloadJson) ||
                string.IsNullOrEmpty(envelope.PayloadSha256Hex))
                throw new InvalidOperationException("Unsupported offline save envelope.");
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson));
            if (envelope.PayloadSha256Hex.Length != 64 ||
                !CryptographicOperations.FixedTimeEquals(digest,
                    Convert.FromHexString(envelope.PayloadSha256Hex)))
                throw new InvalidOperationException("Offline save digest mismatch.");
            payload = JsonSerializer.Deserialize<TacticalOfflinePayload>(envelope.PayloadJson)
                ?? throw new InvalidOperationException("Offline payload missing.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("Invalid offline save encoding.", ex);
        }
        if (payload.Version != SaveFormat ||
            payload.Actions is null || payload.Stocks is null || payload.Cycles is null ||
            payload.Actions.Length > MaximumActions)
            throw new InvalidOperationException("Unsupported or unbounded offline payload.");

        var recreated = new EconomicPhysicalTacticalOfflineSession(payload.Seed,
            new GoldbergParameters(payload.CoarseM, payload.CoarseN),
            new GoldbergParameters(payload.FineM, payload.FineN));
        foreach (var action in payload.Actions)
        {
            if (action is null)
                throw new InvalidOperationException("Null offline action.");
            switch (action.Kind)
            {
                case "move":
                    recreated.MoveWorkers(
                        recreated.Workers.PhysicalId(new StrategicCellId(action.Source)),
                        recreated.Workers.PhysicalId(new StrategicCellId(action.Destination)),
                        (EconomicActivityKind)action.FromJob,
                        (EconomicActivityKind)action.ToJob, action.Workers);
                    break;
                case "tick" when action.Source == 0 && action.Destination == 0 &&
                                 action.FromJob == 0 && action.ToJob == 0 &&
                                 action.Workers == 0:
                    recreated.AdvanceMovementTurn();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported offline action.");
            }
        }
        // Compare the reconstructed physical state rather than trusting
        // supplied counters/stocks or treating a digest as authorization.
        var rebuilt = recreated.SnapshotPayload();
        if (!string.Equals(JsonSerializer.Serialize(rebuilt),
                JsonSerializer.Serialize(payload), StringComparison.Ordinal))
            throw new InvalidOperationException("Offline replay diverged from saved state.");
        return recreated;
    }

    private TacticalOfflinePayload SnapshotPayload() => new(
        SaveFormat, _seed, _coarse.M, _coarse.N, _fine.M, _fine.N,
        MovementTurn, Workers.Revision, Workers.TotalWorkers,
        _history.ToArray(),
        Stocks.Select(x => new TacticalPrimitiveStockRow(
            x.TileId.FineStrategicCellId.Value, x.Commodity.Value, x.RawQuantity)).ToArray(),
        _cycles.ToArray());

    private void EnsureJournalCapacity()
    {
        if (_history.Count >= MaximumActions)
            throw new InvalidOperationException("Offline action journal is full.");
    }
}
