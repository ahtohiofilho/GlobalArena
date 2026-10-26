using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// Transient report comparing explicit local-access observations to food
/// demand. This reports data supplied by a caller; it is not proof of
/// delivery/ingestion, a food inventory mutation or a health outcome.
/// </summary>
public sealed class EconomicFoodAccessCoverageResolvedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public FoodDemandAccessCoverageResult Coverage { get; }

    public EconomicFoodAccessCoverageResolvedEvent(
        EventId id,
        FoodDemandAccessCoverageResult coverage)
    {
        if (id.OriginKind != EventOriginKind.System)
            throw new ArgumentException(
                "Food access reports require system-origin events.",
                nameof(id));
        ArgumentNullException.ThrowIfNull(coverage);
        Id = id;
        Coverage = coverage;
    }
}
