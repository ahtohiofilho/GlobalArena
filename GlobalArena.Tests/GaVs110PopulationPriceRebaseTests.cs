using GlobalArena.Economy;

namespace GlobalArena.Tests;

/// <summary>
/// ECON-REBASE-01: acceptance tests are the mathematical product contract,
/// not reconciliation to any legacy worker-demand rate, cash solver or
/// quota. The curve here is only an explicit TEST FIXTURE.
/// </summary>
public sealed class GaVs110PopulationPriceRebaseTests
{
    // Illustrative monotone function chosen BY THE TEST only, not a
    // canonical in-game price law or a universal commodity calibration.
    private static decimal ExamplePrice(decimal x) => 2m / (1m + x);

    [Fact]
    public void DemandIsExactlyPopulationAndOneToOneRatioUsesNoOtherReference()
    {
        var quote = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(8m), ExamplePrice);
        Assert.True(quote.HasOffer);
        Assert.Equal(8UL, quote.Population);
        Assert.Equal(8m, quote.Offered);
        Assert.Equal(1m, quote.OfferPerResident);
        Assert.Equal(1m, quote.Price);
        Assert.Equal(8m, quote.ReferenceRevenue);
        Assert.False(quote.IsExecutedSettlement);
    }

    [Fact]
    public void PhysicalSupplyAbovePopulationIsNotCappedByEconomicDemand()
    {
        var baseline = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(8m), ExamplePrice);
        var abundant = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(40m), ExamplePrice);
        Assert.Equal(5m, abundant.OfferPerResident);
        Assert.Equal(40m, abundant.Offered);
        Assert.True(abundant.Price < baseline.Price);
        Assert.Equal(abundant.Price * 8m, abundant.ReferenceRevenue);
    }

    [Fact]
    public void ScalingPopulationAndSupplyTogetherPreservesRelativePrice()
    {
        var a = PopulationPriceKernel.Quote(
            10UL, PhysicalOffer.FromAvailable(10m), ExamplePrice);
        var b = PopulationPriceKernel.Quote(
            100UL, PhysicalOffer.FromAvailable(100m), ExamplePrice);
        Assert.Equal(a.OfferPerResident, b.OfferPerResident);
        Assert.Equal(a.Price, b.Price);
        Assert.Equal(a.ReferenceRevenue * 10m, b.ReferenceRevenue);
    }

    [Fact]
    public void RevenueUsesPriceTimesPopulationNotPriceTimesPhysicalOffer()
    {
        var result = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(24m), ExamplePrice);
        Assert.Equal(3m, result.OfferPerResident);
        Assert.Equal(0.5m, result.Price);
        Assert.Equal(4m, result.ReferenceRevenue);
        Assert.NotEqual(result.Price * result.Offered, result.ReferenceRevenue);
    }

    [Fact]
    public void DeliberateRetentionReducesOfferWithoutCreatingStockOrDemand()
    {
        var physical = PhysicalOffer.FromAvailable(25m, retained: 5m);
        Assert.Equal(25m, physical.Available);
        Assert.Equal(5m, physical.Retained);
        Assert.Equal(20m, physical.Offered);
        var quote = PopulationPriceKernel.Quote(5UL, physical, ExamplePrice);
        Assert.Equal(4m, quote.OfferPerResident);
        Assert.Equal(20m, quote.Offered);
        Assert.Equal(quote.Price * 5m, quote.ReferenceRevenue);
    }

    [Fact]
    public void ZeroOfferHasNoFictitiousPriceOrRevenue()
    {
        var notCalled = false;
        decimal Curve(decimal x)
        {
            notCalled = true;
            return 100m;
        }
        var quote = PopulationPriceKernel.Quote(
            8UL, PhysicalOffer.FromAvailable(0m), Curve);
        Assert.False(notCalled);
        Assert.False(quote.HasOffer);
        Assert.Equal(0m, quote.Price);
        Assert.Equal(0m, quote.ReferenceRevenue);
    }

    [Fact]
    public void PricesRequireAnExplicitFunctionAndConservedValidInputs()
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
            PopulationPriceKernel.Quote(
                8UL, PhysicalOffer.FromAvailable(4m), _ => -1m));
        Assert.Throws<OverflowException>(() =>
            PopulationPriceKernel.Quote(
                ulong.MaxValue, PhysicalOffer.FromAvailable(1m),
                _ => decimal.MaxValue));
    }

    [Fact]
    public void PurchasingPowerChangesPriceButNeverThePopulationDemandOrSupplyRatio()
    {
        // Fixture ONLY: power enters multiplicatively; no production
        // coefficient or universal policy is ratified by this test.
        static decimal IllustrativePrice(decimal x, decimal m) =>
            ExamplePrice(x) * m;

        var supply = PhysicalOffer.FromAvailable(24m);
        var baseline = PopulationPriceKernel.Quote(
            8UL, supply, AbstractPurchasingPowerIndex.Neutral, IllustrativePrice);
        var weak = PopulationPriceKernel.Quote(
            8UL, supply,
            AbstractPurchasingPowerIndex.FromRelativeCapacity(0.8m),
            IllustrativePrice);
        var strong = PopulationPriceKernel.Quote(
            8UL, supply,
            AbstractPurchasingPowerIndex.FromRelativeCapacity(1.3m),
            IllustrativePrice);
        var oldPriceOnly = PopulationPriceKernel.Quote(
            8UL, supply, ExamplePrice);

        Assert.Equal(oldPriceOnly, baseline);
        Assert.Equal(8UL, weak.Population);
        Assert.Equal(8UL, strong.Population);
        Assert.Equal(24m, weak.Offered);
        Assert.Equal(24m, strong.Offered);
        Assert.Equal(3m, baseline.OfferPerResident);
        Assert.Equal(3m, weak.OfferPerResident);
        Assert.Equal(3m, strong.OfferPerResident);
        Assert.True(weak.Price < baseline.Price);
        Assert.True(strong.Price > baseline.Price);
        Assert.Equal(weak.Price * 8m, weak.ReferenceRevenue);
        Assert.Equal(strong.Price * 8m, strong.ReferenceRevenue);
    }

    [Fact]
    public void PurchasingPowerDoesNotBehaveAsPurchasingBudgetOrFoodCeiling()
    {
        var index = AbstractPurchasingPowerIndex.FromRelativeCapacity(0.5m);
        var excess = PopulationPriceKernel.Quote(
            4UL, PhysicalOffer.FromAvailable(4000m), index,
            (x, buyingPower) => buyingPower / (1m + x));
        Assert.True(excess.HasOffer);
        Assert.Equal(4UL, excess.Population);
        Assert.Equal(4000m, excess.Offered);
        Assert.Equal(1000m, excess.OfferPerResident);
        Assert.Equal(excess.Price * 4m, excess.ReferenceRevenue);
        Assert.False(excess.IsExecutedSettlement);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AbstractPurchasingPowerIndex.FromRelativeCapacity(-0.1m));
        Assert.Equal(1m, AbstractPurchasingPowerIndex.Neutral.RelativeCapacity);
    }

    [Fact]
    public void CleanKernelHasNoDependenciesOnLegacySimulationOrCashLedgers()
    {
        var references = typeof(PopulationPriceKernel).Assembly
            .GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(references,
            name => name is not null && name.StartsWith("GlobalArena.",
                StringComparison.Ordinal));
    }
}
