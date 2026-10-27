using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFinePriceAwareServiceResolverTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(4, 0);

    private sealed class SmoothTestCost(ulong baseUnitCost)
        : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 3UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var denominator = expectedLoadMicrograms + 100_000_000;
            // Synthetic smooth scale economy, not a gameplay modal tariff.
            return new FoodTransportCostFraction(
                (BigInteger)baseUnitCost * denominator +
                    60_000_000_000, denominator);
        }
    }

    private sealed class WholeItineraryTestPricing(bool equalPrices = false)
        : IEconomicFineTransportItineraryPricing
    {
        public List<ulong[]> EvaluatedAssetPaths { get; } = new();
        public List<SmoothTestCost> Functions { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
        {
            EvaluatedAssetPaths.Add(orderedAssets.Select(x => x.AssetId).ToArray());
            Assert.All(orderedAssets, asset =>
            {
                Assert.Equal(intent.TechnologyTypeId, asset.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion,
                    asset.TechnologyCatalogVersion);
            });
            var f = new SmoothTestCost(
                equalPrices ? 50UL :
                orderedAssets.Count == 1 ? 500UL : 50UL);
            Functions.Add(f);
            return f;
        }
    }

    private sealed class MarketValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new((BigInteger)800_000 * Unit - (BigInteger)10_000 * q, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(2 * (BigInteger)800_000 * q * Unit -
                (BigInteger)10_000 * q * q, 2 * (BigInteger)Unit * Unit);
    }

    private sealed record Fixture(
        StrategicSurfaceGraph Graph,
        Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors,
        EconomicTransportBuiltAsset[] Assets,
        ulong[] LongerAssetIds,
        StrategicEdgeId DirectEdgeId);

    private static Fixture MakeFixture()
    {
        var graph = new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(Fine));
        var direct = graph.StrategicTopology.Edges[0];
        var source = direct.IncidentCellIds[0];
        var destination = direct.IncidentCellIds[1];
        var byNode = new Dictionary<StrategicCellId, List<StrategicEdge>>();
        foreach (var edge in graph.StrategicTopology.Edges)
            foreach (var cell in edge.IncidentCellIds)
            {
                if (!byNode.TryGetValue(cell, out var adjacent))
                    byNode[cell] = adjacent = new List<StrategicEdge>();
                adjacent.Add(edge);
            }

        var frontier = new Queue<StrategicCellId>();
        var parents = new Dictionary<StrategicCellId,
            (StrategicCellId Parent, StrategicEdge Edge)>();
        var visited = new HashSet<StrategicCellId> { source };
        frontier.Enqueue(source);
        while (frontier.Count > 0 && !visited.Contains(destination))
        {
            var at = frontier.Dequeue();
            foreach (var edge in byNode[at].OrderBy(x => x.Id.Value))
            {
                if (edge.Id == direct.Id)
                    continue;
                var next = edge.IncidentCellIds.First(x => x != at);
                if (!visited.Add(next))
                    continue;
                parents.Add(next, (at, edge));
                frontier.Enqueue(next);
            }
        }
        Assert.Contains(destination, visited);

        var alternate = new List<StrategicEdge>();
        for (var node = destination; node != source; node = parents[node].Parent)
            alternate.Add(parents[node].Edge);
        alternate.Reverse();
        Assert.True(alternate.Count > 1);

        var assets = new List<EconomicTransportBuiltAsset>();
        var longerIds = new List<ulong>();
        for (var i = 0; i < alternate.Count; i++)
        {
            var id = (ulong)i + 1UL;
            longerIds.Add(id);
            assets.Add(new EconomicTransportBuiltAsset(
                id, 10UL, new PhysicalTacticalEdgeId(Fine, alternate[i].Id), 2UL));
        }
        assets.Add(new EconomicTransportBuiltAsset(
            100UL, 10UL, new PhysicalTacticalEdgeId(Fine, direct.Id), 2UL));

        return new Fixture(graph, new()
        {
            [new EconomicPointId(1UL)] = new PhysicalTacticalTileId(Fine, source),
            [new EconomicPointId(2UL)] = new PhysicalTacticalTileId(Fine, destination)
        }, assets.ToArray(), longerIds.ToArray(), direct.Id);
    }

    private static StrategicEconomicRouteKey Route() => new(
        new EconomicPointId(1UL), new EconomicPointId(2UL));

    private static EconomicFineTransportServiceIntent Intent(
        ulong service = 10UL, ulong forecast = 500_000_000UL,
        bool available = true, ulong technology = 10UL,
        ulong catalog = 2UL) =>
        new(service, Route(), technology, catalog,
            new BigInteger(forecast), available);

    private static EconomicProductionDemandResult Flows() => new(
        new[] { new EconomicSupplyFlow(
            new EconomicPointId(1UL), new CommodityId(7U),
            EconomicActivityKind.Agriculture, 3UL * Unit) },
        new[] { new EconomicDemandFlow(
            new EconomicPointId(2UL), new CommodityId(7U),
            3UL * Unit) });

    private static IReadOnlyList<EconomicTransportServiceSelection> Run(
        Fixture fixture, WholeItineraryTestPricing pricing,
        EconomicFinePriceAwareSearchBudget? budget = null,
        IEnumerable<EconomicTransportBuiltAsset>? assets = null,
        IEnumerable<EconomicFineTransportServiceIntent>? intents = null,
        StrategicEdgeAccessSnapshot? access = null,
        ulong turn = 1UL,
        EconomicFinePriceAwareSearchDiagnostics? diagnostics = null,
        EconomicFinePriceAwareSearchMode searchMode =
            EconomicFinePriceAwareSearchMode.Exhaustive) =>
        new EconomicFinePriceAwareServiceResolver().Resolve(
            turn, fixture.Graph, fixture.Anchors, Flows(),
            access ?? StrategicEdgeAccessSnapshot.AllOpen,
            assets ?? fixture.Assets, intents ?? new[] { Intent() },
            pricing, budget ??
                new EconomicFinePriceAwareSearchBudget(10_000UL, 100UL),
            diagnostics, searchMode);

    /// <summary>
    /// Actual fine physical constructed side branches connected ONLY through
    /// the source tile. A simple route cannot enter and then return through
    /// its source, so these branch links are never eligible for settlement.
    /// The original direct/longer competing economic choices still exist.
    /// </summary>
    private static Fixture WithBuiltSourceSpurs(
        Fixture original, int requestedBranches)
    {
        var edges = original.Graph.StrategicTopology.Edges;
        var byNode = new Dictionary<StrategicCellId, List<StrategicEdge>>();
        foreach (var edge in edges)
            foreach (var cell in edge.IncidentCellIds)
            {
                if (!byNode.TryGetValue(cell, out var adjacent))
                    byNode[cell] = adjacent = new List<StrategicEdge>();
                adjacent.Add(edge);
            }

        var alreadyUsed = new HashSet<StrategicCellId>();
        foreach (var asset in original.Assets)
        {
            var edge = edges.First(x => x.Id == asset.GeographicEdgeId);
            foreach (var node in edge.IncidentCellIds)
                alreadyUsed.Add(node);
        }

        var source = original.Anchors[new EconomicPointId(1UL)]
            .FineStrategicCellId;
        var visited = new HashSet<StrategicCellId>(alreadyUsed);
        var queue = new Queue<StrategicCellId>();
        queue.Enqueue(source);
        var additional = new List<EconomicTransportBuiltAsset>();
        while (queue.Count > 0 && additional.Count < requestedBranches)
        {
            var node = queue.Dequeue();
            foreach (var edge in byNode[node].OrderBy(x => x.Id.Value))
            {
                if (additional.Count == requestedBranches)
                    break;
                var next = edge.IncidentCellIds.First(x => x != node);
                if (!visited.Add(next))
                    continue;
                additional.Add(new EconomicTransportBuiltAsset(
                    (ulong)(1000 + additional.Count), 10UL,
                    new PhysicalTacticalEdgeId(Fine, edge.Id), 2UL));
                queue.Enqueue(next);
            }
        }
        Assert.Equal(requestedBranches, additional.Count);
        return new Fixture(original.Graph, original.Anchors,
            original.Assets.Concat(additional).ToArray(),
            original.LongerAssetIds, original.DirectEdgeId);
    }

    /// <summary>
    /// Attach a fully CONSTRUCTED closed physical cycle to exactly ONE core
    /// vertex. All its vertices have degree >= 2, so ordinary leaf peeling
    /// leaves this useless branch intact; the block-cut graph excludes it
    /// because any source/destination simple path would repeat the join.
    /// </summary>
    private static Fixture WithBuiltPendantCycle(Fixture original)
    {
        var edges = original.Graph.StrategicTopology.Edges;
        var builtCells = original.Assets.SelectMany(asset =>
            edges.Single(e => e.Id == asset.GeographicEdgeId).IncidentCellIds)
            .ToHashSet();
        var source = original.Anchors[new EconomicPointId(1UL)]
            .FineStrategicCellId;
        var sourceNeighbors = edges.Where(e =>
                e.IncidentCellIds.Contains(source))
            .Select(e => (Edge: e, Other: e.IncidentCellIds.First(x => x != source)))
            .Where(x => !builtCells.Contains(x.Other))
            .ToArray();

        StrategicEdge? first = null;
        StrategicEdge? second = null;
        StrategicEdge? between = null;
        foreach (var a in sourceNeighbors)
        {
            foreach (var b in sourceNeighbors)
            {
                if (a.Other == b.Other)
                    continue;
                var bridge = edges.FirstOrDefault(e =>
                    e.IncidentCellIds.Contains(a.Other) &&
                    e.IncidentCellIds.Contains(b.Other));
                if (bridge is null)
                    continue;
                first = a.Edge;
                second = b.Edge;
                between = bridge;
                break;
            }
            if (between is not null)
                break;
        }
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(between);
        var newAssets = original.Assets.Concat(new[]
        {
            new EconomicTransportBuiltAsset(
                1000UL, 10UL, new PhysicalTacticalEdgeId(Fine, first!.Id), 2UL),
            new EconomicTransportBuiltAsset(
                1001UL, 10UL, new PhysicalTacticalEdgeId(Fine, second!.Id), 2UL),
            new EconomicTransportBuiltAsset(
                1002UL, 10UL, new PhysicalTacticalEdgeId(Fine, between!.Id), 2UL)
        }).ToArray();
        return new Fixture(original.Graph, original.Anchors,
            newAssets, original.LongerAssetIds, original.DirectEdgeId);
    }

    [Fact]
    public void BlockCutEliminatesCyclicDetourThatLeafPruningCannotEliminate()
    {
        var fixture = WithBuiltPendantCycle(MakeFixture());
        var fullCounters = new EconomicFinePriceAwareSearchDiagnostics();
        var leafCounters = new EconomicFinePriceAwareSearchDiagnostics();
        var blockCounters = new EconomicFinePriceAwareSearchDiagnostics();
        var full = Assert.Single(Run(fixture,
            new WholeItineraryTestPricing(), diagnostics: fullCounters));
        var leaf = Assert.Single(Run(fixture,
            new WholeItineraryTestPricing(), diagnostics: leafCounters,
            searchMode: EconomicFinePriceAwareSearchMode.TerminalLeafPruning));
        var block = Assert.Single(Run(fixture,
            new WholeItineraryTestPricing(), diagnostics: blockCounters,
            searchMode: EconomicFinePriceAwareSearchMode.BlockCutCorridorPruning));

        Assert.Equal(fixture.LongerAssetIds, block.AssetIds);
        Assert.Equal(full.AssetIds, leaf.AssetIds);
        Assert.Equal(full.AssetIds, block.AssetIds);
        Assert.Equal(0, full.Quote.RawUnitCostPerGram.CompareTo(
            block.Quote.RawUnitCostPerGram));
        Assert.Equal(fullCounters.CompletePaths, blockCounters.CompletePaths);
        Assert.Equal(fullCounters.EvaluatedFreightFunctions,
            blockCounters.EvaluatedFreightFunctions);
        Assert.Equal(2UL, blockCounters.CompletePaths);
        Assert.True(blockCounters.ExaminedLinks < leafCounters.ExaminedLinks);
        Assert.True(blockCounters.ExaminedLinks < fullCounters.ExaminedLinks);
        Assert.True(blockCounters.PrunedPhysicalVertices >
            leafCounters.PrunedPhysicalVertices);
        Assert.True(blockCounters.RejectedPrunedLinks > 0UL);
    }

    [Fact]
    public void BlockCutMaintainsExactEconomicChoicesAcrossPhysicalClosuresAndParallelAssets()
    {
        var fixture = WithBuiltPendantCycle(MakeFixture());
        var direct = fixture.Assets.Last(x => x.AssetId == 100UL);
        var options = fixture.Assets.Append(new EconomicTransportBuiltAsset(
            1_500UL, 10UL,
            new PhysicalTacticalEdgeId(Fine, direct.GeographicEdgeId),
            2UL)).ToArray();
        for (var seed = 1; seed <= 24; seed++)
        {
            var rng = new Random(seed * 15427);
            var closed = (seed % 4) switch
            {
                0 => new StrategicEdgeAccessSnapshot(
                    new[] { fixture.DirectEdgeId }),
                1 => new StrategicEdgeAccessSnapshot(
                    new[] { options[^2].GeographicEdgeId }),
                _ => StrategicEdgeAccessSnapshot.AllOpen
            };
            var chosen = seed % 3 == 0
                ? options.Where(x => x.AssetId != 1002UL).ToArray()
                : options;
            var permuted = chosen.OrderBy(_ => rng.Next()).ToArray();
            var forecast = (ulong)(500_000_000 + seed * 5_000_000);
            var a = new EconomicFinePriceAwareSearchDiagnostics();
            var b = new EconomicFinePriceAwareSearchDiagnostics();
            var baseSelection = Assert.Single(Run(
                fixture, new WholeItineraryTestPricing(),
                assets: chosen, access: closed,
                intents: new[] { Intent(200UL, forecast), Intent(100UL, forecast) },
                diagnostics: a));
            var prunedSelection = Assert.Single(Run(
                fixture, new WholeItineraryTestPricing(),
                assets: permuted, access: closed,
                intents: new[] { Intent(100UL, forecast), Intent(200UL, forecast) },
                diagnostics: b,
                searchMode: EconomicFinePriceAwareSearchMode.BlockCutCorridorPruning));
            Assert.Equal(baseSelection.AssetIds, prunedSelection.AssetIds);
            Assert.Equal(baseSelection.ServiceId, prunedSelection.ServiceId);
            Assert.Equal(0, baseSelection.Quote.RawUnitCostPerGram.CompareTo(
                prunedSelection.Quote.RawUnitCostPerGram));
            Assert.Equal(a.CompletePaths, b.CompletePaths);
            Assert.Equal(a.EvaluatedFreightFunctions, b.EvaluatedFreightFunctions);
        }
    }

    [Fact]
    public void PassingAnIntermediateEconomicPointDoesNotCreateACommercialStop()
    {
        var fixture = MakeFixture();
        var firstLongEdge = fixture.Graph.StrategicTopology.Edges.Single(e =>
            e.Id == fixture.Assets.First(x => x.AssetId ==
                fixture.LongerAssetIds[0]).GeographicEdgeId);
        var sourceCell = fixture.Anchors[new EconomicPointId(1UL)]
            .FineStrategicCellId;
        var intermediate = firstLongEdge.IncidentCellIds.First(x =>
            x != sourceCell);
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>(
            fixture.Anchors)
        {
            [new EconomicPointId(3UL)] =
                new PhysicalTacticalTileId(Fine, intermediate)
        };
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 3UL * Unit) },
            new[]
            {
                new EconomicDemandFlow(new EconomicPointId(2UL),
                    new CommodityId(7U), 3UL * Unit),
                new EconomicDemandFlow(new EconomicPointId(3UL),
                    new CommodityId(7U), 3UL * Unit)
            });
        var actual = new EconomicFinePriceAwareServiceResolver().Resolve(
            1UL, fixture.Graph, anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, fixture.Assets,
            new[] { Intent() }, new WholeItineraryTestPricing(),
            new EconomicFinePriceAwareSearchBudget(10_000UL, 100UL),
            searchMode: EconomicFinePriceAwareSearchMode.BlockCutCorridorPruning);
        var selection = Assert.Single(actual);
        Assert.Equal(new EconomicPointId(2UL),
            selection.Quote.RouteKey.DestinationEconomicPointId);
        Assert.Contains(fixture.LongerAssetIds[0], selection.AssetIds);
        // No declared service to point 3 => NO quote despite physical
        // passage and a matching destination commodity demand there.
    }

    [Fact]
    public void DeadEndPruningRetainsCheaperLongerPhysicalRouteAndExactQuote()
    {
        var fixture = WithBuiltSourceSpurs(MakeFixture(), 38);
        var baseline = new EconomicFinePriceAwareSearchDiagnostics();
        var pruned = new EconomicFinePriceAwareSearchDiagnostics();
        var exhaustive = Assert.Single(Run(
            fixture, new WholeItineraryTestPricing(), diagnostics: baseline));
        var optimized = Assert.Single(Run(
            fixture, new WholeItineraryTestPricing(), diagnostics: pruned,
            searchMode: EconomicFinePriceAwareSearchMode.TerminalLeafPruning));
        Assert.Equal(fixture.LongerAssetIds, optimized.AssetIds);
        Assert.Equal(exhaustive.AssetIds, optimized.AssetIds);
        Assert.Equal(0, exhaustive.Quote.RawUnitCostPerGram.CompareTo(
            optimized.Quote.RawUnitCostPerGram));
        Assert.Equal(baseline.CompletePaths, pruned.CompletePaths);
        Assert.Equal(baseline.EvaluatedFreightFunctions,
            pruned.EvaluatedFreightFunctions);
        Assert.True(pruned.PrunedPhysicalVertices > 0UL);
        Assert.True(pruned.RejectedPrunedLinks > 0UL);
        Assert.True(pruned.ExaminedLinks < baseline.ExaminedLinks);
        Assert.Equal(0UL, baseline.PrunedPhysicalVertices);
    }

    [Fact]
    public void SeededBranchClosuresAndInputPermutationsKeepFullPriceParity()
    {
        var full = WithBuiltSourceSpurs(MakeFixture(), 38);
        for (var seed = 1; seed <= 24; seed++)
        {
            var random = new Random(seed * 104729);
            var extraCount = 4 + seed;
            var selected = full.Assets.Take(
                full.Assets.Length - 38 + extraCount).ToArray();
            var closed = seed % 3 == 0
                ? new StrategicEdgeAccessSnapshot(
                    new[] { selected[^1].GeographicEdgeId })
                : StrategicEdgeAccessSnapshot.AllOpen;
            var permuted = selected.OrderBy(_ => random.Next()).ToArray();
            var baseline = new EconomicFinePriceAwareSearchDiagnostics();
            var pruning = new EconomicFinePriceAwareSearchDiagnostics();
            var expected = Assert.Single(Run(
                full, new WholeItineraryTestPricing(),
                assets: selected, access: closed,
                intents: new[] { Intent(200UL), Intent(100UL) },
                diagnostics: baseline));
            var actual = Assert.Single(Run(
                full, new WholeItineraryTestPricing(),
                assets: permuted, access: closed,
                intents: new[] { Intent(100UL), Intent(200UL) },
                diagnostics: pruning,
                searchMode: EconomicFinePriceAwareSearchMode.TerminalLeafPruning));
            Assert.Equal(100UL, actual.ServiceId);
            Assert.Equal(expected.AssetIds, actual.AssetIds);
            Assert.Equal(0, expected.Quote.RawUnitCostPerGram.CompareTo(
                actual.Quote.RawUnitCostPerGram));
            Assert.Equal(expected.Quote.ExpectedLoadMicrograms,
                actual.Quote.ExpectedLoadMicrograms);
            Assert.Equal(baseline.CompletePaths, pruning.CompletePaths);
            Assert.Equal(baseline.EvaluatedFreightFunctions,
                pruning.EvaluatedFreightFunctions);
        }
    }

    [Fact]
    public void PrunedSearchRespectsFailClosedPathBudgetAndInvalidSearchMode()
    {
        var fixture = WithBuiltSourceSpurs(MakeFixture(), 12);
        Assert.Throws<InvalidOperationException>(() => Run(
            fixture, new WholeItineraryTestPricing(),
            budget: new EconomicFinePriceAwareSearchBudget(10_000UL, 1UL),
            searchMode: EconomicFinePriceAwareSearchMode.TerminalLeafPruning));
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(
            fixture, new WholeItineraryTestPricing(),
            searchMode: (EconomicFinePriceAwareSearchMode)255));
    }

    [Fact]
    public void LongerItineraryCanWinByWholeServiceFreightCostNotLegCount()
    {
        var fixture = MakeFixture();
        var hopFactory = new WholeItineraryTestPricing();
        var hop = Assert.Single(
            new EconomicFineTransportServiceItineraryDiscovery().Discover(
                fixture.Graph, fixture.Anchors,
                StrategicEdgeAccessSnapshot.AllOpen,
                fixture.Assets, new[] { Intent() }, hopFactory));
        Assert.Equal(new[] { 100UL }, hop.AssetPath);

        var prices = new WholeItineraryTestPricing();
        var counters = new EconomicFinePriceAwareSearchDiagnostics();
        var chosen = Assert.Single(Run(fixture, prices, diagnostics: counters));
        Assert.Equal(fixture.LongerAssetIds, chosen.AssetIds);
        Assert.True(chosen.AssetIds.Count > hop.AssetPath.Count);
        Assert.Equal(2UL, counters.CompletePaths);
        Assert.Equal(2UL, counters.EvaluatedFreightFunctions);
        Assert.Equal(1UL, counters.SelectedServices);
        Assert.Equal(2, prices.Functions.Count);
        Assert.All(prices.Functions, f => Assert.Equal(1, f.Evaluations));
        Assert.Equal(Fine, chosen.FineTopologyParameters);
        Assert.Equal(2UL, chosen.TechnologyCatalogVersion);
        Assert.Equal(1UL, chosen.Quote.TurnOrdinal);
        Assert.Equal(3UL, chosen.Quote.RulesetVersion);

        var state = WorldState.CreateInitial();
        var stateHash = new CanonicalWorldStateHasher().Compute(state);
        var trades = new EconomicTurnLockedFreightMarketResolver().Resolve(
            1UL, Flows(),
            new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(new CommodityId(7U), "food", 1_000UL)
            }),
            new[] { chosen.Quote },
            new[] { new EconomicTurnCommodityValueOffer(
                new EconomicPointId(2UL), new CommodityId(7U), new MarketValue()) },
            new[] { new EconomicTurnSourceReservation(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 20_000UL) },
            new EconomicCongestionMarketSearchPolicy(Unit, 10),
            selectionMode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex);
        Assert.Equal(3, trades.Count);
        Assert.All(trades, t =>
        {
            Assert.Same(chosen.Quote, t.FreightQuote);
            Assert.Equal(0, t.RawFreightCharge.CompareTo(
                chosen.Quote.ChargeForRawQuantity(t.RawQuantity, 1_000UL)));
        });
        Assert.True(trades[0].RawMarginalCommodityUnitValueAtDecision
            .CompareTo(trades[1].RawMarginalCommodityUnitValueAtDecision) > 0);
        Assert.Equal(stateHash, new CanonicalWorldStateHasher().Compute(state));
        Assert.All(prices.Functions, f => Assert.Equal(1, f.Evaluations));

        var newQuote = Assert.Single(Run(fixture,
            new WholeItineraryTestPricing(), intents:
                new[] { Intent(forecast: 8_000_000_000UL) }, turn: 2UL));
        Assert.True(newQuote.Quote.RawUnitCostPerGram.CompareTo(
            chosen.Quote.RawUnitCostPerGram) < 0);
        Assert.All(trades, t => Assert.Same(chosen.Quote, t.FreightQuote));
    }

    [Fact]
    public void DeterministicTieBreakingAndInputOrderPreserveSelectedService()
    {
        var fixture = MakeFixture();
        var a = Assert.Single(Run(fixture,
            new WholeItineraryTestPricing(equalPrices: true),
            intents: new[] { Intent(200UL), Intent(100UL) }));
        var b = Assert.Single(Run(fixture,
            new WholeItineraryTestPricing(equalPrices: true),
            assets: fixture.Assets.Reverse(),
            intents: new[] { Intent(100UL), Intent(200UL) }));
        Assert.Equal(100UL, a.ServiceId);
        Assert.Equal(100UL, b.ServiceId);
        // Equal prices select the lexicographically smallest asset-ID path,
        // even though it has more hops than the physical shortcut.
        Assert.Equal(fixture.LongerAssetIds, a.AssetIds);
        Assert.Equal(a.AssetIds, b.AssetIds);
        Assert.Equal(0, a.Quote.RawUnitCostPerGram.CompareTo(
            b.Quote.RawUnitCostPerGram));
    }

    [Fact]
    public void SearchBudgetExhaustionThrowsRatherThanAcceptingPartialOptimum()
    {
        var fixture = MakeFixture();
        var diagnostics = new EconomicFinePriceAwareSearchDiagnostics();
        Assert.Throws<InvalidOperationException>(() =>
            Run(fixture, new WholeItineraryTestPricing(),
                new EconomicFinePriceAwareSearchBudget(10_000UL, 1UL),
                diagnostics: diagnostics));
        // Only the first candidate was accepted for evaluation; the
        // second would exceed the budget and aborts without a best route.
        Assert.Equal(1UL, diagnostics.CompletePaths);

        Assert.Throws<InvalidOperationException>(() =>
            Run(fixture, new WholeItineraryTestPricing(),
                new EconomicFinePriceAwareSearchBudget(1UL, 100UL)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicFinePriceAwareSearchBudget(0UL, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicFinePriceAwareSearchBudget(10UL, 0UL));

        // A reused diagnostics instance starts fresh even after a failed
        // attempt, and does not influence the algorithm.
        Assert.Single(Run(fixture, new WholeItineraryTestPricing(),
            diagnostics: diagnostics));
        Assert.Equal(2UL, diagnostics.CompletePaths);
        Assert.Equal(2UL, diagnostics.EvaluatedFreightFunctions);
    }

    [Fact]
    public void ClosuresUnbuiltInfrastructureAndTechnologyIdentityFailClosed()
    {
        var fixture = MakeFixture();
        var factory = new WholeItineraryTestPricing();
        var directOnly = fixture.Assets.Where(x => x.AssetId == 100UL).ToArray();
        Assert.Equal(new[] { 100UL },
            Assert.Single(Run(fixture, factory, assets: directOnly)).AssetIds);

        var withoutDirect = fixture.Assets
            .Where(x => x.AssetId != 100UL).ToArray();
        Assert.Equal(fixture.LongerAssetIds,
            Assert.Single(Run(fixture, new WholeItineraryTestPricing(),
                assets: withoutDirect)).AssetIds);

        var closed = new StrategicEdgeAccessSnapshot(new[] {
            fixture.DirectEdgeId
        });
        Assert.Equal(fixture.LongerAssetIds,
            Assert.Single(Run(fixture, new WholeItineraryTestPricing(),
                access: closed)).AssetIds);
        Assert.Empty(Run(fixture, new WholeItineraryTestPricing(),
            assets: directOnly, access: closed));

        Assert.Empty(Run(fixture, new WholeItineraryTestPricing(),
            assets: Array.Empty<EconomicTransportBuiltAsset>()));
        Assert.Empty(Run(fixture, new WholeItineraryTestPricing(),
            intents: new[] { Intent(available: false) }));
        Assert.Empty(Run(fixture, new WholeItineraryTestPricing(),
            intents: new[] { Intent(technology: 20UL) }));
        Assert.Empty(Run(fixture, new WholeItineraryTestPricing(),
            intents: new[] { Intent(catalog: 3UL) }));
    }

    [Fact]
    public void InvalidTopologyAssetsAndDuplicateServicesRejectBeforePricing()
    {
        var fixture = MakeFixture();
        var prices = new WholeItineraryTestPricing();
        var asset = fixture.Assets[0];
        Assert.Throws<ArgumentException>(() =>
            Run(fixture, prices, assets: new[] { asset, asset }));
        Assert.Throws<ArgumentException>(() =>
            Run(fixture, prices, assets:
                new[] { new EconomicTransportBuiltAsset(
                    88UL, 10UL, asset.GeographicEdgeId, 2UL) }));
        Assert.Throws<ArgumentException>(() =>
            Run(fixture, prices, assets:
                new[] { new EconomicTransportBuiltAsset(
                    88UL, 10UL,
                    new PhysicalTacticalEdgeId(
                        new GoldbergParameters(10, 0), asset.GeographicEdgeId),
                    2UL) }));
        Assert.Throws<ArgumentException>(() =>
            Run(fixture, prices, intents: new[] { Intent(), Intent() }));
        Assert.Throws<ArgumentException>(() =>
            Run(fixture, prices,
                access: new StrategicEdgeAccessSnapshot(
                    new[] { new StrategicEdgeId(
                        Fine.StrategicEdgeCount + 1UL) })));
        Assert.Empty(prices.Functions);
    }
}
