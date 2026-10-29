using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// One physical tile is the sole location of each worker allocation.
/// A worker counts as both a producer and a consumer; no separate
/// strategic resident or city is synthesized from these records.
/// </summary>
public sealed record PhysicalWorkerAllocation(
    PhysicalTacticalTileId TileId, EconomicActivityKind Activity, ulong Workers);

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
    public const ulong MovementTicksPerEconomicCycle = 6UL;

    public RuntimeWorldBinding CoarseWorldBinding { get; }
    public GoldbergParameters CoarseParameters { get; }
    public GoldbergParameters FineParameters { get; }
    public ulong MovementTurn { get; }
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
        IEnumerable<PhysicalEconomicCycle> cycles)
    {
        ArgumentNullException.ThrowIfNull(coarseWorldBinding);
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(cycles);

        if (!coarseParameters.IsValid || !fineParameters.IsValid ||
            coarseWorldBinding.StrategicCellCount != coarseParameters.StrategicCellCount)
            throw new ArgumentException("Physical economy topology does not match its world binding.");

        // Refuse non-nested or incompatible Goldberg meshes before reading IDs.
        _ = new GoldbergScaledRefinement(coarseParameters, fineParameters);

        var sortedAllocations = allocations
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

        var sortedStocks = stocks
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
        if ((ulong)orderedCycles.Length != movementTurn / MovementTicksPerEconomicCycle)
            throw new ArgumentException("Economic cycle count disagrees with movement time.");
        for (var i = 0; i < orderedCycles.Length; i++)
        {
            if (orderedCycles[i] is null ||
                orderedCycles[i].MovementTurn != checked((ulong)(i + 1) * MovementTicksPerEconomicCycle))
                throw new ArgumentException("Economic cycles must follow the six-tick cadence.");
        }

        CoarseWorldBinding = coarseWorldBinding;
        CoarseParameters = coarseParameters;
        FineParameters = fineParameters;
        MovementTurn = movementTurn;
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
