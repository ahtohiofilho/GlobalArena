using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicLogicalTransportProviderRegistryTests
{
    private static EconomicTransportLogicalNodeId Node(ulong id) => new(id);
    private static EconomicTransportLogicalLink Link(
        ulong id, ulong a, ulong b, ulong technology = 71UL,
        ulong version = 4UL, bool operational = true) =>
        new(id, Node(a), Node(b), technology, version, operational);
    private static EconomicLogicalTransportServiceIntent Intent(
        ulong service = 100UL, ulong technology = 71UL, ulong version = 4UL,
        ulong expected = 500_000_000UL,
        ulong destinationPoint = 2UL, ulong destinationNode = 3UL) =>
        new(service, new StrategicEconomicRouteKey(
                new EconomicPointId(1UL), new EconomicPointId(destinationPoint)),
            Node(1UL), Node(destinationNode), technology, version,
            new BigInteger(expected));
    private static EconomicLogicalTransportTechnologyDefinition Definition(
        ulong technology = 71UL, ulong version = 4UL,
        ulong provider = 9UL, ulong providerVersion = 2UL) =>
        new(technology, version, provider, providerVersion);
    private static EconomicLogicalTransportAuthorizationSnapshot Snapshot(
        IEnumerable<EconomicTransportLogicalLink> links,
        ulong ordinal = 1UL, ulong providerId = 9UL,
        ulong providerVersion = 2UL,
        bool includeDestination = true)
    {
        var anchors = new List<EconomicLogicalTransportPointAnchor>
        {
            new(new EconomicPointId(1UL), Node(1UL))
        };
        if (includeDestination)
            anchors.Add(new(new EconomicPointId(2UL), Node(3UL)));
        return new EconomicLogicalTransportAuthorizationSnapshot(
            ordinal, anchors,
            links.Select(link => new EconomicLogicalTransportAuthorizedLink(
                providerId, providerVersion, link)));
    }

    private sealed class PriceFunction(ulong baseRawCost) :
        IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 17UL;
        public int Evaluations { get; private set; }
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var d = expectedLoadMicrograms + 500_000_000;
            return new FoodTransportCostFraction(
                (BigInteger)baseRawCost * d + 200_000_000_000, d);
        }
    }

    private sealed class Prices : IEconomicLogicalTransportItineraryPricing
    {
        public List<PriceFunction> Functions { get; } = new();
        public List<ulong[]> PricedLinks { get; } = new();
        public ulong CostForTechnology71 { get; set; } = 300UL;

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicLogicalTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportLogicalLink> orderedLinks)
        {
            PricedLinks.Add(orderedLinks.Select(x => x.LinkId).ToArray());
            var cost = intent.TechnologyTypeId == 71UL
                ? CostForTechnology71 : 100UL;
            var function = new PriceFunction(cost);
            Functions.Add(function);
            return function;
        }
    }

    private sealed class FixedProvider(
        ulong providerId, ulong version,
        IReadOnlyList<EconomicTransportLogicalLink>? links)
        : IEconomicLogicalTransportTraversalProvider
    {
        public ulong ProviderId { get; set; } = providerId;
        public ulong ProviderVersion { get; set; } = version;
        public IReadOnlyList<EconomicTransportLogicalLink>? FindItinerary(
            EconomicLogicalTransportServiceIntent intent) => links;
    }

    [Fact]
    public void RegisteredLinesJoinAtNodeAndOnlyAuthorizedCommercialEndpointsAreQuoted()
    {
        var links = new[]
        {
            Link(20UL, 2UL, 3UL), Link(11UL, 1UL, 2UL),
            Link(30UL, 2UL, 4UL), Link(40UL, 2UL, 5UL)
        };
        var registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() },
            new IEconomicLogicalTransportTraversalProvider[]
            {
                new EconomicConstructedLogicalLineProvider(9UL, 2UL, links)
            });
        var snapshot = Snapshot(links);
        var prices = new Prices();
        var selection = Assert.Single(
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                1UL, registry, snapshot, new[] { Intent() }, prices));
        Assert.Equal(1UL, selection.AuthorizationSnapshotOrdinal);
        Assert.Equal(new[] { Node(1UL), Node(2UL), Node(3UL) },
            selection.Selection.NodePath);
        Assert.Equal(new[] { 11UL, 20UL }, selection.Selection.LinkIds);
        Assert.Equal(9UL, selection.Selection.ProviderId);
        Assert.Equal(2UL, selection.Selection.ProviderVersion);
        Assert.Equal(71UL, selection.Selection.TechnologyTypeId);
        Assert.Equal(4UL, selection.Selection.TechnologyCatalogVersion);
        Assert.Equal(1UL, selection.Quote.TurnOrdinal);
        Assert.Equal(new[] { 11UL, 20UL }, Assert.Single(prices.PricedLinks));
        Assert.Equal(1, Assert.Single(prices.Functions).Evaluations);
        Assert.Equal(new EconomicPointId(2UL),
            selection.Quote.RouteKey.DestinationEconomicPointId);
        // The transit node 2 has multiple line approaches but does not
        // acquire an implicit cargo service, transfer facility or quote.
    }

    [Fact]
    public void SnapshotCannotBeBypassedBySpoofedPhysicalLinkOrMissingEndpoint()
    {
        var installed = new[] { Link(11UL, 1UL, 2UL), Link(20UL, 2UL, 3UL) };
        var forged = new[] { Link(11UL, 1UL, 9UL), Link(20UL, 9UL, 3UL) };
        var provider = new FixedProvider(9UL, 2UL, forged);
        var registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() }, new[] { provider });
        var prices = new Prices();
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                1UL, registry, Snapshot(installed), new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);

        provider = new FixedProvider(9UL, 2UL, installed);
        registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() }, new[] { provider });
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                1UL, registry,
                Snapshot(installed, includeDestination: false),
                new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);

        // A provider cannot simply choose a new economic destination
        // without the snapshot declaring its logical node binding.
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                1UL, registry, Snapshot(installed),
                new[] { Intent(destinationPoint: 3UL) }, prices));
        Assert.Empty(prices.Functions);
    }

    [Fact]
    public void InoperativeAndUnlistedPhysicalSegmentsFailBeforePricing()
    {
        var first = Link(11UL, 1UL, 2UL);
        var second = Link(20UL, 2UL, 3UL);
        var provider = new FixedProvider(9UL, 2UL, new[] { first, second });
        var registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() }, new[] { provider });
        var resolver = new EconomicRegisteredLogicalTransportQuoteResolver();
        var prices = new Prices();

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            1UL, registry, Snapshot(new[] { first }),
            new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            1UL, registry, Snapshot(new[] { first,
                Link(20UL, 2UL, 3UL, operational: false) }),
            new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            1UL, registry, Snapshot(new[] { first,
                Link(20UL, 2UL, 3UL, version: 5UL) }),
            new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);
    }

    [Fact]
    public void DifferentTechnologiesCanShareProviderYetNeedOwnVersionedRegistration()
    {
        var links = new[]
        {
            Link(11UL, 1UL, 2UL),
            Link(20UL, 2UL, 3UL),
            Link(31UL, 1UL, 2UL, technology: 800UL, version: 1UL),
            Link(40UL, 2UL, 3UL, technology: 800UL, version: 1UL)
        };
        var registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition(),
                Definition(800UL, 1UL) },
            new IEconomicLogicalTransportTraversalProvider[]
            {
                new EconomicConstructedLogicalLineProvider(9UL, 2UL, links)
            });
        var intentA = Intent(100UL);
        var intentB = Intent(200UL, 800UL, 1UL);
        var prices = new Prices();
        var selected = Assert.Single(
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                3UL, registry, Snapshot(links),
                new[] { intentB, intentA }, prices));
        Assert.Equal(200UL, selected.Selection.ServiceId);
        Assert.Equal(800UL, selected.Selection.TechnologyTypeId);
        Assert.Equal(new[] { 31UL, 40UL }, selected.Selection.LinkIds);
        Assert.Equal(2, prices.Functions.Count);
        Assert.All(prices.Functions, x => Assert.Equal(1, x.Evaluations));

        // Changing the caller-supplied cost can select another registered
        // technology without adding a modal-name switch to the allocator.
        var alternatePrices = new Prices { CostForTechnology71 = 20UL };
        var alternate = Assert.Single(
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                3UL, registry, Snapshot(links),
                new[] { intentA, intentB }, alternatePrices));
        Assert.Equal(100UL, alternate.Selection.ServiceId);
        Assert.Equal(new[] { 11UL, 20UL }, alternate.Selection.LinkIds);
    }

    [Fact]
    public void ReplacingAssetRequiresFreshSnapshotAndPreservesPriorTurnFreight()
    {
        var old = new[] { Link(11UL, 1UL, 2UL), Link(20UL, 2UL, 3UL) };
        var newAssets = new[]
        {
            Link(77UL, 1UL, 2UL), Link(20UL, 2UL, 3UL)
        };
        var provider = new FixedProvider(9UL, 2UL, old);
        var registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() }, new[] { provider });
        var quoter = new EconomicRegisteredLogicalTransportQuoteResolver();
        var prices = new Prices();
        var first = Assert.Single(quoter.Resolve(
            1UL, registry, Snapshot(old, ordinal: 100UL),
            new[] { Intent() }, prices));
        Assert.Equal(new[] { 11UL, 20UL }, first.Selection.LinkIds);

        provider = new FixedProvider(9UL, 2UL, newAssets);
        registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() }, new[] { provider });
        Assert.Throws<InvalidOperationException>(() => quoter.Resolve(
            2UL, registry, Snapshot(old, ordinal: 100UL),
            new[] { Intent(expected: 9_000_000_000UL) }, prices));
        Assert.Equal(1, prices.Functions.Count);

        var second = Assert.Single(quoter.Resolve(
            2UL, registry, Snapshot(newAssets, ordinal: 101UL),
            new[] { Intent(expected: 9_000_000_000UL) }, prices));
        Assert.Equal(new[] { 77UL, 20UL }, second.Selection.LinkIds);
        Assert.Equal(101UL, second.AuthorizationSnapshotOrdinal);
        Assert.Equal(100UL, first.AuthorizationSnapshotOrdinal);
        Assert.Equal(1UL, first.Quote.TurnOrdinal);
        Assert.Equal(2UL, second.Quote.TurnOrdinal);
        Assert.NotSame(first.Quote, second.Quote);
        Assert.True(first.Quote.RawUnitCostPerGram.CompareTo(
            second.Quote.RawUnitCostPerGram) > 0);
        Assert.All(prices.Functions, x => Assert.Equal(1, x.Evaluations));
    }

    [Fact]
    public void DuplicateOrUnknownRegistrationsAndChangedProvidersFailClosed()
    {
        var valid = new FixedProvider(9UL, 2UL,
            new[] { Link(11UL, 1UL, 2UL), Link(20UL, 2UL, 3UL) });
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalTransportProviderRegistry(
                new[] { Definition(), Definition() }, new[] { valid }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalTransportProviderRegistry(
                new[] { Definition() }, Array.Empty<IEconomicLogicalTransportTraversalProvider>()));
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalTransportProviderRegistry(
                new[] { Definition() },
                new IEconomicLogicalTransportTraversalProvider[] { valid, valid }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalTransportAuthorizationSnapshot(
                1UL,
                new[] { new EconomicLogicalTransportPointAnchor(
                        new EconomicPointId(1UL), Node(1UL)),
                    new EconomicLogicalTransportPointAnchor(
                        new EconomicPointId(1UL), Node(1UL)) },
                Array.Empty<EconomicLogicalTransportAuthorizedLink>()));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(new[] { Link(11UL, 1UL, 2UL), Link(11UL, 1UL, 2UL) }));

        var registry = new EconomicLogicalTransportProviderRegistry(
            new[] { Definition() }, new[] { valid });
        var prices = new Prices();
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                1UL, registry, Snapshot(new[] { Link(11UL, 1UL, 2UL) }),
                new[] { Intent(100UL, technology: 99UL) }, prices));
        valid.ProviderVersion = 3UL;
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicRegisteredLogicalTransportQuoteResolver().Resolve(
                1UL, registry, Snapshot(new[] { Link(11UL, 1UL, 2UL) }),
                new[] { Intent() }, prices));
        Assert.Empty(prices.Functions);
    }

    [Fact]
    public void RegistryInputOrderCannotAlterAuthorizedLinkOrFreight()
    {
        var links = new[] { Link(11UL, 1UL, 2UL), Link(20UL, 2UL, 3UL) };
        var reference = new EconomicRegisteredLogicalTransportQuoteResolver();
        var expected = Assert.Single(reference.Resolve(1UL,
            new EconomicLogicalTransportProviderRegistry(
                new[] { Definition() },
                new IEconomicLogicalTransportTraversalProvider[]
                {
                    new EconomicConstructedLogicalLineProvider(9UL, 2UL, links)
                }),
            Snapshot(links), new[] { Intent() }, new Prices()));
        var reversed = Assert.Single(reference.Resolve(1UL,
            new EconomicLogicalTransportProviderRegistry(
                new[] { Definition() },
                new IEconomicLogicalTransportTraversalProvider[]
                {
                    new EconomicConstructedLogicalLineProvider(
                        9UL, 2UL, links.Reverse())
                }),
            Snapshot(links.Reverse()), new[] { Intent() }, new Prices()));
        Assert.Equal(expected.Selection.LinkIds, reversed.Selection.LinkIds);
        Assert.Equal(expected.Selection.NodePath, reversed.Selection.NodePath);
        Assert.Equal(0, expected.Quote.RawUnitCostPerGram.CompareTo(
            reversed.Quote.RawUnitCostPerGram));
        Assert.Equal(expected.AuthorizationSnapshotOrdinal,
            reversed.AuthorizationSnapshotOrdinal);
    }
}
