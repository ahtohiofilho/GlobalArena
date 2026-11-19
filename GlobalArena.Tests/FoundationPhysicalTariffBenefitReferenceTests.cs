using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalTariffBenefitReferenceTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    [Fact]
    public void MonetaryInvestmentBenefitIsReadOnlySellerSpecificAndNeverAnAutomaticToll()
    {
        var preview = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)),
            workersPerCivilization: 3);
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            preview.Session.GeneratedWorld, preview.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var state = preview.Snapshot.DeployInitialPhysicalTransport(
                seed.Transport, preview.Session.GeneratedWorld, preview.Incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 1_000_000_000_000UL),
                        (Blue, 1_000_000_000_000UL) }));
        var coast = state.Transport.Edges.First(edge =>
            edge.CargoMicroAtReferenceLoad == 500_000UL);

        var natural = FoundationPhysicalTariffBenefitReference.Evaluate(
            coast, Blue, 500_000UL, 1U, 2U);
        Assert.Equal(0UL, natural.ProposedExternalTariffMicro);
        Assert.Equal(0UL, natural.SellerPayableTariffMicro);
        Assert.Equal(0UL, natural.AvoidedPhysicalCostMicro);

        var invested = state.RecordMonetaryInfrastructureInvestment(
            Red, coast.Edge,
            checked(coast.NaturalConstructionCostMoneyMicro!.Value * 32UL));
        var funded = invested.Transport.Edges.Single(x => x.Edge == coast.Edge);
        Assert.Equal(Red, funded.InfrastructureOwner);
        Assert.Equal(0UL, funded.ExternalTariffMicroPerWholeCargoUnit);
        Assert.True(funded.CommittedInvestmentMoneyMicro > 0UL);

        var owner = FoundationPhysicalTariffBenefitReference.Evaluate(
            funded, Red, 500_000UL, 1U, 2U);
        var outsider = FoundationPhysicalTariffBenefitReference.Evaluate(
            funded, Blue, 500_000UL, 1U, 2U);
        Assert.True(owner.ImprovedCapacityMicro > owner.NaturalCapacityMicro);
        Assert.True(owner.AvoidedPhysicalCostMicro > 0UL);
        Assert.True(outsider.ProposedExternalTariffMicro > 0UL);
        Assert.Equal(0UL, owner.SellerPayableTariffMicro);
        Assert.Equal(outsider.ProposedExternalTariffMicro,
            outsider.SellerPayableTariffMicro);
        Assert.Equal(owner.ProposedExternalTariffMicro,
            outsider.ProposedExternalTariffMicro);
        Assert.Equal(outsider, FoundationPhysicalTariffBenefitReference.Evaluate(
            funded, Blue, 500_000UL, 1U, 2U));

        var zeroShare = FoundationPhysicalTariffBenefitReference.Evaluate(
            funded, Blue, 500_000UL, 0U, 1U);
        Assert.Equal(0UL, zeroShare.ProposedExternalTariffMicro);
        var fullShare = FoundationPhysicalTariffBenefitReference.Evaluate(
            funded, Blue, 500_000UL, 1U, 1U);
        Assert.Equal(fullShare.AvoidedPhysicalCostMicro,
            fullShare.ProposedExternalTariffMicro);
        Assert.True(fullShare.ProposedExternalTariffMicro >=
            outsider.ProposedExternalTariffMicro);
        Assert.Same(state.Transport, state.Transport);
        Assert.Equal(0UL, state.Transport.Edges.Single(
            x => x.Edge == coast.Edge).ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(0UL, funded.ExternalTariffMicroPerWholeCargoUnit);
    }

    [Fact]
    public void DeniesUnboundedOrInvalidTariffPolicyWithoutInventingCapital()
    {
        var id = new PhysicalTacticalEdgeId(
            new GoldbergParameters(2, 0), new StrategicEdgeId(1UL));
        var edge = new PhysicalTransportEdgeRuntimeRecord(
            id, PhysicalTransportOwnershipKind.NoInfrastructure,
            null, 1_000UL, ulong.MaxValue, 0UL);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalTariffBenefitReference.Evaluate(
                edge, Red, 100UL, 1U, 2U));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTariffBenefitReference.Evaluate(
                edge, Red, 100UL, 3U, 2U));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTariffBenefitReference.Evaluate(
                edge, Red, 0UL, 1U, 2U));
    }
}
