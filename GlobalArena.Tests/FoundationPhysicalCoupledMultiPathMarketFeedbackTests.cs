using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalCoupledMultiPathMarketFeedbackTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private const string CargoUnit = "test-coupled-common-cargo-micro";
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalCoupledMultiPathMarketFeedbackTests(
        ITestOutputHelper output) => _output = output;

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand(
        params CommodityId[] commodities) =>
        new(commodities,
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(activity => commodities.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        activity, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo(
        params CommodityId[] commodities) =>
        new(CargoUnit, commodities.Select(item =>
            new FoundationCommodityCargoConversion(item, 1_000_000UL)));

    private static PhysicalTacticalTileId Tile(
        PhysicalTacticalIncidenceMap map, StrategicCellId id) =>
        new(map.Refinement.FineParameters, id);

    private static StrategicCellId Coarse(
        PhysicalTacticalIncidenceMap map, StrategicCellId id) =>
        map.TileIncidences[checked((int)id.Value - 1)]
            .IncidentCoarseCellIds[0];

    private static PhysicalTacticalEdgeId Edge(
        PhysicalTacticalIncidenceMap map, StrategicCellId a, StrategicCellId b)
    {
        var geometry = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(a) &&
            edge.IncidentCellIds.Contains(b));
        return new PhysicalTacticalEdgeId(
            map.Refinement.FineParameters, geometry.Id);
    }

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge, CivilizationId? owner = null) =>
        new(edge, owner.HasValue
                ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, 100_000UL, owner.HasValue ? 30_000UL : 0UL);

    private static FoundationPhysicalEdgeCarryingScale Capacity(
        PhysicalTacticalEdgeId edge, ulong capacity = 10_000_000UL) =>
        new(edge, capacity);

    private static (StrategicCellId A, StrategicCellId B,
        StrategicCellId C, StrategicCellId D) Diamond(
        PhysicalTacticalIncidenceMap map)
    {
        foreach (var first in map.FineTopology.Cells)
            foreach (var b in first.AdjacentCellIds)
                foreach (var c in first.AdjacentCellIds.Where(x => x != b))
                {
                    var bCell = map.FineTopology.Cells[
                        checked((int)b.Value - 1)];
                    var cCell = map.FineTopology.Cells[
                        checked((int)c.Value - 1)];
                    var d = bCell.AdjacentCellIds.FirstOrDefault(x =>
                        x != first.Id && x != b && x != c &&
                        cCell.AdjacentCellIds.Contains(x));
                    if (d.IsValid)
                        return (first.Id, b, c, d);
                }
        throw new InvalidOperationException("Goldberg fine graph has no diamond.");
    }

    private static PhysicalWorkerRuntimeState Census(
        PhysicalTacticalIncidenceMap map, StrategicCellId a,
        StrategicCellId d)
    {
        return new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red, Coarse(map, a), One,
                10_000_000UL, PhysicalWorkerActivity.Producing,
                1_000_000U, Tile(map, a), PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue, Coarse(map, d), One,
                0UL, PhysicalWorkerActivity.Other,
                1_000_000U, Tile(map, d), PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
    }

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState census,
        FoundationActivityCommodityDemandPolicy policy,
        PhysicalTacticalTileId preferred)
        => FoundationPhysicalCommodityDemandProjector.Project(census, policy)
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Tile == preferred ? 1_000_000_000_000UL :
                        1_000_000UL)).ToArray();

    private static (
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Capacities) DiamondRoads(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId a, StrategicCellId b,
        StrategicCellId c, StrategicCellId d)
    {
        var roads = new[] { Edge(map, a, b), Edge(map, b, d),
            Edge(map, a, c), Edge(map, c, d) };
        return (roads.Select(x => Road(x)).ToArray(),
            roads.Select(x => Capacity(x)).ToArray());
    }

    [Fact]
    public void BuyerFreightLoopClosesWithTwoActualPathsInOnePhysicalMarketDelivery()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Census(host.Incidence, a, d);
        var (roads, capacities) = DiamondRoads(host.Incidence, a, b, c, d);
        var policy = Demand(One);
        var before = host.StateHash;
        var plan = FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            census, host.Incidence, policy, Cargo(One),
            Quotes(census, policy, Tile(host.Incidence, d)),
            roads, capacities, maxProducerPasses: 8,
            maxBatchesPerDelivery: 32, maxFeedbackIterations: 8);

        Assert.True(plan.ExactCostAndLoadAgreement);
        Assert.InRange(plan.FeedbackIterations, 2, 8);
        Assert.Equal(10_000_000UL,
            plan.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(plan.Freight.AllocationAudit.TotalAvailableGoodsMicro,
            plan.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        var remote = Assert.Single(plan.Freight.MarketAllocation.Candidates,
            line => line.SourceTile == Tile(host.Incidence, a) &&
                line.BuyerTile == Tile(host.Incidence, d));
        var parts = plan.Freight.Parts.Where(x =>
            x.ParentDeliveryOrdinal == remote.DeliveryOrdinal).ToArray();
        Assert.Equal(2, parts.Length);
        Assert.Equal(remote.GoodsMicro,
            parts.Aggregate(0UL, (sum, part) =>
                checked(sum + part.Delivery.GoodsMicro)));
        Assert.Contains(parts, x =>
            x.Delivery.RouteTiles[1].FineStrategicCellId == b);
        Assert.Contains(parts, x =>
            x.Delivery.RouteTiles[1].FineStrategicCellId == c);
        Assert.Equal(4, plan.Freight.AllocationAudit.Freight.UsedEdges.Count);
        Assert.All(plan.ConsistentEdgeCosts, cost =>
        {
            var reading = Assert.Single(plan.Freight.ProjectedEdgeCosts,
                edge => edge.Edge == cost.Edge);
            Assert.Equal(reading.CongestedPhysicalCostMicroPerWholeCargoUnit,
                cost.PhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(reading.ExternalTariffMicroPerWholeCargoUnit,
                cost.ExternalTariffMicroPerWholeCargoUnit);
        });
        Assert.Equal(before, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void OneIterationBudgetFailsClosedWhenRealCrossingCarriesCargo()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Census(host.Incidence, a, d);
        var (roads, capacities) = DiamondRoads(host.Incidence, a, b, c, d);
        var policy = Demand(One);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
                census, host.Incidence, policy, Cargo(One),
                Quotes(census, policy, Tile(host.Incidence, d)),
                roads, capacities, maxProducerPasses: 8,
                maxBatchesPerDelivery: 32, maxFeedbackIterations: 1));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void LocalWorkerMarketClosesImmediatelyWithoutImaginaryInfrastructure()
    {
        var host = Host();
        var policy = Demand(One, Two);
        var inputs = host.GetCommodityMarketInputs(policy);
        var quotes = inputs.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL));
        var snapshot = host.Snapshot;
        var hash = host.StateHash;
        var result = host.CoordinateCoupledMultiPathMarketCandidates(
            policy, Cargo(One, Two), quotes,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
            maxProducerPasses: 8, maxBatchesPerDelivery: 4,
            maxFeedbackIterations: 2);
        Assert.Equal(1, result.FeedbackIterations);
        Assert.Empty(result.Freight.ProjectedEdgeCosts);
        Assert.Empty(result.Freight.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(11_000_000UL,
            result.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Same(snapshot, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void InvalidPoliciesCannotProducePhantomClearedEconomicCycles()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Census(host.Incidence, a, d);
        var (roads, capacities) = DiamondRoads(host.Incidence, a, b, c, d);
        var policy = Demand(One);
        var quotes = Quotes(census, policy, Tile(host.Incidence, d));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
                census, host.Incidence, policy, Cargo(One),
                quotes, roads, capacities, 8, 32, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
                census, host.Incidence, policy, Cargo(One),
                quotes, roads, capacities, 0, 32, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
                census, host.Incidence, policy, Cargo(One),
                quotes, roads, capacities, 8, 0, 8));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
                census, host.Incidence, policy, Cargo(One),
                quotes, roads, capacities.Skip(1), 8, 32, 8));
    }

    [Fact]
    public void WorkerAndPolicyPermutationPreservesCoupledRouteAndMarketPlan()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Census(host.Incidence, a, d);
        var reverse = new PhysicalWorkerRuntimeState(census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var policy = Demand(One);
        var (roads, scales) = DiamondRoads(host.Incidence, a, b, c, d);
        var quotes = Quotes(census, policy, Tile(host.Incidence, d));
        var one = FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            census, host.Incidence, policy, Cargo(One),
            quotes, roads, scales, 8, 32, 8);
        var two = FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            reverse, host.Incidence, policy, Cargo(One),
            quotes.Reverse(), roads.Reverse(), scales.Reverse(), 8, 32, 8);
        Assert.Equal(one.FeedbackIterations, two.FeedbackIterations);
        Assert.Equal(one.ConsistentEdgeCosts.ToArray(),
            two.ConsistentEdgeCosts.ToArray());
        Assert.Equal(one.Freight.MarketAllocation.MarketTotals.ToArray(),
            two.Freight.MarketAllocation.MarketTotals.ToArray());
        Assert.Equal(one.Freight.Parts.Select(x => (
            x.ParentDeliveryOrdinal, x.Delivery.DeliveryOrdinal,
            x.CargoMicro, string.Join(",", x.Delivery.RouteTiles.Select(t =>
                t.FineStrategicCellId.Value)))),
            two.Freight.Parts.Select(x => (
            x.ParentDeliveryOrdinal, x.Delivery.DeliveryOrdinal,
            x.CargoMicro, string.Join(",", x.Delivery.RouteTiles.Select(t =>
                t.FineStrategicCellId.Value)))));
    }

    [Fact]
    public void DenselyInterconnectedFineGraphHasCompetingRealProducerPaths()
    {
        const int sourceCount = 12;
        const int buyerCount = 12;
        const int componentSize = 56;
        var host = Host();
        var map = host.Incidence;
        var seed = map.FineTopology.Cells[0].Id;
        var queue = new Queue<StrategicCellId>();
        var selected = new List<StrategicCellId>();
        var seen = new HashSet<StrategicCellId> { seed };
        queue.Enqueue(seed);
        while (queue.Count > 0 && selected.Count < componentSize)
        {
            var at = queue.Dequeue();
            selected.Add(at);
            var cell = map.FineTopology.Cells[
                checked((int)at.Value - 1)];
            foreach (var neighbor in cell.AdjacentCellIds.OrderBy(x => x.Value))
                if (seen.Add(neighbor))
                    queue.Enqueue(neighbor);
        }
        Assert.Equal(componentSize, selected.Count);
        var set = selected.ToHashSet();
        var borders = map.FineTopology.Edges
            .Where(e => set.Contains(e.IncidentCellIds[0]) &&
                set.Contains(e.IncidentCellIds[1]))
            .ToArray();
        Assert.True(borders.Length >= componentSize);
        var sources = selected.Take(sourceCount).ToArray();
        var buyers = selected.Skip(componentSize - buyerCount).ToArray();
        var workers = new List<PhysicalWorkerRuntimeRecord>();
        for (int i = 0; i < sourceCount; i++)
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)i + 1UL),
                Red, Coarse(map, sources[i]), One,
                1_000_000UL, PhysicalWorkerActivity.Producing,
                1_000_000U, Tile(map, sources[i]),
                PhysicalWorkerOccupation.Agriculture));
        for (int i = 0; i < buyerCount; i++)
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(sourceCount + i + 1)),
                Blue, Coarse(map, buyers[i]), One,
                0UL, PhysicalWorkerActivity.Other,
                1_000_000U, Tile(map, buyers[i]),
                PhysicalWorkerOccupation.Unassigned));
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var costs = borders.Select(e => Road(
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                e.Id))).ToArray();
        var scales = costs.Select(edge =>
            Capacity(edge.Edge, 1_000_000_000UL)).ToArray();
        var policy = Demand(One);
        var preferred = Tile(map, buyers[^1]);
        var quote = Quotes(census, policy, preferred);
        var timer = Stopwatch.StartNew();
        var result = FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            census, map, policy, Cargo(One), quote,
            costs, scales, maxProducerPasses: 32,
            maxBatchesPerDelivery: 4, maxFeedbackIterations: 6);
        timer.Stop();
        _output.WriteLine(
            $"Connected network: cells={componentSize}, " +
            $"edges={borders.Length}, sources={sourceCount}, buyers={buyerCount}, " +
            $"routeParts={result.Freight.Parts.Count}, " +
            $"routingBatches={result.Freight.RoutingBatches}, " +
            $"passes={result.FeedbackIterations}, elapsedMs={timer.ElapsedMilliseconds}");
        Assert.True(result.ExactCostAndLoadAgreement);
        Assert.Equal(sourceCount,
            result.Freight.MarketAllocation.ProductiveLotCount);
        Assert.Equal(sourceCount + buyerCount,
            result.Freight.MarketAllocation.MarketTotals.Count);
        Assert.Equal((ulong)sourceCount * 1_000_000UL,
            result.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(result.Freight.AllocationAudit.TotalAvailableGoodsMicro,
            result.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.True(result.Freight.AllocationAudit.Freight.UsedEdges.Count > 1);
        Assert.Equal(costs.Length, result.Freight.ProjectedEdgeCosts.Count);
        Assert.Equal(costs.Length, result.ConsistentEdgeCosts.Count);
        Assert.All(result.ConsistentEdgeCosts, cost =>
        {
            var edge = Assert.Single(result.Freight.ProjectedEdgeCosts,
                e => e.Edge == cost.Edge);
            Assert.Equal(edge.CongestedPhysicalCostMicroPerWholeCargoUnit,
                cost.PhysicalCostMicroPerWholeCargoUnit);
        });
    }
}
