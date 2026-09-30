using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicLandWaterMapTests
{
    [Fact]
    public void DefaultSeaLevelIsZero()
    {
        Assert.Equal(
            0L,
            StrategicSeaLevel.Default.RawValue);
    }

    [Fact]
    public void NullElevationIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicLandWaterMap(
                    null!,
                    StrategicSeaLevel.Default));
    }

    [Fact]
    public void MapCountMatchesElevationCount()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        Assert.Equal(
            elevation.Count,
            map.Count);
    }

    [Fact]
    public void ValueAboveSeaLevelIsLand()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        Assert.Equal(
            StrategicLandWaterKind.Land,
            map.GetKind(
                2));
    }

    [Fact]
    public void ValueEqualToSeaLevelIsWater()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        Assert.Equal(
            StrategicLandWaterKind.Water,
            map.GetKind(
                1));
    }

    [Fact]
    public void ValueBelowSeaLevelIsWater()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        Assert.Equal(
            StrategicLandWaterKind.Water,
            map.GetKind(
                0));
    }

    [Fact]
    public void LandAndWaterCountsCoverEveryNode()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        Assert.Equal(
            map.Count,
            map.LandCount
            + map.WaterCount);
    }

    [Fact]
    public void KindSnapshotIsReadOnly()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        var kinds =
            Assert.IsAssignableFrom<
                IList<StrategicLandWaterKind>>(
                map.Kinds);

        Assert.Throws<NotSupportedException>(
            () =>
                kinds.Add(
                    StrategicLandWaterKind.Land));
    }

    [Fact]
    public void CellIdLookupUsesCanonicalSurfaceIndex()
    {
        var elevation =
            CreateClassificationElevation();

        var map =
            new StrategicLandWaterMap(
                elevation,
                StrategicSeaLevel.Default);

        for (var index = 0;
             index < map.Count;
             index++)
        {
            Assert.Equal(
                map.GetKind(
                    index),
                map.GetKind(
                    elevation.SurfaceGraph.GetCellId(
                        index)));
        }
    }

    [Fact]
    public void RepeatedClassificationIsDeterministic()
    {
        var elevation =
            CreateClassificationElevation();

        var first =
            new StrategicLandWaterMap(
                elevation,
                new StrategicSeaLevel(
                    100L));

        var second =
            new StrategicLandWaterMap(
                elevation,
                new StrategicSeaLevel(
                    100L));

        Assert.Equal(
            first.Kinds,
            second.Kinds);

        Assert.Equal(
            first.LandCount,
            second.LandCount);

        Assert.Equal(
            first.WaterCount,
            second.WaterCount);
    }

    private static StrategicScalarField CreateClassificationElevation()
    {
        var graph =
            new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        1,
                        0)));

        var values =
            new long[
                graph.NodeCount];

        values[0] =
            -1L;

        values[1] =
            0L;

        values[2] =
            1L;

        return new StrategicScalarField(
            graph,
            values);
    }
}
