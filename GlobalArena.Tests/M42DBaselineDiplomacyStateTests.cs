using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M42DBaselineDiplomacyStateTests
{
    [Fact]
    public void RelationEntryRejectsInvalidFirstCivilization()
    {
        Assert.Throws<ArgumentException>(
            () => new BaselineDiplomacyRelationEntry(
                default,
                new CivilizationId(2UL),
                BaselineDiplomacyRelationKind.Enemy));
    }

    [Fact]
    public void RelationEntryRejectsInvalidSecondCivilization()
    {
        Assert.Throws<ArgumentException>(
            () => new BaselineDiplomacyRelationEntry(
                new CivilizationId(1UL),
                default,
                BaselineDiplomacyRelationKind.Enemy));
    }

    [Fact]
    public void RelationEntryRejectsSelfRelation()
    {
        Assert.Throws<ArgumentException>(
            () => new BaselineDiplomacyRelationEntry(
                new CivilizationId(1UL),
                new CivilizationId(1UL),
                BaselineDiplomacyRelationKind.Enemy));
    }

    [Fact]
    public void RelationEntryRejectsNeutralPersistence()
    {
        Assert.Throws<ArgumentException>(
            () => new BaselineDiplomacyRelationEntry(
                new CivilizationId(1UL),
                new CivilizationId(2UL),
                BaselineDiplomacyRelationKind.Neutral));
    }

    [Fact]
    public void RelationEntryRejectsUnknownRelationValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BaselineDiplomacyRelationEntry(
                new CivilizationId(1UL),
                new CivilizationId(2UL),
                (BaselineDiplomacyRelationKind)99));
    }

    [Fact]
    public void RelationEntryCanonicalizesPairOrder()
    {
        var entry =
            new BaselineDiplomacyRelationEntry(
                new CivilizationId(2UL),
                new CivilizationId(1UL),
                BaselineDiplomacyRelationKind.Ally);

        Assert.Equal(
            1UL,
            entry.First.Value);

        Assert.Equal(
            2UL,
            entry.Second.Value);

        Assert.Equal(
            BaselineDiplomacyRelationKind.Ally,
            entry.Relation);
    }

    [Fact]
    public void EmptyStateHasNoPersistedRelations()
    {
        Assert.Empty(
            BaselineDiplomacyRuntimeState.Empty.Relations);
    }

    [Fact]
    public void NeutralIsDefaultForDistinctCivilizations()
    {
        Assert.Equal(
            BaselineDiplomacyRelationKind.Neutral,
            BaselineDiplomacyRuntimeState.Empty.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));
    }

    [Fact]
    public void SelfLookupIsNeutral()
    {
        Assert.Equal(
            BaselineDiplomacyRelationKind.Neutral,
            BaselineDiplomacyRuntimeState.Empty.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(1UL)));
    }

    [Fact]
    public void LookupRejectsInvalidCivilization()
    {
        Assert.Throws<ArgumentException>(
            () => BaselineDiplomacyRuntimeState.Empty.GetRelation(
                default,
                new CivilizationId(2UL)));
    }

    [Fact]
    public void StateRejectsNullRelationCollection()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BaselineDiplomacyRuntimeState(
                null!));
    }

    [Fact]
    public void StateRejectsNullRelationEntry()
    {
        Assert.Throws<ArgumentException>(
            () => new BaselineDiplomacyRuntimeState(
                new BaselineDiplomacyRelationEntry[]
                {
                    null!
                }));
    }

    [Fact]
    public void StateRejectsDuplicateCanonicalPair()
    {
        Assert.Throws<ArgumentException>(
            () => new BaselineDiplomacyRuntimeState(
                new[]
                {
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(1UL),
                        new CivilizationId(2UL),
                        BaselineDiplomacyRelationKind.Enemy),
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(2UL),
                        new CivilizationId(1UL),
                        BaselineDiplomacyRelationKind.Ally)
                }));
    }

    [Fact]
    public void StateCanonicalizesRelationOrder()
    {
        var state =
            new BaselineDiplomacyRuntimeState(
                new[]
                {
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(3UL),
                        new CivilizationId(2UL),
                        BaselineDiplomacyRelationKind.Ally),
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(3UL),
                        new CivilizationId(1UL),
                        BaselineDiplomacyRelationKind.Enemy),
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(2UL),
                        new CivilizationId(1UL),
                        BaselineDiplomacyRelationKind.Ally)
                });

        Assert.Equal(
            new[]
            {
                (1UL, 2UL),
                (1UL, 3UL),
                (2UL, 3UL)
            },
            state.Relations.Select(
                relation =>
                    (
                        relation.First.Value,
                        relation.Second.Value
                    )));
    }

    [Fact]
    public void EnemyLookupIsSymmetric()
    {
        var state =
            new BaselineDiplomacyRuntimeState(
                new[]
                {
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(1UL),
                        new CivilizationId(2UL),
                        BaselineDiplomacyRelationKind.Enemy)
                });

        Assert.Equal(
            BaselineDiplomacyRelationKind.Enemy,
            state.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));

        Assert.Equal(
            BaselineDiplomacyRelationKind.Enemy,
            state.GetRelation(
                new CivilizationId(2UL),
                new CivilizationId(1UL)));
    }

    [Fact]
    public void AllyLookupIsSymmetric()
    {
        var state =
            new BaselineDiplomacyRuntimeState(
                new[]
                {
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(1UL),
                        new CivilizationId(2UL),
                        BaselineDiplomacyRelationKind.Ally)
                });

        Assert.Equal(
            BaselineDiplomacyRelationKind.Ally,
            state.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));

        Assert.Equal(
            BaselineDiplomacyRelationKind.Ally,
            state.GetRelation(
                new CivilizationId(2UL),
                new CivilizationId(1UL)));
    }

    [Fact]
    public void WithRelationAddsOverride()
    {
        var state =
            BaselineDiplomacyRuntimeState.Empty.WithRelation(
                new CivilizationId(2UL),
                new CivilizationId(1UL),
                BaselineDiplomacyRelationKind.Enemy);

        Assert.Single(
            state.Relations);

        Assert.Equal(
            BaselineDiplomacyRelationKind.Enemy,
            state.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));
    }

    [Fact]
    public void WithRelationReplacesOverride()
    {
        var state =
            BaselineDiplomacyRuntimeState.Empty
                .WithRelation(
                    new CivilizationId(1UL),
                    new CivilizationId(2UL),
                    BaselineDiplomacyRelationKind.Enemy)
                .WithRelation(
                    new CivilizationId(2UL),
                    new CivilizationId(1UL),
                    BaselineDiplomacyRelationKind.Ally);

        Assert.Single(
            state.Relations);

        Assert.Equal(
            BaselineDiplomacyRelationKind.Ally,
            state.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));
    }

    [Fact]
    public void WithNeutralRemovesOverride()
    {
        var state =
            BaselineDiplomacyRuntimeState.Empty
                .WithRelation(
                    new CivilizationId(1UL),
                    new CivilizationId(2UL),
                    BaselineDiplomacyRelationKind.Enemy)
                .WithRelation(
                    new CivilizationId(2UL),
                    new CivilizationId(1UL),
                    BaselineDiplomacyRelationKind.Neutral);

        Assert.Empty(
            state.Relations);

        Assert.Equal(
            BaselineDiplomacyRelationKind.Neutral,
            state.GetRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL)));
    }

    [Fact]
    public void WithRelationRejectsSelfPair()
    {
        Assert.Throws<ArgumentException>(
            () => BaselineDiplomacyRuntimeState.Empty.WithRelation(
                new CivilizationId(1UL),
                new CivilizationId(1UL),
                BaselineDiplomacyRelationKind.Enemy));
    }

    [Fact]
    public void WithRelationRejectsUnknownRelationValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BaselineDiplomacyRuntimeState.Empty.WithRelation(
                new CivilizationId(1UL),
                new CivilizationId(2UL),
                (BaselineDiplomacyRelationKind)99));
    }

    [Fact]
    public void WorldStateDefaultsToEmptyDiplomacy()
    {
        Assert.Empty(
            WorldState.CreateInitial()
                .Diplomacy
                .Relations);
    }

    [Fact]
    public void WorldStateRejectsUnknownDiplomacyParticipant()
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
            () => state.WithDiplomacy(
                new BaselineDiplomacyRuntimeState(
                    new[]
                    {
                        new BaselineDiplomacyRelationEntry(
                            new CivilizationId(1UL),
                            new CivilizationId(2UL),
                            BaselineDiplomacyRelationKind.Enemy)
                    })));
    }

    [Fact]
    public void CivilizationRosterCannotDropDiplomacyParticipant()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL),
                            new CivilizationId(3UL)
                        }))
                .WithDiplomacy(
                    new BaselineDiplomacyRuntimeState(
                        new[]
                        {
                            new BaselineDiplomacyRelationEntry(
                                new CivilizationId(1UL),
                                new CivilizationId(2UL),
                                BaselineDiplomacyRelationKind.Ally)
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(1UL),
                        new CivilizationId(3UL)
                    })));
    }

    [Fact]
    public void DiplomacyIsPreservedAcrossRevision()
    {
        var diplomacy =
            new BaselineDiplomacyRuntimeState(
                new[]
                {
                    new BaselineDiplomacyRelationEntry(
                        new CivilizationId(1UL),
                        new CivilizationId(2UL),
                        BaselineDiplomacyRelationKind.Enemy)
                });

        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL)
                        }))
                .WithDiplomacy(
                    diplomacy);

        var advanced =
            state.AdvanceRevision();

        Assert.Same(
            diplomacy,
            advanced.Diplomacy);

        Assert.Equal(
            1UL,
            advanced.Revision);
    }

    [Fact]
    public void DifferentDiplomacyChangesCanonicalHash()
    {
        var civilizations =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(1UL),
                    new CivilizationId(2UL)
                });

        var neutral =
            WorldState.CreateInitial()
                .WithCivilizations(
                    civilizations);

        var enemy =
            neutral.WithDiplomacy(
                BaselineDiplomacyRuntimeState.Empty.WithRelation(
                    new CivilizationId(2UL),
                    new CivilizationId(1UL),
                    BaselineDiplomacyRelationKind.Enemy));

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.NotEqual(
            hasher.Compute(
                neutral),
            hasher.Compute(
                enemy));
    }

    [Fact]
    public void DiplomacyStateHasKnownCanonicalHash()
    {
        var state =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationRuntimeRecord(
                                new CivilizationId(3UL),
                                new StrategicCellId(15UL)),
                            new CivilizationRuntimeRecord(
                                new CivilizationId(2UL),
                                new StrategicCellId(10UL)),
                            new CivilizationRuntimeRecord(
                                new CivilizationId(1UL),
                                new StrategicCellId(3UL))
                        }))
                .WithTerritory(
                    new StrategicTerritoryRuntimeState(
                        new[]
                        {
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(15UL),
                                new CivilizationId(3UL)),
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(10UL),
                                new CivilizationId(2UL)),
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(1UL))
                        }))
                .WithDiplomacy(
                    new BaselineDiplomacyRuntimeState(
                        new[]
                        {
                            new BaselineDiplomacyRelationEntry(
                                new CivilizationId(2UL),
                                new CivilizationId(1UL),
                                BaselineDiplomacyRelationKind.Enemy),
                            new BaselineDiplomacyRelationEntry(
                                new CivilizationId(3UL),
                                new CivilizationId(1UL),
                                BaselineDiplomacyRelationKind.Ally)
                        }));

        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    state);

        Assert.Equal(
            KnownDiplomacyDigest,
            hash.HexDigest);
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

    private const string KnownDiplomacyDigest =
        "650F062B08FCCFEDC4F697209E1A77321"
        + "1536D890B1DC2CFB0DB95FD5F5586F4";
}
