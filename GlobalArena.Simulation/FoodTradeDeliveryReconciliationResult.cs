namespace GlobalArena.Simulation;

/// <summary>
/// Read-only per-allocation delivery-attestation coverage. Food without a
/// recorded market trade is not represented here. No field denotes actual
/// consumption or independently verified shipment delivery.
/// </summary>
public sealed class FoodTradeDeliveryReconciliationResult
{
    public IReadOnlyList<FoodTradeDeliveryReconciliationEntry> Entries { get; }

    public ulong TotalRawFoodTradeAllocated { get; }
    public ulong RawAllocationWithAttestation { get; }
    public ulong RawAllocationWithoutAttestation { get; }
    public ulong TotalRawAttestedDelivered { get; }
    public ulong RawAllocationNotAttestedDelivered { get; }

    public FoodTradeDeliveryReconciliationResult(
        IEnumerable<FoodTradeDeliveryReconciliationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var rows = entries.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Delivery report cannot contain null rows.",
                nameof(entries));

        rows = rows
            .OrderBy(row => row.Allocation.CommodityId.Value)
            .ThenBy(row => row.Allocation.SourceEconomicPointId.Value)
            .ThenBy(row => (byte)row.Allocation.SourceActivity)
            .ThenBy(row => row.Allocation.DestinationEconomicPointId.Value)
            .ThenByDescending(row => row.Allocation.RawUnitValue)
            .ThenBy(row => row.Allocation.RawUnitTransferCost)
            .ToArray();

        var keys = new HashSet<FoodTradeAllocationIdentity>();
        UInt128 total = 0U;
        UInt128 observed = 0U;
        UInt128 delivered = 0U;

        foreach (var row in rows)
        {
            if (!keys.Add(FoodTradeAllocationIdentity.From(row.Allocation)))
                throw new ArgumentException(
                    "Delivery report has duplicated market allocation identities.",
                    nameof(entries));

            total += row.Allocation.RawQuantity;
            if (row.HasAttestation)
            {
                observed += row.Allocation.RawQuantity;
                delivered += row.RawAttestedDeliveredQuantity!.Value;
            }
        }

        if (total > ulong.MaxValue || observed > ulong.MaxValue ||
            delivered > ulong.MaxValue)
            throw new OverflowException(
                "Delivery reconciliation aggregates exceed the UInt64 range.");

        Entries = Array.AsReadOnly(rows);
        TotalRawFoodTradeAllocated = (ulong)total;
        RawAllocationWithAttestation = (ulong)observed;
        RawAllocationWithoutAttestation =
            TotalRawFoodTradeAllocated - RawAllocationWithAttestation;
        TotalRawAttestedDelivered = (ulong)delivered;
        RawAllocationNotAttestedDelivered =
            RawAllocationWithAttestation - TotalRawAttestedDelivered;
    }
}
