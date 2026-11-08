using GlobalArena.Foundation.Agriculture;

namespace GlobalArena.Foundation.Agriculture.Tests;

public sealed class AgriculturalGeographyProjectionTests
{
    [Fact]
    public void ProjectionUsesActualLogicalNeighborsAndStableNodeIdentifiers()
    {
        var geography = CreateThreeNodeGeography();
        var projection = AgriculturalGeographyProjection.Build(geography);

        Assert.Equal(3, projection.NodeCount);
        Assert.Equal(100, geography.GetNode(0).StableNodeId);
        Assert.Equal(200, geography.GetNode(1).StableNodeId);
        Assert.Equal(300, geography.GetNode(2).StableNodeId);
        Assert.Equal(15_000, projection.GetSlope(0));
        Assert.Equal(20_000, projection.GetSlope(1));
        Assert.Equal(25_000, projection.GetSlope(2));

        Span<int> actual = stackalloc int[CropCatalog.Count];
        projection.CopyScores(0, actual);
        foreach (var crop in CropCatalog.All)
        {
            var expected = AgriculturalSuitability.Evaluate(
                crop.Id, new AgriculturalNode(650_000, 880_000, 15_000, true));
            Assert.Equal(expected, projection.GetSuitability(0, crop.Id));
            Assert.Equal(expected, actual[CropCatalog.IndexOf(crop.Id)]);
        }

        Assert.Equal(CropId.Rice, projection.GetLeadingCrop(0));
        Assert.Equal(CropId.Barley, projection.GetLeadingCrop(1));
        Assert.Null(projection.GetLeadingCrop(2)); // water has no terrestrial leader
        foreach (var crop in CropCatalog.All)
            Assert.Equal(0, projection.GetSuitability(2, crop.Id));
    }

    [Fact]
    public void InputMutationAfterSnapshotCannotChangeProjectedGeography()
    {
        var nodes = new[]
        {
            new AgriculturalGeographicNode(10, 0, 650_000, 880_000, true),
            new AgriculturalGeographicNode(20, 0, -650_000, 250_000, true),
            new AgriculturalGeographicNode(30, 0, 350_000, 530_000, true)
        };
        int[] offsets = { 0, 2, 4, 6 };
        int[] adjacency = { 1, 2, 0, 2, 0, 1 };
        var geography = new AgriculturalLogicalGeography(nodes, offsets, adjacency);
        var first = AgriculturalGeographyProjection.Build(geography);

        nodes[0] = nodes[0] with { TemperatureRaw = -1_000_000, MoistureRaw = 0 };
        offsets[1] = 0;
        adjacency[0] = 2;

        var second = AgriculturalGeographyProjection.Build(geography);
        Assert.Equal(CropId.Rice, second.GetLeadingCrop(0));
        for (int i = 0; i < geography.NodeCount; i++)
            foreach (var crop in CropCatalog.All)
                Assert.Equal(first.GetSuitability(i, crop.Id), second.GetSuitability(i, crop.Id));
    }

    [Fact]
    public void SlotPermutationDoesNotChangeStableNodePhysicalScores()
    {
        var a = CreateThreeNodeGeography();
        var b = new AgriculturalLogicalGeography(
            new[] { a.GetNode(2), a.GetNode(0), a.GetNode(1) },
            new[] { 0, 2, 4, 6 },
            new[] { 1, 2, 0, 2, 0, 1 });
        var first = AgriculturalGeographyProjection.Build(a);
        var second = AgriculturalGeographyProjection.Build(b);
        var permuted = new[] { 2, 0, 1 };
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(a.GetNode(permuted[i]).StableNodeId, b.GetNode(i).StableNodeId);
            Assert.Equal(first.GetSlope(permuted[i]), second.GetSlope(i));
            foreach (var crop in CropCatalog.All)
                Assert.Equal(first.GetSuitability(permuted[i], crop.Id), second.GetSuitability(i, crop.Id));
        }
    }

    [Theory]
    [InlineData(12)]
    [InlineData(120)]
    [InlineData(1_200)]
    [InlineData(12_000)]
    public void SyntheticLogicalMapsExhibitAllSixSpecializationsAtDifferentDensities(int nodeCount)
    {
        var map = CreateSyntheticRing(nodeCount);
        var result = AgriculturalGeographyProjection.Build(map);
        var leaders = new HashSet<CropId>();
        long firstChecksum = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            Assert.Equal(0, result.GetSlope(i));
            var crop = CropCatalog.All[i % CropCatalog.Count].Id;
            Assert.Equal(crop, result.GetLeadingCrop(i));
            leaders.Add(result.GetLeadingCrop(i)!.Value);
            for (int j = 0; j < CropCatalog.Count; j++)
            {
                var score = result.GetSuitability(i, CropCatalog.All[j].Id);
                Assert.InRange(score, AgriculturalSuitability.LandFloor, 1_000_000);
                firstChecksum = checked(firstChecksum + score * (j + 1L));
            }
        }
        Assert.Equal(CropCatalog.Count, leaders.Count);
        var again = AgriculturalGeographyProjection.Build(map);
        long secondChecksum = 0;
        for (int i = 0; i < nodeCount; i++)
            for (int j = 0; j < CropCatalog.Count; j++)
                secondChecksum += again.GetSuitability(i, CropCatalog.All[j].Id) * (j + 1L);
        Assert.Equal(firstChecksum, secondChecksum);
    }

    [Fact]
    public void AbsoluteElevationShiftDoesNotAddAltitudePenalty()
    {
        var baseNodes = new[]
        {
            new AgriculturalGeographicNode(1, 10_000, 250_000, 500_000, true),
            new AgriculturalGeographicNode(2, 20_000, 250_000, 500_000, true),
            new AgriculturalGeographicNode(3, -10_000, 250_000, 500_000, true)
        };
        var shifted = baseNodes.Select(n => n with { ElevationRaw = n.ElevationRaw + 500_000 }).ToArray();
        int[] offsets = { 0, 2, 4, 6 };
        int[] adjacent = { 1, 2, 0, 2, 0, 1 };
        var a = AgriculturalGeographyProjection.Build(
            new AgriculturalLogicalGeography(baseNodes, offsets, adjacent));
        var b = AgriculturalGeographyProjection.Build(
            new AgriculturalLogicalGeography(shifted, offsets, adjacent));
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(a.GetSlope(i), b.GetSlope(i));
            foreach (var crop in CropCatalog.All)
                Assert.Equal(a.GetSuitability(i, crop.Id), b.GetSuitability(i, crop.Id));
        }
    }

    [Fact]
    public void StructuralErrorsAndInvalidQueriesAreRejected()
    {
        var nodes = CreateThreeNodeGeography();
        Assert.Throws<ArgumentException>(() => new AgriculturalLogicalGeography(
            new[] { nodes.GetNode(0), nodes.GetNode(0), nodes.GetNode(2) },
            new[] { 0, 2, 4, 6 }, new[] { 1, 2, 0, 2, 0, 1 }));
        Assert.Throws<ArgumentException>(() => new AgriculturalLogicalGeography(
            new[] { nodes.GetNode(0), nodes.GetNode(1), nodes.GetNode(2) },
            new[] { 0, 2, 4, 6 }, new[] { 2, 1, 0, 2, 0, 1 }));
        Assert.Throws<ArgumentException>(() => new AgriculturalLogicalGeography(
            new[] { nodes.GetNode(0), nodes.GetNode(1), nodes.GetNode(2) },
            new[] { 0, 1, 2, 3 }, new[] { 1, 2, 1 }));
        Assert.Throws<ArgumentException>(() => new AgriculturalLogicalGeography(
            new[] { nodes.GetNode(0), nodes.GetNode(1), nodes.GetNode(2) },
            new[] { 0, 2, 4 }, new[] { 1, 2, 0, 2, 0, 1 }));
        var result = AgriculturalGeographyProjection.Build(nodes);
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetSlope(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetSuitability(3, CropId.Maize));
        Assert.Throws<ArgumentException>(() => result.CopyScores(0, new int[5]));
    }

    [Fact]
    public void TwoDimensionalClimateGradientProducesSixDistinctCropRegions()
    {
        // Synthetic LAB climate, not a replacement for canonical worldgen.
        // A regular 13x13 logical lattice tests geographic differentiation
        // without assigning any biome labels or arbitrary crop availability.
        const int size = 13;
        const int n = size * size;
        var nodes = new AgriculturalGeographicNode[n];
        var offsets = new int[n + 1];
        var adjacent = new List<int>(4 * n);
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                int i = row * size + col;
                nodes[i] = new AgriculturalGeographicNode(
                    10_000 + i, 0,
                    -1_000_000 + row * 2_000_000 / (size - 1),
                    col * 1_000_000 / (size - 1), true);
                offsets[i] = adjacent.Count;
                if (row > 0) adjacent.Add(i - size);
                if (col > 0) adjacent.Add(i - 1);
                if (col + 1 < size) adjacent.Add(i + 1);
                if (row + 1 < size) adjacent.Add(i + size);
            }
        }
        offsets[n] = adjacent.Count;
        var projected = AgriculturalGeographyProjection.Build(
            new AgriculturalLogicalGeography(nodes, offsets, adjacent));
        var counts = new Dictionary<CropId, int>();
        for (int i = 0; i < n; i++)
        {
            CropId leading = projected.GetLeadingCrop(i)!.Value;
            counts.TryGetValue(leading, out int count);
            counts[leading] = count + 1;
        }
        Assert.Equal(6, counts.Count);
        Assert.Equal(20, counts[CropId.Maize]);
        Assert.Equal(33, counts[CropId.Sorghum]);
        Assert.Equal(22, counts[CropId.Wheat]);
        Assert.Equal(26, counts[CropId.Rice]);
        Assert.Equal(30, counts[CropId.Soybean]);
        Assert.Equal(38, counts[CropId.Barley]);
    }

    private static AgriculturalLogicalGeography CreateThreeNodeGeography()
    {
        return new AgriculturalLogicalGeography(
            new[]
            {
                new AgriculturalGeographicNode(100, 10_000, 650_000, 880_000, true),
                new AgriculturalGeographicNode(200, 20_000, -650_000, 250_000, true),
                new AgriculturalGeographicNode(300, -10_000, 350_000, 530_000, false)
            },
            new[] { 0, 2, 4, 6 },
            new[] { 1, 2, 0, 2, 0, 1 });
    }

    private static AgriculturalLogicalGeography CreateSyntheticRing(int n)
    {
        var nodes = new AgriculturalGeographicNode[n];
        var offsets = new int[n + 1];
        var neighbors = new int[2 * n];
        for (int i = 0; i < n; i++)
        {
            var crop = CropCatalog.All[i % CropCatalog.Count];
            nodes[i] = new AgriculturalGeographicNode(
                i + 100L, 0, crop.IdealTemperature, crop.IdealMoisture, true);
            offsets[i] = 2 * i;
            int left = (i + n - 1) % n;
            int right = (i + 1) % n;
            neighbors[2 * i] = Math.Min(left, right);
            neighbors[2 * i + 1] = Math.Max(left, right);
        }
        offsets[n] = neighbors.Length;
        return new AgriculturalLogicalGeography(nodes, offsets, neighbors);
    }
}
