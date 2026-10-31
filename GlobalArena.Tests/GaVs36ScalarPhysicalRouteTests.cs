using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs36ScalarPhysicalRouteTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private sealed class TestEdgeCosts(
        ulong normal = 100UL,
        PhysicalTacticalTileId? blockedSource = null,
        PhysicalTacticalTileId? costlyNeighbor = null,
        ulong costlyEdge = 10_000UL) : IScalarPhysicalEdgeCostProvider
    {
        public ulong Calls { get; private set; }

        public ulong CostPoints(
            CanonicalPhysicalTacticalSession session,
            PhysicalTacticalTileId from, PhysicalTacticalTileId to)
        {
            Calls++;
            return from == blockedSource && to == costlyNeighbor
                ? costlyEdge : normal;
        }
    }

    private static PhysicalTacticalTileId TwoHops(
        CanonicalPhysicalTacticalSession game)
    {
        var neighbors = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        return neighbors.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !neighbors.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
    }

    private static ScalarPhysicalRoutePreview Quote(
        CanonicalPhysicalTacticalSession game,
        PhysicalTacticalTileId destination, ulong pointsPerTurn,
        IScalarPhysicalEdgeCostProvider provider) =>
        ScalarPhysicalRouteProjection.Preview(game, game.InitialTile,
            destination, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, pointsPerTurn, provider);

    [Fact]
    public void UniformCostsHaveSamePhysicalLegsAsLegacyButDifferentTiming()
    {
        var game = Start();
        var destination = TwoHops(game);
        var legacy = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        var originalSave = game.ExportSaveJson();
        var originalHash = game.CurrentHash;
        var provider = new TestEdgeCosts();
        var result = Quote(game, destination, 150UL, provider);

        Assert.True(result.HasRoute);
        Assert.Equal(legacy.MovementLegs, (ulong)result.EdgeCosts.Count);
        Assert.Equal(result.EdgeCosts.Count + 1, result.Tiles.Count);
        Assert.Equal(game.InitialTile, result.Tiles[0]);
        Assert.Equal(destination, result.Tiles[^1]);
        Assert.All(result.EdgeCosts, cost => Assert.Equal(100UL, cost));
        Assert.Equal(200UL, result.Timing!.TotalCostPoints);
        Assert.Equal(game.State.MovementTurn + 2UL,
            result.Timing.ArrivalTurn);
        Assert.Equal(game.State.MovementTurn + 4UL,
            legacy.EarliestArrivalTurn);
        Assert.True(provider.Calls > 0);
        Assert.Equal(originalSave, game.ExportSaveJson());
        Assert.Equal(originalHash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void ChoosesCheapestPhysicalPathOverExpensiveLegacyFirstHop()
    {
        var game = Start();
        var near = game.AdjacentWorkable(game.InitialTile).ToHashSet();
        var candidates = near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != game.InitialTile && !near.Contains(x))
            .Distinct().OrderBy(x => x.FineStrategicCellId.Value);
        var destination = candidates.First(x =>
            game.AdjacentWorkable(x).Count(near.Contains) >= 2);
        var legacy = PhysicalRemoteDestinationRouteProjection.Preview(
            game, game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(2UL, legacy.MovementLegs);
        var forbiddenFirstHop = legacy.Tiles[1];
        var provider = new TestEdgeCosts(
            blockedSource: game.InitialTile, costlyNeighbor: forbiddenFirstHop);
        var result = Quote(game, destination, 200UL, provider);

        Assert.NotEqual(forbiddenFirstHop, result.Tiles[1]);
        Assert.Equal(2, result.EdgeCosts.Count);
        Assert.Equal(200UL, result.Timing!.TotalCostPoints);
        Assert.Equal(1UL, result.Timing.TotalMovementTurns);
        Assert.Equal(100UL, result.EdgeCosts[0]);
        Assert.Equal(100UL, result.EdgeCosts[1]);
    }

    [Fact]
    public void SameGeometryAndCostsGiveDeterministicRouteAndSchedule()
    {
        var game = Start();
        var destination = TwoHops(game);
        var a = Quote(game, destination, 130UL, new TestEdgeCosts());
        var b = Quote(game, destination, 130UL, new TestEdgeCosts());
        Assert.Equal(a.Availability, b.Availability);
        Assert.Equal(a.Tiles.ToArray(), b.Tiles.ToArray());
        Assert.Equal(a.EdgeCosts.ToArray(), b.EdgeCosts.ToArray());
        Assert.Equal(a.Timing!.ArrivalTurn, b.Timing!.ArrivalTurn);
        Assert.Equal(a.Timing.Legs.ToArray(), b.Timing.Legs.ToArray());
    }

    [Fact]
    public void SpeedChangesArrivalNotRouteOrPhysicalTileSelection()
    {
        var game = Start();
        var destination = TwoHops(game);
        var slow = Quote(game, destination, 80UL, new TestEdgeCosts());
        var fast = Quote(game, destination, 200UL, new TestEdgeCosts());
        Assert.Equal(slow.Tiles.ToArray(), fast.Tiles.ToArray());
        Assert.Equal(3UL, slow.Timing!.TotalMovementTurns);
        Assert.Equal(1UL, fast.Timing!.TotalMovementTurns);
    }

    [Fact]
    public void UnreachableWaterRemainsInaccessibleDespiteAvailablePointBudget()
    {
        var game = Start();
        var water = game.FineWorld.StrategicTopology.Cells
            .First(x => !game.IsPhysicalWorkableLand(x.Id)).Id;
        var ocean = new PhysicalTacticalTileId(new GoldbergParameters(18, 0), water);
        var provider = new TestEdgeCosts();
        var result = Quote(game, ocean, ulong.MaxValue, provider);
        Assert.False(result.HasRoute);
        Assert.Equal(PhysicalPrimitiveRouteAvailability.DestinationNeedsOtherTechnology,
            result.Availability);
        Assert.Empty(result.Tiles);
        Assert.Empty(result.EdgeCosts);
        Assert.Null(result.Timing);
        Assert.Equal(0UL, provider.Calls);
    }

    [Fact]
    public void RejectsZeroSpeedZeroEdgeCostsAndMissingProvider()
    {
        var game = Start();
        var destination = TwoHops(game);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Quote(game, destination, 0UL, new TestEdgeCosts()));
        Assert.Throws<ArgumentNullException>(() =>
            Quote(game, destination, 200UL, null!));
        Assert.Throws<InvalidOperationException>(() =>
            Quote(game, destination, 200UL, new TestEdgeCosts(normal: 0UL)));
    }
}
