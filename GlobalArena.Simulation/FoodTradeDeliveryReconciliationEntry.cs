namespace GlobalArena.Simulation;

/// <summary>
/// Comparison for one registered-food market allocation. Null delivered
/// quantity means no attestation was supplied; explicit zero is distinct.
/// Neither case automatically changes the residents' observed food access.
/// </summary>
public sealed class FoodTradeDeliveryReconciliationEntry
{
    public EconomicTradeAllocation Allocation { get; }
    public bool HasAttestation { get; }
    public ulong? RawAttestedDeliveredQuantity { get; }
    public ulong? RawAllocationWithoutAttestedDelivery { get; }
    public string? EvidenceSourceKey { get; }

    public FoodTradeDeliveryReconciliationEntry(
        EconomicTradeAllocation allocation,
        FoodTradeDeliveryAttestation? attestation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        Allocation = allocation;

        if (attestation is null)
            return;

        if (FoodTradeAllocationIdentity.From(attestation.ReferencedAllocation)
                != FoodTradeAllocationIdentity.From(allocation) ||
            attestation.ReferencedAllocation.RawQuantity != allocation.RawQuantity)
            throw new ArgumentException(
                "Delivery attestation must exactly identify the market allocation.",
                nameof(attestation));

        if (attestation.RawAttestedDeliveredQuantity > allocation.RawQuantity)
            throw new ArgumentOutOfRangeException(
                nameof(attestation),
                "Attested delivery exceeds the selected market allocation.");

        HasAttestation = true;
        RawAttestedDeliveredQuantity = attestation.RawAttestedDeliveredQuantity;
        RawAllocationWithoutAttestedDelivery =
            allocation.RawQuantity - attestation.RawAttestedDeliveredQuantity;
        EvidenceSourceKey = attestation.EvidenceSourceKey;
    }
}
