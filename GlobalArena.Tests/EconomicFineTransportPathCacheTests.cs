using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFineTransportPathCacheTests
{
    private static readonly GoldbergParameters Fine = new(4, 0);
    private const ulong QuantityUnit = EconomicTransferPolicy.FixedPointDenominator;

    private sealed class FreightFunction(ulong legCount) : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var d = expectedLoadMicrograms + 1_000_000_000;
            return new FoodTransportCostFraction(
                ((BigInteger)20 + legCount * 8) * d +
                120_000_000_000, d);
        }
    }

    private sealed class Prices : IEconomicFineTransportItineraryPricing
    {
        public List<FreightFunction> Functions { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> assets)
        {
            Assert.All(assets, x =>
            {
                Assert.Equal(intent.TechnologyTypeId, x.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion,
                    x.TechnologyCatalogVersion);
            });
            var function = new FreightFunction((ulong)assets.Count);
            Functions.Add(function);
            return function;
        }
    }

    private sealed record Fixture(
        StrategicSurfaceGraph Graph,
        Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors,
        EconomicTransportBuiltAsset[] Assets,
        StrategicEdge First,
        StrategicEdge Second);

    private static Fixture CreateFixture()
    {
        var graph = new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(Fine));
        var first = graph.StrategicTopology.Edges[0];
        var start = first.IncidentCellIds[0];
        var middle = first.IncidentCellIds[1];
        var second = graph.StrategicTopology.Edges.First(e =>
            e.Id != first.Id && e.IncidentCellIds.Contains(middle) &&
            !e.IncidentCellIds.Contains(start));
        var end = second.IncidentCellIds.First(x => x != middle);
        return new Fixture(graph, new()
        {
            [new EconomicPointId(1UL)] = new PhysicalTacticalTileId(Fine, start),
            [new EconomicPointId(2UL)] = new PhysicalTacticalTileId(Fine, end)
        }, new[]
        {
            Asset(11UL, first),
            Asset(12UL, second)
        }, first, second);
    }

    private static EconomicTransportBuiltAsset Asset(
        ulong id, StrategicEdge edge, ulong technology = 10UL,
        ulong version = 3UL, bool operational = true) =>
        new(id, technology, new PhysicalTacticalEdgeId(Fine, edge.Id),
            version, operational);

    private static EconomicFineTransportServiceIntent Intent(
        ulong forecast = 500_000_000UL, ulong technology = 10UL) =>
        new(101UL,
            new StrategicEconomicRouteKey(
                new EconomicPointId(1UL), new EconomicPointId(2UL)),
            technology, 3UL, new BigInteger(forecast));

    private static EconomicProductionDemandResult Flows() => new(
        new[] { new EconomicSupplyFlow(
            new EconomicPointId(1UL), new CommodityId(7U),
            EconomicActivityKind.Agriculture, 3UL * QuantityUnit) },
        new[] { new EconomicDemandFlow(
            new EconomicPointId(2UL), new CommodityId(7U),
            3UL * QuantityUnit) });

    private static (
        EconomicTransportServiceOffer[] Offers, Prices Pricing,
        EconomicFineTransportPathReuseDiagnostics Counters) Discover(
        Fixture fixture, EconomicFineTransportPathCache? cache,
        EconomicTransportBuiltAsset[]? assets = null,
        ulong forecast = 500_000_000UL,
        StrategicEdgeAccessSnapshot? access = null,
        Dictionary<EconomicPointId, PhysicalTacticalTileId>? anchors = null,
        ulong technology = 10UL)
    {
        var pricing = new Prices();
        var counters = new EconomicFineTransportPathReuseDiagnostics();
        var offers = new EconomicFineTransportServiceItineraryDiscovery().Discover(
            fixture.Graph, anchors ?? fixture.Anchors,
            access ?? StrategicEdgeAccessSnapshot.AllOpen,
            assets ?? fixture.Assets,
            new[] { Intent(forecast, technology) }, pricing, cache, counters);
        return (offers.ToArray(), pricing, counters);
    }

    private static string QuoteSignature(
        Fixture fixture, EconomicTransportBuiltAsset[] assets,
        EconomicTransportServiceOffer[] offers, ulong turn,
        StrategicEdgeAccessSnapshot? access = null,
        Dictionary<EconomicPointId, PhysicalTacticalTileId>? anchors = null)
    {
        var selections = new EconomicTransportServiceQuoteResolver().Resolve(
            turn, fixture.Graph, anchors ?? fixture.Anchors, Flows(),
            access ?? StrategicEdgeAccessSnapshot.AllOpen, assets, offers);
        return string.Join(";", selections.Select(x =>
            x.ServiceId + ":" + string.Join(",", x.AssetIds) + ":" +
            x.Quote.RawUnitCostPerGram.Numerator + "/" +
            x.Quote.RawUnitCostPerGram.Denominator + ":" +
            x.Quote.TurnOrdinal));
    }

    [Fact]
    public void SamePhysicalGraphReusesOnlyPathWhileNewCycleRepricesFresh()
    {
        var fixture = CreateFixture();
        var cache = new EconomicFineTransportPathCache();
        var first = Discover(fixture, cache);
        var firstQuote = QuoteSignature(
            fixture, fixture.Assets, first.Offers, 1UL);
        Assert.Equal(new[] { 11UL, 12UL }, Assert.Single(first.Offers).AssetPath);
        Assert.Equal(1, first.Counters.SearchedPaths);
        Assert.Equal(0, first.Counters.ReusedPaths);
        Assert.Equal(1, cache.CachedPathCount);
        Assert.Equal(1, Assert.Single(first.Pricing.Functions).Evaluations);

        var second = Discover(fixture, cache, forecast: 8_000_000_000UL);
        var secondQuote = QuoteSignature(
            fixture, fixture.Assets, second.Offers, 2UL);
        Assert.Equal(first.Offers[0].AssetPath, second.Offers[0].AssetPath);
        Assert.Equal(1, second.Counters.ReusedPaths);
        Assert.Equal(0, second.Counters.SearchedPaths);
        Assert.NotEqual(firstQuote, secondQuote);
        Assert.Equal(1, Assert.Single(second.Pricing.Functions).Evaluations);
        Assert.Equal(1, Assert.Single(first.Pricing.Functions).Evaluations);

        var fresh = Discover(fixture, null, forecast: 8_000_000_000UL);
        Assert.Equal(secondQuote, QuoteSignature(
            fixture, fixture.Assets, fresh.Offers, 2UL));
        Assert.Equal(0, first.Counters.ReusedPaths); // immutable prior ledger
    }

    [Fact]
    public void CorridorClosureAndReplacementInvalidateOnlyPhysicalDependents()
    {
        var fixture = CreateFixture();
        var cache = new EconomicFineTransportPathCache();
        var first = Discover(fixture, cache);
        var closed = new StrategicEdgeAccessSnapshot(
            new[] { fixture.First.Id });

        var blocked = Discover(fixture, cache, access: closed);
        Assert.Empty(blocked.Offers);
        Assert.Equal(0, blocked.Counters.ReusedPaths);
        Assert.Equal(1, blocked.Counters.UnreachableIntents);
        Assert.Equal(0, cache.CachedPathCount);
        Assert.Empty(blocked.Pricing.Functions);

        var replacement = fixture.Assets.Where(x => x.AssetId != 11UL)
            .Append(Asset(90UL, fixture.First)).ToArray();
        var rebuilt = Discover(fixture, cache, assets: replacement);
        Assert.Equal(new[] { 90UL, 12UL }, Assert.Single(rebuilt.Offers).AssetPath);
        Assert.Equal(0, rebuilt.Counters.ReusedPaths);
        Assert.Equal(1, rebuilt.Counters.SearchedPaths);
        Assert.Equal(1, Assert.Single(rebuilt.Pricing.Functions).Evaluations);
        Assert.Equal(new[] { 11UL, 12UL }, first.Offers[0].AssetPath);

        var fresh = Discover(fixture, null, assets: replacement);
        Assert.Equal(
            QuoteSignature(fixture, replacement, fresh.Offers, 3UL),
            QuoteSignature(fixture, replacement, rebuilt.Offers, 3UL));
    }

    [Fact]
    public void DifferentModeChangeRetainsPathButMatchingModeChangeReroutes()
    {
        var fixture = CreateFixture();
        var cache = new EconomicFineTransportPathCache();
        var first = Discover(fixture, cache);
        var unrelated = fixture.Assets.Append(
            Asset(50UL, fixture.First, technology: 20UL)).ToArray();
        var unrelatedPass = Discover(fixture, cache, assets: unrelated);
        Assert.Equal(1, unrelatedPass.Counters.ReusedPaths);
        Assert.Equal(first.Offers[0].AssetPath, unrelatedPass.Offers[0].AssetPath);

        // A lower-ID parallel built asset changes canonical choice for
        // this technology, even while the old path remains fully usable.
        var competing = unrelated.Append(Asset(1UL, fixture.First)).ToArray();
        var updated = Discover(fixture, cache, assets: competing);
        Assert.Equal(new[] { 1UL, 12UL }, Assert.Single(updated.Offers).AssetPath);
        Assert.Equal(0, updated.Counters.ReusedPaths);
        Assert.Equal(1, updated.Counters.SearchedPaths);
        Assert.Equal(
            QuoteSignature(fixture, competing,
                Discover(fixture, null, assets: competing).Offers, 2UL),
            QuoteSignature(fixture, competing, updated.Offers, 2UL));
    }

    [Fact]
    public void ChangedAnchorOrTechnologyMustNotReinterpretCachedPath()
    {
        var fixture = CreateFixture();
        var cache = new EconomicFineTransportPathCache();
        Assert.Single(Discover(fixture, cache).Offers);
        var anchor = new Dictionary<EconomicPointId, PhysicalTacticalTileId>(
            fixture.Anchors)
        {
            [new EconomicPointId(2UL)] =
                new PhysicalTacticalTileId(Fine,
                    fixture.First.IncidentCellIds[1])
        };
        var rerouted = Discover(fixture, cache, anchors: anchor);
        Assert.Equal(new[] { 11UL }, Assert.Single(rerouted.Offers).AssetPath);
        Assert.Equal(0, rerouted.Counters.ReusedPaths);
        Assert.Equal(1, rerouted.Counters.SearchedPaths);

        var otherTechnology = Discover(fixture, cache, technology: 20UL);
        Assert.Empty(otherTechnology.Offers);
        Assert.Equal(0, otherTechnology.Counters.ReusedPaths);
        Assert.Equal(1, otherTechnology.Counters.UnreachableIntents);
    }

    [Fact]
    public void InvalidSnapshotNeverBypassesValidationUsingCacheHits()
    {
        var fixture = CreateFixture();
        var cache = new EconomicFineTransportPathCache();
        Assert.Single(Discover(fixture, cache).Offers);
        var oldCount = cache.CachedPathCount;

        Assert.Throws<ArgumentException>(() => Discover(fixture, cache,
            assets: new[] { fixture.Assets[0], fixture.Assets[0] }));
        Assert.Throws<ArgumentException>(() => Discover(fixture, cache,
            assets: new[] {
                new EconomicTransportBuiltAsset(
                    11UL, 10UL, fixture.First.Id, 3UL),
                fixture.Assets[1]
            }));
        Assert.Equal(oldCount, cache.CachedPathCount);

        var result = Discover(fixture, cache);
        Assert.Equal(1, result.Counters.ReusedPaths);
        Assert.Single(result.Offers);
    }
}
