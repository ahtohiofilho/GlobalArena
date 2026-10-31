using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs37GeographicLandEdgeCostTests
{
    private static readonly SphericalPoint3 A = new(1, 0, 0);
    private static readonly SphericalPoint3 Near = new(1, 0.08, 0);
    private static readonly SphericalPoint3 Far = new(1, 0.20, 0);

    private static GeographicLandEdgeCostQuote Quote(
        SphericalPoint3 to, long startHeight = 0, long endHeight = 0,
        long moisture = 250_000L, bool forest = false) =>
        GeographicLandEdgeCostCalculator.Quote(
            A, to, startHeight, endHeight, moisture, moisture,
            fromForest: false, toForest: forest);

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    [Fact]
    public void DistanceIsDerivedFromCanonicalSphericalCenters()
    {
        var near = Quote(Near);
        var far = Quote(Far);
        Assert.True(near.ChordMicroUnits < far.ChordMicroUnits);
        Assert.True(near.DistancePoints < far.DistancePoints);
        Assert.True(near.TotalPoints < far.TotalPoints);
        Assert.True(near.TotalPoints > 0UL);
        Assert.Equal(0U, near.GradePenaltyPercent);
        Assert.Equal(near.DistancePoints, near.TotalPoints);
    }

    [Fact]
    public void SameBiomeLabelsDoNotForceIdenticalMountainPlainCosts()
    {
        var mild = Quote(Near, 550_000L, 450_000L);
        var steep = Quote(Near, 550_000L, 0L);
        Assert.Equal(mild.DistancePoints, steep.DistancePoints);
        Assert.True(steep.GradePenaltyPercent > mild.GradePenaltyPercent);
        Assert.True(steep.TotalPoints > mild.TotalPoints);
    }

    [Fact]
    public void HighPlateauHasNoHeightPenaltyWithoutSlope()
    {
        var seaLevel = Quote(Near);
        var plateau = Quote(Near, 900_000L, 900_000L);
        Assert.Equal(0U, plateau.GradePenaltyPercent);
        Assert.Equal(seaLevel.TotalPoints, plateau.TotalPoints);
    }

    [Fact]
    public void UphillAndDownhillHaveDirectionalCostsWithoutAlteredDistance()
    {
        var uphill = Quote(Near, 0L, 600_000L);
        var downhill = Quote(Near, 600_000L, 0L);
        Assert.Equal(uphill.DistancePoints, downhill.DistancePoints);
        Assert.Equal(uphill.ChordMicroUnits, downhill.ChordMicroUnits);
        Assert.True(uphill.GradePenaltyPercent > downhill.GradePenaltyPercent);
        Assert.True(uphill.TotalPoints > downhill.TotalPoints);
    }

    [Fact]
    public void WetSoilAndForestCoverDoNotChargeTwoCorrelatedFullPenalties()
    {
        var plain = Quote(Near);
        var forest = Quote(Near, forest: true);
        var wet = Quote(Near, moisture: 1_000_000L);
        var both = Quote(Near, moisture: 1_000_000L, forest: true);
        Assert.Equal(15U, forest.SurfacePenaltyPercent);
        Assert.Equal(20U, wet.SurfacePenaltyPercent);
        Assert.Equal(20U, both.SurfacePenaltyPercent);
        Assert.Equal(wet.TotalPoints, both.TotalPoints);
        Assert.True(forest.TotalPoints > plain.TotalPoints);
    }

    [Fact]
    public void RepeatedQuotesAreIdenticalAndInputsAreRangeChecked()
    {
        var a = Quote(Near, -500_000L, 500_000L, 700_000L, forest: true);
        var b = Quote(Near, -500_000L, 500_000L, 700_000L, forest: true);
        Assert.Equal(a, b);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Quote(Near, endHeight: 1_000_001L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Quote(Near, moisture: -1L));
        Assert.Throws<ArgumentException>(() => Quote(A));
    }

    [Fact]
    public void RealGeographyProducesReadOnlyPositiveMovementCostAndRoute()
    {
        var game = Start();
        var provider = new CanonicalGeographicLandEdgeCostProvider();
        var origin = game.InitialTile;
        var near = game.AdjacentWorkable(origin).ToHashSet();
        var target = near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != origin && !near.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
        var direct = near.OrderBy(x => x.FineStrategicCellId.Value).First();
        var saved = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var quote = provider.Explain(game, origin, direct);
        Assert.True(quote.ChordMicroUnits > 0UL);
        Assert.True(quote.DistancePoints > 0UL);
        Assert.True(quote.TotalPoints >= quote.DistancePoints);
        Assert.Equal(quote.TotalPoints, provider.CostPoints(game, origin, direct));

        var route = ScalarPhysicalRouteProjection.Preview(
            game, origin, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, 200UL, provider);
        Assert.True(route.HasRoute);
        Assert.Equal(origin, route.Tiles[0]);
        Assert.Equal(target, route.Tiles[^1]);
        Assert.True(route.Timing!.TotalCostPoints > 0UL);
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void SeaIsNeverMadeWalkableByLargeScalarMovementBudgets()
    {
        var game = Start();
        var oceanTile = game.FineWorld.StrategicTopology.Cells
            .First(x => !game.IsPhysicalWorkableLand(x.Id));
        var ocean = new PhysicalTacticalTileId(
            new GoldbergParameters(18, 0), oceanTile.Id);
        var provider = new CanonicalGeographicLandEdgeCostProvider();
        Assert.Throws<InvalidOperationException>(() =>
            provider.Explain(game, game.InitialTile, ocean));
        var route = ScalarPhysicalRouteProjection.Preview(
            game, game.InitialTile, ocean,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, ulong.MaxValue, provider);
        Assert.False(route.HasRoute);
        Assert.Null(route.Timing);
    }
}
