using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalAuditedSellerTollBindingTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain, Wood },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("audited-seller-cargo", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
            new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
        });

    private static (
        WorldState State,
        PhysicalTacticalIncidenceMap Incidence,
        FoundationActivityCommodityDemandPolicy Demand,
        FoundationCommodityCargoConversionPolicy Cargo) Setup()
    {
        var preview = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            preview.Session.GeneratedWorld, preview.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        return (preview.Snapshot.DeployInitialPhysicalTransport(
                seeded.Transport, preview.Session.GeneratedWorld,
                preview.Incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 1_000_000_000_000UL), (Blue, 0UL) })),
            preview.Incidence, Demand(), Cargo());
    }

    private static FoundationPhysicalLaggedFreightCyclePlan Plan(
        WorldState state, PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demand,
        FoundationCommodityCargoConversionPolicy cargo) =>
        FoundationPhysicalLaggedFreightCyclePlanner.CoordinateWorldCandidate(
            state, incidence, demand, cargo,
            maxProducerPasses: 64);

    [Fact]
    public void NoInvestmentsCreateNoTariffsEvenWhenRealFreightCrossesEdges()
    {
        var scene = Setup();
        var plan = Plan(scene.State, scene.Incidence,
            scene.Demand, scene.Cargo);
        Assert.True(plan.Distribution.AllocationAudit.Freight
            .TotalEdgeTraversalCargoMicro > 0UL);
        var hash = new CanonicalWorldStateHasher()
            .Compute(scene.State);
        var result = FoundationPhysicalAuditedSellerTollBinding.Inspect(
            scene.State, scene.Incidence, scene.Demand,
            scene.Cargo, plan, 1UL, 500_000UL, 1U, 2U);
        Assert.Empty(result.SellerEdgeCandidates);
        Assert.Equal(plan.Distribution.AllocationAudit
            .TotalAllocatedGoodsMicro, result.TotalAuditedGoodsMicro);
        Assert.Equal(plan.Distribution.AllocationAudit.Freight
            .TotalEdgeTraversalCargoMicro,
            result.TotalAuditedEdgeTraversalCargoMicro);
        Assert.Equal(hash, new CanonicalWorldStateHasher()
            .Compute(scene.State));
    }

    [Fact]
    public void OnlyRealSellerTraversalsOfFundedOwnerCrossingAreChargeable()
    {
        var scene = Setup();
        var natural = Plan(scene.State, scene.Incidence,
            scene.Demand, scene.Cargo);
        var usedByBlue = natural.Distribution.Candidates
            .Where(c => c.Seller == Blue && c.RouteTiles.Count > 1)
            .SelectMany(c => Enumerable.Range(1, c.RouteTiles.Count - 1)
                .Select(i => (
                    First: c.RouteTiles[i - 1].FineStrategicCellId.Value,
                    Second: c.RouteTiles[i].FineStrategicCellId.Value)))
            .ToHashSet();
        Assert.NotEmpty(usedByBlue);

        var edge = scene.Incidence.FineTopology.Edges
            .Select(e => (Geometry: e, A: e.IncidentCellIds[0].Value,
                B: e.IncidentCellIds[1].Value))
            .First(e => (usedByBlue.Contains((e.A, e.B)) ||
                usedByBlue.Contains((e.B, e.A))) &&
                scene.State.Transport.Edges.Single(row =>
                    row.Edge == new PhysicalTacticalEdgeId(
                        scene.Incidence.Refinement.FineParameters,
                        e.Geometry.Id)).CargoMicroAtReferenceLoad !=
                    ulong.MaxValue);
        var edgeId = new PhysicalTacticalEdgeId(
            scene.Incidence.Refinement.FineParameters, edge.Geometry.Id);
        var naturalEdge = scene.State.Transport.Edges
            .Single(e => e.Edge == edgeId);
        Assert.NotEqual(ulong.MaxValue,
            naturalEdge.CargoMicroAtReferenceLoad);
        var invested = scene.State.RecordMonetaryInfrastructureInvestment(
            Red, edgeId, checked(
                naturalEdge.NaturalConstructionCostMoneyMicro!.Value * 32UL));
        var plan = Plan(invested, scene.Incidence,
            scene.Demand, scene.Cargo);
        var result = FoundationPhysicalAuditedSellerTollBinding.Inspect(
            invested, scene.Incidence, scene.Demand, scene.Cargo,
            plan, 1UL, naturalEdge.CargoMicroAtReferenceLoad, 1U, 2U);
        var blueCrossingCargo = plan.Distribution.Candidates
            .Where(c => c.Seller == Blue)
            .Where(c => Enumerable.Range(1, c.RouteTiles.Count - 1)
                .Any(i =>
                {
                    var a = c.RouteTiles[i - 1].FineStrategicCellId.Value;
                    var b = c.RouteTiles[i].FineStrategicCellId.Value;
                    return (a == edge.A && b == edge.B) ||
                           (a == edge.B && b == edge.A);
                }))
            .Aggregate(0UL, (sum, c) => checked(sum +
                scene.Cargo.CargoMicro(c.Commodity, c.GoodsMicro)));
        if (blueCrossingCargo > 0UL)
        {
            var toll = Assert.Single(result.SellerEdgeCandidates);
            Assert.Equal(edgeId, toll.Edge);
            Assert.Equal(Blue, toll.Seller);
            Assert.Equal(Red, toll.Owner);
            Assert.Equal(blueCrossingCargo, toll.CargoMicro);
            Assert.True(toll.TariffMicroPerWholeCargoUnit > 0UL);
        }
        else
            Assert.Empty(result.SellerEdgeCandidates);
        Assert.Equal(0UL, invested.Transport.Edges
            .Single(e => e.Edge == edgeId)
            .ExternalTariffMicroPerWholeCargoUnit);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalAuditedSellerTollBinding.Inspect(
                invested, scene.Incidence, scene.Demand, scene.Cargo,
                plan, 2UL, naturalEdge.CargoMicroAtReferenceLoad, 1U, 2U));
    }
}
