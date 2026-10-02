using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M42CStrategicTerritoryControlTests
{
    [Fact]
    public void TerritoryEntryRejectsInvalidCell()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicTerritoryControlEntry(
                default,
                new CivilizationId(1UL)));
    }

    [Fact]
    public void TerritoryEntryRejectsInvalidController()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicTerritoryControlEntry(
                new StrategicCellId(1UL),
                default));
    }

    [Fact]
    public void TerritoryStateRejectsNullControls()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategicTerritoryRuntimeState(
                null!));
    }

    [Fact]
    public void TerritoryStateRejectsNullControlEntry()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicTerritoryRuntimeState(
                new StrategicTerritoryControlEntry[]
                {
                    null!
                }));
    }

    [Fact]
    public void TerritoryStateCanonicalizesByStrategicCell()
    {
        var state =
            new StrategicTerritoryRuntimeState(
                new[]
                {
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(9UL),
                        new CivilizationId(2UL)),
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(3UL),
                        new CivilizationId(1UL))
                });

        Assert.Equal(
            new[]
            {
                (3UL, 1UL),
                (9UL, 2UL)
            },
            state.Controls.Select(
                control =>
                    (
                        control.StrategicCellId.Value,
                        control.Controller.Value
                    )));
    }

    [Fact]
    public void TerritoryStateRejectsDuplicateStrategicCell()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicTerritoryRuntimeState(
                new[]
                {
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(3UL),
                        new CivilizationId(1UL)),
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(3UL),
                        new CivilizationId(2UL))
                }));
    }

    [Fact]
    public void MissingStrategicCellIsUnowned()
    {
        var state =
            new StrategicTerritoryRuntimeState(
                new[]
                {
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(3UL),
                        new CivilizationId(1UL))
                });

        Assert.False(
            state.TryGetController(
                new StrategicCellId(4UL),
                out _));
    }

    [Fact]
    public void ControlledStrategicCellReturnsController()
    {
        var state =
            new StrategicTerritoryRuntimeState(
                new[]
                {
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(3UL),
                        new CivilizationId(2UL))
                });

        Assert.True(
            state.TryGetController(
                new StrategicCellId(3UL),
                out var controller));

        Assert.Equal(
            2UL,
            controller.Value);
    }

    [Fact]
    public void InitialTerritoryMaterializerRejectsNullCivilizations()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                null!));
    }

    [Fact]
    public void InitialTerritoryMaterializerRejectsIdentityOnlyCivilization()
    {
        var civilizations =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(1UL)
                });

        Assert.Throws<InvalidOperationException>(
            () => RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                civilizations));
    }

    [Fact]
    public void InitialTerritoryAssignsEachCivilizationOwnStart()
    {
        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                3);

        var territory =
            RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                civilizations);

        Assert.Equal(
            civilizations.Records.Count,
            territory.Controls.Count);

        foreach (var civilization in
            civilizations.Records)
        {
            Assert.True(
                territory.TryGetController(
                    civilization.StartCellId!.Value,
                    out var controller));

            Assert.Equal(
                civilization.Id,
                controller);
        }
    }

    [Fact]
    public void InitialTerritoryContainsOnlyCivilizationStarts()
    {
        var civilizations =
            RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                4);

        var territory =
            RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
                civilizations);

        var expected =
            civilizations.Records
                .Select(
                    civilization =>
                        civilization.StartCellId!.Value.Value)
                .OrderBy(
                    value =>
                        value);

        Assert.Equal(
            expected,
            territory.Controls.Select(
                control =>
                    control.StrategicCellId.Value));
    }

    [Fact]
    public void WorldStateInitialTerritoryIsEmpty()
    {
        Assert.Empty(
            WorldState.CreateInitial()
                .Territory
                .Controls);
    }

    [Fact]
    public void WorldStateAcceptsValidTerritory()
    {
        var territory =
            new StrategicTerritoryRuntimeState(
                new[]
                {
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(2UL),
                        new CivilizationId(1UL))
                });

        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }))
                .WithTerritory(
                    territory);

        Assert.Same(
            territory,
            state.Territory);
    }

    [Fact]
    public void WorldStateRejectsUnknownTerritoryController()
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
            () => state.WithTerritory(
                new StrategicTerritoryRuntimeState(
                    new[]
                    {
                        new StrategicTerritoryControlEntry(
                            new StrategicCellId(2UL),
                            new CivilizationId(2UL))
                    })));
    }

    [Fact]
    public void BoundWorldRejectsTerritoryCellOutsideWorld()
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
            () => state.WithTerritory(
                new StrategicTerritoryRuntimeState(
                    new[]
                    {
                        new StrategicTerritoryControlEntry(
                            new StrategicCellId(43UL),
                            new CivilizationId(1UL))
                    })));
    }

    [Fact]
    public void CivilizationRosterCannotDropTerritoryController()
    {
        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL)
                        }))
                .WithTerritory(
                    new StrategicTerritoryRuntimeState(
                        new[]
                        {
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(2UL))
                        }));

        Assert.Throws<InvalidOperationException>(
            () => state.WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(1UL)
                    })));
    }

    [Fact]
    public void TerritoryIsPreservedAcrossRevision()
    {
        var territory =
            new StrategicTerritoryRuntimeState(
                new[]
                {
                    new StrategicTerritoryControlEntry(
                        new StrategicCellId(3UL),
                        new CivilizationId(1UL))
                });

        var state =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL)
                        }))
                .WithTerritory(
                    territory);

        var advanced =
            state.AdvanceRevision();

        Assert.Same(
            territory,
            advanced.Territory);

        Assert.Equal(
            1UL,
            advanced.Revision);
    }

    [Fact]
    public void EquivalentTerritoryOrderProducesSameHash()
    {
        var civilizations =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(1UL),
                    new CivilizationId(2UL)
                });

        var first =
            WorldState.CreateInitial()
                .WithCivilizations(
                    civilizations)
                .WithTerritory(
                    new StrategicTerritoryRuntimeState(
                        new[]
                        {
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(9UL),
                                new CivilizationId(2UL)),
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(1UL))
                        }));

        var second =
            WorldState.CreateInitial()
                .WithCivilizations(
                    civilizations)
                .WithTerritory(
                    new StrategicTerritoryRuntimeState(
                        new[]
                        {
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(1UL)),
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(9UL),
                                new CivilizationId(2UL))
                        }));

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.Equal(
            hasher.Compute(first),
            hasher.Compute(second));
    }

    [Fact]
    public void DifferentTerritoryChangesCanonicalHash()
    {
        var civilizations =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(1UL),
                    new CivilizationId(2UL)
                });

        var first =
            WorldState.CreateInitial()
                .WithCivilizations(
                    civilizations)
                .WithTerritory(
                    new StrategicTerritoryRuntimeState(
                        new[]
                        {
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(1UL))
                        }));

        var second =
            WorldState.CreateInitial()
                .WithCivilizations(
                    civilizations)
                .WithTerritory(
                    new StrategicTerritoryRuntimeState(
                        new[]
                        {
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(2UL))
                        }));

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.NotEqual(
            hasher.Compute(first),
            hasher.Compute(second));
    }

    [Fact]
    public void TerritoryStateHasKnownCanonicalHash()
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
                                new StrategicCellId(10UL),
                                new CivilizationId(2UL)),
                            new StrategicTerritoryControlEntry(
                                new StrategicCellId(3UL),
                                new CivilizationId(1UL))
                        }));

        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    state);

        Assert.Equal(
            KnownTerritoryDigest,
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

    private const string KnownTerritoryDigest =
        "01DEA66DE26245DDF3DFAF502F7DE3F3"
        + "8F313DDD9E78F8407BB554D5C6E010D5";
}
