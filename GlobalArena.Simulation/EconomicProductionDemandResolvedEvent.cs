using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// Auditable, transient outcome of one periodic production/demand evaluation.
/// This event carries flows for downstream turn phases but does not represent
/// an inventory, financial settlement, or a resident WorldState mutation.
/// </summary>
public sealed class EconomicProductionDemandResolvedEvent : ISimulationEvent
{
    public EventId Id { get; }

    public EconomicProductionDemandResult Flows { get; }

    public EconomicProductionDemandResolvedEvent(
        EventId id,
        EconomicProductionDemandResult flows)
    {
        if (id.OriginKind != EventOriginKind.System)
            throw new ArgumentException(
                "Periodic economic flows require a system-origin event.",
                nameof(id));

        ArgumentNullException.ThrowIfNull(flows);

        Id = id;
        Flows = flows;
    }
}
