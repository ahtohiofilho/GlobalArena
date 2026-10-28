using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicMultivarietyInteriorClearingTests
{
    private static readonly EconomicFoodPreferenceParameters Preference =
        new(0.1, 2.8, 1.0);

    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId E = new(2UL);
    private static readonly EconomicPointId F = new(3UL);

    private static EconomicMultivarietyOrigin[] Origins() => new[]
    {
        new EconomicMultivarietyOrigin(A, 12.5),
        new EconomicMultivarietyOrigin(E, 15.0),
        new EconomicMultivarietyOrigin(F, 11.25)
    };

    private static EconomicMultivarietyBuyer[] Buyers(
        double incomeMultiplier = 1.0) => new[]
    {
        new EconomicMultivarietyBuyer(A, 10, 50.4 * incomeMultiplier),
        new EconomicMultivarietyBuyer(E, 12, 60.48 * incomeMultiplier),
        new EconomicMultivarietyBuyer(F, 9, 45.36 * incomeMultiplier)
    };

    // Frozen destination/origin fares obtained by summing active resource
    // average tariffs from the independent GA-ECON-004C CLOSED-support oracle.
    // They remain exogenous in THIS reference; not real-cost feedback.
    private static EconomicMultivarietyDeliveredFreight[] Quotes(
        double multiplier = 1.0)
    {
        var ids = new[] { A, E, F };
        var tariff = new[,]
        {
            { 0.0, 0.4527858113141606, 0.345505700416868 },
            { 0.4527858113141606, 0.0, 0.4721960312223556 },
            { 0.345505700416868, 0.4721960312223556, 0.0 }
        };
        var quotes = new List<EconomicMultivarietyDeliveredFreight>();
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
                quotes.Add(new EconomicMultivarietyDeliveredFreight(
                    ids[i], ids[j], tariff[i, j] * multiplier));
        return quotes.ToArray();
    }

    private static EconomicMultivarietyInteriorClearingResult Clear(
        IEnumerable<EconomicMultivarietyOrigin>? origins = null,
        IEnumerable<EconomicMultivarietyBuyer>? buyers = null,
        IEnumerable<EconomicMultivarietyDeliveredFreight>? quotes = null) =>
        new EconomicMultivarietyInteriorClearing().Resolve(
            origins ?? Origins(), buyers ?? Buyers(), quotes ?? Quotes(), Preference);

    [Fact]
    public void ReproducesIndependentEcon004cThreeMarketClosedSupportWithEndogenousPrices()
    {
        var actual = Clear();
        var prices = new[]
        {
            2.0155351015549514, 1.9405700202450464, 2.0524466395000958
        };
        var q = new[,]
        {
            { 7.4113718516844305, 2.527371850238366, 2.509820389770114 },
            { 2.599662077310181, 10.185362753085748, 2.4313725771044927 },
            { 2.488966071005388, 2.2872653966758842, 6.308807033125392 }
        };
        var h = new[]
        {
            23.394790049546597, 28.159443167641353, 21.016323457444326
        };
        Assert.Equal(new[] { A, E, F }, actual.OriginOrder);
        Assert.Equal(new[] { A, E, F }, actual.Buyers.Select(x => x.BuyerId));
        Assert.InRange(actual.NewtonIterations, 1, 25);
        for (var j = 0; j < 3; j++)
            Assert.InRange(Math.Abs(actual.OriginPrices[j] - prices[j]), 0, 1e-7);
        for (var i = 0; i < 3; i++)
        {
            Assert.InRange(Math.Abs(actual.Buyers[i].MonetaryResidual - h[i]), 0, 1e-7);
            for (var j = 0; j < 3; j++)
                Assert.InRange(Math.Abs(actual.Buyers[i].QuantitiesByOrigin[j] - q[i, j]), 0, 1e-7);
        }
        Assert.InRange(actual.MaximumBudgetResidual, 0, 1e-7);
        Assert.InRange(actual.MaximumSupplyResidual, 0, 1e-7);
        Assert.InRange(actual.MaximumMarginalResidual, 0, 1e-7);
        Assert.InRange(Math.Abs(
            actual.BuyerExpenditure - actual.SellerReceipts -
            actual.FreightReceipts), 0, 1e-7);
    }

    [Fact]
    public void OriginPricesChangeWhenAllBuyerBudgetsRise()
    {
        var baseline = Clear();
        var richer = Clear(buyers: Buyers(incomeMultiplier: 1.08));
        Assert.All(Enumerable.Range(0, 3), j =>
        {
            Assert.True(richer.OriginPrices[j] > baseline.OriginPrices[j]);
        });
        Assert.True(richer.Buyers.Sum(x => x.MonetaryResidual) >
            baseline.Buyers.Sum(x => x.MonetaryResidual));
    }

    [Fact]
    public void FreightPerturbationReclearsAllOriginPricesAndQuantities()
    {
        var before = Clear();
        var after = Clear(quotes: Quotes(multiplier: 1.1));
        Assert.Contains(Enumerable.Range(0, 3), j =>
            Math.Abs(after.OriginPrices[j] - before.OriginPrices[j]) > 1e-4);
        Assert.Contains(after.Buyers.Zip(before.Buyers), pair =>
            Enumerable.Range(0, 3).Any(j =>
                Math.Abs(pair.First.QuantitiesByOrigin[j] -
                         pair.Second.QuantitiesByOrigin[j]) > 1e-4));
        Assert.InRange(after.MaximumBudgetResidual, 0, 1e-7);
        Assert.InRange(after.MaximumSupplyResidual, 0, 1e-7);
    }

    [Fact]
    public void ReorderingBuyersOriginsAndFaresDoesNotChangeTheResult()
    {
        var baseline = Clear();
        var reordered = Clear(
            origins: Origins().Reverse(),
            buyers: Buyers().Reverse(),
            quotes: Quotes().Reverse());
        Assert.Equal(baseline.OriginPrices, reordered.OriginPrices);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(baseline.Buyers[i].BuyerId, reordered.Buyers[i].BuyerId);
            Assert.Equal(baseline.Buyers[i].QuantitiesByOrigin,
                reordered.Buyers[i].QuantitiesByOrigin);
            Assert.Equal(baseline.Buyers[i].MonetaryResidual,
                reordered.Buyers[i].MonetaryResidual);
        }
    }

    [Fact]
    public void IdenticalTwoOriginBuyersClearSymmetricallyWithoutTransport()
    {
        var origins = new[]
        {
            new EconomicMultivarietyOrigin(A, 10),
            new EconomicMultivarietyOrigin(E, 10)
        };
        var buyers = new[]
        {
            new EconomicMultivarietyBuyer(A, 10, 50),
            new EconomicMultivarietyBuyer(E, 10, 50)
        };
        var quotes = new[]
        {
            new EconomicMultivarietyDeliveredFreight(A, A, 0),
            new EconomicMultivarietyDeliveredFreight(A, E, 0),
            new EconomicMultivarietyDeliveredFreight(E, A, 0),
            new EconomicMultivarietyDeliveredFreight(E, E, 0)
        };
        var result = Clear(origins, buyers, quotes);
        Assert.InRange(Math.Abs(result.OriginPrices[0] - result.OriginPrices[1]), 0, 1e-8);
        Assert.All(result.Buyers, buyer =>
        {
            Assert.InRange(Math.Abs(buyer.QuantitiesByOrigin[0] - 5), 0, 1e-7);
            Assert.InRange(Math.Abs(buyer.QuantitiesByOrigin[1] - 5), 0, 1e-7);
            Assert.True(buyer.MonetaryResidual > 0);
        });
        Assert.InRange(result.MaximumMarginalResidual, 0, 1e-7);
    }

    [Fact]
    public void AbsenceOfAnyQuoteOrUnknownMarketsFailsClosed()
    {
        Assert.Throws<ArgumentException>(() =>
            Clear(quotes: Quotes().Skip(1)));
        Assert.Throws<ArgumentException>(() =>
            Clear(quotes: Quotes().Concat(Quotes().Take(1))));
        Assert.Throws<ArgumentException>(() =>
            Clear(buyers: Buyers().Concat(Buyers().Take(1))));
        Assert.Throws<ArgumentException>(() =>
            Clear(origins: Origins().Concat(Origins().Take(1))));
        Assert.Throws<ArgumentException>(() =>
            Clear(quotes: Quotes().Select(q =>
                q.BuyerId == A && q.OriginId == E
                    ? new EconomicMultivarietyDeliveredFreight(A,
                        new EconomicPointId(99), q.FreightPerUnit) : q)));
    }

    [Fact]
    public void InfeasibleStrictInteriorIsRejectedRatherThanPostingNegativeHOrPrice()
    {
        // Very high freight to one particular variety gives its buyers
        // zero optimal purchases; an ALL-positive-quantity interior is invalid.
        var quotes = Quotes().Select(q =>
            q.OriginId == E && q.BuyerId != E
                ? new EconomicMultivarietyDeliveredFreight(
                    q.BuyerId, q.OriginId, 10000)
                : q).ToArray();
        Assert.Throws<InvalidOperationException>(() =>
            Clear(quotes: quotes));
    }

    [Fact]
    public void DoesNotMutateWorldStateOrImplementFinancialSettlement()
    {
        var world = WorldState.CreateInitial();
        var hash = new CanonicalWorldStateHasher().Compute(world);
        var result = Clear();
        Assert.NotEmpty(result.Buyers);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(world));
    }
}
