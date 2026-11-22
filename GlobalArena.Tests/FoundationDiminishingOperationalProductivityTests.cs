using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationDiminishingOperationalProductivityTests
{
    private static PhysicalTacticalEdgeId[] Edges()
    {
        var fine = new GoldbergParameters(18, 0);
        var topology = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0), fine));
        return topology.FineTopology.Edges.Take(3)
            .Select(edge => new PhysicalTacticalEdgeId(fine, edge.Id)).ToArray();
    }

    private static PhysicalTransportEdgeRuntimeRecord Edge(
        PhysicalTacticalEdgeId edge, ulong quote, ulong investment) =>
        new(edge, PhysicalTransportOwnershipKind.NoInfrastructure,
            null, 250_000UL, 1_000_000UL, 0UL,
            quote, investment);

    private static FoundationOperationalProductivityPolicy Policy() =>
        new(new FoundationOperationalProductivityBand(
                1_000_000UL, 10_000U),
            new FoundationOperationalProductivityBand(
                2_000_000UL, 5_000U));

    [Fact]
    public void CapitalHasStrictlyDiminishingMarginalServicePerEffort()
    {
        var edge = Edges()[0];
        var policy = Policy();
        ulong[] investments =
        {
            0UL, 2_000_000UL, 4_000_000UL, 6_000_000UL, 8_000_000UL
        };
        var rates = investments.Select(k =>
            FoundationDiminishingOperationalProductivityProjector
                .Project(Edge(edge, 2_000_000UL, k),
                    FoundationOperationalWorkKind.Land, policy)
                .ServiceMicroPerWholeEffort).ToArray();

        Assert.Equal(1_000_000UL, rates[0]);
        Assert.Equal(1_500_000UL, rates[1]);
        Assert.Equal(1_666_666UL, rates[2]);
        Assert.Equal(1_750_000UL, rates[3]);
        Assert.Equal(1_800_000UL, rates[4]);
        for (int i = 1; i < rates.Length; i++)
            Assert.True(rates[i] > rates[i - 1]);
        for (int i = 2; i < rates.Length; i++)
            Assert.True(rates[i] - rates[i - 1] <
                rates[i - 1] - rates[i - 2]);
    }

    [Fact]
    public void ConstructionQuoteIsCostNotAFreeProductivityBonus()
    {
        var e = Edges();
        var policy = Policy();
        var cheap = FoundationDiminishingOperationalProductivityProjector.Project(
            Edge(e[0], 1_000_000UL, 0UL),
            FoundationOperationalWorkKind.Land, policy);
        var expensive = FoundationDiminishingOperationalProductivityProjector.Project(
            Edge(e[1], 9_000_000UL, 0UL),
            FoundationOperationalWorkKind.Land, policy);
        Assert.Equal(cheap.ServiceMicroPerWholeEffort,
            expensive.ServiceMicroPerWholeEffort);
        var cheaperUpgrade = FoundationDiminishingOperationalProductivityProjector
            .Project(Edge(e[0], 1_000_000UL, 1_000_000UL),
                FoundationOperationalWorkKind.Land, policy);
        var expensiveUpgrade = FoundationDiminishingOperationalProductivityProjector
            .Project(Edge(e[1], 9_000_000UL, 1_000_000UL),
                FoundationOperationalWorkKind.Land, policy);
        Assert.True(cheaperUpgrade.ServiceMicroPerWholeEffort >
            expensiveUpgrade.ServiceMicroPerWholeEffort);
    }

    [Fact]
    public void LandAndPortUseExplicitlyIndependentRates()
    {
        var edge = Edges()[0];
        var policy = Policy();
        var row = Edge(edge, 2_000_000UL, 2_000_000UL);
        var land = FoundationDiminishingOperationalProductivityProjector.Project(
            row, FoundationOperationalWorkKind.Land, policy);
        var port = FoundationDiminishingOperationalProductivityProjector.Project(
            row, FoundationOperationalWorkKind.Port, policy);
        Assert.Equal(1_500_000UL, land.ServiceMicroPerWholeEffort);
        Assert.Equal(2_500_000UL, port.ServiceMicroPerWholeEffort);
        Assert.Throws<ArgumentException>(() =>
            new FoundationOperationalProductivityPolicy(
                new FoundationOperationalProductivityBand(0UL, 10_000U),
                policy.Port));
        Assert.Throws<ArgumentException>(() =>
            new FoundationOperationalProductivityPolicy(
                policy.Land,
                new FoundationOperationalProductivityBand(1UL, 0U)));
    }

    [Fact]
    public void FixedPhysicalWorkGetsMoreServiceWithoutCreatingWorkerEffort()
    {
        var edge = Edges()[0];
        var fine = new GoldbergParameters(18, 0);
        var policy = Policy();
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
                            1_000_000UL, 0UL)
                    }))
        };
        var natural = FoundationOperationalServiceProjector.Project(nodes,
            new[] {FoundationDiminishingOperationalProductivityProjector.Project(
                Edge(edge, 2_000_000UL, 0UL),
                FoundationOperationalWorkKind.Land, policy)});
        var upgraded = FoundationOperationalServiceProjector.Project(nodes,
            new[] {FoundationDiminishingOperationalProductivityProjector.Project(
                Edge(edge, 2_000_000UL, 2_000_000UL),
                FoundationOperationalWorkKind.Land, policy)});
        Assert.Equal(1_000_000UL, natural.TotalAllocatedEffortMicro);
        Assert.Equal(1_000_000UL, upgraded.TotalAllocatedEffortMicro);
        Assert.Equal(0UL, upgraded.TotalIdleEffortMicro);
        Assert.Equal(new System.Numerics.BigInteger(1_000_000UL),
            natural.TotalServiceMicro);
        Assert.Equal(new System.Numerics.BigInteger(1_500_000UL),
            upgraded.TotalServiceMicro);
    }

    [Fact]
    public void MissingQuoteOpenSeaInvalidKindAndOverflowAreRejected()
    {
        var edge = Edges()[0];
        var policy = Policy();
        Assert.Throws<ArgumentException>(() =>
            FoundationDiminishingOperationalProductivityProjector.Project(
                Edge(edge, 0UL, 1_000_000UL),
                FoundationOperationalWorkKind.Land, policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationDiminishingOperationalProductivityProjector.Project(
                Edge(edge, 1_000_000UL, 0UL) with
                { CargoMicroAtReferenceLoad = ulong.MaxValue },
                FoundationOperationalWorkKind.Port, policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationDiminishingOperationalProductivityProjector.Project(
                Edge(edge, 1_000_000UL, 0UL),
                (FoundationOperationalWorkKind)9, policy));
        var extreme = new FoundationOperationalProductivityPolicy(
            new FoundationOperationalProductivityBand(
                ulong.MaxValue, uint.MaxValue), Policy().Port);
        Assert.Throws<OverflowException>(() =>
            FoundationDiminishingOperationalProductivityProjector.Project(
                Edge(edge, 1_000_000UL, 1_000_000UL),
                FoundationOperationalWorkKind.Land, extreme));
    }
}
