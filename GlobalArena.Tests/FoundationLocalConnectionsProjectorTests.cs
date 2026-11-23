using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationLocalConnectionsProjectorTests
{
    private static (FoundationTacticalHumanAiTurnHost Host, WorldState World)
        TransportWorld()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            demo.Session.GeneratedWorld, demo.Incidence);
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            demo.Session.GeneratedWorld, demo.Incidence, fine,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000UL, 1_000_000UL));
        var world = demo.Session.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, demo.Session.GeneratedWorld, demo.Incidence);
        return (demo, world);
    }

    [Fact]
    public void EveryCityNodeListsExactlyItsRealIncidentEdgesAndNoOthers()
    {
        var (host, snapshot) = TransportWorld();
        var incidence = host.Incidence;
        var nodeId = incidence.FineTopology.Edges[0].IncidentCellIds[0];
        var node = new PhysicalTacticalTileId(
            incidence.Refinement.FineParameters, nodeId);
        var originalHash = host.StateHash;

        var rows = FoundationLocalConnectionsProjector.Read(
            snapshot, incidence, node);
        var expected = incidence.FineTopology.Edges
            .Where(e => e.IncidentCellIds.Contains(nodeId))
            .ToArray();
        Assert.Equal(expected.Length, rows.Count);
        Assert.Equal(rows.Count, rows.Select(row => row.Edge).Distinct().Count());
        Assert.True(rows.Count >= 3);
        foreach (var row in rows)
        {
            var edge = Assert.Single(expected, e =>
                new PhysicalTacticalEdgeId(
                    incidence.Refinement.FineParameters, e.Id) == row.Edge);
            Assert.Contains(nodeId, edge.IncidentCellIds);
            Assert.Contains(row.Neighbor.FineStrategicCellId,
                edge.IncidentCellIds);
            Assert.NotEqual(node, row.Neighbor);
            var original = Assert.Single(snapshot.Transport.Edges,
                e => e.Edge == row.Edge);
            Assert.Equal(original.OwnershipKind, row.Ownership);
            Assert.Equal(original.InfrastructureOwner, row.InfrastructureOwner);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(original), row.UnitPhysicalFreightMicro);
            Assert.Equal(original.ExternalTariffMicroPerWholeCargoUnit,
                row.ExternalTariffMicro);
            Assert.Equal(original.CommittedInvestmentMoneyMicro.GetValueOrDefault(),
                row.InvestedMoneyMicro);
        }
        Assert.Equal(originalHash, host.StateHash);
    }

    [Fact]
    public void ForeignResolutionCannotMasqueradeAsLocalPhysicalLinks()
    {
        var (host, snapshot) = TransportWorld();
        var incident = host.Incidence;
        var foreign = new PhysicalTacticalTileId(
            incident.Refinement.CoarseParameters,
            incident.Refinement.CoarseParameters.StrategicCellCount > 0
                ? new StrategicCellId(1UL)
                : throw new InvalidOperationException());
        Assert.Throws<InvalidOperationException>(() =>
            FoundationLocalConnectionsProjector.Read(
                snapshot, incident, foreign));
    }
}
