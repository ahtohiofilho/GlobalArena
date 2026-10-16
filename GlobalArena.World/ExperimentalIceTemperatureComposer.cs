using System;
using System.Collections.Generic;

namespace GlobalArena.World;

/// <summary>
/// 01C experimental thermal composition and ice threshold.
/// Keeps the original continuous moisture and elevation unchanged.
/// Does not modify canonical temperature, geography, or biomes.
/// All coefficients are calibration candidates, not physical Celsius.
/// </summary>
public static class ExperimentalIceTemperatureComposer
{
    public const long Scale = 1_000_000L;
    public const long InitialIceThreshold = -300_000L;

    public static long ComputeRaw(double sineLatitude, long elevationRaw, long moistureRaw)
    {
        if (!double.IsFinite(sineLatitude) || sineLatitude < -1.0 || sineLatitude > 1.0)
            throw new ArgumentOutOfRangeException(nameof(sineLatitude));
        if (elevationRaw < -Scale || elevationRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(elevationRaw));
        if (moistureRaw < 0 || moistureRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(moistureRaw));

        // Base temperature already includes latitude and cooling above sea level.
        double baseIndex = ExperimentalGeographicTemperatureGenerator.ComputeRaw(
            sineLatitude, elevationRaw) / (double)Scale;
        // Deliberately simple and explicit: moisture raises temperature;
        // bathymetry lowers ocean temperature, as an experimental gameplay rule.
        double moistureWarming = 0.18 * moistureRaw / Scale;
        double depthCooling = 0.12 * Math.Max(0.0, -elevationRaw / (double)Scale);
        double composed = Math.Clamp(baseIndex + moistureWarming - depthCooling, -1.0, 1.0);
        return checked((long)Math.Round(composed * Scale, MidpointRounding.AwayFromZero));
    }

    public static bool IsIce(long thermalRaw, long threshold = InitialIceThreshold)
    {
        if (thermalRaw < -Scale || thermalRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(thermalRaw));
        if (threshold < -Scale || threshold > Scale)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return thermalRaw <= threshold;
    }

    public static IReadOnlyList<long> Generate(
        StrategicSphericalGeometry geometry,
        IReadOnlyList<long> elevationRaw,
        IReadOnlyList<long> moistureRaw)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(elevationRaw);
        ArgumentNullException.ThrowIfNull(moistureRaw);
        if (geometry.Cells.Count != elevationRaw.Count || geometry.Cells.Count != moistureRaw.Count)
            throw new ArgumentException("All climate inputs must share the same physical spherical geometry.");
        var output = new long[geometry.Cells.Count];
        for (int i = 0; i < output.Length; i++)
        {
            var c = geometry.Cells[i].Center;
            double norm = Math.Sqrt(c.X*c.X + c.Y*c.Y + c.Z*c.Z);
            if (norm <= 0.0 || !double.IsFinite(norm))
                throw new ArgumentException("Invalid spherical cell center.", nameof(geometry));
            output[i] = ComputeRaw(Math.Clamp(c.Z / norm, -1.0, 1.0), elevationRaw[i], moistureRaw[i]);
        }
        return Array.AsReadOnly(output);
    }
}