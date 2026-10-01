using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M42BRuntimeCivilizationMaterializationTests
{
    [Fact]
    public void MaterializerRejectsNullWorld()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeCivilizationMaterializer.Materialize(
                null!,
                1));
    }

    [Fact]
    public void MaterializerRejectsZeroCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                0));
    }

    [Fact]
    public void MaterializerRejectsNegativeCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                -1));
    }

    [Fact]
    public void SameWorldAndCountProduceSameMaterialization()
    {
        var world =
            GenerateWorld(
                0UL);

        var first =
            RuntimeCivilizationMaterializer.Materialize(
                world,
                3);

        var second =
            RuntimeCivilizationMaterializer.Materialize(
                world,
                3);

        Assert.Equal(
            first.Records.Select(
                record =>
                    (
                        record.Id.Value,
                        record.StartCellId!.Value.Value
                    )),
            second.Records.Select(
                record =>
                    (
                        record.Id.Value,
                        record.StartCellId!.Value.Value
                    )));
    }

    [Fact]
    public void MaterializerAssignsCanonicalSequentialIdentities()
    {
        var state =
            RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                4);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL,
                4UL
            },
            state.Civilizations.Select(
                civilization =>
                    civilization.Value));
    }

    [Fact]
    public void MaterializedStartsAreUnique()
    {
        var state =
            RuntimeCivilizationMaterializer.Materialize(
                GenerateWorld(
                    0UL),
                4);

        var starts =
            state.Records
                .Select(
                    record =>
                        record.StartCellId!.Value)
                .ToArray();

        Assert.Equal(
            starts.Length,
            starts.Distinct().Count());
    }

    [Fact]
    public void MaterializedStartsAreStrategicLand()
    {
        var world =
            GenerateWorld(
                0UL);

        var state =
            RuntimeCivilizationMaterializer.Materialize(
                world,
                4);

        Assert.All(
            state.Records,
            record =>
            {
                Assert.True(
                    record.IsMaterialized);

                Assert.Equal(
                    StrategicLandWaterKind.Land,
                    world
                        .StrategicPhysicalFields
                        .LandWater
                        .GetKind(
                            record.StartCellId!.Value));
            });
    }

    [Fact]
    public void MaterializerPreservesAcceptedSelectorOrderAfterLandEligibility()
    {
        var world =
            GenerateWorld(
                0UL);

        var policy =
            StrategicCivilizationPlacementPolicy.Default;

        var maximumCandidateCount =
            world.StrategicSurfaceGraph.NodeCount
            - (policy.ExcludeInitialReference
                ? 1
                : 0);

        var allCandidates =
            StrategicCivilizationStartCandidateSelector.Select(
                world.Request,
                world.StrategicSurfaceGraph,
                world.StrategicCivilizationPlacementSuitability,
                maximumCandidateCount,
                policy);

        var expectedStarts =
            allCandidates
                .CandidateCellIds
                .Where(
                    cellId =>
                        world
                            .StrategicPhysicalFields
                            .LandWater
                            .GetKind(
                                cellId)
                        == StrategicLandWaterKind.Land)
                .Take(
                    3)
                .ToArray();

        var actual =
            RuntimeCivilizationMaterializer.Materialize(
                world,
                3);

        Assert.Equal(
            expectedStarts,
            actual.Records.Select(
                record =>
                    record.StartCellId!.Value));
    }

    [Fact]
    public void MaterializerRejectsCountBeyondAvailableLandStarts()
    {
        var world =
            GenerateWorld(
                0UL);

        var policy =
            StrategicCivilizationPlacementPolicy.Default;

        var maximumCandidateCount =
            world.StrategicSurfaceGraph.NodeCount
            - (policy.ExcludeInitialReference
                ? 1
                : 0);

        var allCandidates =
            StrategicCivilizationStartCandidateSelector.Select(
                world.Request,
                world.StrategicSurfaceGraph,
                world.StrategicCivilizationPlacementSuitability,
                maximumCandidateCount,
                policy);

        var availableLandStarts =
            allCandidates
                .CandidateCellIds
                .Count(
                    cellId =>
                        world
                            .StrategicPhysicalFields
                            .LandWater
                            .GetKind(
                                cellId)
                        == StrategicLandWaterKind.Land);

        Assert.True(
            availableLandStarts > 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeCivilizationMaterializer.Materialize(
                world,
                checked(
                    availableLandStarts
                    + 1)));
    }

    [Fact]
    public void RuntimeStateKeepsIdentityOnlyCompatibility()
    {
        var state =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationId(2UL),
                    new CivilizationId(1UL)
                });

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL
            },
            state.Civilizations.Select(
                civilization =>
                    civilization.Value));

        Assert.All(
            state.Records,
            record =>
            {
                Assert.False(
                    record.IsMaterialized);

                Assert.Null(
                    record.StartCellId);
            });
    }

    [Fact]
    public void RuntimeStateCanonicalizesMaterializedRecordsByIdentity()
    {
        var state =
            new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationRuntimeRecord(
                        new CivilizationId(3UL),
                        new StrategicCellId(9UL)),
                    new CivilizationRuntimeRecord(
                        new CivilizationId(1UL),
                        new StrategicCellId(4UL))
                });

        Assert.Equal(
            new[]
            {
                (1UL, 4UL),
                (3UL, 9UL)
            },
            state.Records.Select(
                record =>
                    (
                        record.Id.Value,
                        record.StartCellId!.Value.Value
                    )));
    }

    [Fact]
    public void RuntimeStateRejectsDuplicateMaterializedStartCells()
    {
        Assert.Throws<ArgumentException>(
            () => new CivilizationRuntimeState(
                new[]
                {
                    new CivilizationRuntimeRecord(
                        new CivilizationId(1UL),
                        new StrategicCellId(5UL)),
                    new CivilizationRuntimeRecord(
                        new CivilizationId(2UL),
                        new StrategicCellId(5UL))
                }));
    }

    [Fact]
    public void BoundWorldRejectsMaterializedStartOutsideWorld()
    {
        var world =
            GenerateWorld(
                0UL);

        var state =
            WorldState.CreateBound(
                world);

        Assert.Throws<InvalidOperationException>(
            () => state.WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationRuntimeRecord(
                            new CivilizationId(1UL),
                            new StrategicCellId(
                                checked(
                                    (ulong)world
                                        .StrategicSurfaceGraph
                                        .NodeCount
                                    + 1UL)))
                    })));
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
