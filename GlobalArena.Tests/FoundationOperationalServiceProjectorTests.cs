using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationOperationalServiceProjectorTests
{
    private static PhysicalTacticalEdgeId[] Edges()
    {
        var fine = new GoldbergParameters(18, 0);
        var topology = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0), fine));
        return topology.FineTopology.Edges.Take(4)
            .Select(edge => new PhysicalTacticalEdgeId(fine, edge.Id)).ToArray();
    }

    [Fact]
    public void ServiceSumsRealTerminalWorkBeforeConversionWithoutCongestion()
    {
        var e = Edges();
        var land = new[]
        {
            new FoundationOperationalWorkEdge(e[0],
                FoundationOperationalWorkKind.Land, 1UL, 0UL),
            new FoundationOperationalWorkEdge(e[1],
                FoundationOperationalWorkKind.Land, 1UL, 2UL),
            new FoundationOperationalWorkEdge(e[2],
                FoundationOperationalWorkKind.Port, 1UL, 0UL)
        };
        var fine = new GoldbergParameters(18, 0);
        var nodes = new[]
        {
            new FoundationPhysicalOperationalNodeAllocation(
                new PhysicalTacticalTileId(fine, new StrategicCellId(1UL)),
                new CivilizationId(1UL),
                FoundationOperationalWorkAllocator.Allocate(
                    4_000_000UL, 2_000_000UL, land)),
            new FoundationPhysicalOperationalNodeAllocation(
                new PhysicalTacticalTileId(fine, new StrategicCellId(2UL)),
                new CivilizationId(2UL),
                FoundationOperationalWorkAllocator.Allocate(
                    1_000_000UL, 0UL, land.Take(1).ToArray()))
        };
        var productivity = new[]
        {
            new FoundationOperationalEdgeProductivity(
                e[0], FoundationOperationalWorkKind.Land, 2_000_000UL),
            new FoundationOperationalEdgeProductivity(
                e[1], FoundationOperationalWorkKind.Land, 1_000_000UL),
            new FoundationOperationalEdgeProductivity(
                e[2], FoundationOperationalWorkKind.Port, 3_000_000UL)
        };
        var result = FoundationOperationalServiceProjector.Project(
            nodes, productivity);
        Assert.Equal(7_000_000UL, result.TotalAllocatedEffortMicro);
        Assert.Equal(0UL, result.TotalIdleEffortMicro);
        Assert.Equal(new System.Numerics.BigInteger(13_000_000UL),
            result.TotalServiceMicro);
        Assert.Equal(3, result.Edges.Count);
        Assert.Equal(2_000_000UL, result.Edges.Single(x =>
            x.Edge == e[0]).EffortMicro);
        Assert.Equal(4_000_000UL, result.Edges.Single(x =>
            x.Edge == e[0]).ServiceMicro);
        Assert.Equal(2, result.Edges.Single(x =>
            x.Edge == e[0]).ContributingTerminals);
        Assert.Equal(3_000_000UL, result.Edges.Single(x =>
            x.Edge == e[1]).ServiceMicro);
        Assert.Equal(6_000_000UL, result.Edges.Single(x =>
            x.Edge == e[2]).ServiceMicro);

        var reordered = FoundationOperationalServiceProjector.Project(
            nodes.Reverse().ToArray(), productivity.Reverse().ToArray());
        Assert.Equal(result.Edges, reordered.Edges);
        Assert.Equal(result.TotalServiceMicro, reordered.TotalServiceMicro);
    }

    [Fact]
    public void NoOperationalWorkersMeansZeroServiceNotFreeCapacity()
    {
        var result = FoundationOperationalServiceProjector.Project(
            Array.Empty<FoundationPhysicalOperationalNodeAllocation>(),
            Array.Empty<FoundationOperationalEdgeProductivity>());
        Assert.Empty(result.Edges);
        Assert.Equal(0UL, result.TotalAllocatedEffortMicro);
        Assert.Equal(0UL, result.TotalIdleEffortMicro);
        Assert.Equal(System.Numerics.BigInteger.Zero, result.TotalServiceMicro);
    }

    [Fact]
    public void OrphanedWorkIsIdleAndDoesNotBecomeTransportService()
    {
        var fine = new GoldbergParameters(18, 0);
        var nodes = new[]
        {
            new FoundationPhysicalOperationalNodeAllocation(
                new PhysicalTacticalTileId(fine, new StrategicCellId(1UL)),
                new CivilizationId(1UL),
                FoundationOperationalWorkAllocator.Allocate(
                    1_000_000UL, 2_000_000UL,
                    Array.Empty<FoundationOperationalWorkEdge>()))
        };
        var result = FoundationOperationalServiceProjector.Project(
            nodes, Array.Empty<FoundationOperationalEdgeProductivity>());
        Assert.Empty(result.Edges);
        Assert.Equal(0UL, result.TotalAllocatedEffortMicro);
        Assert.Equal(3_000_000UL, result.TotalIdleEffortMicro);
        Assert.Equal(0UL, result.TotalServiceMicro);
    }

    [Fact]
    public void PositiveWorkNeedsAnExplicitMatchingServiceFactor()
    {
        var edge = Edges()[0];
        var fine = new GoldbergParameters(18, 0);
        var nodes = new[]
        {
            new FoundationPhysicalOperationalNodeAllocation(
                new PhysicalTacticalTileId(fine, new StrategicCellId(1UL)),
                new CivilizationId(1UL),
                FoundationOperationalWorkAllocator.Allocate(
                    1_000_000UL, 0UL, new[]
                    {
                        new FoundationOperationalWorkEdge(
                            edge, FoundationOperationalWorkKind.Land,
                            1UL, 0UL)
                    }))
        };
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalServiceProjector.Project(
                nodes, Array.Empty<FoundationOperationalEdgeProductivity>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalServiceProjector.Project(
                nodes, new[]
                {
                    new FoundationOperationalEdgeProductivity(
                        edge, FoundationOperationalWorkKind.Port,
                        1_000_000UL)
                }));
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalServiceProjector.Project(
                nodes, new[]
                {
                    new FoundationOperationalEdgeProductivity(
                        edge, FoundationOperationalWorkKind.Land,
                        1_000_000UL),
                    new FoundationOperationalEdgeProductivity(
                        edge, FoundationOperationalWorkKind.Land,
                        1_000_000UL)
                }));
    }

    [Fact]
    public void FractionalWorkFromTwoTerminalsRoundsOnlyOnce()
    {
        var edge = Edges()[0];
        var fine = new GoldbergParameters(18, 0);
        var input = new[] {
            new FoundationOperationalWorkEdge(edge,
                FoundationOperationalWorkKind.Land, 1UL, 0UL)
        };
        var nodes = new[] {1UL, 2UL}.Select(n =>
            new FoundationPhysicalOperationalNodeAllocation(
                new PhysicalTacticalTileId(fine, new StrategicCellId(n)),
                new CivilizationId(1UL),
                FoundationOperationalWorkAllocator.Allocate(
                    1UL, 0UL, input))).ToArray();
        // Each endpoint individually produces 0 when truncated; aggregate
        // 2 effort micro before multiplication to preserve 1 service micro.
        var result = FoundationOperationalServiceProjector.Project(nodes,
            new[] {new FoundationOperationalEdgeProductivity(
                edge, FoundationOperationalWorkKind.Land, 500_000UL)});
        Assert.Single(result.Edges);
        Assert.Equal(2UL, result.Edges[0].EffortMicro);
        Assert.Equal(1UL, result.Edges[0].ServiceMicro);
    }
}
