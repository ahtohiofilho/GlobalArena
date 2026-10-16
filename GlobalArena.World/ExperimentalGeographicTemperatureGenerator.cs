using System;
using System.Collections.Generic;

namespace GlobalArena.World;

/// <summary>
/// Experimental 01C temperature field on the SAME spherical cells and elevations
/// as the 01B tactical candidate. Does not alter canonical climate generators.
/// North axis is positive Z in canonical geometry, independent of the camera.
/// </summary>
public static class ExperimentalGeographicTemperatureGenerator
{
    public const long Scale = 1_000_000L;

    public static IReadOnlyList<long> Generate(
        StrategicSphericalGeometry geometry,
        IReadOnlyList<long> elevations)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(elevations);
        if (geometry.Cells.Count != elevations.Count)
            throw new ArgumentException("The temperature field must share every terrain cell.", nameof(elevations));

        var result = new long[elevations.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var center = geometry.Cells[i].Center;
            var length = Math.Sqrt(center.X * center.X + center.Y * center.Y + center.Z * center.Z);
            if (!double.IsFinite(length) || length <= 0.0)
                throw new ArgumentException("Invalid spherical cell center.", nameof(geometry));
            var sineLatitude = Math.Clamp(center.Z / length, -1.0, 1.0);
            result[i] = ComputeRaw(sineLatitude, elevations[i]);
        }
        return Array.AsReadOnly(result);
    }

    /// <summary>
    /// A deterministic, season-neutral baseline. Latitude argument is sin(latitude)
    /// derived from the canonical Z axis; normalized elevation is from 01B.
    /// This is a prototype thermal index, not calibrated Celsius.
    /// </summary>
    public static long ComputeRaw(double sineLatitude, long elevationRaw)
    {
        if (!double.IsFinite(sineLatitude) || sineLatitude < -1.0 || sineLatitude > 1.0)
            throw new ArgumentOutOfRangeException(nameof(sineLatitude));
        if (elevationRaw < -Scale || elevationRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(elevationRaw));

        // Equator ~0.75, poles ~-0.75. Elevation cooling applies above sea level.
        var latitudinal = 0.75 - 1.50 * Math.Abs(sineLatitude);
        var altitudeCooling = 0.35 * Math.Max(0.0, elevationRaw / (double)Scale);
        var normalized = Math.Clamp(latitudinal - altitudeCooling, -1.0, 1.0);
        return checked((long)Math.Round(normalized * Scale, MidpointRounding.AwayFromZero));
    }
}