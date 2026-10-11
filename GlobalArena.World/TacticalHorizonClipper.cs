namespace GlobalArena.World;

/// <summary>
/// Clips convex spherical cell polygons against the camera-facing hemisphere.
/// Clipping affects presentation only; no canonical tile identity is changed.
/// Edges follow the minor great-circle arc between their endpoints.
/// </summary>
public static class TacticalHorizonClipper
{
    public static IReadOnlyList<SphericalPoint3> Clip(
        TacticalCameraFrame camera,
        IReadOnlyList<SphericalPoint3> vertices)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count < 3)
            throw new ArgumentException("A polygon requires at least three vertices.", nameof(vertices));
        if (vertices.Any(v => !v.IsValid))
            throw new ArgumentException("Polygon vertices must be unit vectors.", nameof(vertices));

        var output = new List<SphericalPoint3>();
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            var da = Dot(camera.Target, a);
            var db = Dot(camera.Target, b);
            var insideA = da >= 0d;
            var insideB = db >= 0d;
            if (insideA && insideB)
                AppendDistinct(output, b);
            else if (insideA && !insideB)
                AppendDistinct(output, Intersect(a, b, camera.Target, da));
            else if (!insideA && insideB)
            {
                AppendDistinct(output, Intersect(a, b, camera.Target, da));
                AppendDistinct(output, b);
            }
        }
        if (output.Count > 1 && Close(output[0], output[^1]))
            output.RemoveAt(output.Count - 1);
        return Array.AsReadOnly(output.ToArray());
    }

    private static SphericalPoint3 Intersect(
        SphericalPoint3 a, SphericalPoint3 b, SphericalPoint3 normal, double da)
    {
        if (da == 0d) return a;
        if (Dot(normal, b) == 0d) return b;
        // Antipodal endpoints do not specify a unique minor great-circle arc.
        if (Dot(a, b) <= -1d + 1e-12)
            throw new ArgumentException("Cannot clip an antipodal polygon edge.");
        double low = 0d, high = 1d;
        for (var i = 0; i < 64; i++)
        {
            var mid = (low + high) * 0.5d;
            var p = new SphericalPoint3(
                a.X * (1d - mid) + b.X * mid,
                a.Y * (1d - mid) + b.Y * mid,
                a.Z * (1d - mid) + b.Z * mid);
            var d = Dot(normal, p);
            if ((d >= 0d) == (da >= 0d)) low = mid;
            else high = mid;
        }
        var t = (low + high) * 0.5d;
        return new SphericalPoint3(
            a.X * (1d - t) + b.X * t,
            a.Y * (1d - t) + b.Y * t,
            a.Z * (1d - t) + b.Z * t);
    }

    private static void AppendDistinct(List<SphericalPoint3> result, SphericalPoint3 point)
    {
        if (result.Count == 0 || !Close(result[^1], point)) result.Add(point);
    }
    private static bool Close(SphericalPoint3 a, SphericalPoint3 b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z) < 1e-12;
    private static double Dot(SphericalPoint3 a, SphericalPoint3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
