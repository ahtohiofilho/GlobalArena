using GlobalArena.Kernel;

namespace GlobalArena.Tests;

public sealed class TurnNumberTests
{
    [Fact]
    public void PositiveValueCreatesTurnNumber()
    {
        var turn = new TurnNumber(1UL);

        Assert.Equal(1UL, turn.Value);
    }

    [Fact]
    public void ZeroIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TurnNumber(0UL));
    }

    [Fact]
    public void NextAdvancesExactlyOneTurn()
    {
        var current = new TurnNumber(41UL);

        var next = current.Next();

        Assert.Equal(42UL, next.Value);
    }

    [Fact]
    public void MaximumValueCannotAdvance()
    {
        var turn = new TurnNumber(ulong.MaxValue);

        Assert.Throws<InvalidOperationException>(
            () => turn.Next());
    }
}
