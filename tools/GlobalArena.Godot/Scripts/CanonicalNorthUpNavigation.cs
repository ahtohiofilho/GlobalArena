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
}
