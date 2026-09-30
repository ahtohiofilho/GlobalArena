using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicWaterAvailabilityFieldGeneratorTests
{
    [Fact]
    public void NullMoistureIsRejected()
    {
        var physical =
            CreatePhysicalFields();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicWaterAvailabilityFieldGenerator.Generate(
                    null!,
                    physical));
    }

    [Fact]
    public void NullPhysicalFieldsAreRejected()
    {
        var moisture =
            CreateMoistureField();

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicWaterAvailabilityFieldGenerator.Generate(
                    moisture,
                    null!));
    }

    [Fact]
    public void DifferentSurfaceGraphsAreRejected()
    {
        var moisture =
            CreateMoistureField();

        var otherRequest =
            CreateRequest(
                77UL,
                new GoldbergParameters(
                    1,
                    0));

        var otherGraph =
            CreateGraph(
                otherRequest.StrategicParameters);

        var physical =
            StrategicPhysicalFieldSet.Generate(
                otherRequest,
                otherGraph);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicWaterAvailabilityFieldGenerator.Generate(
                    moisture,
                    physical));
    }

    [Fact]
    public void ProducesExactlyOneValuePerSurfaceNode()
    {
        var moisture =
            CreateMoistureField();

        var physical =
            CreatePhysicalFields(
                moisture.SurfaceGraph);

        var field =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physical);

        Assert.Equal(
            moisture.Count,
            field.Count);

        Assert.Same(
            moisture.SurfaceGraph,
            field.SurfaceGraph);
    }

    [Fact]
    public void WaterCellsReceiveMaximumAvailability()
    {
        var moisture =
            CreateMoistureField();

        var physical =
            CreatePhysicalFields(
                moisture.SurfaceGraph);

        var field =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physical);

        for (var index = 0;
             index < field.Count;
             index++)
        {
            if (physical.LandWater.GetKind(
                index)
                == StrategicLandWaterKind.Water)
            {
                Assert.Equal(
                    StrategicScalarField.Denominator,
                    field.GetRawValue(
                        index));
            }
        }
    }

    [Fact]
    public void LandCellsPreserveMoistureAvailability()
    {
        var moisture =
            CreateMoistureField();

        var physical =
            CreatePhysicalFields(
                moisture.SurfaceGraph);

        var field =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physical);

        for (var index = 0;
             index < field.Count;
             index++)
        {
            if (physical.LandWater.GetKind(
                index)
                == StrategicLandWaterKind.Land)
            {
                Assert.Equal(
                    moisture.GetRawValue(
                        index),
                    field.GetRawValue(
                        index));
            }
        }
    }

    [Fact]
    public void AvailabilityIsAlwaysNormalized()
    {
        var moisture =
            CreateMoistureField();

        var physical =
            CreatePhysicalFields(
                moisture.SurfaceGraph);

        var field =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physical);

        Assert.All(
            field.RawValues,
            value =>
                Assert.InRange(
                    value,
                    0L,
                    StrategicScalarField.Denominator));
    }

    [Fact]
    public void OutOfRangeMoistureIsRejected()
    {
        var graph =
            CreateGraph(
                new GoldbergParameters(
                    1,
                    0));

        var values =
            Enumerable.Repeat(
                0L,
                graph.NodeCount).
                ToArray();

        values[0] =
            StrategicScalarField.Denominator
            + 1L;

        var moisture =
            new StrategicScalarField(
                graph,
                values);

        var request =
            CreateRequest(
                77UL,
                graph.StrategicTopology.Parameters);

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicWaterAvailabilityFieldGenerator.Generate(
                    moisture,
                    physical));
    }

    [Fact]
    public void RepeatedDerivationIsDeterministic()
    {
        var moisture =
            CreateMoistureField();

        var physical =
            CreatePhysicalFields(
                moisture.SurfaceGraph);

        var first =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physical);

        var second =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                moisture,
                physical);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    private static StrategicScalarField CreateMoistureField()
    {
        var request =
            CreateRequest(
                77UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        return StrategicMoistureFieldGenerator.Generate(
            request,
            graph);
    }

    private static StrategicPhysicalFieldSet CreatePhysicalFields()
    {
        var request =
            CreateRequest(
                77UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        return StrategicPhysicalFieldSet.Generate(
            request,
            graph);
    }

    private static StrategicPhysicalFieldSet CreatePhysicalFields(
        StrategicSurfaceGraph graph)
    {
        var request =
            CreateRequest(
                77UL,
                graph.StrategicTopology.Parameters);

        return StrategicPhysicalFieldSet.Generate(
            request,
            graph);
    }

    private static WorldGenerationRequest CreateRequest(
        ulong seed,
        GoldbergParameters parameters)
    {
        return new WorldGenerationRequest(
            new WorldSeed(
                seed),
            WorldGenerationVersion.Initial,
            parameters);
    }

    private static StrategicSurfaceGraph CreateGraph(
        GoldbergParameters parameters)
    {
        return new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(
                parameters));
    }
}
