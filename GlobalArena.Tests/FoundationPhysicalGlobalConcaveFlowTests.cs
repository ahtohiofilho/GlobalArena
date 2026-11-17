using System.Diagnostics;
using System.Numerics;
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
}
