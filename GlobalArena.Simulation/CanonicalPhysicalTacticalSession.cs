using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed record CanonicalFoodCashAccountSaveRow(ulong Tile, ulong Household, ulong Producer);
public sealed record CanonicalFoodCashReceiptSaveRow(ulong Turn, ulong Tile, ulong FoodRaw, ulong Price, ulong Paid);
public sealed record CanonicalFoodCashSave(
    ulong InitialMoneySupply, CanonicalFoodCashAccountSaveRow[] Accounts,
    CanonicalFoodCashReceiptSaveRow[] Receipts);

public sealed record CanonicalPhysicalSavePayload(
    int Version, ulong Seed, int GeneratorVersion,
    int CoarseM, int CoarseN, int FineM, int FineN,
    string WorldBindingHex, string InitialHashHex,
    ulong Revision, ulong MovementTurn, ulong WorkerRevision,
    string FinalHashHex, CanonicalPhysicalAction[] Actions,
    CanonicalPhysicalWorkerSaveRow[] Workers,
    CanonicalPhysicalStockSaveRow[] Stocks,
    CanonicalPhysicalCycleSaveRow[] Cycles,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CanonicalFoodCashSave? FoodCash = null);

public sealed record CanonicalPhysicalSaveEnvelope(
    int Version, string PayloadJson, string PayloadSha256Hex);

/// <summary>
/// GA-VS11: canonical physical workforce, local stocks, and versioned
/// fixed/adaptive economic cadences resolve from immutable WorldState snapshots. The old
/// offline session is used only transiently to seed the original eight
/// workers or to verify an explicitly requested legacy-save migration.
/// </summary>
public sealed class CanonicalPhysicalTacticalSession
{
    // V9 files retain the historical fixed-six cadence.
    public const int SaveFormat = 9;
    public const int AdaptiveSaveFormat = 10;
    public const int GeographyAlignedSaveFormat = 11;
    public const int CashFoodSaveFormat = 12;
    public const int MaximumActions = 100_000;
    private const int MaximumSaveBytes = 4_000_000;
    private const ulong FoodDemandRawPerWorker = 500_000UL;

    private readonly List<CanonicalPhysicalAction> _journal = new();
    private readonly GoldbergParameters _coarse;
    private readonly GoldbergParameters _fine;
    private readonly ulong _seed;
    private readonly int _generatorVersion;
    private readonly PhysicalEconomicCadenceMode _cadenceMode;
    private readonly PhysicalTacticalGeographyMode _geographyMode;
    private readonly PhysicalFoodMarketMode _marketMode;
    private readonly CanonicalTacticalPhysicalGeography? _canonicalGeography;
    private readonly StrategicPrimitiveResourcePotentialFieldSet _potentials;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly string _initialHashHex;
    private readonly CanonicalWorldStateHasher _hasher = new();
    private WorldState _lastCommittedSnapshot;

    private WorldState CheckedSnapshot
    {
        get
        {
            var live = Game.Snapshot;
            if (!ReferenceEquals(live, _lastCommittedSnapshot))
                throw new InvalidOperationException(
                    "Canonical physical session lost authority: snapshot was changed externally.");
            return live;
        }
    }

    public CanonicalGameSession Game { get; }
    public PhysicalEconomicCadenceMode CadenceMode => _cadenceMode;
    public PhysicalTacticalGeographyMode GeographyMode => _geographyMode;
    public PhysicalFoodMarketMode MarketMode => _marketMode;
    public PhysicalFoodCashMarketState? FoodCash => State.FoodCash;
    public WorldGenerationV2PhysicalComposition? CanonicalTerrain => _canonicalGeography?.Terrain;
    public bool IsPhysicalWorkableLand(StrategicCellId tile) => IsWorkableLand(tile);
    public ulong NextEconomicInterval => PhysicalEconomicCadence.IntervalForTurn(
        checked(State.MovementTurn + 1UL), _cadenceMode);
    public WorldGenerationResult FineWorld { get; }
    public PhysicalTacticalTileId InitialTile { get; }
    public PhysicalTacticalEconomyRuntimeState State =>
        CheckedSnapshot.PhysicalEconomy ??
        throw new InvalidOperationException("Physical economy has not been initialized.");
    public IReadOnlyList<CanonicalPhysicalAction> Events =>
        Array.AsReadOnly(_journal.ToArray());
    public WorldStateHash CurrentHash => _hasher.Compute(CheckedSnapshot);

    public CanonicalPhysicalTacticalSession(
        CanonicalGameSession game, GoldbergParameters fine,
        PhysicalEconomicCadenceMode cadenceMode = PhysicalEconomicCadenceMode.LegacyFixedSix,
        PhysicalTacticalGeographyMode geographyMode = PhysicalTacticalGeographyMode.LegacyV1,
        PhysicalFoodMarketMode marketMode = PhysicalFoodMarketMode.LegacyDirectConsumption)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        var request = Game.GeneratedWorld.Request;
        _seed = request.Seed.Value;
        _generatorVersion = request.Version.Value;
        _coarse = request.StrategicParameters;
        _fine = fine;
        _ = PhysicalEconomicCadence.IntervalForTurn(1UL, cadenceMode);
        _cadenceMode = cadenceMode;
        if (geographyMode is not (PhysicalTacticalGeographyMode.LegacyV1 or PhysicalTacticalGeographyMode.Canonical01C))
            throw new ArgumentOutOfRangeException(nameof(geographyMode));
        if (geographyMode == PhysicalTacticalGeographyMode.Canonical01C &&
            cadenceMode != PhysicalEconomicCadenceMode.AdaptiveThirty)
            throw new ArgumentException("01C canonical geography requires adaptive economic cadence.");
        _geographyMode = geographyMode;
        if (marketMode is not (PhysicalFoodMarketMode.LegacyDirectConsumption or
            PhysicalFoodMarketMode.LocalCashSettlement) ||
            (marketMode == PhysicalFoodMarketMode.LocalCashSettlement &&
             (geographyMode != PhysicalTacticalGeographyMode.Canonical01C ||
              cadenceMode != PhysicalEconomicCadenceMode.AdaptiveThirty)))
            throw new ArgumentException("Cash market requires canonical 01C geography and adaptive cadence.");
        _marketMode = marketMode;
        _ = new GoldbergScaledRefinement(_coarse, _fine);
        if (Game.Snapshot.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(Game.GeneratedWorld))
            throw new InvalidOperationException("Canonical world signature mismatch.");
        _lastCommittedSnapshot = Game.Snapshot;

        // This object is deliberately not retained: it only supplies the
        // validated VS05 initial allocation and exact generated fine world.
        var initial = new EconomicPhysicalTacticalOfflineSession(_seed, _coarse, _fine);
        if (initial.Workers.CoarseWorld.Request.Version != request.Version ||
            RuntimeWorldBinding.FromGeneratedWorld(initial.Workers.CoarseWorld) !=
            Game.Snapshot.WorldBinding)
            throw new InvalidOperationException("Worker seed/topology mismatches canonical world.");

        FineWorld = initial.Workers.FineWorld;
        _canonicalGeography = _geographyMode == PhysicalTacticalGeographyMode.Canonical01C
            ? new CanonicalTacticalPhysicalGeography(FineWorld, _seed) : null;
        InitialTile = _canonicalGeography is null
            ? initial.Workers.InitialTile
            : new PhysicalTacticalTileId(_fine, _canonicalGeography.InitialWorkableTile);
        _incidence = initial.Workers.Incidence;
        _potentials = StrategicPrimitiveResourcePotentialFieldSet.Derive(FineWorld);

        if (Game.Snapshot.PhysicalEconomy is null)
        {
            if (Game.Snapshot.Revision != 0UL ||
                Game.Snapshot.Economy.EconomicPoints.Count != 0 ||
                Game.Snapshot.Population.IsInitialized)
                throw new InvalidOperationException(
                    "Physical initialization requires a new empty canonical game.");

            var seeded = initial.BuildPhysicalRuntimeSnapshot();
            var startingAllocations = _canonicalGeography is null
                ? seeded.Allocations
                : new[]
                {
                    new PhysicalWorkerAllocation(InitialTile, EconomicActivityKind.Agriculture, 4UL),
                    new PhysicalWorkerAllocation(InitialTile, EconomicActivityKind.Forestry, 2UL),
                    new PhysicalWorkerAllocation(InitialTile, EconomicActivityKind.Mining, 2UL)
                };
            PhysicalFoodCashMarketState? initialCash = null;
            if (_marketMode == PhysicalFoodMarketMode.LocalCashSettlement)
            {
                var initialMoney = checked(seeded.TotalWorkers *
                    PhysicalFoodCashMarketState.InitialMicrocreditsPerWorker);
                initialCash = new PhysicalFoodCashMarketState(
                    initialMoney,
                    startingAllocations.GroupBy(x => x.TileId).Select(g =>
                        new PhysicalFoodCashAccount(g.Key,
                            checked(g.Aggregate(0UL,
                                (total, x) => checked(total + x.Workers)) *
                                PhysicalFoodCashMarketState.InitialMicrocreditsPerWorker), 0UL)),
                    Array.Empty<PhysicalFoodCashReceipt>(), _fine, 0UL);
            }
            var initialPhysical = new PhysicalTacticalEconomyRuntimeState(
                seeded.CoarseWorldBinding, seeded.CoarseParameters,
                seeded.FineParameters, seeded.MovementTurn,
                seeded.WorkerRevision, startingAllocations, seeded.Stocks,
                seeded.Cycles, _cadenceMode, _geographyMode, initialCash);
            var next = Game.Snapshot
                .WithPhysicalEconomy(initialPhysical)
                .AdvanceRevision();
            Game.AcceptSnapshotIfCurrent(_lastCommittedSnapshot, next);
            _lastCommittedSnapshot = next;
        }
        else
        {
            var baseline = initial.BuildPhysicalRuntimeSnapshot();
            var expectedAllocations = _canonicalGeography is null
                ? baseline.Allocations
                : new[]
                {
                    new PhysicalWorkerAllocation(InitialTile, EconomicActivityKind.Agriculture, 4UL),
                    new PhysicalWorkerAllocation(InitialTile, EconomicActivityKind.Forestry, 2UL),
                    new PhysicalWorkerAllocation(InitialTile, EconomicActivityKind.Mining, 2UL)
                };
            if (State.FineParameters != _fine ||
                State.GeographyMode != _geographyMode ||
                State.MarketMode != _marketMode ||
                State.CadenceMode != _cadenceMode ||
                State.CoarseParameters != _coarse ||
                State.CoarseWorldBinding != Game.Snapshot.WorldBinding)
                throw new InvalidOperationException("Attached physical snapshot has foreign topology.");
            // Creating another wrapper around an already progressed session
            // would lose its event journal and make an unreplayable V9 save.
            if (Game.Snapshot.Revision != 1UL ||
                State.MovementTurn != 0UL || State.WorkerRevision != 0UL ||
                State.Stocks.Count != 0 || State.Cycles.Count != 0 ||
                !State.Allocations.SequenceEqual(expectedAllocations
                    .OrderBy(x => x.TileId.FineStrategicCellId.Value)
                    .ThenBy(x => (byte)x.Activity)))
                throw new InvalidOperationException(
                    "Existing progressed physical game must be reopened with its canonical event journal.");
        }

        _initialHashHex = _hasher.Compute(Game.Snapshot).HexDigest;
    }

    public static CanonicalPhysicalTacticalSession Start(
        ulong seed = 20261012UL,
        GoldbergParameters? coarse = null,
        GoldbergParameters? fine = null,
        PhysicalEconomicCadenceMode cadenceMode = PhysicalEconomicCadenceMode.LegacyFixedSix,
        PhysicalTacticalGeographyMode geographyMode = PhysicalTacticalGeographyMode.LegacyV1,
        PhysicalFoodMarketMode marketMode = PhysicalFoodMarketMode.LegacyDirectConsumption)
    {
        var coarseParameters = coarse ?? new GoldbergParameters(3, 0);
        var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial, coarseParameters));
        return new CanonicalPhysicalTacticalSession(
            canonical, fine ?? new GoldbergParameters(18, 0), cadenceMode, geographyMode, marketMode);
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

    /// <summary>
    /// Preview the next local food-market opportunity using the current physical
    /// workforce, local food stock and geography. This NEVER commits prices,
    /// purchases or monetary postings. The reference budget is caller supplied.
    /// </summary>
    public EconomicLocalFoodMarketQuote QuoteNextLocalFoodMarket(
        PhysicalTacticalTileId tile, double referenceBudgetPerWorker)
    {
        ValidateTile(tile);
        var state = State;
        var workers = WorkersAt(tile);
        ulong projectedCrop = 0UL;
        foreach (var allocation in state.Allocations.Where(x =>
                     x.TileId == tile &&
                     x.Activity == EconomicActivityKind.Agriculture))
        {
            var rate = PrimitiveYield(tile.FineStrategicCellId,
                EconomicActivityKind.Agriculture);
            if (rate > 0L)
                projectedCrop = checked(projectedCrop +
                    checked(allocation.Workers * (ulong)rate));
        }

        return new EconomicLocalFoodMarketQuoteResolver().Resolve(
            tile, state.MovementTurn, workers,
            StockAt(tile, new CommodityId(1U)), projectedCrop,
            FoodDemandAt(tile), referenceBudgetPerWorker);
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

        PhysicalFoodCashMarketState? nextCash = current.FoodCash;
        if (current.FoodCash is { } oldCash)
        {
            // A move carries a proportionate share of local household cash.
            // No money is minted or credited from a future economic cycle.
            var wallets = oldCash.Accounts.ToDictionary(x => x.Tile, x => x);
            var src = oldCash.At(source);
            var dst = oldCash.At(target);
            var totalSourceWorkers = WorkersAt(source);
            var transferred = (ulong)((UInt128)src.HouseholdMicrocredits *
                amount / totalSourceWorkers);
            wallets[source] = src with
            {
                HouseholdMicrocredits = src.HouseholdMicrocredits - transferred
            };
            wallets[target] = dst with
            {
                HouseholdMicrocredits =
                    checked(dst.HouseholdMicrocredits + transferred)
            };
            nextCash = new PhysicalFoodCashMarketState(
                oldCash.InitialMoneySupplyMicrocredits,
                wallets.Values, oldCash.Receipts, _fine, current.MovementTurn);
        }

        var next = new PhysicalTacticalEconomyRuntimeState(
            current.CoarseWorldBinding, _coarse, _fine, current.MovementTurn,
            checked(current.WorkerRevision + 1UL),
            nextAllocations.Select(x => new PhysicalWorkerAllocation(
                x.Key.Item1, x.Key.Item2, x.Value)),
            current.Stocks, current.Cycles, _cadenceMode, _geographyMode, nextCash);
        if (next.TotalWorkers != current.TotalWorkers)
            throw new InvalidOperationException("Worker conservation failed.");

        Commit(next, "move", source.FineStrategicCellId.Value,
            target.FineStrategicCellId.Value, (byte)fromJob, (byte)toJob, amount);
    }

    /// <summary>
    /// Fixed-six V9 and progressive-thirty V10 settle physical production
    /// and consumption under their versioned rules. No intertile teleportation.
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
        var cashAccounts = current.FoodCash?.Accounts.ToDictionary(x => x.Tile, x => x);
        var cashReceipts = current.FoodCash?.Receipts.ToList();

        var lastSettlement = cycles.Count == 0 ? 0UL : cycles[^1].MovementTurn;
        if (PhysicalEconomicCadence.IsSettlementTurn(turn, lastSettlement, _cadenceMode))
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
                        PrimitiveYield(allocation.TileId.FineStrategicCellId, EconomicActivityKind.Agriculture)),
                    EconomicActivityKind.Forestry => (
                        new CommodityId(3U),
                        PrimitiveYield(allocation.TileId.FineStrategicCellId, EconomicActivityKind.Forestry)),
                    EconomicActivityKind.Mining => (
                        new CommodityId(2U),
                        PrimitiveYield(allocation.TileId.FineStrategicCellId, EconomicActivityKind.Mining)),
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
                ulong delivered;
                if (cashAccounts is null || cashReceipts is null)
                {
                    // V9/V10/V11: preserve legacy direct-food-consumption bytes.
                    delivered = Math.Min(available, demand);
                }
                else
                {
                    // V12: household demand must buy food from the local
                    // producer. The solver handles price, while all physical
                    // and monetary postings are committed in integer units.
                    var account = cashAccounts.GetValueOrDefault(group.Key) ??
                        new PhysicalFoodCashAccount(group.Key, 0UL, 0UL);
                    var residents = group.Aggregate(0UL,
                        (sum, x) => checked(sum + x.Workers));
                    var budget = account.HouseholdMicrocredits /
                        (double)PhysicalFoodCashMarketState.MonetaryScale;
                    delivered = 0UL;
                    if (available > 0UL && account.HouseholdMicrocredits > 0UL)
                    {
                        var quote = new EconomicLocalFoodMarketQuoteResolver().Resolve(
                            group.Key, turn, residents, available, 0UL,
                            demand, budget / residents);
                        var desiredRaw = Math.Floor(quote.ReferenceSold *
                            PhysicalFoodCashMarketState.MonetaryScale);
                        if (!double.IsFinite(desiredRaw) || desiredRaw < 0.0 ||
                            desiredRaw > ulong.MaxValue)
                            throw new InvalidOperationException(
                                "Food quote cannot be converted to fixed-point goods.");
                        delivered = Math.Min(Math.Min((ulong)desiredRaw, available), demand);
                        var rawPrice = quote.PricePerWholeFoodUnit *
                            PhysicalFoodCashMarketState.MonetaryScale;
                        if (!double.IsFinite(rawPrice) || rawPrice < 0.0 ||
                            rawPrice > ulong.MaxValue)
                            throw new InvalidOperationException(
                                "Food price cannot be converted to fixed-point money.");
                        var price = checked((ulong)Math.Round(rawPrice,
                            MidpointRounding.ToEven));
                        // One microcredit per millionth of whole food: the
                        // product of price in microcredits/unit and sold raw
                        // food units is divided by the physical scale.
                        var paid = (ulong)((UInt128)price * delivered /
                            PhysicalFoodCashMarketState.MonetaryScale);
                        if (paid > account.HouseholdMicrocredits)
                            throw new InvalidOperationException("Food purchase exceeds available household cash.");
                        if (delivered > 0UL)
                        {
                            cashAccounts[group.Key] = account with
                            {
                                HouseholdMicrocredits = account.HouseholdMicrocredits - paid,
                                ProducerMicrocredits = checked(account.ProducerMicrocredits + paid)
                            };
                            cashReceipts.Add(new PhysicalFoodCashReceipt(
                                turn, group.Key, delivered, price, paid));
                        }
                    }
                }
                if (delivered != 0UL) stocks[key] = available - delivered;
                consumed = checked(consumed + delivered);
                unmet = checked(unmet + (demand - delivered));
            }

            cycle = new PhysicalEconomicCycle(turn, produced, consumed, unmet);
            cycles.Add(cycle);
        }

        PhysicalFoodCashMarketState? cash = current.FoodCash;
        if (cashAccounts is not null && cashReceipts is not null)
            cash = new PhysicalFoodCashMarketState(
                current.FoodCash!.InitialMoneySupplyMicrocredits,
                cashAccounts.Values, cashReceipts, _fine, turn);

        var next = new PhysicalTacticalEconomyRuntimeState(
            current.CoarseWorldBinding, _coarse, _fine, turn, current.WorkerRevision,
            current.Allocations,
            stocks.Select(x => new PhysicalCommodityStock(
                x.Key.Item1, x.Key.Item2, x.Value)),
            cycles, _cadenceMode, _geographyMode, cash);
        Commit(next, "tick", 0UL, 0UL, 0, 0, 0UL);
        return cycle;
    }

    private void Commit(PhysicalTacticalEconomyRuntimeState next,
        string kind, ulong source, ulong destination, byte fromJob,
        byte toJob, ulong amount)
    {
        var before = CheckedSnapshot;
        var beforeHash = _hasher.Compute(before);
        var staged = before.WithPhysicalEconomy(next).AdvanceRevision();
        var afterHash = _hasher.Compute(staged);
        var action = new CanonicalPhysicalAction(kind, source, destination,
            fromJob, toJob, amount, before.Revision, beforeHash.HexDigest,
            staged.Revision, afterHash.HexDigest);
        Game.AcceptSnapshotIfCurrent(before, staged);
        _lastCommittedSnapshot = staged;
        _journal.Add(action);
    }

    public string ExportSaveJson()
    {
        var payloadJson = JsonSerializer.Serialize(BuildPayload());
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var json = JsonSerializer.Serialize(new CanonicalPhysicalSaveEnvelope(
            CurrentSaveFormat, payloadJson, digest));
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
            if (envelope.Version is not (SaveFormat or AdaptiveSaveFormat or GeographyAlignedSaveFormat or CashFoodSaveFormat) ||
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

        if (payload.Version != envelope.Version ||
            payload.Actions is null || payload.Workers is null ||
            payload.Stocks is null || payload.Cycles is null ||
            ((payload.Version == CashFoodSaveFormat) != (payload.FoodCash is not null)) ||
            (payload.FoodCash is not null &&
             (payload.FoodCash.Accounts is null || payload.FoodCash.Receipts is null)) ||
            payload.Actions.Length > MaximumActions ||
            payload.GeneratorVersion != WorldGenerationVersion.Initial.Value)
            throw new InvalidOperationException("Unsupported canonical save payload.");

        var mode = payload.Version == SaveFormat
            ? PhysicalEconomicCadenceMode.LegacyFixedSix
            : PhysicalEconomicCadenceMode.AdaptiveThirty;
        var terrainMode = payload.Version >= GeographyAlignedSaveFormat
            ? PhysicalTacticalGeographyMode.Canonical01C
            : PhysicalTacticalGeographyMode.LegacyV1;
        var marketMode = payload.Version == CashFoodSaveFormat
            ? PhysicalFoodMarketMode.LocalCashSettlement
            : PhysicalFoodMarketMode.LegacyDirectConsumption;
        var game = Start(payload.Seed,
            new GoldbergParameters(payload.CoarseM, payload.CoarseN),
            new GoldbergParameters(payload.FineM, payload.FineN), mode, terrainMode, marketMode);
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

    private int CurrentSaveFormat => _marketMode == PhysicalFoodMarketMode.LocalCashSettlement
        ? CashFoodSaveFormat
        : _geographyMode == PhysicalTacticalGeographyMode.Canonical01C
        ? GeographyAlignedSaveFormat
        : _cadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty
            ? AdaptiveSaveFormat : SaveFormat;

    private long PrimitiveYield(StrategicCellId tile, EconomicActivityKind activity)
    {
        if (_canonicalGeography is not null)
            return _canonicalGeography.Productivity(tile, activity);
        return activity switch
        {
            EconomicActivityKind.Agriculture => _potentials.Agriculture.GetRawValue(tile),
            EconomicActivityKind.Forestry => _potentials.WoodGathering.GetRawValue(tile),
            EconomicActivityKind.Mining => _potentials.SurfaceStone.GetRawValue(tile),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };
    }

    private CanonicalPhysicalSavePayload BuildPayload()
    {
        var owned = CheckedSnapshot;
        var state = State;
        return new CanonicalPhysicalSavePayload(
            CurrentSaveFormat,
            _seed, _generatorVersion, _coarse.M, _coarse.N, _fine.M, _fine.N,
            owned.WorldBinding!.WorldSignatureSha256Hex, _initialHashHex,
            owned.Revision, state.MovementTurn, state.WorkerRevision,
            CurrentHash.HexDigest, _journal.ToArray(),
            state.Allocations.Select(x => new CanonicalPhysicalWorkerSaveRow(
                x.TileId.FineStrategicCellId.Value, (byte)x.Activity, x.Workers)).ToArray(),
            state.Stocks.Select(x => new CanonicalPhysicalStockSaveRow(
                x.TileId.FineStrategicCellId.Value, x.Commodity.Value,
                x.RawQuantity)).ToArray(),
            state.Cycles.Select(x => new CanonicalPhysicalCycleSaveRow(
                x.MovementTurn, x.ProducedRaw, x.FoodConsumedRaw, x.FoodUnmetRaw)).ToArray(),
            state.FoodCash is { } ledger
                ? new CanonicalFoodCashSave(
                    ledger.InitialMoneySupplyMicrocredits,
                    ledger.Accounts.Select(x => new CanonicalFoodCashAccountSaveRow(
                        x.Tile.FineStrategicCellId.Value,
                        x.HouseholdMicrocredits, x.ProducerMicrocredits)).ToArray(),
                    ledger.Receipts.Select(x => new CanonicalFoodCashReceiptSaveRow(
                        x.MovementTurn, x.Tile.FineStrategicCellId.Value,
                        x.FoodRaw, x.PriceMicrocreditsPerUnit, x.PaidMicrocredits)).ToArray())
                : null);
    }

    private bool IsWorkableLand(StrategicCellId id) =>
        _canonicalGeography?.IsWorkableLand(id) ??
        (FineWorld.StrategicPhysicalFields.LandWater.GetKind(id) ==
            StrategicLandWaterKind.Land &&
         FineWorld.StrategicBiomes.GetKind(id) != StrategicBiomeKind.PolarIce);

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
