using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicBiomeClassifierTests
{
    [Fact]
    public void NullPhysicalFieldsAreRejected()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicBiomeClassifier.Generate(
                    null!,
                    bundle.Climate,
                    bundle.Hydrology));
    }

    [Fact]
    public void NullClimateFieldsAreRejected()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicBiomeClassifier.Generate(
                    bundle.Physical,
                    null!,
                    bundle.Hydrology));
    }

    [Fact]
    public void NullHydrologyFieldsAreRejected()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicBiomeClassifier.Generate(
                    bundle.Physical,
                    bundle.Climate,
                    null!));
    }

    [Fact]
    public void MismatchedAcceptedFieldInstancesAreRejected()
    {
        var first =
            CreateGeneratedBundle();

        var second =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentException>(
            () =>
                StrategicBiomeClassifier.Generate(
                    first.Physical,
                    first.Climate,
                    second.Hydrology));
    }

    [Fact]
    public void GeneratedMapContainsOneBiomePerStrategicNode()
    {
        var bundle =
            CreateGeneratedBundle();

        var biomes =
            StrategicBiomeClassifier.Generate(
                bundle.Physical,
                bundle.Climate,
                bundle.Hydrology);

        Assert.Equal(
            bundle.Graph.NodeCount,
            biomes.Count);
    }

    [Fact]
    public void WaterClassificationHasPrecedence()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 0L,
                seaLevel: 0L,
                temperature: -900_000L,
                moisture: 900_000L,
                availability: 1_000_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Water,
                    kind));
    }

    [Fact]
    public void PolarIceUsesInclusiveTemperatureThreshold()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature:
                    StrategicBiomeClassifier.PolarIceMaximumTemperature,
                moisture: 900_000L,
                availability: 900_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.PolarIce,
                    kind));
    }

    [Fact]
    public void TundraUsesInclusiveTemperatureThreshold()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature:
                    StrategicBiomeClassifier.TundraMaximumTemperature,
                moisture: 900_000L,
                availability: 900_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Tundra,
                    kind));
    }

    [Fact]
    public void HighlandUsesInclusiveElevationThreshold()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation:
                    StrategicBiomeClassifier.HighlandMinimumElevation,
                temperature: 100_000L,
                moisture: 900_000L,
                availability: 900_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Highland,
                    kind));
    }

    [Fact]
    public void WetlandRequiresInlandSinkAvailabilityAndAccumulation()
    {
        var graph =
            CreateGraph();

        var elevations =
            Enumerable.Repeat(
                200_000L,
                graph.NodeCount)
                .ToArray();

        elevations[0] =
            100_000L;

        var biomes =
            CreateBiomeMap(
                graph,
                elevations,
                temperature: 100_000L,
                moisture: 800_000L,
                availability: 800_000L,
                seaLevel: -1_000_000L);

        Assert.Equal(
            StrategicBiomeKind.Wetland,
            biomes.GetKind(
                0));
    }

    [Fact]
    public void DesertUsesInclusiveWaterAvailabilityThreshold()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature: 500_000L,
                moisture: 900_000L,
                availability:
                    StrategicBiomeClassifier.DesertMaximumWaterAvailability);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Desert,
                    kind));
    }

    [Fact]
    public void RainforestRequiresHotAndMoistThresholds()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature:
                    StrategicBiomeClassifier.RainforestMinimumTemperature,
                moisture:
                    StrategicBiomeClassifier.RainforestMinimumMoisture,
                availability: 800_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Rainforest,
                    kind));
    }

    [Fact]
    public void ForestUsesInclusiveMoistureThreshold()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature: 100_000L,
                moisture:
                    StrategicBiomeClassifier.ForestMinimumMoisture,
                availability: 600_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Forest,
                    kind));
    }

    [Fact]
    public void GrasslandIsDeterministicFallback()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature: 100_000L,
                moisture: 300_000L,
                availability: 500_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Grassland,
                    kind));
    }

    [Fact]
    public void HighlandPrecedesWetlandClassification()
    {
        var graph =
            CreateGraph();

        var elevations =
            Enumerable.Repeat(
                700_000L,
                graph.NodeCount)
                .ToArray();

        elevations[0] =
            StrategicBiomeClassifier.HighlandMinimumElevation;

        var biomes =
            CreateBiomeMap(
                graph,
                elevations,
                temperature: 100_000L,
                moisture: 800_000L,
                availability: 800_000L,
                seaLevel: -1_000_000L);

        Assert.Equal(
            StrategicBiomeKind.Highland,
            biomes.GetKind(
                0));
    }

    [Fact]
    public void DesertPrecedesRainforestWhenAvailabilityIsDry()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature: 800_000L,
                moisture: 900_000L,
                availability: 100_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Desert,
                    kind));
    }

    [Fact]
    public void RainforestPrecedesForest()
    {
        var biomes =
            CreateUniformBiomeMap(
                elevation: 100_000L,
                temperature: 800_000L,
                moisture: 900_000L,
                availability: 900_000L);

        Assert.All(
            biomes.Kinds,
            kind =>
                Assert.Equal(
                    StrategicBiomeKind.Rainforest,
                    kind));
    }

    [Fact]
    public void RepeatedClassificationIsDeterministic()
    {
        var bundle =
            CreateGeneratedBundle();

        var first =
            StrategicBiomeClassifier.Generate(
                bundle.Physical,
                bundle.Climate,
                bundle.Hydrology);

        var second =
            StrategicBiomeClassifier.Generate(
                bundle.Physical,
                bundle.Climate,
                bundle.Hydrology);

        Assert.Equal(
            first.Kinds,
            second.Kinds);
    }

    [Fact]
    public void BiomeMapLookupByCellMatchesCanonicalIndex()
    {
        var bundle =
            CreateGeneratedBundle();

        var biomes =
            StrategicBiomeClassifier.Generate(
                bundle.Physical,
                bundle.Climate,
                bundle.Hydrology);

        for (var index = 0;
             index < biomes.Count;
             index++)
        {
            Assert.Equal(
                biomes.GetKind(
                    index),
                biomes.GetKind(
                    biomes.SurfaceGraph.GetCellId(
                        index)));
        }
    }

    [Fact]
    public void InvalidBiomeIndexIsRejected()
    {
        var bundle =
            CreateGeneratedBundle();

        var biomes =
            StrategicBiomeClassifier.Generate(
                bundle.Physical,
                bundle.Climate,
                bundle.Hydrology);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                biomes.GetKind(
                    -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                biomes.GetKind(
                    biomes.Count));
    }

    [Fact]
    public void WorldGenerationResultIncludesStrategicBiomes()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    13UL));

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicBiomes.SurfaceGraph);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicBiomes.Count);
    }

    [Fact]
    public void ResultBiomesCanBeReDerivedWithoutRandomState()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    13UL));

        var regenerated =
            StrategicBiomeClassifier.Generate(
                result.StrategicPhysicalFields,
                result.StrategicClimateFields,
                result.StrategicHydrologyFields);

        Assert.Equal(
            result.StrategicBiomes.Kinds,
            regenerated.Kinds);
    }

    [Fact]
    public void GeneratedWorldUsesOnlyDefinedBiomeKinds()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    17UL));

        Assert.All(
            result.StrategicBiomes.Kinds,
            kind =>
                Assert.True(
                    Enum.IsDefined(
                        kind)));
    }

    [Fact]
    public void DifferentSeedsCanChangeBiomeDistributionWithoutChangingTopology()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var first =
            generator.Generate(
                CreateRequest(
                    1UL));

        var second =
            generator.Generate(
                CreateRequest(
                    2UL));

        Assert.Equal(
            first.StrategicSurfaceGraph.CellIds,
            second.StrategicSurfaceGraph.CellIds);

        Assert.False(
            first.StrategicBiomes.Kinds.SequenceEqual(
                second.StrategicBiomes.Kinds));
    }

    private static StrategicBiomeMap CreateUniformBiomeMap(
        long elevation,
        long temperature,
        long moisture,
        long availability,
        long seaLevel = -1_000_000L)
    {
        var graph =
            CreateGraph();

        return CreateBiomeMap(
            graph,
            Enumerable.Repeat(
                elevation,
                graph.NodeCount)
                .ToArray(),
            temperature,
            moisture,
            availability,
            seaLevel);
    }

    private static StrategicBiomeMap CreateBiomeMap(
        StrategicSurfaceGraph graph,
        long[] elevationValues,
        long temperature,
        long moisture,
        long availability,
        long seaLevel)
    {
        var elevation =
            new StrategicScalarField(
                graph,
                elevationValues);

        var landWater =
            new StrategicLandWaterMap(
                elevation,
                new StrategicSeaLevel(
                    seaLevel));

        var temperatureField =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    temperature,
                    graph.NodeCount));

        var moistureField =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    moisture,
                    graph.NodeCount));

        var availabilityField =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    availability,
                    graph.NodeCount));

        var hydrology =
            StrategicHydrologyFieldGenerator.Generate(
                elevation,
                landWater,
                availabilityField);

        return StrategicBiomeClassifier.Generate(
            landWater,
            temperatureField,
            moistureField,
            availabilityField,
            hydrology);
    }

    private static (
        StrategicSurfaceGraph Graph,
        StrategicPhysicalFieldSet Physical,
        StrategicClimateFieldSet Climate,
        StrategicHydrologyFieldSet Hydrology)
        CreateGeneratedBundle()
    {
        var request =
            CreateRequest(
                13UL);

        var graph =
            new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(
                    request.StrategicParameters));

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        var climate =
            StrategicClimateFieldSet.Generate(
                request,
                graph,
                physical);

        var hydrology =
            StrategicHydrologyFieldGenerator.Generate(
                physical,
                climate);

        return (
            graph,
            physical,
            climate,
            hydrology);
    }

    private static WorldGenerationRequest CreateRequest(
        ulong seed)
    {
        return new WorldGenerationRequest(
            new WorldSeed(
                seed),
            WorldGenerationVersion.Initial,
            new GoldbergParameters(
                1,
                0));
    }

    private static StrategicSurfaceGraph CreateGraph()
    {
        return new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    1,
                    0)));
    }
}
