using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalHorizonClipperTests
{
    private static readonly SphericalPoint3 X = new(1, 0, 0);
    private static readonly SphericalPoint3 Y = new(0, 1, 0);

    private static TacticalCameraFrame Camera() =>
        TacticalCameraFrame.Create(
            StrategicSphericalGeometryGeneratorTestGeometry(), new StrategicCellId(1));

    // Use a real deterministic world and its canonical spherical geometry.
    private static StrategicSphericalGeometry StrategicSphericalGeometryGeneratorTestGeometry()
    {
        IWorldGenerator generator = new DeterministicWorldGenerator();
        return generator.Generate(new WorldGenerationRequest(new WorldSeed(1UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(1, 0)))
            .StrategicSphericalGeometry;
    }

    [Fact]
    public void FrontFacingCellPolygonIsPreserved()
    {
        var frame = Camera();
        var n = frame.Target;
        var p = new[] {
            new SphericalPoint3(n.X + 0.1 * frame.Right.X, n.Y + 0.1 * frame.Right.Y, n.Z + 0.1 * frame.Right.Z),
            new SphericalPoint3(n.X + 0.1 * frame.Up.X, n.Y + 0.1 * frame.Up.Y, n.Z + 0.1 * frame.Up.Z),
            new SphericalPoint3(n.X - 0.1 * frame.Right.X, n.Y - 0.1 * frame.Right.Y, n.Z - 0.1 * frame.Right.Z)
        };
        var clipped = TacticalHorizonClipper.Clip(frame, p);
        Assert.Equal(p.Length, clipped.Count);
        Assert.All(clipped, v => Assert.True(TacticalSurfaceProjection.Project(frame, v).Forward > 0d));
    }

    [Fact]
    public void FullyHiddenTriangleReturnsEmpty()
    {
        var frame = Camera();
        var n = frame.Target;
        var p = new[] {
            new SphericalPoint3(-n.X + 0.1 * frame.Right.X, -n.Y + 0.1 * frame.Right.Y, -n.Z + 0.1 * frame.Right.Z),
            new SphericalPoint3(-n.X + 0.1 * frame.Up.X, -n.Y + 0.1 * frame.Up.Y, -n.Z + 0.1 * frame.Up.Z),
            new SphericalPoint3(-n.X - 0.1 * frame.Right.X, -n.Y - 0.1 * frame.Right.Y, -n.Z - 0.1 * frame.Right.Z)
        };
        Assert.Empty(TacticalHorizonClipper.Clip(frame, p));
    }

    [Fact]
    public void CrossingPolygonProducesHorizonIntersectionsAndStableOrdering()
    {
        var frame = Camera();
        var n = frame.Target;
        var p = new[] {
            new SphericalPoint3(n.X + frame.Right.X, n.Y + frame.Right.Y, n.Z + frame.Right.Z),
            new SphericalPoint3(-n.X + frame.Right.X, -n.Y + frame.Right.Y, -n.Z + frame.Right.Z),
            new SphericalPoint3(-n.X + frame.Up.X, -n.Y + frame.Up.Y, -n.Z + frame.Up.Z),
            new SphericalPoint3(n.X + frame.Up.X, n.Y + frame.Up.Y, n.Z + frame.Up.Z)
        };
        var result = TacticalHorizonClipper.Clip(frame, p);
        Assert.True(result.Count >= 3);
        Assert.All(result, v => Assert.True(TacticalSurfaceProjection.Project(frame, v).Forward >= -1e-12));
        Assert.True(result.Count(v => Math.Abs(TacticalSurfaceProjection.Project(frame, v).Forward) < 1e-12) >= 2);
        Assert.Equal(result.ToArray(), TacticalHorizonClipper.Clip(frame, p).ToArray());
    }

    [Fact]
    public void RejectsInvalidPolygonInputs()
    {
        var frame = Camera();
        Assert.Throws<ArgumentNullException>(() => TacticalHorizonClipper.Clip(frame, null!));
        Assert.Throws<ArgumentException>(() => TacticalHorizonClipper.Clip(frame, new[] { X, Y }));
    }
}
