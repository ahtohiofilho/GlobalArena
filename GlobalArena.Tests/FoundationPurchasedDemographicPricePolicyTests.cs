using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Tests;

public sealed class FoundationPurchasedDemographicPricePolicyTests
{
    [Fact]
    public void R236AProgressivePriceUsesHistoricalIndexAndNotLivingCensus()
    {
        var policy = new FoundationPurchasedDemographicPricePolicy(5UL, 3UL);
        Assert.Equal(5UL, policy.QuoteMarginalMoneyMicro(1UL));
        Assert.Equal(7UL, policy.QuoteMarginalMoneyMicro(2UL));
        Assert.Equal(9UL, policy.QuoteMarginalMoneyMicro(3UL));
        Assert.Equal(10UL, policy.QuoteMarginalMoneyMicro(4UL));
        Assert.Equal(12UL, policy.QuoteMarginalMoneyMicro(5UL));
        Assert.Equal(43UL, policy.QuoteBatchMoneyMicro(1UL, 5UL));
        Assert.Equal(43UL, policy.QuoteBatchMoneyMicro(1UL, 2UL) +
            policy.QuoteBatchMoneyMicro(3UL, 3UL));
        Assert.Equal(0UL, policy.QuoteBatchMoneyMicro(1UL, 0UL));
    }

    [Theory]
    [InlineData(1UL, 1UL)]
    [InlineData(5UL, 3UL)]
    [InlineData(7UL, 13UL)]
    [InlineData(1000001UL, 97UL)]
    public void R236AClosedFormQuoteMatchesIndependentMarginalSum(ulong initial, ulong reference)
    {
        var policy = new FoundationPurchasedDemographicPricePolicy(initial, reference);
        for (ulong start = 1UL; start <= 20UL; start++)
        {
            ulong total = 0UL;
            for (ulong quantity = 1UL; quantity <= 50UL; quantity++)
            {
                ulong index = start + quantity - 1UL;
                ulong expected = (initial * (reference + index - 1UL) + reference - 1UL) / reference;
                total += expected;
                Assert.Equal(expected, policy.QuoteMarginalMoneyMicro(index));
                Assert.Equal(total, policy.QuoteBatchMoneyMicro(start, quantity));
            }
        }
    }

    [Fact]
    public void R236ALargeBatchesAndOverflowAreExplicit()
    {
        var policy = new FoundationPurchasedDemographicPricePolicy(1UL, 1UL);
        Assert.Equal(500_000_000_500_000_000UL, policy.QuoteBatchMoneyMicro(1UL, 1_000_000_000UL));
        Assert.Equal(ulong.MaxValue, policy.QuoteMarginalMoneyMicro(ulong.MaxValue));
        Assert.Throws<OverflowException>(() => policy.QuoteBatchMoneyMicro(ulong.MaxValue, 2UL));
        Assert.Throws<OverflowException>(() => policy.QuoteBatchMoneyMicro(1UL, ulong.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.QuoteBatchMoneyMicro(0UL, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationPurchasedDemographicPricePolicy(0UL, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationPurchasedDemographicPricePolicy(1UL, 0UL));
    }
}
