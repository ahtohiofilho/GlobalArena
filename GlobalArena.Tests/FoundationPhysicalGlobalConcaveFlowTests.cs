using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// First opt-in GLOBAL multi-seller concave-flow gate. The unit example
/// verifies a simultaneous reroute, while the stress replays the exact
/// eight competing sellers/104 real markets that blocked #397.
/// </summary>
public sealed class FoundationPhysicalGlobalConcaveFlowTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalGlobalConcaveFlowTests(ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public void GlobalResidualFlowReassignsFlexibleSellerForConstrainedSeller()
    {
        var markets = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(1_000_000UL, 1_000_000UL)
        };
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(3UL,
                new BigInteger?[] { BigInteger.Zero, BigInteger.Zero }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(3UL,
                new BigInteger?[] { BigInteger.Zero, null })
        };
        var result = FoundationPhysicalConcaveCommodityFlow.Solve(
            sellers, markets, goodsQuantum: 1UL,
            segmentsPerAverageMarket: 3, maxAugmentations: 32);
        Assert.Equal(new ulong[] { 3UL, 3UL }, result.MarketQuanta);
        Assert.Equal(new ulong[] { 0UL, 3UL }, result.AllocationQuanta[0]);
        Assert.Equal(new ulong[] { 3UL, 0UL }, result.AllocationQuanta[1]);
        Assert.True(result.IntegratedNetPotentialScaled >=
            result.SegmentedNetPotentialScaled);
    }


    [Fact]
    public void IndexedHeapMatchesOriginalResidualQueueOnReassignmentsTiesAndBudgets()
    {
        // Reverse-arc reassignment and zero-cost ties are adversarial to a
        // strict predecessor tie order; compare every output including the
        // exact number of residual paths. No hidden tolerance is allowed.
        var buyers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                3UL, new BigInteger?[]
                    { BigInteger.Zero, BigInteger.Zero, BigInteger.Zero }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                3UL, new BigInteger?[]
                    { BigInteger.Zero, null, BigInteger.Zero }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                3UL, new BigInteger?[]
                    { BigInteger.Zero, BigInteger.Zero, null })
        };
        foreach (bool reuse in new[] { true, false })
        foreach (int segments in new[] { 1, 3, 9 })
        {
            var original = FoundationPhysicalConcaveCommodityFlow.Solve(
                sellers, buyers, goodsQuantum: 1UL,
                segmentsPerAverageMarket: segments,
                maxAugmentations: 128, reuseSearchWorkspace: reuse,
                useIndexedHeap: false);
            var optimized = FoundationPhysicalConcaveCommodityFlow.Solve(
                sellers, buyers, goodsQuantum: 1UL,
                segmentsPerAverageMarket: segments,
                maxAugmentations: 128, reuseSearchWorkspace: reuse,
                useIndexedHeap: true);
            Assert.Equal(original.Augmentations, optimized.Augmentations);
            Assert.Equal(original.MarketQuanta, optimized.MarketQuanta);
            Assert.Equal(original.SegmentedNetPotentialScaled,
                optimized.SegmentedNetPotentialScaled);
            Assert.Equal(original.IntegratedNetPotentialScaled,
                optimized.IntegratedNetPotentialScaled);
            for (int i = 0; i < sellers.Length; i++)
                Assert.Equal(original.AllocationQuanta[i],
                    optimized.AllocationQuanta[i]);
            Assert.Equal(9UL,
                optimized.MarketQuanta.Aggregate(0UL, (sum, value) =>
                    checked(sum + value)));
        }

        var oneMarket = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        var oneSeller = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                100UL, new BigInteger?[] { BigInteger.Zero })
        };
        foreach (bool indexed in new[] { true, false })
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    oneSeller, oneMarket, 1UL, 5, 1,
                    reuseSearchWorkspace: true,
                    useIndexedHeap: indexed));
            Assert.Contains("augmentation budget", error.Message);

            var unreachable = Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    new[] { new FoundationPhysicalConcaveCommodityFlow.Seller(
                        1UL, new BigInteger?[] { null }) },
                    oneMarket, 1UL, 1, 32,
                    reuseSearchWorkspace: true,
                    useIndexedHeap: indexed));
            Assert.Contains("reachable market", unreachable.Message);
        }
    }


    [Fact]
    public void ShiftedLabelsMatchExactOriginalWithLargeBigIntegerCostsAndResidualTies()
    {
        // Costs much larger than Int128 prevent any accidental narrowing.
        // Shared zero-cost ties and asymmetric reachability can exercise
        // reverse residual arcs and canonical (distance, node) order.
        BigInteger huge = (BigInteger.One << 240) + 37;
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(4UL,
                new BigInteger?[] { huge, huge + 1, huge }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(3UL,
                new BigInteger?[] { huge, null, huge }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(2UL,
                new BigInteger?[] { null, huge, huge + 2 })
        };
        var markets = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        foreach (bool indexed in new[] { false, true })
        foreach (bool reuse in new[] { false, true })
        foreach (int segments in new[] { 1, 3, 9 })
        {
            var old = FoundationPhysicalConcaveCommodityFlow.Solve(
                sellers, markets, goodsQuantum: 1UL,
                segmentsPerAverageMarket: segments,
                maxAugmentations: 256,
                reuseSearchWorkspace: reuse,
                useIndexedHeap: indexed,
                useShiftedLabels: false);
            var shifted = FoundationPhysicalConcaveCommodityFlow.Solve(
                sellers, markets, goodsQuantum: 1UL,
                segmentsPerAverageMarket: segments,
                maxAugmentations: 256,
                reuseSearchWorkspace: reuse,
                useIndexedHeap: indexed,
                useShiftedLabels: true);
            Assert.Equal(old.Augmentations, shifted.Augmentations);
            Assert.Equal(old.MarketQuanta, shifted.MarketQuanta);
            Assert.Equal(old.SegmentedNetPotentialScaled,
                shifted.SegmentedNetPotentialScaled);
            Assert.Equal(old.IntegratedNetPotentialScaled,
                shifted.IntegratedNetPotentialScaled);
            for (int i = 0; i < sellers.Length; i++)
                Assert.Equal(old.AllocationQuanta[i],
                    shifted.AllocationQuanta[i]);
            Assert.Equal(9UL, shifted.MarketQuanta.Aggregate(0UL,
                (sum, n) => checked(sum + n)));
        }

        var oneMarket = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        var oneSeller = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                100UL, new BigInteger?[] { huge })
        };
        foreach (bool shifted in new[] { false, true })
        foreach (bool indexed in new[] { false, true })
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    oneSeller, oneMarket, 1UL, 5, 1,
                    reuseSearchWorkspace: true,
                    useIndexedHeap: indexed,
                    useShiftedLabels: shifted));
            Assert.Contains("augmentation budget", error.Message);
            Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    new[] { new FoundationPhysicalConcaveCommodityFlow.Seller(
                        1UL, new BigInteger?[] { null }) },
                    oneMarket, 1UL, 1, 16,
                    reuseSearchWorkspace: true,
                    useIndexedHeap: indexed,
                    useShiftedLabels: shifted));
        }
    }

    [Fact]
    public void ResidualDominanceMatchesShiftedReferenceOn240BitFreightAndAllHeapModes()
    {
        // Both A/B paths retain shifted exact BigInteger labels. Test the
        // provable nonnegative-cost dominance check on 240-bit freight,
        // reverse residual arcs, ties, both heaps and both workspaces.
        BigInteger huge = (BigInteger.One << 240) + 37;
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(4UL,
                new BigInteger?[] { huge, huge + 1, huge }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(3UL,
                new BigInteger?[] { huge, null, huge }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(2UL,
                new BigInteger?[] { null, huge, huge + 2 })
        };
        var markets = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        foreach (bool indexed in new[] { false, true })
        foreach (bool reuse in new[] { false, true })
        foreach (int segments in new[] { 1, 3, 9 })
        {
            var old = FoundationPhysicalConcaveCommodityFlow.Solve(
                sellers, markets, goodsQuantum: 1UL,
                segmentsPerAverageMarket: segments,
                maxAugmentations: 256,
                reuseSearchWorkspace: reuse,
                useIndexedHeap: indexed,
                useShiftedLabels: true,
                useResidualDominanceFastPath: false);
            var dominant = FoundationPhysicalConcaveCommodityFlow.Solve(
                sellers, markets, goodsQuantum: 1UL,
                segmentsPerAverageMarket: segments,
                maxAugmentations: 256,
                reuseSearchWorkspace: reuse,
                useIndexedHeap: indexed,
                useShiftedLabels: true,
                useResidualDominanceFastPath: true);
            Assert.Equal(old.Augmentations, dominant.Augmentations);
            Assert.Equal(old.MarketQuanta, dominant.MarketQuanta);
            Assert.Equal(old.SegmentedNetPotentialScaled,
                dominant.SegmentedNetPotentialScaled);
            Assert.Equal(old.IntegratedNetPotentialScaled,
                dominant.IntegratedNetPotentialScaled);
            for (int i = 0; i < sellers.Length; i++)
                Assert.Equal(old.AllocationQuanta[i],
                    dominant.AllocationQuanta[i]);
            Assert.Equal(9UL, dominant.MarketQuanta.Aggregate(0UL,
                (sum, n) => checked(sum + n)));
        }

        var oneMarket = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        var oneSeller = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                100UL, new BigInteger?[] { huge })
        };
        foreach (bool dominance in new[] { false, true })
        foreach (bool indexed in new[] { false, true })
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    oneSeller, oneMarket, 1UL, 5, 1,
                    reuseSearchWorkspace: true,
                    useIndexedHeap: indexed,
                    useShiftedLabels: true,
                    useResidualDominanceFastPath: dominance));
            Assert.Contains("augmentation budget", error.Message);
            Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    new[] { new FoundationPhysicalConcaveCommodityFlow.Seller(
                        1UL, new BigInteger?[] { null }) },
                    oneMarket, 1UL, 1, 16,
                    reuseSearchWorkspace: true,
                    useIndexedHeap: indexed,
                    useShiftedLabels: true,
                    useResidualDominanceFastPath: dominance));
        }
    }

    [Fact]
    public void SingleMarketWholeSegmentsHaveExactIntegratedPotential()
    {
        var result = FoundationPhysicalConcaveCommodityFlow.Solve(
            new[] { new FoundationPhysicalConcaveCommodityFlow.Seller(
                100UL, new BigInteger?[] { BigInteger.Zero }) },
            new[] { new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL) }, goodsQuantum: 1UL,
            segmentsPerAverageMarket: 5, maxAugmentations: 8);
        Assert.Equal(100UL, result.AllocationQuanta[0][0]);
        Assert.Equal(100UL, result.MarketQuanta[0]);
        Assert.Equal(result.IntegratedNetPotentialScaled,
            result.SegmentedNetPotentialScaled);
        Assert.Equal(5, result.Augmentations);
    }

    [Fact]
    public void InsufficientGlobalAugmentationBudgetFailsClosed()
    {
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(100UL,
                new BigInteger?[] { BigInteger.Zero })
        };
        var markets = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(1_000_000UL, 1_000_000UL)
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalConcaveCommodityFlow.Solve(sellers, markets,
                goodsQuantum: 1UL, segmentsPerAverageMarket: 5,
                maxAugmentations: 1));
        Assert.Contains("augmentation budget", error.Message);
    }

    [Fact]
    public void UnreachableSourceOrInvalidFreightIsRejected()
    {
        var markets = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(1UL, 1UL)
        };
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalConcaveCommodityFlow.Solve(
                new[] { new FoundationPhysicalConcaveCommodityFlow.Seller(
                    1UL, new BigInteger?[] { null }) },
                markets, 1UL, 1, 10));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalConcaveCommodityFlow.Solve(
                new[] { new FoundationPhysicalConcaveCommodityFlow.Seller(
                    1UL, new BigInteger?[] { -BigInteger.One }) },
                markets, 1UL, 1, 10));
    }

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(
                profile => new FoundationActivityCommodityDemandCoefficient(
                    profile, Grain, 1_000_000UL)));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("concave-flow-shared-cargo-micro", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
        });

    private sealed record Case(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationCommodityMarketPlanningQuote[] Quotes,
        FoundationPhysicalRoutingEdgeCost[] Roads);

    private static Case EightCompetingSellersInConnectedGoldbergMarkets()
    {
        const int cells = 112;
        const int sellers = 8;
        const int nonSellers = 96;
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var seed = map.FineTopology.Cells[0].Id;
        var queue = new Queue<StrategicCellId>();
        var discovered = new HashSet<StrategicCellId> { seed };
        var component = new List<StrategicCellId>();
        queue.Enqueue(seed);
        while (queue.Count > 0 && component.Count < cells)
        {
            var id = queue.Dequeue();
            component.Add(id);
            foreach (var adjacent in map.FineTopology.Cells[
                         checked((int)id.Value - 1)].AdjacentCellIds
                         .OrderBy(next => next.Value))
                if (discovered.Add(adjacent))
                    queue.Enqueue(adjacent);
        }
        Assert.Equal(cells, component.Count);
        var chosen = component.ToHashSet();
        var borders = map.FineTopology.Edges.Where(edge =>
            chosen.Contains(edge.IncidentCellIds[0]) &&
            chosen.Contains(edge.IncidentCellIds[1])).ToArray();
        Assert.True(borders.Length > cells);

        PhysicalTacticalTileId tile(StrategicCellId id) =>
            new(map.Refinement.FineParameters, id);
        StrategicCellId coarse(StrategicCellId id) =>
            map.TileIncidences[checked((int)id.Value - 1)]
                .IncidentCoarseCellIds[0];

        var people = new List<PhysicalWorkerRuntimeRecord>();
        for (int i = 0; i < sellers; i++)
            people.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)i + 1UL),
                Red, coarse(component[i]), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tile(component[i]), PhysicalWorkerOccupation.Agriculture));
        for (int i = 0; i < nonSellers; i++)
        {
            var location = component[cells - nonSellers + i];
            people.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(sellers + i + 1)),
                Blue, coarse(location), Grain, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                tile(location), PhysicalWorkerOccupation.Unassigned));
        }
        var census = new PhysicalWorkerRuntimeState(people,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(census, Demand()).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Owner == Blue ? 1_000_000_000UL :
                        1_000_000UL)).ToArray();
        var roads = borders.Select(border =>
            new FoundationPhysicalRoutingEdgeCost(
                new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                    border.Id),
                FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, 100_000UL, 0UL)).ToArray();
        return new Case(host, census, quotes, roads);
    }

    [Fact]
    public void SharedGrainStressReturnsAuditedGlobalFlowWithoutSellerBestResponsePasses()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var snapshot = stress.Host.Snapshot;
        var hash = stress.Host.StateHash;
        var watch = Stopwatch.StartNew();
        var flow = FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate(
            stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
            stress.Quotes, stress.Roads, segmentsPerAverageMarket: 4,
            maxFlowAugmentations: 4096);
        watch.Stop();

        _output.WriteLine(
            $"Global concave flow: sellers={flow.ProductiveLotCount}, " +
            $"markets={flow.RealMarketCount}, " +
            $"roads={stress.Roads.Length}, " +
            $"augmentations={flow.FlowAugmentations}, " +
            $"segmentationUnderstatement={flow.SegmentationUnderstatementScaled}, " +
            $"milliseconds={watch.ElapsedMilliseconds}");
        Assert.Equal(8, flow.ProductiveLotCount);
        Assert.Equal(104, flow.RealMarketCount);
        Assert.InRange(flow.FlowAugmentations, 1, 4096);
        Assert.Equal(8_000_000UL,
            flow.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(flow.AllocationAudit.TotalAvailableGoodsMicro,
            flow.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(8_000_000UL,
            flow.MarketTotals.Aggregate(0UL,
                (sum, line) => checked(sum + line.AllocatedGoodsMicro)));
        Assert.True(flow.Candidates.Select(x => x.BuyerTile).Distinct().Count() > 1);
        Assert.True(flow.SegmentationUnderstatementScaled >= BigInteger.Zero);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(hash, stress.Host.StateHash);
        Assert.Same(snapshot, stress.Host.Snapshot);

        var repeated = FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate(
            stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
            stress.Quotes.Reverse(), stress.Roads,
            segmentsPerAverageMarket: 4, maxFlowAugmentations: 4096);
        Assert.Equal(
            flow.Candidates.Select(x => (x.DeliveryOrdinal, x.SourceTile,
                x.Seller, x.BuyerTile, x.Buyer, x.Commodity, x.GoodsMicro,
                Route: string.Join(",", x.RouteTiles.Select(t =>
                    t.FineStrategicCellId.Value)))),
            repeated.Candidates.Select(x => (x.DeliveryOrdinal, x.SourceTile,
                x.Seller, x.BuyerTile, x.Buyer, x.Commodity, x.GoodsMicro,
                Route: string.Join(",", x.RouteTiles.Select(t =>
                    t.FineStrategicCellId.Value)))));
        Assert.Equal(flow.SegmentedNetPotentialScaled,
            repeated.SegmentedNetPotentialScaled);
    }

    [Fact]
    public void ReusedResidualWorkspaceMatchesOriginalWorkerBackedGoldbergFlowBitForBit()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var snapshot = stress.Host.Snapshot;
        var stateHash = stress.Host.StateHash;
        foreach (int segments in new[] { 2, 4, 8 })
        {
            long baselineBefore = GC.GetAllocatedBytesForCurrentThread();
            var baselineClock = Stopwatch.StartNew();
            var reference =
                FoundationPhysicalGlobalConcaveFlowPlanner
                    .CoordinateWithSearchWorkspace(
                        stress.Workers, stress.Host.Incidence,
                        Demand(), Cargo(), stress.Quotes, stress.Roads,
                        segments, 8192, reuseSearchWorkspace: false);
            baselineClock.Stop();
            long baselineBytes = GC.GetAllocatedBytesForCurrentThread() -
                baselineBefore;

            long reusedBefore = GC.GetAllocatedBytesForCurrentThread();
            var reusedClock = Stopwatch.StartNew();
            var optimized =
                FoundationPhysicalGlobalConcaveFlowPlanner
                    .CoordinateWithSearchWorkspace(
                        stress.Workers, stress.Host.Incidence,
                        Demand(), Cargo(), stress.Quotes, stress.Roads,
                        segments, 8192, reuseSearchWorkspace: true);
            reusedClock.Stop();
            long reusedBytes = GC.GetAllocatedBytesForCurrentThread() -
                reusedBefore;

            Assert.Equal(reference.ProductiveLotCount,
                optimized.ProductiveLotCount);
            Assert.Equal(reference.RealMarketCount, optimized.RealMarketCount);
            Assert.Equal(reference.FlowAugmentations, optimized.FlowAugmentations);
            Assert.Equal(reference.SegmentedNetPotentialScaled,
                optimized.SegmentedNetPotentialScaled);
            Assert.Equal(reference.IntegratedNetPotentialScaled,
                optimized.IntegratedNetPotentialScaled);
            Assert.Equal(reference.SegmentationUnderstatementScaled,
                optimized.SegmentationUnderstatementScaled);
            Assert.Equal(reference.MarketTotals.Select(m =>
                    (m.Tile, m.Buyer, m.Commodity, m.DemandMicro,
                        m.AllocatedGoodsMicro)),
                optimized.MarketTotals.Select(m =>
                    (m.Tile, m.Buyer, m.Commodity, m.DemandMicro,
                        m.AllocatedGoodsMicro)));
            Assert.Equal(reference.Candidates.Select(d =>
                    (d.DeliveryOrdinal, d.Seller, d.SourceOccupation,
                        d.SourceTile, d.Buyer, d.BuyerTile, d.Commodity,
                        d.GoodsMicro, Route: string.Join(",", d.RouteTiles
                            .Select(t => t.FineStrategicCellId.Value)))),
                optimized.Candidates.Select(d =>
                    (d.DeliveryOrdinal, d.Seller, d.SourceOccupation,
                        d.SourceTile, d.Buyer, d.BuyerTile, d.Commodity,
                        d.GoodsMicro, Route: string.Join(",", d.RouteTiles
                            .Select(t => t.FineStrategicCellId.Value)))));
            Assert.Equal(reference.AllocationAudit.TotalAvailableGoodsMicro,
                optimized.AllocationAudit.TotalAvailableGoodsMicro);
            Assert.Equal(reference.AllocationAudit.TotalAllocatedGoodsMicro,
                optimized.AllocationAudit.TotalAllocatedGoodsMicro);
            Assert.Equal(reference.AllocationAudit.Freight
                    .TotalEdgeTraversalCargoMicro,
                optimized.AllocationAudit.Freight
                    .TotalEdgeTraversalCargoMicro);
            Assert.Equal(8_000_000UL,
                optimized.AllocationAudit.TotalAllocatedGoodsMicro);
            Assert.Equal(stateHash, stress.Host.StateHash);
            Assert.Same(snapshot, stress.Host.Snapshot);
            Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
            _output.WriteLine(
                $"GA_NEXT_RESIDUAL_WORKSPACE segments={segments} " +
                $"referenceMs={baselineClock.ElapsedMilliseconds} " +
                $"reuseMs={reusedClock.ElapsedMilliseconds} " +
                $"referenceManagedAllocatedBytes={baselineBytes} " +
                $"reuseManagedAllocatedBytes={reusedBytes} " +
                $"augmentations={optimized.FlowAugmentations}; EXACT");
            var diagnosticDirectory = Environment.GetEnvironmentVariable(
                "GA_RESIDUAL_WORKSPACE_REPORT_DIR");
            if (!string.IsNullOrWhiteSpace(diagnosticDirectory))
            {
                // Write ONLY AFTER all exact market, seller, routing,
                // conservation and uncommitted-state comparisons passed.
                // No shipment identities or price/treasury state are exported.
                Directory.CreateDirectory(diagnosticDirectory);
                var observation = new
                {
                    Schema = "ga-next.residual-workspace.v1",
                    SegmentsPerAverageMarket = segments,
                    SellerCount = optimized.ProductiveLotCount,
                    MarketCount = optimized.RealMarketCount,
                    TotalAvailableGoodsMicro =
                        optimized.AllocationAudit.TotalAvailableGoodsMicro,
                    TotalAllocatedGoodsMicro =
                        optimized.AllocationAudit.TotalAllocatedGoodsMicro,
                    Augmentations = optimized.FlowAugmentations,
                    ReferenceElapsedMilliseconds =
                        baselineClock.ElapsedMilliseconds,
                    ReusedElapsedMilliseconds =
                        reusedClock.ElapsedMilliseconds,
                    ReferenceAllocatedBytesCurrentThread = baselineBytes,
                    ReusedAllocatedBytesCurrentThread = reusedBytes,
                    ExactlyEquivalent = true,
                    CandidateSettledEconomically = false
                };
                File.WriteAllText(
                    Path.Combine(diagnosticDirectory,
                        $"segments-{segments}.json"),
                    JsonSerializer.Serialize(observation,
                        new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    [Fact]
    public void ReusedResidualQueueMatchesReferenceOnConstrainedSellerReassignment()
    {
        var buyers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                3UL, new BigInteger?[]
                    { BigInteger.Zero, BigInteger.Zero }),
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                3UL, new BigInteger?[] { BigInteger.Zero, null })
        };
        var reference = FoundationPhysicalConcaveCommodityFlow.Solve(
            sellers, buyers, 1UL, 3, 32,
            reuseSearchWorkspace: false);
        var reused = FoundationPhysicalConcaveCommodityFlow.Solve(
            sellers, buyers, 1UL, 3, 32,
            reuseSearchWorkspace: true);
        Assert.Equal(reference.Augmentations, reused.Augmentations);
        Assert.Equal(reference.MarketQuanta, reused.MarketQuanta);
        for (int i = 0; i < sellers.Length; i++)
            Assert.Equal(reference.AllocationQuanta[i],
                reused.AllocationQuanta[i]);
        Assert.Equal(reference.IntegratedNetPotentialScaled,
            reused.IntegratedNetPotentialScaled);
        Assert.Equal(reference.SegmentedNetPotentialScaled,
            reused.SegmentedNetPotentialScaled);
        Assert.Equal(new ulong[] { 0UL, 3UL },
            reused.AllocationQuanta[0]);
        Assert.Equal(new ulong[] { 3UL, 0UL },
            reused.AllocationQuanta[1]);
    }

    [Fact]
    public void ResidualWorkspaceAndReferenceFailClosedOnIdenticalAugmentationBudget()
    {
        var sellers = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Seller(
                100UL, new BigInteger?[] { BigInteger.Zero }),
        };
        var markets = new[]
        {
            new FoundationPhysicalConcaveCommodityFlow.Market(
                1_000_000UL, 1_000_000UL)
        };
        foreach (bool reuse in new[] { false, true })
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                FoundationPhysicalConcaveCommodityFlow.Solve(
                    sellers, markets, goodsQuantum: 1UL,
                    segmentsPerAverageMarket: 5, maxAugmentations: 1,
                    reuseSearchWorkspace: reuse));
            Assert.Contains("augmentation budget", exception.Message);
        }
    }

    [Fact]
    public void SameCommodityPrecisionSweepReportsSegmentedUnderstatementWithoutClaimingExactOptimum()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var snapshot = stress.Host.Snapshot;
        var hash = stress.Host.StateHash;
        var results = new List<FoundationPhysicalGlobalConcaveFlowPlan>();
        foreach (int segments in new[] { 2, 4, 8 })
        {
            var watch = Stopwatch.StartNew();
            var plan = FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, segmentsPerAverageMarket: segments,
                maxFlowAugmentations: 8192);
            watch.Stop();
            _output.WriteLine(
                $"precisionSweep segments={segments}, " +
                $"understatement={plan.SegmentationUnderstatementScaled}, " +
                $"integratedPotential={plan.IntegratedNetPotentialScaled}, " +
                $"segmentedPotential={plan.SegmentedNetPotentialScaled}, " +
                $"augmentations={plan.FlowAugmentations}, " +
                $"milliseconds={watch.ElapsedMilliseconds}");
            Assert.Equal(8_000_000UL,
                plan.AllocationAudit.TotalAllocatedGoodsMicro);
            Assert.Equal(plan.AllocationAudit.TotalAvailableGoodsMicro,
                plan.AllocationAudit.TotalAllocatedGoodsMicro);
            Assert.True(plan.SegmentationUnderstatementScaled >=
                BigInteger.Zero);
            Assert.Equal(104, plan.RealMarketCount);
            results.Add(plan);
        }
        // Different segmentation boundaries can yield different optima.
        // No false assertion of monotonic continuous utility or global gap.
        Assert.Equal(3, results.Count);
        Assert.Equal(hash, stress.Host.StateHash);
        Assert.Same(snapshot, stress.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void SameGrainStressOscillationIsDetectedAndCannotBePublishedAsEquilibrium()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var hash = stress.Host.StateHash;
        var snapshot = stress.Host.Snapshot;
        var scales = stress.Roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(
                road.Edge, 100_000_000UL)).ToArray();
        var watch = Stopwatch.StartNew();
        var error = Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalGlobalConcaveCongestionFeedback.Coordinate(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, scales,
                segmentsPerAverageMarket: 4,
                maxFlowAugmentationsPerSolve: 4096,
                maxFeedbackIterations: 16));
        watch.Stop();
        Assert.Contains("oscillated", error.Message);
        _output.WriteLine(
            $"sameGrainCongestion: 8 sellers / 104 markets; " +
            $"documented exact-rate-vector oscillation, " +
            $"milliseconds={watch.ElapsedMilliseconds}; " +
            $"NO fixed point or settled sale certified");
        Assert.Equal(hash, stress.Host.StateHash);
        Assert.Same(snapshot, stress.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void StableConnectedTwoMarketCorridorCertifiesExactAuditAndRateEquality()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var border = map.FineTopology.Edges[0];
        var tileA = new PhysicalTacticalTileId(
            map.Refinement.FineParameters, border.IncidentCellIds[0]);
        var tileB = new PhysicalTacticalTileId(
            map.Refinement.FineParameters, border.IncidentCellIds[1]);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[
                checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];

        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, coarse(tileA), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tileA, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, coarse(tileB), Grain, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                tileB, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);

        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(workers, Demand()).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Owner == Blue ? 1_000_000_000UL :
                        1_000_000UL)).ToArray();
        var road = new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                border.Id),
            FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, 100_000UL, 0UL);
        var scale = new FoundationPhysicalEdgeCarryingScale(
            road.Edge, 100_000_000UL);
        var hash = host.StateHash;
        var snapshot = host.Snapshot;
        var plan = FoundationPhysicalGlobalConcaveCongestionFeedback.Coordinate(
            workers, map, Demand(), Cargo(), quotes,
            new[] { road }, new[] { scale },
            segmentsPerAverageMarket: 4,
            maxFlowAugmentationsPerSolve: 128,
            maxFeedbackIterations: 6);
        Assert.True(plan.ExactCostAndLoadAgreement);
        Assert.Equal(2, plan.FeedbackIterations);
        Assert.Equal(2, plan.Distribution.RealMarketCount);
        Assert.Equal(1_000_000UL,
            plan.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Single(plan.Edges);
        Assert.Single(plan.ConsistentEdgeCosts);
        var edge = plan.Edges[0];
        Assert.True(edge.CargoMicro > 0UL);
        Assert.Equal(
            FoundationPhysicalEndogenousCongestionFeedback
                .CalculateCongestedRate(
                    edge.FreePhysicalCostMicroPerWholeCargoUnit,
                    edge.CargoMicro, edge.CargoMicroAtReferenceLoad),
            edge.CongestedPhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(edge.CongestedPhysicalCostMicroPerWholeCargoUnit,
            plan.ConsistentEdgeCosts[0]
                .PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(edge.CargoMicro,
            plan.Distribution.AllocationAudit.Freight
                .TotalEdgeTraversalCargoMicro);
        Assert.Equal(hash, host.StateHash);
        Assert.Same(snapshot, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void GlobalConcaveCongestionBudgetAndMissingCarryingScaleFailClosed()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var snapshot = stress.Host.Snapshot;
        var hash = stress.Host.StateHash;
        var scales = stress.Roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(
                road.Edge, 100_000_000UL)).ToArray();
        var error = Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalGlobalConcaveCongestionFeedback.Coordinate(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, scales,
                segmentsPerAverageMarket: 4,
                maxFlowAugmentationsPerSolve: 4096,
                maxFeedbackIterations: 1));
        Assert.Contains("iteration budget", error.Message);

        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalGlobalConcaveCongestionFeedback.Coordinate(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, scales.Skip(1),
                segmentsPerAverageMarket: 4,
                maxFlowAugmentationsPerSolve: 4096,
                maxFeedbackIterations: 8));
        Assert.Equal(hash, stress.Host.StateHash);
        Assert.Same(snapshot, stress.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }


    [Fact]
    public void GlobalConcaveFlowCanSplitOneRealDeliveryAcrossTwoGoldbergCorridorsWithStrictGain()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var topology = map.FineTopology.Cells;
        var origin = topology[0].Id;
        var neighbors = topology[0].AdjacentCellIds.ToHashSet();
        var choice = topology
            .Where(cell => cell.Id != origin)
            .Select(cell => new
            {
                Target = cell.Id,
                Intermediate = cell.AdjacentCellIds
                    .Where(neighbors.Contains)
                    .OrderBy(x => x.Value).Take(2).ToArray()
            })
            .First(x => x.Intermediate.Length == 2 &&
                !neighbors.Contains(x.Target));
        var crossingPairs = new HashSet<(ulong, ulong)>();
        void pair(StrategicCellId x, StrategicCellId y) =>
            crossingPairs.Add((Math.Min(x.Value, y.Value),
                Math.Max(x.Value, y.Value)));
        foreach (var middle in choice.Intermediate)
        {
            pair(origin, middle);
            pair(middle, choice.Target);
        }
        var edges = map.FineTopology.Edges
            .Where(edge => crossingPairs.Contains((
                Math.Min(edge.IncidentCellIds[0].Value,
                    edge.IncidentCellIds[1].Value),
                Math.Max(edge.IncidentCellIds[0].Value,
                    edge.IncidentCellIds[1].Value)))).ToArray();
        Assert.Equal(4, edges.Length);
        PhysicalTacticalTileId tile(StrategicCellId id) =>
            new(map.Refinement.FineParameters, id);
        StrategicCellId coarse(StrategicCellId id) =>
            map.TileIncidences[checked((int)id.Value - 1)]
                .IncidentCoarseCellIds[0];

        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, coarse(origin), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tile(origin), PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, coarse(choice.Target), Grain, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                tile(choice.Target), PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(census, Demand()).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Owner == Blue ? 1_000_000_000UL :
                        1_000_000UL)).ToArray();
        var roads = edges.Select(edge =>
            new FoundationPhysicalRoutingEdgeCost(
                new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                    edge.Id),
                FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, 100_000UL, 0UL)).ToArray();
        var scales = roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(road.Edge, 100_000UL))
            .ToArray();
        var hash = host.StateHash;
        var snapshot = host.Snapshot;
        var plan = FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(
            census, map, Demand(), Cargo(), quotes, roads, scales,
            segmentsPerAverageMarket: 4, maxFlowAugmentations: 64,
            maxRouteBatchesPerParent: 8, maxTotalRouteBatches: 32);

        Assert.True(plan.AppliedStrictImprovement);
        Assert.True(plan.FinalIntegratedFreightCost <
            plan.BaselineIntegratedFreightCost);
        Assert.Equal(1_000_000UL,
            plan.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(plan.MarketAllocation.AllocationAudit.TotalAllocatedGoodsMicro,
            plan.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.True(plan.Parts.Where(p => p.Delivery.BuyerTile ==
                tile(choice.Target)).Select(p => string.Join(",",
                    p.Delivery.RouteTiles.Select(t =>
                        t.FineStrategicCellId.Value))).Distinct().Count() >= 2);
        Assert.Equal(plan.Parts.Count, plan.AllocationAudit.DeliveryCount);
        Assert.Equal(hash, host.StateHash);
        Assert.Same(snapshot, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void GlobalEightProducerMultipathConservesEveryParentAndNeverIncreasesIntegratedCost()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var scales = stress.Roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(
                road.Edge, 1_000_000UL)).ToArray();
        var snapshot = stress.Host.Snapshot;
        var hash = stress.Host.StateHash;
        var watch = Stopwatch.StartNew();
        var plan = FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(
            stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
            stress.Quotes, stress.Roads, scales,
            segmentsPerAverageMarket: 4, maxFlowAugmentations: 4096,
            maxRouteBatchesPerParent: 2, maxTotalRouteBatches: 4096);
        watch.Stop();
        _output.WriteLine(
            $"globalMultipath sellers={plan.MarketAllocation.ProductiveLotCount}, " +
            $"markets={plan.MarketAllocation.RealMarketCount}, " +
            $"parents={plan.MarketAllocation.Candidates.Count}, " +
            $"parts={plan.Parts.Count}, batches={plan.RouteBatchDecisions}, " +
            $"accepted={plan.AppliedStrictImprovement}, " +
            $"initialIntegratedCost={plan.BaselineIntegratedFreightCost}, " +
            $"finalIntegratedCost={plan.FinalIntegratedFreightCost}, " +
            $"milliseconds={watch.ElapsedMilliseconds}");
        Assert.Equal(8, plan.MarketAllocation.ProductiveLotCount);
        Assert.Equal(104, plan.MarketAllocation.RealMarketCount);
        Assert.Equal(8_000_000UL,
            plan.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(plan.MarketAllocation.AllocationAudit.TotalAllocatedGoodsMicro,
            plan.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.True(plan.FinalIntegratedFreightCost <=
            plan.BaselineIntegratedFreightCost);
        Assert.Equal(stress.Roads.Length, plan.ProjectedEdges.Count);
        var totals = plan.Parts.GroupBy(p => p.ParentDeliveryOrdinal)
            .ToDictionary(g => g.Key, g => g.Aggregate(
                0UL, (sum, x) => checked(sum + x.Delivery.GoodsMicro)));
        foreach (var parent in plan.MarketAllocation.Candidates)
            Assert.Equal(parent.GoodsMicro, totals[parent.DeliveryOrdinal]);
        Assert.Equal(
            plan.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro,
            plan.ProjectedEdges.Aggregate(BigInteger.Zero,
                (sum, edge) => sum + edge.CargoMicro));
        Assert.Equal(hash, stress.Host.StateHash);
        Assert.Same(snapshot, stress.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void GlobalMultipathRouteBatchBudgetFailsClosedInsteadOfTruncatingGoods()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var scales = stress.Roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(
                road.Edge, 1_000_000UL)).ToArray();
        var snapshot = stress.Host.Snapshot;
        var hash = stress.Host.StateHash;
        var error = Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, scales,
                segmentsPerAverageMarket: 4, maxFlowAugmentations: 4096,
                maxRouteBatchesPerParent: 2, maxTotalRouteBatches: 1));
        Assert.Contains("batch budget", error.Message);
        Assert.Equal(hash, stress.Host.StateHash);
        Assert.Same(snapshot, stress.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }


    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void UnusedSequentialSeedElisionMatchesFullReferenceOn104GoldbergMarkets(
        int segmentsPerMarket)
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var policy = Demand();
        var cargo = Cargo();
        var initialHash = stress.Host.StateHash;
        var initialSnapshot = stress.Host.Snapshot;

        (FoundationPhysicalGlobalConcaveFlowPlan Plan, double ElapsedMs,
            long AllocatedBytes) Measure(bool oldSequentialSeed)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            var result = FoundationPhysicalGlobalConcaveFlowPlanner
                .CoordinateWithSearchWorkspace(
                    stress.Workers, stress.Host.Incidence, policy, cargo,
                    stress.Quotes, stress.Roads, segmentsPerMarket, 8192,
                    reuseSearchWorkspace: true,
                    useSequentialSeedReference: oldSequentialSeed);
            double milliseconds =
                Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(double.IsFinite(milliseconds) && milliseconds >= 0);
            Assert.True(bytes >= 0);
            return (result, milliseconds, bytes);
        }

        var reference = Measure(oldSequentialSeed: true);
        var optimized = Measure(oldSequentialSeed: false);
        var original = reference.Plan;
        var current = optimized.Plan;

        Assert.Equal(original.ProductiveLotCount, current.ProductiveLotCount);
        Assert.Equal(original.RealMarketCount, current.RealMarketCount);
        Assert.Equal(original.FlowAugmentations, current.FlowAugmentations);
        Assert.Equal(original.SegmentsPerAverageMarket,
            current.SegmentsPerAverageMarket);
        Assert.Equal(original.SegmentedNetPotentialScaled,
            current.SegmentedNetPotentialScaled);
        Assert.Equal(original.IntegratedNetPotentialScaled,
            current.IntegratedNetPotentialScaled);
        Assert.Equal(original.SegmentationUnderstatementScaled,
            current.SegmentationUnderstatementScaled);
        Assert.Equal(original.MarketTotals, current.MarketTotals);
        Assert.Equal(original.Candidates.Select(d => (
                d.DeliveryOrdinal, d.Seller, d.SourceOccupation,
                d.SourceTile, d.Buyer, d.BuyerTile, d.Commodity,
                d.GoodsMicro,
                Route: string.Join(",", d.RouteTiles.Select(
                    tile => tile.FineStrategicCellId.Value)))),
            current.Candidates.Select(d => (
                d.DeliveryOrdinal, d.Seller, d.SourceOccupation,
                d.SourceTile, d.Buyer, d.BuyerTile, d.Commodity,
                d.GoodsMicro,
                Route: string.Join(",", d.RouteTiles.Select(
                    tile => tile.FineStrategicCellId.Value)))));
        Assert.Equal(original.AllocationAudit.DeliveryCount,
            current.AllocationAudit.DeliveryCount);
        Assert.Equal(original.AllocationAudit.TotalAvailableGoodsMicro,
            current.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(original.AllocationAudit.TotalAllocatedGoodsMicro,
            current.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(original.AllocationAudit.Freight.TotalDeliveredCargoMicro,
            current.AllocationAudit.Freight.TotalDeliveredCargoMicro);
        Assert.Equal(
            original.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro,
            current.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
        Assert.Equal(
            original.AllocationAudit.Freight.UsedEdges.Select(edge =>
                (edge.Edge, edge.TotalCargoMicro)),
            current.AllocationAudit.Freight.UsedEdges.Select(edge =>
                (edge.Edge, edge.TotalCargoMicro)));
        Assert.Equal(8, current.ProductiveLotCount);
        Assert.Equal(104, current.RealMarketCount);
        Assert.Equal(8_000_000UL,
            current.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(8_000_000UL,
            current.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(initialHash, stress.Host.StateHash);
        Assert.Same(initialSnapshot, stress.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Blue).Availability);
        _output.WriteLine(
            $"GA-NEXT SEED ELISION A/B segments={segmentsPerMarket} " +
            $"referenceMs={reference.ElapsedMs:F3} " +
            $"optimizedMs={optimized.ElapsedMs:F3} " +
            $"referenceAllocated={reference.AllocatedBytes} " +
            $"optimizedAllocated={optimized.AllocatedBytes} EXACT");

        string? directory = Environment.GetEnvironmentVariable(
            "GA_SEED_ELISION_AB_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory,
                    $"seed-elision-segments-{segmentsPerMarket}.json"),
                JsonSerializer.Serialize(new
                {
                    Schema = "ga-next.global-seed-elision-ab.v1",
                    Scenario = "112-connected-Goldberg-8-sellers-104-markets",
                    SellerCount = current.ProductiveLotCount,
                    MarketCount = current.RealMarketCount,
                    SegmentsPerAverageMarket = segmentsPerMarket,
                    FlowAugmentations = current.FlowAugmentations,
                    TotalAvailableGoodsMicro =
                        current.AllocationAudit.TotalAvailableGoodsMicro,
                    TotalAllocatedGoodsMicro =
                        current.AllocationAudit.TotalAllocatedGoodsMicro,
                    ReferenceElapsedMilliseconds = reference.ElapsedMs,
                    OptimizedElapsedMilliseconds = optimized.ElapsedMs,
                    ReferenceAllocatedBytesCurrentThread =
                        reference.AllocatedBytes,
                    OptimizedAllocatedBytesCurrentThread =
                        optimized.AllocatedBytes,
                    ExactlyEquivalent = true,
                    StateHashUnchanged = true,
                    NoClosedCycle = true,
                    CandidateSettledEconomically = false
                }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void GlobalSeedPreflightAndReferenceRejectInvalidMarketsAndRoads()
    {
        var stress = EightCompetingSellersInConnectedGoldbergMarkets();
        var demand = Demand();
        var cargo = Cargo();
        var missingQuote = stress.Quotes.Skip(1).ToArray();
        var duplicatedQuote = stress.Quotes.Concat(new[]
        {
            stress.Quotes[0]
        }).ToArray();
        var invalidCost = stress.Roads.Select((road, i) =>
            i == 0 ? road with
            {
                PhysicalCostMicroPerWholeCargoUnit = 0UL
            } : road).ToArray();

        foreach (bool reference in new[] { true, false })
        {
            FoundationPhysicalGlobalConcaveFlowPlan Calculate(
                FoundationCommodityMarketPlanningQuote[] quotes,
                FoundationPhysicalRoutingEdgeCost[] roads,
                int maxAugmentations = 8192) =>
                FoundationPhysicalGlobalConcaveFlowPlanner
                    .CoordinateWithSearchWorkspace(stress.Workers,
                        stress.Host.Incidence, demand, cargo, quotes, roads,
                        4, maxAugmentations,
                        reuseSearchWorkspace: true,
                        useSequentialSeedReference: reference);

            Assert.Throws<ArgumentException>(() =>
                Calculate(missingQuote, stress.Roads));
            Assert.Throws<ArgumentException>(() =>
                Calculate(duplicatedQuote, stress.Roads));
            Assert.Throws<ArgumentException>(() =>
                Calculate(stress.Quotes, invalidCost));
            var budget = Assert.Throws<InvalidOperationException>(() =>
                Calculate(stress.Quotes, stress.Roads, maxAugmentations: 1));
            Assert.Contains("augmentation budget", budget.Message);
        }
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
