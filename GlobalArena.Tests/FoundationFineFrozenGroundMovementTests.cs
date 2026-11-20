using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFrozenGroundMovementTests
{
    [Fact]
    public void FreightCrossingKindIsSymmetricAndBlocksIceToOpenSea()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            demo.Session.GeneratedWorld, demo.Incidence);
        int ground = 0, sea = 0, port = 0, iceBoundary = 0;
        foreach (var edge in demo.Incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            var crossing = fine.CrossingAtIndices(a, b);
            Assert.Equal(crossing, fine.CrossingAtIndices(b, a));
            var first = fine.SurfaceAtIndex(a);
            var second = fine.SurfaceAtIndex(b);
            switch (crossing)
            {
                case FoundationFineTransportCrossingKind.Ground:
                    Assert.NotEqual(FoundationFineSurfaceKind.OpenWater, first);
                    Assert.NotEqual(FoundationFineSurfaceKind.OpenWater, second);
                    ground++;
                    break;
                case FoundationFineTransportCrossingKind.OpenSea:
                    Assert.Equal(FoundationFineSurfaceKind.OpenWater, first);
                    Assert.Equal(FoundationFineSurfaceKind.OpenWater, second);
                    sea++;
                    break;
                case FoundationFineTransportCrossingKind.Port:
                    Assert.Contains(FoundationFineSurfaceKind.Land, new[] { first, second });
                    Assert.Contains(FoundationFineSurfaceKind.OpenWater, new[] { first, second });
                    port++;
                    break;
                case FoundationFineTransportCrossingKind.BlockedIceSeaBoundary:
                    Assert.Contains(FoundationFineSurfaceKind.FirmSeaIce,
                        new[] { first, second });
                    Assert.Contains(FoundationFineSurfaceKind.OpenWater,
                        new[] { first, second });
                    iceBoundary++;
                    break;
                default:
                    throw new InvalidOperationException("Unknown crossing surface.");
            }
        }
        Assert.True(ground > 0 && sea > 0 && port > 0 && iceBoundary > 0);
        Assert.Equal(demo.Incidence.FineTopology.Edges.Count,
            ground + sea + port + iceBoundary);
    }

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
