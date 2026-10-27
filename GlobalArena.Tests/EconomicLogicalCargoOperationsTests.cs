using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicLogicalCargoOperationsTests
{
    private static EconomicTransportLogicalNodeId Node(ulong id) => new(id);

    private static EconomicTransportLogicalLink Link(
        ulong id, ulong a, ulong b, ulong technology = 71UL,
        ulong version = 4UL) =>
        new(id, Node(a), Node(b), technology, version);

    private static EconomicLogicalTransportServiceIntent Intent(
        ulong service = 100UL, ulong technology = 71UL,
        ulong version = 4UL, ulong forecast = 500_000_000UL) =>
        new(service, new StrategicEconomicRouteKey(
            new EconomicPointId(1UL), new EconomicPointId(2UL)),
            Node(1UL), Node(3UL), technology, version,
            new BigInteger(forecast));

    private static EconomicLogicalCargoFacility Facility(
        ulong id, ulong point, ulong node, bool operational = true) =>
        new(id, new EconomicPointId(point), Node(node), operational);

    private static EconomicLogicalCargoPermission Permit(
        ulong facility, EconomicLogicalCargoOperation op,
        ulong technology = 71UL, ulong version = 4UL) =>
        new(facility, technology, version, op);

    private static EconomicLogicalTransportAuthorizationSnapshot Physical(
        EconomicTransportLogicalLink[] links, ulong ordinal = 1UL) =>
        new(ordinal, new[]
        {
            new EconomicLogicalTransportPointAnchor(
                new EconomicPointId(1UL), Node(1UL)),
            new EconomicLogicalTransportPointAnchor(
                new EconomicPointId(2UL), Node(3UL))
        }, links.Select(link =>
            new EconomicLogicalTransportAuthorizedLink(9UL, 2UL, link)));

    private static EconomicLogicalTransportProviderRegistry Registry(
        IEnumerable<EconomicTransportLogicalLink> links) =>
        new(new[]
        {
            new EconomicLogicalTransportTechnologyDefinition(
                71UL, 4UL, 9UL, 2UL),
            new EconomicLogicalTransportTechnologyDefinition(
                800UL, 1UL, 9UL, 2UL)
        }, new IEconomicLogicalTransportTraversalProvider[]
        {
            new EconomicConstructedLogicalLineProvider(9UL, 2UL, links)
        });

    private static EconomicLogicalCargoOperationsSnapshot Operations(
        ulong ordinal = 1UL,
        bool includeLoad = true, bool includeUnload = true,
        bool sourceOperational = true, bool destinationOperational = true,
        bool includeIntermediate = false)
    {
        var facilities = new List<EconomicLogicalCargoFacility>
        {
            Facility(101UL, 1UL, 1UL, sourceOperational),
            Facility(202UL, 2UL, 3UL, destinationOperational)
        };
        if (includeIntermediate)
            facilities.Add(Facility(303UL, 3UL, 2UL));

        var grants = new List<EconomicLogicalCargoPermission>();
        if (includeLoad)
            grants.Add(Permit(101UL, EconomicLogicalCargoOperation.Load));
        if (includeUnload)
            grants.Add(Permit(202UL, EconomicLogicalCargoOperation.Unload));
        return new EconomicLogicalCargoOperationsSnapshot(
            ordinal, facilities, grants);
    }

    private sealed class Function(ulong basePrice) :
        IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 3UL;
        public int Evaluations { get; private set; }
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var d = expectedLoadMicrograms + 100_000_000;
            return new FoodTransportCostFraction(
                (BigInteger)basePrice * d + 100_000_000_000, d);
        }
    }

    private sealed class Pricing : IEconomicLogicalTransportItineraryPricing
    {
        public List<Function> Functions { get; } = new();
        public ulong CheapServiceId { get; set; } = 200UL;
        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicLogicalTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportLogicalLink> links)
        {
            Assert.NotEmpty(links);
            var f = new Function(intent.ServiceId == CheapServiceId
                ? 10UL : 50UL);
            Functions.Add(f);
            return f;
        }
    }

    private static EconomicTransportLogicalLink[] Network() => new[]
    {
        Link(11UL, 1UL, 2UL),
        Link(20UL, 2UL, 3UL),
        Link(30UL, 2UL, 4UL),
        Link(40UL, 2UL, 5UL)
    };

    private static IReadOnlyList<EconomicOperationalLogicalTransportSelection>
        Resolve(EconomicLogicalCargoOperationsSnapshot operations,
            Pricing prices, EconomicTransportLogicalLink[]? network = null,
            ulong turn = 1UL,
            IEnumerable<EconomicLogicalTransportServiceIntent>? intents = null,
            ulong physicalOrdinal = 1UL) 
    {
        var links = network ?? Network();
        return new EconomicOperationalLogicalTransportQuoteResolver().Resolve(
            turn, Registry(links), Physical(links, physicalOrdinal),
            operations, intents ?? new[] { Intent() }, prices);
    }

    [Fact]
    public void SeparateLinesJoinAtNodeButOnlyEndpointsWithCargoGrantsMayTrade()
    {
        var prices = new Pricing();
        var selected = Assert.Single(Resolve(Operations(
            includeIntermediate: true), prices));
        Assert.Equal(101UL, selected.LoadFacilityId);
        Assert.Equal(202UL, selected.UnloadFacilityId);
        Assert.Equal(1UL, selected.OperationsSnapshotOrdinal);
        Assert.Equal(1UL, selected.RegisteredSelection.AuthorizationSnapshotOrdinal);
        Assert.Equal(new[] { Node(1UL), Node(2UL), Node(3UL) },
            selected.RegisteredSelection.Selection.NodePath);
        Assert.Equal(new[] { 11UL, 20UL },
            selected.RegisteredSelection.Selection.LinkIds);
        Assert.Equal(new EconomicPointId(2UL),
            selected.Quote.RouteKey.DestinationEconomicPointId);
        Assert.Equal(1, Assert.Single(prices.Functions).Evaluations);

        // Although multiple actual constructed lines pass through B and
        // even a facility may be physically located there, B has NO
        // explicit commercial handling permission for this technology.
        var operations = Operations(includeIntermediate: true);
        Assert.Throws<InvalidOperationException>(() =>
            operations.RequireOperation(new EconomicPointId(3UL), Node(2UL),
                71UL, 4UL, EconomicLogicalCargoOperation.Unload));
    }

    [Fact]
    public void MissingLoadOrUnloadRejectsQuoteBeforeAnyPricing()
    {
        foreach (var missingLoad in new[] { true, false })
        {
            var prices = new Pricing();
            var operations = Operations(includeLoad: !missingLoad,
                includeUnload: missingLoad);
            Assert.Throws<InvalidOperationException>(() =>
                Resolve(operations, prices));
            Assert.Empty(prices.Functions);
        }
    }

    [Fact]
    public void WrongFacilityNodeOrTechnologyDoesNotGrantCargoRights()
    {
        var prices = new Pricing();
        var wrongNode = new EconomicLogicalCargoOperationsSnapshot(1UL,
            new[] { Facility(101UL, 1UL, 2UL), Facility(202UL, 2UL, 3UL) },
            new[] { Permit(101UL, EconomicLogicalCargoOperation.Load),
                Permit(202UL, EconomicLogicalCargoOperation.Unload) });
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(wrongNode, prices));
        Assert.Empty(prices.Functions);

        var wrongTechnology = new EconomicLogicalCargoOperationsSnapshot(1UL,
            new[] { Facility(101UL, 1UL, 1UL), Facility(202UL, 2UL, 3UL) },
            new[]
            {
                Permit(101UL, EconomicLogicalCargoOperation.Load, 800UL, 1UL),
                Permit(202UL, EconomicLogicalCargoOperation.Unload)
            });
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(wrongTechnology, prices));
        Assert.Empty(prices.Functions);
    }

    [Fact]
    public void DisabledFacilityAndMismatchingSnapshotOrdinalsFailClosed()
    {
        var prices = new Pricing();
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Operations(sourceOperational: false), prices));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Operations(destinationOperational: false), prices));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Operations(ordinal: 2UL), prices));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Operations(), prices, physicalOrdinal: 3UL));
        Assert.Empty(prices.Functions);
    }

    [Fact]
    public void AFacilityCanHandleMultipleApproachesWithoutPerLinkTerminals()
    {
        var snapshot = new EconomicLogicalCargoOperationsSnapshot(1UL,
            new[] { Facility(500UL, 5UL, 2UL), Facility(101UL, 1UL, 1UL),
                Facility(202UL, 2UL, 3UL) },
            new[]
            {
                Permit(500UL, EconomicLogicalCargoOperation.Load),
                Permit(500UL, EconomicLogicalCargoOperation.Unload),
                Permit(101UL, EconomicLogicalCargoOperation.Load),
                Permit(202UL, EconomicLogicalCargoOperation.Unload)
            });
        Assert.Equal(500UL, snapshot.RequireOperation(
            new EconomicPointId(5UL), Node(2UL), 71UL, 4UL,
            EconomicLogicalCargoOperation.Load));
        Assert.Equal(500UL, snapshot.RequireOperation(
            new EconomicPointId(5UL), Node(2UL), 71UL, 4UL,
            EconomicLogicalCargoOperation.Unload));
        Assert.Single(Resolve(snapshot, new Pricing()));
    }

    [Fact]
    public void TransferRequiresExplicitDirectedGrantAndBothHandlingCapabilities()
    {
        var facility = Facility(500UL, 5UL, 2UL);
        var transfer = new EconomicLogicalCargoTransferPermission(
            500UL, 71UL, 4UL, 800UL, 1UL);
        var permissions = new[]
        {
            Permit(500UL, EconomicLogicalCargoOperation.Unload),
            Permit(500UL, EconomicLogicalCargoOperation.Load, 800UL, 1UL)
        };
        var snapshot = new EconomicLogicalCargoOperationsSnapshot(1UL,
            new[] { facility }, permissions, new[] { transfer });
        snapshot.RequireTransfer(500UL, Node(2UL), 71UL, 4UL, 800UL, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            snapshot.RequireTransfer(500UL, Node(2UL),
                800UL, 1UL, 71UL, 4UL));
        Assert.Throws<InvalidOperationException>(() =>
            snapshot.RequireTransfer(500UL, Node(3UL),
                71UL, 4UL, 800UL, 1UL));

        var noTransfer = new EconomicLogicalCargoOperationsSnapshot(
            1UL, new[] { facility }, permissions);
        Assert.Throws<InvalidOperationException>(() =>
            noTransfer.RequireTransfer(500UL, Node(2UL),
                71UL, 4UL, 800UL, 1UL));

        var missingLoad = new EconomicLogicalCargoOperationsSnapshot(1UL,
            new[] { facility },
            new[] { Permit(500UL, EconomicLogicalCargoOperation.Unload) },
            new[] { transfer });
        Assert.Throws<InvalidOperationException>(() =>
            missingLoad.RequireTransfer(500UL, Node(2UL),
                71UL, 4UL, 800UL, 1UL));
    }

    [Fact]
    public void MultipleFacilitiesUseCanonicalIdAndRetainQuoteProvenance()
    {
        var facilities = new[]
        {
            Facility(203UL, 2UL, 3UL), Facility(202UL, 2UL, 3UL),
            Facility(105UL, 1UL, 1UL), Facility(101UL, 1UL, 1UL)
        };
        var permissions = new[]
        {
            Permit(203UL, EconomicLogicalCargoOperation.Unload),
            Permit(105UL, EconomicLogicalCargoOperation.Load),
            Permit(202UL, EconomicLogicalCargoOperation.Unload),
            Permit(101UL, EconomicLogicalCargoOperation.Load)
        };
        var first = new EconomicLogicalCargoOperationsSnapshot(
            1UL, facilities, permissions);
        var reversed = new EconomicLogicalCargoOperationsSnapshot(
            1UL, facilities.Reverse(), permissions.Reverse());
        var a = Assert.Single(Resolve(first, new Pricing()));
        var b = Assert.Single(Resolve(reversed, new Pricing()));
        Assert.Equal(101UL, a.LoadFacilityId);
        Assert.Equal(202UL, a.UnloadFacilityId);
        Assert.Equal(a.LoadFacilityId, b.LoadFacilityId);
        Assert.Equal(a.UnloadFacilityId, b.UnloadFacilityId);
        Assert.Equal(a.Quote.RawUnitCostPerGram, b.Quote.RawUnitCostPerGram);
    }

    [Fact]
    public void CompetingServicesQuoteOnceAndProvenanceFollowsCheapestOffer()
    {
        var prices = new Pricing();
        var result = Assert.Single(Resolve(Operations(), prices,
            intents: new[] { Intent(200UL), Intent(100UL) }));
        Assert.Equal(200UL,
            result.RegisteredSelection.Selection.ServiceId);
        Assert.Equal(2, prices.Functions.Count);
        Assert.All(prices.Functions, f => Assert.Equal(1, f.Evaluations));

        // Active unauthorized alternatives do not bypass the gate just
        // because another service could have been commercially selected.
        var wrongTech = Intent(300UL, 800UL, 1UL);
        var invalidPrices = new Pricing();
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Operations(), invalidPrices,
                intents: new[] { Intent(200UL), wrongTech }));
        Assert.Empty(invalidPrices.Functions);
    }

    [Fact]
    public void OperationRevocationInNewCycleNeverAltersPriorFrozenFreight()
    {
        var prices = new Pricing();
        var first = Assert.Single(Resolve(Operations(), prices));
        var earlierPrice = first.Quote.RawUnitCostPerGram;
        var nextPrices = new Pricing();
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(Operations(ordinal: 2UL, includeUnload: false),
                nextPrices, turn: 2UL, physicalOrdinal: 2UL));
        Assert.Empty(nextPrices.Functions);
        Assert.Equal(earlierPrice, first.Quote.RawUnitCostPerGram);
        Assert.Equal(1UL, first.Quote.TurnOrdinal);

        var restored = Assert.Single(Resolve(
            Operations(ordinal: 3UL), nextPrices, turn: 3UL,
            physicalOrdinal: 3UL, intents: new[] { Intent(
                forecast: 8_000_000_000UL) }));
        Assert.Equal(3UL, restored.Quote.TurnOrdinal);
        Assert.Equal(3UL, restored.OperationsSnapshotOrdinal);
        Assert.Equal(101UL, restored.LoadFacilityId);
        Assert.Equal(202UL, restored.UnloadFacilityId);
        Assert.NotEqual(earlierPrice, restored.Quote.RawUnitCostPerGram);
        Assert.Equal(1, Assert.Single(nextPrices.Functions).Evaluations);
    }

    [Fact]
    public void InvalidOrDuplicateFacilityAndGrantIdentityFailsClosed()
    {
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalCargoOperationsSnapshot(1UL,
                new[] { Facility(101UL, 1UL, 1UL),
                    Facility(101UL, 1UL, 1UL) },
                Array.Empty<EconomicLogicalCargoPermission>()));
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalCargoOperationsSnapshot(1UL,
                new[] { Facility(101UL, 1UL, 1UL) },
                new[] { Permit(202UL, EconomicLogicalCargoOperation.Load) }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalCargoOperationsSnapshot(1UL,
                new[] { Facility(101UL, 1UL, 1UL) },
                new[] {
                    Permit(101UL, EconomicLogicalCargoOperation.Load),
                    Permit(101UL, EconomicLogicalCargoOperation.Load)
                }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicLogicalCargoOperationsSnapshot(1UL,
                new[] { Facility(101UL, 1UL, 1UL) },
                Array.Empty<EconomicLogicalCargoPermission>(),
                new[] { new EconomicLogicalCargoTransferPermission(
                    202UL, 71UL, 4UL, 800UL, 1UL) }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicLogicalCargoPermission(101UL, 71UL, 4UL,
                (EconomicLogicalCargoOperation)255));
    }
}
