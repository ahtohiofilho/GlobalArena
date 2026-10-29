using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A worker is a producer AND a consumer. This snapshot stores only physical
/// tactical tile allocations. There are no settlement/city entities and no
/// worker counts attached to coarse strategic regions.
/// </summary>
public sealed record TacticalWorkerAssignment(
    PhysicalTacticalTileId TileId, EconomicActivityKind Activity, ulong Workers);

public sealed record TacticalWorkerFlow(
    PhysicalTacticalTileId TileId, CommodityId CommodityId,
    EconomicActivityKind Activity, ulong RawQuantity);

/// <summary>
/// GA-VS03 functional reference: physical fine Goldberg tiles are the
/// economic locations; coarse identities are only deterministic presentation
/// buckets. No physical tile is duplicated when intersecting strategic borders.
/// This is an in-memory interaction probe, not a canonical saved game.
/// </summary>
public sealed class EconomicPhysicalTacticalWorkerSession
{
    public static readonly GoldbergParameters CoarseParameters = new(2, 0);
    public static readonly GoldbergParameters FineParameters = new(12, 0);

    private readonly Dictionary<(PhysicalTacticalTileId Tile, EconomicActivityKind Activity), ulong>
        _workforce = new();

    private readonly StrategicPrimitiveResourcePotentialFieldSet _geographicPotential;
    private readonly StrategicCellId[] _presentationBuckets;

    public WorldGenerationResult CoarseWorld { get; }
    public WorldGenerationResult FineWorld { get; }
    public PhysicalTacticalIncidenceMap Incidence { get; }
    public ulong Revision { get; private set; }
    public ulong PreviewCount { get; private set; }
    public PhysicalTacticalTileId InitialTile { get; }

    public EconomicPhysicalTacticalWorkerSession(ulong seed = 0UL)
    {
        var generator = new DeterministicWorldGenerator();
        CoarseWorld = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial, CoarseParameters));
        FineWorld = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial, FineParameters));
        Incidence = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(CoarseParameters, FineParameters));

        if (CoarseWorld.StrategicTopology.Cells.Count != Incidence.CoarseTopology.Cells.Count ||
            FineWorld.StrategicTopology.Cells.Count != Incidence.FineTopology.Cells.Count ||
            Incidence.TileIncidences.Count != FineWorld.StrategicTopology.Cells.Count)
            throw new InvalidOperationException("Physical tactical Goldberg hierarchy mismatch.");

        // Identifiers and fine neighbor graph must match the actual generated
        // world; do not infer authority from a visually similar topology.
        foreach (var cell in FineWorld.StrategicTopology.Cells)
            if (!cell.AdjacentCellIds.SequenceEqual(
                    Incidence.FineTopology.Cells[checked((int)cell.Id.Value - 1)].AdjacentCellIds))
                throw new InvalidOperationException(
                    "Physical fine topology differs from the generated world.");

        _geographicPotential = StrategicPrimitiveResourcePotentialFieldSet.Derive(FineWorld);
        _presentationBuckets = Incidence.TileIncidences.Select(incidence =>
        {
            // Only an accounting bucket for a summary; it does NOT imply
            // territorial sovereignty, ownership, or a city.
            return incidence.IncidentCoarseCellIds[0];
        }).ToArray();

        // Start in the geographically strongest workable agricultural fine
        // tile with at least one adjacent usable land tile. Avoid the former
        // G2 tundra scenario, which was an arbitrary strategic starting site.
        var first = FineWorld.StrategicTopology.Cells
            .Where(cell => IsWorkableLand(cell.Id) &&
                cell.AdjacentCellIds.Any(IsWorkableLand))
            .OrderByDescending(cell =>
                _geographicPotential.Agriculture.GetRawValue(cell.Id))
            .ThenBy(cell => cell.Id.Value)
            .FirstOrDefault() ?? throw new InvalidOperationException(
                "No connected productive tactical land in the generated world.");

        InitialTile = PhysicalId(first.Id);
        _workforce.Add((InitialTile, EconomicActivityKind.Agriculture), 4UL);
        _workforce.Add((InitialTile, EconomicActivityKind.Forestry), 2UL);
        _workforce.Add((InitialTile, EconomicActivityKind.Mining), 2UL);
    }

    public IReadOnlyList<TacticalWorkerAssignment> Assignments =>
        Array.AsReadOnly(_workforce.OrderBy(x => x.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(x => (byte)x.Key.Activity)
            .Select(x => new TacticalWorkerAssignment(
                x.Key.Tile, x.Key.Activity, x.Value)).ToArray());

    public ulong TotalWorkers => _workforce.Values.Aggregate(
        0UL, (a, b) => checked(a + b));

    public ulong WorkersAt(PhysicalTacticalTileId id) =>
        _workforce.Where(x => x.Key.Tile == id)
            .Aggregate(0UL, (total, item) => checked(total + item.Value));

    public PhysicalTacticalTileId PhysicalId(StrategicCellId fineId) =>
        new(FineParameters, fineId);

    public bool IsWorkableLand(StrategicCellId fineId) =>
        FineWorld.StrategicPhysicalFields.LandWater.GetKind(fineId) ==
            StrategicLandWaterKind.Land &&
        FineWorld.StrategicBiomes.GetKind(fineId) != StrategicBiomeKind.PolarIce;

    public StrategicCellId DisplayBucket(PhysicalTacticalTileId id)
    {
        ValidateTile(id);
        return _presentationBuckets[checked((int)id.FineStrategicCellId.Value - 1)];
    }

    public IReadOnlyList<StrategicCellId> Incidents(PhysicalTacticalTileId id)
    {
        ValidateTile(id);
        return Incidence.TileIncidences[
            checked((int)id.FineStrategicCellId.Value - 1)].IncidentCoarseCellIds;
    }

    public IReadOnlyList<PhysicalTacticalTileId> AdjacentWorkable(
        PhysicalTacticalTileId source)
    {
        ValidateTile(source);
        return Array.AsReadOnly(FineWorld.StrategicTopology.Cells[
            checked((int)source.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds.Where(IsWorkableLand)
            .OrderBy(id => id.Value).Select(PhysicalId).ToArray());
    }

    /// <summary>
    /// Place existing workers in an adjacent physical tile; optionally change
    /// their assigned job. Vacated tiles simply have zero workers. No
    /// occupation of the parent strategic area is implied by this order.
    /// </summary>
    public void MoveWorkers(PhysicalTacticalTileId source,
        PhysicalTacticalTileId target,
        EconomicActivityKind fromJob, EconomicActivityKind toJob,
        ulong amount)
    {
        ValidateTile(source);
        ValidateTile(target);
        ValidateJob(fromJob);
        ValidateJob(toJob);
        if (source == target)
            throw new InvalidOperationException(
                "Moving workers to the same tile is not a physical relocation.");
        if (amount == 0UL)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!IsWorkableLand(target.FineStrategicCellId) ||
            !AdjacentWorkable(source).Contains(target))
            throw new InvalidOperationException(
                "Workers may relocate only to an adjacent workable physical tile.");

        var key = (source, fromJob);
        if (!_workforce.TryGetValue(key, out var existing) || existing < amount)
            throw new InvalidOperationException(
                "Source tactical tile lacks workers with the chosen job.");

        var targetKey = (target, toJob);
        _workforce.TryGetValue(targetKey, out var other);
        var combined = checked(other + amount);
        var nextRevision = checked(Revision + 1UL);
        // All checks above occur BEFORE the first mutation.
        if (existing == amount) _workforce.Remove(key);
        else _workforce[key] = existing - amount;
        _workforce[targetKey] = combined;
        Revision = nextRevision;
    }

    public (IReadOnlyList<TacticalWorkerFlow> Supply,
        IReadOnlyList<TacticalWorkerFlow> Demand) Preview()
    {
        var supply = new List<TacticalWorkerFlow>();
        var demand = new List<TacticalWorkerFlow>();
        foreach (var group in Assignments.GroupBy(x => x.TileId))
        {
            var total = group.Aggregate(0UL,
                (sum, x) => checked(sum + x.Workers));
            // Same workers cause demand regardless of their productive job.
            demand.Add(new TacticalWorkerFlow(group.Key,
                new CommodityId(1U), EconomicActivityKind.Agriculture,
                checked(total * 500_000UL)));

            foreach (var allocation in group)
            {
                var (commodity, rate) = allocation.Activity switch
                {
                    EconomicActivityKind.Agriculture => (
                        new CommodityId(1U), _geographicPotential.Agriculture
                            .GetRawValue(group.Key.FineStrategicCellId)),
                    EconomicActivityKind.Forestry => (
                        new CommodityId(3U), _geographicPotential.WoodGathering
                            .GetRawValue(group.Key.FineStrategicCellId)),
                    EconomicActivityKind.Mining => (
                        new CommodityId(2U), _geographicPotential.SurfaceStone
                            .GetRawValue(group.Key.FineStrategicCellId)),
                    _ => (default(CommodityId), 0L)
                };
                if (rate > 0L)
                    supply.Add(new TacticalWorkerFlow(group.Key, commodity,
                        allocation.Activity, checked(allocation.Workers * (ulong)rate)));
            }
        }
        return (Array.AsReadOnly(supply.ToArray()),
            Array.AsReadOnly(demand.ToArray()));
    }

    public void AdvancePreview()
    {
        _ = Preview(); // No production settlement or inventory mutation.
        PreviewCount = checked(PreviewCount + 1UL);
    }

    public IReadOnlyDictionary<StrategicCellId, ulong> StrategicDisplayTotals()
    {
        var values = new Dictionary<StrategicCellId, ulong>();
        foreach (var group in Assignments.GroupBy(x => x.TileId))
        {
            var bucket = DisplayBucket(group.Key);
            values.TryGetValue(bucket, out var count);
            values[bucket] = checked(count + group.Aggregate(
                0UL, (sum, a) => checked(sum + a.Workers)));
        }
        if (values.Values.Aggregate(0UL, (sum, x) => checked(sum + x)) != TotalWorkers)
            throw new InvalidOperationException(
                "Strategic presentation duplicated physical workers.");
        return values;
    }

    private static void ValidateJob(EconomicActivityKind activity)
    {
        if (activity != EconomicActivityKind.Agriculture &&
            activity != EconomicActivityKind.Forestry &&
            activity != EconomicActivityKind.Mining)
            throw new ArgumentOutOfRangeException(nameof(activity));
    }

    private void ValidateTile(PhysicalTacticalTileId id)
    {
        if (!id.IsValid || id.FineGoldbergParameters != FineParameters ||
            id.FineStrategicCellId.Value >
                (ulong)Incidence.FineTopology.Cells.Count)
            throw new ArgumentException(
                "Physical tile identity is not in this fine Goldberg world.",
                nameof(id));
    }
}
