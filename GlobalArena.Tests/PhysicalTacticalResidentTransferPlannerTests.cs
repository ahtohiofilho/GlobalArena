using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalResidentTransferPlannerTests
{
    private static PhysicalTacticalIncidenceMap CreateMap() =>
        PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0), new GoldbergParameters(18, 0)));

    private static ulong[] Ids(IEnumerable<PhysicalTacticalTileId> tiles) =>
        tiles.Select(tile => tile.FineStrategicCellId.Value).ToArray();

    [Fact]
    public void All540AdjacentTransfersAreCompleteDeterministicAndIdentityPreserving()
    {
        var map = CreateMap();
        var transitions = 0;
        foreach (var cell in map.CoarseTopology.Cells)
        {
            foreach (var neighbor in cell.AdjacentCellIds)
            {
                transitions++;
                var plan = PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, cell.Id, neighbor, 3);
                var repeated = PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, cell.Id, neighbor, 3);
                var expectedSource = Ids(PhysicalTacticalViewportQuery.SelectTiles(map, cell.Id, 3));
                var expectedTarget = Ids(PhysicalTacticalViewportQuery.SelectTiles(map, neighbor, 3));
                Assert.Equal(expectedSource, Ids(plan.SourceResident));
                Assert.Equal(expectedTarget, Ids(plan.DestinationResident));
                Assert.Equal(expectedTarget, Ids(plan.Reused.Concat(plan.ToPrepare).OrderBy(t => t.FineStrategicCellId.Value)));
                Assert.Equal(expectedSource, Ids(plan.Reused.Concat(plan.EligibleForRetirement).OrderBy(t => t.FineStrategicCellId.Value)));
                Assert.Empty(plan.ToPrepare.Intersect(plan.Reused));
                Assert.Empty(plan.EligibleForRetirement.Intersect(plan.DestinationResident));
                Assert.Equal(Ids(plan.ToPrepare), Ids(repeated.ToPrepare));
                Assert.Equal(Ids(plan.Reused), Ids(repeated.Reused));
                Assert.True(plan.Reused.Count >= 979);
                Assert.True(plan.ToPrepare.Count <= 218);
                Assert.Equal(plan.Reused.Count, plan.Reused.Distinct().Count());
                Assert.Equal(Ids(plan.ToPrepare), Ids(plan.ToPrepare.OrderBy(t => t.FineStrategicCellId.Value)));
            }
        }
        Assert.Equal(92, map.CoarseTopology.Cells.Count);
        Assert.Equal(540, transitions);
    }

    [Fact]
    public void SameFocusHasNoTransferAndInputsAreGuarded()
    {
        var map = CreateMap();
        var focus = map.CoarseTopology.Cells[0].Id;
        var same = PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, focus, focus, 3);
        Assert.Empty(same.ToPrepare);
        Assert.Empty(same.EligibleForRetirement);
        Assert.Equal(Ids(same.SourceResident), Ids(same.Reused));
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalResidentTransferPlanner.PlanAdjacent(null!, focus, focus));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, focus, focus, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, focus, focus, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, focus, default));
        var distant = map.CoarseTopology.Cells.First(cell => cell.Id != focus && !map.CoarseTopology.Cells[0].AdjacentCellIds.Contains(cell.Id)).Id;
        Assert.Throws<ArgumentException>(() => PhysicalTacticalResidentTransferPlanner.PlanAdjacent(map, focus, distant));
        Assert.Throws<NotSupportedException>(() => ((IList<PhysicalTacticalTileId>)same.Reused).Clear());
    }
}