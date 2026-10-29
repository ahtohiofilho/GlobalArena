using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// No city objects, no capital or investment function assumed.
/// Construction of a free primitive opportunity MUST precede an
/// allocation. The same existing worker produces and consumes.
/// </summary>
public sealed class GaVs06InfrastructureFirstLabTests
{
    [Theory]
    [InlineData(0UL, 2, 12)]
    [InlineData(20261012UL, 3, 18)]
    public void FreePrimitiveSiteMustExistBeforeWorkersCanMove(
        ulong seed, int coarseM, int fineM)
    {
        var game = NewGame(seed, coarseM, fineM);
        var origin = game.InitialTile;
        var next = game.AdjacentWorkable(origin).First(id =>
            game.CanInstallPrimitiveSite(id, EconomicActivityKind.Agriculture));
        var previous = game.Preview();
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Equal(3, game.PrimitiveSites.Count);
        Assert.All(game.Assignments, allocation =>
            Assert.True(game.HasPrimitiveSite(allocation.TileId, allocation.Activity)));
        Assert.True(game.CanInstallPrimitiveSite(next, EconomicActivityKind.Agriculture));
        Assert.False(game.HasPrimitiveSite(next, EconomicActivityKind.Agriculture));
        Assert.Throws<InvalidOperationException>(() => game.MoveWorkers(
            origin, next, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Agriculture, 3UL));
        Assert.Equal(0UL, game.Revision);
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Single(game.Preview().Demand); // failed move produced no remote consumers

        game.InstallPrimitiveSite(next, EconomicActivityKind.Agriculture);
        Assert.Equal(1UL, game.Revision);
        Assert.Equal(4, game.PrimitiveSites.Count);
        Assert.Equal(0UL, game.WorkersAt(next));
        Assert.Equal(previous.Supply, game.Preview().Supply); // no workers were moved
        Assert.Equal(previous.Demand, game.Preview().Demand);
        Assert.Throws<InvalidOperationException>(() => game.InstallPrimitiveSite(
            next, EconomicActivityKind.Agriculture));
        Assert.Equal(1UL, game.Revision);

        game.MoveWorkers(origin, next, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Agriculture, 3UL);
        Assert.Equal(2UL, game.Revision);
        Assert.Equal(5UL, game.WorkersAt(origin));
        Assert.Equal(3UL, game.WorkersAt(next));
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Equal(2, game.Preview().Demand.Count);
        Assert.Equal(4_000_000UL, game.Preview().Demand.Aggregate(
            0UL, (sum, x) => checked(sum + x.RawQuantity)));
        Assert.Equal(8UL, game.StrategicDisplayTotals().Values.Aggregate(
            0UL, (sum, x) => checked(sum + x)));
        Assert.Equal(4, game.PrimitiveSites.Count);
    }

    [Fact]
    public void DifferentActivityRequiresItsOwnInstalledOpportunity()
    {
        var game = NewGame(0UL, 2, 12);
        var origin = game.InitialTile;
        var dest = game.AdjacentWorkable(origin).First(id =>
            game.CanInstallPrimitiveSite(id, EconomicActivityKind.Agriculture) &&
            game.CanInstallPrimitiveSite(id, EconomicActivityKind.Forestry));

        game.InstallPrimitiveSite(dest, EconomicActivityKind.Agriculture);
        Assert.Throws<InvalidOperationException>(() => game.MoveWorkers(
            origin, dest, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL));
        Assert.Equal(8UL, game.TotalWorkers);
        Assert.Equal(0UL, game.WorkersAt(dest));

        game.InstallPrimitiveSite(dest, EconomicActivityKind.Forestry);
        game.MoveWorkers(origin, dest, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        Assert.Equal(2UL, game.WorkersAt(dest));
        Assert.Contains(game.Assignments,
            x => x.TileId == dest && x.Activity == EconomicActivityKind.Forestry
                 && x.Workers == 2UL);
    }

    [Fact]
    public void InvalidTerrainAndForeignWorldCannotAcquirePrimitiveSites()
    {
        var game = NewGame(0UL, 2, 12);
        var water = game.FineWorld.StrategicTopology.Cells
            .Select(x => x.Id).First(id => !game.IsWorkableLand(id));
        var forbidden = game.PhysicalId(water);
        var revision = game.Revision;
        Assert.False(game.CanInstallPrimitiveSite(
            forbidden, EconomicActivityKind.Agriculture));
        Assert.Throws<InvalidOperationException>(() => game.InstallPrimitiveSite(
            forbidden, EconomicActivityKind.Agriculture));

        var foreignTile = new PhysicalTacticalTileId(
            new GoldbergParameters(18, 0), new StrategicCellId(1UL));
        Assert.Throws<ArgumentException>(() => game.InstallPrimitiveSite(
            foreignTile, EconomicActivityKind.Agriculture));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.InstallPrimitiveSite(
            game.InitialTile, EconomicActivityKind.TradeLogistics));
        Assert.Equal(revision, game.Revision);
        Assert.Equal(3, game.PrimitiveSites.Count);
    }

    [Fact]
    public void LegacyProbeStillReplaysWithoutImplicitFacilityMigration()
    {
        var legacy = new EconomicPhysicalTacticalWorkerSession();
        Assert.False(legacy.RequirePrimitiveSites);
        Assert.Empty(legacy.PrimitiveSites);
        var adjacent = legacy.AdjacentWorkable(legacy.InitialTile)[0];
        Assert.Throws<InvalidOperationException>(() => legacy.InstallPrimitiveSite(
            adjacent, EconomicActivityKind.Agriculture));
        legacy.MoveWorkers(legacy.InitialTile, adjacent,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        Assert.Equal(3UL, legacy.WorkersAt(adjacent));
        Assert.Empty(legacy.PrimitiveSites);
        Assert.Equal(1UL, legacy.Revision);
    }

    [Fact]
    public void IdenticalFreeSiteActionsDeterministicallyReplay()
    {
        var a = NewGame(0UL, 2, 12);
        var b = NewGame(0UL, 2, 12);
        var da = a.AdjacentWorkable(a.InitialTile).First(x =>
            a.CanInstallPrimitiveSite(x, EconomicActivityKind.Agriculture));
        var db = b.AdjacentWorkable(b.InitialTile).First(x =>
            b.CanInstallPrimitiveSite(x, EconomicActivityKind.Agriculture));
        Assert.Equal(da, db);
        a.InstallPrimitiveSite(da, EconomicActivityKind.Agriculture);
        b.InstallPrimitiveSite(db, EconomicActivityKind.Agriculture);
        a.MoveWorkers(a.InitialTile, da, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Agriculture, 3UL);
        b.MoveWorkers(b.InitialTile, db, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Agriculture, 3UL);
        Assert.Equal(a.PrimitiveSites, b.PrimitiveSites);
        Assert.Equal(a.Assignments, b.Assignments);
        Assert.Equal(a.Preview().Supply, b.Preview().Supply);
        Assert.Equal(a.Preview().Demand, b.Preview().Demand);
        Assert.Equal(a.Revision, b.Revision);
    }

    private static EconomicPhysicalTacticalWorkerSession NewGame(
        ulong seed, int coarseM, int fineM) =>
        new(seed, new GoldbergParameters(coarseM, 0),
            new GoldbergParameters(fineM, 0), requirePrimitiveSites: true);
}
