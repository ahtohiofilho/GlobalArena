using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicLogicalTransportConnectivityTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    private static EconomicTransportLogicalNodeId Node(ulong id) => new(id);
    private static EconomicTransportLogicalLink Link(
        ulong id, ulong a, ulong b, ulong tech = 10UL,
        ulong version = 1UL, bool active = true) =>
        new(id, Node(a), Node(b), tech, version, active);
    private static StrategicEconomicRouteKey CommercialRoute(
        ulong source = 1UL, ulong destination = 2UL) =>
        new(new EconomicPointId(source), new EconomicPointId(destination));
    private static EconomicLogicalTransportServiceIntent Intent(
        ulong id = 100UL, ulong destinationNode = 3UL,
        ulong destinationPoint = 2UL, ulong technology = 10UL,
        ulong version = 1UL, ulong forecast = 500_000_000UL) =>
        new(id, CommercialRoute(1UL, destinationPoint),
            Node(1UL), Node(destinationNode), technology, version,
            new BigInteger(forecast));

    private sealed class Function(ulong baseCost) :
        IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 7UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var d = expectedLoadMicrograms + 100_000_000;
            return new FoodTransportCostFraction(
                (BigInteger)baseCost * d + 60_000_000_000, d);
        }
    }

    private sealed class Pricing : IEconomicLogicalTransportItineraryPricing
    {
        public List<Function> Functions { get; } = new();
        public List<ulong[]> PricedPaths { get; } = new();
        public ulong BaseCost { get; set; } = 100UL;

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicLogicalTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportLogicalLink> orderedLinks)
        {
            Assert.NotEmpty(orderedLinks);
            Assert.All(orderedLinks, link =>
            {
                Assert.Equal(intent.TechnologyTypeId, link.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion,
                    link.TechnologyCatalogVersion);
            });
            PricedPaths.Add(orderedLinks.Select(x => x.LinkId).ToArray());
            var f = new Function(BaseCost + (ulong)orderedLinks.Count * 10UL);
            Functions.Add(f);
            return f;
        }
    }

    private sealed class MarketValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(800_000 * (BigInteger)Unit - 10_000 * (BigInteger)q, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(1_600_000 * (BigInteger)q * Unit -
                10_000 * (BigInteger)q * q, 2 * (BigInteger)Unit * Unit);
    }

    private sealed class StubProvider : IEconomicLogicalTransportTraversalProvider
    {
        private readonly IReadOnlyList<EconomicTransportLogicalLink>? _path;
        public ulong ProviderId => 92UL;
        public ulong ProviderVersion => 3UL;

        public StubProvider(params EconomicTransportLogicalLink[] path) =>
            _path = path;

        public IReadOnlyList<EconomicTransportLogicalLink>? FindItinerary(
            EconomicLogicalTransportServiceIntent intent) => _path;
    }

    [Fact]
    public void SeparateLinesSharingNodeFormOneContinuousCommercialItinerary()
    {
        var edges = new[]
        {
            Link(21UL, 2UL, 3UL),
            Link(11UL, 1UL, 2UL),
            Link(31UL, 2UL, 4UL),
            Link(41UL, 2UL, 5UL)
        };
        var provider = new EconomicConstructedLogicalLineProvider(
            1UL, 1UL, edges);
        var prices = new Pricing();
        var selected = Assert.Single(new EconomicLogicalTransportQuoteResolver()
            .Resolve(1UL, provider, new[] { Intent() }, prices));

        Assert.Equal(new[] { Node(1UL), Node(2UL), Node(3UL) },
            selected.NodePath);
        Assert.Equal(new[] { 11UL, 21UL }, selected.LinkIds);
        Assert.Equal(100UL, selected.ServiceId);
        Assert.Equal(1UL, selected.ProviderId);
        Assert.Equal(1UL, selected.ProviderVersion);
        Assert.Equal(10UL, selected.TechnologyTypeId);
        Assert.Equal(1UL, selected.TechnologyCatalogVersion);
        Assert.Equal(new[] { 11UL, 21UL }, Assert.Single(prices.PricedPaths));
        Assert.Equal(1, Assert.Single(prices.Functions).Evaluations);
        Assert.Equal(1UL, selected.Quote.TurnOrdinal);

        // Three distinct incoming/outgoing lines meet at node 2;
        // neither line end nor the shared node creates cargo operations.
        var reversed = new EconomicConstructedLogicalLineProvider(
            1UL, 1UL, edges.Reverse());
        var again = Assert.Single(new EconomicLogicalTransportQuoteResolver()
            .Resolve(1UL, reversed, new[] { Intent() }, new Pricing()));
        Assert.Equal(selected.NodePath, again.NodePath);
        Assert.Equal(selected.LinkIds, again.LinkIds);
        Assert.Equal(0, selected.Quote.RawUnitCostPerGram.CompareTo(
            again.Quote.RawUnitCostPerGram));
    }

    [Fact]
    public void IntermediateNodeWithDemandHasNoUnrequestedServiceOrTrade()
    {
        var provider = new EconomicConstructedLogicalLineProvider(1UL, 1UL,
            new[] { Link(11UL, 1UL, 2UL), Link(21UL, 2UL, 3UL) });
        var price = new Pricing();
        var selected = Assert.Single(new EconomicLogicalTransportQuoteResolver()
            .Resolve(1UL, provider, new[] { Intent() }, price));
        Assert.Equal(new EconomicPointId(2UL),
            selected.Quote.RouteKey.DestinationEconomicPointId);
        Assert.Equal(3, selected.NodePath.Count);

        var source = new EconomicPointId(1UL);
        var destination = new EconomicPointId(2UL);
        var intermediate = new EconomicPointId(3UL);
        var commodity = new CommodityId(7U);
        var flows = new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(source, commodity,
                    EconomicActivityKind.Agriculture, 3UL * Unit)
            },
            new[]
            {
                new EconomicDemandFlow(destination, commodity, 3UL * Unit),
                new EconomicDemandFlow(intermediate, commodity, 3UL * Unit)
            });
        var foods = new FoodCommodityCatalog(new[]
        {
            new FoodCommodityDefinition(commodity, "logical-commodity", 1_000UL)
        });
        var values = new[]
        {
            new EconomicTurnCommodityValueOffer(
                destination, commodity, new MarketValue()),
            new EconomicTurnCommodityValueOffer(
                intermediate, commodity, new MarketValue())
        };
        var reservations = new[]
        {
            new EconomicTurnSourceReservation(
                source, commodity, EconomicActivityKind.Agriculture, 20_000UL)
        };
        var market = new EconomicTurnLockedFreightMarketResolver();
        var exact = market.Resolve(
            1UL, flows, foods, new[] { selected.Quote }, values, reservations,
            new EconomicCongestionMarketSearchPolicy(Unit, 12));
        var indexed = market.Resolve(
            1UL, flows, foods, new[] { selected.Quote }, values, reservations,
            new EconomicCongestionMarketSearchPolicy(Unit, 12),
            selectionMode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex);
        Assert.NotEmpty(exact);
        Assert.Equal(exact.Count, indexed.Count);
        Assert.All(exact, trade =>
        {
            Assert.Equal(destination, trade.DestinationEconomicPointId);
            Assert.Same(selected.Quote, trade.FreightQuote);
        });
        Assert.Equal(1, Assert.Single(price.Functions).Evaluations);
        Assert.Equal(3, selected.NodePath.Count);
    }

    [Fact]
    public void TechnologyBoundariesAndDisconnectedSegmentsNeverImplyFreeTransfer()
    {
        var links = new[]
        {
            Link(11UL, 1UL, 2UL),
            Link(22UL, 2UL, 3UL, tech: 20UL),
            Link(33UL, 5UL, 6UL),
            Link(44UL, 2UL, 3UL, active: false)
        };
        var provider = new EconomicConstructedLogicalLineProvider(1UL, 1UL, links);
        var price = new Pricing();
        Assert.Empty(new EconomicLogicalTransportQuoteResolver().Resolve(
            1UL, provider, new[] { Intent() }, price));
        Assert.Empty(price.Functions);

        var fixedProvider = new EconomicConstructedLogicalLineProvider(
            1UL, 1UL, links.Append(Link(45UL, 2UL, 3UL)));
        var selected = Assert.Single(new EconomicLogicalTransportQuoteResolver()
            .Resolve(2UL, fixedProvider, new[] { Intent() }, price));
        Assert.Equal(new[] { 11UL, 45UL }, selected.LinkIds);
        Assert.Equal(1, Assert.Single(price.Functions).Evaluations);
        Assert.Equal(2UL, selected.Quote.TurnOrdinal);
    }

    [Fact]
    public void ParallelAlternativesHaveDeterministicCanonicalLinkIdentity()
    {
        var links = new[]
        {
            Link(90UL, 1UL, 2UL), Link(2UL, 1UL, 2UL),
            Link(31UL, 2UL, 3UL)
        };
        var pricing = new Pricing();
        var resolved = Assert.Single(new EconomicLogicalTransportQuoteResolver()
            .Resolve(1UL,
                new EconomicConstructedLogicalLineProvider(1UL, 1UL, links),
                new[] { Intent() }, pricing));
        Assert.Equal(new[] { 2UL, 31UL }, resolved.LinkIds);

        var disconnected = new EconomicConstructedLogicalLineProvider(
            1UL, 1UL, links.Where(x => x.LinkId != 31UL));
        Assert.Empty(new EconomicLogicalTransportQuoteResolver().Resolve(
            2UL, disconnected, new[] { Intent() }, new Pricing()));
    }

    [Fact]
    public void ProviderCanSupplyNonGoldbergTraversalWithoutModalEnum()
    {
        // A distinct versioned provider need not consume Goldberg borders,
        // constructed-road assets or a predefined Road/Rail/Ship enum.
        var provider = new StubProvider(
            Link(700UL, 1UL, 8UL, 427UL, 3UL),
            Link(701UL, 8UL, 3UL, 427UL, 3UL));
        var pricing = new Pricing();
        var selection = Assert.Single(new EconomicLogicalTransportQuoteResolver()
            .Resolve(7UL, provider,
                new[] { Intent(100UL, technology: 427UL, version: 3UL) },
                pricing));
        Assert.Equal(92UL, selection.ProviderId);
        Assert.Equal(3UL, selection.ProviderVersion);
        Assert.Equal(427UL, selection.TechnologyTypeId);
        Assert.Equal(new[] { 700UL, 701UL }, selection.LinkIds);
        Assert.Equal(new[] { Node(1UL), Node(8UL), Node(3UL) },
            selection.NodePath);
        Assert.Equal(1, Assert.Single(pricing.Functions).Evaluations);
    }

    [Fact]
    public void DiscontinuousOrRepeatedProviderRouteFailsBeforePricing()
    {
        var prices = new Pricing();
        var invalid = new StubProvider(
            Link(11UL, 1UL, 2UL), Link(12UL, 5UL, 3UL));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicLogicalTransportQuoteResolver().Resolve(
                1UL, invalid, new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);

        var loop = new StubProvider(
            Link(11UL, 1UL, 2UL), Link(12UL, 2UL, 4UL),
            Link(13UL, 4UL, 2UL), Link(21UL, 2UL, 3UL));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicLogicalTransportQuoteResolver().Resolve(
                1UL, loop, new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);

        var wrongTechnology = new StubProvider(
            Link(11UL, 1UL, 2UL, tech: 20UL),
            Link(12UL, 2UL, 3UL, tech: 20UL));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicLogicalTransportQuoteResolver().Resolve(
                1UL, wrongTechnology, new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);
    }

    [Fact]
    public void NewForecastRequotesExistingLinesWithoutChangingPriorLedger()
    {
        var provider = new EconomicConstructedLogicalLineProvider(1UL, 1UL,
            new[] { Link(11UL, 1UL, 2UL), Link(21UL, 2UL, 3UL) });
        var price = new Pricing();
        var resolver = new EconomicLogicalTransportQuoteResolver();
        var first = Assert.Single(resolver.Resolve(1UL, provider,
            new[] { Intent(forecast: 500_000_000UL) }, price));
        var later = Assert.Single(resolver.Resolve(2UL, provider,
            new[] { Intent(forecast: 8_000_000_000UL) }, price));

        Assert.Equal(first.NodePath, later.NodePath);
        Assert.Equal(first.LinkIds, later.LinkIds);
        Assert.True(first.Quote.RawUnitCostPerGram.CompareTo(
            later.Quote.RawUnitCostPerGram) > 0);
        Assert.Equal(1UL, first.Quote.TurnOrdinal);
        Assert.Equal(2UL, later.Quote.TurnOrdinal);
        Assert.All(price.Functions, x => Assert.Equal(1, x.Evaluations));
        Assert.Equal(2, price.Functions.Count);

        var hash = new CanonicalWorldStateHasher().Compute(WorldState.CreateInitial());
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(
            WorldState.CreateInitial()));
    }

    [Fact]
    public void LinkAndProviderIdentityValidationFailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Node(0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Link(0UL, 1UL, 2UL));
        Assert.Throws<ArgumentException>(() =>
            Link(1UL, 1UL, 1UL));
        Assert.Throws<ArgumentException>(() =>
            new EconomicConstructedLogicalLineProvider(1UL, 1UL,
                new[] { Link(7UL, 1UL, 2UL), Link(7UL, 1UL, 3UL) }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicConstructedLogicalLineProvider(0UL, 1UL,
                Array.Empty<EconomicTransportLogicalLink>()));

        var provider = new EconomicConstructedLogicalLineProvider(
            1UL, 1UL, new[] { Link(11UL, 1UL, 2UL) });
        var duplicate = new[] { Intent(9UL, 2UL), Intent(9UL, 2UL) };
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalTransportQuoteResolver().Resolve(
                1UL, provider, duplicate, new Pricing()));
    }
}
