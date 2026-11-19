using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFirstInteractiveTurnHostTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationGameplayEconomicConfiguration Configuration() =>
        new(
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL),
            new FoundationAuthoritativeEconomicSettlementPolicy(
                new FoundationActivityCommodityDemandPolicy(
                    new[] { Grain, Wood },
                    Enum.GetValues<FoundationPhysicalDemandProfile>()
                        .SelectMany(profile =>
                            new[] { Grain, Wood }.Select(item =>
                                new FoundationActivityCommodityDemandCoefficient(
                                    profile, item, 1_000_000UL)))),
                new FoundationCommodityCargoConversionPolicy(
                    "r86-signed-fine-cargo", new[]
                    {
                        new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                        new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
                    }),
                new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                }, maxProducerPasses: 64),
            new[] { (Red, 100_000_000UL), (Blue, 0UL) });

    [Fact]
    public void SignedG18HostClosesTwoAllReadyTurnsAndRejectsLegacyConstructor()
    {
        var legacy = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var generated = legacy.Session.GeneratedWorld;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            generated, legacy.Incidence);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            generated, legacy.Incidence, fine);
        var policy = Configuration();
        var opened = FoundationFineFirstEconomicCycleGate.OpenNewEconomy(
            legacy.Snapshot, generated, legacy.Incidence, fine,
            signature, policy);
        legacy.Session.AcceptSnapshot(opened);
        Assert.Throws<InvalidOperationException>(() =>
            new FoundationTacticalHumanAiTurnHost(
                legacy.Session, legacy.Incidence, new[] { Red },
                new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
                {
                    [Blue] = new FoundationTacticalAiHoldPolicy()
                }, enableMonetaryInfrastructure: true,
                settlementPolicy: policy.Settlement));
        var host = new FoundationTacticalHumanAiTurnHost(
            legacy.Session, legacy.Incidence, new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            }, enableMonetaryInfrastructure: true,
            settlementPolicy: policy.Settlement,
            fineGeography: fine, fineSignature: signature);
        var initialHash = host.StateHash;
        host.SetHumanReady(Red);
        var first = host.TryCloseTurn();
        Assert.NotNull(first);
        Assert.True(first!.ReplayMatched);
        Assert.NotNull(first.EconomicSettlement);
        Assert.Equal(1UL, first.EconomicSettlement!.EconomicCycleOrdinal);
        Assert.Equal(first.EconomicSettlement.ProducedGoodsMicro,
            first.EconomicSettlement.SoldAndConsumedGoodsMicro);
        Assert.True(first.EconomicSettlement.GrossSalesScaled > 0);
        Assert.NotEqual(initialHash, first.Hash);
        Assert.Equal(signature.Sha256Hex,
            host.Snapshot.WorldBinding!.WorldSignatureSha256Hex);

        host.SetHumanReady(Red);
        var second = host.TryCloseTurn();
        Assert.NotNull(second);
        Assert.True(second!.ReplayMatched);
        Assert.Equal(2UL, second.EconomicSettlement!.EconomicCycleOrdinal);
        Assert.Equal(2UL,
            host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(second.Hash, host.StateHash);
        Assert.True(host.Snapshot.Transport.IsInitialized);
    }
}
