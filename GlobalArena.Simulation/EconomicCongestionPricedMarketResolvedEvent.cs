using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// System-origin, transient, exact convex-cost market selection outcome.
/// Distinct from legacy EconomicMarketAllocationResolvedEvent, whose flat
/// transfer-cost contract cannot represent incremental convex pricing.
/// </summary>
public sealed class EconomicCongestionPricedMarketResolvedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public EconomicCongestionMarketResult Market { get; }

    public EconomicCongestionPricedMarketResolvedEvent(
        EventId id, EconomicCongestionMarketResult market)
    {
        if (id.OriginKind != EventOriginKind.System)
            throw new ArgumentException(
                "Congestion-priced market events require a system origin.", nameof(id));
        ArgumentNullException.ThrowIfNull(market);
        Id = id;
        Market = market;
    }
}
