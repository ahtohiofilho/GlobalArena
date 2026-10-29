using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs05OriginalGodotTacticalWorkerBindingTests
{
    [Fact]
    public void PhysicalWorkforceUsesExactProductionGodotG3ToG18Identity()
    {
        var session = new EconomicPhysicalTacticalWorkerSession(
            20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        Assert.Equal(new GoldbergParameters(3, 0), session.CoarseWorldParameters);
        Assert.Equal(new GoldbergParameters(18, 0), session.FineWorldParameters);
        Assert.Equal(92, session.CoarseWorld.StrategicTopology.Cells.Count);
        Assert.Equal(3242, session.FineWorld.StrategicTopology.Cells.Count);
        Assert.Equal(3242, session.Incidence.TileIncidences.Count);
        Assert.Equal(new GoldbergParameters(18, 0), session.InitialTile.FineGoldbergParameters);
        Assert.Equal(8UL, session.WorkersAt(session.InitialTile));
        Assert.Contains(session.DisplayBucket(session.InitialTile),
            session.Incidents(session.InitialTile));
        Assert.Equal(8UL, session.StrategicDisplayTotals().Values.Aggregate(
            0UL, (sum, value) => checked(sum + value)));
    }

    [Fact]
    public void MovingOnProductionFineMeshConservesWorkersAndGivesNewDemand()
    {
        var session = new EconomicPhysicalTacticalWorkerSession(
            20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        var target = session.AdjacentWorkable(session.InitialTile).First();
        session.MoveWorkers(session.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        Assert.Equal(1UL, session.Revision);
        Assert.Equal(5UL, session.WorkersAt(session.InitialTile));
        Assert.Equal(3UL, session.WorkersAt(target));
        Assert.Equal(8UL, session.TotalWorkers);
        Assert.Equal(2, session.Assignments.Select(x => x.TileId).Distinct().Count());
        Assert.Equal(4_000_000UL, session.Preview().Demand.Aggregate(
            0UL, (sum, demand) => checked(sum + demand.RawQuantity)));
        Assert.Equal(8UL, session.StrategicDisplayTotals().Values.Aggregate(
            0UL, (sum, value) => checked(sum + value)));
        Assert.Throws<InvalidOperationException>(() => session.MoveWorkers(
            session.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL));
        Assert.Equal(1UL, session.Revision);
        Assert.Equal(8UL, session.TotalWorkers);
    }

    [Fact]
    public void LegacyDemoTileIdentityCannotEnterProductionGodotHierarchy()
    {
        var session = new EconomicPhysicalTacticalWorkerSession(
            20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        var wrongIdentity = new PhysicalTacticalTileId(
            new GoldbergParameters(12, 0), new StrategicCellId(1UL));
        Assert.Throws<ArgumentException>(() => session.DisplayBucket(wrongIdentity));
    }
}
