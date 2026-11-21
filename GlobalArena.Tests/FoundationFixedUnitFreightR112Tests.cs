using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// R112 regression: transport unit rates are independent of current cargo
/// and previous-cycle congestion; only settled investment changes the rate.
/// No kilometers or planet-circumference constants enter the economics.
/// </summary>
public sealed class FoundationFixedUnitFreightR112Tests
{
    [Fact]
    public void UninvestedCostIsNaturalAndInvestmentLowersUnitRateMonotonically()
    {
        var edge = new PhysicalTransportEdgeRuntimeRecord(
            new PhysicalTacticalEdgeId(
                new GoldbergParameters(3, 0), new StrategicEdgeId(1UL)),
            PhysicalTransportOwnershipKind.NoInfrastructure,
            null, 250_000UL, 1_000_000UL, 0UL,
            NaturalConstructionCostMoneyMicro: 2_000_000UL);
        Assert.Equal(250_000UL,
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(edge));
        ulong first = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(edge with
            { CommittedInvestmentMoneyMicro = 2_000_000UL });
        ulong second = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(edge with
            { CommittedInvestmentMoneyMicro = 8_000_000UL });
        Assert.InRange(first, 176_775UL, 176_777UL);
        Assert.Equal(111_803UL, second);
        Assert.True(second < first);
        Assert.True(first < 250_000UL);
        Assert.Equal(1UL, FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(edge with
            { CommittedInvestmentMoneyMicro = ulong.MaxValue }));
    }

    [Fact]
    public void FixedRateDoesNotDependOnReferenceCargoOrObservedFlow()
    {
        var edge = new PhysicalTransportEdgeRuntimeRecord(
            new PhysicalTacticalEdgeId(
                new GoldbergParameters(3, 0), new StrategicEdgeId(1UL)),
            PhysicalTransportOwnershipKind.NoInfrastructure,
            null, 500_000UL, 1UL, 0UL,
            NaturalConstructionCostMoneyMicro: 1_000_000UL);
        var tiny = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(edge);
        var huge = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(edge with
            { CargoMicroAtReferenceLoad = 1_000_000_000UL });
        Assert.Equal(500_000UL, tiny);
        Assert.Equal(tiny, huge);
    }
}
