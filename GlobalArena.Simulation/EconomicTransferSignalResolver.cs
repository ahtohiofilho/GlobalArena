using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class EconomicTransferSignalResolver
{
    public EconomicTransferSignal Resolve(
        EconomyRuntimeState economy,
        StrategicEconomicRoute route,
        EconomicTransferPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(
            economy);

        ArgumentNullException.ThrowIfNull(
            route);

        ArgumentNullException.ThrowIfNull(
            policy);

        var sourcePoint =
            FindPoint(
                economy,
                route.Key.SourceEconomicPointId,
                "source");

        var destinationPoint =
            FindPoint(
                economy,
                route.Key.DestinationEconomicPointId,
                "destination");

        if (sourcePoint.StrategicCellId
            != route.SourceStrategicCellId)
        {
            throw new ArgumentException(
                "Route source strategic cell does not match the source EconomicPoint anchor.",
                nameof(route));
        }

        if (destinationPoint.StrategicCellId
            != route.DestinationStrategicCellId)
        {
            throw new ArgumentException(
                "Route destination strategic cell does not match the destination EconomicPoint anchor.",
                nameof(route));
        }

        var sourceLogisticsWorkforce =
            GetTradeLogisticsWorkforce(
                sourcePoint);

        var destinationLogisticsWorkforce =
            sourcePoint.Id == destinationPoint.Id
                ? 0UL
                : GetTradeLogisticsWorkforce(
                    destinationPoint);

        var totalLogisticsWorkforce =
            (UInt128)sourceLogisticsWorkforce
            + destinationLogisticsWorkforce;

        var rawEffectiveCapacity =
            (UInt128)policy.RawBaseRouteCapacity
            + totalLogisticsWorkforce
                * policy.RawCapacityPerTradeLogisticsWorker;

        if (rawEffectiveCapacity
            > ulong.MaxValue)
        {
            throw new OverflowException(
                "Effective transfer capacity exceeds UInt64 range.");
        }

        var rawBaseUnitCost =
            (UInt128)(uint)route.HopCount
            * policy.RawCostPerHop;

        if (rawBaseUnitCost
            > ulong.MaxValue)
        {
            throw new OverflowException(
                "Base transfer unit cost exceeds UInt64 range.");
        }

        return new EconomicTransferSignal(
            route.Key,
            route.HopCount,
            (ulong)rawBaseUnitCost,
            (ulong)rawEffectiveCapacity,
            policy.RawCongestionUnitCostAtCapacity);
    }

    private static EconomicPointRuntimeState FindPoint(
        EconomyRuntimeState economy,
        EconomicPointId pointId,
        string role)
    {
        foreach (var point in
            economy.EconomicPoints)
        {
            if (point.Id
                == pointId)
            {
                return point;
            }
        }

        throw new KeyNotFoundException(
            $"Transfer route {role} EconomicPoint {pointId.Value} does not exist in the Economy state.");
    }

    private static ulong GetTradeLogisticsWorkforce(
        EconomicPointRuntimeState point)
    {
        foreach (var allocation in
            point.ActivityAllocations)
        {
            if (allocation.Kind
                == EconomicActivityKind.TradeLogistics)
            {
                return allocation.Workforce;
            }
        }

        return 0UL;
    }
}
