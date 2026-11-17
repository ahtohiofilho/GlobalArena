using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalCongestionStabilityScaleTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Item = new(1U);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalCongestionStabilityScaleTests(
        ITestOutputHelper output) => _output = output;

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Item },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .Select(profile =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, Item, 1_000_000UL)));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("fixture-shared-cargo-micro", new[]
        {
            new FoundationCommodityCargoConversion(Item, 1_000_000UL)
        });

    private static PhysicalTacticalTileId Tile(
        PhysicalTacticalIncidenceMap map, StrategicCellId id) =>
        new(map.Refinement.FineParameters, id);

    private static PhysicalTacticalEdgeId Edge(
        PhysicalTacticalIncidenceMap map, StrategicCellId a, StrategicCellId b)
    {
        var found = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(a) &&
            edge.IncidentCellIds.Contains(b));
        return new PhysicalTacticalEdgeId(map.Refinement.FineParameters, found.Id);
    }

    private static StrategicCellId Coarse(
        PhysicalTacticalIncidenceMap map, StrategicCellId fineId) =>
        map.TileIncidences[checked((int)fineId.Value - 1)]
            .IncidentCoarseCellIds[0];

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge) =>
        new(edge, FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, 100_000UL, 0UL);

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState workers,
        FoundationTacticalHumanAiTurnHost host,
        PhysicalTacticalTileId preferred) =>
        FoundationPhysicalCommodityDemandProjector.Project(workers, Demand())
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Tile == preferred ? 1_000_000_000UL : 1_000_000UL))
            .ToArray();

    private static (StrategicCellId A, StrategicCellId B,
        StrategicCellId C, StrategicCellId D) Diamond(
        PhysicalTacticalIncidenceMap map)
    {
        foreach (var a in map.FineTopology.Cells)
        {
            foreach (var b in a.AdjacentCellIds)
            {
                var neighborsB = map.FineTopology.Cells[
                    checked((int)b.Value - 1)].AdjacentCellIds;
                foreach (var c in a.AdjacentCellIds.Where(x => x != b))
                {
                    var neighborsC = map.FineTopology.Cells[
                        checked((int)c.Value - 1)].AdjacentCellIds;
                    var d = neighborsB.FirstOrDefault(id =>
                        id != a.Id && id != b && id != c &&
                        neighborsC.Contains(id));
                    if (d.IsValid)
                        return (a.Id, b, c, d);
                }
            }
        }
        throw new InvalidOperationException(
            "Goldberg topology lacks two distinct two-hop physical paths.");
    }

    [Fact]
    public void RelaxationIsMicroExactAndCannotPretendAChangedRateIsStable()
    {
        Assert.Equal(102UL,
            FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                100UL, 107UL, 4U));
        Assert.Equal(98UL,
            FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                100UL, 93UL, 4U));
        Assert.Equal(101UL,
            FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                100UL, 101UL, 256U));
        Assert.Equal(99UL,
            FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                100UL, 99UL, 256U));
        Assert.Equal(100UL,
            FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                100UL, 100UL, 256U));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.RelaxCostTowards(
                100UL, 100UL, 0U));
    }

    [Fact]
    public void UnderRelaxedFeedbackCanReachExactCostAndCargoAgreement()
    {
        var host = Host();
        var first = host.Incidence.FineTopology.Edges[0];
        var a = Tile(host.Incidence, first.IncidentCellIds[0]);
        var b = Tile(host.Incidence, first.IncidentCellIds[1]);
        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red,
                Coarse(host.Incidence, first.IncidentCellIds[0]),
                Item, 1_000_000UL, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue,
                Coarse(host.Incidence, first.IncidentCellIds[1]),
                Item, 0UL, PhysicalWorkerActivity.Other,
                1_000_000U, b, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var physicalEdge = new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters, first.Id);
        var result = FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            workers, host.Incidence, Demand(), Cargo(),
            Quotes(workers, host, b), new[] { Road(physicalEdge) },
            new[] { new FoundationPhysicalEdgeCarryingScale(
                physicalEdge, 1_000_000UL) },
            maxProducerPasses: 4, maxCongestionIterations: 64,
            relaxation: new FoundationPhysicalCongestionRelaxationPolicy(
                4U, 4U));
        var reading = Assert.Single(result.Edges);
        Assert.True(result.ConsistentWithFinalEdgeLoads);
        Assert.Equal(4U, result.FinalRelaxationDenominator);
        Assert.Equal(0, result.AdaptiveRestarts);
        Assert.InRange(result.CongestionIterations, 3, 64);
        Assert.Equal(
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                reading.FreePhysicalCostMicroPerWholeCargoUnit,
                reading.CargoMicro, reading.CargoMicroAtReferenceLoad),
            reading.CongestedPhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(1_000_000UL,
            result.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void DiamondRouteFlippingMustFailClosedEvenAfterAllowedAdaptiveRestarts()
    {
        var host = Host();
        var (a, b, c, d) = Diamond(host.Incidence);
        var from = Tile(host.Incidence, a);
        var destination = Tile(host.Incidence, d);
        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red,
                Coarse(host.Incidence, a), Item, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                from, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue,
                Coarse(host.Incidence, d), Item, 0UL,
                PhysicalWorkerActivity.Traveling, 1_000_000U,
                destination, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var edgeCosts = new[]
        {
            Road(Edge(host.Incidence, a, b)),
            Road(Edge(host.Incidence, b, d)),
            Road(Edge(host.Incidence, a, c)),
            Road(Edge(host.Incidence, c, d))
        };
        var scales = edgeCosts.Select(e =>
            new FoundationPhysicalEdgeCarryingScale(
                e.Edge, 1_000_000UL)).ToArray();
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
                census, host.Incidence, Demand(), Cargo(),
                Quotes(census, host, destination),
                edgeCosts, scales, maxProducerPasses: 4,
                maxCongestionIterations: 36,
                relaxation: new FoundationPhysicalCongestionRelaxationPolicy(
                    1U, 8U)));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void RelaxationPolicyAndBudgetsAreExplicitlyValidated()
    {
        var host = Host();
        var census = host.Snapshot.Workers;
        var market = FoundationPhysicalCommodityDemandProjector.Project(
            census, Demand());
        var quotes = market.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL));
        foreach (var bad in new[]
        {
            new FoundationPhysicalCongestionRelaxationPolicy(0U, 8U),
            new FoundationPhysicalCongestionRelaxationPolicy(8U, 4U)
        })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
                    census, host.Incidence, Demand(), Cargo(), quotes,
                    Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
                    Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
                    4, 4, bad));
    }

    [Fact]
    public void FiveHundredTwelveDistinctPhysicalProducerRoutesAndOverThousandMarkets()
    {
        const int pairCount = 512;
        var host = Host();
        var map = host.Incidence;
        var fine = map.Refinement.FineParameters;
        var selected = new List<StrategicEdgeId>(pairCount);
        var occupied = new HashSet<StrategicCellId>();
        foreach (var border in map.FineTopology.Edges)
        {
            var a = border.IncidentCellIds[0];
            var b = border.IncidentCellIds[1];
            if (occupied.Contains(a) || occupied.Contains(b))
                continue;
            occupied.Add(a);
            occupied.Add(b);
            selected.Add(border.Id);
            if (selected.Count == pairCount) break;
        }
        Assert.Equal(pairCount, selected.Count);
        Assert.Equal(pairCount * 2, occupied.Count);

        var workers = new List<PhysicalWorkerRuntimeRecord>(pairCount * 2);
        var roads = new List<FoundationPhysicalRoutingEdgeCost>(pairCount);
        var capacities = new List<FoundationPhysicalEdgeCarryingScale>(pairCount);
        var preferred = new HashSet<PhysicalTacticalTileId>();
        for (var i = 0; i < selected.Count; i++)
        {
            var border = map.FineTopology.Edges[
                checked((int)selected[i].Value - 1)];
            var a = border.IncidentCellIds[0];
            var b = border.IncidentCellIds[1];
            var sourceTile = new PhysicalTacticalTileId(fine, a);
            var buyerTile = new PhysicalTacticalTileId(fine, b);
            preferred.Add(buyerTile);
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(i * 2 + 1)),
                Red, Coarse(map, a), Item, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                sourceTile, PhysicalWorkerOccupation.Agriculture));
            workers.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(i * 2 + 2)),
                Blue, Coarse(map, b), Item, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                buyerTile, PhysicalWorkerOccupation.Unassigned));
            var edge = new PhysicalTacticalEdgeId(fine, border.Id);
            roads.Add(Road(edge));
            capacities.Add(new FoundationPhysicalEdgeCarryingScale(
                edge, 100_000_000UL));
        }
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var markets = FoundationPhysicalCommodityDemandProjector.Project(
            census, Demand());
        Assert.Equal(pairCount, markets.SupplyLots.Count);
        Assert.Equal(pairCount * 2, markets.DemandByCivilization.Count);
        var quotes = markets.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                preferred.Contains(line.Tile)
                    ? 1_000_000_000UL : 1_000_000UL)).ToArray();

        var timer = Stopwatch.StartNew();
        var plan = FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            census, map, Demand(), Cargo(), quotes,
            roads, capacities, maxProducerPasses: 4,
            maxCongestionIterations: 4);
        timer.Stop();
        _output.WriteLine(
            $"Distributed topology: fine tiles={map.FineTopology.Cells.Count}, " +
            $"physical roads={pairCount}, producers={pairCount}, " +
            $"markets={markets.DemandByCivilization.Count}, " +
            $"workers={census.Workers.Count}, elapsedMs={timer.ElapsedMilliseconds}, " +
            $"feedbackPasses={plan.CongestionIterations}");

        Assert.Equal(pairCount,
            plan.Distribution.ProductiveLotCount);
        Assert.Equal(pairCount,
            plan.Edges.Count);
        Assert.Equal(pairCount * 2,
            plan.Distribution.MarketTotals.Count);
        Assert.Equal((ulong)pairCount * 1_000_000UL,
            plan.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(plan.Distribution.AllocationAudit.TotalAvailableGoodsMicro,
            plan.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(pairCount, plan.Distribution.AllocationAudit.Freight.UsedEdges.Count);
        Assert.All(plan.Edges, reading =>
        {
            Assert.True(reading.CargoMicro > 0UL);
            Assert.Equal(100_000_000UL,
                reading.CargoMicroAtReferenceLoad);
            Assert.Equal(
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    reading.FreePhysicalCostMicroPerWholeCargoUnit,
                    reading.CargoMicro, reading.CargoMicroAtReferenceLoad),
                reading.CongestedPhysicalCostMicroPerWholeCargoUnit);
        });
        Assert.True(plan.ConsistentWithFinalEdgeLoads);
    }
}
