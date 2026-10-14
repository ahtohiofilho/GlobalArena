using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalArena.World;

/// <summary>Independent-scale experimental candidate; no canonical world changes.</summary>
public static class TacticalIndependentScaleGenerator
{
    // Independent RNG domains: changing one band cannot change the others' counts.
    public static (int Macro, int Medium, int Micro) GetCounts(ulong seed)
    {
        ulong macroState = seed ^ 0xB5AD4ECEDA1CE2A9UL;
        ulong mediumState = seed ^ 0x5D953A4BFCD9937BUL;
        ulong microState = seed ^ 0xC2B2AE3D27D4EB4FUL;
        return ((int)(Next(ref macroState) * 11.0),
            20 + (int)(Next(ref mediumState) * 181.0),
            400 + (int)(Next(ref microState) * 3601.0));
    }

    public static (double Macro, double Medium, double Micro) GetIntensity(ulong seed)
    {
        ulong a = seed ^ 0xA24BAED4963EE407UL;
        ulong b = seed ^ 0x9FB21C651E98DF25UL;
        ulong c = seed ^ 0xD6E8FEB86659FD93UL;
        return (0.70 + 0.60 * Next(ref a),
                0.70 + 0.60 * Next(ref b),
                0.70 + 0.60 * Next(ref c));
    }

    public static IReadOnlyList<long> Generate(StrategicSphericalGeometry geometry, ulong seed)
    {
        return GenerateWeighted(geometry, seed, 1.0, 1.0, 1.0);
    }

    public static IReadOnlyList<long> GenerateWeighted(StrategicSphericalGeometry geometry, ulong seed, double macroWeight, double mediumWeight, double microWeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!double.IsFinite(macroWeight) || !double.IsFinite(mediumWeight) || !double.IsFinite(microWeight) || macroWeight <= 0 || mediumWeight <= 0 || microWeight <= 0) throw new ArgumentOutOfRangeException(nameof(macroWeight));
        var counts = GetCounts(seed);
        var intensities = GetIntensity(seed);
        ulong state = seed ^ 0xD1B54A32D192ED03UL;
        var impulses = new List<(double X, double Y, double Z, double Radius, double CosRadius, double Amplitude, int Band, int Phase)>();
        for (int band = 0; band < 3; band++)
        {
            int count = band == 0 ? counts.Macro : band == 1 ? counts.Medium : counts.Micro;
            for (int j = 0; j < count; j++)
            {
                double longitude = 2.0 * Math.PI * Next(ref state);
                double z = 2.0 * Next(ref state) - 1.0;
                double planar = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
                double radius = band == 0 ? 0.48 + 0.80 * Next(ref state)
                    : band == 1 ? 0.11 + 0.30 * Next(ref state) : 0.024 + 0.090 * Next(ref state);
                double weight = band == 0 ? 0.73 : band == 1 ? 0.24 : 0.105;
                double sign = Next(ref state) < (band == 0 ? 0.50 : 0.50) ? -1.0 : 1.0;
                var amplitude = weight * sign * (0.35 + 0.65 * Next(ref state));
                int phase = 0; // one-pass candidate only
                impulses.Add((planar * Math.Cos(longitude), planar * Math.Sin(longitude), z,
                    radius, Math.Cos(radius), amplitude, band, phase));
            }
        }
        // Single additive pass; band amplitudes and counts are independent.
        var output = new long[geometry.Cells.Count];
        for (int i = 0; i < output.Length; i++)
        {
            var p = geometry.Cells[i].Center;
            double first = -0.08, second = 0.0;
            foreach (var impulse in impulses)
            {
                double dot = Math.Clamp(p.X * impulse.X + p.Y * impulse.Y + p.Z * impulse.Z, -1.0, 1.0);
                if (dot <= impulse.CosRadius) continue;
                double ratio = Math.Acos(dot) / impulse.Radius;
                double t = Math.Clamp(1.0 - ratio * ratio, 0.0, 1.0);
                double kernel = t * t * (3.0 - 2.0 * t);
                int total = impulse.Band == 0 ? counts.Macro : impulse.Band == 1 ? counts.Medium : counts.Micro;
                double norm = Math.Sqrt((impulse.Band == 0 ? 10.0 : impulse.Band == 1 ? 100.0 : 1000.0) / Math.Max(1, total));
                double intensity = impulse.Band == 0 ? intensities.Macro : impulse.Band == 1 ? intensities.Medium : intensities.Micro;
                double weightMultiplier = impulse.Band == 0 ? macroWeight : impulse.Band == 1 ? mediumWeight : microWeight;
                double contribution = weightMultiplier * norm * intensity * impulse.Amplitude * kernel;
                if (impulse.Phase == 0) first += contribution; else second += contribution;
            }
            double combined = first;
            output[i] = (long)Math.Round(Math.Clamp(combined, -1.0, 1.0) * 1_000_000.0,
                MidpointRounding.AwayFromZero);
        }
        return Array.AsReadOnly(output);
    }

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
