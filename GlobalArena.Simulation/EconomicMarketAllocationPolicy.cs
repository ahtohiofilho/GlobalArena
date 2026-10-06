namespace GlobalArena.Simulation;

public sealed class EconomicMarketAllocationPolicy
{
    public const int MaxBandCount = 64;

    public ulong RawInitialUnitValue { get; }

    public ulong RawFloorUnitValue { get; }

    public int DemandBandCount { get; }

    public int TransferBandCount { get; }

    public EconomicMarketAllocationPolicy(
        ulong rawInitialUnitValue,
        ulong rawFloorUnitValue,
        int demandBandCount,
        int transferBandCount)
    {
        if (rawInitialUnitValue == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawInitialUnitValue),
                "Initial market unit value must be positive.");
        }

        if (rawFloorUnitValue
            > rawInitialUnitValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawFloorUnitValue),
                "Market floor unit value cannot exceed initial unit value.");
        }

        if (demandBandCount <= 0
            || demandBandCount > MaxBandCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(demandBandCount),
                $"Demand band count must be in the inclusive range 1..{MaxBandCount}.");
        }

        if (transferBandCount <= 0
            || transferBandCount > MaxBandCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transferBandCount),
                $"Transfer band count must be in the inclusive range 1..{MaxBandCount}.");
        }

        RawInitialUnitValue =
            rawInitialUnitValue;

        RawFloorUnitValue =
            rawFloorUnitValue;

        DemandBandCount =
            demandBandCount;

        TransferBandCount =
            transferBandCount;
    }
}
