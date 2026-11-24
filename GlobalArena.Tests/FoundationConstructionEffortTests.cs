using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Tests;

public sealed class FoundationConstructionEffortTests
{
    [Theory]
    [InlineData(0UL, 300UL, 0UL)]
    [InlineData(200UL, 100UL, 0UL)]
    [InlineData(300UL, 0UL, 0UL)]
    [InlineData(500UL, 0UL, 200UL)]
    public void OfferedEffortIsConserved(ulong offered, ulong remaining, ulong unused)
    {
        var start = new FoundationConstructionProgress(2000000UL, 300UL);
        var result = start.Apply(offered);
        Assert.Equal(remaining, result.Progress.RemainingEffortMicro);
        Assert.Equal(unused, result.UnusedEffortMicro);
        Assert.Equal(offered, result.UsedEffortMicro + result.UnusedEffortMicro);
        Assert.Equal(300UL - remaining, result.Progress.CompletedEffortMicro);
        Assert.Equal(remaining == 0UL, result.Progress.IsComplete);
        Assert.Equal(start.FundedMoneyMicro, result.Progress.FundedMoneyMicro);
        Assert.Equal(0UL, start.CompletedEffortMicro);
    }

    [Fact]
    public void ProgressCarriesAcrossCallsAndCompletedProjectConsumesNothing()
    {
        var start = new FoundationConstructionProgress(2000000UL, 800UL);
        var first = start.Apply(500UL);
        var second = first.Progress.Apply(500UL);
        var third = second.Progress.Apply(500UL);
        Assert.False(first.Progress.IsComplete);
        Assert.Equal(300UL, first.Progress.RemainingEffortMicro);
        Assert.True(second.Progress.IsComplete);
        Assert.Equal(300UL, second.UsedEffortMicro);
        Assert.Equal(200UL, second.UnusedEffortMicro);
        Assert.Equal(0UL, third.UsedEffortMicro);
        Assert.Equal(500UL, third.UnusedEffortMicro);
        Assert.Equal(second.Progress, third.Progress);
    }

    [Fact]
    public void InvalidProgressIsRejectedAndExtremeEffortDoesNotOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationConstructionProgress(0UL, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationConstructionProgress(1UL, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationConstructionProgress(1UL, 1UL, 2UL));
        Assert.Throws<InvalidOperationException>(() => default(FoundationConstructionProgress).Apply(1UL));
        var result = new FoundationConstructionProgress(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue - 1UL)
            .Apply(ulong.MaxValue);
        Assert.True(result.Progress.IsComplete);
        Assert.Equal(1UL, result.UsedEffortMicro);
        Assert.Equal(ulong.MaxValue - 1UL, result.UnusedEffortMicro);
    }
}
