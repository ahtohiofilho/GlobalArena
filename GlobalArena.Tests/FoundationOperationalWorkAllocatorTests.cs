using GlobalArena.Kernel;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationOperationalWorkAllocatorTests
{
    private static PhysicalTacticalEdgeId[] RealEdges()
    {
        var fine = new GoldbergParameters(18, 0);
        var refinement = new GoldbergScaledRefinement(
            new GoldbergParameters(3, 0), fine);
        var topology = PhysicalTacticalIncidenceMapper.Materialize(refinement);
        return topology.FineTopology.Edges
            .OrderBy(edge => edge.Id.Value)
            .Take(4)
            .Select(edge => new PhysicalTacticalEdgeId(fine, edge.Id))
            .ToArray();
    }

    [Fact]
    public void CapitalProportionalAllocationConservesBothIndependentWorkPools()
    {
        var e = RealEdges();
        var edges = new[]
        {
            new FoundationOperationalWorkEdge(e[0],
                FoundationOperationalWorkKind.Land, 1UL, 1UL),
            new FoundationOperationalWorkEdge(e[1],
                FoundationOperationalWorkKind.Land, 1UL, 3UL),
            new FoundationOperationalWorkEdge(e[2],
                FoundationOperationalWorkKind.Port, 1UL, 0UL)
        };
        var result = FoundationOperationalWorkAllocator.Allocate(
            6_000_000UL, 2_000_000UL, edges);
        Assert.Equal(2_000_000UL, result.Shares.Single(
            s => s.Edge == e[0]).EffortMicro);
        Assert.Equal(4_000_000UL, result.Shares.Single(
            s => s.Edge == e[1]).EffortMicro);
        Assert.Equal(2_000_000UL, result.Shares.Single(
            s => s.Edge == e[2]).EffortMicro);
        Assert.Equal(0UL, result.UnallocatedLandEffortMicro);
        Assert.Equal(0UL, result.UnallocatedPortEffortMicro);
    }

    [Fact]
    public void NaturalBaselineWorksWithoutInvestedCapitalAndWithoutAnyCargo()
    {
        var e = RealEdges();
        var edges = new[]
        {
            new FoundationOperationalWorkEdge(e[0],
                FoundationOperationalWorkKind.Land, 1UL, 0UL),
            new FoundationOperationalWorkEdge(e[1],
                FoundationOperationalWorkKind.Land, 1UL, 0UL),
            new FoundationOperationalWorkEdge(e[2],
                FoundationOperationalWorkKind.Land, 1UL, 0UL)
        };
        var a = FoundationOperationalWorkAllocator.Allocate(2UL, 0UL, edges);
        var b = FoundationOperationalWorkAllocator.Allocate(
            2UL, 0UL, edges.Reverse().ToArray());
        Assert.Equal(2UL, a.Shares.Aggregate(0UL,
            (total, share) => checked(total + share.EffortMicro)));
        Assert.Equal(a.Shares, b.Shares);
        Assert.Equal(1UL, a.Shares[0].EffortMicro);
        Assert.Equal(1UL, a.Shares[1].EffortMicro);
        Assert.Equal(0UL, a.Shares[2].EffortMicro);
    }

    [Fact]
    public void NoCrossingDoesNotCreatePhantomWorkOrPhantomEdges()
    {
        var a = FoundationOperationalWorkAllocator.Allocate(
            7UL, 11UL, Array.Empty<FoundationOperationalWorkEdge>());
        Assert.Empty(a.Shares);
        Assert.Equal(7UL, a.UnallocatedLandEffortMicro);
        Assert.Equal(11UL, a.UnallocatedPortEffortMicro);
    }

    [Fact]
    public void CapitalCannotDuplicateWorkAndDuplicateEdgesAreRejected()
    {
        var e = RealEdges();
        var edges = new[]
        {
            new FoundationOperationalWorkEdge(e[0],
                FoundationOperationalWorkKind.Land, 1UL, ulong.MaxValue),
            new FoundationOperationalWorkEdge(e[1],
                FoundationOperationalWorkKind.Land, 1UL, 0UL)
        };
        var result = FoundationOperationalWorkAllocator.Allocate(
            ulong.MaxValue, 0UL, edges);
        Assert.Equal(ulong.MaxValue, result.Shares.Aggregate(0UL,
            (sum, item) => checked(sum + item.EffortMicro)));
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalWorkAllocator.Allocate(1UL, 0UL,
                new[] { edges[0], edges[0] }));
    }
}
