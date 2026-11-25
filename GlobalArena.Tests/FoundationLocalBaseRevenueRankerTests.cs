using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Tests;

public sealed class FoundationLocalBaseRevenueRankerTests
{
    private static FoundationLocalBaseCandidate Wood(ulong quantity, ulong? price = 2UL) =>
        new(PhysicalWorkerOccupation.Forestry, null, quantity, price, 1UL);
    private static FoundationLocalBaseCandidate Stone(ulong quantity, ulong? price = 2UL) =>
        new(PhysicalWorkerOccupation.Mining, null, quantity, price, 1UL);
    private static FoundationLocalBaseCandidate Crop(byte crop, ulong quantity, ulong? price) =>
        new(PhysicalWorkerOccupation.Agriculture, crop, quantity, price, 1UL);

    [Fact]
    public void LocalPriceAndAvailableEffortDetermineChoiceWithoutMarket()
    {
        var wood = Wood(8);
        var crop = Crop(1, 10, 1);
        var stone = Stone(6);
        var counts = new Dictionary<(PhysicalWorkerOccupation, byte?), uint>();
        Assert.Equal(wood, FoundationLocalBaseRevenueRanker.Select(new[] { crop, stone, wood }, counts));
        var residualWood = wood with { PotentialGoodsMicro = 3 };
        Assert.Equal(stone, FoundationLocalBaseRevenueRanker.Select(new[] { crop, residualWood, stone }, counts));
        Assert.Empty(counts);
    }

    [Fact]
    public void MissingPriceUsesReferenceButObservedZeroDoesNot()
    {
        var missing = Wood(8, null);
        var zero = Stone(ulong.MaxValue, 0UL);
        var counts = new Dictionary<(PhysicalWorkerOccupation, byte?), uint>();
        Assert.Equal(new BigInteger(8), missing.EstimatedRevenue);
        Assert.Equal(missing, FoundationLocalBaseRevenueRanker.Select(new[] { zero, missing }, counts));
        Assert.Null(FoundationLocalBaseRevenueRanker.Select(new[] { zero, missing with { PotentialGoodsMicro = 0 } }, counts));
        Assert.Null(FoundationLocalBaseRevenueRanker.Select(Array.Empty<FoundationLocalBaseCandidate>(), counts));
    }

    [Fact]
    public void ReservationsDiversifyWithoutCappingAnActivity()
    {
        var wood = Wood(8);
        var corn = Crop(1, 14, 1);
        var stone = Stone(6);
        var soy = Crop(2, 10, 1);
        var candidates = new[] { soy, stone, corn, wood };
        var counts = new Dictionary<(PhysicalWorkerOccupation, byte?), uint>();
        var expected = new[] { wood, corn, stone, soy, wood };
        foreach (var item in expected)
        {
            var selected = FoundationLocalBaseRevenueRanker.Select(candidates, counts);
            Assert.Equal(item, selected);
            var key = (item.Occupation, item.Crop);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        Assert.Equal(2U, counts[(wood.Occupation, wood.Crop)]);
    }

    [Fact]
    public void ExactRationalTiesAreCanonicalAndLargeScoresDoNotOverflow()
    {
        var a = Crop(1, ulong.MaxValue, ulong.MaxValue);
        var b = Crop(2, ulong.MaxValue, ulong.MaxValue);
        var counts = new Dictionary<(PhysicalWorkerOccupation, byte?), uint>();
        Assert.Equal(a, FoundationLocalBaseRevenueRanker.Select(new[] { b, a }, counts));
        counts[(a.Occupation, a.Crop)] = 1;
        Assert.Equal(b, FoundationLocalBaseRevenueRanker.Select(new[] { a, b }, counts));
        var c = Crop(3, 3, 1);
        var d = Crop(4, 4, 1);
        counts[(c.Occupation, c.Crop)] = 1;
        counts[(d.Occupation, d.Crop)] = 2;
        Assert.Equal(c, FoundationLocalBaseRevenueRanker.Select(new[] { d, c }, counts));
    }

    [Fact]
    public void RejectsDuplicateActivitiesAndMissingInitialReference()
    {
        var wood = Wood(8);
        var counts = new Dictionary<(PhysicalWorkerOccupation, byte?), uint>();
        Assert.Throws<ArgumentException>(() => FoundationLocalBaseRevenueRanker.Select(new[] { wood, wood }, counts));
        Assert.Throws<ArgumentException>(() => FoundationLocalBaseRevenueRanker.Select(new[] { wood with { ReferencePriceMicro = 0 } }, counts));
    }
}
