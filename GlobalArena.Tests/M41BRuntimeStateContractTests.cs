using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M41BRuntimeStateContractTests
{
    [Fact]
    public void CivilizationIdRejectsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CivilizationId(0UL));
    }

    [Fact]
    public void DefaultCivilizationIdIsInvalid()
    {
        var id = default(CivilizationId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => _ = id.Value);
    }

    [Fact]
    public void CivilizationIdAcceptsPositiveValue()
    {
        var id = new CivilizationId(9UL);

        Assert.True(id.IsValid);
        Assert.Equal(9UL, id.Value);
    }

    [Fact]
    public void CommodityIdRejectsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommodityId(0U));
    }

    [Fact]
    public void DefaultCommodityIdIsInvalid()
    {
        var id = default(CommodityId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => _ = id.Value);
    }

    [Fact]
    public void CommodityIdAcceptsPositiveValue()
    {
        var id = new CommodityId(7U);

        Assert.True(id.IsValid);
        Assert.Equal(7U, id.Value);
    }

    [Fact]
    public void MilitaryUnitIdRejectsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MilitaryUnitId(0UL));
    }

    [Fact]
    public void DefaultMilitaryUnitIdIsInvalid()
    {
        var id = default(MilitaryUnitId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => _ = id.Value);
    }

    [Fact]
    public void MilitaryUnitIdAcceptsPositiveValue()
    {
        var id = new MilitaryUnitId(11UL);

        Assert.True(id.IsValid);
        Assert.Equal(11UL, id.Value);
    }

    [Fact]
    public void CivilizationRosterEmptyIsEmpty()
    {
        Assert.Empty(
            CivilizationRuntimeState
                .Empty
                .Civilizations);
    }

    [Fact]
    public void CivilizationRosterIsCanonicalByIdentity()
    {
        var state =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(3UL),
                    new CivilizationId(1UL),
                    new CivilizationId(2UL)
                });

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            state.Civilizations.Select(
                civilization =>
                    civilization.Value));
    }

    [Fact]
    public void CivilizationRosterRejectsDuplicates()
    {
        Assert.Throws<ArgumentException>(
            () => new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(1UL),
                    new CivilizationId(1UL)
                }));
    }

    [Fact]
    public void CivilizationRosterRejectsInvalidIdentity()
    {
        Assert.Throws<ArgumentException>(
            () => new CivilizationRuntimeState(
                new[]
                {
                    default(CivilizationId)
                }));
    }

    [Fact]
    public void EconomicPointIdRejectsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicPointId(0UL));
    }

    [Fact]
    public void EconomyStateIsCanonicalByPointIdentity()
    {
        var state =
            new EconomyRuntimeState(
                new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(3UL),
                        new CivilizationId(2UL),
                        new StrategicCellId(3UL),
                        0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(2UL),
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
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(2UL),
                        0UL)
                }));
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
    public void MilitaryUnitRequiresValidIdentityOwnerAndLocation()
    {
        Assert.Throws<ArgumentException>(
            () => new MilitaryUnitRuntimeState(
                default,
                new CivilizationId(1UL),
                new StrategicCellId(1UL)));

        Assert.Throws<ArgumentException>(
            () => new MilitaryUnitRuntimeState(
                new MilitaryUnitId(1UL),
                default,
                new StrategicCellId(1UL)));

        Assert.Throws<ArgumentException>(
            () => new MilitaryUnitRuntimeState(
                new MilitaryUnitId(1UL),
                new CivilizationId(1UL),
                default));
    }

    [Fact]
    public void WarfareStateIsCanonicalByUnitIdentity()
    {
        var state =
            new WarfareRuntimeState(
                new[]
                {
                    new MilitaryUnitRuntimeState(
                        new MilitaryUnitId(8UL),
                        new CivilizationId(2UL),
                        new StrategicCellId(5UL)),
                    new MilitaryUnitRuntimeState(
                        new MilitaryUnitId(2UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(3UL))
                });

        Assert.Equal(
            new ulong[]
            {
                2UL,
                8UL
            },
            state.Units.Select(
                unit =>
                    unit.Id.Value));
    }

    [Fact]
    public void WarfareStateRejectsDuplicateUnitIdentity()
    {
        Assert.Throws<ArgumentException>(
            () => new WarfareRuntimeState(
                new[]
                {
                    new MilitaryUnitRuntimeState(
                        new MilitaryUnitId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(3UL)),
                    new MilitaryUnitRuntimeState(
                        new MilitaryUnitId(1UL),
                        new CivilizationId(2UL),
                        new StrategicCellId(4UL))
                }));
    }

    [Fact]
    public void InitialWorldStateHasEmptyRuntimeDomains()
    {
        var state =
            WorldState.CreateInitial();

        Assert.Equal(0UL, state.Revision);
        Assert.Empty(state.Civilizations.Civilizations);
        Assert.Empty(state.Economy.EconomicPoints);
        Assert.Empty(state.Warfare.Units);
    }

    [Fact]
    public void WorldStateWithMethodsReturnNewStateAndPreserveOriginal()
    {
        var initial =
            WorldState.CreateInitial();

        var civilizations =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(1UL)
                });

        var economy =
            new EconomyRuntimeState(
                new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        1UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture,
                                1UL)
                        })
                });

        var warfare =
            new WarfareRuntimeState(
                new[]
                {
                    new MilitaryUnitRuntimeState(
                        new MilitaryUnitId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL))
                });

        var populated =
            initial
                .WithCivilizations(
                    civilizations)
                .WithEconomy(
                    economy)
                .WithWarfare(
                    warfare);

        Assert.NotSame(initial, populated);
        Assert.Empty(initial.Civilizations.Civilizations);
        Assert.Empty(initial.Economy.EconomicPoints);
        Assert.Empty(initial.Warfare.Units);

        Assert.Same(
            civilizations,
            populated.Civilizations);

        Assert.Same(
            economy,
            populated.Economy);

        Assert.Same(
            warfare,
            populated.Warfare);
    }

    [Fact]
    public void AdvanceRevisionPreservesRuntimeDomainSnapshots()
    {
        var state =
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
                                new StrategicCellId(2UL),
                                1UL)
                        }))
                .WithWarfare(
                    new WarfareRuntimeState(
                        new[]
                        {
                            new MilitaryUnitRuntimeState(
                                new MilitaryUnitId(1UL),
                                new CivilizationId(1UL),
                                new StrategicCellId(2UL))
                        }));

        var advanced =
            state.AdvanceRevision();

        Assert.Equal(1UL, advanced.Revision);
        Assert.Same(state.Civilizations, advanced.Civilizations);
        Assert.Same(state.Economy, advanced.Economy);
        Assert.Same(state.Warfare, advanced.Warfare);
    }
}