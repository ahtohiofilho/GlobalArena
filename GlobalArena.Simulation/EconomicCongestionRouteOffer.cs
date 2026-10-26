namespace GlobalArena.Simulation;

/// <summary>
/// Directional economic transfer option with explicit base price per whole
/// commodity flow unit. Unlike legacy EconomicTransferSignal, there is no
/// RawEffectiveCapacity or congestion-at-capacity threshold. Geographic
/// strategic-edge congestion is priced separately and smoothly.
/// </summary>
public sealed class EconomicCongestionRouteOffer
{
    public StrategicEconomicRouteKey RouteKey { get; }
    public ulong RawBaseUnitTransferCost { get; }

    public EconomicCongestionRouteOffer(
        StrategicEconomicRouteKey routeKey,
        ulong rawBaseUnitTransferCost)
    {
        if (!routeKey.SourceEconomicPointId.IsValid ||
            !routeKey.DestinationEconomicPointId.IsValid)
            throw new ArgumentException("Route requires valid source and destination.", nameof(routeKey));

        RouteKey = routeKey;
        RawBaseUnitTransferCost = rawBaseUnitTransferCost;
    }
}
