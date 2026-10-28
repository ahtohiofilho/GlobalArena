using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

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

