namespace GlobalArena.World;

public readonly record struct SphericalPoint3
{
    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public SphericalPoint3(
        double x,
        double y,
        double z)
    {
        if (!double.IsFinite(x)
            || !double.IsFinite(y)
            || !double.IsFinite(z))
        {
            throw new ArgumentException(
                "Spherical point components must be finite.");
        }

        var scale =
            Math.Max(
                Math.Abs(x),
                Math.Max(
                    Math.Abs(y),
                    Math.Abs(z)));

        if (scale == 0d)
        {
            throw new ArgumentException(
                "Spherical point cannot be the zero vector.");
        }

        var scaledX =
            x / scale;

        var scaledY =
            y / scale;

        var scaledZ =
            z / scale;

        var scaledLength =
            Math.Sqrt(
                checked(
                    scaledX * scaledX
                    + scaledY * scaledY
                    + scaledZ * scaledZ));

        if (!double.IsFinite(scaledLength)
            || scaledLength == 0d)
        {
            throw new ArgumentException(
                "Spherical point cannot be normalized.");
        }

        X =
            scaledX / scaledLength;

        Y =
            scaledY / scaledLength;

        Z =
            scaledZ / scaledLength;
    }

    public double LengthSquared =>
        X * X
        + Y * Y
        + Z * Z;

    public bool IsValid =>
        double.IsFinite(X)
        && double.IsFinite(Y)
        && double.IsFinite(Z)
        && Math.Abs(
            LengthSquared - 1d)
            <= 1e-12;
}
