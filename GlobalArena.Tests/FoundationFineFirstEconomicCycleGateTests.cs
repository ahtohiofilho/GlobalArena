using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFirstEconomicCycleGateTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationGameplayEconomicConfiguration Policy()
    {
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { Grain, Wood },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "r85-g18-real-cargo", new[]
            {
                new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
            });
        return new FoundationGameplayEconomicConfiguration(
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL),
            new FoundationAuthoritativeEconomicSettlementPolicy(
                demand, cargo, new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                }, maxProducerPasses: 64),
            new[] { (Red, 0UL), (Blue, 0UL) });
    }

    [Fact]
    public void FreshG18EconomyClosesTwoRealCyclesWithExactReplayAndNoV1Identity()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var source = host.Snapshot;
        var originalHash = host.StateHash;
        var world = host.Session.GeneratedWorld;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            world, host.Incidence);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, host.Incidence, fine);
        var config = Policy();
        var opened = FoundationFineFirstEconomicCycleGate.OpenNewEconomy(
            source, world, host.Incidence, fine, signature, config);
        Assert.True(opened.WorldBinding!.IsFineFirstWorld);
        Assert.True(opened.Treasury.IsInitialized);
        Assert.Equal(fine.FineElevationRaw.Count,
            host.Incidence.TileIncidences.Count);
        Assert.NotEqual(originalHash,
            new CanonicalWorldStateHasher().Compute(opened));
        Assert.NotEqual(
            FoundationPhysicalNaturalTransportSeeder.Create(
                world, host.Incidence, config.OperatingTransport)
            .LandTileCount,
            fine.LandTileCount);

        var context1 = new SimulationContext(
            new TurnNumber(1UL), new SimulationSeed(712UL));
        var first = FoundationFineFirstEconomicCycleGate.CloseAndReplay(
            opened, world, host.Incidence, fine, signature,
            config.Settlement, context1);
        Assert.True(first.ReplayMatched);
        Assert.Equal(1UL, first.Event.EconomicCycleOrdinal);
        Assert.True(first.Event.ProducedGoodsMicro > 0UL);
        Assert.Equal(first.Event.ProducedGoodsMicro,
            first.Event.SoldAndConsumedGoodsMicro);
        Assert.Equal(first.Event.GrossSalesScaled,
            first.Event.PhysicalFreightScaled +
            first.Event.CreditedToTreasuriesScaled);
        Assert.Equal(1UL,
            first.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(signature.Sha256Hex,
            first.Next.WorldBinding!.WorldSignatureSha256Hex);

        var context2 = new SimulationContext(
            new TurnNumber(2UL), new SimulationSeed(713UL));
        var second = FoundationFineFirstEconomicCycleGate.CloseAndReplay(
            first.Next, world, host.Incidence, fine, signature,
            config.Settlement, context2);
        Assert.Equal(2UL, second.Event.EconomicCycleOrdinal);
        Assert.Equal(2UL,
            second.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(second.Event.ProducedGoodsMicro > 0UL);
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.Equal(source, host.Snapshot);
        Assert.Equal(originalHash, host.StateHash);

        var legacyExecutor = new FoundationEconomicSalesSettlementEventExecutor(
            world, host.Incidence, config.Settlement);
        Assert.Throws<InvalidOperationException>(() =>
            legacyExecutor.Execute(opened, first.Event, context1));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstEconomicCycleGate.OpenNewEconomy(
                opened, world, host.Incidence, fine, signature, config));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFineFirstEconomicCycleGate.CloseAndReplay(
                first.Next, world, host.Incidence, fine,
                signature with { Sha256Hex = new string('0', 64) },
                config.Settlement, context2));
    }
}
