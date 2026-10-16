using System;
using System.Collections.Generic;

namespace GlobalArena.World;

/// <summary>
/// 01C experimental: pure stochastic moisture patches on the physical sphere.
/// This revision increases spatial frequency and reduces average patch scale,
/// while keeping a three-scale procedural hierarchy with medium and micro
/// contributions stronger than macro.
/// </summary>
public static class ExperimentalRandomMoisturePatchGenerator
{
    public const long Scale = 1_000_000L;
    public enum MoistureBand { Arid, SemiArid, Fertile, Humid }

    private readonly record struct Patch(
        double X, double Y, double Z,
        double Ux, double Uy, double Uz,
        double Vx, double Vy, double Vz,
        double Radius, double Strength, double Phase,
        double WarpA, double WarpB);

    public static IReadOnlyList<long> Generate(StrategicSphericalGeometry geometry, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var patches = BuildPatches(seed);
        var values = new long[geometry.Cells.Count];
        for (int i = 0; i < values.Length; i++)
        {
            var c = geometry.Cells[i].Center;
            var norm = Math.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z);
            if (!double.IsFinite(norm) || norm <= 0.0)
                throw new ArgumentException("Invalid spherical center.", nameof(geometry));
            values[i] = SampleNormalized(c.X / norm, c.Y / norm, c.Z / norm, patches);
        }
        return Array.AsReadOnly(values);
    }

    public static MoistureBand Classify(long raw)
    {
        if (raw < 0 || raw > Scale) throw new ArgumentOutOfRangeException(nameof(raw));
        if (raw < 250_000) return MoistureBand.Arid;
        if (raw < 500_000) return MoistureBand.SemiArid;
        if (raw < 750_000) return MoistureBand.Fertile;
        return MoistureBand.Humid;
    }

    public static long Sample(ulong seed, double x, double y, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            throw new ArgumentOutOfRangeException(nameof(x));
        var norm = Math.Sqrt(x * x + y * y + z * z);
        if (norm <= 0.0 || !double.IsFinite(norm))
            throw new ArgumentOutOfRangeException(nameof(x));
        return SampleNormalized(x / norm, y / norm, z / norm, BuildPatches(seed));
    }

    private static long SampleNormalized(double x, double y, double z, Patch[] patches)
    {
        double field = 0.50;
        foreach (var p in patches)
        {
            var cosine = Math.Clamp(x * p.X + y * p.Y + z * p.Z, -1.0, 1.0);
            var angle = Math.Acos(cosine);
            var u = x * p.Ux + y * p.Uy + z * p.Uz;
            var v = x * p.Vx + y * p.Vy + z * p.Vz;
            var theta = Math.Atan2(v, u);
            var shape = 1.0
                + p.WarpA * Math.Sin(3.0 * theta + p.Phase)
                + p.WarpB * Math.Cos(5.0 * theta - p.Phase);
            var distance = angle / (p.Radius * shape);
            if (distance >= 1.0) continue;
            var t = Math.Clamp(1.0 - distance * distance, 0.0, 1.0);
            var kernel = t * t * (3.0 - 2.0 * t);
            field += p.Strength * kernel;
        }

        // Restore useful variation after distributing influence across finer bands.
        // Continuous before classification; symmetric and geography-independent.
        double amplified = 0.50 + 4.0 * (field - 0.50);
        return (long)Math.Round(
            Math.Clamp(amplified, 0.0, 1.0) * Scale,
            MidpointRounding.AwayFromZero);
    }

    private static Patch[] BuildPatches(ulong seed)
    {
        ulong state = seed ^ 0xE7DA5B4F2CB19A63UL;
        int macroCount = 8 + (int)(Next(ref state) * 9.0);      // 8..16
        int mediumCount = 90 + (int)(Next(ref state) * 121.0);  // 90..210
        int microCount = 320 + (int)(Next(ref state) * 381.0);  // 320..700
        var patches = new Patch[macroCount + mediumCount + microCount];
        int cursor = 0;

        cursor = FillScale(ref state, patches, cursor, macroCount,
            minRadius: 0.22, maxRadius: 0.44,
            minStrength: 0.035, maxStrength: 0.085,
            weight: 0.70,
            minWarpA: 0.04, maxWarpA: 0.09,
            minWarpB: 0.03, maxWarpB: 0.07);

        cursor = FillScale(ref state, patches, cursor, mediumCount,
            minRadius: 0.08, maxRadius: 0.18,
            minStrength: 0.020, maxStrength: 0.060,
            weight: 1.25,
            minWarpA: 0.05, maxWarpA: 0.14,
            minWarpB: 0.04, maxWarpB: 0.11);

        _ = FillScale(ref state, patches, cursor, microCount,
            minRadius: 0.025, maxRadius: 0.070,
            minStrength: 0.010, maxStrength: 0.028,
            weight: 1.35,
            minWarpA: 0.07, maxWarpA: 0.18,
            minWarpB: 0.05, maxWarpB: 0.14);

        return patches;
    }

    private static int FillScale(
        ref ulong state,
        Patch[] patches,
        int start,
        int count,
        double minRadius,
        double maxRadius,
        double minStrength,
        double maxStrength,
        double weight,
        double minWarpA,
        double maxWarpA,
        double minWarpB,
        double maxWarpB)
    {
        for (int i = 0; i < count; i++)
        {
            double azimuth = 2.0 * Math.PI * Next(ref state);
            double z = 2.0 * Next(ref state) - 1.0;
            double xy = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
            double x = xy * Math.Cos(azimuth);
            double y = xy * Math.Sin(azimuth);

            double ux = -Math.Sin(azimuth);
            double uy = Math.Cos(azimuth);
            double uz = 0.0;
            double vx = -z * Math.Cos(azimuth);
            double vy = -z * Math.Sin(azimuth);
            double vz = xy;

            double radius = Lerp(minRadius, maxRadius, Next(ref state)) * 2.00;
            double strength = Lerp(minStrength, maxStrength, Next(ref state)) * weight;
            if (Next(ref state) < 0.5) strength = -strength;
            double phase = 2.0 * Math.PI * Next(ref state);
            double warpA = Lerp(minWarpA, maxWarpA, Next(ref state));
            double warpB = Lerp(minWarpB, maxWarpB, Next(ref state));

            patches[start + i] = new Patch(
                x, y, z,
                ux, uy, uz,
                vx, vy, vz,
                radius, strength, phase,
                warpA, warpB);
        }

        return start + count;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Next(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong x = state;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}