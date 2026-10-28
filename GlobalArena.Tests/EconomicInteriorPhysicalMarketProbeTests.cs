using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicInteriorPhysicalMarketProbeTests
{
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId E = new(2UL);
    private static readonly EconomicPointId F = new(3UL);
    private static readonly EconomicPhysicalResourceId Shared = new(20UL);
    private static readonly EconomicPhysicalResourceId BranchA = new(21UL);
    private static readonly EconomicPhysicalResourceId BranchE = new(22UL);
    private static readonly EconomicPhysicalResourceId BranchF = new(23UL);

    private static readonly EconomicFoodPreferenceParameters Parameters =
        new(0.1, 2.8, 1.0);

    private sealed class ReferenceQuoteFunction : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms) =>
            new(BigInteger.One, BigInteger.One);
    }

    private static EconomicMultivarietyOrigin[] Origins() => new[]
    {
        new EconomicMultivarietyOrigin(A, 12.5),
        new EconomicMultivarietyOrigin(E, 15),
        new EconomicMultivarietyOrigin(F, 11.25)
    };

    private static EconomicMultivarietyBuyer[] Buyers() => new[]
    {
        new EconomicMultivarietyBuyer(A, 10, 50.4),
        new EconomicMultivarietyBuyer(E, 12, 60.48),
        new EconomicMultivarietyBuyer(F, 9, 45.36)
    };

    private static EconomicPhysicalResourceId Branch(EconomicPointId origin) =>
        origin == A ? BranchA : origin == E ? BranchE : BranchF;

    private static EconomicInteriorPhysicalMarketIncidence[] Routes(
        double initialFare = 0.1)
    {
        var result = new List<EconomicInteriorPhysicalMarketIncidence>();
        foreach (var buyer in new[] { A, E, F })
            foreach (var origin in new[] { A, E, F })
            {
                var quote = EconomicTurnFreightQuote.FromForecast(
                    1UL, new StrategicEconomicRouteKey(origin, buyer),
                    new BigInteger(1_000_000), new ReferenceQuoteFunction());
                var domestic = buyer == origin;
                var route = new EconomicReclearingQuotedRoute(quote,
                    domestic ? Array.Empty<EconomicPhysicalResourceId>() :
                        new[] { Shared, Branch(origin) });
                result.Add(new EconomicInteriorPhysicalMarketIncidence(
                    route, 1UL, domestic ? 0 : initialFare));
            }
        return result.ToArray();
    }

    private static EconomicDensityResourceCostProfile[] Profiles(
        ulong sharedLinearMilli = 100UL) => new[]
    {
        new EconomicDensityResourceCostProfile(Shared, sharedLinearMilli, 50, 50, 25),
        new EconomicDensityResourceCostProfile(BranchA, 30, 10, 30, 20),
        new EconomicDensityResourceCostProfile(BranchE, 30, 10, 30, 20),
        new EconomicDensityResourceCostProfile(BranchF, 30, 10, 30, 20)
    };

    private static EconomicInteriorPhysicalMarketResult Evaluate(
        IEnumerable<EconomicMultivarietyOrigin>? origins = null,
        IEnumerable<EconomicMultivarietyBuyer>? buyers = null,
        IEnumerable<EconomicInteriorPhysicalMarketIncidence>? routes = null,
        IEnumerable<EconomicDensityResourceCostProfile>? profiles = null) =>
        new EconomicInteriorPhysicalMarketProbe().Evaluate(
            origins ?? Origins(), buyers ?? Buyers(), routes ?? Routes(),
            profiles ?? Profiles(), Parameters);

    private static double ToDouble(FoodTransportCostFraction x) =>
        (double)x.Numerator / (double)x.Denominator;

    [Fact]
    public void ReclearsSharedPhysicalLoadsAndReconcilesAverageCostWithoutDoubleCharge()
    {
        var result = Evaluate();
        Assert.InRange(result.OuterIterations, 1, 160);
        Assert.Equal(3, result.Market.OriginPrices.Count);
        Assert.All(result.Market.OriginPrices, x => Assert.True(x > 0));
        Assert.Contains(Shared, result.ResourceLoadsMicrograms.Keys);
        Assert.Equal(4, result.ResourceLoadsMicrograms.Count);

        var sharedMarketMass = BigInteger.Zero;
        var branchTotals = new Dictionary<EconomicPhysicalResourceId, BigInteger>();
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
            {
                if (i == j) continue;
                var q = result.Market.Buyers[i].QuantitiesByOrigin[j];
                var mass = new BigInteger(Math.Round(q * 1_000_000.0,
                    MidpointRounding.ToEven));
                sharedMarketMass += mass;
                var branch = Branch(result.Market.OriginOrder[j]);
                branchTotals[branch] = branchTotals.GetValueOrDefault(branch) + mass;
            }
        Assert.Equal(sharedMarketMass, result.ResourceLoadsMicrograms[Shared]);
        foreach (var (id, mass) in branchTotals)
            Assert.Equal(mass, result.ResourceLoadsMicrograms[id]);

        var costLow = ToDouble(result.RealResourceCostLower);
        var costHigh = ToDouble(result.RealResourceCostUpper);
        Assert.True(costLow <= costHigh);
        Assert.InRange(result.Market.FreightReceipts - costHigh, -1e-5, 1e-5);
        Assert.InRange(result.Market.FreightReceipts - costLow, -1e-5, 1e-5);
        Assert.InRange(result.MaximumQuotedVsImpliedFareResidual, 0, 1e-7);
        Assert.InRange(Math.Abs(result.FreightRevenueMinusRealCostMidpoint), 0, 1e-5);
        Assert.InRange(result.Market.MaximumBudgetResidual, 0, 1e-7);
        Assert.InRange(result.Market.MaximumSupplyResidual, 0, 1e-7);
    }

    [Fact]
    public void RealResourceCostChangesEndogenousPricesAndCrossOriginQuantities()
    {
        var baseline = Evaluate();
        var costlier = Evaluate(profiles: Profiles(sharedLinearMilli: 280UL));
        Assert.Contains(Enumerable.Range(0, 3), j =>
            Math.Abs(costlier.Market.OriginPrices[j] -
                baseline.Market.OriginPrices[j]) > 1e-4);
        Assert.Contains(Enumerable.Range(0, 3), i =>
            Enumerable.Range(0, 3).Any(j =>
                Math.Abs(costlier.Market.Buyers[i].QuantitiesByOrigin[j] -
                    baseline.Market.Buyers[i].QuantitiesByOrigin[j]) > 1e-4));
    }

    [Fact]
    public void ReorderingInputsDoesNotChangeCanonicalResult()
    {
        var a = Evaluate();
        var b = Evaluate(
            origins: Origins().Reverse(),
            buyers: Buyers().Reverse(),
            routes: Routes().Reverse(),
            profiles: Profiles().Reverse());
        Assert.Equal(a.Market.OriginPrices, b.Market.OriginPrices);
        Assert.Equal(a.MaximumQuotedVsImpliedFareResidual, b.MaximumQuotedVsImpliedFareResidual);
        Assert.Equal(a.FreightRevenueMinusRealCostMidpoint, b.FreightRevenueMinusRealCostMidpoint);
        Assert.Equal(a.OuterIterations, b.OuterIterations);
        foreach (var id in new[] { Shared, BranchA, BranchE, BranchF })
            Assert.Equal(a.ResourceLoadsMicrograms[id], b.ResourceLoadsMicrograms[id]);
    }

    [Fact]
    public void MissingDuplicateAndUnpricedPhysicalIncidenceFailClosed()
    {
        Assert.Throws<ArgumentException>(() => Evaluate(routes: Routes().Skip(1)));
        Assert.Throws<ArgumentException>(() => Evaluate(routes: Routes().Concat(Routes().Take(1))));
        Assert.Throws<ArgumentException>(() => Evaluate(profiles: Profiles().Skip(1)));
        Assert.Throws<ArgumentException>(() => Evaluate(profiles: Profiles().Concat(Profiles().Take(1))));
        Assert.Throws<ArgumentException>(() => Evaluate(
            routes: Routes().Select(x =>
                x.Route.Quote.RouteKey.SourceEconomicPointId == A &&
                x.Route.Quote.RouteKey.DestinationEconomicPointId == E
                    ? new EconomicInteriorPhysicalMarketIncidence(
                        new EconomicReclearingQuotedRoute(x.Route.Quote,
                            new[] { new EconomicPhysicalResourceId(999UL) }), 1, .1)
                    : x)));
    }

    [Fact]
    public void ZeroCostPolicyClearsWithoutAnyFinancialPosting()
    {
        var zero = new[]
        {
            new EconomicDensityResourceCostProfile(Shared, 0, 0, 0, 25),
            new EconomicDensityResourceCostProfile(BranchA, 0, 0, 0, 20),
            new EconomicDensityResourceCostProfile(BranchE, 0, 0, 0, 20),
            new EconomicDensityResourceCostProfile(BranchF, 0, 0, 0, 20)
        };
        var world = WorldState.CreateInitial();
        var before = new CanonicalWorldStateHasher().Compute(world);
        var result = Evaluate(profiles: zero, routes: Routes(initialFare: 0.0));
        Assert.Equal(1, result.OuterIterations);
        Assert.Equal(0.0, result.Market.FreightReceipts);
        Assert.Equal(0.0, ToDouble(result.RealResourceCostUpper));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void WrongTariffSeedDoesNotPreventASmallPhysicalEquilibrium()
    {
        var a = Evaluate(routes: Routes(initialFare: 0.1));
        var b = Evaluate(routes: Routes(initialFare: 0.3));
        for (var j = 0; j < 3; j++)
            Assert.InRange(Math.Abs(a.Market.OriginPrices[j] -
                b.Market.OriginPrices[j]), 0, 2e-6);
        Assert.InRange(b.MaximumQuotedVsImpliedFareResidual, 0, 1e-7);
    }
}
