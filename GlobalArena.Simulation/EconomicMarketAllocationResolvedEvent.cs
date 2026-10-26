using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// Auditable transient market outcome. Settlement quantities describe an
/// allocation decision; no treasury, inventory or resident state is mutated.
/// </summary>
public sealed class EconomicMarketAllocationResolvedEvent : ISimulationEvent
{
    public EventId Id { get; }

    public EconomicMarketAllocationResult Allocation { get; }

    public EconomicMarketAllocationResolvedEvent(
        EventId id,
        EconomicMarketAllocationResult allocation)
    {
        if (id.OriginKind != EventOriginKind.System)
            throw new ArgumentException(
                "Periodic market allocation requires a system-origin event.",
                nameof(id));

        ArgumentNullException.ThrowIfNull(allocation);

        Id = id;
        Allocation = allocation;
    }
}
