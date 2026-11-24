using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Tests;

public sealed class FoundationConstructionWorkPolicyTests
{
    [Theory]
    [InlineData(2_000_000UL, 1_000_000UL, 2_000_000UL)]
    [InlineData(2_000_000UL, 2_000_000UL, 1_000_000UL)]
    [InlineData(4_000_000UL, 2_000_000UL, 2_000_000UL)]
    [InlineData(1UL, 3_000_000UL, 1UL)]
    [InlineData(1_000_000UL, 3_000_000UL, 333_334UL)]
    [InlineData(ulong.MaxValue, 1_000_000UL, ulong.MaxValue)]
    public void QuoteUsesExplicitRateAndRoundsUp(ulong money, ulong rate, ulong expected)
    {
        var quote = FoundationConstructionWorkPolicy.Quote(money, rate);
        Assert.Equal(money, quote.FundedMoneyMicro);
        Assert.Equal(expected, quote.RequiredEffortMicro);
        Assert.Equal(0UL, quote.CompletedEffortMicro);
        Assert.False(quote.IsComplete);
    }

    [Fact]
    public void QuotedProjectPreservesResidualWork()
    {
        var quote = FoundationConstructionWorkPolicy.Quote(300UL, 1_000_000UL);
        var result = quote.Apply(500UL);
        Assert.True(result.Progress.IsComplete);
        Assert.Equal(300UL, result.UsedEffortMicro);
        Assert.Equal(200UL, result.UnusedEffortMicro);
    }

    [Fact]
    public void RejectsInvalidRateAndUnrepresentableWork()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationConstructionWorkPolicy.RequiredWorkMicro(0UL, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationConstructionWorkPolicy.RequiredWorkMicro(1UL, 0UL));
        Assert.Throws<OverflowException>(() =>
            FoundationConstructionWorkPolicy.RequiredWorkMicro(ulong.MaxValue, 1UL));
        Assert.Equal(1_000_000UL, FoundationConstructionWorkPolicy.NeutralIndividualProductivityMicro);
    }
}
