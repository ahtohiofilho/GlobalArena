namespace GlobalArena.Simulation;

/// <summary>
/// Immutable trade-specific attestation view. Aggregate quantities are
/// separated BY FOOD COMMODITY, because abstract flow units for different
/// commodities have different caller-defined physical mass scales.
/// </summary>
public sealed class FoodTradeDeliveryReconciliationResult
{
    public IReadOnlyList<FoodTradeDeliveryReconciliationEntry> Entries { get; }
    public IReadOnlyList<FoodTradeDeliveryCommoditySummary> CommoditySummaries { get; }

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
        foreach (var row in rows)
        {
            if (!keys.Add(FoodTradeAllocationIdentity.From(row.Allocation)))
                throw new ArgumentException(
                    "Delivery report has duplicated market allocation identities.",
                    nameof(entries));
        }

        var summaries = new List<FoodTradeDeliveryCommoditySummary>();
        foreach (var group in rows.GroupBy(row => row.Allocation.CommodityId))
        {
            UInt128 total = 0U;
            UInt128 observed = 0U;
            UInt128 delivered = 0U;

            foreach (var row in group)
            {
                total += row.Allocation.RawQuantity;
                if (row.HasAttestation)
                {
                    observed += row.Allocation.RawQuantity;
                    delivered += row.RawAttestedDeliveredQuantity!.Value;
                }
            }

            if (total > ulong.MaxValue ||
                observed > ulong.MaxValue ||
                delivered > ulong.MaxValue)
                throw new OverflowException(
                    "Food delivery quantities exceed UInt64 within one commodity.");

            summaries.Add(new FoodTradeDeliveryCommoditySummary(
                group.Key, (ulong)total, (ulong)observed, (ulong)delivered));
        }

        Entries = Array.AsReadOnly(rows);
        CommoditySummaries = Array.AsReadOnly(summaries.ToArray());
    }
}
