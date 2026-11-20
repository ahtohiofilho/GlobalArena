using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFrozenGroundMovementTests
{
    [Fact]
    public void SignedG18WorkersTraverseFirmIceButNeverOpenWater()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var world = demo.Session.GeneratedWorld;
        var map = demo.Incidence;
        var fine = FoundationFineFirstPhysicalGeography.Generate(world, map);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, map, fine);
        var resolver = new PhysicalTacticalWorkerMoveEventExecutor(
            world, map, fine, identity);
        var legacy = new PhysicalTacticalWorkerMoveEventExecutor(world, map);
        int permitted = 0, rejected = 0;
        int frozen = 0;
        foreach (var edge in map.FineTopology.Edges)
        {
            var from = edge.IncidentCellIds[0];
            var to = edge.IncidentCellIds[1];
            var a = fine.SurfaceAtIndex(checked((int)from.Value - 1));
            var b = fine.SurfaceAtIndex(checked((int)to.Value - 1));
            var origin = new PhysicalTacticalTileId(map.Refinement.FineParameters, from);
            var destination = new PhysicalTacticalTileId(map.Refinement.FineParameters, to);
            Assert.Equal(new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                edge.Id), legacy.CheckEdge(origin, destination));
            if (a == FoundationFineSurfaceKind.FirmSeaIce ||
                b == FoundationFineSurfaceKind.FirmSeaIce) frozen++;
            if (a == FoundationFineSurfaceKind.OpenWater ||
                b == FoundationFineSurfaceKind.OpenWater)
            {
                Assert.Throws<InvalidOperationException>(() =>
                    resolver.CheckEdge(origin, destination));
                rejected++;
            }
            else
            {
                Assert.Equal(new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                    edge.Id), resolver.CheckEdge(origin, destination));
                permitted++;
            }
        }
        Assert.True(frozen > 0);
        Assert.True(permitted > 0 && rejected > 0);
        Assert.Equal(map.FineTopology.Edges.Count, permitted + rejected);
    }
}
