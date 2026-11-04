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
    public void CleanKernelHasNoDependenciesOnLegacySimulationOrCashLedgers()
    {
        var references = typeof(PopulationPriceKernel).Assembly
            .GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(references,
            name => name is not null && name.StartsWith("GlobalArena.",
                StringComparison.Ordinal));
    }
}
