namespace GlobalArena.Simulation;

public sealed class SimulationEventLog
{
    public IReadOnlyList<SimulationEventLogEntry> Entries { get; }

    public SimulationEventLog(
        IEnumerable<SimulationEventLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var snapshot = entries.ToArray();

        for (var index = 0;
             index < snapshot.Length;
             index++)
        {
            var entry = snapshot[index];

            ArgumentNullException.ThrowIfNull(entry);

            var expectedSequence =
                (ulong)index + 1UL;

            if (entry.ResolutionSequence
                != expectedSequence)
            {
                throw new ArgumentException(
                    "Event log entries must use contiguous resolution sequence values starting at one.",
                    nameof(entries));
            }
        }

        Entries = Array.AsReadOnly(snapshot);
    }
}
