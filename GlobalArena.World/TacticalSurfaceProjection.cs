namespace GlobalArena.World;

/// <summary>
/// Deterministic orthographic coordinates in the tangent-plane basis of a
/// tactical camera frame. It does not decide renderer scale or screen pixels.
/// </summary>
public static class TacticalSurfaceProjection
{
    public static TacticalProjectedPoint Project(
        TacticalCameraFrame camera,
        SphericalPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (!point.IsValid)
            throw new ArgumentException("Expected a unit spherical point.", nameof(point));
        var x = Dot(camera.Right, point);
        var y = Dot(camera.Up, point);
        var forward = Dot(camera.Target, point);
        return new TacticalProjectedPoint(x, y, forward);
    }

    private static double Dot(SphericalPoint3 a, SphericalPoint3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}

/// <summary>
/// Renderer-neutral orthographic coordinates. Forward > 0 means that the
/// surface normal is on the camera-facing hemisphere; it is not terrain occlusion.
/// </summary>
public readonly record struct TacticalProjectedPoint(double X, double Y, double Forward)
{
    public bool IsFrontHemisphere => Forward > 0d;
}