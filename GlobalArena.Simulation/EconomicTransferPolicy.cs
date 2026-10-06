using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class EconomicTransferPolicy
{
    public const ulong FixedPointDenominator =
        (ulong)StrategicScalarField.Denominator;

    public ulong RawCostPerHop { get; }

    public ulong RawBaseRouteCapacity { get; }

    public ulong RawCapacityPerTradeLogisticsWorker { get; }

    public ulong RawCongestionUnitCostAtCapacity { get; }

    public EconomicTransferPolicy(
        ulong rawCostPerHop,
        ulong rawBaseRouteCapacity,
        ulong rawCapacityPerTradeLogisticsWorker,
        ulong rawCongestionUnitCostAtCapacity)
    {
        if (rawCostPerHop == 0UL
            || rawCostPerHop > FixedPointDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawCostPerHop),
                $"Transfer cost per hop must be in the inclusive range 1..{FixedPointDenominator}.");
        }

        if (rawBaseRouteCapacity == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawBaseRouteCapacity),
                "Base route capacity must be positive.");
        }

        if (rawCongestionUnitCostAtCapacity
            > FixedPointDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawCongestionUnitCostAtCapacity),
                $"Congestion unit-cost scale must be in the inclusive range 0..{FixedPointDenominator}.");
        }

        RawCostPerHop =
            rawCostPerHop;

        RawBaseRouteCapacity =
            rawBaseRouteCapacity;

        RawCapacityPerTradeLogisticsWorker =
            rawCapacityPerTradeLogisticsWorker;

        RawCongestionUnitCostAtCapacity =
            rawCongestionUnitCostAtCapacity;
    }
}
