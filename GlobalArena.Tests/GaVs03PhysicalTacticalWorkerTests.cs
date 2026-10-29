using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs03PhysicalTacticalWorkerTests
{
    [Fact]
    public void TacticalWorkersExistInOnePhysicalTileNotInEveryCoarseRegion()
    {
        var game = new EconomicPhysicalTacticalWorkerSession();
        Assert.Equal(42, game.CoarseWorld.StrategicTopology.Cells.Count);
        Assert.Equal(1442, game.Incidence.TileIncidences.Count);
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Single(game.Assignments.Select(x => x.TileId).Distinct());
        Assert.Equal(8UL, game.WorkersAt(game.InitialTile));
        Assert.Equal(8UL, game.StrategicDisplayTotals().Values.Sum(x => checked((long)x)));
        Assert.Single(game.StrategicDisplayTotals());
        Assert.Equal(0UL, game.Revision);
    }

    [Fact]
    public void MovingExistingWorkersToNextFineTileCreatesNoCityAndKeepsGlobalPopulation()
    {
        var game = new EconomicPhysicalTacticalWorkerSession();
        var original = game.InitialTile;
        var target = game.AdjacentWorkable(original)[0];
        var sourceBucket = game.DisplayBucket(original);
        game.MoveWorkers(original, target,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);

        Assert.Equal(1UL, game.Revision);
        Assert.Equal(5UL, game.WorkersAt(original));
        Assert.Equal(3UL, game.WorkersAt(target));
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Equal(2, game.Assignments.Select(x => x.TileId).Distinct().Count());
        Assert.Equal(8UL, game.StrategicDisplayTotals().Values.Aggregate(
            0UL, (sum, n) => checked(sum + n)));
        Assert.Contains(game.Assignments, x =>
            x.TileId == target && x.Activity == EconomicActivityKind.Forestry
            && x.Workers == 3UL);
        Assert.True(sourceBucket.IsValid);
    }

    [Fact]
    public void DemandIsGeneratedByAllWorkersWhereTheyActuallyLive()
    {
        var game = new EconomicPhysicalTacticalWorkerSession();
        var origin = game.InitialTile;
        var first = game.Preview();
        Assert.Single(first.Demand);
        Assert.Equal(4_000_000UL, first.Demand[0].RawQuantity);
        Assert.All(first.Supply, row => Assert.Equal(origin, row.TileId));

        var destination = game.AdjacentWorkable(origin)[0];
        game.MoveWorkers(origin, destination,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Mining, 2UL);
        var result = game.Preview();
        Assert.Equal(2, result.Demand.Count);
        Assert.Equal(4_000_000UL, result.Demand.Aggregate(0UL,
            (sum, d) => checked(sum + d.RawQuantity)));
        Assert.Contains(result.Demand, x =>
            x.TileId == destination && x.RawQuantity == 1_000_000UL);
        Assert.Equal(1UL, game.Revision);
        game.AdvancePreview();
        Assert.Equal(1UL, game.PreviewCount);
        Assert.Equal(1UL, game.Revision); // Previews never settle a turn.
    }

    [Fact]
    public void SharedEdgeAndVertexPhysicalIdsHaveExactlyOneLocationAndOneRollup()
    {
        var game = new EconomicPhysicalTacticalWorkerSession();
        Assert.Equal(120, game.Incidence.TileIncidences.Count(x =>
            x.IncidentCoarseCellIds.Count == 2));
        Assert.Equal(80, game.Incidence.TileIncidences.Count(x =>
            x.IncidentCoarseCellIds.Count == 3));
        foreach (var incidence in game.Incidence.TileIncidences)
        {
            var id = incidence.PhysicalTacticalTileId;
            Assert.Equal(incidence.IncidentCoarseCellIds, game.Incidents(id));
            Assert.Contains(game.DisplayBucket(id),
                incidence.IncidentCoarseCellIds);
        }
    }

    [Fact]
    public void InvalidTransfersAreAtomicAndNoWorkerOrRevisionChanges()
    {
        var game = new EconomicPhysicalTacticalWorkerSession();
        var source = game.InitialTile;
        var dest = game.AdjacentWorkable(source)[0];
        var initialAssignments = game.Assignments.ToArray();

        Assert.Throws<InvalidOperationException>(() =>
            game.MoveWorkers(source, dest, EconomicActivityKind.Agriculture,
                EconomicActivityKind.Mining, 5UL));
        Assert.Throws<InvalidOperationException>(() =>
            game.MoveWorkers(source, source, EconomicActivityKind.Agriculture,
                EconomicActivityKind.Mining, 2UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            game.MoveWorkers(source, dest, EconomicActivityKind.Agriculture,
                EconomicActivityKind.Mining, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            game.MoveWorkers(source, dest, EconomicActivityKind.TradeLogistics,
                EconomicActivityKind.Mining, 1UL));
        Assert.Equal(0UL, game.Revision);
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Equal(initialAssignments, game.Assignments);
    }

    [Fact]
    public void SameSeedAndPhysicalOrdersGiveIdenticalStateAndProjection()
    {
        var a = new EconomicPhysicalTacticalWorkerSession(0UL);
        var b = new EconomicPhysicalTacticalWorkerSession(0UL);
        Assert.Equal(a.InitialTile, b.InitialTile);
        var targetA = a.AdjacentWorkable(a.InitialTile)[0];
        var targetB = b.AdjacentWorkable(b.InitialTile)[0];
        Assert.Equal(targetA, targetB);
        a.MoveWorkers(a.InitialTile, targetA,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        b.MoveWorkers(b.InitialTile, targetB,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        Assert.Equal(a.Assignments, b.Assignments);
        Assert.Equal(a.StrategicDisplayTotals(), b.StrategicDisplayTotals());
        Assert.Equal(a.Preview().Demand, b.Preview().Demand);
        Assert.Equal(a.Preview().Supply, b.Preview().Supply);
    }
}
