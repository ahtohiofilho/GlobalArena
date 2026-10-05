using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M43CProductionDemandFlowResolutionTests
{
    [Fact]
    public void ProductionPotentialRejectsInvalidPoint()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicProductionPotential(
                default,
                new CommodityId(1U),
                EconomicActivityKind.Agriculture,
                1L));
    }

    [Fact]
    public void ProductionPotentialRejectsInvalidCommodity()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicProductionPotential(
                new EconomicPointId(1UL),
                default,
                EconomicActivityKind.Agriculture,
                1L));
    }

    [Fact]
    public void ProductionPotentialRejectsTradeLogistics()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicProductionPotential(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                EconomicActivityKind.TradeLogistics,
                1L));
    }

    [Fact]
    public void ProductionPotentialRejectsZeroRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicProductionPotential(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                EconomicActivityKind.Agriculture,
                0L));
    }

    [Fact]
    public void ProductionPotentialRejectsRateAboveNormalizedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicProductionPotential(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                EconomicActivityKind.Agriculture,
                StrategicScalarField.Denominator + 1L));
    }

    [Fact]
    public void DemandProfileRejectsInvalidPoint()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicDemandProfile(
                default,
                new CommodityId(1U),
                1L));
    }

    [Fact]
    public void DemandProfileRejectsInvalidCommodity()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicDemandProfile(
                new EconomicPointId(1UL),
                default,
                1L));
    }

    [Fact]
    public void DemandProfileRejectsZeroRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicDemandProfile(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                0L));
    }

    [Fact]
    public void DemandProfileRejectsRateAboveNormalizedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicDemandProfile(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                StrategicScalarField.Denominator + 1L));
    }

    [Fact]
    public void SupplyFlowRejectsZeroQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicSupplyFlow(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                EconomicActivityKind.Agriculture,
                0UL));
    }

    [Fact]
    public void DemandFlowRejectsZeroQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicDemandFlow(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                0UL));
    }

    [Fact]
    public void ResolverRejectsUnknownProductionPoint()
    {
        Assert.Throws<InvalidOperationException>(
            () => new EconomicProductionDemandResolver().Resolve(
                EconomyRuntimeState.Empty,
                new[]
                {
                    Production(
                        1UL,
                        1U,
                        EconomicActivityKind.Agriculture,
                        1L)
                },
                Array.Empty<EconomicDemandProfile>()));
    }

    [Fact]
    public void ResolverRejectsUnknownDemandPoint()
    {
        Assert.Throws<InvalidOperationException>(
            () => new EconomicProductionDemandResolver().Resolve(
                EconomyRuntimeState.Empty,
                Array.Empty<EconomicProductionPotential>(),
                new[]
                {
                    Demand(
                        1UL,
                        1U,
                        1L)
                }));
    }

    [Fact]
    public void ResolverRejectsMissingProductionActivityAllocation()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    2UL,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining,
                            2UL)
                    }));

        Assert.Throws<InvalidOperationException>(
            () => new EconomicProductionDemandResolver().Resolve(
                economy,
                new[]
                {
                    Production(
                        1UL,
                        1U,
                        EconomicActivityKind.Agriculture,
                        1L)
                },
                Array.Empty<EconomicDemandProfile>()));
    }

    [Fact]
    public void ResolverRejectsDuplicateProductionKey()
    {
        var economy =
            Economy(
                PointWithAgriculture(
                    1UL,
                    2UL,
                    2UL));

        var production =
            Production(
                1UL,
                1U,
                EconomicActivityKind.Agriculture,
                1L);

        Assert.Throws<ArgumentException>(
            () => new EconomicProductionDemandResolver().Resolve(
                economy,
                new[]
                {
                    production,
                    production
                },
                Array.Empty<EconomicDemandProfile>()));
    }

    [Fact]
    public void ResolverRejectsDuplicateDemandKey()
    {
        var economy =
            Economy(
                PointWithAgriculture(
                    1UL,
                    2UL,
                    2UL));

        var demand =
            Demand(
                1UL,
                1U,
                1L);

        Assert.Throws<ArgumentException>(
            () => new EconomicProductionDemandResolver().Resolve(
                economy,
                Array.Empty<EconomicProductionPotential>(),
                new[]
                {
                    demand,
                    demand
                }));
    }

    [Fact]
    public void AgricultureSupplyUsesAllocatedWorkforce()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    5UL,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture,
                            2UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.TradeLogistics,
                            1UL)
                    }));

        var result =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                new[]
                {
                    Production(
                        1UL,
                        1U,
                        EconomicActivityKind.Agriculture,
                        500_000L)
                },
                Array.Empty<EconomicDemandProfile>());

        var flow =
            Assert.Single(
                result.SupplyFlows);

        Assert.Equal(
            1_000_000UL,
            flow.RawQuantity);
    }

    [Fact]
    public void MiningSupplyUsesAllocatedWorkforce()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    4UL,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining,
                            3UL)
                    }));

        var result =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                new[]
                {
                    Production(
                        1UL,
                        2U,
                        EconomicActivityKind.Mining,
                        250_000L)
                },
                Array.Empty<EconomicDemandProfile>());

        Assert.Equal(
            750_000UL,
            Assert.Single(result.SupplyFlows).RawQuantity);
    }

    [Fact]
    public void DemandUsesTotalWorkforceIncludingUnallocatedAndLogisticsWorkers()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    5UL,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture,
                            2UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.TradeLogistics,
                            1UL)
                    }));

        var result =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                Array.Empty<EconomicProductionPotential>(),
                new[]
                {
                    Demand(
                        1UL,
                        1U,
                        500_000L)
                });

        Assert.Equal(
            2_500_000UL,
            Assert.Single(result.DemandFlows).RawQuantity);
    }

    [Fact]
    public void ZeroWorkforceDemandEmitsNoFlow()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    0UL));

        var result =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                Array.Empty<EconomicProductionPotential>(),
                new[]
                {
                    Demand(
                        1UL,
                        1U,
                        500_000L)
                });

        Assert.Empty(
            result.DemandFlows);
    }

    [Fact]
    public void ResolverCanonicalizesSupplyOutputOrdering()
    {
        var economy =
            Economy(
                PointWithAgriculture(
                    2UL,
                    1UL,
                    1UL),
                PointWithAgriculture(
                    1UL,
                    1UL,
                    1UL));

        var result =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                new[]
                {
                    Production(2UL, 1U, EconomicActivityKind.Agriculture, 1L),
                    Production(1UL, 2U, EconomicActivityKind.Agriculture, 1L),
                    Production(1UL, 1U, EconomicActivityKind.Agriculture, 1L)
                },
                Array.Empty<EconomicDemandProfile>());

        Assert.Equal(
            new[]
            {
                (1UL, 1U),
                (1UL, 2U),
                (2UL, 1U)
            },
            result.SupplyFlows.Select(
                flow =>
                    (
                        flow.EconomicPointId.Value,
                        flow.CommodityId.Value
                    )));
    }

    [Fact]
    public void ResolverCanonicalizesDemandOutputOrdering()
    {
        var economy =
            Economy(
                PointWithAgriculture(2UL, 1UL, 1UL),
                PointWithAgriculture(1UL, 1UL, 1UL));

        var result =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                Array.Empty<EconomicProductionPotential>(),
                new[]
                {
                    Demand(2UL, 1U, 1L),
                    Demand(1UL, 2U, 1L),
                    Demand(1UL, 1U, 1L)
                });

        Assert.Equal(
            new[]
            {
                (1UL, 1U),
                (1UL, 2U),
                (2UL, 1U)
            },
            result.DemandFlows.Select(
                flow =>
                    (
                        flow.EconomicPointId.Value,
                        flow.CommodityId.Value
                    )));
    }

    [Fact]
    public void EquivalentInputOrderingProducesEquivalentFlows()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    3UL,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture,
                            2UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining,
                            1UL)
                    }));

        EconomicProductionPotential[] production =
        {
            Production(1UL, 2U, EconomicActivityKind.Mining, 400_000L),
            Production(1UL, 1U, EconomicActivityKind.Agriculture, 500_000L)
        };

        EconomicDemandProfile[] demand =
        {
            Demand(1UL, 2U, 250_000L),
            Demand(1UL, 1U, 750_000L)
        };

        var resolver =
            new EconomicProductionDemandResolver();

        var first =
            resolver.Resolve(
                economy,
                production,
                demand);

        Array.Reverse(
            production);

        Array.Reverse(
            demand);

        var second =
            resolver.Resolve(
                economy,
                production,
                demand);

        Assert.Equal(
            first.SupplyFlows.Select(
                flow =>
                    (
                        flow.EconomicPointId,
                        flow.CommodityId,
                        flow.Activity,
                        flow.RawQuantity
                    )),
            second.SupplyFlows.Select(
                flow =>
                    (
                        flow.EconomicPointId,
                        flow.CommodityId,
                        flow.Activity,
                        flow.RawQuantity
                    )));

        Assert.Equal(
            first.DemandFlows.Select(
                flow =>
                    (
                        flow.EconomicPointId,
                        flow.CommodityId,
                        flow.RawQuantity
                    )),
            second.DemandFlows.Select(
                flow =>
                    (
                        flow.EconomicPointId,
                        flow.CommodityId,
                        flow.RawQuantity
                    )));
    }

    [Fact]
    public void ResolverDoesNotMutateWorldStateOrHash()
    {
        var economy =
            Economy(
                PointWithAgriculture(
                    1UL,
                    2UL,
                    1UL));

        var worldState =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }))
                .WithEconomy(
                    economy);

        var hasher =
            new CanonicalWorldStateHasher();

        var before =
            hasher.Compute(
                worldState);

        _ =
            new EconomicProductionDemandResolver().Resolve(
                economy,
                new[]
                {
                    Production(
                        1UL,
                        1U,
                        EconomicActivityKind.Agriculture,
                        500_000L)
                },
                new[]
                {
                    Demand(
                        1UL,
                        1U,
                        250_000L)
                });

        var after =
            hasher.Compute(
                worldState);

        Assert.Same(
            economy,
            worldState.Economy);

        Assert.Equal(
            before,
            after);

        Assert.Equal(
            7U,
            after.FormatVersion);
    }

    [Fact]
    public void ProductionOverflowFailsFast()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    ulong.MaxValue,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture,
                            ulong.MaxValue)
                    }));

        Assert.Throws<OverflowException>(
            () =>
            {
                _ =
                    new EconomicProductionDemandResolver().Resolve(
                        economy,
                        new[]
                        {
                            Production(
                                1UL,
                                1U,
                                EconomicActivityKind.Agriculture,
                                StrategicScalarField.Denominator)
                        },
                        Array.Empty<EconomicDemandProfile>());
            });
    }

    [Fact]
    public void DemandOverflowFailsFast()
    {
        var economy =
            Economy(
                new EconomicPointRuntimeState(
                    new EconomicPointId(1UL),
                    new CivilizationId(1UL),
                    new StrategicCellId(1UL),
                    ulong.MaxValue));

        Assert.Throws<OverflowException>(
            () =>
            {
                _ =
                    new EconomicProductionDemandResolver().Resolve(
                        economy,
                        Array.Empty<EconomicProductionPotential>(),
                        new[]
                        {
                            Demand(
                                1UL,
                                1U,
                                StrategicScalarField.Denominator)
                        });
            });
    }

    [Fact]
    public void ResultRejectsDuplicateSupplyKey()
    {
        var flow =
            new EconomicSupplyFlow(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                EconomicActivityKind.Agriculture,
                1UL);

        Assert.Throws<ArgumentException>(
            () => new EconomicProductionDemandResult(
                new[]
                {
                    flow,
                    flow
                },
                Array.Empty<EconomicDemandFlow>()));
    }

    [Fact]
    public void ResultRejectsDuplicateDemandKey()
    {
        var flow =
            new EconomicDemandFlow(
                new EconomicPointId(1UL),
                new CommodityId(1U),
                1UL);

        Assert.Throws<ArgumentException>(
            () => new EconomicProductionDemandResult(
                Array.Empty<EconomicSupplyFlow>(),
                new[]
                {
                    flow,
                    flow
                }));
    }

    private static EconomyRuntimeState Economy(
        params EconomicPointRuntimeState[] points)
    {
        return new EconomyRuntimeState(
            points);
    }

    private static EconomicPointRuntimeState PointWithAgriculture(
        ulong id,
        ulong workforce,
        ulong agriculture)
    {
        return new EconomicPointRuntimeState(
            new EconomicPointId(
                id),
            new CivilizationId(
                1UL),
            new StrategicCellId(
                id),
            workforce,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture,
                    agriculture)
            });
    }

    private static EconomicProductionPotential Production(
        ulong point,
        uint commodity,
        EconomicActivityKind activity,
        long rate)
    {
        return new EconomicProductionPotential(
            new EconomicPointId(
                point),
            new CommodityId(
                commodity),
            activity,
            rate);
    }

    private static EconomicDemandProfile Demand(
        ulong point,
        uint commodity,
        long rate)
    {
        return new EconomicDemandProfile(
            new EconomicPointId(
                point),
            new CommodityId(
                commodity),
            rate);
    }
}
