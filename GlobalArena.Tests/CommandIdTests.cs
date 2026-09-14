using GlobalArena.Kernel;

namespace GlobalArena.Tests;

public sealed class CommandIdTests
{
    [Fact]
    public void ValidTurnAndSequenceCreateCommandId()
    {
        var id = new CommandId(
            new TurnNumber(7UL),
            3UL);

        Assert.Equal(7UL, id.Turn.Value);
        Assert.Equal(3UL, id.Sequence);
    }

    [Fact]
    public void ZeroSequenceIsRejected()
    {
        var turn = new TurnNumber(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommandId(turn, 0UL));
    }

    [Fact]
    public void SameTurnAndSequenceProduceEqualIds()
    {
        var first = new CommandId(
            new TurnNumber(12UL),
            5UL);

        var second = new CommandId(
            new TurnNumber(12UL),
            5UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSequencesProduceDifferentIds()
    {
        var turn = new TurnNumber(12UL);

        var first = new CommandId(turn, 1UL);
        var second = new CommandId(turn, 2UL);

        Assert.NotEqual(first, second);
    }
}
