using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Audit-only event: the canonical WorldState is the unique live authority.
/// Each event binds the exact previous and resulting WorldState hashes.
/// </summary>
public sealed record CanonicalPhysicalAction(
    string Kind, ulong Source, ulong Destination, byte FromJob, byte ToJob,
    ulong Workers, ulong BeforeRevision, string BeforeHashHex,
    ulong AfterRevision, string AfterHashHex);

public sealed record CanonicalPhysicalWorkerSaveRow(ulong Tile, byte Job, ulong Workers);
public sealed record CanonicalPhysicalStockSaveRow(ulong Tile, uint Commodity, ulong Raw);
public sealed record CanonicalPhysicalCycleSaveRow(
    ulong MovementTurn, ulong ProducedRaw, ulong FoodConsumedRaw, ulong FoodUnmetRaw);

public sealed record CanonicalPhysicalSavePayload(
    int Version, ulong Seed, int GeneratorVersion,
    int CoarseM, int CoarseN, int FineM, int FineN,
    string WorldBindingHex, string InitialHashHex,
    ulong Revision, ulong MovementTurn, ulong WorkerRevision,
    string FinalHashHex, CanonicalPhysicalAction[] Actions,
    CanonicalPhysicalWorkerSaveRow[] Workers,
    CanonicalPhysicalStockSaveRow[] Stocks,
    CanonicalPhysicalCycleSaveRow[] Cycles);

public sealed record CanonicalPhysicalSaveEnvelope(
    int Version, string PayloadJson, string PayloadSha256Hex);

/// <summary>
/// GA-VS10: canonical physical workforce, local stocks, and six-movement
/// cycle are all resolved from immutable WorldState snapshots. The old
/// offline session is used only transiently to seed the original eight
/// workers or to verify an explicitly requested legacy-save migration.
/// </summary>
public sealed class CanonicalPhysicalTacticalSession
{
    public const int SaveFormat = 9;
    public const int MaximumActions = 100_000;
    private const int MaximumSaveBytes = 4_000_000;
    private const ulong FoodDemandRawPerWorker = 500_000UL;

    private readonly List<CanonicalPhysicalAction> _journal = new();
    private readonly GoldbergParameters _coarse;
    private readonly GoldbergParameters _fine;
    private readonly ulong _seed;
    private readonly int _generatorVersion;
    private readonly StrategicPrimitiveResourcePotentialFieldSet _potentials;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly string _initialHashHex;
    private readonly CanonicalWorldStateHasher _hasher = new();

    public CanonicalGameSession Game { get; }
    public WorldGenerationResult FineWorld { get; }
    public PhysicalTacticalTileId InitialTile { get; }
    public PhysicalTacticalEconomyRuntimeState State =>
        Game.Snapshot.PhysicalEconomy ??
        throw new InvalidOperationException("Physical economy has not been initialized.");
    public IReadOnlyList<CanonicalPhysicalAction> Events =>
        Array.AsReadOnly(_journal.ToArray());
    public WorldStateHash CurrentHash => _hasher.Compute(Game.Snapshot);

    public CanonicalPhysicalTacticalSession(
        CanonicalGameSession game, GoldbergParameters fine)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        var request = Game.GeneratedWorld.Request;
        _seed = request.Seed.Value;
        _generatorVersion = request.Version.Value;
        _coarse = request.StrategicParameters;
        _fine = fine;
        _ = new GoldbergScaledRefinement(_coarse, _fine);
        if (Game.Snapshot.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(Game.GeneratedWorld))
            throw new InvalidOperationException("Canonical world signature mismatch.");

        // This object is deliberately not retained: it only supplies the
        // validated VS05 initial allocation and exact generated fine world.
        var initial = new EconomicPhysicalTacticalOfflineSession(_seed, _coarse, _fine);
        if (initial.Workers.CoarseWorld.Request.Version != request.Version ||
            RuntimeWorldBinding.FromGeneratedWorld(initial.Workers.CoarseWorld) !=
            Game.Snapshot.WorldBinding)
            throw new InvalidOperationException("Worker seed/topology mismatches canonical world.");

        FineWorld = initial.Workers.FineWorld;
        InitialTile = initial.Workers.InitialTile;
        _incidence = initial.Workers.Incidence;
        _potentials = StrategicPrimitiveResourcePotentialFieldSet.Derive(FineWorld);

        if (Game.Snapshot.PhysicalEconomy is null)
        {
            if (Game.Snapshot.Revision != 0UL ||
                Game.Snapshot.Economy.EconomicPoints.Count != 0 ||
                Game.Snapshot.Population.IsInitialized)
                throw new InvalidOperationException(
                    "Physical initialization requires a new empty canonical game.");

            var next = Game.Snapshot
                .WithPhysicalEconomy(initial.BuildPhysicalRuntimeSnapshot())
                .AdvanceRevision();
            Game.AcceptSnapshot(next);
        }
        else if (State.FineParameters != _fine ||
                 State.CoarseParameters != _coarse ||
                 State.CoarseWorldBinding != Game.Snapshot.WorldBinding)
            throw new InvalidOperationException("Attached physical snapshot has foreign topology.");

        _initialHashHex = _hasher.Compute(Game.Snapshot).HexDigest;
    }

    public static CanonicalPhysicalTacticalSession Start(
        ulong seed = 20261012UL,
        GoldbergParameters? coarse = null,
        GoldbergParameters? fine = null)
    {
        var coarseParameters = coarse ?? new GoldbergParameters(3, 0);
        var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial, coarseParameters));
        return new CanonicalPhysicalTacticalSession(
            canonical, fine ?? new GoldbergParameters(18, 0));
    }

    public ulong WorkersAt(PhysicalTacticalTileId tile)
    {
        ValidateTile(tile);
        return State.Allocations.Where(x => x.TileId == tile)
            .Aggregate(0UL, (sum, x) => checked(sum + x.Workers));
    }

    public ulong FoodDemandAt(PhysicalTacticalTileId tile) =>
        checked(WorkersAt(tile) * FoodDemandRawPerWorker);

    public ulong StockAt(PhysicalTacticalTileId tile, CommodityId commodity)
    {
        ValidateTile(tile);
        if (!commodity.IsValid) throw new ArgumentException("Invalid commodity.");
        return State.Stocks.FirstOrDefault(
            x => x.TileId == tile && x.Commodity == commodity)?.RawQuantity ?? 0UL;
    }

    public StrategicCellId DisplayBucket(PhysicalTacticalTileId tile)
    {
        ValidateTile(tile);
        return _incidence.TileIncidences[
            checked((int)tile.FineStrategicCellId.Value - 1)]
            .IncidentCoarseCellIds[0];
    }

    public IReadOnlyList<PhysicalTacticalTileId> AdjacentWorkable(
        PhysicalTacticalTileId source)
    {
        ValidateTile(source);
        return Array.AsReadOnly(FineWorld.StrategicTopology.Cells[
                checked((int)source.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds.Where(IsWorkableLand)
            .OrderBy(id => id.Value)
            .Select(id => new PhysicalTacticalTileId(_fine, id)).ToArray());
    }

    public void MoveWorkers(PhysicalTacticalTileId source,
        PhysicalTacticalTileId target, EconomicActivityKind fromJob,
        EconomicActivityKind toJob, ulong amount)
    {
        EnsureJournalCapacity();
        ValidateTile(source);
        ValidateTile(target);
        ValidateJob(fromJob);
        ValidateJob(toJob);
        if (source == target || amount == 0UL ||
            !IsWorkableLand(target.FineStrategicCellId) ||
            !AdjacentWorkable(source).Contains(target))
            throw new InvalidOperationException("Illegal physical worker relocation.");

        var current = State;
        var nextAllocations = current.Allocations.ToDictionary(
            x => (x.TileId, x.Activity), x => x.Workers);
        var sourceKey = (source, fromJob);
        var targetKey = (target, toJob);
        if (!nextAllocations.TryGetValue(sourceKey, out var available) ||
            available < amount)
            throw new InvalidOperationException("Insufficient source workers.");

        nextAllocations.TryGetValue(targetKey, out var existing);
        var targetWorkers = checked(existing + amount);
        if (available == amount) nextAllocations.Remove(sourceKey);
        else nextAllocations[sourceKey] = available - amount;
        nextAllocations[targetKey] = targetWorkers;

        var next = new PhysicalTacticalEconomyRuntimeState(
            current.CoarseWorldBinding, _coarse, _fine, current.MovementTurn,
            checked(current.WorkerRevision + 1UL),
            nextAllocations.Select(x => new PhysicalWorkerAllocation(
                x.Key.Item1, x.Key.Item2, x.Value)),
            current.Stocks, current.Cycles);
        if (next.TotalWorkers != current.TotalWorkers)
            throw new InvalidOperationException("Worker conservation failed.");

        Commit(next, "move", source.FineStrategicCellId.Value,
            target.FineStrategicCellId.Value, (byte)fromJob, (byte)toJob, amount);
    }

    /// <summary>
    /// Only every sixth movement tick settles physical production and food
    /// consumption. No stock crosses a tile boundary without transport.
    /// </summary>
    public PhysicalEconomicCycle? AdvanceMovementTurn()
    {
        EnsureJournalCapacity();
        var current = State;
        var turn = checked(current.MovementTurn + 1UL);
        PhysicalEconomicCycle? cycle = null;
        var stocks = current.Stocks.ToDictionary(
            x => (x.TileId, x.Commodity), x => x.RawQuantity);
        var cycles = current.Cycles.ToList();

        if (turn % PhysicalTacticalEconomyRuntimeState.MovementTicksPerEconomicCycle == 0UL)
        {
            ulong produced = 0UL;
            ulong consumed = 0UL;
            ulong unmet = 0UL;

            foreach (var allocation in current.Allocations)
            {
                var (commodity, rawRate) = allocation.Activity switch
                {
                    EconomicActivityKind.Agriculture => (
                        new CommodityId(1U),
                        _potentials.Agriculture.GetRawValue(allocation.TileId.FineStrategicCellId)),
                    EconomicActivityKind.Forestry => (
                        new CommodityId(3U),
                        _potentials.WoodGathering.GetRawValue(allocation.TileId.FineStrategicCellId)),
                    EconomicActivityKind.Mining => (
                        new CommodityId(2U),
                        _potentials.SurfaceStone.GetRawValue(allocation.TileId.FineStrategicCellId)),
                    _ => throw new InvalidOperationException("Unsupported productive activity.")
                };
                if (rawRate <= 0L) continue;
                var output = checked(allocation.Workers * (ulong)rawRate);
                var key = (allocation.TileId, commodity);
                stocks.TryGetValue(key, out var existing);
                stocks[key] = checked(existing + output);
                produced = checked(produced + output);
            }

            foreach (var group in current.Allocations.GroupBy(x => x.TileId))
            {
                var demand = checked(group.Aggregate(0UL,
                    (sum, x) => checked(sum + x.Workers)) * FoodDemandRawPerWorker);
                var key = (group.Key, new CommodityId(1U));
                stocks.TryGetValue(key, out var available);
                var delivered = Math.Min(available, demand);
                if (delivered != 0UL) stocks[key] = available - delivered;
                consumed = checked(consumed + delivered);
                unmet = checked(unmet + (demand - delivered));
            }

            cycle = new PhysicalEconomicCycle(turn, produced, consumed, unmet);
            cycles.Add(cycle);
        }

        var next = new PhysicalTacticalEconomyRuntimeState(
            current.CoarseWorldBinding, _coarse, _fine, turn, current.WorkerRevision,
            current.Allocations,
            stocks.Select(x => new PhysicalCommodityStock(
                x.Key.Item1, x.Key.Item2, x.Value)),
            cycles);
        Commit(next, "tick", 0UL, 0UL, 0, 0, 0UL);
        return cycle;
    }

    private void Commit(PhysicalTacticalEconomyRuntimeState next,
        string kind, ulong source, ulong destination, byte fromJob,
        byte toJob, ulong amount)
    {
        var before = Game.Snapshot;
        var beforeHash = _hasher.Compute(before);
        var staged = before.WithPhysicalEconomy(next).AdvanceRevision();
        var afterHash = _hasher.Compute(staged);
        var action = new CanonicalPhysicalAction(kind, source, destination,
            fromJob, toJob, amount, before.Revision, beforeHash.HexDigest,
            staged.Revision, afterHash.HexDigest);
        Game.AcceptSnapshot(staged);
        _journal.Add(action);
    }

    public string ExportSaveJson()
    {
        var payloadJson = JsonSerializer.Serialize(BuildPayload());
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var json = JsonSerializer.Serialize(new CanonicalPhysicalSaveEnvelope(
            SaveFormat, payloadJson, digest));
        if (Encoding.UTF8.GetByteCount(json) > MaximumSaveBytes)
            throw new InvalidOperationException("Canonical save exceeds size limit.");
        return json;
    }

    public static CanonicalPhysicalTacticalSession RestoreSaveJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumSaveBytes)
            throw new InvalidOperationException("Canonical save exceeds size limit.");

        CanonicalPhysicalSaveEnvelope envelope;
        CanonicalPhysicalSavePayload payload;
        try
        {
            envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)
                ?? throw new InvalidOperationException("Missing canonical save envelope.");
            if (envelope.Version != SaveFormat ||
                string.IsNullOrEmpty(envelope.PayloadJson) ||
                string.IsNullOrEmpty(envelope.PayloadSha256Hex))
                throw new InvalidOperationException("Unsupported canonical save envelope.");
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson));
            if (envelope.PayloadSha256Hex.Length != 64 ||
                !CryptographicOperations.FixedTimeEquals(
                    digest, Convert.FromHexString(envelope.PayloadSha256Hex)))
                throw new InvalidOperationException("Canonical save checksum mismatch.");
            payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(envelope.PayloadJson)
                ?? throw new InvalidOperationException("Missing canonical save payload.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("Invalid canonical save encoding.", ex);
        }

        if (payload.Version != SaveFormat ||
            payload.Actions is null || payload.Workers is null ||
            payload.Stocks is null || payload.Cycles is null ||
            payload.Actions.Length > MaximumActions ||
            payload.GeneratorVersion != WorldGenerationVersion.Initial.Value)
            throw new InvalidOperationException("Unsupported canonical save payload.");

        var game = Start(payload.Seed,
            new GoldbergParameters(payload.CoarseM, payload.CoarseN),
            new GoldbergParameters(payload.FineM, payload.FineN));
        if (payload.WorldBindingHex != game.Game.Snapshot.WorldBinding!.WorldSignatureSha256Hex ||
            payload.InitialHashHex != game._initialHashHex)
            throw new InvalidOperationException("Canonical save world identity mismatch.");

        foreach (var action in payload.Actions)
        {
            if (action is null)
                throw new InvalidOperationException("Null canonical action.");
            if (game.Game.Snapshot.Revision != action.BeforeRevision ||
                game.CurrentHash.HexDigest != action.BeforeHashHex)
                throw new InvalidOperationException("Canonical event previous hash mismatch.");

            if (action.Kind == "move")
                game.MoveWorkers(
                    new PhysicalTacticalTileId(game._fine, new StrategicCellId(action.Source)),
                    new PhysicalTacticalTileId(game._fine, new StrategicCellId(action.Destination)),
                    (EconomicActivityKind)action.FromJob,
                    (EconomicActivityKind)action.ToJob, action.Workers);
            else if (action.Kind == "tick" && action.Source == 0UL &&
                     action.Destination == 0UL && action.FromJob == 0 &&
                     action.ToJob == 0 && action.Workers == 0UL)
                game.AdvanceMovementTurn();
            else
                throw new InvalidOperationException("Unsupported canonical action.");

            if (game._journal[^1] != action)
                throw new InvalidOperationException("Canonical event replay diverged.");
        }

        // Reject even a self-consistently rehashed, fabricated summary:
        // all values must be reproducible from the validated world and orders.
        if (JsonSerializer.Serialize(payload) !=
            JsonSerializer.Serialize(game.BuildPayload()))
            throw new InvalidOperationException("Canonical save replay state diverged.");
        return game;
    }

    /// <summary>
    /// Explicit one-time migration path. Does not write/overwrite the legacy
    /// save: caller chooses if/when to export the new canonical format.
    /// </summary>
    public static CanonicalPhysicalTacticalSession MigrateVs07SaveJson(string json)
    {
        var legacy = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(json);
        var envelope = JsonSerializer.Deserialize<TacticalOfflineEnvelope>(json)
            ?? throw new InvalidOperationException("Missing legacy save envelope.");
        var payload = JsonSerializer.Deserialize<TacticalOfflinePayload>(envelope.PayloadJson)
            ?? throw new InvalidOperationException("Missing legacy payload.");
        if (payload.Version != EconomicPhysicalTacticalOfflineSession.SaveFormat)
            throw new InvalidOperationException("Unsupported VS07 version.");

        var game = Start(payload.Seed,
            new GoldbergParameters(payload.CoarseM, payload.CoarseN),
            new GoldbergParameters(payload.FineM, payload.FineN));
        foreach (var action in payload.Actions)
        {
            if (action.Kind == "move")
                game.MoveWorkers(
                    new PhysicalTacticalTileId(game._fine, new StrategicCellId(action.Source)),
                    new PhysicalTacticalTileId(game._fine, new StrategicCellId(action.Destination)),
                    (EconomicActivityKind)action.FromJob, (EconomicActivityKind)action.ToJob,
                    action.Workers);
            else if (action.Kind == "tick")
                game.AdvanceMovementTurn();
            else
                throw new InvalidOperationException("Unsupported legacy event.");
        }

        var actual = game.State;
        if (actual.MovementTurn != legacy.MovementTurn ||
            actual.WorkerRevision != legacy.Workers.Revision ||
            actual.TotalWorkers != legacy.Workers.TotalWorkers ||
            !actual.Allocations.SequenceEqual(legacy.Workers.Assignments.Select(
                x => new PhysicalWorkerAllocation(x.TileId, x.Activity, x.Workers))) ||
            !actual.Stocks.SequenceEqual(legacy.Stocks.Select(
                x => new PhysicalCommodityStock(x.TileId, x.Commodity, x.RawQuantity))) ||
            !actual.Cycles.SequenceEqual(legacy.Cycles.Select(x =>
                new PhysicalEconomicCycle(x.MovementTurn, x.ProducedRaw,
                    x.FoodConsumedRaw, x.FoodUnmetRaw))))
            throw new InvalidOperationException("VS07 migration diverged from legacy replay.");
        return game;
    }

    private CanonicalPhysicalSavePayload BuildPayload()
    {
        var state = State;
        return new CanonicalPhysicalSavePayload(
            SaveFormat, _seed, _generatorVersion, _coarse.M, _coarse.N, _fine.M, _fine.N,
            Game.Snapshot.WorldBinding!.WorldSignatureSha256Hex, _initialHashHex,
            Game.Snapshot.Revision, state.MovementTurn, state.WorkerRevision,
            CurrentHash.HexDigest, _journal.ToArray(),
            state.Allocations.Select(x => new CanonicalPhysicalWorkerSaveRow(
                x.TileId.FineStrategicCellId.Value, (byte)x.Activity, x.Workers)).ToArray(),
            state.Stocks.Select(x => new CanonicalPhysicalStockSaveRow(
                x.TileId.FineStrategicCellId.Value, x.Commodity.Value,
                x.RawQuantity)).ToArray(),
            state.Cycles.Select(x => new CanonicalPhysicalCycleSaveRow(
                x.MovementTurn, x.ProducedRaw, x.FoodConsumedRaw, x.FoodUnmetRaw)).ToArray());
    }

    private bool IsWorkableLand(StrategicCellId id) =>
        FineWorld.StrategicPhysicalFields.LandWater.GetKind(id) ==
            StrategicLandWaterKind.Land &&
        FineWorld.StrategicBiomes.GetKind(id) != StrategicBiomeKind.PolarIce;

    private void ValidateTile(PhysicalTacticalTileId tile)
    {
        if (!tile.IsValid || tile.FineGoldbergParameters != _fine ||
            tile.FineStrategicCellId.Value > (ulong)FineWorld.StrategicTopology.Cells.Count)
            throw new ArgumentException("Physical tile belongs to a different world.");
    }

    private static void ValidateJob(EconomicActivityKind job)
    {
        if (job is not (EconomicActivityKind.Agriculture or
            EconomicActivityKind.Forestry or EconomicActivityKind.Mining))
            throw new ArgumentOutOfRangeException(nameof(job));
    }

    private void EnsureJournalCapacity()
    {
        if (_journal.Count >= MaximumActions)
            throw new InvalidOperationException("Canonical action journal is full.");
    }
}
