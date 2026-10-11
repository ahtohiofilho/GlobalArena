using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalCameraFrameTests
{
    private static double Dot(SphericalPoint3 a, SphericalPoint3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static void AssertFrame(TacticalCameraFrame frame)
    {
        Assert.True(frame.Target.IsValid);
        Assert.True(frame.Right.IsValid);
        Assert.True(frame.Up.IsValid);
        Assert.InRange(Math.Abs(Dot(frame.Target, frame.Right)), 0d, 1e-12);
        Assert.InRange(Math.Abs(Dot(frame.Target, frame.Up)), 0d, 1e-12);
        Assert.InRange(Math.Abs(Dot(frame.Right, frame.Up)), 0d, 1e-12);
        // Right x Up must point outward, along the normal.
        var x = frame.Right.Y * frame.Up.Z - frame.Right.Z * frame.Up.Y;
        var y = frame.Right.Z * frame.Up.X - frame.Right.X * frame.Up.Z;
        var z = frame.Right.X * frame.Up.Y - frame.Right.Y * frame.Up.X;
        Assert.InRange(x * frame.Target.X + y * frame.Target.Y + z * frame.Target.Z,
            1d - 1e-12, 1d + 1e-12);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public void EveryStrategicCellHasStableOrthonormalFrame(int m, int n)
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(17UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(m, n)));
        var geometry = world.StrategicSphericalGeometry;
        foreach (var cell in geometry.Cells)
        {
            var first = TacticalCameraFrame.Create(geometry, cell.CellId);
            var second = TacticalCameraFrame.Create(geometry, cell.CellId);
            Assert.Equal(cell.CellId, first.Focus);
            Assert.Equal(cell.Center, first.Target);
            Assert.Equal(first.Target, second.Target);
            Assert.Equal(first.Right, second.Right);
            Assert.Equal(first.Up, second.Up);
            AssertFrame(first);
        }
    }

    [Fact]
    public void RejectsInvalidFocusAndNullGeometry()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(17UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(1, 0)));
        var geometry = world.StrategicSphericalGeometry;
        Assert.Throws<ArgumentNullException>(() =>
            TacticalCameraFrame.Create(null!, new StrategicCellId(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalCameraFrame.Create(geometry, default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalCameraFrame.Create(geometry, new StrategicCellId(99999)));
    }
}
