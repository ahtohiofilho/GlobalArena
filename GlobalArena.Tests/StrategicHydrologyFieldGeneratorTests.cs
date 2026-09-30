using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicHydrologyFieldGeneratorTests
{
    [Fact]
    public void NullPhysicalFieldsAreRejected()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicHydrologyFieldGenerator.Generate(
                    null!,
                    bundle.Climate));
    }

    [Fact]
    public void NullClimateFieldsAreRejected()
    {
        var bundle =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicHydrologyFieldGenerator.Generate(
                    bundle.Physical,
                    null!));
    }

    [Fact]
    public void PhysicalAndClimateGraphsMustMatch()
    {
        var first =
            CreateGeneratedBundle();

        var second =
            CreateGeneratedBundle();

        Assert.Throws<ArgumentException>(
            () =>
                StrategicHydrologyFieldGenerator.Generate(
                    first.Physical,
                    second.Climate));
    }

    [Fact]
    public void LandWaterMustReferenceSuppliedElevation()
    {
        var graph =
            CreateGraph();

        var firstElevation =
            CreateElevation(
                graph,
                100L);

        var secondElevation =
            CreateElevation(
                graph,
                100L);

        var landWater =
            new StrategicLandWaterMap(
                secondElevation,
                new StrategicSeaLevel(
                    -1L));

        var availability =
            CreateAvailability(
                graph,
                500_000L);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicHydrologyFieldGenerator.Generate(
                    firstElevation,
                    landWater,
                    availability));
    }

    [Fact]
    public void WaterAvailabilityMustShareSurfaceGraph()
    {
        var firstGraph =
            CreateGraph();

        var secondGraph =
            CreateGraph();

        var elevation =
            CreateElevation(
                firstGraph,
                100L);

        var landWater =
            new StrategicLandWaterMap(
                elevation,
                new StrategicSeaLevel(
                    -1L));

        var availability =
            CreateAvailability(
                secondGraph,
                500_000L);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicHydrologyFieldGenerator.Generate(
                    elevation,
                    landWater,
                    availability));
    }

    [Fact]
    public void OutOfRangeWaterAvailabilityIsRejected()
    {
        var graph =
            CreateGraph();

        var elevation =
            CreateElevation(
                graph,
                100L);

        var landWater =
            new StrategicLandWaterMap(
                elevation,
                new StrategicSeaLevel(
                    -1L));

        var values =
            Enumerable.Repeat(
                500_000L,
                graph.NodeCount)
                .ToArray();

        values[0] =
            StrategicScalarField.Denominator
            + 1L;

        var availability =
            new StrategicScalarField(
                graph,
                values);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicHydrologyFieldGenerator.Generate(
                    elevation,
                    landWater,
                    availability));
    }

    [Fact]
    public void GeneratedHydrologyContainsOneValuePerStrategicNode()
    {
        var bundle =
            CreateGeneratedBundle();

        var hydrology =
            StrategicHydrologyFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        Assert.Equal(
            bundle.Graph.NodeCount,
            hydrology.Count);

        Assert.Equal(
            bundle.Graph.NodeCount,
            hydrology.FlowAccumulation.Count);
    }

    [Fact]
    public void EveryWaterNodeIsTerminalOutlet()
    {
        var hydrology =
            GenerateDefault();

        for (var index = 0;
             index < hydrology.Count;
             index++)
        {
            if (hydrology.LandWater.GetKind(
                index)
                == StrategicLandWaterKind.Water)
            {
                Assert.Equal(
                    StrategicHydrologyNodeKind.WaterOutlet,
                    hydrology.GetNodeKind(
                        index));

                Assert.Null(
                    hydrology.GetDownstreamNodeIndex(
                        index));
            }
        }
    }

    [Fact]
    public void EveryDownstreamDestinationIsAdjacent()
    {
        var hydrology =
            GenerateDefault();

        for (var index = 0;
             index < hydrology.Count;
             index++)
        {
            var target =
                hydrology.GetDownstreamNodeIndex(
                    index);

            if (target.HasValue)
            {
                Assert.Contains(
                    target.Value,
                    hydrology.SurfaceGraph.GetNeighborIndexes(
                        index));
            }
        }
    }

    [Fact]
    public void EveryDownstreamDestinationIsStrictlyLower()
    {
        var hydrology =
            GenerateDefault();

        for (var index = 0;
             index < hydrology.Count;
             index++)
        {
            var target =
                hydrology.GetDownstreamNodeIndex(
                    index);

            if (target.HasValue)
            {
                Assert.True(
                    hydrology.Elevation.GetRawValue(
                        target.Value)
                    < hydrology.Elevation.GetRawValue(
                        index));
            }
        }
    }

    [Fact]
    public void LandNodeWithoutLowerNeighborIsInlandSink()
    {
        var graph =
            CreateGraph();

        var values =
            Enumerable.Repeat(
                20L,
                graph.NodeCount)
                .ToArray();

        values[0] =
            10L;

        var hydrology =
            CreateHydrology(
                graph,
                values,
                -100L);

        Assert.Equal(
            StrategicHydrologyNodeKind.InlandSink,
            hydrology.GetNodeKind(
                0));

        Assert.Null(
            hydrology.GetDownstreamNodeIndex(
                0));
    }

    [Fact]
    public void LowestAdjacentElevationWins()
    {
        var graph =
            CreateGraph();

        var neighbors =
            graph.GetNeighborIndexes(
                0);

        var values =
            Enumerable.Repeat(
                200L,
                graph.NodeCount)
                .ToArray();

        values[0] =
            100L;

        values[neighbors[0]] =
            70L;

        values[neighbors[1]] =
            40L;

        values[neighbors[2]] =
            60L;

        var hydrology =
            CreateHydrology(
                graph,
                values,
                -100L);

        Assert.Equal<int?>(
            neighbors[1],
            hydrology.GetDownstreamNodeIndex(
                0));
    }

    [Fact]
    public void EqualLowestElevationUsesCanonicalNodeIndex()
    {
        var graph =
            CreateGraph();

        var neighbors =
            graph.GetNeighborIndexes(
                0);

        var first =
            Math.Min(
                neighbors[0],
                neighbors[1]);

        var second =
            Math.Max(
                neighbors[0],
                neighbors[1]);

        var values =
            Enumerable.Repeat(
                200L,
                graph.NodeCount)
                .ToArray();

        values[0] =
            100L;

        values[first] =
            40L;

        values[second] =
            40L;

        var hydrology =
            CreateHydrology(
                graph,
                values,
                -100L);

        Assert.Equal<int?>(
            first,
            hydrology.GetDownstreamNodeIndex(
                0));
    }

    [Fact]
    public void StrictDescentPreventsDrainageCycles()
    {
        var hydrology =
            GenerateDefault();

        for (var start = 0;
             start < hydrology.Count;
             start++)
        {
            var visited =
                new HashSet<int>();

            var current =
                start;

            while (true)
            {
                Assert.True(
                    visited.Add(
                        current));

                var target =
                    hydrology.GetDownstreamNodeIndex(
                        current);

                if (!target.HasValue)
                {
                    break;
                }

                current =
                    target.Value;
            }
        }
    }

    [Fact]
    public void TerminalAccumulationConservesAllLandRunoff()
    {
        var hydrology =
            GenerateDefault();

        Int128 localRunoff =
            0;

        Int128 terminalAccumulation =
            0;

        for (var index = 0;
             index < hydrology.Count;
             index++)
        {
            if (hydrology.LandWater.GetKind(
                index)
                == StrategicLandWaterKind.Land)
            {
                localRunoff +=
                    hydrology.WaterAvailability.GetRawValue(
                        index);
            }

            if (hydrology.GetNodeKind(
                index)
                != StrategicHydrologyNodeKind.Downstream)
            {
                terminalAccumulation +=
                    hydrology.FlowAccumulation.GetRawValue(
                        index);
            }
        }

        Assert.Equal(
            localRunoff,
            terminalAccumulation);
    }

    [Fact]
    public void AllWaterWorldHasZeroFlowAccumulation()
    {
        var graph =
            CreateGraph();

        var hydrology =
            CreateHydrology(
                graph,
                Enumerable.Repeat(
                    -10L,
                    graph.NodeCount)
                    .ToArray(),
                0L);

        Assert.All(
            hydrology.FlowAccumulation.RawValues,
            value =>
                Assert.Equal(
                    0L,
                    value));
    }

    [Fact]
    public void FlatLandWorldMakesEveryNodeSinkWithLocalAccumulation()
    {
        var graph =
            CreateGraph();

        const long availability =
            321_000L;

        var hydrology =
            CreateHydrology(
                graph,
                Enumerable.Repeat(
                    100L,
                    graph.NodeCount)
                    .ToArray(),
                -1L,
                availability);

        for (var index = 0;
             index < hydrology.Count;
             index++)
        {
            Assert.Equal(
                StrategicHydrologyNodeKind.InlandSink,
                hydrology.GetNodeKind(
                    index));

            Assert.Equal(
                availability,
                hydrology.FlowAccumulation.GetRawValue(
                    index));
        }
    }

    [Fact]
    public void RepeatedGenerationIsDeterministic()
    {
        var bundle =
            CreateGeneratedBundle();

        var first =
            StrategicHydrologyFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        var second =
            StrategicHydrologyFieldGenerator.Generate(
                bundle.Physical,
                bundle.Climate);

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));
    }

    [Fact]
    public void WorldGenerationResultIncludesStrategicHydrology()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    7UL));

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicHydrologyFields.SurfaceGraph);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicHydrologyFields.Count);
    }

    [Fact]
    public void ResultHydrologyCanBeRegeneratedWithoutRandomState()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    7UL));

        var regenerated =
            StrategicHydrologyFieldGenerator.Generate(
                result.StrategicPhysicalFields,
                result.StrategicClimateFields);

        Assert.Equal(
            CreateSignature(
                result.StrategicHydrologyFields),
            CreateSignature(
                regenerated));
    }

    private static StrategicHydrologyFieldSet GenerateDefault()
    {
        var bundle =
            CreateGeneratedBundle();

        return StrategicHydrologyFieldGenerator.Generate(
            bundle.Physical,
            bundle.Climate);
    }

    private static (
        StrategicSurfaceGraph Graph,
        StrategicPhysicalFieldSet Physical,
        StrategicClimateFieldSet Climate)
        CreateGeneratedBundle()
    {
        var request =
            CreateRequest(
                7UL);

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

        return (
            graph,
            physical,
            climate);
    }

    private static StrategicHydrologyFieldSet CreateHydrology(
        StrategicSurfaceGraph graph,
        long[] elevationValues,
        long seaLevel,
        long availability = 500_000L)
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

        var waterAvailability =
            CreateAvailability(
                graph,
                availability);

        return StrategicHydrologyFieldGenerator.Generate(
            elevation,
            landWater,
            waterAvailability);
    }

    private static StrategicScalarField CreateElevation(
        StrategicSurfaceGraph graph,
        long value)
    {
        return new StrategicScalarField(
            graph,
            Enumerable.Repeat(
                value,
                graph.NodeCount));
    }

    private static StrategicScalarField CreateAvailability(
        StrategicSurfaceGraph graph,
        long value)
    {
        return new StrategicScalarField(
            graph,
            Enumerable.Repeat(
                value,
                graph.NodeCount));
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

    private static string CreateSignature(
        StrategicHydrologyFieldSet hydrology)
    {
        return string.Join(
            "|",
            Enumerable.Range(
                0,
                hydrology.Count)
                .Select(
                    index =>
                        index
                        + ":"
                        + (int)hydrology.GetNodeKind(
                            index)
                        + ":"
                        + (hydrology.GetDownstreamNodeIndex(
                            index)?.ToString()
                            ?? "-")
                        + ":"
                        + hydrology.FlowAccumulation.GetRawValue(
                            index)));
    }
}
