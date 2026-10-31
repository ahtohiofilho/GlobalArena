using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs38SharedPhysicalEdgeFactsTests
{
    private static CanonicalPhysicalTacticalSession Start(ulong seed = 20261012UL) =>
        CanonicalPhysicalTacticalSession.Start(
            seed, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static PhysicalTacticalTileId TwoHops(
        CanonicalPhysicalTacticalSession game)
    {
        var adjacent = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return adjacent.SelectMany(game.AdjacentWorkable)
            .Where(id => id != game.InitialTile && !adjacent.Contains(id))
            .OrderBy(id => id.FineStrategicCellId.Value).First();
    }

    private static ScalarPhysicalRoutePreview Preview(
        CanonicalPhysicalTacticalSession game,
        PhysicalTacticalTileId destination,
        IScalarPhysicalEdgeCostProvider provider) =>
        ScalarPhysicalRouteProjection.Preview(
            game, game.InitialTile, destination,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, 200UL, provider);

    [Fact]
    public void MaterializationExactlyMatchesTheWalkableGraph()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var snapshot = new CanonicalLandEdgeFactsSnapshot(game);
        var expected = game.FineWorld.StrategicTopology.Cells
            .Where(c => game.IsPhysicalWorkableLand(c.Id))
            .Sum(c => c.AdjacentCellIds.Count(game.IsPhysicalWorkableLand));
        Assert.Equal(expected, snapshot.DirectedEdgeCount);
        Assert.True(expected > 100);
        Assert.Equal(0, snapshot.DirectedEdgeCount % 2);
        Assert.Equal(snapshot.DirectedEdgeCount,
            snapshot.Edges.Select(f => (f.From, f.To)).Distinct().Count());
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void EveryReverseEdgeSharesImmutableGeographyButNotNecessarilyMovementCost()
    {
        var game = Start();
        var snapshot = new CanonicalLandEdgeFactsSnapshot(game);
        var direct = new CanonicalGeographicLandEdgeCostProvider();

        foreach (var f in snapshot.Edges)
        {
            var reverse = snapshot.Find(f.To, f.From);
            Assert.NotNull(reverse);
            Assert.Equal(f.UnitSphereChordMicroUnits, reverse.UnitSphereChordMicroUnits);
            Assert.Equal(f.SignedElevationDeltaRaw, -reverse.SignedElevationDeltaRaw);
            Assert.Equal(f.AverageMoistureRaw, reverse.AverageMoistureRaw);
            Assert.Equal(f.TouchesForest, reverse.TouchesForest);
            Assert.Equal(f.UnitSphereChordMicroUnits,
                f.ExperimentalWalkingQuote.ChordMicroUnits);
            Assert.Equal(f.SignedElevationDeltaRaw,
                f.ExperimentalWalkingQuote.SignedElevationChangeRaw);
            Assert.Equal(
                direct.CostPoints(game, f.From, f.To),
                snapshot.CostPoints(game, f.From, f.To));
        }
    }

    [Fact]
    public void PrecalculatedWalkingQuotesSelectTheSamePhysicalPathAndEta()
    {
        var game = Start();
        var destination = TwoHops(game);
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var cached = new CanonicalLandEdgeFactsSnapshot(game);
        var reference = new CanonicalGeographicLandEdgeCostProvider();

        var baseline = Preview(game, destination, reference);
        for (var i = 0; i < 3; i++)
        {
            var quote = Preview(game, destination, cached);
            Assert.True(quote.HasRoute);
            Assert.Equal(baseline.Tiles.ToArray(), quote.Tiles.ToArray());
            Assert.Equal(baseline.EdgeCosts.ToArray(), quote.EdgeCosts.ToArray());
            Assert.Equal(baseline.Timing!.TotalCostPoints,
                quote.Timing!.TotalCostPoints);
            Assert.Equal(baseline.Timing.ArrivalTurn, quote.Timing.ArrivalTurn);
        }

        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void RepeatedReadsReuseFactsWithoutRecalculatingTheWorld()
    {
        var game = Start();
        var snapshot = new CanonicalLandEdgeFactsSnapshot(game);
        var edge = snapshot.Edges[0];
        var first = snapshot.Find(edge.From, edge.To);
        for (var i = 0; i < 1000; i++)
        {
            Assert.Same(first, snapshot.Find(edge.From, edge.To));
            Assert.Equal(edge.ExperimentalWalkingQuote.TotalPoints,
                snapshot.CostPoints(game, edge.From, edge.To));
        }
    }

    [Fact]
    public void NoEdgeExistsForWaterOrNonadjacentLand()
    {
        var game = Start();
        var snapshot = new CanonicalLandEdgeFactsSnapshot(game);
        var water = game.FineWorld.StrategicTopology.Cells
            .First(c => !game.IsPhysicalWorkableLand(c.Id));
        var sea = new PhysicalTacticalTileId(
            game.InitialTile.FineGoldbergParameters, water.Id);
        var remote = TwoHops(game);

        Assert.Null(snapshot.Find(game.InitialTile, sea));
        Assert.Null(snapshot.Find(game.InitialTile, remote));
        Assert.Throws<InvalidOperationException>(
            () => snapshot.CostPoints(game, game.InitialTile, sea));
        Assert.Throws<InvalidOperationException>(
            () => snapshot.CostPoints(game, game.InitialTile, remote));
        var scalar = Preview(game, sea, snapshot);
        Assert.False(scalar.HasRoute);
        Assert.Null(scalar.Timing);
    }

    [Fact]
    public void DoesNotLeakOneWorldOrSessionIntoAnother()
    {
        var first = Start();
        var second = Start();
        var snapshot = new CanonicalLandEdgeFactsSnapshot(first);
        var edge = snapshot.Edges[0];
        Assert.Throws<InvalidOperationException>(() =>
            snapshot.CostPoints(second, edge.From, edge.To));
        Assert.Throws<ArgumentNullException>(
            () => new CanonicalLandEdgeFactsSnapshot(null!));
    }

    [Fact]
    public void FactsStayStableAcrossEconomicAndMovementTicks()
    {
        var game = Start();
        var snapshot = new CanonicalLandEdgeFactsSnapshot(game);
        var edge = snapshot.Edges[0];
        var original = snapshot.Find(edge.From, edge.To);
        game.AdvanceMovementTurn();
        game.AdvanceMovementTurn();

        Assert.Same(original, snapshot.Find(edge.From, edge.To));
        Assert.Equal(original!.ExperimentalWalkingQuote.TotalPoints,
            snapshot.CostPoints(game, edge.From, edge.To));
        Assert.Equal(new CanonicalGeographicLandEdgeCostProvider().Explain(
            game, edge.From, edge.To), original.ExperimentalWalkingQuote);
    }
}
