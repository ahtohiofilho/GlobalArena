namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// Pure F-084B experiment: worker edge traversal duration, NOT freight cost.
/// One logical edge remains exactly one hop, regardless of visual geometry.
/// Travel uses the F-013 universal action-time budget; transport services,
/// vehicle tech, route choice, and concurrency are intentionally absent.
/// </summary>
public static class WorkerEdgeTravel
{
    public const long Scale = 1_000_000;

    /// <summary>
    /// WorkerEfficiencyRaw is positive action efficiency, 1e6 = baseline.
    /// The caller passes a stable physical edge snapshot at departure.
    /// Ceiling quantization ensures that no travel has zero elapsed time.
    /// </summary>
    public static long RequiredTimeRaw(
        NaturalLogicalEdge edge,
        long workerEfficiencyRaw,
        WorkerTravelCalibration calibration)
    {
        calibration.Validate();
        if (workerEfficiencyRaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(workerEfficiencyRaw));

        // Validate the shared physical index envelope WITHOUT consulting,
        // inheriting, or multiplying NaturalEdgeEvaluator's commercial C or K.
        if (edge.FromMedium is not (NodeMedium.Land or NodeMedium.Water) ||
            edge.ToMedium is not (NodeMedium.Land or NodeMedium.Water))
            throw new ArgumentOutOfRangeException(nameof(edge), "Unknown medium.");
        if (edge.FromElevationRaw is < -1_000_000 or > 1_000_000 ||
            edge.ToElevationRaw is < -1_000_000 or > 1_000_000 ||
            edge.LandRoughnessRaw is < 0 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(edge), "Invalid physical field.");
        if (edge.FromMedium == NodeMedium.Water &&
            edge.ToMedium == NodeMedium.Water && edge.LandRoughnessRaw != 0)
            throw new ArgumentException("Water-water edges cannot contain land roughness.", nameof(edge));

        var medium = edge.FromMedium == NodeMedium.Water && edge.ToMedium == NodeMedium.Water
            ? EdgeMedium.Water
            : edge.FromMedium == NodeMedium.Land && edge.ToMedium == NodeMedium.Land
                ? EdgeMedium.Land
                : EdgeMedium.Coast;

        var baseEffort = medium switch
        {
            EdgeMedium.Land => calibration.NeutralLandEffortRaw,
            EdgeMedium.Water => calibration.WaterEffortRaw,
            EdgeMedium.Coast => calibration.CoastEffortRaw,
            _ => throw new InvalidOperationException()
        };

        long roughness = medium == EdgeMedium.Water ? 0 : edge.LandRoughnessRaw;
        long grade = medium == EdgeMedium.Land
            ? Math.Min(Scale, Math.Abs((long)edge.FromElevationRaw - edge.ToElevationRaw))
            : 0;

        var effort = checked(baseEffort
            + calibration.RoughnessEffortWeightRaw * roughness / Scale
            + calibration.GradeEffortWeightRaw * grade / Scale);

        // Use Int128 for exact ceiling division even at very large worker
        // efficiency. No floating point or geodesic distance in the kernel.
        var numerator = (Int128)effort * Scale;
        var duration = (numerator + workerEfficiencyRaw - 1) / workerEfficiencyRaw;
        return checked((long)(duration < 1 ? 1 : duration));
    }
}
