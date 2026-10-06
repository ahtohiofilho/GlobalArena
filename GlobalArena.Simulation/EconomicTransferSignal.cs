namespace GlobalArena.Simulation;

public sealed class EconomicTransferSignal
{
    public StrategicEconomicRouteKey RouteKey { get; }

    public int HopCount { get; }

    public ulong RawBaseUnitCost { get; }

    public ulong RawEffectiveCapacity { get; }

    public ulong RawCongestionUnitCostAtCapacity { get; }

    public EconomicTransferSignal(
        StrategicEconomicRouteKey routeKey,
        int hopCount,
        ulong rawBaseUnitCost,
        ulong rawEffectiveCapacity,
        ulong rawCongestionUnitCostAtCapacity)
    {
        if (!routeKey.SourceEconomicPointId.IsValid
            || !routeKey.DestinationEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Transfer signal route key must contain valid EconomicPoint identities.",
                nameof(routeKey));
        }

        if (hopCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hopCount),
                "Transfer signal hop count cannot be negative.");
        }

        if (rawEffectiveCapacity == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawEffectiveCapacity),
                "Transfer signal effective capacity must be positive.");
        }

        if (rawCongestionUnitCostAtCapacity
            > EconomicTransferPolicy.FixedPointDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawCongestionUnitCostAtCapacity),
                "Transfer signal congestion scale must use the normalized fixed-point range.");
        }

        RouteKey =
            routeKey;

        HopCount =
            hopCount;

        RawBaseUnitCost =
            rawBaseUnitCost;

        RawEffectiveCapacity =
            rawEffectiveCapacity;

        RawCongestionUnitCostAtCapacity =
            rawCongestionUnitCostAtCapacity;
    }

    public ulong GetRawMarginalUnitCost(
        ulong rawProposedLoad)
    {
        if (rawProposedLoad
            > RawEffectiveCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawProposedLoad),
                "Proposed route load cannot exceed effective capacity.");
        }

        var rawSurcharge =
            ((UInt128)RawCongestionUnitCostAtCapacity
                * rawProposedLoad)
            / RawEffectiveCapacity;

        var rawTotal =
            (UInt128)RawBaseUnitCost
            + rawSurcharge;

        if (rawTotal
            > ulong.MaxValue)
        {
            throw new OverflowException(
                "Transfer marginal unit cost exceeds UInt64 range.");
        }

        return (ulong)rawTotal;
    }
}
