using GlobalArena.Economy;

namespace GlobalArena.Tests;

/// <summary>
/// ECON-REBASE-02: N is derived from comparable REAL physical final output
/// per resident, frozen base-cycle unit valuations, and the PREVIOUS cycle.
/// No legacy demand quota, wallet, nominal revenue or price-feedback loop.
/// </summary>
public sealed class GaVs111RealPurchasingPowerDerivationTests
{
    private static RealCompletedCycleObservation Cycle(
        ulong cycle, ulong residents, params RealFinalOutputRow[] output) =>
        RealCompletedCycleObservation.FromPhysicalObservations(
            cycle, residents, output);

    private static FrozenRealPurchasingPowerBasis Basis() =>
        RealPurchasingPowerDerivation.FreezeBase(
            Cycle(1UL, 10UL,
                new RealFinalOutputRow(1U, 10m),
                new RealFinalOutputRow(2U, 4m)),
            [
                new FrozenBaseUnitValuation(1U, 2m),
                new FrozenBaseUnitValuation(2U, 5m)
            ]);

    [Fact]
    public void BaselineCycleProducesNeutralOneForFollowingCycle()
    {
        var basis = Basis();
        var first = Cycle(1UL, 10UL,
            new RealFinalOutputRow(1U, 10m),
            new RealFinalOutputRow(2U, 4m));
        var index = RealPurchasingPowerDerivation.FromPriorCycle(
            basis, first, pricingCycle: 2UL);
        Assert.Equal(1UL, index.EvidenceCycle);
        Assert.Equal(2UL, index.PricingCycle);
        Assert.Equal(40m, index.BaseRealOutputValue);
        Assert.Equal(40m, index.CurrentRealOutputValue);
        Assert.Equal(1m, index.PurchasingPower.RelativeCapacity);
        Assert.Equal(AbstractPurchasingPowerIndex.Neutral, index.PurchasingPower);
    }

    [Fact]
    public void RealFinalOutputGrowthChangesNextCycleEconomicDemandNotPhysicalPopulation()
    {
        var previous = Cycle(2UL, 12UL,
            new RealFinalOutputRow(1U, 12m),
            new RealFinalOutputRow(2U, 12m)); // fixed-base value: 24 + 60 = 84
        var capacity = RealPurchasingPowerDerivation.FromPriorCycle(
            Basis(), previous, pricingCycle: 3UL);

        // Real value per capita: 84/12 compared with baseline 40/10
        Assert.Equal(1.75m, capacity.PurchasingPower.RelativeCapacity);
        Assert.Equal(84m, capacity.CurrentRealOutputValue);
        Assert.Equal(2UL, capacity.EvidenceCycle);
        Assert.Equal(3UL, capacity.PricingCycle);

        // A new pricing quote uses N solely inside D = P*N and x = S/D.
        static decimal FixturePrice(decimal x) => 2m / (1m + x);
        var quote = PopulationPriceKernel.Quote(
            12UL, PhysicalOffer.FromAvailable(21m),
            capacity.PurchasingPower, FixturePrice);
        Assert.Equal(12UL, quote.Population);
        Assert.Equal(21m, quote.EffectiveDemand);
        Assert.Equal(21m, quote.Offered);
        Assert.Equal(1m, quote.SupplyDemandRatio);
        Assert.Equal(1m, quote.Price);
        Assert.Equal(21m, quote.ReferenceRevenue);
        Assert.False(quote.IsExecutedSettlement);
    }

    [Fact]
    public void DecliningRealProductionChangesCapacityWithoutNominalRevenue()
    {
        var previous = Cycle(2UL, 10UL,
            new RealFinalOutputRow(1U, 5m),
            new RealFinalOutputRow(2U, 2m));
        var result = RealPurchasingPowerDerivation.FromPriorCycle(
            Basis(), previous, 3UL);
        Assert.Equal(20m, result.CurrentRealOutputValue);
        Assert.Equal(0.5m, result.PurchasingPower.RelativeCapacity);
    }

    [Fact]
    public void PopulationGrowthAloneReducesRealCapacityPerPerson()
    {
        var previous = Cycle(2UL, 20UL,
            new RealFinalOutputRow(1U, 10m),
            new RealFinalOutputRow(2U, 4m));
        var result = RealPurchasingPowerDerivation.FromPriorCycle(
            Basis(), previous, 3UL);
        Assert.Equal(40m, result.CurrentRealOutputValue);
        Assert.Equal(0.5m, result.PurchasingPower.RelativeCapacity);
    }

    [Fact]
    public void FrozenUnitValuationsMakeOrderAndLaterNominalRepricingIrrelevant()
    {
        var originalWeights = new[]
        {
            new FrozenBaseUnitValuation(2U, 5m),
            new FrozenBaseUnitValuation(1U, 2m)
        };
        var baseline = Cycle(1UL, 10UL,
            new RealFinalOutputRow(2U, 4m),
            new RealFinalOutputRow(1U, 10m));
        var basis = RealPurchasingPowerDerivation.FreezeBase(
            baseline, originalWeights);
        originalWeights[0] = new FrozenBaseUnitValuation(2U, 999m);
        var current = Cycle(2UL, 12UL,
            new RealFinalOutputRow(2U, 12m),
            new RealFinalOutputRow(1U, 12m));
        var index = RealPurchasingPowerDerivation.FromPriorCycle(
            basis, current, 3UL);

        Assert.Equal(2m, basis.UnitValuations[0].ReferenceValuePerPhysicalUnit);
        Assert.Equal(5m, basis.UnitValuations[1].ReferenceValuePerPhysicalUnit);
        Assert.Equal(1.75m, index.PurchasingPower.RelativeCapacity);
        Assert.Equal(1U, current.FinalOutput[0].Commodity);
        Assert.Equal(2U, current.FinalOutput[1].Commodity);
    }

    [Fact]
    public void ZeroRealActivityDoesNotInventZeroPurchasingCapacityOrANewFloor()
    {
        var previous = Cycle(2UL, 10UL,
            new RealFinalOutputRow(1U, 0m),
            new RealFinalOutputRow(2U, 0m));
        Assert.Throws<InvalidOperationException>(() =>
            RealPurchasingPowerDerivation.FromPriorCycle(
                Basis(), previous, 3UL));

        Assert.Throws<InvalidOperationException>(() =>
            RealPurchasingPowerDerivation.FreezeBase(
                Cycle(1UL, 10UL,
                    new RealFinalOutputRow(1U, 0m)),
                [new FrozenBaseUnitValuation(1U, 2m)]));
    }

    [Fact]
    public void RefusesUnobservedForeignOrMissingCommodityInsteadOfFakingASignal()
    {
        var basis = Basis();
        var missing = Cycle(2UL, 10UL,
            new RealFinalOutputRow(1U, 10m));
        var foreign = Cycle(2UL, 10UL,
            new RealFinalOutputRow(1U, 10m),
            new RealFinalOutputRow(3U, 4m));
        Assert.Throws<InvalidOperationException>(() =>
            RealPurchasingPowerDerivation.FromPriorCycle(
                basis, missing, 3UL));
        Assert.Throws<InvalidOperationException>(() =>
            RealPurchasingPowerDerivation.FromPriorCycle(
                basis, foreign, 3UL));
    }

    [Fact]
    public void EvidenceMustBeFromImmediatelyPreviousCompletedEconomicCycle()
    {
        var basis = Basis();
        var previous = Cycle(2UL, 10UL,
            new RealFinalOutputRow(1U, 10m),
            new RealFinalOutputRow(2U, 4m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RealPurchasingPowerDerivation.FromPriorCycle(
                basis, previous, pricingCycle: 2UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RealPurchasingPowerDerivation.FromPriorCycle(
                basis, previous, pricingCycle: 4UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RealPurchasingPowerDerivation.FromPriorCycle(
                basis, Cycle(0UL, 10UL,
                    new RealFinalOutputRow(1U, 10m)), 1UL));
    }

    [Fact]
    public void PreventsDuplicateNegativeAndInvalidBaseValuations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Cycle(0UL, 10UL, new RealFinalOutputRow(1U, 1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Cycle(1UL, 0UL, new RealFinalOutputRow(1U, 1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Cycle(1UL, 10UL, new RealFinalOutputRow(1U, -1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Cycle(1UL, 10UL, new RealFinalOutputRow(0U, 1m)));
        Assert.Throws<ArgumentException>(() =>
            Cycle(1UL, 10UL, new RealFinalOutputRow(1U, 1m),
                new RealFinalOutputRow(1U, 2m)));
        Assert.Throws<ArgumentException>(() =>
            Cycle(1UL, 10UL));

        var sample = Cycle(1UL, 10UL, new RealFinalOutputRow(1U, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RealPurchasingPowerDerivation.FreezeBase(sample,
                [new FrozenBaseUnitValuation(1U, -1m)]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RealPurchasingPowerDerivation.FreezeBase(sample,
                [new FrozenBaseUnitValuation(1U, 0m)]));
        Assert.Throws<ArgumentException>(() =>
            RealPurchasingPowerDerivation.FreezeBase(sample,
                [new FrozenBaseUnitValuation(1U, 1m),
                 new FrozenBaseUnitValuation(1U, 1m)]));
        Assert.Throws<InvalidOperationException>(() =>
            RealPurchasingPowerDerivation.FreezeBase(sample,
                [new FrozenBaseUnitValuation(2U, 1m)]));
    }

    [Fact]
    public void FixedBasketEvaluationScalesWithNumberOfCommoditiesNotResidents()
    {
        var originRows = Enumerable.Range(1, 1000)
            .Select(i => new RealFinalOutputRow((uint)i, 1m)).ToArray();
        var prices = Enumerable.Range(1, 1000)
            .Select(i => new FrozenBaseUnitValuation((uint)i, 1m)).ToArray();
        var baseline = Cycle(1UL, 100_000_000UL, originRows);
        var reference = RealPurchasingPowerDerivation.FreezeBase(baseline, prices);
        var latest = Cycle(2UL, 100_000_000UL, originRows);
        var result = RealPurchasingPowerDerivation.FromPriorCycle(
            reference, latest, 3UL);
        Assert.Equal(1m, result.PurchasingPower.RelativeCapacity);
        Assert.Equal(1000m, result.CurrentRealOutputValue);
    }
}
