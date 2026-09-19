using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class SeededSimulationEventOrderer
    : ISimulationEventOrderer
{
    public IReadOnlyList<ISimulationEvent> Order(
        IReadOnlyList<ISimulationEvent> events,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return Array.Empty<ISimulationEvent>();
        }

        var ordered = events.ToArray();

        if (ordered.Length == 1)
        {
            return Array.AsReadOnly(ordered);
        }

        var random = new DeterministicRandom(
            context.Seed);

        for (var index = ordered.Length - 1; index > 0; index--)
        {
            var swapIndex = NextIndex(
                random,
                index + 1);

            (ordered[index], ordered[swapIndex]) =
                (ordered[swapIndex], ordered[index]);
        }

        return Array.AsReadOnly(ordered);
    }

    private static int NextIndex(
        DeterministicRandom random,
        int exclusiveUpperBound)
    {
        var bound = (ulong)exclusiveUpperBound;

        var threshold =
            unchecked(0UL - bound) % bound;

        while (true)
        {
            var value = random.NextUInt64();

            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }
}
