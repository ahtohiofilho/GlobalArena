using GlobalArena.Kernel;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class SimulationEventOrdererContractTests
{
    [Fact]
    public void OrdererCanReceiveEventsAndContextAndReturnExplicitOrder()
    {
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var first = CreateEvent(turn, commandSequence: 1UL, eventSequence: 1UL);
        var second = CreateEvent(turn, commandSequence: 2UL, eventSequence: 1UL);
        var third = CreateEvent(turn, commandSequence: 3UL, eventSequence: 1UL);

        IReadOnlyList<ISimulationEvent> events =
            new ISimulationEvent[]
            {
                first,
                second,
                third
            };

        var orderer = new TestEventOrderer(
            new ISimulationEvent[]
            {
                third,
                first,
                second
            });

        var ordered = orderer.Order(
            events,
            context);

        Assert.Same(events, orderer.Events);
        Assert.Equal(context, orderer.Context);

        Assert.Equal(
            new ISimulationEvent[]
            {
                third,
                first,
                second
            },
            ordered);
    }

    [Fact]
    public void OrdererCanReturnNewOrderWithoutMutatingInput()
    {
        var turn = new TurnNumber(2UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(456UL));

        var first = CreateEvent(turn, commandSequence: 1UL, eventSequence: 1UL);
        var second = CreateEvent(turn, commandSequence: 2UL, eventSequence: 1UL);

        var input = new ISimulationEvent[]
        {
            first,
            second
        };

        IReadOnlyList<ISimulationEvent> events = input;

        var orderer = new TestEventOrderer(
            new ISimulationEvent[]
            {
                second,
                first
            });

        var ordered = orderer.Order(
            events,
            context);

        Assert.Equal(
            new ISimulationEvent[]
            {
                first,
                second
            },
            input);

        Assert.Equal(
            new ISimulationEvent[]
            {
                second,
                first
            },
            ordered);
    }

    private static TestEvent CreateEvent(
        TurnNumber turn,
        ulong commandSequence,
        ulong eventSequence)
    {
        return new TestEvent(
            new EventId(
                new CommandId(
                    turn,
                    commandSequence),
                eventSequence));
    }

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestEventOrderer
        : ISimulationEventOrderer
    {
        private readonly IReadOnlyList<ISimulationEvent> _orderedEvents;

        public TestEventOrderer(
            IReadOnlyList<ISimulationEvent> orderedEvents)
        {
            _orderedEvents = orderedEvents;
        }

        public IReadOnlyList<ISimulationEvent>? Events { get; private set; }

        public SimulationContext Context { get; private set; }

        public IReadOnlyList<ISimulationEvent> Order(
            IReadOnlyList<ISimulationEvent> events,
            SimulationContext context)
        {
            Events = events;
            Context = context;

            return _orderedEvents;
        }
    }
}
