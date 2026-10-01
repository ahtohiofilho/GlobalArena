namespace GlobalArena.Runtime;

public sealed class StrategicStockEntry
{
    public CivilizationId Owner { get; }

    public CommodityId Commodity { get; }

    public long Quantity { get; }

    public StrategicStockEntry(
        CivilizationId owner,
        CommodityId commodity,
        long quantity)
    {
        if (!owner.IsValid)
        {
            throw new ArgumentException(
                "Stock owner must be a valid civilization identity.",
                nameof(owner));
        }

        if (!commodity.IsValid)
        {
            throw new ArgumentException(
                "Stock commodity must be a valid commodity identity.",
                nameof(commodity));
        }

        if (quantity < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Strategic stock quantity cannot be negative.");
        }

        Owner = owner;
        Commodity = commodity;
        Quantity = quantity;
    }
}
