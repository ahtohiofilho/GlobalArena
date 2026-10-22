using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalPrefetchGeometryAvailabilityTests
{
    [Fact]
    public void EveryDirectedAdjacentTransferCanPrepareUnprojectedCanonicalTiles()
    {
        var coarse = new GoldbergParameters(3, 0);
        var fine = new GoldbergParameters(18, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(coarse, fine));
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, fine));
        var geometry = world.StrategicSphericalGeometry;
        Assert.Equal(3242, geometry.Cells.Count);
        Assert.Equal(92, map.CoarseTopology.Cells.Count);
        var transitions = 0;
        foreach (var source in map.CoarseTopology.Cells)
        {
            var current = PhysicalTacticalViewportQuery.SelectTiles(map, source.Id, 3).ToHashSet();
            var prepared = new HashSet<PhysicalTacticalTileId>();
            foreach (var destination in source.AdjacentCellIds)
            {
                var plan = PhysicalTacticalResidentTransferPlanner.PlanAdjacent(
                    map, source.Id, destination, 3);
                Assert.Equal(current.Count, plan.SourceResident.Count);
                Assert.All(plan.ToPrepare, id => Assert.DoesNotContain(id, current));
                Assert.True(plan.ToPrepare.Count <= 218);
                foreach (var tile in plan.ToPrepare)
                {
                    var cell = geometry.GetCell(tile.FineStrategicCellId);
                    Assert.True(cell.Center.IsValid);
                    Assert.InRange(cell.BoundaryVertexIds.Count, 5, 6);
                    foreach (var vertex in cell.BoundaryVertexIds)
                        Assert.True(geometry.GetVertex(vertex).Position.IsValid);
                    prepared.Add(tile);
                }
                transitions++;
            }
            Assert.InRange(prepared.Count, 1, 3242);
        }
        Assert.Equal(540, transitions);
    }
}
