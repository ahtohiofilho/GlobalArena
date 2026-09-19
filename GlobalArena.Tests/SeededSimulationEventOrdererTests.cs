using GlobalArena.Kernel;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class SeededSimulationEventOrdererTests
{
    [Fact]
    public void SameInputAndContextProducesSameOrderAcrossCalls()
    {
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var events = CreateEvents(
            turn,
            count: 8);

        var orderer = new SeededSimulationEventOrderer();

        var first = orderer.Order(
            events,
            context);

        var second = orderer.Order(
            events,
            context);

        Assert.Equal(
            first.Select(simulationEvent => simulationEvent.Id),
            second.Select(simulationEvent => simulationEvent.Id));
    }

    [Fact]
    public void SeedZeroProducesStableReferenceOrder()
    {
        var turn = new TurnNumber(2UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(0UL));

        var events = CreateEvents(
            turn,
            count: 5);

        var orderer = new SeededSimulationEventOrderer();

        var ordered = orderer.Order(
            events,
            context);

        Assert.Equal(
            new ulong[]
            {
                3UL,
                4UL,
                2UL,
                5UL,
                1UL
            },
            ordered.Select(
                simulationEvent =>
                    simulationEvent.Id.OriginCommandId.Sequence));
    }

    [Fact]
    public void OrderingPreservesMembershipAndDoesNotMutateInput()
    {
        var turn = new TurnNumber(3UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(456UL));

        var input = CreateEvents(
            turn,
            count: 8).ToArray();

        var snapshot = input.ToArray();

        var orderer = new SeededSimulationEventOrderer();

        var ordered = orderer.Order(
            input,
            context);

        Assert.Equal(
            snapshot.Select(simulationEvent => simulationEvent.Id),
            input.Select(simulationEvent => simulationEvent.Id));

        Assert.Equal(
            snapshot.Length,
            ordered.Count);

        Assert.Equal(
            snapshot
                .Select(simulationEvent => simulationEvent.Id)
                .OrderBy(id => id.OriginCommandId.Sequence)
                .ThenBy(id => id.Sequence),
            ordered
                .Select(simulationEvent => simulationEvent.Id)
                .OrderBy(id => id.OriginCommandId.Sequence)
                .ThenBy(id => id.Sequence));
    }

    [Fact]
    public void EmptyCollectionProducesEmptyOrder()
    {
        var context = new SimulationContext(
            new TurnNumber(4UL),
            new SimulationSeed(789UL));

        var orderer = new SeededSimulationEventOrderer();

        var ordered = orderer.Order(
            Array.Empty<ISimulationEvent>(),
            context);

        Assert.Empty(ordered);
    }

    [Fact]
    public void SingleEventRemainsSingle()
    {
        var turn = new TurnNumber(5UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(101112UL));

        var simulationEvent = CreateEvent(
            turn,
            commandSequence: 1UL);

        var orderer = new SeededSimulationEventOrderer();

        var ordered = orderer.Order(
            new ISimulationEvent[]
            {
                simulationEvent
            },
            context);

        Assert.Same(
            simulationEvent,
            Assert.Single(ordered));
    }

    [Fact]
    public void NullEventsAreRejected()
    {
        var context = new SimulationContext(
            new TurnNumber(6UL),
            new SimulationSeed(131415UL));

        var orderer = new SeededSimulationEventOrderer();

        Assert.Throws<ArgumentNullException>(
            () => orderer.Order(
                null!,
                context));
    }

    private static IReadOnlyList<ISimulationEvent> CreateEvents(
        TurnNumber turn,
        int count)
    {
        var events = new ISimulationEvent[count];

        for (var index = 0; index < count; index++)
        {
            events[index] = CreateEvent(
                turn,
                (ulong)index + 1UL);
        }

        return events;
    }

    private static TestEvent CreateEvent(
        TurnNumber turn,
        ulong commandSequence)
    {
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
