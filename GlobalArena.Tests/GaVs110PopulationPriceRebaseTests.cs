using GlobalArena.Economy;

namespace GlobalArena.Tests;

/// <summary>
/// ECON-REBASE-01: the buyer-power index modifies effective DEMAND pressure,
/// never the quantity of food an individual must consume.
/// All price curves below are EXAMPLES FOR QA, not approved game policy.
/// </summary>
public sealed class GaVs110PopulationPriceRebaseTests
{
    private static decimal ExamplePrice(decimal x) => 2m / (1m + x);

    [Fact]
    public void NeutralPurchasingPowerIsIdenticalToPopulationOnlyPricing()
    {
        var supply = PhysicalOffer.FromAvailable(8m);
        var neutral = PopulationPriceKernel.Quote(
            8UL, supply, AbstractPurchasingPowerIndex.Neutral, ExamplePrice);
        var shorthand = PopulationPriceKernel.Quote(8UL, supply, ExamplePrice);

        Assert.Equal(shorthand, neutral);
        Assert.True(neutral.HasOffer);
        Assert.Equal(8UL, neutral.Population);
        Assert.Equal(1m, neutral.RelativePurchasingPower);
        Assert.Equal(8m, neutral.EffectiveDemand);
        Assert.Equal(8m, neutral.Offered);
        Assert.Equal(1m, neutral.SupplyDemandRatio);
        Assert.Equal(1m, neutral.Price);
        Assert.Equal(8m, neutral.ReferenceRevenue);
        Assert.False(neutral.IsExecutedSettlement);
    }

    [Fact]
    public void RicherPopulationExertsHigherDemandWithoutInventingResidents()
    {
        var supply = PhysicalOffer.FromAvailable(24m);
        var weak = PopulationPriceKernel.Quote(
            8UL, supply, AbstractPurchasingPowerIndex.FromRelativeCapacity(0.5m),
            ExamplePrice);
        var neutral = PopulationPriceKernel.Quote(
            8UL, supply, AbstractPurchasingPowerIndex.Neutral, ExamplePrice);
        var strong = PopulationPriceKernel.Quote(
            8UL, supply, AbstractPurchasingPowerIndex.FromRelativeCapacity(2m),
            ExamplePrice);

        Assert.Equal(8UL, weak.Population);
        Assert.Equal(8UL, neutral.Population);
        Assert.Equal(8UL, strong.Population);
        Assert.Equal(24m, weak.Offered);
        Assert.Equal(24m, neutral.Offered);
        Assert.Equal(24m, strong.Offered);

        Assert.Equal(4m, weak.EffectiveDemand);
        Assert.Equal(8m, neutral.EffectiveDemand);
        Assert.Equal(16m, strong.EffectiveDemand);
        Assert.Equal(6m, weak.SupplyDemandRatio);
        Assert.Equal(3m, neutral.SupplyDemandRatio);
        Assert.Equal(1.5m, strong.SupplyDemandRatio);

        Assert.True(weak.Price < neutral.Price);
        Assert.True(neutral.Price < strong.Price);
        Assert.Equal(weak.Price * weak.EffectiveDemand, weak.ReferenceRevenue);
        Assert.Equal(neutral.Price * neutral.EffectiveDemand, neutral.ReferenceRevenue);
        Assert.Equal(strong.Price * strong.EffectiveDemand, strong.ReferenceRevenue);
    }

    [Fact]
    public void RevenueIsPriceTimesEffectiveDemandNotNominalPopulationOrOffer()
    {
        var quote = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(24m),
            AbstractPurchasingPowerIndex.FromRelativeCapacity(2m),
            ExamplePrice);
        Assert.Equal(16m, quote.EffectiveDemand);
        Assert.Equal(1.5m, quote.SupplyDemandRatio);
        Assert.Equal(0.8m, quote.Price);
        Assert.Equal(12.8m, quote.ReferenceRevenue);
        Assert.NotEqual(quote.Price * quote.Population, quote.ReferenceRevenue);
        Assert.NotEqual(quote.Price * quote.Offered, quote.ReferenceRevenue);
    }

    [Fact]
    public void SupplyBeyondEffectiveDemandIsNotCappedOrCalledUnmet()
    {
        var result = PopulationPriceKernel.Quote(
            4UL, PhysicalOffer.FromAvailable(4000m),
            AbstractPurchasingPowerIndex.FromRelativeCapacity(0.5m),
            ExamplePrice);
        Assert.Equal(4000m, result.Offered);
        Assert.Equal(2m, result.EffectiveDemand);
        Assert.Equal(2000m, result.SupplyDemandRatio);
        Assert.Equal(result.Price * 2m, result.ReferenceRevenue);
        Assert.True(result.HasOffer);
        Assert.False(result.IsExecutedSettlement);
    }

    [Fact]
    public void ScalingPopulationAndSupplyTogetherPreservesPriceAtSameBuyingPower()
    {
        var index = AbstractPurchasingPowerIndex.FromRelativeCapacity(2m);
        var a = PopulationPriceKernel.Quote(
            10UL, PhysicalOffer.FromAvailable(10m), index, ExamplePrice);
        var b = PopulationPriceKernel.Quote(
            100UL, PhysicalOffer.FromAvailable(100m), index, ExamplePrice);
        Assert.Equal(a.SupplyDemandRatio, b.SupplyDemandRatio);
        Assert.Equal(a.Price, b.Price);
        Assert.Equal(a.ReferenceRevenue * 10m, b.ReferenceRevenue);
    }

    [Fact]
    public void ThePriceFunctionReceivesOnlySOverEffectiveDemand()
    {
        var called = 0;
        decimal Captured(decimal ratio)
        {
            called++;
            Assert.Equal(0.5m, ratio);
            return 3m;
        }

        var quote = PopulationPriceKernel.Quote(
            10UL, PhysicalOffer.FromAvailable(10m),
            AbstractPurchasingPowerIndex.FromRelativeCapacity(2m), Captured);
        Assert.Equal(1, called);
        Assert.Equal(3m, quote.Price);
        Assert.Equal(60m, quote.ReferenceRevenue);
    }

    [Fact]
    public void RetentionChangesPhysicalSupplyButNotPopulationOrPower()
    {
        var index = AbstractPurchasingPowerIndex.FromRelativeCapacity(2m);
        var supply = PhysicalOffer.FromAvailable(25m, retained: 5m);
        var quote = PopulationPriceKernel.Quote(5UL, supply, index, ExamplePrice);
        Assert.Equal(25m, supply.Available);
        Assert.Equal(5m, supply.Retained);
        Assert.Equal(20m, supply.Offered);
        Assert.Equal(10m, quote.EffectiveDemand);
        Assert.Equal(2m, quote.SupplyDemandRatio);
    }

    [Fact]
    public void NoPhysicalOfferDoesNotCallCurveOrInventRevenue()
    {
        var called = false;
        decimal Curve(decimal _) { called = true; return 100m; }
        var quote = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(0m),
            AbstractPurchasingPowerIndex.FromRelativeCapacity(2m), Curve);
        Assert.False(called);
        Assert.False(quote.HasOffer);
        Assert.Equal(16m, quote.EffectiveDemand);
        Assert.Equal(0m, quote.Price);
        Assert.Equal(0m, quote.ReferenceRevenue);
    }

    [Fact]
    public void InvalidPopulationPowerSupplyAndPriceFailClosed()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PopulationPriceKernel.Quote(8UL, PhysicalOffer.FromAvailable(4m), null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PopulationPriceKernel.Quote(0UL, PhysicalOffer.FromAvailable(4m), ExamplePrice));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalOffer.FromAvailable(-1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalOffer.FromAvailable(4m, retained: 5m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AbstractPurchasingPowerIndex.FromRelativeCapacity(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AbstractPurchasingPowerIndex.FromRelativeCapacity(-0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PopulationPriceKernel.Quote(
                8UL, PhysicalOffer.FromAvailable(4m),
                default(AbstractPurchasingPowerIndex), ExamplePrice));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PopulationPriceKernel.Quote(8UL, PhysicalOffer.FromAvailable(4m),
                _ => -1m));
        Assert.Throws<OverflowException>(() =>
            PopulationPriceKernel.Quote(
                ulong.MaxValue, PhysicalOffer.FromAvailable(1m),
                AbstractPurchasingPowerIndex.FromRelativeCapacity(decimal.MaxValue),
                ExamplePrice));
        Assert.Throws<OverflowException>(() =>
            PopulationPriceKernel.Quote(
                ulong.MaxValue, PhysicalOffer.FromAvailable(1m),
                _ => decimal.MaxValue));
    }

    [Fact]
    public void NoDependenciesOnLegacyDemandSolversOrMonetaryLedger()
    {
        var references = typeof(PopulationPriceKernel).Assembly
            .GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(references,
            name => name is not null && name.StartsWith(
                "GlobalArena.", StringComparison.Ordinal));
    }
}
