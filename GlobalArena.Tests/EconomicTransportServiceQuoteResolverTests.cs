using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicTransportServiceQuoteResolverTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    private sealed class TestFreightFunction(ulong baseCost)
        : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Calls { get; private set; }

        // Synthetic continuous decreasing cost in expected freight volume,
        // testing a caller-defined function, not a modal tariff catalogue.
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Calls++;
            var d = expectedLoadMicrograms + 100_000_000;
            return new FoodTransportCostFraction(
                (BigInteger)baseCost * d + 60_000_000_000, d);
        }
    }

    private sealed class CommodityValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new((BigInteger)600_000 * Unit - 10_000 * (BigInteger)q, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(2 * (BigInteger)600_000 * q * Unit -
                10_000 * (BigInteger)q * q, 2 * (BigInteger)Unit * Unit);
    }

    private static (
        StrategicSurfaceGraph Graph,
        EconomyRuntimeState Economy,
        EconomicProductionDemandResult Flows,
        StrategicEdge Edge) Fixture()
    {
        var topology = GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(2, 0));
        var edge = topology.Edges[0];
        var economy = new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(
                new EconomicPointId(1UL), new CivilizationId(1UL),
                edge.IncidentCellIds[0], 0UL),
            new EconomicPointRuntimeState(
                new EconomicPointId(2UL), new CivilizationId(1UL),
                edge.IncidentCellIds[1], 0UL)
        });
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 3UL * Unit) },
            new[] { new EconomicDemandFlow(
                new EconomicPointId(2UL), new CommodityId(7U),
                3UL * Unit) });
        return (new StrategicSurfaceGraph(topology), economy, flows, edge);
    }

    private static StrategicEconomicRouteKey Route() => new(
        new EconomicPointId(1UL), new EconomicPointId(2UL));

    private static IReadOnlyList<EconomicTransportServiceSelection> Select(
        ulong turn, StrategicSurfaceGraph graph,
        EconomyRuntimeState economy,
        EconomicProductionDemandResult flows,
        IEnumerable<EconomicTransportBuiltAsset> assets,
        IEnumerable<EconomicTransportServiceOffer> offers,
        StrategicEdgeAccessSnapshot? access = null) =>
        new EconomicTransportServiceQuoteResolver().Resolve(
            turn, graph, economy, flows,
            access ?? StrategicEdgeAccessSnapshot.AllOpen, assets, offers);

    [Fact]
    public void ParallelTechnologyAssetsRemainSeparateAndCheapestExplicitServiceWins()
    {
        var (graph, economy, flows, edge) = Fixture();
        var assets = new[]
        {
            new EconomicTransportBuiltAsset(1, 10, edge.Id, 1UL),
            new EconomicTransportBuiltAsset(2, 20, edge.Id, 1UL)
        };
        var firstPrice = new TestFreightFunction(100);
        var secondPrice = new TestFreightFunction(40);
        var offers = new[]
        {
            new EconomicTransportServiceOffer(10, Route(), new[] { 1UL },
                Array.Empty<int>(), new BigInteger(500_000_000), firstPrice),
            new EconomicTransportServiceOffer(20, Route(), new[] { 2UL },
                Array.Empty<int>(), new BigInteger(500_000_000), secondPrice)
        };
        var beforeHash = new CanonicalWorldStateHasher().Compute(
            WorldState.CreateInitial());

        var selected = Assert.Single(Select(
            1UL, graph, economy, flows, assets, offers));
        Assert.Equal(20UL, selected.ServiceId);
        Assert.Equal(new[] { 2UL }, selected.AssetIds);
        Assert.Equal(new[] { 20UL }, selected.TechnologyTypeIds);
        Assert.Equal(1UL, selected.TechnologyCatalogVersion);
        Assert.Equal(1, firstPrice.Calls);
        Assert.Equal(1, secondPrice.Calls);
        Assert.Equal(1UL, selected.Quote.TurnOrdinal);
        Assert.Equal(Route(), selected.Quote.RouteKey);

        var market = new EconomicTurnLockedFreightMarketResolver();
        var foods = new FoodCommodityCatalog(new[]
        {
            new FoodCommodityDefinition(new CommodityId(7U), "test-grain", 1_000UL)
        });
        var valuations = new[]
        {
            new EconomicTurnCommodityValueOffer(
                new EconomicPointId(2UL), new CommodityId(7U),
                new CommodityValue())
        };
        var reservations = new[]
        {
            new EconomicTurnSourceReservation(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 20_000UL)
        };
        var trades = market.Resolve(
            1UL, flows, foods, selected.Select(x => x.Quote),
            valuations, reservations,
            new EconomicCongestionMarketSearchPolicy(Unit, 10));
        Assert.Equal(3, trades.Count);
        Assert.All(trades, trade =>
        {
            Assert.Same(selected.Quote, trade.FreightQuote);
            Assert.Equal(0, trade.RawFreightCharge.CompareTo(
                selected.Quote.ChargeForRawQuantity(
                    trade.RawQuantity, 1_000UL)));
        });
        Assert.True(trades[0].RawMarginalCommodityUnitValueAtDecision
            .CompareTo(trades[1].RawMarginalCommodityUnitValueAtDecision) > 0);
        Assert.Equal(1, firstPrice.Calls);
        Assert.Equal(1, secondPrice.Calls);
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(
            WorldState.CreateInitial()));

        // Changing the pre-turn expectation is permitted only for a new
        // economic turn; it does not retroactively change prior trades.
        var repriced = Assert.Single(Select(
            2UL, graph, economy, flows, assets,
            new[]
            {
                new EconomicTransportServiceOffer(10, Route(),
                    new[] { 1UL }, Array.Empty<int>(),
                    new BigInteger(8_000_000_000), firstPrice),
                new EconomicTransportServiceOffer(20, Route(),
                    new[] { 2UL }, Array.Empty<int>(),
                    new BigInteger(8_000_000_000), secondPrice)
            }));
        Assert.True(repriced.Quote.RawUnitCostPerGram.CompareTo(
            selected.Quote.RawUnitCostPerGram) < 0);
        Assert.Equal(1UL, selected.Quote.TurnOrdinal);
        Assert.All(trades, t => Assert.Same(selected.Quote, t.FreightQuote));
    }

    [Fact]
    public void UnbuiltUnavailableOrClosedTransportCannotCreateMarketLane()
    {
        var (graph, economy, flows, edge) = Fixture();
        var price = new TestFreightFunction(80);
        var available = new EconomicTransportServiceOffer(
            1, Route(), new[] { 1UL }, Array.Empty<int>(),
            BigInteger.Zero, price);
        var built = new[] { new EconomicTransportBuiltAsset(1, 10, edge.Id, 1UL) };
        Assert.Empty(Select(1, graph, economy, flows, built,
            Array.Empty<EconomicTransportServiceOffer>()));

        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows,
                Array.Empty<EconomicTransportBuiltAsset>(), new[] { available }));
        Assert.Empty(Select(1, graph, economy, flows,
            new[] { new EconomicTransportBuiltAsset(
                1, 10, edge.Id, 1UL, isOperational: false) },
            new[] { available }));
        Assert.Empty(Select(1, graph, economy, flows,
            built, new[] { available },
            new StrategicEdgeAccessSnapshot(new[] { edge.Id })));
        Assert.Empty(Select(1, graph, economy, flows, built,
            new[] { new EconomicTransportServiceOffer(
                1, Route(), new[] { 1UL }, Array.Empty<int>(),
                BigInteger.Zero, price, isAvailable: false) }));
        Assert.Equal(0, price.Calls);

        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows,
                new[] { built[0], built[0] }, new[] { available }));
        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows,
                new[] { new EconomicTransportBuiltAsset(
                    1, 10, new StrategicEdgeId(9_999_999UL), 1UL) },
                new[] { available }));
    }

    [Fact]
    public void EquivalentServicesChooseStableIdAndRejectDuplicateIds()
    {
        var (graph, economy, flows, edge) = Fixture();
        var built = new[]
        {
            new EconomicTransportBuiltAsset(1, 10, edge.Id, 1UL),
            new EconomicTransportBuiltAsset(2, 20, edge.Id, 1UL)
        };
        var a = new EconomicTransportServiceOffer(200, Route(),
            new[] { 1UL }, Array.Empty<int>(), BigInteger.Zero,
            new TestFreightFunction(80));
        var b = new EconomicTransportServiceOffer(100, Route(),
            new[] { 2UL }, Array.Empty<int>(), BigInteger.Zero,
            new TestFreightFunction(80));

        Assert.Equal(100UL,
            Assert.Single(Select(1, graph, economy, flows, built,
                new[] { a, b })).ServiceId);
        Assert.Equal(100UL,
            Assert.Single(Select(1, graph, economy, flows,
                built.Reverse(), new[] { b, a })).ServiceId);
        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows, built, new[] { a, a }));
    }

    [Fact]
    public void MultitechItineraryNeedsExplicitHandoffAndRealContiguousAssets()
    {
        var (graph, _, _, edge) = Fixture();
        var first = edge.IncidentCellIds[0];
        var middle = edge.IncidentCellIds[1];
        var otherEdge = graph.StrategicTopology.Edges.First(x =>
            x.Id != edge.Id && x.IncidentCellIds.Contains(middle) &&
            !x.IncidentCellIds.Contains(first));
        var end = otherEdge.IncidentCellIds.First(x => x != middle);
        var economy = new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(new EconomicPointId(1UL),
                new CivilizationId(1UL), first, 0UL),
            new EconomicPointRuntimeState(new EconomicPointId(2UL),
                new CivilizationId(1UL), end, 0UL)
        });
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(new EconomicPointId(1UL),
                new CommodityId(7U), EconomicActivityKind.Agriculture, Unit) },
            new[] { new EconomicDemandFlow(new EconomicPointId(2UL),
                new CommodityId(7U), Unit) });
        var built = new[]
        {
            new EconomicTransportBuiltAsset(1, 10, edge.Id, 1UL),
            new EconomicTransportBuiltAsset(2, 20, otherEdge.Id, 1UL)
        };
        var price = new TestFreightFunction(80);
        EconomicTransportServiceOffer Offer(params int[] handoffs) =>
            new(1, Route(), new[] { 1UL, 2UL }, handoffs,
                BigInteger.Zero, price);

        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows, built,
                new[] { Offer() }));
        var selected = Assert.Single(Select(1, graph, economy,
            flows, built, new[] { Offer(0) }));
        Assert.Equal(new[] { 1UL, 2UL }, selected.AssetIds);
        Assert.Equal(new[] { 10UL, 20UL }, selected.TechnologyTypeIds);
        Assert.Equal(1, price.Calls);
        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows,
                new[]
                {
                    built[0],
                    new EconomicTransportBuiltAsset(
                        2, 20, otherEdge.Id, 2UL)
                },
                new[] { Offer(0) }));
        Assert.Throws<ArgumentException>(() =>
            Select(1, graph, economy, flows,
                new[] { built[1], built[0] },
                new[] { new EconomicTransportServiceOffer(
                    1, Route(), new[] { 2UL, 1UL }, new[] { 0 },
                    BigInteger.Zero, price) }));
    }
}
