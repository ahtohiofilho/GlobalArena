using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Real physical source of DEMAND INTENSITY for one civilization at one fine
/// tile. All workers count exactly once, including traveling, military,
/// inactive and zero-output workers. This is NOT commodity-specific demand.
/// </summary>
public sealed record FoundationPhysicalDemandSite(
    PhysicalTacticalTileId Tile, CivilizationId Owner,
    int WorkerCount, ulong DemandMultiplierMicro,
    int ProducingWorkers, int TravelingWorkers,
    int MilitaryWorkers, int OtherWorkers);

/// <summary>
/// Real active WORK CAPACITY at a physical tile, identified by the worker's
/// existing occupation AND existing commodity identity. Output potential is
/// not a sale, a physical inventory, or a calibrated agricultural yield.
/// Commodity IDs of the demonstration scenario remain fixture inputs.
/// </summary>
public sealed record FoundationPhysicalSupplyLot(
    PhysicalTacticalTileId Tile, CivilizationId Owner,
    PhysicalWorkerOccupation Occupation, CommodityId Commodity,
    int WorkerCount, ulong CapacityMicroPerTurn);

/// <summary>
/// Immutable, canonical-order, read-only economic inputs from one committed
/// physical-worker census. No artificial buyer, market, price, item or edge.
/// </summary>
public sealed class FoundationPhysicalWorkforceEconomicInputs
{
    public IReadOnlyList<FoundationPhysicalDemandSite> DemandSites { get; }
    public IReadOnlyList<FoundationPhysicalSupplyLot> SupplyLots { get; }
    public int WorkerCount { get; }
    public int ActiveProducingWorkerCount { get; }
    public int PositiveSupplyWorkerCount { get; }
    public ulong TotalDemandMultiplierMicro { get; }
    public ulong TotalActiveCapacityMicroPerTurn { get; }

    internal FoundationPhysicalWorkforceEconomicInputs(
        FoundationPhysicalDemandSite[] demand,
        FoundationPhysicalSupplyLot[] supply,
        int workers, int activeWorkers, int supplierWorkers,
        ulong demandMicro, ulong capacityMicro)
    {
        DemandSites = Array.AsReadOnly(demand);
        SupplyLots = Array.AsReadOnly(supply);
        WorkerCount = workers;
        ActiveProducingWorkerCount = activeWorkers;
        PositiveSupplyWorkerCount = supplierWorkers;
        TotalDemandMultiplierMicro = demandMicro;
        TotalActiveCapacityMicroPerTurn = capacityMicro;
    }
}

/// <summary>
/// Physically grounded bridge from the authoritative v11 workforce to future
/// market clearing. The projector does not cache or mutate a census and is
/// intentionally separate from the visual worker grouping and the legacy
/// floating-point market experiment.
/// </summary>
public static class FoundationPhysicalWorkforceEconomicProjector
{
    private sealed class DemandBucket
    {
        public int Workers;
        public ulong DemandMicro;
        public int Producing;
        public int Traveling;
        public int Military;
        public int Other;
    }

    private sealed class SupplyBucket
    {
        public int Workers;
        public ulong CapacityMicro;
    }

    public static FoundationPhysicalWorkforceEconomicInputs Project(
        PhysicalWorkerRuntimeState census)
    {
        ArgumentNullException.ThrowIfNull(census);
        if (!census.IsInitialized ||
            !census.HasAuthoritativeTacticalLocations ||
            !census.HasAuthoritativeOccupations)
            throw new InvalidOperationException(
                "Physical economic inputs require a complete authoritative v11 " +
                "worker census with real fine locations and occupations.");

        // A worker contributes one and only one demand location. One active
        // worker can contribute at most one positive supply lot. No filtering
        // by civilian ownership: all civilizations belong to this same world.
        var demand = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner), DemandBucket>();
        var supply = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner,
             PhysicalWorkerOccupation Role, CommodityId Commodity), SupplyBucket>();
        int activeWorkers = 0;
        int supplierWorkers = 0;
        ulong demandMicro = 0UL;
        ulong capacityMicro = 0UL;
        foreach (var worker in census.Workers)
        {
            var tile = worker.TacticalTileId!.Value;
            var owner = worker.Owner;
            var key = (tile, owner);
            if (!demand.TryGetValue(key, out var demandBucket))
                demand.Add(key, demandBucket = new DemandBucket());
            demandBucket.Workers = checked(demandBucket.Workers + 1);
            demandBucket.DemandMicro = checked(
                demandBucket.DemandMicro + worker.DemandMultiplierMicro);
            demandMicro = checked(demandMicro + worker.DemandMultiplierMicro);

            switch (worker.Activity)
            {
                case PhysicalWorkerActivity.Producing:
                    demandBucket.Producing++;
                    activeWorkers = checked(activeWorkers + 1);
                    break;
                case PhysicalWorkerActivity.Traveling:
                    demandBucket.Traveling++;
                    break;
                case PhysicalWorkerActivity.Military:
                    demandBucket.Military++;
                    break;
                case PhysicalWorkerActivity.Other:
                    demandBucket.Other++;
                    break;
                default:
                    throw new InvalidOperationException("Unknown authoritative worker activity.");
            }

            if (worker.Activity != PhysicalWorkerActivity.Producing ||
                worker.OutputMicroPerTurn == 0UL)
                continue;
            var role = worker.Occupation!.Value;
            if (role == PhysicalWorkerOccupation.Unassigned)
                throw new InvalidOperationException(
                    "A producing worker needs a real productive occupation.");
            var supplyKey = (tile, owner, role, worker.Commodity);
            if (!supply.TryGetValue(supplyKey, out var supplyBucket))
                supply.Add(supplyKey, supplyBucket = new SupplyBucket());
            supplyBucket.Workers = checked(supplyBucket.Workers + 1);
            supplyBucket.CapacityMicro = checked(
                supplyBucket.CapacityMicro + worker.OutputMicroPerTurn);
            capacityMicro = checked(capacityMicro + worker.OutputMicroPerTurn);
            supplierWorkers = checked(supplierWorkers + 1);
        }

        var demandSites = demand
            .OrderBy(item => item.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(item => item.Key.Owner.Value)
            .Select(item => new FoundationPhysicalDemandSite(
                item.Key.Tile, item.Key.Owner, item.Value.Workers,
                item.Value.DemandMicro, item.Value.Producing,
                item.Value.Traveling, item.Value.Military, item.Value.Other))
            .ToArray();
        var supplyLots = supply
            .OrderBy(item => item.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(item => item.Key.Owner.Value)
            .ThenBy(item => item.Key.Role)
            .ThenBy(item => item.Key.Commodity.Value)
            .Select(item => new FoundationPhysicalSupplyLot(
                item.Key.Tile, item.Key.Owner, item.Key.Role, item.Key.Commodity,
                item.Value.Workers, item.Value.CapacityMicro))
            .ToArray();

        // Explicit conservation: neither a demand site nor a supply lot can
        // fabricate a worker or silently lose/multiply a micro-unit.
        if (demandSites.Sum(site => (long)site.WorkerCount) != census.Workers.Count ||
            demandSites.Aggregate(0UL, (sum, site) =>
                checked(sum + site.DemandMultiplierMicro)) != demandMicro ||
            supplyLots.Sum(lot => (long)lot.WorkerCount) != supplierWorkers ||
            supplyLots.Aggregate(0UL, (sum, lot) =>
                checked(sum + lot.CapacityMicroPerTurn)) != capacityMicro)
            throw new InvalidOperationException(
                "Physical economic projection lost or duplicated a worker.");

        return new FoundationPhysicalWorkforceEconomicInputs(
            demandSites, supplyLots, census.Workers.Count, activeWorkers,
            supplierWorkers, demandMicro, capacityMicro);
    }
}
