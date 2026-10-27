using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// Synthetic access-graph tests: routes are economic quotes, NOT constructed
/// physical roads, rails or ports. Preserve the existing turn-locked
/// transport charge and dynamically priced commodity-market contract.
/// </summary>
public sealed class EconomicTurnFreightSparseGraphTests
{
    private const ulong One = EconomicTransferPolicy.FixedPointDenominator;

    private sealed class ContinuousTestFreight : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Calls { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Calls++;
            var d = expectedLoadMicrograms + 1_000_000_000;
            return new FoodTransportCostFraction(
                20 * d + (BigInteger)120_000 * 1_000_000, d);
        }
    }

    private sealed class DecreasingTestCommodity : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong alreadyAllocatedRawQuantity)
        {
            var n = (BigInteger)600_000 * One -
                (BigInteger)5_000 * alreadyAllocatedRawQuantity;
            return new FoodTransportCostFraction(n, One);
        }

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocatedRawQuantity)
        {
            var q = (BigInteger)allocatedRawQuantity;
            return new FoodTransportCostFraction(
                2 * (BigInteger)600_000 * q * One -
                    (BigInteger)5_000 * q * q,
                2 * (BigInteger)One * One);
        }
    }

    private static string Transcript(
        IReadOnlyList<EconomicTurnLockedFreightTrade> result) =>
        string.Join(";", result.Select(t => string.Join(":",
            t.DecisionOrdinal,
            t.SourceEconomicPointId.Value,
            t.DestinationEconomicPointId.Value,
            t.CommodityId.Value,
            t.RawQuantity,
            t.RawMarginalCommodityUnitValueAtDecision.Numerator,
            t.RawMarginalCommodityUnitValueAtDecision.Denominator,
            t.RawCommodityValue.Numerator, t.RawCommodityValue.Denominator,
            t.RawFreightCharge.Numerator, t.RawFreightCharge.Denominator,
            t.RawNetReturn.Numerator, t.RawNetReturn.Denominator)));

    [Theory]
    [InlineData(false, 24, 3)]
    [InlineData(true, 34, 3)]
    public void SparseLocalAndHubAccessRetainExactMarketParity(
        bool hub, int expectedQuotes, int localDegree)
    {
        const int count = 8;
        var foods = new FoodCommodityCatalog(new[]
        {
            new FoodCommodityDefinition(new CommodityId(7), "test-seven", 1_000UL),
            new FoodCommodityDefinition(new CommodityId(8), "test-eight", 1_500UL)
        });
        var supplies = new List<EconomicSupplyFlow>();
        var demands = new List<EconomicDemandFlow>();
        var reservations = new List<EconomicTurnSourceReservation>();
        var values = new List<EconomicTurnCommodityValueOffer>();
        var quotes = new List<EconomicTurnFreightQuote>();
        var freights = new ContinuousTestFreight();

        for (var s = 0; s < count; s++)
            for (uint commodity = 7; commodity <= 8; commodity++)
            {
                var source = new EconomicPointId((ulong)(s + 1));
                var good = new CommodityId(commodity);
                supplies.Add(new EconomicSupplyFlow(source, good,
                    EconomicActivityKind.Agriculture, 3 * One));
                reservations.Add(new EconomicTurnSourceReservation(
                    source, good, EconomicActivityKind.Agriculture, 10_000UL));
            }

        for (var d = 0; d < count; d++)
            for (uint commodity = 7; commodity <= 8; commodity++)
            {
                var destination = new EconomicPointId((ulong)(d + 101));
                var good = new CommodityId(commodity);
                demands.Add(new EconomicDemandFlow(destination, good, 4 * One));
                values.Add(new EconomicTurnCommodityValueOffer(
                    destination, good, new DecreasingTestCommodity()));
            }

        for (var sourceIndex = 0; sourceIndex < count; sourceIndex++)
            for (var destinationIndex = 0; destinationIndex < count; destinationIndex++)
            {
                var near = destinationIndex == sourceIndex ||
                    destinationIndex == (sourceIndex + 1) % count ||
                    destinationIndex == (sourceIndex + count - 1) % count;
                var quoted = near ||
                    (hub && (sourceIndex == 0 || destinationIndex == 0));
                if (!quoted)
                    continue;

                var route = new StrategicEconomicRouteKey(
                    new EconomicPointId((ulong)(sourceIndex + 1)),
                    new EconomicPointId((ulong)(destinationIndex + 101)));
                quotes.Add(EconomicTurnFreightQuote.FromForecast(
                    1UL, route, (BigInteger)(900 + sourceIndex * 100) * 1_000_000,
                    freights));
            }

        Assert.Equal(expectedQuotes, quotes.Count);
        Assert.Equal(expectedQuotes, freights.Calls);
        Assert.All(Enumerable.Range(0, count), src => Assert.True(
            quotes.Count(q =>
                q.RouteKey.SourceEconomicPointId.Value == (ulong)(src + 1)) >=
            localDegree));

        var resolver = new EconomicTurnLockedFreightMarketResolver();
        var flows = new EconomicProductionDemandResult(supplies, demands);
        var policy = new EconomicCongestionMarketSearchPolicy(One, 120);
        var baseDiagnostic = new EconomicTurnFreightMarketDiagnostics();
        var indexDiagnostic = new EconomicTurnFreightMarketDiagnostics();

        var exhaustive = resolver.Resolve(1UL, flows, foods, quotes,
            values, reservations, policy, baseDiagnostic);
        var indexed = resolver.Resolve(1UL, flows, foods, quotes,
            values, reservations, policy, indexDiagnostic,
            EconomicTurnFreightCandidateSelectionMode.DependencyIndex);

        Assert.Equal(Transcript(exhaustive), Transcript(indexed));
        Assert.True(exhaustive.Count > 0);
        Assert.True(indexDiagnostic.LaneExaminations <
            baseDiagnostic.LaneExaminations);
        Assert.Equal(expectedQuotes, freights.Calls); // no intra-turn requote
        Assert.All(indexed, trade => Assert.Contains(quotes, q =>
            q.RouteKey == trade.FreightQuote.RouteKey));
        Assert.Equal(indexed.Sum(t => (long)t.RawQuantity),
            exhaustive.Sum(t => (long)t.RawQuantity));
    }
}
