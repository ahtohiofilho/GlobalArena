using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Aggregate within a single food commodity's abstract flow-unit scale.
/// Totals for different commodities must not be summed as physical mass
/// because each food can define a different GramsPerFlowUnit conversion.
/// </summary>
public sealed class FoodTradeDeliveryCommoditySummary
{
    public CommodityId CommodityId { get; }
    public ulong RawAllocated { get; }
    public ulong RawAllocatedWithAttestation { get; }
    public ulong RawAllocatedWithUnknownDelivery { get; }
    public ulong RawAttestedDelivered { get; }
    public ulong RawAllocatedNotAttestedDelivered { get; }

    public FoodTradeDeliveryCommoditySummary(
        CommodityId commodityId,
        ulong rawAllocated,
        ulong rawAllocatedWithAttestation,
        ulong rawAttestedDelivered)
    {
        if (!commodityId.IsValid)
            throw new ArgumentException("A food commodity identity is required.", nameof(commodityId));
        if (rawAllocatedWithAttestation > rawAllocated)
            throw new ArgumentOutOfRangeException(nameof(rawAllocatedWithAttestation));
        if (rawAttestedDelivered > rawAllocatedWithAttestation)
            throw new ArgumentOutOfRangeException(nameof(rawAttestedDelivered));

        CommodityId = commodityId;
        RawAllocated = rawAllocated;
        RawAllocatedWithAttestation = rawAllocatedWithAttestation;
        RawAllocatedWithUnknownDelivery = rawAllocated - rawAllocatedWithAttestation;
        RawAttestedDelivered = rawAttestedDelivered;
        RawAllocatedNotAttestedDelivered = rawAllocatedWithAttestation - rawAttestedDelivered;
    }
}
