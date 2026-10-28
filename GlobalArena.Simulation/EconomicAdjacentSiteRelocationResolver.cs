using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A movement of existing residents/workers from one EconomicPoint into a
/// NEW point on an adjacent strategic LAND cell already controlled by the
/// same civilization. It does not claim new territory or assert fine-tile
/// travel, physical crossing feasibility, construction or a gameplay command.
/// </summary>
public sealed class EconomicAdjacentSiteRelocationOrder
{
    public EconomicPointId SourcePointId { get; }
    public EconomicPointId NewPointId { get; }
    public StrategicCellId DestinationStrategicCellId { get; }
    public ulong Residents { get; }
    public ulong Workers { get; }
    public IReadOnlyList<EconomicActivityWorkforceAllocation> MovingAllocations { get; }

    public EconomicAdjacentSiteRelocationOrder(
        EconomicPointId sourcePointId, EconomicPointId newPointId,
        StrategicCellId destinationStrategicCellId,
        ulong residents, ulong workers,
        IEnumerable<EconomicActivityWorkforceAllocation> movingAllocations)
    {
        if (!sourcePointId.IsValid || !newPointId.IsValid ||
            sourcePointId == newPointId || !destinationStrategicCellId.IsValid)
            throw new ArgumentException("Relocation needs distinct valid point IDs and destination.");
        if (residents == 0UL || workers == 0UL || workers > residents)
            throw new ArgumentOutOfRangeException(nameof(workers),
                "Relocation needs positive residents, positive workers and workers <= residents.");
        ArgumentNullException.ThrowIfNull(movingAllocations);

        var canonical = movingAllocations.ToArray();
        if (canonical.Any(x => x is null))
            throw new ArgumentException("Moving allocations cannot contain null.", nameof(movingAllocations));
        canonical = canonical.OrderBy(x => (byte)x.Kind).ToArray();

        ulong allocated = 0UL;
        for (var i = 0; i < canonical.Length; i++)
        {
            if (i > 0 && canonical[i - 1].Kind == canonical[i].Kind)
                throw new ArgumentException("Duplicate activity in moving workforce.", nameof(movingAllocations));
            allocated = checked(allocated + canonical[i].Workforce);
        }
        if (allocated > workers)
            throw new ArgumentException("Moving allocated workforce exceeds moving workers.");

        SourcePointId = sourcePointId;
        NewPointId = newPointId;
        DestinationStrategicCellId = destinationStrategicCellId;
        Residents = residents;
        Workers = workers;
        MovingAllocations = Array.AsReadOnly(canonical);
    }
}

/// <summary>
/// Pure reference transaction over the EXISTING authoritative economy/census
/// WorldState fields. No command scheduling or event attestation yet. No
/// new hash section: the existing economy and population hash changes.
/// Requires strategic land adjacency, not a full fine-grid route certificate.
/// </summary>
public sealed class EconomicAdjacentSiteRelocationResolver
{
    public WorldState Apply(
        WorldGenerationResult generatedWorld, WorldState original,
        CivilizationId owner, EconomicAdjacentSiteRelocationOrder order)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(order);

        if (!owner.IsValid || !original.Civilizations.Civilizations.Contains(owner))
            throw new ArgumentException("Relocation issuer must exist in world.", nameof(owner));
        if (original.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Relocation requires the exact bound generated world.");
        if (!original.Population.IsInitialized)
            throw new InvalidOperationException("Relocation requires initialized resident census.");

        var points = original.Economy.EconomicPoints.ToDictionary(x => x.Id);
        if (!points.TryGetValue(order.SourcePointId, out var source) ||
            source.Owner != owner)
            throw new InvalidOperationException("Source point must belong to the issuer.");
        if (points.ContainsKey(order.NewPointId))
            throw new InvalidOperationException("New point identity is already occupied.");
        if (source.StrategicCellId == order.DestinationStrategicCellId)
            throw new InvalidOperationException("A new adjacent site requires another strategic cell.");
        if (!original.Territory.TryGetController(source.StrategicCellId, out var sourceController)
            || sourceController != owner ||
            !original.Territory.TryGetController(order.DestinationStrategicCellId, out var destinationController)
            || destinationController != owner)
            throw new InvalidOperationException("Both strategic cells must already be owned by the issuer.");

        var topology = generatedWorld.StrategicTopology;
        if (order.DestinationStrategicCellId.Value > (ulong)topology.Cells.Count)
            throw new InvalidOperationException("Destination strategic cell is outside generated topology.");
        var sourceCell = topology.Cells[checked((int)source.StrategicCellId.Value - 1)];
        if (!sourceCell.AdjacentCellIds.Contains(order.DestinationStrategicCellId))
            throw new InvalidOperationException("Destination is not adjacent to source.");
        var landWater = generatedWorld.StrategicPhysicalFields.LandWater;
        if (landWater.GetKind(source.StrategicCellId) != StrategicLandWaterKind.Land ||
            landWater.GetKind(order.DestinationStrategicCellId) != StrategicLandWaterKind.Land)
            throw new InvalidOperationException("Basic land relocation requires two land cells.");

        var census = original.Population.PointPopulations.ToDictionary(x => x.EconomicPointId);
        var sourceResidents = census[source.Id].Residents;
        if (sourceResidents < order.Residents || source.Workforce < order.Workers)
            throw new InvalidOperationException("Cannot move more residents/workers than exist.");

        // Only unallocated workers or workers of the SAME declared source
        // activity may move; moving an agricultural worker does not silently
        // remove a mining worker.
        var sourceAllocations = source.ActivityAllocations.ToDictionary(x => x.Kind,
            x => x.Workforce);
        ulong movingAllocated = 0UL;
        foreach (var moved in order.MovingAllocations)
        {
            if (!sourceAllocations.TryGetValue(moved.Kind, out var available) ||
                available < moved.Workforce)
                throw new InvalidOperationException("Moving workers exceed their source activity.");
            movingAllocated = checked(movingAllocated + moved.Workforce);
        }
        var movingUnallocated = order.Workers - movingAllocated;
        if (movingUnallocated > source.UnallocatedWorkforce)
            throw new InvalidOperationException("Insufficient unallocated source workers.");

        foreach (var moved in order.MovingAllocations)
            sourceAllocations[moved.Kind] -= moved.Workforce;

        var sourceAfter = new EconomicPointRuntimeState(
            source.Id, owner, source.StrategicCellId,
            source.Workforce - order.Workers,
            sourceAllocations.Where(x => x.Value > 0UL)
                .Select(x => new EconomicActivityWorkforceAllocation(x.Key, x.Value)));
        var destination = new EconomicPointRuntimeState(
            order.NewPointId, owner, order.DestinationStrategicCellId,
            order.Workers, order.MovingAllocations);

        var nextEconomy = new EconomyRuntimeState(
            original.Economy.EconomicPoints.Where(x => x.Id != source.Id)
                .Append(sourceAfter).Append(destination));
        var nextPopulation = new PopulationRuntimeState(
            original.Population.PointPopulations.Where(x => x.EconomicPointId != source.Id)
                .Append(new PopulationPointRuntimeState(source.Id,
                    sourceResidents - order.Residents))
                .Append(new PopulationPointRuntimeState(order.NewPointId, order.Residents)));

        // WorldState enforces resident >= workforce and the census/point
        // one-to-one invariant in one atomic immutable snapshot.
        return original.WithEconomyAndPopulation(nextEconomy, nextPopulation);
    }
}
