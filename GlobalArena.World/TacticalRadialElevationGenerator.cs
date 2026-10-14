namespace GlobalArena.World;

/// <summary>
/// Experimental tactical-first elevation surface. Samples the same seeded spherical
/// radial basis at every fine-grid center; neither sampling order nor coarse ownership
/// affects the result. Does not replace the accepted WorldGenerationVersion.Initial.
/// </summary>
public static class TacticalRadialElevationGenerator
{
    public const long Minimum = -1_000_000L;
    public const long Maximum = 1_000_000L;

    public static IReadOnlyList<long> Generate(
        StrategicSphericalGeometry fineGeometry,
        ulong seed,
        int deformationCount = 36)
    {
        ArgumentNullException.ThrowIfNull(fineGeometry);
        if (deformationCount is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(deformationCount));

        var centers = new (SphericalPoint3 Center, double Amplitude, double Radius)[deformationCount];
        var state = seed ^ 0xD1B54A32D192ED03UL;
        for (var i = 0; i < deformationCount; i++)
        {
            var longitude = 2.0 * Math.PI * NextUnit(ref state);
            var z = 2.0 * NextUnit(ref state) - 1.0;
            var radius = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
            var point = new SphericalPoint3(radius * Math.Cos(longitude),
                radius * Math.Sin(longitude), z);
            // Alternate broad uplift / subsidence with varying width.
            var amplitude = (i % 4 == 0 ? -1.0 : 1.0) * (0.28 + 0.72 * NextUnit(ref state));
            var influenceRadius = 0.18 + 0.72 * NextUnit(ref state);
            centers[i] = (point, amplitude, influenceRadius);
        }

        var values = new long[fineGeometry.Cells.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var point = fineGeometry.Cells[i].Center;
            double total = -0.18;
            foreach (var center in centers)
            {
                var dot = Math.Clamp(point.X * center.Center.X +
                    point.Y * center.Center.Y + point.Z * center.Center.Z, -1.0, 1.0);
                var distance = Math.Acos(dot);
                var ratio = distance / center.Radius;
                if (ratio >= 1.0) continue;
                var fade = 1.0 - ratio * ratio;
                total += center.Amplitude * fade * fade;
            }
            // Preserve the former 36-center baseline; prevent center-count changes
            // from unintentionally exploding the total field amplitude.
            if (deformationCount > 36)
                total = -0.18 + (total + 0.18) / Math.Sqrt(deformationCount / 36.0);
            values[i] = (long)Math.Round(Math.Clamp(total, -1.0, 1.0) *
                StrategicScalarField.Denominator, MidpointRounding.AwayFromZero);
        }
        return Array.AsReadOnly(values);
    }

    // Seed-specific complexity: 150%-300% of the original 36 centers (54..108).
    // Isolated preview API. The original Generate default remains unchanged.
    public static int GetVariedDeformationCount(ulong seed)
    {
        unchecked
        {
            ulong x = seed ^ 0xA0761D6478BD642FUL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return 54 + (int)(x % 55UL);
        }
    }

    public static IReadOnlyList<long> GenerateVaried(StrategicSphericalGeometry geometry, ulong seed)
    {
        return GenerateMultiscale(geometry, seed, GetVariedDetailStrength(seed));
    }

    // Spatial-complexity parameter is chosen once from the seed, independent of sampling order.
    // 0=gentle landscape, 1=strong high-frequency relief (same elevation domain).
    public static double GetVariedDetailStrength(ulong seed)
    {
        unchecked
        {
            var x = seed ^ 0xE7037ED1A0B428DBUL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return 0.25 + ((x >> 11) * (1.0 / 9007199254740992.0)) * 1.35;
        }
    }

    // Global spherical field: both strategic overview and tactical zoom must sample
    // this same result. The high-detail hierarchy has many more short-range impulses.
    public static (int Continental, int Regional, int Local) GetHierarchicalImpulseCounts(ulong seed)
    {
        var state = seed ^ 0x14C3A1B2D579EC65UL;
        // One global, latitude-independent draw; no coastline preference.
        int continental = 5 + (int)(NextUnit(ref state) * 16.0); // 5..20
        int regional = checked(continental * 10);
        int local = checked(continental * 100);
        return (continental, regional, local);
    }

    public static IReadOnlyList<long> GenerateMultiscale(
        StrategicSphericalGeometry geometry, ulong seed, double detailStrength)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!double.IsFinite(detailStrength) || detailStrength < 0.0 || detailStrength > 2.0)
            throw new ArgumentOutOfRangeException(nameof(detailStrength));
        var counts = GetHierarchicalImpulseCounts(seed);
        var count = counts.Continental + counts.Regional + counts.Local;
        var state = seed ^ 0xD1B54A32D192ED03UL;
        var centers = new (double X, double Y, double Z, double Amplitude, double Radius, double CosRadius)[count];
        for (var index = 0; index < count; index++)
        {
            // Uniform sphere samples, independent of fine-grid topology and iteration order.
            var longitude = 2.0 * Math.PI * NextUnit(ref state);
            var z = 2.0 * NextUnit(ref state) - 1.0;
            var planar = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
            var band = index < counts.Continental ? 0
                : index < counts.Continental + counts.Regional ? 1 : 2;
            var radius = band == 0 ? 0.48 + 0.80 * NextUnit(ref state)
                : band == 1 ? 0.11 + 0.30 * NextUnit(ref state)
                : 0.024 + 0.090 * NextUnit(ref state);
            // Medium and small impulses reshape the same global field; they do not
            // create independent tactical terrain. Gentle seeds have weaker detail.
            var weight = band == 0 ? 0.73
                : band == 1 ? 0.24 * detailStrength : 0.105 * detailStrength;
            var sign = NextUnit(ref state) < (band == 0 ? 0.44 : 0.50) ? -1.0 : 1.0;
            var amplitude = sign * weight * (0.35 + 0.65 * NextUnit(ref state));
            centers[index] = (planar * Math.Cos(longitude), planar * Math.Sin(longitude),
                z, amplitude, radius, Math.Cos(radius));
        }
        var values = new long[geometry.Cells.Count];
        // Separate band normalizations preserve macro structure with many more small
        // impulses. Avoid clamping per impulse; clamp only the complete field.
        var continentalNorm = Math.Sqrt(10.0 / counts.Continental);
        var regionalNorm = Math.Sqrt(100.0 / counts.Regional);
        var localNorm = Math.Sqrt(1000.0 / counts.Local);
        for (var i = 0; i < values.Length; i++)
        {
            var point = geometry.Cells[i].Center;
            double elevation = -0.08;
            for (var j = 0; j < centers.Length; j++)
            {
                var impulse = centers[j];
                var dot = Math.Clamp(point.X * impulse.X + point.Y * impulse.Y +
                    point.Z * impulse.Z, -1.0, 1.0);
                // Most short-range impulses are rejected before expensive acos.
                if (dot <= impulse.CosRadius) continue;
                var ratio = Math.Acos(dot) / impulse.Radius;
                // Smoothstep-style compact kernel: zero value AND slope at boundary
                // and center, avoiding abrupt crater rims.
                var t = Math.Clamp(1.0 - ratio * ratio, 0.0, 1.0);
                var fade = t * t * (3.0 - 2.0 * t);
                var norm = j < counts.Continental ? continentalNorm
                    : j < counts.Continental + counts.Regional ? regionalNorm : localNorm;
                elevation += norm * impulse.Amplitude * fade;
            }
            values[i] = (long)Math.Round(Math.Clamp(elevation, -1.0, 1.0) *
                StrategicScalarField.Denominator, MidpointRounding.AwayFromZero);
        }
        return Array.AsReadOnly(values);
    }

    private static double NextUnit(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var x = state;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
