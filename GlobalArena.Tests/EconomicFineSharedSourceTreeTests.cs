using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFineSharedSourceTreeTests
{
    private static readonly GoldbergParameters Fine = new(4, 0);

    private sealed class Cost : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Calls { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Calls++;
            return new FoodTransportCostFraction(
                expectedLoadMicrograms + 1_000_000,
                expectedLoadMicrograms + 500_000);
        }
    }

    private sealed class Pricing : IEconomicFineTransportItineraryPricing
    {
        public List<Cost> Functions { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
        {
            Assert.NotEmpty(orderedAssets);
            Assert.All(orderedAssets, a =>
            {
                Assert.Equal(intent.TechnologyTypeId, a.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion,
                    a.TechnologyCatalogVersion);
            });
            var function = new Cost();
            Functions.Add(function);
            return function;
        }
    }

    private sealed record Fixture(
        StrategicSurfaceGraph Geography,
        Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors,
        StrategicEdge Direct,
        StrategicEdge Left,
        StrategicEdge Right,
        EconomicTransportBuiltAsset[] Assets);

    private static Fixture Make()
    {
        var graph = new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(Fine));
        var direct = graph.StrategicTopology.Edges[0];
        var a = direct.IncidentCellIds[0];
        var b = direct.IncidentCellIds[1];
        var edges = graph.StrategicTopology.Edges;
        var left = edges.First(edge =>
            edge.Id != direct.Id &&
            edge.IncidentCellIds.Contains(a) &&
            edges.Any(other =>
                other.Id != edge.Id && other.Id != direct.Id &&
                other.IncidentCellIds.Contains(b) &&
                other.IncidentCellIds.Any(x =>
                    x != a && x != b &&
                    edge.IncidentCellIds.Contains(x))));
        var c = left.IncidentCellIds.First(x => x != a);
        var right = edges.Single(edge =>
            edge.IncidentCellIds.Contains(b) &&
            edge.IncidentCellIds.Contains(c));
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [new EconomicPointId(1UL)] = new(Fine, a),
            [new EconomicPointId(2UL)] = new(Fine, b),
            [new EconomicPointId(3UL)] = new(Fine, c)
        };
        EconomicTransportBuiltAsset Asset(
            ulong id, StrategicEdge edge, ulong technology = 10UL) =>
            new(id, technology, new PhysicalTacticalEdgeId(Fine, edge.Id), 1UL);
        return new Fixture(graph, anchors, direct, left, right, new[]
        {
            Asset(90UL, direct),
            Asset(20UL, left),
            Asset(21UL, right)
        });
    }

    private static EconomicFineTransportServiceIntent Intent(
        ulong service, ulong destination, ulong technology = 10UL) =>
        new(service, new StrategicEconomicRouteKey(
            new EconomicPointId(1UL), new EconomicPointId(destination)),
            technology, 1UL, new BigInteger(600_000_000));

    private static IReadOnlyList<EconomicTransportServiceOffer> Discover(
        Fixture fixture, IEnumerable<EconomicTransportBuiltAsset> assets,
        IEnumerable<EconomicFineTransportServiceIntent> intents,
        EconomicFineTransportPathReuseDiagnostics diagnostics,
        Pricing pricing, EconomicFineTransportPathCache? cache = null,
        StrategicEdgeAccessSnapshot? access = null) =>
        new EconomicFineTransportServiceItineraryDiscovery().Discover(
            fixture.Geography, fixture.Anchors,
            access ?? StrategicEdgeAccessSnapshot.AllOpen,
            assets, intents, pricing, cache, diagnostics);

    [Fact]
    public void TwoDestinationsInPhysicalCycleShareOneCanonicalSourceTree()
    {
        var f = Make();
        var pricing = new Pricing();
        var counters = new EconomicFineTransportPathReuseDiagnostics();
        var first = Discover(f, f.Assets,
            new[] { Intent(2UL, 3UL), Intent(1UL, 2UL) },
            counters, pricing);
        Assert.Equal(2, first.Count);
        Assert.Equal(new[] { 1UL, 2UL }, first.Select(x => x.ServiceId));
        Assert.Equal(new[] { 90UL }, first[0].AssetPath);
        Assert.Equal(new[] { 20UL }, first[1].AssetPath);
        Assert.Equal(1, counters.SourceTreeBuilds);
        Assert.Equal(2, counters.SearchedPaths);
        Assert.All(pricing.Functions, x => Assert.Equal(0, x.Calls));

        var shuffled = Discover(f, f.Assets.Reverse(),
            new[] { Intent(1UL, 2UL), Intent(2UL, 3UL) },
            new EconomicFineTransportPathReuseDiagnostics(), new Pricing());
        Assert.Equal(first[0].AssetPath, shuffled[0].AssetPath);
        Assert.Equal(first[1].AssetPath, shuffled[1].AssetPath);
    }

    [Fact]
    public void ClosingDirectBuiltEdgeSelectsTwoLegAlternativeWithoutFreeTravel()
    {
        var f = Make();
        var counters = new EconomicFineTransportPathReuseDiagnostics();
        var closed = new StrategicEdgeAccessSnapshot(new[] { f.Direct.Id });
        var offers = Discover(f, f.Assets,
            new[] { Intent(1UL, 2UL), Intent(2UL, 3UL) },
            counters, new Pricing(), access: closed);
        Assert.Equal(new[] { 20UL, 21UL }, offers[0].AssetPath);
        Assert.Equal(new[] { 20UL }, offers[1].AssetPath);
        Assert.Equal(1, counters.SourceTreeBuilds);

        var quotePricing = new Pricing();
        var original = Discover(f, f.Assets,
            new[] { Intent(1UL, 2UL) },
            new EconomicFineTransportPathReuseDiagnostics(), quotePricing);
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(new EconomicPointId(1UL),
                new CommodityId(7U), EconomicActivityKind.Agriculture,
                EconomicTransferPolicy.FixedPointDenominator) },
            new[] { new EconomicDemandFlow(new EconomicPointId(2UL),
                new CommodityId(7U),
                EconomicTransferPolicy.FixedPointDenominator) });
        var quoter = new EconomicTransportServiceQuoteResolver();
        var firstQuote = Assert.Single(quoter.Resolve(
            1UL, f.Geography, f.Anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, f.Assets, original));
        var nextQuote = Assert.Single(quoter.Resolve(
            2UL, f.Geography, f.Anchors, flows, closed, f.Assets,
            new[] { offers[0] }));
        Assert.Equal(new[] { 90UL }, firstQuote.AssetIds);
        Assert.Equal(new[] { 20UL, 21UL }, nextQuote.AssetIds);
        Assert.Equal(1, Assert.Single(quotePricing.Functions).Calls);
        Assert.Equal(1UL, firstQuote.Quote.TurnOrdinal);
        Assert.Equal(2UL, nextQuote.Quote.TurnOrdinal);

        // An unavailable or unconstructed physical edge is not implicitly
        // traversable merely because the fine topology is adjacent.
        var onlyLeft = Discover(f, f.Assets.Where(x => x.AssetId == 20UL),
            new[] { Intent(1UL, 2UL) },
            new EconomicFineTransportPathReuseDiagnostics(), new Pricing());
        Assert.Empty(onlyLeft);
    }

    [Fact]
    public void LowerAssetIdParallelAlternativeChangesCanonicalRouteAndCache()
    {
        var f = Make();
        var cache = new EconomicFineTransportPathCache();
        var first = Discover(f, f.Assets,
            new[] { Intent(1UL, 2UL), Intent(2UL, 3UL) },
            new EconomicFineTransportPathReuseDiagnostics(), new Pricing(), cache);
        Assert.Equal(new[] { 90UL }, first[0].AssetPath);

        var lower = new EconomicTransportBuiltAsset(
            1UL, 10UL, new PhysicalTacticalEdgeId(Fine, f.Direct.Id), 1UL);
        var assets = f.Assets.Append(lower).ToArray();
        var counters = new EconomicFineTransportPathReuseDiagnostics();
        var current = Discover(f, assets,
            new[] { Intent(1UL, 2UL), Intent(2UL, 3UL) },
            counters, new Pricing(), cache);
        Assert.Equal(new[] { 1UL }, current[0].AssetPath);
        Assert.Equal(new[] { 20UL }, current[1].AssetPath);
        Assert.Equal(0, counters.ReusedPaths);
        Assert.Equal(1, counters.SourceTreeBuilds);
        Assert.Equal(2, counters.SearchedPaths);
        Assert.Equal(new[] { 90UL }, first[0].AssetPath);
    }

    [Fact]
    public void IdenticalSourceWithDifferentTechnologyBuildsSeparateTrees()
    {
        var f = Make();
        var extra = new EconomicTransportBuiltAsset(
            500UL, 20UL, new PhysicalTacticalEdgeId(Fine, f.Direct.Id), 1UL);
        var counters = new EconomicFineTransportPathReuseDiagnostics();
        var offers = Discover(f, f.Assets.Append(extra),
            new[] { Intent(10UL, 2UL), Intent(20UL, 2UL, 20UL),
                    Intent(30UL, 3UL) },
            counters, new Pricing());
        Assert.Equal(3, offers.Count);
        Assert.Equal(2, counters.SourceTreeBuilds);
        Assert.Equal(new[] { 500UL }, offers[1].AssetPath);
    }
}
