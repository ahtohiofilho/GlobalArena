using GlobalArena.Kernel;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class SimulationEventLogContractTests
{
    [Fact]
    public void EntryPreservesEventIdentityResolutionOrderAndOutcome()
    {
        var turn = new TurnNumber(1UL);
        var commandId = new CommandId(
            turn,
            1UL);

        var simulationEvent = new TestEvent(
            new EventId(
                commandId,
                2UL));

        var entry = new SimulationEventLogEntry(
            resolutionSequence: 3UL,
            simulationEvent,
            wasEligible: true,
            wasExecuted: true);

        Assert.Equal(
            3UL,
            entry.ResolutionSequence);

        Assert.Same(
            simulationEvent,
            entry.Event);

        Assert.Equal(
            simulationEvent.Id,
            entry.EventId);

        Assert.True(
            entry.WasEligible);

        Assert.True(
            entry.WasExecuted);
    }

    [Fact]
    public void RejectedEventCanBeRecordedWithoutExecution()
    {
        var entry = new SimulationEventLogEntry(
            resolutionSequence: 1UL,
            CreateEvent(),
            wasEligible: false,
            wasExecuted: false);

        Assert.False(
            entry.WasEligible);

        Assert.False(
            entry.WasExecuted);
    }

    [Fact]
    public void ExecutedEventCannotBeMarkedIneligible()
    {
        Assert.Throws<ArgumentException>(
            () => new SimulationEventLogEntry(
                resolutionSequence: 1UL,
                CreateEvent(),
                wasEligible: false,
                wasExecuted: true));
    }

    [Fact]
    public void ResolutionSequenceMustBeGreaterThanZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationEventLogEntry(
                resolutionSequence: 0UL,
                CreateEvent(),
                wasEligible: true,
                wasExecuted: true));
    }

    [Fact]
    public void EventCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulationEventLogEntry(
                resolutionSequence: 1UL,
                null!,
                wasEligible: false,
                wasExecuted: false));
    }

    [Fact]
    public void LogSnapshotsEntriesAndPreservesResolutionOrder()
    {
        var first = new SimulationEventLogEntry(
            resolutionSequence: 1UL,
            CreateEvent(
                commandSequence: 1UL),
            wasEligible: true,
            wasExecuted: true);

        var second = new SimulationEventLogEntry(
            resolutionSequence: 2UL,
            CreateEvent(
                commandSequence: 2UL),
            wasEligible: false,
            wasExecuted: false);

        var source =
            new List<SimulationEventLogEntry>
            {
                first,
                second
            };

        var log =
            new SimulationEventLog(source);

        source.Clear();

        Assert.Equal(
            2,
            log.Entries.Count);

        Assert.Same(
            first,
            log.Entries[0]);

        Assert.Same(
            second,
            log.Entries[1]);
    }

    [Fact]
    public void LogRequiresContiguousResolutionSequence()
    {
        var first = new SimulationEventLogEntry(
            resolutionSequence: 1UL,
            CreateEvent(
                commandSequence: 1UL),
            wasEligible: true,
            wasExecuted: true);

        var third = new SimulationEventLogEntry(
            resolutionSequence: 3UL,
            CreateEvent(
                commandSequence: 2UL),
            wasEligible: true,
            wasExecuted: true);

        Assert.Throws<ArgumentException>(
            () => new SimulationEventLog(
                new[]
                {
                    first,
                    third
                }));
    }

    [Fact]
    public void EmptyLogIsValid()
    {
        var log = new SimulationEventLog(
            Array.Empty<SimulationEventLogEntry>());

        Assert.Empty(
            log.Entries);
    }

    private static TestEvent CreateEvent(
        ulong commandSequence = 1UL)
    {
        var turn = new TurnNumber(1UL);

        return new TestEvent(
            new EventId(
                new CommandId(
                    turn,
                    commandSequence),
                1UL));
    }

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;
}
