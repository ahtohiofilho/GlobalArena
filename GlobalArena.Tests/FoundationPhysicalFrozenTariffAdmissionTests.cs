using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalFrozenTariffAdmissionTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationAuthoritativeEconomicSettlementPolicy Policy() =>
        new(
            new FoundationActivityCommodityDemandPolicy(
                new[] { Grain, Wood },
                Enum.GetValues<FoundationPhysicalDemandProfile>()
                    .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                        new FoundationActivityCommodityDemandCoefficient(
                            profile, item, 1_000_000UL)))),
            new FoundationCommodityCargoConversionPolicy(
                "authoritative-sales-cargo", new[]
                {
                    new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                    new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
                }),
            new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Blue, 500U)
            }, maxProducerPasses: 64);

    [Fact]
    public void RealClosedCycleQuotesPhysicalFreightOnceAndAddsProposedTariffOnce()
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
        var state = preview.Snapshot.DeployInitialPhysicalTransport(
                seeded.Transport, preview.Session.GeneratedWorld,
                preview.Incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 1_000_000_000_000UL), (Blue, 0UL) }));
        var coast = state.Transport.Edges.First(e =>
            e.CargoMicroAtReferenceLoad == 500_000UL);
        state = state.RecordMonetaryInfrastructureInvestment(
            Red, coast.Edge,
            checked(coast.NaturalConstructionCostMoneyMicro!.Value * 32UL));
        preview.Session.AcceptSnapshot(state);
        var policy = Policy();
        var host = new FoundationTacticalHumanAiTurnHost(
            preview.Session, preview.Incidence, new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            }, enableMonetaryInfrastructure: true,
            settlementPolicy: policy);
        host.SetHumanReady(Red);
        var closed = host.TryCloseTurn();
        Assert.NotNull(closed);
        Assert.True(closed!.ReplayMatched);
        Assert.Equal(1UL,
            host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);

        var before = host.Snapshot;
        var hash = host.StateHash;
        var owner = FoundationPhysicalFrozenTariffAdmission.Inspect(
            before, host.Incidence, policy.Cargo,
            coast.Edge, Red, 2UL, 500_000UL, 1U, 2U);
        var other = FoundationPhysicalFrozenTariffAdmission.Inspect(
            before, host.Incidence, policy.Cargo,
            coast.Edge, Blue, 2UL, 500_000UL, 1U, 2U);
        Assert.True(owner.UsedSettledCongestion);
        Assert.Equal(owner.FrozenPhysicalCostMicro,
            other.FrozenPhysicalCostMicro);
        Assert.Equal(owner.CarriedSurchargeMicro,
            other.CarriedSurchargeMicro);
        Assert.Equal(0UL, owner.SellerPayableTariffMicro);
        Assert.Equal((UInt128)owner.FrozenPhysicalCostMicro,
            owner.SellerTotalCostMicro);
        Assert.True(other.SellerPayableTariffMicro > 0UL);
        Assert.Equal(other.ProposedExternalTariffMicro,
            other.SellerPayableTariffMicro);
        Assert.Equal((UInt128)other.FrozenPhysicalCostMicro +
            other.SellerPayableTariffMicro, other.SellerTotalCostMicro);
        Assert.Equal((UInt128)other.SellerPayableTariffMicro,
            other.SellerTotalCostMicro - owner.SellerTotalCostMicro);
        Assert.Equal(other, FoundationPhysicalFrozenTariffAdmission.Inspect(
            before, host.Incidence, policy.Cargo,
            coast.Edge, Blue, 2UL, 500_000UL, 1U, 2U));
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(0UL, host.Snapshot.Transport.Edges.Single(
            x => x.Edge == coast.Edge).ExternalTariffMicroPerWholeCargoUnit);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalFrozenTariffAdmission.Inspect(
                before, host.Incidence, policy.Cargo,
                coast.Edge, Blue, 1UL, 500_000UL, 1U, 2U));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalFrozenTariffAdmission.Inspect(
                before, host.Incidence, policy.Cargo,
                coast.Edge, Blue, 3UL, 500_000UL, 1U, 2U));
        var wrongCargo = new FoundationCommodityCargoConversionPolicy(
            "wrong-unit", new[]
            {
                new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
            });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalFrozenTariffAdmission.Inspect(
                before, host.Incidence, wrongCargo,
                coast.Edge, Blue, 2UL, 500_000UL, 1U, 2U));
    }

    [Fact]
    public void ExistingTariffCannotStackWithProspectiveTariff()
    {
        var preview = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)),
            workersPerCivilization: 3);
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            preview.Session.GeneratedWorld, preview.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL));
        var coast = seeded.Transport.Edges.First(row =>
            row.CargoMicroAtReferenceLoad == 500_000UL);
        var withTariff = PhysicalTransportInfrastructureRuntimeState.CreateInitial(
            seeded.Transport.Edges.Select(row => row.Edge == coast.Edge
                ? row with
                {
                    OwnershipKind = PhysicalTransportOwnershipKind.Owned,
                    InfrastructureOwner = Red,
                    ExternalTariffMicroPerWholeCargoUnit = 200UL
                } : row));
        var state = preview.Snapshot.DeployInitialPhysicalTransport(
                withTariff, preview.Session.GeneratedWorld, preview.Incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Blue, 0UL) }));
        var conversion = new FoundationCommodityCargoConversionPolicy(
            "test-cargo", new[]
            {
                new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
            });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalFrozenTariffAdmission.Inspect(
                state, preview.Incidence, conversion,
                coast.Edge, Blue, 1UL, 500_000UL, 1U, 2U));
    }
}
