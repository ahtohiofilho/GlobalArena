namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// The two physical kinds present at endpoints of a logical unit edge.
/// They are not transport modes or biome labels.
/// </summary>
public enum NodeMedium : byte
{
    Land = 1,
    Water = 2
}

public enum EdgeMedium : byte
{
    Land = 1,
    Water = 2,
    Coast = 3
}

/// <summary>
/// Edge-local inputs supplied by the new authoritative logical world.
/// Elevation is used as a signed index, never a geodesic length in metres.
/// LandRoughnessRaw is the physical land-side friction/roughness signal,
/// supplied by the future worldgen rather than inferred from a biome.
/// </summary>
public readonly record struct NaturalLogicalEdge(
    NodeMedium FromMedium,
    NodeMedium ToMedium,
    int FromElevationRaw,
    int ToElevationRaw,
    int LandRoughnessRaw);

/// <summary>
/// Results are dimensionless impedance and a natural throughput rate (both
/// fixed-point 1e6); they are not turn duration, money or tolls.
/// </summary>
public readonly record struct NaturalEdgeEvaluation(
    EdgeMedium Medium,
    long ImpedanceRaw,
    long NaturalCapacityRateRaw)
{
    public const int LogicalDistance = 1;
}
