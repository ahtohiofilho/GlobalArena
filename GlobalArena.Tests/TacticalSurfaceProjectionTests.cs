using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalSurfaceProjectionTests
{
    private static (TacticalCameraFrame Frame, StrategicSphericalGeometry Geometry) Create()
    {
        var result = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(17UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));
        var geometry = result.StrategicSphericalGeometry;
        return (TacticalCameraFrame.Create(geometry, geometry.Cells[0].CellId), geometry);
    }

    [Fact]
    public void FocusProjectsToOriginOnVisibleHemisphere()
    {
        var (frame, _) = Create();
        var output = TacticalSurfaceProjection.Project(frame, frame.Target);
        Assert.InRange(Math.Abs(output.X), 0d, 1e-12);
        Assert.InRange(Math.Abs(output.Y), 0d, 1e-12);
        Assert.InRange(output.Forward, 1d - 1e-12, 1d + 1e-12);
        Assert.True(output.IsFrontHemisphere);
    }

    [Fact]
    public void BasisProjectsWithExpectedSignsAndVisibility()
    {
        var (frame, _) = Create();
        var right = TacticalSurfaceProjection.Project(frame, frame.Right);
        var up = TacticalSurfaceProjection.Project(frame, frame.Up);
        Assert.InRange(right.X, 1d - 1e-12, 1d + 1e-12);
        Assert.InRange(Math.Abs(right.Y), 0d, 1e-12);
        Assert.InRange(up.Y, 1d - 1e-12, 1d + 1e-12);
        Assert.InRange(Math.Abs(up.X), 0d, 1e-12);
        Assert.False(right.IsFrontHemisphere);
        Assert.False(up.IsFrontHemisphere);
    }

    [Fact]
    public void ProjectionIsDeterministicForEveryCanonicalCellCenter()
    {
        var (frame, geometry) = Create();
        foreach (var cell in geometry.Cells)
        {
            var first = TacticalSurfaceProjection.Project(frame, cell.Center);
            var second = TacticalSurfaceProjection.Project(frame, cell.Center);
            Assert.Equal(first, second);
            Assert.InRange(first.X, -1d - 1e-12, 1d + 1e-12);
            Assert.InRange(first.Y, -1d - 1e-12, 1d + 1e-12);
            Assert.InRange(first.Forward, -1d - 1e-12, 1d + 1e-12);
        }
    }

    [Fact]
    public void RejectsNullCamera()
    {
        var (frame, _) = Create();
        Assert.Throws<ArgumentNullException>(() =>
            TacticalSurfaceProjection.Project(null!, frame.Target));
    }
}