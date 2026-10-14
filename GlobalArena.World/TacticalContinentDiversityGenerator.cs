using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalArena.World;

/// <summary>Isolated experimental terrain diversity candidate. Does not change canonical generation.</summary>
public static class TacticalContinentDiversityGenerator
{
    public static (int Macro, int Medium, int Micro) GetCounts(ulong seed)
    {
        ulong state = seed ^ 0xB5AD4ECEDA1CE2A9UL;
        var macro = (int)(Next(ref state) * 21.0); // 0..20
        // Zero macro does not suppress the other scales.
        return (macro, Math.Max(50, macro * 10), Math.Max(500, macro * 100));
    }

    public static IReadOnlyList<long> Generate(StrategicSphericalGeometry geometry, ulong seed,
        bool twoPasses = false)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var counts = GetCounts(seed);
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
                int phase = twoPasses && j >= (count + 1) / 2 ? 1 : 0;
                impulses.Add((planar * Math.Cos(longitude), planar * Math.Sin(longitude), z,
                    radius, Math.Cos(radius), amplitude, band, phase));
            }
        }
        // Phase one and phase two are evaluated independently before mixing.
        // A bounded intermediate transformation makes the two-pass path genuinely distinct
        // from a mathematically equivalent reordering of additions.
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
                double contribution = norm * impulse.Amplitude * kernel;
                if (impulse.Phase == 0) first += contribution; else second += contribution;
            }
            double combined = twoPasses ? Math.Tanh(first) + second : first;
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
