using Godot;
using System;

namespace GlobalArena.Godot;

// Presentation-only spherical navigation. The meridian remains meaningful
// as a camera bearing even when the geographic center is exactly a pole.
internal sealed class CanonicalNorthUpNavigation
{
    private Vector3 _meridian = Vector3.Right;
    private float _latitude;
    public Vector3 Center(Vector3 axis) =>
        MathF.Abs(_latitude) >= Mathf.Pi / 2f
            ? axis * MathF.Sign(_latitude)
            : (axis * MathF.Sin(_latitude) + _meridian * MathF.Cos(_latitude)).Normalized();
    public Vector3 North(Vector3 axis) =>
        (axis * MathF.Cos(_latitude) - _meridian * MathF.Sin(_latitude)).Normalized();

    public void SetCenter(Vector3 point, Vector3 axis)
    {
        point = point.Normalized();
        float height = Mathf.Clamp(point.Dot(axis), -1f, 1f);
        var horizontal = point - axis * height;
        _latitude = MathF.Atan2(height, horizontal.Length());
        if (horizontal.LengthSquared() > 1e-12f)
            _meridian = horizontal.Normalized();
        else
        {
            var tangent = _meridian - axis * _meridian.Dot(axis);
            if (tangent.LengthSquared() < 1e-8f)
            {
                var seed = MathF.Abs(axis.Dot(Vector3.Right)) < 0.9f
                    ? Vector3.Right : Vector3.Up;
                tangent = seed - axis * seed.Dot(axis);
            }
            _meridian = tangent.Normalized();
        }
    }

    public void Move(Vector3 axis, float longitudeDelta, float latitudeDelta)
    {
        _meridian = (new Quaternion(axis, longitudeDelta) * _meridian).Normalized();
        _latitude = Mathf.Clamp(_latitude + latitudeDelta, -Mathf.Pi / 2f, Mathf.Pi / 2f);
    }
    internal static void ExercisePolarContract()
    {
        var nav = new CanonicalNorthUpNavigation();
        var axis = Vector3.Back;
        nav.SetCenter(Vector3.Right, axis);
        foreach (float sign in new[] { 1f, -1f })
        {
            nav.SetCenter(Vector3.Right, axis);
            nav.Move(axis, 0f, sign * 4f);
            if (nav.Center(axis).DistanceTo(axis * sign) > 1e-6f)
                throw new InvalidOperationException("R185 pole clamp failed.");
            nav.Move(axis, 0.3f, sign);
            if (nav.Center(axis).DistanceTo(axis * sign) > 1e-6f ||
                !nav.North(axis).IsFinite())
                throw new InvalidOperationException("R185 polar meridian failed.");
            nav.Move(axis, 0f, -sign * 0.2f);
            var before = nav.Center(axis).Dot(axis);
            for (int j = 0; j < 100; j++) nav.Move(axis, 0.1f, 0f);
            if (MathF.Abs(nav.Center(axis).Dot(axis) - before) > 1e-6f ||
                MathF.Abs(before) >= 1f)
                throw new InvalidOperationException("R185 longitude or equator return failed.");
        }
        GD.Print("GA_NEXT_R185_POLAR_CONTRACT_PASS both_poles=true longitude_continuous=true");
    }
}
