using GlobalArena.Kernel;

namespace GlobalArena.Tests;

public sealed class EventIdTests
{
    [Fact]
    public void ValidOriginCommandAndSequenceCreateEventId()
    {
        var commandId = new CommandId(
            new TurnNumber(7UL),
            3UL);

        var eventId = new EventId(
            commandId,
            2UL);

        Assert.Equal(commandId, eventId.OriginCommandId);
        Assert.Equal(2UL, eventId.Sequence);
    }

    [Fact]
    public void ZeroSequenceIsRejected()
    {
        var commandId = new CommandId(
            new TurnNumber(1UL),
            1UL);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EventId(commandId, 0UL));
    }

    [Fact]
    public void SameOriginAndSequenceProduceEqualIds()
    {
        var commandId = new CommandId(
            new TurnNumber(12UL),
            5UL);

        var first = new EventId(commandId, 2UL);
        var second = new EventId(commandId, 2UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSequencesProduceDifferentIds()
    {
        var commandId = new CommandId(
            new TurnNumber(12UL),
            5UL);

        var first = new EventId(commandId, 1UL);
        var second = new EventId(commandId, 2UL);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentOriginsProduceDifferentIds()
    {
        var turn = new TurnNumber(12UL);

        var firstCommand = new CommandId(turn, 1UL);
        var secondCommand = new CommandId(turn, 2UL);

        var first = new EventId(firstCommand, 1UL);
        var second = new EventId(secondCommand, 1UL);

        Assert.NotEqual(first, second);
    }
}
