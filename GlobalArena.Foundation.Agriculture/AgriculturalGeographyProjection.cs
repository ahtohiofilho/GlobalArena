namespace GlobalArena.Foundation.Agriculture;

/// <summary>
/// Deterministic six-column cache tied to one immutable external logical
/// geography snapshot. No mutation of geography, crops, or economy.
/// </summary>
public sealed class AgriculturalGeographyProjection
{
    private readonly int[] _scores;
    private readonly int[] _slopes;

    public AgriculturalLogicalGeography Geography { get; }
    public int NodeCount => Geography.NodeCount;

    private AgriculturalGeographyProjection(
        AgriculturalLogicalGeography geography, int[] scores, int[] slopes)
    {
        Geography = geography;
        _scores = scores;
        _slopes = slopes;
    }

    /// <summary>
    /// O(N*degree + N*6) time and O(N*6) fixed-point result memory.
    /// No spherical/geodesic measurements and no separate biome mask.
    /// </summary>
    public static AgriculturalGeographyProjection Build(AgriculturalLogicalGeography geography)
    {
        ArgumentNullException.ThrowIfNull(geography);
        var scores = new int[checked(geography.NodeCount * CropCatalog.Count)];
        var slopes = new int[geography.NodeCount];

        for (int i = 0; i < geography.NodeCount; i++)
        {
            var node = geography.GetNode(i);
            ReadOnlySpan<int> adjacent = geography.GetNeighbors(i);
            long differenceSum = 0;
            foreach (int neighbor in adjacent)
                differenceSum += Math.Abs((long)node.ElevationRaw - geography.GetNode(neighbor).ElevationRaw);
            var slope = checked((int)Math.Min(
                AgriculturalSuitability.Scale, differenceSum / adjacent.Length));

            slopes[i] = slope;
            AgriculturalSuitability.EvaluateAll(
                new AgriculturalNode(node.TemperatureRaw, node.MoistureRaw, slope, node.IsLand),
                scores.AsSpan(i * CropCatalog.Count, CropCatalog.Count));
        }

        return new AgriculturalGeographyProjection(geography, scores, slopes);
    }

    public int GetSlope(int nodeIndex)
    {
        CheckIndex(nodeIndex);
        return _slopes[nodeIndex];
    }

    public int GetSuitability(int nodeIndex, CropId crop)
    {
        CheckIndex(nodeIndex);
        return _scores[nodeIndex * CropCatalog.Count + CropCatalog.IndexOf(crop)];
    }

    /// <summary>
    /// Water has no leading terrestrial crop. Ties resolve by stable catalog
    /// position, never discovery/rendering order.
    /// </summary>
    public CropId? GetLeadingCrop(int nodeIndex)
    {
        CheckIndex(nodeIndex);
        if (!Geography.GetNode(nodeIndex).IsLand)
            return null;

        int offset = nodeIndex * CropCatalog.Count;
        int winner = 0;
        int best = _scores[offset];
        for (int j = 1; j < CropCatalog.Count; j++)
            if (_scores[offset + j] > best)
            {
                best = _scores[offset + j];
                winner = j;
            }
        return CropCatalog.All[winner].Id;
    }

    public void CopyScores(int nodeIndex, Span<int> destination)
    {
        CheckIndex(nodeIndex);
        if (destination.Length != CropCatalog.Count)
            throw new ArgumentException("Destination must hold six scores.", nameof(destination));
        _scores.AsSpan(nodeIndex * CropCatalog.Count, CropCatalog.Count).CopyTo(destination);
    }

    private void CheckIndex(int nodeIndex)
    {
        if ((uint)nodeIndex >= (uint)NodeCount)
            throw new ArgumentOutOfRangeException(nameof(nodeIndex));
    }
}
