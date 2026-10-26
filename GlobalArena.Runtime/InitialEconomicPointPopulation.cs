namespace GlobalArena.Runtime;

/// <summary>
/// Explicit resident headcount at one already validated economic point.
/// Residents include workers; they must not be mistaken for workforce.
/// A zero headcount is valid for a point with zero workforce.
/// </summary>
public sealed class InitialEconomicPointPopulation
{
    public EconomicPointId EconomicPointId { get; }

    public ulong Residents { get; }

    public InitialEconomicPointPopulation(
        EconomicPointId economicPointId,
        ulong residents)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Initial local population requires a valid economic point identity.",
                nameof(economicPointId));

        EconomicPointId = economicPointId;
        Residents = residents;
    }
}
