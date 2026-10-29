using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// One physical fine tile, one primitive job, one population headcount.
/// These workers ARE the residents and consumers; never a second census.
/// </summary>
public sealed record PhysicalWorkerRuntimeAllocation(
    PhysicalTacticalTileId TileId, EconomicActivityKind Activity, ulong Workers);

public sealed record PhysicalCommodityRuntimeStock(
    PhysicalTacticalTileId TileId, CommodityId CommodityId, ulong RawQuantity);

/// <summary>
/// Immutable physical economic snapshot bound to the same generated world as
/// WorldState. Coarse cells are ONLY a map/indexing level, not economic points.
/// This does not invent a settlement/city or a separate resident population.
/// </summary>
public sealed class PhysicalEconomyRuntimeState
{
    public RuntimeWorldBinding WorldBinding { get; }
    public GoldbergParameters CoarseParameters { get; }
    public GoldbergParameters FineParameters { get; }
    public ulong MovementTurn { get; }
    public ulong WorkerRevision { get; }
    public ulong EconomicCycles { get; }
    public ulong TotalWorkers { get; }
    public IReadOnlyList<PhysicalWorkerRuntimeAllocation> Workers { get; }
    public IReadOnlyList<PhysicalCommodityRuntimeStock> Stocks { get; }

    public PhysicalEconomyRuntimeState(
        RuntimeWorldBinding worldBinding,
        GoldbergParameters coarseParameters,
        GoldbergParameters fineParameters,
        ulong movementTurn,
        ulong workerRevision,
        ulong economicCycles,
        IEnumerable<PhysicalWorkerRuntimeAllocation> workers,
        IEnumerable<PhysicalCommodityRuntimeStock> stocks)
    {
        ArgumentNullException.ThrowIfNull(worldBinding);
        ArgumentNullException.ThrowIfNull(workers);
        ArgumentNullException.ThrowIfNull(stocks);
        if (!coarseParameters.IsValid || !fineParameters.IsValid ||
            coarseParameters.StrategicCellCount != worldBinding.StrategicCellCount)
            throw new ArgumentException("Physical economy coarse Goldberg hierarchy is not bound to the actual world.");
        _ = new GoldbergScaledRefinement(coarseParameters, fineParameters);
        if (economicCycles != movementTurn / 6UL)
            throw new ArgumentException("Physical economic cycle counter disagrees with six-turn schedule.");

        var workforce = workers.ToArray();
        var inventories = stocks.ToArray();
        if (workforce.Any(x => x is null) || inventories.Any(x => x is null))
            throw new ArgumentException("Physical economic snapshot cannot contain null entries.");

        var orderedWorkers = workforce.OrderBy(x => x.TileId.FineStrategicCellId.Value)
            .ThenBy(x => (byte)x.Activity).ToArray();
        var orderedStocks = inventories.OrderBy(x => x.TileId.FineStrategicCellId.Value)
            .ThenBy(x => x.CommodityId.Value).ToArray();

        var workerKeys = new HashSet<(PhysicalTacticalTileId, EconomicActivityKind)>();
        ulong total = 0;
        foreach (var item in orderedWorkers)
        {
            if (!item.TileId.IsValid || item.TileId.FineGoldbergParameters != fineParameters ||
                item.Workers == 0UL ||
                item.Activity is not (EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
                !workerKeys.Add((item.TileId, item.Activity)))
                throw new ArgumentException("Invalid, repeated, zero or unsupported physical worker allocation.");
            total = checked(total + item.Workers);
        }

        var stockKeys = new HashSet<(PhysicalTacticalTileId, CommodityId)>();
        foreach (var item in orderedStocks)
        {
            if (!item.TileId.IsValid || item.TileId.FineGoldbergParameters != fineParameters ||
                !item.CommodityId.IsValid || !stockKeys.Add((item.TileId, item.CommodityId)))
                throw new ArgumentException("Invalid or repeated physical inventory entry.");
        }

        WorldBinding = worldBinding;
        CoarseParameters = coarseParameters;
        FineParameters = fineParameters;
        MovementTurn = movementTurn;
        WorkerRevision = workerRevision;
        EconomicCycles = economicCycles;
        TotalWorkers = total;
        Workers = Array.AsReadOnly(orderedWorkers);
        Stocks = Array.AsReadOnly(orderedStocks);
    }
}
