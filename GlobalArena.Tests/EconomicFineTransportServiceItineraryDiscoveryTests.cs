using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFineTransportServiceItineraryDiscoveryTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(4, 0);
    private static readonly GoldbergParameters Coarse = new(2, 0);

    private sealed class CostFunction(ulong baseCost) : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Calls { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Calls++;
            var denominator = expectedLoadMicrograms + 100_000_000;
            // Synthetic-only smooth economies of scale; not a mode tariff.
            return new FoodTransportCostFraction(
                baseCost * denominator + 60_000_000_000, denominator);
        }
    }

    private sealed class PriceFactory : IEconomicFineTransportItineraryPricing
    {
        public int Calls { get; private set; }
        public List<CostFunction> Functions { get; } = new();
        public List<ulong[]> SeenPaths { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
        {
            Calls++;
            var path = orderedAssets.Select(x => x.AssetId).ToArray();
            SeenPaths.Add(path);
            Assert.All(orderedAssets, a =>
            {
                Assert.Equal(intent.TechnologyTypeId, a.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion, a.TechnologyCatalogVersion);
            });
            var cost = new CostFunction(
                checked((ulong)(20 + orderedAssets.Count * 30 +
                    (intent.TechnologyTypeId == 20UL ? 10 : 0))));
            Functions.Add(cost);
            return cost;
        }
    }

    private sealed class CommodityValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new((BigInteger)800_000 * Unit - (BigInteger)10_000 * q, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(2 * (BigInteger)800_000 * q * Unit -
                (BigInteger)10_000 * q * q, 2 * (BigInteger)Unit * Unit);
    }

    private static StrategicEconomicRouteKey Route() =>
        new(new EconomicPointId(1UL), new EconomicPointId(2UL));

    private static EconomicFineTransportServiceIntent Intent(
        ulong serviceId = 1UL, ulong technology = 10UL,
        ulong catalogVersion = 3UL, ulong forecast = 500_000_000UL,
        bool available = true) =>
        new(serviceId, Route(), technology, catalogVersion,
            new BigInteger(forecast), available);

    private static (
        StrategicSurfaceGraph Graph,
        StrategicEdge First,
        StrategicEdge Second,
        Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors)
        TwoLegFixture()
    {
        var graph = new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(Fine));
        var first = graph.StrategicTopology.Edges[0];
        var start = first.IncidentCellIds[0];
        var middle = first.IncidentCellIds[1];
        var second = graph.StrategicTopology.Edges.First(e =>
            e.Id != first.Id &&
            e.IncidentCellIds.Contains(middle) &&
            !e.IncidentCellIds.Contains(start));
        var finish = second.IncidentCellIds.First(x => x != middle);
        return (graph, first, second, new()
        {
            [new EconomicPointId(1UL)] = new PhysicalTacticalTileId(Fine, start),
            [new EconomicPointId(2UL)] = new PhysicalTacticalTileId(Fine, finish)
        });
    }

    private static EconomicTransportBuiltAsset Asset(
        ulong assetId, ulong technologyId, StrategicEdge edge,
        ulong catalogVersion = 3UL, bool operational = true,
        GoldbergParameters? resolution = null) =>
        new(assetId, technologyId,
            new PhysicalTacticalEdgeId(resolution ?? Fine, edge.Id),
            catalogVersion, operational);

    private static IReadOnlyList<EconomicTransportServiceOffer> Discover(
        StrategicSurfaceGraph graph,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors,
        IEnumerable<EconomicTransportBuiltAsset> assets,
        IEnumerable<EconomicFineTransportServiceIntent> intents,
        PriceFactory factory,
        StrategicEdgeAccessSnapshot? access = null) =>
        new EconomicFineTransportServiceItineraryDiscovery().Discover(
            graph, anchors, access ?? StrategicEdgeAccessSnapshot.AllOpen,
            assets, intents, factory);

    [Fact]
    public void FindsBuiltTwoLegRouteInDeterministicAssetOrderOnly()
    {
        var (graph, first, second, anchors) = TwoLegFixture();
        // Parallel first-corridor assets: smallest asset ID always wins
        // under the explicit hop-minimal/canonical-asset discovery rule.
        var assets = new[]
        {
            Asset(20UL, 10UL, first),
            Asset(4UL, 10UL, second),
            Asset(3UL, 10UL, first)
        };
        var factory = new PriceFactory();
        var offers = Discover(graph, anchors, assets,
            new[] { Intent() }, factory);
        var found = Assert.Single(offers);
        Assert.Equal(new[] { 3UL, 4UL }, found.AssetPath);
        Assert.Empty(found.TransferAfterLeg);
        Assert.Equal(Route(), found.RouteKey);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(new[] { 3UL, 4UL }, Assert.Single(factory.SeenPaths));
        Assert.Equal(0, Assert.Single(factory.Functions).Calls);

        var reordered = Discover(graph, anchors, assets.Reverse(),
            new[] { Intent() }, new PriceFactory());
        Assert.Equal(found.AssetPath, Assert.Single(reordered).AssetPath);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EconomicTransportServiceOffer>)offers).Clear());

        Assert.Empty(Discover(graph, anchors,
            Array.Empty<EconomicTransportBuiltAsset>(),
            new[] { Intent() }, new PriceFactory()));
    }

    [Fact]
    public void GeneratedParallelServicesResolveToLockedQuotesAndDynamicMarkets()
    {
        var (graph, first, second, anchors) = TwoLegFixture();
        var assets = new[]
        {
            Asset(10UL, 10UL, first), Asset(11UL, 10UL, second),
            Asset(20UL, 20UL, first), Asset(21UL, 20UL, second)
        };
        var factory = new PriceFactory();
        var offers = Discover(graph, anchors, assets,
            new[] { Intent(200UL, 20UL), Intent(100UL, 10UL) },
            factory);

        Assert.Equal(new[] { 100UL, 200UL },
            offers.Select(x => x.ServiceId));
        Assert.Equal(2, factory.Calls);
        Assert.Equal(new[] { 10UL, 11UL }, offers[0].AssetPath);
        Assert.Equal(new[] { 20UL, 21UL }, offers[1].AssetPath);

        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 3 * Unit) },
            new[] { new EconomicDemandFlow(
                new EconomicPointId(2UL), new CommodityId(7U), 3 * Unit) });
        var selections = new EconomicTransportServiceQuoteResolver().Resolve(
            1UL, graph, anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, assets, offers);
        var best = Assert.Single(selections);
        Assert.Equal(100UL, best.ServiceId);
        Assert.Equal(new[] { 10UL, 11UL }, best.AssetIds);
        Assert.Equal(Fine, best.FineTopologyParameters);
        Assert.All(factory.Functions, function => Assert.Equal(1, function.Calls));

        var hashState = WorldState.CreateInitial();
        var originalHash = new CanonicalWorldStateHasher().Compute(hashState);
        var trades = new EconomicTurnLockedFreightMarketResolver().Resolve(
            1UL, flows, new FoodCommodityCatalog(new[]
            {
                new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
            }),
            selections.Select(x => x.Quote),
            new[] { new EconomicTurnCommodityValueOffer(
                new EconomicPointId(2UL), new CommodityId(7U), new CommodityValue()) },
            new[] { new EconomicTurnSourceReservation(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 30_000UL) },
            new EconomicCongestionMarketSearchPolicy(Unit, 10),
            selectionMode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex);

        Assert.Equal(3, trades.Count);
        Assert.All(trades, t =>
        {
            Assert.Same(best.Quote, t.FreightQuote);
            Assert.Equal(0, t.RawFreightCharge.CompareTo(
                best.Quote.ChargeForRawQuantity(t.RawQuantity, 1_000UL)));
        });
        Assert.True(trades[0].RawMarginalCommodityUnitValueAtDecision.CompareTo(
            trades[1].RawMarginalCommodityUnitValueAtDecision) > 0);
        Assert.All(factory.Functions, function => Assert.Equal(1, function.Calls));
        Assert.Equal(originalHash, new CanonicalWorldStateHasher().Compute(hashState));

        // Next economic-cycle forecast can reprice; previous cycle remains frozen.
        var nextOffers = Discover(graph, anchors, assets,
            new[] { Intent(100UL, 10UL, forecast: 8_000_000_000UL) },
            new PriceFactory());
        var nextQuote = Assert.Single(
            new EconomicTransportServiceQuoteResolver().Resolve(
                2UL, graph, anchors, flows,
                StrategicEdgeAccessSnapshot.AllOpen, assets, nextOffers));
        Assert.True(nextQuote.Quote.RawUnitCostPerGram.CompareTo(
            best.Quote.RawUnitCostPerGram) < 0);
        Assert.All(trades, t => Assert.Same(best.Quote, t.FreightQuote));
    }

    [Fact]
    public void DoesNotCreateFreeRoutesTransfersOrUnavailableService()
    {
        var (graph, first, second, anchors) = TwoLegFixture();
        var prices = new PriceFactory();
        var splitModes = new[]
        {
            Asset(1UL, 10UL, first), Asset(2UL, 20UL, second)
        };
        Assert.Empty(Discover(graph, anchors, splitModes,
            new[] { Intent(1UL, 10UL), Intent(2UL, 20UL) }, prices));
        Assert.Equal(0, prices.Calls);
        Assert.Empty(Discover(graph, anchors,
            new[] { Asset(1UL, 10UL, first) },
            new[] { Intent() }, prices));
        Assert.Empty(Discover(graph, anchors,
            new[] { Asset(1UL, 10UL, first), Asset(2UL, 10UL, second,
                catalogVersion: 4UL) },
            new[] { Intent() }, prices));
        Assert.Empty(Discover(graph, anchors,
            new[] { Asset(1UL, 10UL, first), Asset(2UL, 10UL, second) },
            new[] { Intent(available: false) }, prices));

        Assert.Empty(Discover(graph, anchors,
            new[] { Asset(1UL, 10UL, first, operational: false),
                Asset(2UL, 10UL, second) },
            new[] { Intent() }, prices));
        Assert.Empty(Discover(graph, anchors,
            new[] { Asset(1UL, 10UL, first), Asset(2UL, 10UL, second) },
            new[] { Intent() }, prices,
            new StrategicEdgeAccessSnapshot(new[] { first.Id })));
        Assert.Equal(0, prices.Calls);
    }

    [Fact]
    public void NewGeographySnapshotReroutesWithoutMutatingPreviousOffer()
    {
        var (graph, first, second, anchors) = TwoLegFixture();
        var assets = new[] { Asset(1UL, 10UL, first), Asset(2UL, 10UL, second) };
        var prices = new PriceFactory();
        var firstOffer = Assert.Single(Discover(graph, anchors, assets,
            new[] { Intent() }, prices));
        Assert.Equal(new[] { 1UL, 2UL }, firstOffer.AssetPath);

        var closed = new StrategicEdgeAccessSnapshot(new[] { first.Id });
        var newOffers = Discover(graph, anchors, assets,
            new[] { Intent() }, prices, closed);
        Assert.Empty(newOffers);
        Assert.Equal(new[] { 1UL, 2UL }, firstOffer.AssetPath);
        Assert.Equal(1, prices.Calls);

        var rebuilt = Assert.Single(Discover(graph, anchors,
            assets.Reverse(), new[] { Intent() }, prices));
        Assert.Equal(firstOffer.AssetPath, rebuilt.AssetPath);
    }

    [Fact]
    public void FailsClosedOnForeignGeographyDuplicateAssetsAndIntentIdentity()
    {
        var (graph, first, second, anchors) = TwoLegFixture();
        var a = Asset(1UL, 10UL, first);
        var b = Asset(2UL, 10UL, second);
        var prices = new PriceFactory();

        Assert.Throws<ArgumentException>(() => Discover(graph, anchors,
            new[] { a, a }, new[] { Intent() }, prices));
        Assert.Throws<ArgumentException>(() => Discover(graph, anchors,
            new[] { Asset(1UL, 10UL, first, resolution: Coarse) },
            new[] { Intent() }, prices));
        Assert.Throws<ArgumentException>(() => Discover(graph, anchors,
            new[] { new EconomicTransportBuiltAsset(
                1UL, 10UL, first.Id, 3UL), b },
            new[] { Intent() }, prices));
        Assert.Throws<ArgumentException>(() => Discover(graph, anchors,
            new[] { a, b }, new[] { Intent(), Intent() }, prices));
        Assert.Throws<ArgumentException>(() => Discover(graph, anchors,
            new[] { a, b }, new[] { Intent() }, prices,
            new StrategicEdgeAccessSnapshot(new[]
            {
                new StrategicEdgeId(Fine.StrategicEdgeCount + 1UL)
            })));
        var wrong = new Dictionary<EconomicPointId, PhysicalTacticalTileId>(anchors)
        {
            [new EconomicPointId(1UL)] =
                new PhysicalTacticalTileId(Coarse, new StrategicCellId(1UL))
        };
        Assert.Throws<ArgumentException>(() =>
            Discover(graph, wrong, new[] { a, b },
                new[] { Intent() }, prices));
        var missing = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [new EconomicPointId(1UL)] = anchors[new EconomicPointId(1UL)]
        };
        Assert.Throws<ArgumentException>(() =>
            Discover(graph, missing, new[] { a, b },
                new[] { Intent() }, prices));
    }
}
