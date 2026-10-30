using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// One physical tile is the sole location of each worker allocation.
/// A worker counts as both a producer and a consumer; no separate
/// strategic resident or city is synthesized from these records.
/// </summary>
public sealed record PhysicalWorkerAllocation(
    PhysicalTacticalTileId TileId, EconomicActivityKind Activity, ulong Workers);

/// <summary>Legacy games retain V1 geography; VS12 uses canonical 01C fine terrain.</summary>
public enum PhysicalTacticalGeographyMode : byte
{
    LegacyV1 = 0,
    Canonical01C = 1
}

public sealed record PhysicalCommodityStock(
    PhysicalTacticalTileId TileId, CommodityId Commodity, ulong RawQuantity);

public sealed record PhysicalEconomicCycle(
    ulong MovementTurn, ulong ProducedRaw, ulong FoodConsumedRaw, ulong FoodUnmetRaw);

/// <summary>
/// Immutable, canonical-ready physical economic snapshot. The world binding
/// identifies the *coarse generated world*, while all worker/stock identities
/// belong to the validated fine topology. Persistence/event integration
/// remains an independent, versioned migration gate.
/// </summary>
public sealed class PhysicalTacticalEconomyRuntimeState
{
    // Historical constant retained for callers validating legacy V9 replays.
    public const ulong MovementTicksPerEconomicCycle = PhysicalEconomicCadence.LegacyInterval;

    public RuntimeWorldBinding CoarseWorldBinding { get; }
    public GoldbergParameters CoarseParameters { get; }
    public GoldbergParameters FineParameters { get; }
    public ulong MovementTurn { get; }
    public PhysicalEconomicCadenceMode CadenceMode { get; }
    public PhysicalTacticalGeographyMode GeographyMode { get; }
    public PhysicalFoodCashMarketState? FoodCash { get; }
    public PhysicalFoodMarketMode MarketMode => FoodCash is null
        ? PhysicalFoodMarketMode.LegacyDirectConsumption : PhysicalFoodMarketMode.LocalCashSettlement;
    public PhysicalWorkerMovementMode MovementMode { get; }
    public IReadOnlyList<PhysicalWorkerTransit> Transits { get; }
    public ulong WorkersInTransit => Transits.Aggregate(0UL,
        (sum, x) => checked(sum + x.Workers));
    public ulong WorkerRevision { get; }
    public ulong TotalWorkers { get; }
    public IReadOnlyList<PhysicalWorkerAllocation> Allocations { get; }
    public IReadOnlyList<PhysicalCommodityStock> Stocks { get; }
    public IReadOnlyList<PhysicalEconomicCycle> Cycles { get; }

    public PhysicalTacticalEconomyRuntimeState(
        RuntimeWorldBinding coarseWorldBinding,
        GoldbergParameters coarseParameters,
        GoldbergParameters fineParameters,
        ulong movementTurn,
        ulong workerRevision,
        IEnumerable<PhysicalWorkerAllocation> allocations,
        IEnumerable<PhysicalCommodityStock> stocks,
        IEnumerable<PhysicalEconomicCycle> cycles,
        PhysicalEconomicCadenceMode cadenceMode = PhysicalEconomicCadenceMode.LegacyFixedSix,
        PhysicalTacticalGeographyMode geographyMode = PhysicalTacticalGeographyMode.LegacyV1,
        PhysicalFoodCashMarketState? foodCash = null,
        PhysicalWorkerMovementMode movementMode = PhysicalWorkerMovementMode.LegacyInstant,
        IEnumerable<PhysicalWorkerTransit>? transits = null)
    {
        ArgumentNullException.ThrowIfNull(coarseWorldBinding);
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(cycles);
        if (geographyMode is not (PhysicalTacticalGeographyMode.LegacyV1 or PhysicalTacticalGeographyMode.Canonical01C))
            throw new ArgumentOutOfRangeException(nameof(geographyMode));
        if (geographyMode == PhysicalTacticalGeographyMode.Canonical01C &&
            cadenceMode != PhysicalEconomicCadenceMode.AdaptiveThirty)
            throw new ArgumentException("01C physical economy requires adaptive cadence.");

        if (!coarseParameters.IsValid || !fineParameters.IsValid ||
            coarseWorldBinding.StrategicCellCount != coarseParameters.StrategicCellCount)
            throw new ArgumentException("Physical economy topology does not match its world binding.");

        // Refuse non-nested or incompatible Goldberg meshes before reading IDs.
        _ = new GoldbergScaledRefinement(coarseParameters, fineParameters);

        var allocationRows = allocations.ToArray();
        if (allocationRows.Any(x => x is null))
            throw new ArgumentException("Physical workforce cannot contain null records.");
        var sortedAllocations = allocationRows
            .OrderBy(x => x.TileId.IsValid ? x.TileId.FineStrategicCellId.Value : 0UL)
            .ThenBy(x => (byte)x.Activity).ToArray();
        ulong totalWorkers = 0UL;
        var allocated = new HashSet<(PhysicalTacticalTileId, EconomicActivityKind)>();
        foreach (var allocation in sortedAllocations)
        {
            ValidateTile(allocation.TileId, fineParameters);
            if (allocation.Workers == 0UL ||
                allocation.Activity is not (EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
                !allocated.Add((allocation.TileId, allocation.Activity)))
                throw new ArgumentException("Invalid or duplicate physical workforce allocation.");
            totalWorkers = checked(totalWorkers + allocation.Workers);
        }

        if (movementMode is not (PhysicalWorkerMovementMode.LegacyInstant or
            PhysicalWorkerMovementMode.TimedAdjacent) ||
            (movementMode == PhysicalWorkerMovementMode.TimedAdjacent &&
             (geographyMode != PhysicalTacticalGeographyMode.Canonical01C || foodCash is null)))
            throw new ArgumentException("Timed transit requires local cash market and canonical 01C terrain.");
        var orderedTransits = (transits ?? Array.Empty<PhysicalWorkerTransit>()).ToArray();
        if (movementMode == PhysicalWorkerMovementMode.LegacyInstant &&
            orderedTransits.Length != 0)
            throw new ArgumentException("Legacy movement cannot contain worker transits.");
        orderedTransits = orderedTransits.OrderBy(x => x?.OrderId ?? 0UL).ToArray();
        var transitIds = new HashSet<ulong>();
        foreach (var transit in orderedTransits)
        {
            if (transit is null || transit.OrderId == 0UL ||
                transit.OrderId > workerRevision ||
                !transitIds.Add(transit.OrderId) || transit.Workers == 0UL ||
                transit.DepartureTurn > movementTurn ||
                transit.ArrivalTurn <= movementTurn ||
                transit.ArrivalTurn <= transit.DepartureTurn ||
                transit.ArrivalTurn - transit.DepartureTurn > 3UL ||
                transit.Source == transit.Destination ||
                transit.FromJob is not (EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
                transit.ToJob is not (EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or EconomicActivityKind.Mining))
                throw new ArgumentException("Invalid or stale timed worker transit.");
            ValidateTile(transit.Source, fineParameters);
            ValidateTile(transit.Destination, fineParameters);
            totalWorkers = checked(totalWorkers + transit.Workers);
        }

        var stockRows = stocks.ToArray();
        if (stockRows.Any(x => x is null))
            throw new ArgumentException("Physical stock cannot contain null records.");
        var sortedStocks = stockRows
            .OrderBy(x => x.TileId.IsValid ? x.TileId.FineStrategicCellId.Value : 0UL)
            .ThenBy(x => x.Commodity.IsValid ? x.Commodity.Value : 0U).ToArray();
        var stockKeys = new HashSet<(PhysicalTacticalTileId, CommodityId)>();
        foreach (var stock in sortedStocks)
        {
            ValidateTile(stock.TileId, fineParameters);
            if (!stock.Commodity.IsValid ||
                !stockKeys.Add((stock.TileId, stock.Commodity)))
                throw new ArgumentException("Invalid or duplicate physical commodity stock.");
        }

        var orderedCycles = cycles.ToArray();
        if (orderedCycles.Any(x => x is null))
            throw new ArgumentException("Economic cycles cannot contain null records.");
        PhysicalEconomicCadence.ValidateTimeline(movementTurn, orderedCycles, cadenceMode);
        if (foodCash is not null &&
            (geographyMode != PhysicalTacticalGeographyMode.Canonical01C ||
             cadenceMode != PhysicalEconomicCadenceMode.AdaptiveThirty))
            throw new ArgumentException("Cash food settlement requires the canonical 01C geography and adaptive clock.");

        CoarseWorldBinding = coarseWorldBinding;
        CoarseParameters = coarseParameters;
        FineParameters = fineParameters;
        MovementTurn = movementTurn;
        CadenceMode = cadenceMode;
        GeographyMode = geographyMode;
        FoodCash = foodCash;
        MovementMode = movementMode;
        Transits = Array.AsReadOnly(orderedTransits);
        WorkerRevision = workerRevision;
        TotalWorkers = totalWorkers;
        Allocations = Array.AsReadOnly(sortedAllocations);
        Stocks = Array.AsReadOnly(sortedStocks);
        Cycles = Array.AsReadOnly(orderedCycles);
    }

    private static void ValidateTile(PhysicalTacticalTileId tile, GoldbergParameters fine)
    {
        if (!tile.IsValid || tile.FineGoldbergParameters != fine)
            throw new ArgumentException("Economic record references a foreign physical tile.");
    }
}
