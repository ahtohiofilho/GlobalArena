using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public readonly record struct StrategicEconomicRouteKey
{
    public EconomicPointId SourceEconomicPointId { get; }

    public EconomicPointId DestinationEconomicPointId { get; }

    public StrategicEconomicRouteKey(
        EconomicPointId sourceEconomicPointId,
        EconomicPointId destinationEconomicPointId)
    {
        if (!sourceEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Strategic economic route source point identity must be valid.",
                nameof(sourceEconomicPointId));
        }

        if (!destinationEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Strategic economic route destination point identity must be valid.",
                nameof(destinationEconomicPointId));
        }

        SourceEconomicPointId = sourceEconomicPointId;
        DestinationEconomicPointId = destinationEconomicPointId;
    }
}
