namespace GlobalArena.World;

internal readonly record struct StrategicCellConstructionProvenance
{
    public int FirstSeedVertex { get; }

    public long FirstWeight { get; }

    public int SecondSeedVertex { get; }

    public long SecondWeight { get; }

    public int ThirdSeedVertex { get; }

    public long ThirdWeight { get; }

    public bool IsValid =>
        FirstSeedVertex > 0
        && FirstWeight > 0
        && SecondWeight >= 0
        && ThirdWeight >= 0
        && (SecondWeight == 0
            || SecondSeedVertex > FirstSeedVertex)
        && (ThirdWeight == 0
            || ThirdSeedVertex > SecondSeedVertex);

    private StrategicCellConstructionProvenance(
        int firstSeedVertex,
        long firstWeight,
        int secondSeedVertex,
        long secondWeight,
        int thirdSeedVertex,
        long thirdWeight)
    {
        FirstSeedVertex =
            firstSeedVertex;

        FirstWeight =
            firstWeight;

        SecondSeedVertex =
            secondSeedVertex;

        SecondWeight =
            secondWeight;

        ThirdSeedVertex =
            thirdSeedVertex;

        ThirdWeight =
            thirdWeight;
    }

    public static StrategicCellConstructionProvenance Create(
        int firstSeedVertex,
        long firstWeight,
        int secondSeedVertex,
        long secondWeight,
        int thirdSeedVertex,
        long thirdWeight)
    {
        var candidates =
            new[]
            {
                new SeedContribution(
                    firstSeedVertex,
                    firstWeight),
                new SeedContribution(
                    secondSeedVertex,
                    secondWeight),
                new SeedContribution(
                    thirdSeedVertex,
                    thirdWeight)
            };

        if (candidates.Any(
            item =>
                item.Weight < 0
                || (item.Weight > 0
                    && item.SeedVertexId <= 0)))
        {
            throw new ArgumentException(
                "Construction provenance weights must be non-negative and weighted seed IDs must be positive.");
        }

        var weighted =
            candidates
                .Where(
                    item =>
                        item.Weight > 0)
                .OrderBy(
                    item =>
                        item.SeedVertexId)
                .ToArray();

        if (weighted.Length is < 1 or > 3
            || weighted
                .Select(
                    item =>
                        item.SeedVertexId)
                .Distinct()
                .Count()
                != weighted.Length)
        {
            throw new ArgumentException(
                "Construction provenance must contain one to three distinct weighted seed vertices.");
        }

        return new StrategicCellConstructionProvenance(
            weighted[0].SeedVertexId,
            weighted[0].Weight,
            weighted.ElementAtOrDefault(1).SeedVertexId,
            weighted.ElementAtOrDefault(1).Weight,
            weighted.ElementAtOrDefault(2).SeedVertexId,
            weighted.ElementAtOrDefault(2).Weight);
    }

    public SeedContribution[] GetContributions()
    {
        var result =
            new List<SeedContribution>(3)
            {
                new(
                    FirstSeedVertex,
                    FirstWeight)
            };

        if (SecondWeight > 0)
        {
            result.Add(
                new SeedContribution(
                    SecondSeedVertex,
                    SecondWeight));
        }

        if (ThirdWeight > 0)
        {
            result.Add(
                new SeedContribution(
                    ThirdSeedVertex,
                    ThirdWeight));
        }

        return result.ToArray();
    }

    internal readonly record struct SeedContribution(
        int SeedVertexId,
        long Weight);
}
