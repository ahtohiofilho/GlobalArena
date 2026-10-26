using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit food identity for the existing commodity-flow system. The food
/// baseline measures physical quantity in kilograms; one kilogram is one
/// fixed-point whole commodity unit. This is a declared interpretation for
/// inputs using this contract, not an inference about historical test flows.
/// It does not imply nutritional energy or consumption satisfaction.
/// </summary>
public sealed class FoodCommodityDefinition
{
    public const ulong RawQuantityPerKilogram =
        (ulong)StrategicScalarField.Denominator;

    public CommodityId CommodityId { get; }
    public string StableKey { get; }

    public FoodCommodityDefinition(CommodityId commodityId, string stableKey)
    {
        if (!commodityId.IsValid)
            throw new ArgumentException(
                "Food requires a valid commodity identity.",
                nameof(commodityId));

        ArgumentNullException.ThrowIfNull(stableKey);
        if (stableKey.Length == 0 ||
            stableKey.Any(c =>
                !(c is >= 'a' and <= 'z') &&
                !(c is >= '0' and <= '9') &&
                c != '-'))
            throw new ArgumentException(
                "Food stable keys must be nonempty lowercase ASCII letters, digits or hyphens.",
                nameof(stableKey));

        CommodityId = commodityId;
        StableKey = stableKey;
    }
}
