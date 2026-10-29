using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// GA-VS01: small, deterministic, interactive NEOLITHIC gameplay probe.
/// Uses the real G3-C worker-only founding resolver and G3-D geographic
/// production/demand projections. NOT a complete economic turn settlement,
/// durable save, combat loop or the Godot viewer.
/// </summary>
public sealed class EconomicNeolithicInteractionSession
{
    private static readonly CivilizationId Player = new(1UL);
    private static readonly EconomicPrimitiveCommodityMapping Commodities =
        new(new CommodityId(1U), new CommodityId(3U), new CommodityId(2U));

    private readonly EconomicNeolithicSiteFoundingResolver _founding = new();
    private readonly EconomicGeographicPrimitiveWorkerEconomyResolver _economy = new();
    private readonly CanonicalWorldStateHasher _hasher = new();

    public WorldGenerationResult GeneratedWorld { get; }
    public WorldState CurrentWorld { get; private set; }
    public ulong CompletedPreviewTurns { get; private set; }

    public EconomicNeolithicInteractionSession(ulong seed = 0UL)
    {
        GeneratedWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

        var first = GeneratedWorld.StrategicTopology.Cells.FirstOrDefault(cell =>
            IsColonizableLand(cell.Id) &&
            cell.AdjacentCellIds.Any(IsColonizableLand));
        if (first is null)
            throw new InvalidOperationException(
                "Generated world has no adjacent inhabitable land for a Neolithic start.");

        var origin = new EconomicPointId(1UL);
        CurrentWorld = WorldState.CreateBound(GeneratedWorld)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Player }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(first.Id, Player)
            }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        origin, Player, first.Id, 8UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 3UL),
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Forestry, 3UL),
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Mining, 2UL)
                        })
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(origin, 8UL)
                }));
    }

    public WorldStateHash CurrentHash => _hasher.Compute(CurrentWorld);

    public IReadOnlyList<StrategicCellId> AvailableLandNeighbors(
        EconomicPointId source)
    {
        var point = CurrentWorld.Economy.EconomicPoints.SingleOrDefault(
            p => p.Id == source && p.Owner == Player);
        if (point is null)
            throw new ArgumentException("Select a settlement belonging to the player.",
                nameof(source));

        var cell = GeneratedWorld.StrategicTopology.Cells.Single(
            c => c.Id == point.StrategicCellId);
        return cell.AdjacentCellIds
            .Where(id => IsColonizableLand(id) &&
                !CurrentWorld.Territory.TryGetController(id, out _) &&
                !CurrentWorld.Economy.EconomicPoints.Any(p =>
                    p.StrategicCellId == id))
            .OrderBy(id => id.Value).ToArray();
    }

    /// <summary>
    /// Transfers EXISTING workers to adjacent free land, without charging
    /// wood/stone for basic occupation. The worker census is mirrored by
    /// the existing G3-C resolver, not independently created residents.
    /// This is an in-memory proposal applied by this single-player probe;
    /// no authorized durable turn or remote commitment is implied.
    /// </summary>
    public EconomicPointId Found(
        EconomicPointId source, StrategicCellId destination, ulong workers)
    {
        var sourcePoint = CurrentWorld.Economy.EconomicPoints.SingleOrDefault(
            p => p.Id == source && p.Owner == Player);
        if (sourcePoint is null)
            throw new InvalidOperationException("Source settlement does not exist.");
        if (workers == 0UL || workers >= sourcePoint.Workforce)
            throw new ArgumentOutOfRangeException(nameof(workers),
                "Transfer at least one worker and leave at least one at the source.");
        if (!AvailableLandNeighbors(source).Contains(destination))
            throw new InvalidOperationException(
                "Target is not free neighboring land belonging to the generated world.");

        // Deterministically transfer existing productive assignments, while
        // still allowing unallocated people to move. This never creates
        // duplicate allocations or more workers than the source owns.
        var remaining = workers;
        var moving = new List<EconomicActivityWorkforceAllocation>();
        foreach (var allocation in sourcePoint.ActivityAllocations)
        {
            var count = Math.Min(remaining, allocation.Workforce);
            if (count > 0UL)
                moving.Add(new EconomicActivityWorkforceAllocation(
                    allocation.Kind, count));
            remaining -= count;
            if (remaining == 0UL)
                break;
        }

        var nextPoint = new EconomicPointId(checked(
            CurrentWorld.Economy.EconomicPoints.Max(p => p.Id.Value) + 1UL));
        var proposed = _founding.ProposeBasic(
            GeneratedWorld, CurrentWorld, Player, CurrentHash,
            EconomicMaterialReserveBook.Empty,
            new EconomicPrimitiveWorkerMoveOrder(
                source, nextPoint, destination, workers, moving));
        CurrentWorld = proposed.ProposedWorld.AdvanceRevision();
        if (_hasher.Compute(CurrentWorld) == proposed.PriorWorldHash)
            throw new InvalidOperationException("Founding failed to change the world.");
        return nextPoint;
    }

    public EconomicProductionDemandResult PreviewGeographicEconomy()
    {
        var demand = CurrentWorld.Economy.EconomicPoints.Select(
            p => new EconomicDemandProfile(p.Id, Commodities.Food, 500_000L));
        return _economy.Resolve(GeneratedWorld, CurrentWorld, Commodities,
            demand);
    }

    /// <summary>
    /// Executes a new geography/worker-based PREVIEW, not resource inventory
    /// settlement. Its counter is UX-only and not a canonical game turn.
    /// </summary>
    public EconomicProductionDemandResult AdvancePreview()
    {
        var result = PreviewGeographicEconomy();
        CompletedPreviewTurns = checked(CompletedPreviewTurns + 1UL);
        return result;
    }

    private bool IsColonizableLand(StrategicCellId id) =>
        GeneratedWorld.StrategicPhysicalFields.LandWater.GetKind(id) ==
            StrategicLandWaterKind.Land &&
        GeneratedWorld.StrategicBiomes.GetKind(id) !=
            StrategicBiomeKind.PolarIce;
}
