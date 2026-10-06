using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class EconomicTradeAllocation
{
    public EconomicPointId SourceEconomicPointId { get; }

    public EconomicActivityKind SourceActivity { get; }

    public EconomicPointId DestinationEconomicPointId { get; }

    public CommodityId CommodityId { get; }

    public ulong RawQuantity { get; }

    public ulong RawUnitValue { get; }

    public ulong RawUnitTransferCost { get; }

    public ulong RawUnitNetReturn =>
        RawUnitValue - RawUnitTransferCost;

    public ulong RawGrossSettlementValue { get; }

    public ulong RawTransferSettlementCost { get; }

    public ulong RawNetSettlementReturn { get; }

    public EconomicTradeAllocation(
        EconomicPointId sourceEconomicPointId,
        EconomicActivityKind sourceActivity,
        EconomicPointId destinationEconomicPointId,
        CommodityId commodityId,
        ulong rawQuantity,
        ulong rawUnitValue,
        ulong rawUnitTransferCost)
    {
        if (!sourceEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Trade allocation source EconomicPoint identity must be valid.",
                nameof(sourceEconomicPointId));
        }

        if (sourceActivity != EconomicActivityKind.Agriculture
            && sourceActivity != EconomicActivityKind.Mining)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceActivity),
                "Trade allocation source activity must be Agriculture or Mining.");
        }

        if (!destinationEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Trade allocation destination EconomicPoint identity must be valid.",
                nameof(destinationEconomicPointId));
        }

        if (!commodityId.IsValid)
        {
            throw new ArgumentException(
                "Trade allocation Commodity identity must be valid.",
                nameof(commodityId));
        }

        if (rawQuantity == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawQuantity),
                "Trade allocation quantity must be positive.");
        }

        if (rawUnitValue
            <= rawUnitTransferCost)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawUnitValue),
                "Trade allocation requires strictly positive unit net return.");
        }

        var rawGross =
            ((UInt128)rawQuantity
                * rawUnitValue)
            / EconomicTransferPolicy.FixedPointDenominator;

        var rawTransfer =
            ((UInt128)rawQuantity
                * rawUnitTransferCost)
            / EconomicTransferPolicy.FixedPointDenominator;

        if (rawGross
            > ulong.MaxValue)
        {
            throw new OverflowException(
                "Trade allocation gross settlement value exceeds UInt64 range.");
        }

        if (rawTransfer
            > ulong.MaxValue)
        {
            throw new OverflowException(
                "Trade allocation transfer settlement cost exceeds UInt64 range.");
        }

        var gross =
            (ulong)rawGross;

        var transfer =
            (ulong)rawTransfer;

        if (transfer
            > gross)
        {
            throw new InvalidOperationException(
                "Positive unit return produced negative settlement return.");
        }

        SourceEconomicPointId =
            sourceEconomicPointId;

        SourceActivity =
            sourceActivity;

        DestinationEconomicPointId =
            destinationEconomicPointId;

        CommodityId =
            commodityId;

        RawQuantity =
            rawQuantity;

        RawUnitValue =
            rawUnitValue;

        RawUnitTransferCost =
            rawUnitTransferCost;

        RawGrossSettlementValue =
            gross;

        RawTransferSettlementCost =
            transfer;

        RawNetSettlementReturn =
            gross - transfer;
    }
}
