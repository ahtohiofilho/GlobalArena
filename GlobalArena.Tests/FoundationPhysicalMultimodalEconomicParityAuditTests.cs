using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// R71 policy-mismatch regression. It protects the settlement boundary
/// against silently treating the opt-in R55 physical experiment as
/// the existing seller-specific authoritative marginal-freight schedule.
/// </summary>
public sealed class FoundationPhysicalMultimodalEconomicParityAuditTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private static (FoundationTacticalHumanAiTurnHost Host, WorldState Snapshot)
        Prepare()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var snapshot = host.Snapshot.DeployInitialPhysicalTransport(
                seeded.Transport, host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 10_000_000_000_000UL),
                    (Blue, 10_000_000_000_000UL)
                }));
        return (host, snapshot);
    }

    private static FoundationPhysicalMultimodalEconomicParityAudit.Evidence
        Compare(FoundationTacticalHumanAiTurnHost host, WorldState state)
    {
        var fine = host.Incidence.Refinement.FineParameters;
        return FoundationPhysicalMultimodalEconomicParityAudit.Compare(
            state, host.Session.GeneratedWorld, host.Incidence, Red,
            new PhysicalTacticalTileId(fine, new StrategicCellId(16UL)),
            new PhysicalTacticalTileId(fine, new StrategicCellId(27UL)));
    }

    [Fact]
    public void FreeFlowResearchAndEconomyLandCostsCoincideButAreNotInterchangeable()
    {
        var (host, opening) = Prepare();
        var oldHash = host.StateHash;
        var report = Compare(host, opening);
        Assert.Equal(report.ExperimentalLandOnlyMicroPerWholeCargo,
            report.AuthoritativeSellerLandOnlyMicroPerWholeCargo);
        Assert.Equal(0L, report.DifferentlyPricedPhysicalEdges);
        Assert.Equal(0L, report.SellerThirdPartyTariffEdges);
        Assert.True(report.AuthoritativeCoastalCrossings > 0);
        Assert.False(report.CanSafelyReplaceEconomicRouting);
        Assert.Equal(report, Compare(host, opening));
        Assert.Equal(oldHash, host.StateHash);
    }

    [Fact]
    public void RealCoastalInvestmentExposesDifferentRatePoliciesWithoutChangingMarket()
    {
        var (host, opening) = Prepare();
        var fields = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var land = host.Incidence.TileIncidences.Select(tile =>
        {
            long sum = 0;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                sum = checked(sum + fields.Elevation.GetRawValue(coarse));
            return sum / tile.IncidentCoarseCellIds.Count >
                fields.SeaLevel.RawValue;
        }).ToArray();
        var coastEdge = host.Incidence.FineTopology.Edges.First(edge =>
            land[checked((int)edge.IncidentCellIds[0].Value - 1)] !=
            land[checked((int)edge.IncidentCellIds[1].Value - 1)]);
        var id = new PhysicalTacticalEdgeId(
            host.Incidence.Refinement.FineParameters, coastEdge.Id);
        var infrastructure = opening.Transport.Edges.Single(
            record => record.Edge == id);
        var improved = opening.RecordMonetaryInfrastructureInvestment(
            Red, id,
            checked(infrastructure.NaturalConstructionCostMoneyMicro!.Value *
                16UL));
        var before = Compare(host, opening);
        var after = Compare(host, improved);
        Assert.Equal(0L, before.DifferentlyPricedPhysicalEdges);
        Assert.True(after.DifferentlyPricedPhysicalEdges > 0);
        Assert.True(after.ExperimentalPhysicalCostDrifts > 0);
        Assert.False(after.CanSafelyReplaceEconomicRouting);
        Assert.Equal(before.AuthoritativeSellerLandOnlyMicroPerWholeCargo,
            after.AuthoritativeSellerLandOnlyMicroPerWholeCargo);
        Assert.Equal(before, Compare(host, opening));
        Assert.Equal(after, Compare(host, improved));
    }
}
