using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicFoodPreferenceAndSingleOriginClearingTests
{
    private static readonly EconomicFoodPreferenceParameters Canonical =
        new(0.1, 2.8, 1.0);
    private static readonly EconomicFoodPreferenceEvaluator Preference = new();
    private static EconomicPointId Point(ulong value) => new(value);

    [Fact]
    public void OneVarietyHasZeroDiversityAndExactGaProp001MonetaryMarginal()
    {
        var r = Preference.Evaluate(10, 15, new[] { 5.0 }, Canonical);
        Assert.Equal(0, r.Diversity);
        Assert.Equal(1, r.VarietyMultiplier);
        var expected = Math.Pow(15.0 / 20.0, 2) * (2.0 / 1.5);
        Assert.InRange(Math.Abs(r.MarginalWillingnessToPay[0] - expected), 0, 1e-13);
        Assert.Equal(0.5, r.FoodPerResident, 12);
    }

    [Fact]
    public void ThreeVarietyMonetaryMarginalsMatchUtilityGradientRatios()
    {
        var q = new[] { 5.1, 2.2, 1.4 };
        var population = 10.0;
        var h = 12.3;
        var r = Preference.Evaluate(population, h, q, Canonical);
        const double step = 1e-5;
        var uh = (Preference.Evaluate(population, h + step, q, Canonical).Utility -
                  Preference.Evaluate(population, h - step, q, Canonical).Utility) /
                  (2 * step);
        for (var j = 0; j < q.Length; j++)
        {
            var plus = (double[])q.Clone();
            var minus = (double[])q.Clone();
            plus[j] += step;
            minus[j] -= step;
            var uq = (Preference.Evaluate(population, h, plus, Canonical).Utility -
                      Preference.Evaluate(population, h, minus, Canonical).Utility) /
                      (2 * step);
            Assert.InRange(
                Math.Abs(uq / uh - r.MarginalWillingnessToPay[j]),
                0, 2e-8);
        }
        Assert.True(r.Diversity > 0);
        Assert.True(r.VarietyMultiplier > 1);
        Assert.True(r.AbundanceFactor > 0 && r.AbundanceFactor < 1);
    }

    [Fact]
    public void ReproducesIndependentEcon004cThreeCityPreferenceOracle()
    {
        // Independent GA-ECON-004C open/high-support Python benchmark.
        // These fixed values are a regression oracle for the PREFERENCE
        // evaluator only, not proof of a C# market equilibrium.
        var populations = new[] { 10.0, 12.0, 9.0 };
        var residuals = new[]
        {
            23.397242857159487, 28.157879425966712, 21.017301971979684
        };
        var quantities = new[]
        {
            new[] { 7.295076060822664, 2.6194115830809643, 2.5391107019821395 },
            new[] { 2.6847101694844815, 10.054269574636058, 2.47039006894024 },
            new[] { 2.5202137696928553, 2.3263188422829786, 6.240499229077621 }
        };
        var expectedUtility = new[]
        {
            7.749867708218602, 9.406966505695134, 6.814240732861318
        };
        var expectedWtp = new[]
        {
            new[] { 2.018602278324748, 2.3701177934660196, 2.3900438762685563 },
            new[] { 2.44352296624117, 1.945197105549598, 2.511519787990762 },
            new[] { 2.353917752774, 2.401988491721055, 2.054728401819305 }
        };

        for (var i = 0; i < populations.Length; i++)
        {
            var result = Preference.Evaluate(
                populations[i], residuals[i], quantities[i], Canonical);
            Assert.InRange(Math.Abs(result.Utility - expectedUtility[i]), 0, 2e-11);
            for (var j = 0; j < quantities[i].Length; j++)
                Assert.InRange(Math.Abs(result.MarginalWillingnessToPay[j] -
                    expectedWtp[i][j]), 0, 2e-11);
        }
    }

    [Fact]
    public void MonovarietyDoesNotDependOnUnusedDiversityCoefficients()
    {
        var baseline = Preference.Evaluate(10, 18, new[] { 6.0 }, Canonical);
        var adjusted = Preference.Evaluate(10, 18, new[] { 6.0 },
            new EconomicFoodPreferenceParameters(0.5, 0, 0));
        Assert.Equal(baseline.Utility, adjusted.Utility, 12);
        Assert.Equal(baseline.MarginalWillingnessToPay[0],
            adjusted.MarginalWillingnessToPay[0], 12);
    }

    [Fact]
    public void ZeroFoodRemainsValidButZeroOrNegativeResidualHDoesNot()
    {
        var r = Preference.Evaluate(8, 12, new[] { 0.0, 0.0 }, Canonical);
        Assert.Equal(0.0, r.Diversity);
        Assert.Equal(1.0, r.VarietyMultiplier);
        Assert.All(r.MarginalWillingnessToPay, value => Assert.True(value > 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Preference.Evaluate(8, 0, new[] { 0.0 }, Canonical));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Preference.Evaluate(0, 12, new[] { 0.0 }, Canonical));
        Assert.Throws<ArgumentException>(() =>
            Preference.Evaluate(8, 12, new[] { -1.0 }, Canonical));
        Assert.Throws<ArgumentException>(() =>
            Preference.Evaluate(8, 12, Array.Empty<double>(), Canonical));
    }

    [Fact]
    public void LargeFoodLoadKeepsAbundanceFiniteWithoutPowOverflow()
    {
        var r = Preference.Evaluate(10, 1.0e20, new[] { 2.0e40, 1.0e40 }, Canonical);
        Assert.True(double.IsFinite(r.Utility));
        Assert.True(double.IsFinite(r.AbundanceFactor));
        Assert.Equal(1.0, r.AbundanceFactor);
        Assert.All(r.MarginalWillingnessToPay, w => Assert.True(double.IsFinite(w)));
    }

    private static EconomicSingleVarietyBuyer[] Buyers(
        double firstBudget = 50, double secondBudget = 60,
        double firstFreight = .1, double secondFreight = .3) => new[]
    {
        new EconomicSingleVarietyBuyer(Point(1), 10, firstBudget, firstFreight),
        new EconomicSingleVarietyBuyer(Point(2), 12, secondBudget, secondFreight)
    };

    private static EconomicSingleVarietyOriginClearingResult Clear(
        double supply = 8,
        IEnumerable<EconomicSingleVarietyBuyer>? buyers = null) =>
        new EconomicSingleVarietyOriginClearing().Resolve(
            supply, buyers ?? Buyers(), Canonical);

    [Fact]
    public void OriginPriceIsEndogenousAndClearsFiniteSupplyWithPositiveH()
    {
        var r = Clear();
        Assert.True(r.OriginPrice > 0);
        Assert.InRange(Math.Abs(r.Sold - 8), 0, 2e-7);
        Assert.InRange(r.Unsold, 0, 2e-7);
        Assert.All(r.Buyers, buyer =>
        {
            Assert.True(buyer.PurchasedQuantity > 0);
            Assert.True(buyer.MonetaryResidual > 0);
            Assert.InRange(Math.Abs(buyer.MarginalWillingnessToPay -
                buyer.DeliveredPrice), 0, 2e-7);
        });
        Assert.InRange(r.MaximumBudgetResidual, 0, 1e-9);
        Assert.InRange(Math.Abs(r.TotalBuyerExpenditure -
            r.SellerReceipts - r.FreightReceipts), 0, 1e-8);
    }

    [Fact]
    public void GreaterBuyerBudgetsIncreaseTheEndogenousOriginPrice()
    {
        var baseline = Clear();
        var richer = Clear(buyers: Buyers(firstBudget: 60, secondBudget: 70));
        Assert.True(richer.OriginPrice > baseline.OriginPrice);
        Assert.InRange(Math.Abs(richer.Sold - 8), 0, 2e-7);
    }

    [Fact]
    public void IncreasedFreightChangesWhoReceivesTheFiniteGood()
    {
        var baseline = Clear();
        var expensive = Clear(buyers: Buyers(secondFreight: 4));
        Assert.True(expensive.Buyers.Single(x => x.PointId == Point(2)).PurchasedQuantity <
                    baseline.Buyers.Single(x => x.PointId == Point(2)).PurchasedQuantity);
        Assert.True(expensive.Buyers.Single(x => x.PointId == Point(1)).PurchasedQuantity >
                    baseline.Buyers.Single(x => x.PointId == Point(1)).PurchasedQuantity);
        Assert.InRange(Math.Abs(expensive.Sold - 8), 0, 2e-7);
    }

    [Fact]
    public void GoodsRemainUnsoldWhenNoBuyerWillPayEvenTheirFreight()
    {
        var r = Clear(supply: 200, buyers: Buyers(
            firstBudget: 2, secondBudget: 2,
            firstFreight: 20, secondFreight: 20));
        Assert.Equal(0.0, r.OriginPrice);
        Assert.Equal(0.0, r.Sold);
        Assert.Equal(200.0, r.Unsold);
        Assert.Equal(0.0, r.SellerReceipts);
        Assert.Equal(0.0, r.FreightReceipts);
    }

    [Fact]
    public void PermutedBuyerInputsProduceIdenticalResults()
    {
        var baseline = Clear();
        var permuted = Clear(buyers: Buyers().Reverse());
        Assert.Equal(baseline.OriginPrice, permuted.OriginPrice);
        Assert.Equal(baseline.Sold, permuted.Sold);
        Assert.Equal(baseline.FreightReceipts, permuted.FreightReceipts);
        Assert.Equal(
            baseline.Buyers.Select(x => x.PurchasedQuantity),
            permuted.Buyers.Select(x => x.PurchasedQuantity));
    }

    [Fact]
    public void IdenticalBuyersWithoutFreightDivideSupplySymmetrically()
    {
        var same = new[]
        {
            new EconomicSingleVarietyBuyer(Point(8), 10, 50, 0),
            new EconomicSingleVarietyBuyer(Point(3), 10, 50, 0)
        };
        var r = Clear(supply: 10, buyers: same);
        Assert.True(r.OriginPrice > 0);
        Assert.Equal(2, r.Buyers.Count);
        Assert.All(r.Buyers, b =>
            Assert.InRange(Math.Abs(b.PurchasedQuantity - 5), 0, 1e-7));
        Assert.Equal(Point(3), r.Buyers[0].PointId);
    }

    [Fact]
    public void InvalidBudgetDuplicateBuyerAndUnknownSupplyFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicSingleVarietyBuyer(Point(1), 10, -2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Clear(supply: 0));
        Assert.Throws<ArgumentException>(() =>
            Clear(buyers: new[] { Buyers()[0], Buyers()[0] }));
        Assert.Throws<ArgumentException>(() =>
            Clear(buyers: Array.Empty<EconomicSingleVarietyBuyer>()));
    }
}
