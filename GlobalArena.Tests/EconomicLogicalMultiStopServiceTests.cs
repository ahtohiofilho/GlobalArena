using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicLogicalMultiStopServiceTests
{
    private const ulong Tech = 71UL;
    private const ulong Ver = 4UL;
    private static EconomicTransportLogicalNodeId Node(ulong id) => new(id);
    private static EconomicLogicalServiceCall Call(ulong point, ulong node) =>
        new(new EconomicPointId(point), Node(node));
    private static EconomicTransportLogicalLink Link(
        ulong id, ulong a, ulong b, ulong technology = Tech) =>
        new(id, Node(a), Node(b), technology, Ver);
    private static EconomicLogicalServiceConsignment Cargo(
        ulong id, int board, int alight, ulong forecast) =>
        new(id, board, alight, new BigInteger(forecast));
    private static EconomicLogicalMultiStopPattern Pattern(
        ulong id, EconomicLogicalServiceCall[] calls,
        EconomicLogicalServiceConsignment[] cargo, bool active = true) =>
        new(id, Tech, Ver, calls, cargo, active);

    private static readonly EconomicTransportLogicalLink[] Network =
    {
        Link(11UL, 1UL, 2UL),
        Link(20UL, 2UL, 3UL),
        Link(9UL, 1UL, 3UL)
    };

    private static readonly EconomicLogicalServiceCall[] AllCalls =
    {
        Call(1UL, 1UL), Call(2UL, 2UL), Call(3UL, 3UL)
    };
    private static readonly EconomicLogicalServiceCall[] DirectCalls =
    {
        Call(1UL, 1UL), Call(3UL, 3UL)
    };

    private static EconomicLogicalTransportProviderRegistry Registry(
        IEnumerable<EconomicTransportLogicalLink>? links = null) =>
        new(new[]
        {
            new EconomicLogicalTransportTechnologyDefinition(Tech, Ver, 9UL, 2UL)
        }, new IEconomicLogicalTransportTraversalProvider[]
        {
            new EconomicConstructedLogicalLineProvider(
                9UL, 2UL, links ?? Network)
        });

    private static EconomicLogicalTransportAuthorizationSnapshot Physical(
        ulong ordinal = 1UL,
        IEnumerable<EconomicTransportLogicalLink>? links = null) =>
        new(ordinal, new[]
        {
            new EconomicLogicalTransportPointAnchor(
                new EconomicPointId(1UL), Node(1UL)),
            new EconomicLogicalTransportPointAnchor(
                new EconomicPointId(2UL), Node(2UL)),
            new EconomicLogicalTransportPointAnchor(
                new EconomicPointId(3UL), Node(3UL))
        }, (links ?? Network).Select(x =>
            new EconomicLogicalTransportAuthorizedLink(9UL, 2UL, x)));

    private static EconomicLogicalCargoOperationsSnapshot Operations(
        ulong ordinal = 1UL, bool middleHasCargoGrants = true) =>
        new(ordinal, new[]
        {
            new EconomicLogicalCargoFacility(101UL,
                new EconomicPointId(1UL), Node(1UL)),
            new EconomicLogicalCargoFacility(202UL,
                new EconomicPointId(2UL), Node(2UL)),
            new EconomicLogicalCargoFacility(303UL,
                new EconomicPointId(3UL), Node(3UL))
        }, new[]
        {
            new EconomicLogicalCargoPermission(101UL, Tech, Ver,
                EconomicLogicalCargoOperation.Load),
            new EconomicLogicalCargoPermission(303UL, Tech, Ver,
                EconomicLogicalCargoOperation.Unload)
        }.Concat(middleHasCargoGrants ? new[]
        {
            new EconomicLogicalCargoPermission(202UL, Tech, Ver,
                EconomicLogicalCargoOperation.Load),
            new EconomicLogicalCargoPermission(202UL, Tech, Ver,
                EconomicLogicalCargoOperation.Unload)
        } : Array.Empty<EconomicLogicalCargoPermission>()));

    private sealed class Function(FoodTransportCostFraction unitPrice) :
        IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 17UL;
        public int Evaluations { get; private set; }
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            return unitPrice;
        }
    }

    /// <summary>
    /// TEST-ONLY illustrative economic cost policy. A scheduled middle call
    /// incurs 1,200 cost units shared ONCE across 12 forecast grams of all
    /// consignments (not once per OD). An OD that loads or unloads at B
    /// additionally bears 25 units per gram of cargo handling; A->C cargo
    /// stays aboard, thus never incurs this per-cargo handling term.
    /// The production contract never imposes these arbitrary coefficients.
    /// </summary>
    private sealed class SharedStopPricing : IEconomicLogicalMultiStopPricing
    {
        public List<Function> Functions { get; } = new();
        public Dictionary<ulong, ulong[]> Ridden { get; } = new();
        public List<(BigInteger[] Legs, BigInteger[] Boarded,
            BigInteger[] Alighted)> Forecasts { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForConsignment(
            EconomicLogicalMultiStopLoadContext context,
            EconomicLogicalServiceConsignment consignment,
            IReadOnlyList<EconomicTransportLogicalLink> wholePatternLinks,
            IReadOnlyList<EconomicTransportLogicalLink> riddenLinks)
        {
            Ridden.Add(consignment.ConsignmentId,
                riddenLinks.Select(x => x.LinkId).ToArray());
            Forecasts.Add((
                context.ExpectedOnboardMicrograms.ToArray(),
                context.ExpectedBoardedMicrograms.ToArray(),
                context.ExpectedAlightedMicrograms.ToArray()));
            var numerator = (BigInteger)10;
            var denominator = BigInteger.One;
            if (context.Pattern.Calls.Count > 2)
            {
                var total = context.Pattern.Consignments.Aggregate(
                    BigInteger.Zero, (sum, x) =>
                        sum + x.ExpectedLoadMicrograms);
                if (total.Sign > 0)
                {
                    // Only the fixture's single real scheduled stop has
                    // overhead: 1200 currency / (forecast grams).
                    numerator = 10 * total + 1200 * 1_000_000;
                    denominator = total;
                }
                if (consignment.BoardCallIndex == 1 ||
                    consignment.AlightCallIndex == 1)
                    numerator += 25 * denominator;
            }
            var function = new Function(new FoodTransportCostFraction(
                numerator, denominator));
            Functions.Add(function);
            return function;
        }
    }

    private static IReadOnlyList<EconomicLogicalMultiStopSelection> Resolve(
        IEnumerable<EconomicLogicalMultiStopPattern> patterns,
        SharedStopPricing pricing,
        ulong turn = 1UL,
        EconomicLogicalTransportProviderRegistry? registry = null,
        EconomicLogicalTransportAuthorizationSnapshot? physical = null,
        EconomicLogicalCargoOperationsSnapshot? operations = null) =>
        new EconomicLogicalMultiStopQuoteResolver().Resolve(
            turn, registry ?? Registry(), physical ?? Physical(),
            operations ?? Operations(), patterns, pricing);

    [Fact]
    public void ThreeCargoDestinationsShareOneScheduledStopWithoutUnloadingThroughCargo()
    {
        var scheduled = Pattern(100UL, AllCalls, new[]
        {
            Cargo(1UL, 0, 2, 6_000_000UL),
            Cargo(2UL, 0, 1, 2_000_000UL),
            Cargo(3UL, 1, 2, 4_000_000UL)
        });
        var pricing = new SharedStopPricing();
        var chosen = Resolve(new[] { scheduled }, pricing);
        Assert.Equal(3, chosen.Count);
        Assert.Equal(new[] { 11UL, 20UL },
            chosen[0].WholePatternLinkIds);
        Assert.Equal(new[] { 11UL, 20UL }, pricing.Ridden[1UL]);
        Assert.Equal(new[] { 11UL }, pricing.Ridden[2UL]);
        Assert.Equal(new[] { 20UL }, pricing.Ridden[3UL]);
        Assert.Equal(new BigInteger[] { 8_000_000, 10_000_000 },
            pricing.Forecasts[0].Legs);
        Assert.Equal(new BigInteger[] { 8_000_000, 4_000_000, 0 },
            pricing.Forecasts[0].Boarded);
        Assert.Equal(new BigInteger[] { 0, 2_000_000, 10_000_000 },
            pricing.Forecasts[0].Alighted);

        var through = chosen.Single(x => x.ConsignmentId == 1UL);
        var toMiddle = chosen.Single(x => x.ConsignmentId == 2UL);
        var fromMiddle = chosen.Single(x => x.ConsignmentId == 3UL);
        Assert.Equal(101UL, through.LoadFacilityId);
        Assert.Equal(303UL, through.UnloadFacilityId);
        Assert.Equal(202UL, toMiddle.UnloadFacilityId);
        Assert.Equal(202UL, fromMiddle.LoadFacilityId);
        Assert.Equal(new FoodTransportCostFraction(110, 1),
            through.Quote.RawUnitCostPerGram);
        Assert.Equal(new FoodTransportCostFraction(135, 1),
            toMiddle.Quote.RawUnitCostPerGram);
        Assert.Equal(new FoodTransportCostFraction(135, 1),
            fromMiddle.Quote.RawUnitCostPerGram);

        // 12 forecast grams × 100 raw shared charge = 1200 ONCE.
        // The other 25/g at B applies only to the 2g + 4g that
        // physically load or unload there, not 6g passing through.
        var sharedRecovered = (through.Quote.RawUnitCostPerGram.Numerator -
            10 * through.Quote.RawUnitCostPerGram.Denominator) *
            12;
        Assert.Equal(new BigInteger(1200), sharedRecovered);
        Assert.Equal(3, pricing.Functions.Count);
        Assert.All(pricing.Functions, f => Assert.Equal(1, f.Evaluations));
    }

    [Fact]
    public void DirectServiceCompetesWithCallingServiceWithoutPermanentStopSurcharge()
    {
        var calls = Pattern(100UL, AllCalls,
            new[] { Cargo(1UL, 0, 2, 6_000_000UL) });
        var direct = Pattern(200UL, DirectCalls,
            new[] { Cargo(4UL, 0, 1, 6_000_000UL) });
        var price = new SharedStopPricing();
        var result = Assert.Single(Resolve(new[] { calls, direct }, price));
        Assert.Equal(200UL, result.PatternId);
        Assert.Equal(4UL, result.ConsignmentId);
        Assert.Equal(new[] { 9UL }, result.RiddenLinkIds);
        Assert.Equal(new FoodTransportCostFraction(10, 1),
            result.Quote.RawUnitCostPerGram);
        Assert.Equal(2, price.Functions.Count);
        Assert.All(price.Functions, f => Assert.Equal(1, f.Evaluations));
    }

    [Fact]
    public void PassingThroughMiddleNodeWithoutScheduledCallNeedsNoMiddleCargoFacility()
    {
        var direct = Pattern(200UL, DirectCalls,
            new[] { Cargo(4UL, 0, 1, 6_000_000UL) });
        // Force A->B->C traversal by removing the physical A->C shortcut:
        // B is physically crossed but is NOT a scheduled service call.
        var edges = Network.Where(x => x.LinkId != 9UL).ToArray();
        var price = new SharedStopPricing();
        var chosen = Assert.Single(Resolve(
            new[] { direct }, price, registry: Registry(edges),
            physical: Physical(links: edges),
            operations: Operations(middleHasCargoGrants: false)));
        Assert.Equal(new[] { 11UL, 20UL }, chosen.RiddenLinkIds);
        Assert.Equal(new FoodTransportCostFraction(10, 1),
            chosen.Quote.RawUnitCostPerGram);
        Assert.Equal(1, Assert.Single(price.Functions).Evaluations);
    }

    [Fact]
    public void ScheduledStopWithNoBookedCargoAtMiddleDoesNotRequireCargoHandling()
    {
        var pattern = Pattern(100UL, AllCalls,
            new[] { Cargo(1UL, 0, 2, 6_000_000UL) });
        var price = new SharedStopPricing();
        var selection = Assert.Single(Resolve(new[] { pattern }, price,
            operations: Operations(middleHasCargoGrants: false)));
        Assert.Equal(new[] { 11UL, 20UL }, selection.RiddenLinkIds);
        Assert.Equal(new FoodTransportCostFraction(210, 1),
            selection.Quote.RawUnitCostPerGram);
        Assert.Equal(1, Assert.Single(price.Functions).Evaluations);
    }

    [Fact]
    public void BookedMiddleCargoRequiresExactLoadingAndUnloadingAuthorizationBeforePrice()
    {
        var price = new SharedStopPricing();
        var pattern = Pattern(100UL, AllCalls, new[]
        {
            Cargo(1UL, 0, 2, 6_000_000UL),
            Cargo(2UL, 0, 1, 2_000_000UL),
            Cargo(3UL, 1, 2, 4_000_000UL)
        });
        Assert.Throws<InvalidOperationException>(() => Resolve(
            new[] { pattern }, price,
            operations: Operations(middleHasCargoGrants: false)));
        Assert.Empty(price.Functions);
        Assert.Empty(price.Ridden);
    }

    [Fact]
    public void AMissingPhysicalSegmentOrSnapshotMismatchCannotProducePartialQuotes()
    {
        var price = new SharedStopPricing();
        var stop = Pattern(100UL, AllCalls,
            new[] { Cargo(1UL, 0, 2, 6_000_000UL) });
        var onlyFirst = Network.Where(x => x.LinkId != 20UL).ToArray();
        var noRoute = Resolve(new[] { stop }, price,
            physical: Physical(links: onlyFirst));
        Assert.Empty(noRoute);
        Assert.Empty(price.Functions);

        Assert.Throws<InvalidOperationException>(() => Resolve(
            new[] { stop }, price,
            operations: Operations(ordinal: 2UL)));
        Assert.Empty(price.Functions);
    }

    [Fact]
    public void LaterCycleForecastRepricesOnlyNewSnapshotWhileOldQuoteRemainsFrozen()
    {
        var first = Pattern(100UL, AllCalls, new[]
        {
            Cargo(1UL, 0, 2, 6_000_000UL),
            Cargo(2UL, 0, 1, 2_000_000UL),
            Cargo(3UL, 1, 2, 4_000_000UL)
        });
        var priced = new SharedStopPricing();
        var oldSelection = Resolve(new[] { first }, priced)
            .Single(x => x.ConsignmentId == 1UL);
        var originalPrice = oldSelection.Quote.RawUnitCostPerGram;
        var later = Pattern(100UL, AllCalls, new[]
        {
            Cargo(1UL, 0, 2, 12_000_000UL),
            Cargo(2UL, 0, 1, 2_000_000UL),
            Cargo(3UL, 1, 2, 4_000_000UL)
        });
        var newPricing = new SharedStopPricing();
        var nextSelection = Resolve(new[] { later }, newPricing, turn: 2UL)
            .Single(x => x.ConsignmentId == 1UL);
        Assert.True(originalPrice.CompareTo(
            nextSelection.Quote.RawUnitCostPerGram) > 0);
        Assert.Equal(1UL, oldSelection.Quote.TurnOrdinal);
        Assert.Equal(2UL, nextSelection.Quote.TurnOrdinal);
        Assert.Equal(originalPrice, oldSelection.Quote.RawUnitCostPerGram);
        Assert.All(priced.Functions, f => Assert.Equal(1, f.Evaluations));
        Assert.All(newPricing.Functions, f => Assert.Equal(1, f.Evaluations));
    }

    [Fact]
    public void ScheduledStopsAndConsignmentsAreCanonicalAndMalformedInputsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => Pattern(100UL, AllCalls,
            new[] { Cargo(1UL, 0, 2, 10UL), Cargo(1UL, 1, 2, 20UL) }));
        Assert.Throws<ArgumentException>(() => Pattern(100UL,
            new[] { Call(1UL, 1UL), Call(2UL, 1UL) },
            new[] { Cargo(1UL, 0, 1, 10UL) }));
        Assert.Throws<ArgumentException>(() => Pattern(100UL, DirectCalls,
            new[] { Cargo(1UL, 0, 2, 10UL) }));
        Assert.Throws<ArgumentException>(() => Cargo(1UL, 1, 1, 10UL));
        Assert.Throws<ArgumentException>(() => Resolve(
            new[] {
                Pattern(100UL, DirectCalls,
                    new[] { Cargo(1UL, 0, 1, 10UL) }),
                Pattern(200UL, DirectCalls,
                    new[] { Cargo(1UL, 0, 1, 10UL) })
            }, new SharedStopPricing()));

        var first = Pattern(100UL, AllCalls, new[]
        {
            Cargo(3UL, 1, 2, 4_000_000UL),
            Cargo(1UL, 0, 2, 6_000_000UL),
            Cargo(2UL, 0, 1, 2_000_000UL)
        });
        var second = Pattern(100UL, AllCalls, first.Consignments
            .Reverse().ToArray());
        var a = Resolve(new[] { first }, new SharedStopPricing());
        var b = Resolve(new[] { second }, new SharedStopPricing());
        Assert.Equal(a.Select(x => x.ConsignmentId),
            b.Select(x => x.ConsignmentId));
        Assert.Equal(a.Select(x => x.Quote.RawUnitCostPerGram),
            b.Select(x => x.Quote.RawUnitCostPerGram));
    }
}
