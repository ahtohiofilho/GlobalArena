using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M43BEconomicPointSubstrateTests
{
    [Fact]
    public void DefaultEconomicPointIdIsInvalid()
    {
        var id = default(EconomicPointId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => _ = id.Value);
    }

    [Fact]
    public void EconomicPointIdRejectsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicPointId(0UL));
    }

    [Fact]
    public void EconomicPointIdAcceptsPositiveValue()
    {
        var id =
            new EconomicPointId(
                7UL);

        Assert.True(
            id.IsValid);

        Assert.Equal(
            7UL,
            id.Value);
    }

    [Fact]
    public void EconomicActivityKindValuesAreStable()
    {
        Assert.Equal(
            1,
            (byte)EconomicActivityKind.Agriculture);

        Assert.Equal(
            2,
            (byte)EconomicActivityKind.Mining);

        Assert.Equal(
            3,
            (byte)EconomicActivityKind.TradeLogistics);
    }

    [Fact]
    public void ActivityAllocationRejectsUndefinedKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicActivityWorkforceAllocation(
                (EconomicActivityKind)99,
                1UL));
    }

    [Fact]
    public void ActivityAllocationRejectsZeroWorkforce()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicActivityWorkforceAllocation(
                EconomicActivityKind.Agriculture,
                0UL));
    }

    [Fact]
    public void EconomicPointRejectsInvalidIdentity()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicPointRuntimeState(
                default,
                new CivilizationId(1UL),
                new StrategicCellId(1UL),
                0UL));
    }

    [Fact]
    public void EconomicPointRejectsInvalidOwner()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                default,
                new StrategicCellId(1UL),
                0UL));
    }

    [Fact]
    public void EconomicPointRejectsInvalidStrategicAnchor()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                new CivilizationId(1UL),
                default,
                0UL));
    }

    [Fact]
    public void EconomicPointAllowsZeroWorkforceWithoutActivities()
    {
        var point =
            new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                new CivilizationId(1UL),
                new StrategicCellId(1UL),
                0UL);

        Assert.Equal(
            0UL,
            point.Workforce);

        Assert.Equal(
            0UL,
            point.AllocatedWorkforce);

        Assert.Equal(
            0UL,
            point.UnallocatedWorkforce);

        Assert.Empty(
            point.ActivityAllocations);
    }

    [Fact]
    public void EconomicPointRejectsAllocatedWorkforceAboveTotal()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                new CivilizationId(1UL),
                new StrategicCellId(1UL),
                1UL,
                new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture,
                        2UL)
                }));
    }

    [Fact]
    public void EconomicPointRejectsDuplicateActivityKinds()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                new CivilizationId(1UL),
                new StrategicCellId(1UL),
                3UL,
                new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture,
                        1UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture,
                        2UL)
                }));
    }

    [Fact]
    public void EconomicPointCanonicalizesActivitiesByKind()
    {
        var point =
            new EconomicPointRuntimeState(
                new EconomicPointId(1UL),
                new CivilizationId(1UL),
                new StrategicCellId(1UL),
                6UL,
                new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.TradeLogistics,
                        1UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining,
                        2UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture,
                        3UL)
                });

        Assert.Equal(
            new[]
            {
                EconomicActivityKind.Agriculture,
                EconomicActivityKind.Mining,
                EconomicActivityKind.TradeLogistics
            },
            point.ActivityAllocations.Select(
                allocation =>
                    allocation.Kind));

        Assert.Equal(
            6UL,
            point.AllocatedWorkforce);

        Assert.Equal(
            0UL,
            point.UnallocatedWorkforce);
    }

    [Fact]
    public void EconomyStateCanonicalizesPointsByIdentity()
    {
        var state =
            new EconomyRuntimeState(
                new[]
                {
                    CreatePoint(
                        3UL,
                        1UL,
                        3UL,
                        0UL),
                    CreatePoint(
                        1UL,
                        1UL,
                        1UL,
                        0UL),
                    CreatePoint(
                        2UL,
                        1UL,
                        2UL,
                        0UL)
                });

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            state.EconomicPoints.Select(
                point =>
                    point.Id.Value));
    }

    [Fact]
    public void EconomyStateRejectsDuplicatePointIdentity()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomyRuntimeState(
                new[]
                {
                    CreatePoint(
                        1UL,
                        1UL,
                        1UL,
                        0UL),
                    CreatePoint(
                        1UL,
                        1UL,
                        2UL,
                        0UL)
                }));
    }

    [Fact]
    public void EconomyStateSnapshotsInputArray()
    {
        EconomicPointRuntimeState[] points =
        {
            CreatePoint(
                1UL,
                1UL,
                1UL,
                0UL),
            CreatePoint(
                2UL,
                1UL,
                2UL,
                0UL)
        };

        var state =
            new EconomyRuntimeState(
                points);

        Array.Reverse(
            points);

        Assert.Equal(
            1UL,
            state.EconomicPoints[0].Id.Value);

        Assert.Equal(
            2UL,
            state.EconomicPoints[1].Id.Value);
    }

    [Fact]
    public void BoundWorldStateRejectsEconomicPointOutsideGeneratedWorld()
    {
        var state =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        CreatePoint(
                            1UL,
                            1UL,
                            43UL,
                            0UL)
                    })));
    }

    [Fact]
    public void WorldStateRejectsUnknownEconomicPointOwner()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        CreatePoint(
                            1UL,
                            2UL,
                            1UL,
                            0UL)
                    })));
    }

    [Fact]
    public void EquivalentEconomicOrderingProducesSameHash()
    {
        var first =
            CreateEquivalentEconomicState(
                reversePoints: false,
                reverseActivities: false);

        var second =
            CreateEquivalentEconomicState(
                reversePoints: true,
                reverseActivities: true);

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.Equal(
            hasher.Compute(
                first),
            hasher.Compute(
                second));
    }

    [Fact]
    public void EconomicHashChangesWhenWorkforceOrActivityChanges()
    {
        var baseline =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }))
                .WithEconomy(
                    new EconomyRuntimeState(
                        new[]
                        {
                            new EconomicPointRuntimeState(
                                new EconomicPointId(1UL),
                                new CivilizationId(1UL),
                                new StrategicCellId(1UL),
                                4UL,
                                new[]
                                {
                                    new EconomicActivityWorkforceAllocation(
                                        EconomicActivityKind.Agriculture,
                                        4UL)
                                })
                        }));

        var workforceChanged =
            baseline.WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new EconomicPointRuntimeState(
                            new EconomicPointId(1UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(1UL),
                            5UL,
                            new[]
                            {
                                new EconomicActivityWorkforceAllocation(
                                    EconomicActivityKind.Agriculture,
                                    4UL)
                            })
                    }));

        var activityChanged =
            baseline.WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new EconomicPointRuntimeState(
                            new EconomicPointId(1UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(1UL),
                            4UL,
                            new[]
                            {
                                new EconomicActivityWorkforceAllocation(
                                    EconomicActivityKind.Mining,
                                    4UL)
                            })
                    }));

        var hasher =
            new CanonicalWorldStateHasher();

        var baselineHash =
            hasher.Compute(
                baseline);

        Assert.NotEqual(
            baselineHash,
            hasher.Compute(
                workforceChanged));

        Assert.NotEqual(
            baselineHash,
            hasher.Compute(
                activityChanged));
    }

    private static EconomicPointRuntimeState CreatePoint(
        ulong id,
        ulong owner,
        ulong strategicCell,
        ulong workforce)
    {
        return new EconomicPointRuntimeState(
            new EconomicPointId(
                id),
            new CivilizationId(
                owner),
            new StrategicCellId(
                strategicCell),
            workforce);
    }

    private static WorldState CreateEquivalentEconomicState(
        bool reversePoints,
        bool reverseActivities)
    {
        EconomicActivityWorkforceAllocation[] firstActivities =
        {
            new(
                EconomicActivityKind.Agriculture,
                3UL),
            new(
                EconomicActivityKind.TradeLogistics,
                1UL)
        };

        if (reverseActivities)
        {
            Array.Reverse(
                firstActivities);
        }

        EconomicPointRuntimeState[] points =
        {
            new(
                new EconomicPointId(1UL),
                new CivilizationId(1UL),
                new StrategicCellId(1UL),
                4UL,
                firstActivities),
            new(
                new EconomicPointId(2UL),
                new CivilizationId(1UL),
                new StrategicCellId(2UL),
                2UL,
                new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining,
                        2UL)
                })
        };

        if (reversePoints)
        {
            Array.Reverse(
                points);
        }

        return WorldState.CreateInitial()
            .WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(1UL)
                    }))
            .WithEconomy(
                new EconomyRuntimeState(
                    points));
    }

    private static WorldGenerationResult GenerateWorld(
        ulong seed)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        return generator.Generate(
            new WorldGenerationRequest(
                new WorldSeed(
                    seed),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    0)));
    }
}