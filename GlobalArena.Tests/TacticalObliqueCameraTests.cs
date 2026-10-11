using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalObliqueCameraTests
{
    private static TacticalCameraFrame Frame(int m, int n, int focus)
    {
        var geometry = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(17UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(m, n))).StrategicSphericalGeometry;
        return TacticalCameraFrame.Create(geometry, new StrategicCellId((ulong)focus));
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(2, 1, 7)]
    [InlineData(6, 0, 81)]
    public void CameraUsesCanonicalTargetAndOrthonormalAxes(int m, int n, int focus)
    {
        var source = Frame(m, n, focus);
        var camera = TacticalObliqueCamera.Create(source);
        Assert.Equal(45d, camera.TiltDegrees);
        Assert.InRange(Math.Abs(camera.Forward.Length - 1d), 0d, 1e-12d);
        Assert.InRange(Math.Abs(camera.Right.Length - 1d), 0d, 1e-12d);
        Assert.InRange(Math.Abs(camera.Up.Length - 1d), 0d, 1e-12d);
        Assert.InRange(Math.Abs(camera.Forward.Dot(camera.Right)), 0d, 1e-12d);
        Assert.InRange(Math.Abs(camera.Forward.Dot(camera.Up)), 0d, 1e-12d);
        Assert.InRange(Math.Abs(camera.Right.Dot(camera.Up)), 0d, 1e-12d);
        Assert.InRange(camera.Target.Minus(camera.Eye).Dot(camera.Forward), 0.399999999d, 0.400000001d);
        var center = camera.Project(source.Target, 60d, 16d / 9d);
        Assert.InRange(Math.Abs(center.X), 0d, 1e-12d);
        Assert.InRange(Math.Abs(center.Y), 0d, 1e-12d);
    }

    [Fact]
    public void InclinationAndPerspectiveAreDeterministicAndAdjustable()
    {
        var frame = Frame(6, 0, 81);
        var first = TacticalObliqueCamera.Create(frame);
        var repeat = TacticalObliqueCamera.Create(frame);
        Assert.Equal(first.Eye, repeat.Eye);
        Assert.Equal(first.Forward, repeat.Forward);
        var vertical = TacticalObliqueCamera.Create(frame, 90d);
        Assert.NotEqual(first.Eye, vertical.Eye);
        var point = new SphericalPoint3(frame.Target.X + 0.01d, frame.Target.Y + 0.01d, frame.Target.Z);
        var wide = first.Project(point, 70d, 16d / 9d);
        var narrow = first.Project(point, 40d, 16d / 9d);
        Assert.True(Math.Abs(narrow.X) >= Math.Abs(wide.X));
        Assert.True(Math.Abs(narrow.Y) >= Math.Abs(wide.Y));
    }

    [Fact]
    public void RejectsInvalidParameters()
    {
        var frame = Frame(1, 0, 1);
        Assert.Throws<ArgumentNullException>(() => TacticalObliqueCamera.Create(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalObliqueCamera.Create(frame, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalObliqueCamera.Create(frame, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalObliqueCamera.Create(frame, distance: -1d));
        var camera = TacticalObliqueCamera.Create(frame);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Project(frame.Target, 0d, 1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Project(frame.Target, 60d, 0d));
    }
}
