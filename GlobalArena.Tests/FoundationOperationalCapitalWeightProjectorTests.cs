using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationOperationalCapitalWeightProjectorTests
{
    private static PhysicalTacticalEdgeId[] RealEdges()
    {
        var fine = new GoldbergParameters(18, 0);
        var incidence = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0), fine));
        return incidence.FineTopology.Edges
            .OrderBy(e => e.Id.Value).Take(3)
            .Select(e => new PhysicalTacticalEdgeId(fine, e.Id)).ToArray();
    }

    private static PhysicalTransportEdgeRuntimeRecord Crossing(
        PhysicalTacticalEdgeId edge, ulong quote, ulong investment) =>
        new(edge, PhysicalTransportOwnershipKind.NoInfrastructure,
            null, 250_000UL, 1_000_000UL, 0UL,
            quote, investment);

    [Fact]
    public void ZeroInvestmentHasSameNaturalWeightRegardlessOfConstructionDifficulty()
    {
        var edges = RealEdges();
        var cheap = FoundationOperationalCapitalWeightProjector.Project(
            Crossing(edges[0], 1_000_000UL, 0UL),
            FoundationOperationalWorkKind.Land);
        var expensive = FoundationOperationalCapitalWeightProjector.Project(
            Crossing(edges[1], 10_000_000UL, 0UL),
            FoundationOperationalWorkKind.Land);
        Assert.Equal(1_000_000UL, cheap.NaturalWeightMicro);
        Assert.Equal(cheap.NaturalWeightMicro, expensive.NaturalWeightMicro);
        Assert.Equal(0UL, cheap.InvestedWeightMicro);
        Assert.Equal(0UL, expensive.InvestedWeightMicro);
        var allocation = FoundationOperationalWorkAllocator.Allocate(
            2_000_000UL, 0UL, new[] { cheap, expensive });
        Assert.Equal(1_000_000UL, allocation.Shares[0].EffortMicro);
        Assert.Equal(1_000_000UL, allocation.Shares[1].EffortMicro);
    }

    [Fact]
    public void SameMoneyInvestmentIsNormalizedByOriginalQuote()
    {
        var e = RealEdges();
        var cheap = FoundationOperationalCapitalWeightProjector.Project(
            Crossing(e[0], 2_000_000UL, 100_000UL),
            FoundationOperationalWorkKind.Land);
        var costly = FoundationOperationalCapitalWeightProjector.Project(
            Crossing(e[1], 4_000_000UL, 100_000UL),
            FoundationOperationalWorkKind.Land);
        Assert.Equal(50_000UL, cheap.InvestedWeightMicro);
        Assert.Equal(25_000UL, costly.InvestedWeightMicro);
        var fullQuote = FoundationOperationalCapitalWeightProjector.Project(
            Crossing(e[2], 2_000_000UL, 2_000_000UL),
            FoundationOperationalWorkKind.Port);
        Assert.Equal(1_000_000UL, fullQuote.InvestedWeightMicro);
        Assert.Equal(1_000_000UL, fullQuote.NaturalWeightMicro);
    }

    [Fact]
    public void CapitalChangesOnlyWorkDistributionNotServicePerUnitTwice()
    {
        var e = RealEdges();
        var edges = new[]
        {
            FoundationOperationalCapitalWeightProjector.Project(
                Crossing(e[0], 2_000_000UL, 1_000_000UL),
                FoundationOperationalWorkKind.Land),
            FoundationOperationalCapitalWeightProjector.Project(
                Crossing(e[1], 2_000_000UL, 0UL),
                FoundationOperationalWorkKind.Land)
        };
        var allocation = FoundationOperationalWorkAllocator.Allocate(
            5_000_000UL, 0UL, edges);
        Assert.Equal(3_000_000UL, allocation.Shares[0].EffortMicro);
        Assert.Equal(2_000_000UL, allocation.Shares[1].EffortMicro);

        var result = FoundationOperationalServiceProjector.Project(
            new[]
            {
                new FoundationPhysicalOperationalNodeAllocation(
                    new PhysicalTacticalTileId(new GoldbergParameters(18, 0),
                        new StrategicCellId(1UL)),
                    new CivilizationId(1UL), allocation)
            },
            e.Take(2).Select(edge => new FoundationOperationalEdgeProductivity(
                edge, FoundationOperationalWorkKind.Land, 1_000_000UL)).ToArray());
        Assert.Equal(new System.Numerics.BigInteger(5_000_000UL),
            result.TotalServiceMicro);
        Assert.Equal(5_000_000UL, result.TotalAllocatedEffortMicro);
    }

    [Fact]
    public void RejectsNonphysicalOrUnrepresentableWeights()
    {
        var edge = RealEdges()[0];
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalCapitalWeightProjector.Project(
                Crossing(edge, 0UL, 1UL), FoundationOperationalWorkKind.Land));
        Assert.Throws<ArgumentException>(() =>
            FoundationOperationalCapitalWeightProjector.Project(
                Crossing(edge, 1UL, 1UL) with
                { CargoMicroAtReferenceLoad = ulong.MaxValue },
                FoundationOperationalWorkKind.Port));
        Assert.Throws<OverflowException>(() =>
            FoundationOperationalCapitalWeightProjector.Project(
                Crossing(edge, 1UL, ulong.MaxValue),
                FoundationOperationalWorkKind.Land));
    }
}
