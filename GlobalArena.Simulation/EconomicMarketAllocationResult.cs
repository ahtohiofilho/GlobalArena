namespace GlobalArena.Simulation;

public sealed class EconomicMarketAllocationResult
{
    private readonly EconomicTradeAllocation[] _allocations;

    private readonly IReadOnlyList<EconomicTradeAllocation> _readOnlyAllocations;

    public IReadOnlyList<EconomicTradeAllocation> Allocations =>
        _readOnlyAllocations;

    public ulong TotalRawAllocatedQuantity { get; }

    public ulong TotalRawGrossSettlementValue { get; }

    public ulong TotalRawTransferSettlementCost { get; }

    public ulong TotalRawNetSettlementReturn { get; }

    public EconomicMarketAllocationResult(
        IEnumerable<EconomicTradeAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(
            allocations);

        var canonical =
            allocations.ToArray();

        if (canonical.Any(
            allocation =>
                allocation is null))
        {
            throw new ArgumentException(
                "Market allocation result cannot contain null allocations.",
                nameof(allocations));
        }

        canonical =
            canonical
                .OrderBy(
                    allocation =>
                        allocation.CommodityId.Value)
                .ThenBy(
                    allocation =>
                        allocation.SourceEconomicPointId.Value)
                .ThenBy(
                    allocation =>
                        (byte)allocation.SourceActivity)
                .ThenBy(
                    allocation =>
                        allocation.DestinationEconomicPointId.Value)
                .ThenByDescending(
                    allocation =>
                        allocation.RawUnitValue)
                .ThenBy(
                    allocation =>
                        allocation.RawUnitTransferCost)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            var previous =
                canonical[index - 1];

            var current =
                canonical[index];

            if (previous.CommodityId == current.CommodityId
                && previous.SourceEconomicPointId == current.SourceEconomicPointId
                && previous.SourceActivity == current.SourceActivity
                && previous.DestinationEconomicPointId == current.DestinationEconomicPointId
                && previous.RawUnitValue == current.RawUnitValue
                && previous.RawUnitTransferCost == current.RawUnitTransferCost)
            {
                throw new ArgumentException(
                    "Market allocation result cannot contain duplicate canonical allocation keys.",
                    nameof(allocations));
            }
        }

        UInt128 totalQuantity = 0U;
        UInt128 totalGross = 0U;
        UInt128 totalTransfer = 0U;
        UInt128 totalNet = 0U;

        foreach (var allocation in
            canonical)
        {
            totalQuantity +=
                allocation.RawQuantity;

            totalGross +=
                allocation.RawGrossSettlementValue;

            totalTransfer +=
                allocation.RawTransferSettlementCost;

            totalNet +=
                allocation.RawNetSettlementReturn;
        }

        if (totalQuantity > ulong.MaxValue
            || totalGross > ulong.MaxValue
            || totalTransfer > ulong.MaxValue
            || totalNet > ulong.MaxValue)
        {
            throw new OverflowException(
                "Market allocation aggregate totals exceed UInt64 range.");
        }

        _allocations =
            canonical;

        _readOnlyAllocations =
            Array.AsReadOnly(
                _allocations);

        TotalRawAllocatedQuantity =
            (ulong)totalQuantity;

        TotalRawGrossSettlementValue =
            (ulong)totalGross;

        TotalRawTransferSettlementCost =
            (ulong)totalTransfer;

        TotalRawNetSettlementReturn =
            (ulong)totalNet;
    }
}
