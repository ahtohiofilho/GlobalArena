namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// Pure, fixed-point laboratory for F-084(1): NATURAL uninvested impedance
/// and capacity on one unit logical edge. No geometry, actors, prices, money,
/// routes, permanent freight, capacity consumption or investments.
/// </summary>
public static class NaturalEdgeEvaluator
{
    public const int Scale = 1_000_000;

    public static NaturalEdgeEvaluation Evaluate(
        NaturalLogicalEdge edge,
        NaturalEdgeCalibration calibration)
    {
        calibration.Validate();
        ValidateEdge(edge);

        var medium = edge.FromMedium == NodeMedium.Water && edge.ToMedium == NodeMedium.Water
            ? EdgeMedium.Water
            : edge.FromMedium == NodeMedium.Land && edge.ToMedium == NodeMedium.Land
                ? EdgeMedium.Land
                : EdgeMedium.Coast;

        var baseImpedance = medium switch
        {
            EdgeMedium.Land => calibration.LandImpedanceBaseRaw,
            EdgeMedium.Coast => calibration.CoastImpedanceBaseRaw,
            EdgeMedium.Water => calibration.WaterImpedanceBaseRaw,
            _ => throw new InvalidOperationException()
        };

        var baseCapacity = medium switch
        {
            EdgeMedium.Land => calibration.LandCapacityRateRaw,
            EdgeMedium.Coast => calibration.CoastCapacityRateRaw,
            EdgeMedium.Water => calibration.WaterCapacityRateRaw,
            _ => throw new InvalidOperationException()
        };

        // Bathymetry is not a steep ascent for the abstract sea service.
        // Coast has one category-local interface cost; do not also apply
        // an arbitrary elevation jump into submerged terrain.
        long roughness = medium == EdgeMedium.Water ? 0 : edge.LandRoughnessRaw;
        long grade = medium == EdgeMedium.Land
            ? Math.Min(Scale, Math.Abs((long)edge.FromElevationRaw - edge.ToElevationRaw))
            : 0L;

        long roughnessCost = calibration.RoughnessImpedanceWeightRaw * roughness / Scale;
        long gradeCost = calibration.GradeImpedanceWeightRaw * grade / Scale;

        long impedance = checked(baseImpedance + roughnessCost + gradeCost);

        // Capacity is a *rate*, not a wallet or mass held in storage.
        // Saturation, investment and cargo-specific loads are later F-084 gates.
        long denominator = checked(Scale
            + calibration.RoughnessCapacityPenaltyRaw * roughness / Scale
            + calibration.GradeCapacityPenaltyRaw * grade / Scale);

        // The base is never 0 and the parameter range cannot overflow.
        long capacity = checked(baseCapacity * Scale / denominator);
        if (capacity < 1)
            throw new InvalidOperationException("Natural edge capacity underflowed.");

        return new NaturalEdgeEvaluation(medium, impedance, capacity);
    }

    private static void ValidateEdge(NaturalLogicalEdge edge)
    {
        if (edge.FromMedium is not (NodeMedium.Land or NodeMedium.Water)
            || edge.ToMedium is not (NodeMedium.Land or NodeMedium.Water))
            throw new ArgumentOutOfRangeException(nameof(edge), "Unknown logical physical medium.");
        if (edge.FromElevationRaw < -Scale || edge.FromElevationRaw > Scale
            || edge.ToElevationRaw < -Scale || edge.ToElevationRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(edge), "Elevation must be within [-1e6,+1e6].");
        if (edge.LandRoughnessRaw < 0 || edge.LandRoughnessRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(edge), "Roughness must be within [0,1e6].");
        if (edge.FromMedium == NodeMedium.Water && edge.ToMedium == NodeMedium.Water
            && edge.LandRoughnessRaw != 0)
            throw new ArgumentException("Water-water edges cannot include land roughness.", nameof(edge));
    }
}
