using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit food identity and physical mass scale for a whole abstract
/// commodity flow unit. GramsPerFlowUnit is caller-provided: flow units are
/// not assumed to be kilograms or tonnes by convention. This does not define
/// caloric value, actual consumption, or persistent physical inventory.
/// </summary>
public sealed class FoodCommodityDefinition
{
    public CommodityId CommodityId { get; }
    public string StableKey { get; }
    public ulong GramsPerFlowUnit { get; }

    public FoodCommodityDefinition(
        CommodityId commodityId,
        string stableKey,
        ulong gramsPerFlowUnit)
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

        if (gramsPerFlowUnit == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(gramsPerFlowUnit),
                "Food mass per whole flow unit must be positive.");

        CommodityId = commodityId;
        StableKey = stableKey;
        GramsPerFlowUnit = gramsPerFlowUnit;
    }
}
