using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit catalog mapping. Commodity IDs are supplied by the scenario,
/// not hard-coded by the geographic resolver.
/// </summary>
public sealed class EconomicPrimitiveCommodityMapping
{
    public CommodityId Food { get; }
    public CommodityId Wood { get; }
    public CommodityId Stone { get; }

    public EconomicPrimitiveCommodityMapping(
        CommodityId food, CommodityId wood, CommodityId stone)
    {
        if (!food.IsValid || !wood.IsValid || !stone.IsValid ||
            food == wood || food == stone || wood == stone)
            throw new ArgumentException(
                "Primitive resource mapping needs three distinct valid commodity IDs.");
        Food = food;
        Wood = wood;
        Stone = stone;
    }
}

/// <summary>
/// G3-D: constructs worker production potentials from actual generated-world
/// environmental fields. Activity placement and demand are still driven by
/// worker allocation. Zero geographic potential yields no supply row, not an
/// invented minimum yield, and no structure is required to start production.
/// </summary>
public sealed class EconomicGeographicPrimitiveWorkerEconomyResolver
{
    private readonly EconomicPrimitiveWorkerEconomyResolver _existing = new();

    public EconomicProductionDemandResult Resolve(
        WorldGenerationResult generatedWorld,
        WorldState world,
        EconomicPrimitiveCommodityMapping commodities,
        IEnumerable<EconomicDemandProfile> workerDemandProfiles)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(commodities);
        ArgumentNullException.ThrowIfNull(workerDemandProfiles);

        if (world.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Geographic production requires the exact bound generated world.");

        var geographic = StrategicPrimitiveResourcePotentialFieldSet.Derive(generatedWorld);
        var potentials = new List<EconomicProductionPotential>();

        foreach (var point in world.Economy.EconomicPoints)
        {
            var cell = point.StrategicCellId;

            foreach (var allocation in point.ActivityAllocations)
            {
                CommodityId commodity;
                long rawRate;

                switch (allocation.Kind)
                {
                    case EconomicActivityKind.Agriculture:
                        commodity = commodities.Food;
                        rawRate = geographic.Agriculture.GetRawValue(cell);
                        break;
                    case EconomicActivityKind.Forestry:
                        commodity = commodities.Wood;
                        rawRate = geographic.WoodGathering.GetRawValue(cell);
                        break;
                    case EconomicActivityKind.Mining:
                        commodity = commodities.Stone;
                        rawRate = geographic.SurfaceStone.GetRawValue(cell);
                        break;
                    default:
                        continue; // Nonproductive logistics workers still consume.
                }

                if (rawRate == 0L)
                    continue;

                potentials.Add(new EconomicProductionPotential(
                    point.Id, commodity, allocation.Kind, rawRate));
            }
        }

        return _existing.Resolve(
            generatedWorld, world, potentials, workerDemandProfiles);
    }
}
