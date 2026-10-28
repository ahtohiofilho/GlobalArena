using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicReferenceScenarioReclearingProbeTests
{
    private const ulong One = EconomicTransferPolicy.FixedPointDenominator;

    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId E = new(2UL);
    private static readonly EconomicPointId F = new(3UL);
    private static readonly CommodityId Grain = new(7U);
    private static readonly EconomicPhysicalResourceId Ad = new(11UL);
    private static readonly EconomicPhysicalResourceId De = new(12UL);
    private static readonly EconomicPhysicalResourceId Df = new(13UL);
    private static readonly EconomicPhysicalResourceId Transfer = new(14UL);
    private static readonly EconomicPhysicalResourceId Direct = new(15UL);

    private sealed class LinearValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong allocated)
        {
            var n = (BigInteger)300_000UL * One - (BigInteger)40_000UL * allocated;
            if (n.Sign < 0)
                throw new InvalidOperationException("Value curve outside its valid domain.");
            return new FoodTransportCostFraction(n, One);
        }

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong allocated)
        {
            var q = (BigInteger)allocated;
            var n = 2 * (BigInteger)300_000UL * One * q
                - 40_000 * q * q;
            if (n.Sign < 0)
                throw new InvalidOperationException("Value curve outside its valid domain.");
            return new FoodTransportCostFraction(n, 2 * (BigInteger)One * One);
        }
    }

    private sealed class ConstantFreight(ulong rawPerGram) : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms) =>
            new(rawPerGram, BigInteger.One);
    }

    private static EconomicTurnFreightQuote Quote(
        ulong destination, ulong rawPerGram, ulong turn = 1UL) =>
        EconomicTurnFreightQuote.FromForecast(
            turn, new StrategicEconomicRouteKey(A, new EconomicPointId(destination)),
            6_000_000, new ConstantFreight(rawPerGram));

    private static EconomicReclearingQuotedRoute[] BaselineRoutes(ulong turn = 1UL) => new[]
    {
        new EconomicReclearingQuotedRoute(Quote(2UL, 90_000UL, turn),
            new[] { Ad, De, Transfer }),
        new EconomicReclearingQuotedRoute(Quote(3UL, 80_000UL, turn),
            new[] { Ad, Df })
    };

    private static EconomicReclearingQuotedRoute[] ProposedRoutes(ulong turn = 1UL) => new[]
    {
        new EconomicReclearingQuotedRoute(Quote(2UL, 20_000UL, turn),
            new[] { Direct }),
        new EconomicReclearingQuotedRoute(Quote(3UL, 80_000UL, turn),
            new[] { Ad, Df })
    };

    private static EconomicDensityResourceCostProfile[] Profiles() => new[]
    {
        new EconomicDensityResourceCostProfile(Ad, 400, 50, 2000, 15),
        new EconomicDensityResourceCostProfile(De, 100, 20, 400, 20),
        new EconomicDensityResourceCostProfile(Df, 150, 20, 400, 20),
        new EconomicDensityResourceCostProfile(Transfer, 60, 15, 300, 15),
        new EconomicDensityResourceCostProfile(Direct, 160, 1100, 250, 24)
    };

    private static EconomicProductionDemandResult Flows() => new(
        new[] { new EconomicSupplyFlow(
            A, Grain, EconomicActivityKind.Agriculture, 6 * One) },
        new[]
        {
            new EconomicDemandFlow(E, Grain, 5 * One),
            new EconomicDemandFlow(F, Grain, 5 * One)
        });

    private static FoodCommodityCatalog Foods() => new(new[]
    {
        new FoodCommodityDefinition(Grain, "grain", 1UL)
    });

    private static EconomicTurnCommodityValueOffer[] Values() => new[]
    {
        new EconomicTurnCommodityValueOffer(E, Grain, new LinearValue()),
        new EconomicTurnCommodityValueOffer(F, Grain, new LinearValue())
    };

    private static EconomicTurnSourceReservation[] Reservations() => new[]
    {
        new EconomicTurnSourceReservation(A, Grain,
            EconomicActivityKind.Agriculture, 10_000UL)
    };

    private static EconomicReferenceScenarioComparison Run(
        IEnumerable<EconomicReclearingQuotedRoute>? baseline = null,
        IEnumerable<EconomicReclearingQuotedRoute>? proposed = null,
        IEnumerable<EconomicDensityResourceCostProfile>? profiles = null) =>
        new EconomicReferenceScenarioReclearingProbe().Evaluate(
            1UL, Flows(), Foods(), profiles ?? Profiles(),
            baseline ?? BaselineRoutes(),
            proposed ?? ProposedRoutes(),
            Values(), Reservations(),
            new EconomicCongestionMarketSearchPolicy(One, 30));

    private static BigInteger TotalAt(
        EconomicReclearedReferenceScenario x, EconomicPointId destination) =>
        x.Trades.Where(t => t.DestinationEconomicPointId == destination)
            .Aggregate(BigInteger.Zero, (s, t) => s + t.RawQuantity);

    [Fact]
    public void DistinctFrozenQuotesProduceDistinctFreshDemandValueClearings()
    {
        var r = Run();
        Assert.True(TotalAt(r.Proposed, E) > TotalAt(r.Baseline, E));
        Assert.Equal(new BigInteger(6 * One), r.Baseline.RawTradedQuantity);
        Assert.Equal(new BigInteger(6 * One), r.Proposed.RawTradedQuantity);
        Assert.Equal(new BigInteger(6 * One), TotalAt(r.Baseline, E) + TotalAt(r.Baseline, F));
        Assert.Equal(new BigInteger(6 * One), TotalAt(r.Proposed, E) + TotalAt(r.Proposed, F));

        // Demand value is re-evaluated as each destination receives goods.
        var eTrades = r.Proposed.Trades.Where(x => x.DestinationEconomicPointId == E).ToArray();
        Assert.True(eTrades.Length >= 2);
        Assert.True(eTrades[0].RawMarginalCommodityUnitValueAtDecision.CompareTo(
            eTrades[1].RawMarginalCommodityUnitValueAtDecision) > 0);
    }

    [Fact]
    public void CostsAreEvaluatedOnActualClearedTradeMassNotProposedForecastLots()
    {
        var r = Run();
        Assert.Equal(TotalAt(r.Baseline, E) + TotalAt(r.Baseline, F),
            r.Baseline.ResourceLoadsMicrograms[Ad]);
        Assert.Equal(TotalAt(r.Baseline, E),
            r.Baseline.ResourceLoadsMicrograms[De]);
        Assert.Equal(TotalAt(r.Baseline, E),
            r.Baseline.ResourceLoadsMicrograms[Transfer]);
        Assert.Equal(TotalAt(r.Baseline, F),
            r.Baseline.ResourceLoadsMicrograms[Df]);
        Assert.False(r.Baseline.ResourceLoadsMicrograms.ContainsKey(Direct));

        Assert.Equal(TotalAt(r.Proposed, E),
            r.Proposed.ResourceLoadsMicrograms[Direct]);
        Assert.Equal(TotalAt(r.Proposed, F),
            r.Proposed.ResourceLoadsMicrograms[Ad]);
        Assert.Equal(TotalAt(r.Proposed, F),
            r.Proposed.ResourceLoadsMicrograms[Df]);
        Assert.False(r.Proposed.ResourceLoadsMicrograms.ContainsKey(De));

        // A pre-turn forecast is not a physical realization.
        Assert.NotEqual(new BigInteger(6_000_000),
            r.Proposed.ResourceLoadsMicrograms[Direct]);
        Assert.True(r.Baseline.RealCostLower.CompareTo(r.Baseline.RealCostUpper) <= 0);
        Assert.True(r.Proposed.RealCostLower.CompareTo(r.Proposed.RealCostUpper) <= 0);
    }

    [Fact]
    public void RealCostDeltaHasCertifiedOrderedRationalBoundsButNoApprovalDecision()
    {
        var r = Run();
        Assert.True(r.RealCostDeltaLowerDenominator > 0);
        Assert.True(r.RealCostDeltaUpperDenominator > 0);
        Assert.True(
            r.SignedRealCostDeltaLowerNumerator * r.RealCostDeltaUpperDenominator <=
            r.SignedRealCostDeltaUpperNumerator * r.RealCostDeltaLowerDenominator);
        Assert.NotEmpty(r.Baseline.Trades);
        Assert.NotEmpty(r.Proposed.Trades);
    }

    [Fact]
    public void EnumerationAndRepeatedResolutionAreDeterministicWithoutWorldMutation()
    {
        var a = Run();
        var b = Run(
            baseline: BaselineRoutes().Reverse(),
            proposed: ProposedRoutes().Reverse(),
            profiles: Profiles().Reverse());
        Assert.Equal(a.SignedRealCostDeltaLowerNumerator,
            b.SignedRealCostDeltaLowerNumerator);
        Assert.Equal(a.RealCostDeltaLowerDenominator,
            b.RealCostDeltaLowerDenominator);
        Assert.Equal(a.SignedRealCostDeltaUpperNumerator,
            b.SignedRealCostDeltaUpperNumerator);
        Assert.Equal(a.RealCostDeltaUpperDenominator,
            b.RealCostDeltaUpperDenominator);
        Assert.Equal(a.Baseline.RawTradedQuantity, b.Baseline.RawTradedQuantity);
        Assert.Equal(a.Proposed.ResourceLoadsMicrograms[Direct],
            b.Proposed.ResourceLoadsMicrograms[Direct]);
    }

    [Fact]
    public void QuotedRouteMustHaveOneDeclaredPhysicalResourcePerIncidence()
    {
        Assert.Throws<ArgumentException>(() =>
            new EconomicReclearingQuotedRoute(Quote(2, 90_000),
                new[] { Ad, Ad }));
        Assert.Throws<ArgumentException>(() => Run(
            proposed: ProposedRoutes().Concat(ProposedRoutes().Take(1))));
        Assert.Throws<ArgumentException>(() => Run(
            proposed: ProposedRoutes(turn: 2)));
    }

    [Fact]
    public void MissingResourceProfileFailsInsteadOfCreatingFreeFreight()
    {
        Assert.Throws<ArgumentException>(() => Run(
            profiles: Profiles().Where(p => p.ResourceId != Direct)));
    }

    [Fact]
    public void ReferencePriceClearingCannotBypassSupplyOrDemandLimits()
    {
        var r = Run();
        Assert.All(new[] { r.Baseline, r.Proposed }, scenario =>
        {
            Assert.True(scenario.RawTradedQuantity <= 6 * One);
            Assert.True(TotalAt(scenario, E) <= 5 * One);
            Assert.True(TotalAt(scenario, F) <= 5 * One);
            Assert.All(scenario.Trades, t =>
                Assert.True(t.RawNetReturn.Numerator.Sign > 0));
        });
    }
}
