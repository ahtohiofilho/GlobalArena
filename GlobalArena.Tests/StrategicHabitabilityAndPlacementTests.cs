using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicHabitabilityAndPlacementTests
{
    [Fact]
    public void HabitabilityPolicyRejectsNonPositiveVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new StrategicHabitabilityPolicy(
                    0,
                    500_000L,
                    500_000L,
                    0L));
    }

    [Fact]
    public void HabitabilityPolicyRejectsAllZeroWeights()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new StrategicHabitabilityPolicy(
                    1,
                    0L,
                    0L,
                    0L));
    }

    [Fact]
    public void HabitabilityPolicyRejectsInvalidWaterMultiplier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new StrategicHabitabilityPolicy(
                    1,
                    500_000L,
                    500_000L,
                    1_000_001L));
    }

    [Fact]
    public void PlacementPolicyRejectsNonPositiveVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new StrategicCivilizationPlacementPolicy(
                    0,
                    500_000L,
                    500_000L,
                    500_000L,
                    500_000L,
                    0L,
                    true));
    }

    [Fact]
    public void PlacementPolicyRejectsAllZeroLocalWeights()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new StrategicCivilizationPlacementPolicy(
                    1,
                    0L,
                    0L,
                    500_000L,
                    500_000L,
                    0L,
                    true));
    }

    [Fact]
    public void PlacementPolicyRejectsAllZeroSelectionWeights()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new StrategicCivilizationPlacementPolicy(
                    1,
                    500_000L,
                    500_000L,
                    0L,
                    0L,
                    0L,
                    true));
    }

    [Fact]
    public void NullPhysicalFieldsAreRejectedForHabitability()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicHabitabilityFieldGenerator.Generate(
                    null!,
                    bundle.Climate));
    }

    [Fact]
    public void NullClimateFieldsAreRejectedForHabitability()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicHabitabilityFieldGenerator.Generate(
                    bundle.Physical,
                    null!));
    }

    [Fact]
    public void HabitabilityRequiresSharedCanonicalGraph()
    {
        var first =
            CreateGeneratedBundle();

        var second =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentException>(
            () =>
                StrategicHabitabilityFieldGenerator.Generate(
                    first.Physical,
                    second.Climate));
    }

    [Fact]
    public void HabitabilityContainsOneNormalizedValuePerNode()
    {
        var bundle =
            CreateGeneratedBundle();

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        Assert.Equal(
            bundle.Graph.NodeCount,
            habitability.Count);

        Assert.All(
            habitability.RawValues,
            value =>
                Assert.InRange(
                    value,
                    0L,
                    StrategicScalarField.Denominator));
    }

    [Fact]
    public void DefaultHabitabilityPolicyMakesWaterBaselineZero()
    {
        var fields =
            CreateUniformFields(
                elevation: 0L,
                seaLevel: 0L,
                temperature: 0L,
                waterAvailability: 1_000_000L);

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                fields.Physical,
                fields.Climate);

        Assert.All(
            habitability.RawValues,
            value =>
                Assert.Equal(
                    0L,
                    value));
    }

    [Fact]
    public void WaterHabitabilityRemainsPolicyConfigurable()
    {
        var fields =
            CreateUniformFields(
                elevation: 0L,
                seaLevel: 0L,
                temperature: 0L,
                waterAvailability: 1_000_000L);

        var policy =
            new StrategicHabitabilityPolicy(
                2,
                500_000L,
                500_000L,
                1_000_000L);

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                fields.Physical,
                fields.Climate,
                policy);

        Assert.All(
            habitability.RawValues,
            value =>
                Assert.Equal(
                    1_000_000L,
                    value));
    }

    [Fact]
    public void TemperatureComfortCanAffectHabitability()
    {
        var temperate =
            CreateUniformFields(
                elevation: 100_000L,
                seaLevel: -1_000_000L,
                temperature: 0L,
                waterAvailability: 500_000L);

        var extreme =
            CreateUniformFields(
                elevation: 100_000L,
                seaLevel: -1_000_000L,
                temperature: 1_000_000L,
                waterAvailability: 500_000L);

        var first =
            StrategicHabitabilityFieldGenerator.Generate(
                temperate.Physical,
                temperate.Climate);

        var second =
            StrategicHabitabilityFieldGenerator.Generate(
                extreme.Physical,
                extreme.Climate);

        Assert.True(
            first.GetRawValue(
                0)
            > second.GetRawValue(
                0));
    }

    [Fact]
    public void RepeatedHabitabilityGenerationIsDeterministic()
    {
        var bundle =
            CreateGeneratedBundle();

        var first =
            StrategicHabitabilityFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        var second =
            StrategicHabitabilityFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void PlacementSuitabilityRequiresSharedCanonicalGraph()
    {
        var first =
            CreateGeneratedBundle();

        var second =
            CreateGeneratedBundle();

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                first.Physical,
                first.Climate);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                    first.Physical,
                    habitability,
                    second.Resources));
    }

    [Fact]
    public void PlacementSuitabilityContainsOneNormalizedValuePerNode()
    {
        var bundle =
            CreateGeneratedBundle();

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        var suitability =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                bundle.Physical,
                habitability,
                bundle.Resources);

        Assert.Equal(
            bundle.Graph.NodeCount,
            suitability.Count);

        Assert.All(
            suitability.RawValues,
            value =>
                Assert.InRange(
                    value,
                    0L,
                    StrategicScalarField.Denominator));
    }

    [Fact]
    public void DefaultPlacementPolicyMakesWaterLocalSuitabilityZero()
    {
        var fields =
            CreateUniformFields(
                elevation: 0L,
                seaLevel: 0L,
                temperature: 0L,
                waterAvailability: 1_000_000L);

        var resources =
            new StrategicResourcePotentialFieldSetAccessor(
                fields.Graph,
                1_000_000L)
                .Fields;

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                fields.Physical,
                fields.Climate,
                new StrategicHabitabilityPolicy(
                    1,
                    500_000L,
                    500_000L,
                    1_000_000L));

        var suitability =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                fields.Physical,
                habitability,
                resources);

        Assert.All(
            suitability.RawValues,
            value =>
                Assert.Equal(
                    0L,
                    value));
    }

    [Fact]
    public void WaterPlacementSuitabilityRemainsPolicyConfigurable()
    {
        var fields =
            CreateUniformFields(
                elevation: 0L,
                seaLevel: 0L,
                temperature: 0L,
                waterAvailability: 1_000_000L);

        var resources =
            new StrategicResourcePotentialFieldSetAccessor(
                fields.Graph,
                1_000_000L)
                .Fields;

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                fields.Physical,
                fields.Climate,
                new StrategicHabitabilityPolicy(
                    1,
                    500_000L,
                    500_000L,
                    1_000_000L));

        var policy =
            new StrategicCivilizationPlacementPolicy(
                2,
                500_000L,
                500_000L,
                500_000L,
                500_000L,
                1_000_000L,
                true);

        var suitability =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                fields.Physical,
                habitability,
                resources,
                policy);

        Assert.All(
            suitability.RawValues,
            value =>
                Assert.Equal(
                    1_000_000L,
                    value));
    }

    [Fact]
    public void ResourcePotentialCanAffectPlacementWithoutChangingHabitability()
    {
        var fields =
            CreateUniformFields(
                elevation: 100_000L,
                seaLevel: -1_000_000L,
                temperature: 0L,
                waterAvailability: 500_000L);

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                fields.Physical,
                fields.Climate);

        var lowResources =
            new StrategicResourcePotentialFieldSetAccessor(
                fields.Graph,
                0L)
                .Fields;

        var highResources =
            new StrategicResourcePotentialFieldSetAccessor(
                fields.Graph,
                1_000_000L)
                .Fields;

        var low =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                fields.Physical,
                habitability,
                lowResources);

        var high =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                fields.Physical,
                habitability,
                highResources);

        Assert.True(
            high.GetRawValue(
                0)
            > low.GetRawValue(
                0));
    }

    [Fact]
    public void SelectorRejectsZeroCandidateCount()
    {
        var bundle =
            CreateGeneratedBundle();

        var suitability =
            CreatePlacementSuitability(
                bundle);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                StrategicCivilizationStartCandidateSelector.Select(
                    bundle.Request,
                    bundle.Graph,
                    suitability,
                    0));
    }

    [Fact]
    public void SelectorRejectsTooManyCandidatesWhenReferenceIsExcluded()
    {
        var bundle =
            CreateGeneratedBundle();

        var suitability =
            CreatePlacementSuitability(
                bundle);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                StrategicCivilizationStartCandidateSelector.Select(
                    bundle.Request,
                    bundle.Graph,
                    suitability,
                    bundle.Graph.NodeCount));
    }

    [Fact]
    public void SelectorUsesCivilizationPlacementDomainForInitialReference()
    {
        var bundle =
            CreateGeneratedBundle();

        var suitability =
            CreatePlacementSuitability(
                bundle);

        var selected =
            StrategicCivilizationStartCandidateSelector.Select(
                bundle.Request,
                bundle.Graph,
                suitability,
                2);

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                bundle.Request,
                WorldGenerationRandomDomain.CivilizationPlacement);

        var expectedIndex =
            checked(
                (int)(
                    stream.NextUInt64()
                    % (ulong)bundle.Graph.NodeCount));

        Assert.Equal(
            bundle.Graph.GetCellId(
                expectedIndex),
            selected.InitialReferenceCellId);
    }

    [Fact]
    public void DefaultSelectorExcludesInitialReference()
    {
        var bundle =
            CreateGeneratedBundle();

        var suitability =
            CreatePlacementSuitability(
                bundle);

        var selected =
            StrategicCivilizationStartCandidateSelector.Select(
                bundle.Request,
                bundle.Graph,
                suitability,
                3);

        Assert.DoesNotContain(
            selected.InitialReferenceCellId,
            selected.CandidateCellIds);
    }

    [Fact]
    public void SelectionIsDeterministicAndUnique()
    {
        var bundle =
            CreateGeneratedBundle();

        var suitability =
            CreatePlacementSuitability(
                bundle);

        var first =
            StrategicCivilizationStartCandidateSelector.Select(
                bundle.Request,
                bundle.Graph,
                suitability,
                4);

        var second =
            StrategicCivilizationStartCandidateSelector.Select(
                bundle.Request,
                bundle.Graph,
                suitability,
                4);

        Assert.Equal(
            first.InitialReferenceCellId,
            second.InitialReferenceCellId);

        Assert.Equal(
            first.CandidateCellIds,
            second.CandidateCellIds);

        Assert.Equal(
            first.Count,
            first.CandidateCellIds.Distinct().Count());
    }

    [Fact]
    public void PureDispersionPolicyCanPreferLowerLocalSuitability()
    {
        var request =
            CreateRequest(
                41UL);

        var graph =
            CreateGraph();

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.CivilizationPlacement);

        var referenceIndex =
            checked(
                (int)(
                    stream.NextUInt64()
                    % (ulong)graph.NodeCount));

        var values =
            Enumerable.Repeat(
                0L,
                graph.NodeCount)
                .ToArray();

        var neighbor =
            graph.GetNeighborIndexes(
                referenceIndex)[0];

        values[neighbor] =
            1_000_000L;

        var suitability =
            new StrategicScalarField(
                graph,
                values);

        var policy =
            new StrategicCivilizationPlacementPolicy(
                3,
                500_000L,
                500_000L,
                0L,
                1_000_000L,
                1_000_000L,
                true);

        var selected =
            StrategicCivilizationStartCandidateSelector.Select(
                request,
                graph,
                suitability,
                1,
                policy);

        Assert.NotEqual(
            graph.GetCellId(
                neighbor),
            selected.CandidateCellIds[0]);
    }

    [Fact]
    public void LocalOnlyPolicyChoosesHighestSuitabilityWithCanonicalTieBreak()
    {
        var request =
            CreateRequest(
                43UL);

        var graph =
            CreateGraph();

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.CivilizationPlacement);

        var referenceIndex =
            checked(
                (int)(
                    stream.NextUInt64()
                    % (ulong)graph.NodeCount));

        var eligible =
            Enumerable.Range(
                0,
                graph.NodeCount)
                .Where(
                    index =>
                        index != referenceIndex)
                .ToArray();

        var firstEligible =
            eligible[0];

        var secondEligible =
            eligible[1];

        var values =
            new long[
                graph.NodeCount];

        values[firstEligible] =
            900_000L;

        values[secondEligible] =
            900_000L;

        var suitability =
            new StrategicScalarField(
                graph,
                values);

        var policy =
            new StrategicCivilizationPlacementPolicy(
                4,
                500_000L,
                500_000L,
                1_000_000L,
                0L,
                1_000_000L,
                true);

        var selected =
            StrategicCivilizationStartCandidateSelector.Select(
                request,
                graph,
                suitability,
                1,
                policy);

        Assert.Equal(
            graph.GetCellId(
                Math.Min(
                    firstEligible,
                    secondEligible)),
            selected.CandidateCellIds[0]);
    }

    [Fact]
    public void InitialReferenceCanRemainEligibleWhenPolicyAllowsIt()
    {
        var bundle =
            CreateGeneratedBundle();

        var values =
            Enumerable.Repeat(
                0L,
                bundle.Graph.NodeCount)
                .ToArray();

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                bundle.Request,
                WorldGenerationRandomDomain.CivilizationPlacement);

        var referenceIndex =
            checked(
                (int)(
                    stream.NextUInt64()
                    % (ulong)bundle.Graph.NodeCount));

        values[referenceIndex] =
            1_000_000L;

        var suitability =
            new StrategicScalarField(
                bundle.Graph,
                values);

        var policy =
            new StrategicCivilizationPlacementPolicy(
                5,
                500_000L,
                500_000L,
                1_000_000L,
                0L,
                1_000_000L,
                false);

        var selected =
            StrategicCivilizationStartCandidateSelector.Select(
                bundle.Request,
                bundle.Graph,
                suitability,
                1,
                policy);

        Assert.Equal(
            selected.InitialReferenceCellId,
            selected.CandidateCellIds[0]);
    }

    [Fact]
    public void WorldGenerationResultIncludesHabitabilityAndPlacementSuitability()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    47UL));

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicHabitability.SurfaceGraph);

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicCivilizationPlacementSuitability.SurfaceGraph);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicHabitability.Count);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicCivilizationPlacementSuitability.Count);
    }

    [Fact]
    public void ResultFieldsCanBeRegeneratedWithoutMutatingSourceLayers()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    53UL));

        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                result.StrategicPhysicalFields,
                result.StrategicClimateFields);

        var suitability =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                result.StrategicPhysicalFields,
                habitability,
                result.StrategicResourcePotentialFields);

        Assert.Equal(
            result.StrategicHabitability.RawValues,
            habitability.RawValues);

        Assert.Equal(
            result.StrategicCivilizationPlacementSuitability.RawValues,
            suitability.RawValues);
    }

    private static StrategicScalarField CreatePlacementSuitability(
        GeneratedBundle bundle)
    {
        var habitability =
            StrategicHabitabilityFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        return StrategicCivilizationPlacementSuitabilityGenerator.Generate(
            bundle.Physical,
            habitability,
            bundle.Resources);
    }

    private static GeneratedBundle CreateGeneratedBundle()
    {
        var request =
            CreateRequest(
                37UL);

        var graph =
            CreateGraph();

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        var climate =
            StrategicClimateFieldSet.Generate(
                request,
                graph,
                physical);

        var resources =
            StrategicResourcePotentialFieldSet.Generate(
                request,
                graph);

        return new GeneratedBundle(
            request,
            graph,
            physical,
            climate,
            resources);
    }

    private static (
        StrategicSurfaceGraph Graph,
        StrategicPhysicalFieldSet Physical,
        StrategicClimateFieldSet Climate)
        CreateUniformFields(
            long elevation,
            long seaLevel,
            long temperature,
            long waterAvailability)
    {
        var graph =
            CreateGraph();

        var elevationField =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    elevation,
                    graph.NodeCount));

        var relief =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    0L,
                    graph.NodeCount));

        var sea =
            new StrategicSeaLevel(
                seaLevel);

        var landWater =
            new StrategicLandWaterMap(
                elevationField,
                sea);

        var physical =
            StrategicPhysicalFieldSetAccessor.Create(
                elevationField,
                relief,
                sea,
                landWater);

        var temperatureField =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    temperature,
                    graph.NodeCount));

        var moisture =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    waterAvailability,
                    graph.NodeCount));

        var availability =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    waterAvailability,
                    graph.NodeCount));

        var climate =
            StrategicClimateFieldSetAccessor.Create(
                temperatureField,
                moisture,
                availability);

        return (
            graph,
            physical,
            climate);
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

    private sealed record GeneratedBundle(
        WorldGenerationRequest Request,
        StrategicSurfaceGraph Graph,
        StrategicPhysicalFieldSet Physical,
        StrategicClimateFieldSet Climate,
        StrategicResourcePotentialFieldSet Resources);

    private sealed class StrategicResourcePotentialFieldSetAccessor
    {
        public StrategicResourcePotentialFieldSet Fields { get; }

        public StrategicResourcePotentialFieldSetAccessor(
            StrategicSurfaceGraph graph,
            long rawValue)
        {
            var request =
                CreateRequest(
                    59UL);

            var generated =
                StrategicResourcePotentialFieldSet.Generate(
                    request,
                    graph);

            var field =
                new StrategicScalarField(
                    graph,
                    Enumerable.Repeat(
                        rawValue,
                        graph.NodeCount));

            Fields =
                (StrategicResourcePotentialFieldSet)
                    Activator.CreateInstance(
                        typeof(StrategicResourcePotentialFieldSet),
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic,
                        binder: null,
                        args: new object[]
                        {
                            field
                        },
                        culture: null)!;
        }
    }

    private static class StrategicPhysicalFieldSetAccessor
    {
        public static StrategicPhysicalFieldSet Create(
            StrategicScalarField elevation,
            StrategicScalarField relief,
            StrategicSeaLevel seaLevel,
            StrategicLandWaterMap landWater)
        {
            return
                (StrategicPhysicalFieldSet)
                    Activator.CreateInstance(
                        typeof(StrategicPhysicalFieldSet),
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic,
                        binder: null,
                        args: new object[]
                        {
                            elevation,
                            relief,
                            seaLevel,
                            landWater
                        },
                        culture: null)!;
        }
    }

    private static class StrategicClimateFieldSetAccessor
    {
        public static StrategicClimateFieldSet Create(
            StrategicScalarField temperature,
            StrategicScalarField moisture,
            StrategicScalarField waterAvailability)
        {
            return
                (StrategicClimateFieldSet)
                    Activator.CreateInstance(
                        typeof(StrategicClimateFieldSet),
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic,
                        binder: null,
                        args: new object[]
                        {
                            temperature,
                            moisture,
                            waterAvailability
                        },
                        culture: null)!;
        }
    }
}
