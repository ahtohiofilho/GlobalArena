namespace GlobalArena.World;

/// <summary>
/// Renderer-independent orthonormal surface frame for a tactical camera.
/// The camera target is the canonical strategic cell center. Camera distance,
/// projection and GPU resources are intentionally left to the renderer.
/// </summary>
public sealed class TacticalCameraFrame
{
    public StrategicCellId Focus { get; }
    public SphericalPoint3 Target { get; }
    public SphericalPoint3 Up { get; }
    public SphericalPoint3 Right { get; }

    private TacticalCameraFrame(
        StrategicCellId focus,
        SphericalPoint3 target,
        SphericalPoint3 up,
        SphericalPoint3 right)
    {
        Focus = focus;
        Target = target;
        Up = up;
        Right = right;
    }

    public static TacticalCameraFrame Create(
        StrategicSphericalGeometry geometry,
        StrategicCellId focus)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var target = geometry.GetCell(focus).Center;

        // Choose a deterministic reference axis that is not almost parallel
        // to the surface normal, including at the geographic poles.
        var referenceX = Math.Abs(target.Z) > 0.9d ? 1d : 0d;
        var referenceY = 0d;
        var referenceZ = Math.Abs(target.Z) > 0.9d ? 0d : 1d;

        // East/right = reference x normal; north/up = normal x east.
        var right = new SphericalPoint3(
            referenceY * target.Z - referenceZ * target.Y,
            referenceZ * target.X - referenceX * target.Z,
            referenceX * target.Y - referenceY * target.X);
        var up = new SphericalPoint3(
            target.Y * right.Z - target.Z * right.Y,
            target.Z * right.X - target.X * right.Z,
            target.X * right.Y - target.Y * right.X);

        return new TacticalCameraFrame(focus, target, up, right);
    }
}
