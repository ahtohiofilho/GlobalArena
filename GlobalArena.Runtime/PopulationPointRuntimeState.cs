namespace GlobalArena.Runtime;

/// <summary>
/// Immutable authoritative resident headcount for one existing economic point.
/// Residency is not workforce, employment or a consumption-rate assumption.
/// </summary>
public sealed class PopulationPointRuntimeState
{
    public EconomicPointId EconomicPointId { get; }

    public ulong Residents { get; }

    public PopulationPointRuntimeState(
        EconomicPointId economicPointId,
        ulong residents)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Resident population requires a valid economic point identity.",
                nameof(economicPointId));

        EconomicPointId = economicPointId;
        Residents = residents;
    }
}
