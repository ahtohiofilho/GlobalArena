namespace GlobalArena.World;

/// <summary>Unnormalized Cartesian camera-space vector; never a physical tile identity.</summary>
public readonly record struct TacticalCameraVector(double X, double Y, double Z)
{
    public double Dot(TacticalCameraVector other) => X * other.X + Y * other.Y + Z * other.Z;
    public TacticalCameraVector Minus(TacticalCameraVector other) => new(X - other.X, Y - other.Y, Z - other.Z);
    public TacticalCameraVector Plus(TacticalCameraVector other) => new(X + other.X, Y + other.Y, Z + other.Z);
    public TacticalCameraVector Times(double scalar) => new(X * scalar, Y * scalar, Z * scalar);
    public double Length => Math.Sqrt(Dot(this));
    public TacticalCameraVector Normalized()
    {
        var length = Length;
        if (!double.IsFinite(length) || length <= 0d) throw new ArgumentException("Invalid camera direction.");
        return Times(1d / length);
    }
    public static TacticalCameraVector From(SphericalPoint3 point) => new(point.X, point.Y, point.Z);
    public static TacticalCameraVector Cross(TacticalCameraVector a, TacticalCameraVector b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}

/// <summary>
/// Real spherical, oblique camera basis. Tilt is measured above the local tangent
/// plane: 90 degrees looks straight down; 45 degrees is the initial tactical view.
/// This type neither flattens the planet nor creates simulation or GPU geometry.
/// </summary>
public sealed class TacticalObliqueCamera
{
    public TacticalCameraVector Eye { get; }
    public TacticalCameraVector Target { get; }
    public TacticalCameraVector Forward { get; }
    public TacticalCameraVector Right { get; }
    public TacticalCameraVector Up { get; }
    public double TiltDegrees { get; }
    public double Distance { get; }

    private TacticalObliqueCamera(TacticalCameraVector eye, TacticalCameraVector target,
        TacticalCameraVector forward, TacticalCameraVector right, TacticalCameraVector up,
        double tiltDegrees, double distance)
    {
        Eye = eye; Target = target; Forward = forward; Right = right; Up = up;
        TiltDegrees = tiltDegrees; Distance = distance;
    }

    public static TacticalObliqueCamera Create(TacticalCameraFrame frame,
        double tiltDegrees = 45d, double distance = 0.4d)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!double.IsFinite(tiltDegrees) || tiltDegrees < 15d || tiltDegrees > 90d)
            throw new ArgumentOutOfRangeException(nameof(tiltDegrees));
        if (!double.IsFinite(distance) || distance <= 0d || distance > 10d)
            throw new ArgumentOutOfRangeException(nameof(distance));
        var radians = tiltDegrees * Math.PI / 180d;
        var target = TacticalCameraVector.From(frame.Target);
        var normal = target;
        var tangentUp = TacticalCameraVector.From(frame.Up);
        var right = TacticalCameraVector.From(frame.Right);
        var eye = target.Plus(normal.Times(distance * Math.Sin(radians)))
            .Minus(tangentUp.Times(distance * Math.Cos(radians)));
        var forward = target.Minus(eye).Normalized();
        var cameraUp = TacticalCameraVector.Cross(right, forward).Normalized();
        return new TacticalObliqueCamera(eye, target, forward, right, cameraUp,
            tiltDegrees, distance);
    }

    /// <summary>Perspective coordinates (Y up) of an unmodified unit-sphere point.</summary>
    public (double X, double Y, double Depth) Project(
        SphericalPoint3 worldPoint, double verticalFovDegrees, double aspectRatio)
    {
        if (!worldPoint.IsValid) throw new ArgumentException("A canonical unit point is required.", nameof(worldPoint));
        if (!double.IsFinite(verticalFovDegrees) || verticalFovDegrees <= 1d || verticalFovDegrees >= 150d)
            throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0d)
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        var delta = TacticalCameraVector.From(worldPoint).Minus(Eye);
        var depth = delta.Dot(Forward);
        if (depth <= 1e-10d) throw new NotSupportedException("Point is behind the oblique camera near plane.");
        var tangent = Math.Tan(verticalFovDegrees * Math.PI / 360d);
        return (delta.Dot(Right) / (depth * tangent * aspectRatio),
            delta.Dot(Up) / (depth * tangent), depth);
    }
}
