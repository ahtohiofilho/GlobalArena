using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalMultiPathFreightPlannerTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private const string Unit = "test-shared-multipath-cargo-micro";
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalMultiPathFreightPlannerTests(
        ITestOutputHelper output) => _output = output;

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand(
        params CommodityId[] goods) =>
        new(goods, Enum.GetValues<FoundationPhysicalDemandProfile>()
            .SelectMany(profile => goods.Select(good =>
                new FoundationActivityCommodityDemandCoefficient(
                    profile, good, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo(
        params CommodityId[] goods) =>
        new(Unit, goods.Select(good =>
            new FoundationCommodityCargoConversion(good, 1_000_000UL)));

    private static StrategicCellId Coarse(
        PhysicalTacticalIncidenceMap map, StrategicCellId fine) =>
        map.TileIncidences[checked((int)fine.Value - 1)]
            .IncidentCoarseCellIds[0];

    private static PhysicalTacticalTileId Tile(
        PhysicalTacticalIncidenceMap map, StrategicCellId cell) =>
        new(map.Refinement.FineParameters, cell);

    private static PhysicalTacticalEdgeId Crossing(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId from, StrategicCellId to)
    {
        var geometry = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(from) &&
            edge.IncidentCellIds.Contains(to));
        return new PhysicalTacticalEdgeId(
            map.Refinement.FineParameters, geometry.Id);
    }

    private static (StrategicCellId A, StrategicCellId B,
        StrategicCellId C, StrategicCellId D) Diamond(
        PhysicalTacticalIncidenceMap map)
    {
        foreach (var a in map.FineTopology.Cells)
        {
            foreach (var b in a.AdjacentCellIds)
            {
                var fromB = map.FineTopology.Cells[
                    checked((int)b.Value - 1)].AdjacentCellIds;
                foreach (var c in a.AdjacentCellIds.Where(x => x != b))
                {
                    var fromC = map.FineTopology.Cells[
                        checked((int)c.Value - 1)].AdjacentCellIds;
                    var d = fromB.FirstOrDefault(id =>
                        id != a.Id && id != b && id != c &&
                        fromC.Contains(id));
                    if (d.IsValid)
                        return (a.Id, b, c, d);
                }
            }
        }
        throw new InvalidOperationException(
            "No two physical alternative routes available.");
    }

    private static PhysicalWorkerRuntimeState Census(
        PhysicalTacticalIncidenceMap map, StrategicCellId from,
        StrategicCellId to, bool twoCommodities = false)
    {
        var a = Tile(map, from);
        var d = Tile(map, to);
        var workers = new List<PhysicalWorkerRuntimeRecord>
        {
            new(new PhysicalWorkerId(1UL), Red, Coarse(map, from),
                One, 10_000_000UL, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Agriculture),
            new(new PhysicalWorkerId(2UL), Blue, Coarse(map, to),
                One, 0UL, PhysicalWorkerActivity.Other,
                1_000_000U, d, PhysicalWorkerOccupation.Unassigned)
        };
        if (twoCommodities)
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(3UL), Red, Coarse(map, from),
                Two, 10_000_000UL, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Forestry));
        return new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
    }

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState workers,
        FoundationActivityCommodityDemandPolicy policy,
        PhysicalTacticalTileId preferred) =>
        FoundationPhysicalCommodityDemandProjector.Project(workers, policy)
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Tile == preferred ? 1_000_000_000UL
                        : 1_000_000UL)).ToArray();

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge,
        CivilizationId? owner = null,
        ulong tariff = 0UL) =>
        new(edge,
            owner.HasValue ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, 100_000UL, tariff);

    private static FoundationPhysicalEdgeCarryingScale Scale(
        PhysicalTacticalEdgeId edge,
        ulong reference = 10_000_000UL) =>
        new(edge, reference);

    private static (
        FoundationPhysicalRoutingEdgeCost[] Costs,
        FoundationPhysicalEdgeCarryingScale[] Scales) DiamondRoads(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId a, StrategicCellId b,
        StrategicCellId c, StrategicCellId d)
    {
        var edges = new[]
        {
            Crossing(map, a, b), Crossing(map, b, d),
            Crossing(map, a, c), Crossing(map, c, d)
        };
        return (edges.Select(edge => Road(edge)).ToArray(),
            edges.Select(edge => Scale(edge)).ToArray());
    }

    [Fact]
    public void OneRemoteMarketDeliveryCanUseBothRealAlternativeRoutes()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Census(host.Incidence, a, d);
        var (costs, scales) = DiamondRoads(host.Incidence, a, b, c, d);
        var result = FoundationPhysicalMultiPathFreightPlanner.Plan(
            census, host.Incidence, Demand(One), Cargo(One),
            Quotes(census, Demand(One), Tile(host.Incidence, d)),
            costs, scales, maxProducerPasses: 8, maxBatchesPerDelivery: 32);

        Assert.Equal(10_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(result.MarketAllocation.AllocationAudit.TotalAllocatedGoodsMicro,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        var original = Assert.Single(result.MarketAllocation.Candidates,
            x => x.SourceTile == Tile(host.Incidence, a) &&
                x.BuyerTile == Tile(host.Incidence, d));
        var paths = result.Parts.Where(
            x => x.ParentDeliveryOrdinal == original.DeliveryOrdinal).ToArray();
        Assert.Equal(2, paths.Length);
        Assert.Equal(original.GoodsMicro,
            paths.Aggregate(0UL, (sum, x) =>
                checked(sum + x.Delivery.GoodsMicro)));
        Assert.Equal(original.GoodsMicro,
            paths.Aggregate(0UL, (sum, x) =>
                checked(sum + x.CargoMicro)));
        Assert.All(paths, path =>
        {
            Assert.Equal(a, path.Delivery.RouteTiles[0].FineStrategicCellId);
            Assert.Equal(d, path.Delivery.RouteTiles[^1].FineStrategicCellId);
            Assert.Equal(3, path.Delivery.RouteTiles.Count);
        });
        Assert.Contains(paths, x =>
            x.Delivery.RouteTiles[1].FineStrategicCellId == b);
        Assert.Contains(paths, x =>
            x.Delivery.RouteTiles[1].FineStrategicCellId == c);
        var loads = result.AllocationAudit.Freight.UsedEdges;
        Assert.Equal(4, loads.Count);
        Assert.Equal(checked(original.GoodsMicro * 2UL),
            result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
        Assert.All(result.ProjectedEdgeCosts, reading =>
        {
            Assert.True(reading.CargoMicro > 0UL);
            Assert.Equal(
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    reading.FreePhysicalCostMicroPerWholeCargoUnit,
                    reading.CargoMicro, reading.CargoMicroAtReferenceLoad),
                reading.CongestedPhysicalCostMicroPerWholeCargoUnit);
        });
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void NoRoadMeansLocalDeliveriesAndNoImaginaryTraffic()
    {
        var host = Host();
        var census = host.Snapshot.Workers;
        var demand = Demand(One);
        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(census, demand).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity, 1_000_000UL));
        var hash = host.StateHash;
        var snapshot = host.Snapshot;
        var result = host.PlanMultiPathTradeCandidates(
            demand, Cargo(One), quotes,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
            maxProducerPasses: 8, maxBatchesPerDelivery: 16);

        Assert.All(result.Parts, part =>
        {
            Assert.Single(part.Delivery.RouteTiles);
            Assert.Equal(part.Delivery.SourceTile, part.Delivery.BuyerTile);
        });
        Assert.Empty(result.ProjectedEdgeCosts);
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(result.AllocationAudit.TotalAvailableGoodsMicro,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(hash, host.StateHash);
        Assert.Same(snapshot, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void SharedPhysicalEdgeAccountsForTwoCommoditiesWithoutDuplicatedCapacity()
    {
        var host = Host();
        var first = host.Incidence.FineTopology.Edges[0];
        var a = first.IncidentCellIds[0];
        var b = first.IncidentCellIds[1];
        var edge = new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters, first.Id);
        var state = Census(host.Incidence, a, b, twoCommodities: true);
        var policy = Demand(One, Two);
        var result = FoundationPhysicalMultiPathFreightPlanner.Plan(
            state, host.Incidence, policy, Cargo(One, Two),
            Quotes(state, policy, Tile(host.Incidence, b)),
            new[] { Road(edge, owner: Blue, tariff: 30_000UL) },
            new[] { Scale(edge) }, maxProducerPasses: 8,
            maxBatchesPerDelivery: 16);
        Assert.Equal(20_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        var load = Assert.Single(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(edge, load.Edge);
        Assert.Equal(2, load.ByCommodity.Count);
        Assert.Equal(load.TotalCargoMicro,
            load.ByCommodity.Aggregate(0UL,
                (sum, line) => checked(sum + line.CargoMicro)));
        var reading = Assert.Single(result.ProjectedEdgeCosts);
        Assert.Equal(Blue, reading.InfrastructureOwner);
        Assert.Equal(30_000UL, reading.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(load.TotalCargoMicro, reading.CargoMicro);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
            reading.OwnershipKind);
    }

    [Fact]
    public void InvalidScalesAndBudgetsCannotManufacturePhysicalCapacity()
    {
        var host = Host();
        var geometry = host.Incidence.FineTopology.Edges[0];
        var state = Census(host.Incidence,
            geometry.IncidentCellIds[0], geometry.IncidentCellIds[1]);
        var edge = new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters, geometry.Id);
        var quotes = Quotes(state, Demand(One),
            Tile(host.Incidence, geometry.IncidentCellIds[1]));
        var road = new[] { Road(edge) };
        var proper = Scale(edge);
        foreach (var invalid in new[]
        {
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
            new[] { proper, proper },
            new[] { proper with { CargoMicroAtReferenceLoad = 0UL } },
            new[] { new FoundationPhysicalEdgeCarryingScale(
                new PhysicalTacticalEdgeId(new GoldbergParameters(12, 0),
                    edge.FineStrategicEdgeId), 1_000_000UL) }
        })
            Assert.Throws<ArgumentException>(() =>
                FoundationPhysicalMultiPathFreightPlanner.Plan(
                    state, host.Incidence, Demand(One), Cargo(One),
                    quotes, road, invalid, 8, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalMultiPathFreightPlanner.Plan(
                state, host.Incidence, Demand(One), Cargo(One),
                quotes, road, new[] { proper }, 8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalMultiPathFreightPlanner.Plan(
                state, host.Incidence, Demand(One), Cargo(One),
                quotes, road, new[] { proper }, 0, 16));
    }

    [Fact]
    public void EnumerationOrderDoesNotChangeAnyPhysicalRoutePartOrEdgeLoad()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var census = Census(host.Incidence, a, d);
        var flipped = new PhysicalWorkerRuntimeState(
            census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var (costs, scales) = DiamondRoads(host.Incidence, a, b, c, d);
        var quotes = Quotes(census, Demand(One), Tile(host.Incidence, d));
        var first = FoundationPhysicalMultiPathFreightPlanner.Plan(
            census, host.Incidence, Demand(One), Cargo(One),
            quotes, costs, scales, 8, 32);
        var second = FoundationPhysicalMultiPathFreightPlanner.Plan(
            flipped, host.Incidence, Demand(One), Cargo(One),
            quotes.Reverse(), costs.Reverse(), scales.Reverse(), 8, 32);
        Assert.Equal(first.ProjectedEdgeCosts.ToArray(),
            second.ProjectedEdgeCosts.ToArray());
        Assert.Equal(first.RoutingBatches, second.RoutingBatches);
        Assert.Equal(first.Parts.Select(x => (
            x.ParentDeliveryOrdinal, x.Delivery.DeliveryOrdinal,
            x.Delivery.GoodsMicro, x.CargoMicro,
            string.Join(",", x.Delivery.RouteTiles.Select(tile =>
                tile.FineStrategicCellId.Value)))),
            second.Parts.Select(x => (
            x.ParentDeliveryOrdinal, x.Delivery.DeliveryOrdinal,
            x.Delivery.GoodsMicro, x.CargoMicro,
            string.Join(",", x.Delivery.RouteTiles.Select(tile =>
                tile.FineStrategicCellId.Value)))));
    }

    [Fact]
    public void DistributedRealWorldCanSplitRoutesWithoutWorkerPerRouteExplosion()
    {
        const int count = 128;
        var host = Host();
        var map = host.Incidence;
        var usedTiles = new HashSet<StrategicCellId>();
        var selections = new List<StrategicEdgeId>();
        foreach (var border in map.FineTopology.Edges)
        {
            if (usedTiles.Contains(border.IncidentCellIds[0]) ||
                usedTiles.Contains(border.IncidentCellIds[1]))
                continue;
            usedTiles.Add(border.IncidentCellIds[0]);
            usedTiles.Add(border.IncidentCellIds[1]);
            selections.Add(border.Id);
            if (selections.Count == count) break;
        }
        Assert.Equal(count, selections.Count);
        var workers = new List<PhysicalWorkerRuntimeRecord>();
        var roads = new List<FoundationPhysicalRoutingEdgeCost>();
        var scales = new List<FoundationPhysicalEdgeCarryingScale>();
        var preferred = new HashSet<PhysicalTacticalTileId>();
        for (int i = 0; i < count; i++)
        {
            var edge = map.FineTopology.Edges[
                checked((int)selections[i].Value - 1)];
            var a = edge.IncidentCellIds[0];
            var b = edge.IncidentCellIds[1];
            var source = Tile(map, a);
            var destination = Tile(map, b);
            preferred.Add(destination);
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(2 * i + 1)),
                Red, Coarse(map, a), One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                source, PhysicalWorkerOccupation.Agriculture));
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(2 * i + 2)),
                Blue, Coarse(map, b), One, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                destination, PhysicalWorkerOccupation.Unassigned));
            var crossing = new PhysicalTacticalEdgeId(
                map.Refinement.FineParameters, edge.Id);
            roads.Add(Road(crossing));
            scales.Add(Scale(crossing, 100_000_000UL));
        }
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var policy = Demand(One);
        var markets = FoundationPhysicalCommodityDemandProjector.Project(
            census, policy);
        var quotes = markets.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                preferred.Contains(line.Tile) ? 1_000_000_000UL :
                    1_000_000UL));
        var timer = Stopwatch.StartNew();
        var result = FoundationPhysicalMultiPathFreightPlanner.Plan(
            census, map, policy, Cargo(One), quotes, roads, scales,
            maxProducerPasses: 4, maxBatchesPerDelivery: 8);
        timer.Stop();
        _output.WriteLine(
            $"Multi-path sparse scale: producers={count}, markets=" +
            $"{markets.DemandByCivilization.Count}, roads={roads.Count}, " +
            $"workerCount={census.Workers.Count}, parts={result.Parts.Count}, " +
            $"batches={result.RoutingBatches}, elapsedMs={timer.ElapsedMilliseconds}");
        Assert.Equal(count, result.MarketAllocation.ProductiveLotCount);
        Assert.Equal(count * 2, result.MarketAllocation.MarketTotals.Count);
        Assert.Equal((ulong)count * 1_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(result.AllocationAudit.TotalAvailableGoodsMicro,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(count, result.AllocationAudit.Freight.UsedEdges.Count);
        Assert.Equal(count, result.ProjectedEdgeCosts.Count);
        Assert.True(result.Parts.Count <= result.RoutingBatches);
    }
}
