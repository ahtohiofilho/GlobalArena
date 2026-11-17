using System.Diagnostics;
using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalMarginalPathRebalancerTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalMarginalPathRebalancerTests(
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
        new("test-integrated-freight-cargo-micro",
            commodities.Select(item =>
                new FoundationCommodityCargoConversion(item, 1_000_000UL)));

    private static PhysicalTacticalTileId Tile(
        PhysicalTacticalIncidenceMap map, StrategicCellId id) =>
        new(map.Refinement.FineParameters, id);

    private static StrategicCellId Coarse(
        PhysicalTacticalIncidenceMap map, StrategicCellId id) =>
        map.TileIncidences[checked((int)id.Value - 1)]
            .IncidentCoarseCellIds[0];

    private static PhysicalTacticalEdgeId Crossing(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId a, StrategicCellId b)
    {
        var e = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(a) &&
            edge.IncidentCellIds.Contains(b));
        return new PhysicalTacticalEdgeId(
            map.Refinement.FineParameters, e.Id);
    }

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
                    if (d.IsValid) return (first.Id, b, c, d);
                }
        throw new InvalidOperationException("No real Goldberg diamond.");
    }

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge, CivilizationId? owner = null,
        ulong tariff = 0UL) =>
        new(edge, owner.HasValue
                ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, 100_000UL, tariff);

    private static FoundationPhysicalEdgeCarryingScale Capacity(
        PhysicalTacticalEdgeId edge, ulong amount = 10_000_000UL) =>
        new(edge, amount);

    private static PhysicalWorkerRuntimeState Workers(
        PhysicalTacticalIncidenceMap map, StrategicCellId source,
        StrategicCellId buyer) =>
        new(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, Coarse(map, source), One, 10_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                Tile(map, source), PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, Coarse(map, buyer), One, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                Tile(map, buyer), PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState workers,
        FoundationActivityCommodityDemandPolicy policy,
        PhysicalTacticalTileId desired) =>
        FoundationPhysicalCommodityDemandProjector.Project(workers, policy)
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Tile == desired ? 1_000_000_000_000UL :
                        1_000_000UL)).ToArray();

    private static (
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Capacities) DiamondRoads(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId a, StrategicCellId b,
        StrategicCellId c, StrategicCellId d,
        bool ownerTariff = false)
    {
        var first = Crossing(map, a, b);
        var roads = new[]
        {
            Road(first, ownerTariff ? Blue : null,
                ownerTariff ? 2_000_000UL : 0UL),
            Road(Crossing(map, b, d)),
            Road(Crossing(map, a, c)),
            Road(Crossing(map, c, d))
        };
        return (roads, roads.Select(x => Capacity(x.Edge)).ToArray());
    }

    private static FoundationPhysicalMultiPathCandidatePlan ForcedSingleRoute(
        PhysicalWorkerRuntimeState workers,
        PhysicalTacticalIncidenceMap map,
        FoundationPhysicalMultiPathCandidatePlan seed,
        FoundationPhysicalRoutingEdgeCost[] roads,
        FoundationPhysicalEdgeCarryingScale[] capacities,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId middle,
        PhysicalTacticalTileId to)
    {
        var parts = new List<FoundationPhysicalMultiPathDeliveryPart>();
        foreach (var parent in seed.MarketAllocation.Candidates)
        {
            var route = parent.SourceTile == from &&
                        parent.BuyerTile == to
                ? new[] { from, middle, to }
                : parent.RouteTiles.ToArray();
            var candidate = parent with
            {
                DeliveryOrdinal = checked((ulong)parts.Count + 1UL),
                RouteTiles = Array.AsReadOnly(route)
            };
            parts.Add(new FoundationPhysicalMultiPathDeliveryPart(
                parent.DeliveryOrdinal, candidate,
                candidate.GoodsMicro));
        }
        var (_, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(map, roads);
        var policy = Demand(One);
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            workers, map, policy, Cargo(One),
            parts.Select(x => x.Delivery), ownership);
        var loads = audit.Freight.UsedEdges.ToDictionary(
            x => x.Edge, x => x.TotalCargoMicro);
        var scales = capacities.ToDictionary(x => x.Edge,
            x => x.CargoMicroAtReferenceLoad);
        var readings = roads.OrderBy(x =>
            x.Edge.FineStrategicEdgeId.Value).Select(x =>
            new FoundationPhysicalCongestedEdgeReading(
                x.Edge, loads.GetValueOrDefault(x.Edge),
                scales[x.Edge], x.PhysicalCostMicroPerWholeCargoUnit,
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    x.PhysicalCostMicroPerWholeCargoUnit,
                    loads.GetValueOrDefault(x.Edge), scales[x.Edge]),
                x.OwnershipKind, x.InfrastructureOwner,
                x.ExternalTariffMicroPerWholeCargoUnit)).ToArray();
        return new FoundationPhysicalMultiPathCandidatePlan(
            seed.MarketAllocation, parts.ToArray(), audit,
            readings, seed.RoutingBatches);
    }

    [Fact]
    public void DiscreteIntegratedCostUsesFifthPowerOfLoadWithoutAHardCargoCeiling()
    {
        var zero = FoundationPhysicalMarginalPathRebalancer
            .IntegratedPhysicalCost(100_000UL, 0UL, 1_000_000UL);
        var one = FoundationPhysicalMarginalPathRebalancer
            .IntegratedPhysicalCost(100_000UL, 1_000_000UL, 1_000_000UL);
        var two = FoundationPhysicalMarginalPathRebalancer
            .IntegratedPhysicalCost(100_000UL, 2_000_000UL, 1_000_000UL);
        Assert.Equal(BigInteger.Zero, zero);
        Assert.Equal(new BigInteger(200_000_000_000UL), one);
        Assert.Equal(new BigInteger(3_400_000_000_000UL), two);
        Assert.True(two - one > one);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalMarginalPathRebalancer.IntegratedPhysicalCost(
                100_000UL, 1UL, 0UL));
    }

    [Fact]
    public void OverloadedDiamondStrictlyReducesIntegratedTotalAndUsesBothPaths()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var workers = Workers(host.Incidence, a, d);
        var (roads, capacities) = DiamondRoads(host.Incidence, a, b, c, d);
        var seed = FoundationPhysicalMultiPathFreightPlanner.Plan(
            workers, host.Incidence, Demand(One), Cargo(One),
            Quotes(workers, Demand(One), Tile(host.Incidence, d)),
            roads, capacities, 8, 32);
        var forced = ForcedSingleRoute(workers, host.Incidence,
            seed, roads, capacities, Tile(host.Incidence, a),
            Tile(host.Incidence, b), Tile(host.Incidence, d));
        var result = FoundationPhysicalMarginalPathRebalancer.Rebalance(
            workers, host.Incidence, Demand(One), Cargo(One),
            forced, roads, capacities,
            maxImprovementPasses: 32, maxFragmentsPerPart: 16);

        Assert.True(result.StableUnderFullMarginalPass);
        Assert.True(result.ReassignedFragments > 0);
        Assert.True(result.FinalIntegratedCost < result.InitialIntegratedCost);
        Assert.InRange(result.ImprovementPasses, 2, 32);
        Assert.Equal(10_000_000UL,
            result.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        var remote = Assert.Single(seed.MarketAllocation.Candidates,
            x => x.BuyerTile == Tile(host.Incidence, d));
        var parts = result.Freight.Parts.Where(
            x => x.ParentDeliveryOrdinal == remote.DeliveryOrdinal).ToArray();
        Assert.Equal(2, parts.Length);
        Assert.Contains(parts, p =>
            p.Delivery.RouteTiles[1].FineStrategicCellId == b);
        Assert.Contains(parts, p =>
            p.Delivery.RouteTiles[1].FineStrategicCellId == c);
        Assert.Equal(remote.GoodsMicro,
            parts.Aggregate(0UL, (sum, p) =>
                checked(sum + p.Delivery.GoodsMicro)));
        Assert.Equal(4, result.Freight.AllocationAudit.Freight.UsedEdges.Count);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ExplicitOwnerTariffIsIncludedInIntegratedRerouteChoice()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var workers = Workers(host.Incidence, a, d);
        var (roads, capacities) = DiamondRoads(
            host.Incidence, a, b, c, d, ownerTariff: true);
        var seed = FoundationPhysicalMultiPathFreightPlanner.Plan(
            workers, host.Incidence, Demand(One), Cargo(One),
            Quotes(workers, Demand(One), Tile(host.Incidence, d)),
            roads, capacities, 8, 16);
        var forced = ForcedSingleRoute(workers, host.Incidence,
            seed, roads, capacities, Tile(host.Incidence, a),
            Tile(host.Incidence, b), Tile(host.Incidence, d));
        var improved = FoundationPhysicalMarginalPathRebalancer.Rebalance(
            workers, host.Incidence, Demand(One), Cargo(One),
            forced, roads, capacities, 32, 16);
        Assert.True(improved.ReassignedFragments > 0);
        Assert.True(improved.FinalIntegratedCost <
            improved.InitialIntegratedCost);
        var remote = seed.MarketAllocation.Candidates.Where(
            x => x.BuyerTile == Tile(host.Incidence, d))
            .Select(x => x.DeliveryOrdinal).ToHashSet();
        Assert.All(improved.Freight.Parts.Where(p =>
            remote.Contains(p.ParentDeliveryOrdinal)), p =>
        {
            Assert.Equal(c,
                p.Delivery.RouteTiles[1].FineStrategicCellId);
        });
        Assert.Contains(improved.Freight.ProjectedEdgeCosts,
            x => x.InfrastructureOwner == Blue &&
                 x.ExternalTariffMicroPerWholeCargoUnit == 2_000_000UL);
    }

    [Fact]
    public void OnePassCannotFalselyClaimLocalOptimalityAfterARealMove()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var workers = Workers(host.Incidence, a, d);
        var (roads, capacities) = DiamondRoads(host.Incidence, a, b, c, d);
        var seed = FoundationPhysicalMultiPathFreightPlanner.Plan(
            workers, host.Incidence, Demand(One), Cargo(One),
            Quotes(workers, Demand(One), Tile(host.Incidence, d)),
            roads, capacities, 8, 16);
        var forced = ForcedSingleRoute(workers, host.Incidence,
            seed, roads, capacities, Tile(host.Incidence, a),
            Tile(host.Incidence, b), Tile(host.Incidence, d));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalMarginalPathRebalancer.Rebalance(
                workers, host.Incidence, Demand(One), Cargo(One),
                forced, roads, capacities, 1, 16));
    }

    [Fact]
    public void LocalTwoCommodityWorkforceDoesNotInventTollOrTransport()
    {
        var host = Host();
        var census = host.Snapshot.Workers;
        var policy = Demand(One, Two);
        var quotes = FoundationPhysicalCommodityDemandProjector.Project(
            census, policy).DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL));
        var hash = host.StateHash;
        var original = host.PlanMultiPathTradeCandidates(
            policy, Cargo(One, Two), quotes,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(), 8, 4);
        var result = FoundationPhysicalMarginalPathRebalancer.Rebalance(
            census, host.Incidence, policy, Cargo(One, Two),
            original, Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(), 4, 4);
        Assert.Equal(1, result.ImprovementPasses);
        Assert.Equal(0, result.ReassignedFragments);
        Assert.Equal(result.InitialIntegratedCost,
            result.FinalIntegratedCost);
        Assert.Equal(11_000_000UL,
            result.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Empty(result.Freight.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void CoupledFeedbackCanOptIntoIntegratedRouteCostWithoutChangingLegacyDefault()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Workers(host.Incidence, a, d);
        var (roads, scales) = DiamondRoads(host.Incidence, a, b, c, d);
        var policy = Demand(One);
        var quotes = Quotes(census, policy, Tile(host.Incidence, d));
        var old = FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            census, host.Incidence, policy, Cargo(One), quotes, roads,
            scales, 8, 16, 8);
        var integrated = FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(
            census, host.Incidence, policy, Cargo(One), quotes, roads,
            scales, 8, 16, 8, maxMarginalPathPasses: 32,
            maxFragmentsPerPathPart: 4);
        Assert.Null(old.PathOptimization);
        Assert.NotNull(integrated.PathOptimization);
        Assert.True(integrated.PathOptimization.StableUnderFullMarginalPass);
        Assert.True(integrated.ExactCostAndLoadAgreement);
        Assert.Equal(old.Freight.AllocationAudit.TotalAllocatedGoodsMicro,
            integrated.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void DenseConnectedHundredMarketsRetainAllRealSalesAndExactEdgeAudits()
    {
        const int physicalCells = 112;
        const int sourceCount = 8;
        const int buyerCount = 96;
        var host = Host();
        var map = host.Incidence;
        var seed = map.FineTopology.Cells[0].Id;
        var queue = new Queue<StrategicCellId>();
        var seen = new HashSet<StrategicCellId> { seed };
        var component = new List<StrategicCellId>();
        queue.Enqueue(seed);
        while (queue.Count > 0 && component.Count < physicalCells)
        {
            var at = queue.Dequeue();
            component.Add(at);
            var cell = map.FineTopology.Cells[
                checked((int)at.Value - 1)];
            foreach (var neighbor in cell.AdjacentCellIds.OrderBy(x => x.Value))
                if (seen.Add(neighbor))
                    queue.Enqueue(neighbor);
        }
        Assert.Equal(physicalCells, component.Count);
        var selected = component.ToHashSet();
        var edges = map.FineTopology.Edges
            .Where(edge => selected.Contains(edge.IncidentCellIds[0]) &&
                selected.Contains(edge.IncidentCellIds[1])).ToArray();
        Assert.True(edges.Length > physicalCells);
        var sources = component.Take(sourceCount).ToArray();
        var buyers = component.Skip(physicalCells - buyerCount).ToArray();
        var workers = new List<PhysicalWorkerRuntimeRecord>();
        for (int i = 0; i < sourceCount; i++)
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(i + 1)),
                Red, Coarse(map, sources[i]), One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                Tile(map, sources[i]), PhysicalWorkerOccupation.Agriculture));
        for (int i = 0; i < buyerCount; i++)
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(sourceCount + i + 1)),
                Blue, Coarse(map, buyers[i]), One, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                Tile(map, buyers[i]), PhysicalWorkerOccupation.Unassigned));
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var policy = Demand(One);
        var quote = FoundationPhysicalCommodityDemandProjector.Project(
            census, policy).DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                line.Owner == Blue ? 1_000_000_000UL :
                    1_000_000UL)).ToArray();
        var roads = edges.Select(edge => Road(
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                edge.Id))).ToArray();
        var capacities = roads.Select(road =>
            Capacity(road.Edge, 100_000_000UL)).ToArray();

        var timer = Stopwatch.StartNew();
        var seedPlan = FoundationPhysicalMultiPathFreightPlanner.Plan(
            census, map, policy, Cargo(One), quote, roads, capacities,
            maxProducerPasses: 32, maxBatchesPerDelivery: 2);
        var final = FoundationPhysicalMarginalPathRebalancer.Rebalance(
            census, map, policy, Cargo(One),
            seedPlan, roads, capacities,
            maxImprovementPasses: 24, maxFragmentsPerPart: 1);
        timer.Stop();

        _output.WriteLine(
            $"Connected marginal freight: fineCells={physicalCells}, " +
            $"roads={roads.Length}, workers={workers.Count}, " +
            $"realMarkets={seedPlan.MarketAllocation.MarketTotals.Count}, " +
            $"producerLots={seedPlan.MarketAllocation.ProductiveLotCount}, " +
            $"fragments={final.PhysicalFragments}, moves={final.ReassignedFragments}, " +
            $"passes={final.ImprovementPasses}, elapsedMs={timer.ElapsedMilliseconds}");
        Assert.Equal(sourceCount,
            final.Freight.MarketAllocation.ProductiveLotCount);
        Assert.Equal(sourceCount + buyerCount,
            final.Freight.MarketAllocation.MarketTotals.Count);
        Assert.True(final.StableUnderFullMarginalPass);
        Assert.True(final.FinalIntegratedCost <=
            final.InitialIntegratedCost);
        Assert.Equal((ulong)sourceCount * 1_000_000UL,
            final.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(final.Freight.AllocationAudit.TotalAvailableGoodsMicro,
            final.Freight.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.True(final.Freight.AllocationAudit.Freight.UsedEdges.Count > 1);
        Assert.Equal(roads.Length,
            final.Freight.ProjectedEdgeCosts.Count);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Blue).Availability);
    }
}
