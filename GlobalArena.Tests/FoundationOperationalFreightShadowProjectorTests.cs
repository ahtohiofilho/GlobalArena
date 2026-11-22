using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationOperationalFreightShadowProjectorTests
{
    private static PhysicalTacticalEdgeId[] RealEdges()
    {
        var fine = new GoldbergParameters(18, 0);
        var topology = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0), fine));
        return topology.FineTopology.Edges.Take(3)
            .Select(x => new PhysicalTacticalEdgeId(fine, x.Id)).ToArray();
    }

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge, ulong freight, ulong tariff = 0UL) =>
        new(edge, FoundationInfrastructureOwnershipKind.Owned,
            new CivilizationId(1UL), freight, tariff);

    private static FoundationOperationalServiceResult Service(
        PhysicalTacticalEdgeId[] edges,
        ulong landEffort, ulong portEffort)
    {
        var fine = new GoldbergParameters(18, 0);
        var allocation = FoundationOperationalWorkAllocator.Allocate(
            landEffort, portEffort,
            new[]
            {
                new FoundationOperationalWorkEdge(
                    edges[0], FoundationOperationalWorkKind.Land,
                    1_000_000UL, 0UL),
                new FoundationOperationalWorkEdge(
                    edges[1], FoundationOperationalWorkKind.Port,
                    1_000_000UL, 0UL)
            });
        return FoundationOperationalServiceProjector.Project(
            new[]
            {
                new FoundationPhysicalOperationalNodeAllocation(
                    new PhysicalTacticalTileId(fine, new StrategicCellId(1UL)),
                    new CivilizationId(1UL), allocation)
            },
            new[]
            {
                new FoundationOperationalEdgeProductivity(
                    edges[0], FoundationOperationalWorkKind.Land,
                    1_000_000UL),
                new FoundationOperationalEdgeProductivity(
                    edges[1], FoundationOperationalWorkKind.Port,
                    1_000_000UL)
            });
    }

    [Fact]
    public void ShadowQuotesOnlyDiscountEdgesWithActualServiceAndPreserveAllTariffs()
    {
        var e = RealEdges();
        var roads = new[]
        {
            Road(e[0], 500_000UL, 120_000UL),
            Road(e[1], 900_000UL, 240_000UL),
            Road(e[2], 700_000UL, 360_000UL)
        };
        var result = FoundationOperationalFreightShadowProjector.Project(
            roads, Service(e, 2_000_000UL, 1_000_000UL),
            new FoundationOperationalFreightShadowPolicy(
                2_000_000UL, 3_000_000UL));
        Assert.Equal(250_000UL, result.CounterfactualRoads[0]
            .PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(675_000UL, result.CounterfactualRoads[1]
            .PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(700_000UL, result.CounterfactualRoads[2]
            .PhysicalCostMicroPerWholeCargoUnit);
        for (int i = 0; i < roads.Length; i++)
        {
            Assert.Equal(roads[i].PhysicalCostMicroPerWholeCargoUnit,
                result.EdgeReadings[i].OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(roads[i].ExternalTariffMicroPerWholeCargoUnit,
                result.CounterfactualRoads[i].ExternalTariffMicroPerWholeCargoUnit);
            Assert.Equal(roads[i].InfrastructureOwner,
                result.CounterfactualRoads[i].InfrastructureOwner);
            Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
                result.CounterfactualRoads[i].OwnershipKind);
        }
        Assert.Equal(500_000UL, roads[0].PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(900_000UL, roads[1].PhysicalCostMicroPerWholeCargoUnit);
    }

    [Fact]
    public void NoWorkerServiceCannotCreateFreeFreightBenefitFromCapitalAlone()
    {
        var e = RealEdges();
        var roads = new[]
        {
            Road(e[0], 220_000UL),
            Road(e[1], 650_000UL),
            Road(e[2], 850_000UL)
        };
        var service = FoundationOperationalServiceProjector.Project(
            Array.Empty<FoundationPhysicalOperationalNodeAllocation>(),
            Array.Empty<FoundationOperationalEdgeProductivity>());
        var result = FoundationOperationalFreightShadowProjector.Project(
            roads, service, new FoundationOperationalFreightShadowPolicy(
                1_000_000UL, 1_000_000UL));
        Assert.Equal(roads, result.CounterfactualRoads);
        Assert.All(result.EdgeReadings, reading =>
            Assert.Equal(0UL, reading.OperationalServiceMicro));
    }

    [Fact]
    public void HigherEffortProducesNonIncreasingFixedFreightWithoutLoadFeedback()
    {
        var e = RealEdges();
        var roads = new[] { Road(e[0], 800_000UL), Road(e[1], 600_000UL) };
        var policy = new FoundationOperationalFreightShadowPolicy(
            2_000_000UL, 2_000_000UL);
        var first = FoundationOperationalFreightShadowProjector.Project(
            roads, Service(e, 1_000_000UL, 0UL), policy);
        var second = FoundationOperationalFreightShadowProjector.Project(
            roads, Service(e, 2_000_000UL, 0UL), policy);
        Assert.Equal(533_333UL,
            first.CounterfactualRoads[0].PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(400_000UL,
            second.CounterfactualRoads[0].PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(600_000UL,
            first.CounterfactualRoads[1].PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(600_000UL,
            second.CounterfactualRoads[1].PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(0UL, second.EdgeReadings[1].OperationalServiceMicro);
        // Cargo volumes and routing results are not inputs to either quote.
        Assert.Equal(800_000UL, roads[0].PhysicalCostMicroPerWholeCargoUnit);
    }

    [Fact]
    public void RejectsMissingRoadDuplicateRoadAndInvalidReferenceScales()
    {
        var e = RealEdges();
        var service = Service(e, 1_000_000UL, 0UL);
        var policy = new FoundationOperationalFreightShadowPolicy(
            1_000_000UL, 1_000_000UL);
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalFreightShadowProjector.Project(
                new[] { Road(e[1], 500_000UL) }, service, policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalFreightShadowProjector.Project(
                new[] { Road(e[0], 500_000UL), Road(e[0], 500_000UL) },
                service, policy));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationOperationalFreightShadowProjector.Project(
                new[] { Road(e[0], 500_000UL) }, service,
                new FoundationOperationalFreightShadowPolicy(
                    0UL, 1_000_000UL)));
    }
}
