namespace GlobalArena.Foundation.Agriculture;

/// <summary>
/// Pure input for an existing LOGICAL node. No biome, worldgen, economic,
/// geometric-distance, price, stock, or actor dependencies.
/// </summary>
public readonly record struct AgriculturalNode(
    int TemperatureRaw,
    int MoistureRaw,
    int SlopeRaw,
    bool IsLand);

/// <summary>
/// Opt-in six-crop suitability LAB. No world state mutations or allocation per
/// evaluated node. All arithmetic uses exact integers and truncation.
/// </summary>
public static class AgriculturalSuitability
{
    public const int Scale = 1_000_000;
    public const int LandFloor = 120_000; // lab candidate; NOT canonized.
    private const long TemperatureWeight = 450L;
    private const long MoistureWeight = 450L;
    private const long SlopeWeight = 100L;
    private const long TotalWeight = TemperatureWeight + MoistureWeight + SlopeWeight;

    public static int Evaluate(CropId crop, AgriculturalNode node)
    {
        // Invalid crop IDs must never silently produce a plausible score.
        var profile = CropCatalog.Get(crop);
        Validate(node);
        return EvaluateValidated(profile, node);
    }

    public static void EvaluateAll(AgriculturalNode node, Span<int> destination)
    {
        if (destination.Length != CropCatalog.Count)
            throw new ArgumentException("Destination must hold exactly six crop values.", nameof(destination));
        Validate(node);
        for (var index = 0; index < CropCatalog.Count; index++)
            destination[index] = EvaluateValidated(CropCatalog.All[index], node);
    }

    private static int EvaluateValidated(CropProfile crop, AgriculturalNode node)
    {
        if (!node.IsLand)
            return 0;

        // All six crops are possible on every ordinary land node.
        // Neither biome labels nor elevation itself alter this assessment.
        var thermal = Closeness(node.TemperatureRaw, crop.IdealTemperature, crop.TemperatureTolerance);
        var moisture = Closeness(node.MoistureRaw, crop.IdealMoisture, crop.MoistureTolerance);
        var slope = Scale - (long)node.SlopeRaw * crop.MaxSlopePenalty / Scale;

        // Weighted average avoids catastrophic multiplication of penalties.
        var composite = (thermal * TemperatureWeight
            + moisture * MoistureWeight + slope * SlopeWeight) / TotalWeight;
        return checked((int)(LandFloor + (Scale - LandFloor) * composite / Scale));
    }

    private static long Closeness(int value, int ideal, int tolerance)
    {
        var gap = Math.Abs((long)value - ideal);
        var loss = Math.Min(Scale, gap * Scale / tolerance);
        return Scale - loss;
    }

    private static void Validate(AgriculturalNode node)
    {
        if (node.TemperatureRaw < -Scale || node.TemperatureRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(node), "Temperature outside [-1e6,1e6].");
        if (node.MoistureRaw < 0 || node.MoistureRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(node), "Moisture outside [0,1e6].");
        if (node.SlopeRaw < 0 || node.SlopeRaw > Scale)
            throw new ArgumentOutOfRangeException(nameof(node), "Slope outside [0,1e6].");
    }
}

/// <summary>
/// A dimensionless local relief indicator based on uniform logical edges.
/// It is not a geodesic-distance or altitude penalty.
/// Caller supplies elevations of each directly connected logical neighbor.
/// </summary>
public static class AgriculturalLogicalSlope
{
    public static int Estimate(int elevationRaw, ReadOnlySpan<int> neighborElevations)
    {
        ValidateElevation(elevationRaw);
        if (neighborElevations.IsEmpty)
            throw new ArgumentException("A graph node needs at least one logical neighbor.", nameof(neighborElevations));

        long summedDifference = 0;
        foreach (var neighbor in neighborElevations)
        {
            ValidateElevation(neighbor);
            summedDifference += Math.Abs((long)elevationRaw - neighbor);
        }

        var averageDifference = summedDifference / neighborElevations.Length;
        return (int)Math.Min(AgriculturalSuitability.Scale, averageDifference);
    }

    private static void ValidateElevation(int elevation)
    {
        if (elevation < -AgriculturalSuitability.Scale || elevation > AgriculturalSuitability.Scale)
            throw new ArgumentOutOfRangeException(nameof(elevation), "Elevation outside [-1e6,1e6].");
    }
}
