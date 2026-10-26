using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// Transient, auditable market-coverage observation, not a consumption or
/// mortality command and not a canonical WorldState mutation.
/// </summary>
public sealed class EconomicFoodMarketCoverageResolvedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public FoodMarketDemandCoverageResult Coverage { get; }

    public EconomicFoodMarketCoverageResolvedEvent(
        EventId id,
        FoodMarketDemandCoverageResult coverage)
    {
        if (id.OriginKind != EventOriginKind.System)
            throw new ArgumentException(
                "Food market coverage requires a system-origin event.",
                nameof(id));

        ArgumentNullException.ThrowIfNull(coverage);
        Id = id;
        Coverage = coverage;
    }
}
