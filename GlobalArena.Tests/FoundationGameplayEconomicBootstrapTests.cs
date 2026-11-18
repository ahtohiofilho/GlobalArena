using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// A production configuration MUST be authored explicitly by a trusted
/// gameplay owner; numeric QA examples must never silently activate the
/// v15 economy of the ordinary Godot scene.
/// </summary>
public sealed class FoundationGameplayEconomicBootstrapTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);

    private static FoundationTacticalHumanAiTurnHost Preview() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);

    private static FoundationGameplayEconomicConfiguration Configuration(
        bool includeBlueOpening = true) =>
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
                    new[] { One, Two },
                    Enum.GetValues<FoundationPhysicalDemandProfile>()
                        .SelectMany(p => new[] { One, Two }.Select(c =>
                            new FoundationActivityCommodityDemandCoefficient(
                                p, c, 1_000_000UL)))),
                new FoundationCommodityCargoConversionPolicy(
                    "explicit-test-only-cargo",
                    new[]
                    {
                        new FoundationCommodityCargoConversion(
                            One, 1_000_000UL),
                        new FoundationCommodityCargoConversion(
                            Two, 1_000_000UL)
                    }),
                new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                }),
            includeBlueOpening
                ? new[] { (Red, 0UL), (Blue, 0UL) }
                : new[] { (Red, 0UL) });

    [Fact]
    public void ExplicitPolicyOptInPreservesRealWorkersAndTurnsOnSettledIncome()
    {
        var plain = Preview();
        var before = plain.Snapshot;
        Assert.Equal(11U, plain.StateHash.FormatVersion);
        Assert.False(before.Transport.IsInitialized);
        Assert.False(before.Treasury.IsInitialized);
        var host = FoundationGameplayEconomicBootstrap.Enable(
            plain, Configuration());
        Assert.Same(before.Workers, host.Snapshot.Workers);
        Assert.Equal(13U, host.StateHash.FormatVersion);
        Assert.True(host.Snapshot.Transport.HasMonetaryConstructionCosts);
        Assert.True(host.Snapshot.Treasury.IsInitialized);
        Assert.Equal(BigInteger.Zero,
            host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(BigInteger.Zero,
            host.Snapshot.Treasury.Get(Blue).CashScaled);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(1UL, host.NextTurn.Value);
        host.SetHumanReady(Red);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.NotNull(receipt!.EconomicSettlement);
        Assert.Equal(15U, host.StateHash.FormatVersion);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(1UL,
            host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(host.Snapshot.Treasury.Get(Red).CashScaled > 0);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset,
            host.GetLastClosedEconomicTradeView(Red).Availability);
        // Existing deterministic AI can resume its REAL traveling worker
        // during the first collective turn. That is a valid worker-event
        // mutation; bootstrap must still preserve each worker's identity,
        // physical node, capacity and location-linked demand.
        Assert.Equal(
            before.Workers.Workers.Select(w =>
                (w.Id, w.Owner, w.TacticalTileId, w.OutputMicroPerTurn,
                    w.DemandMultiplierMicro)),
            host.Snapshot.Workers.Workers.Select(w =>
                (w.Id, w.Owner, w.TacticalTileId, w.OutputMicroPerTurn,
                    w.DemandMultiplierMicro)));
    }

    [Fact]
    public void MissingCivilizationBalanceFailsWithoutChangingSession()
    {
        var plain = Preview();
        var before = plain.Snapshot;
        var digest = plain.StateHash;
        Assert.Throws<InvalidOperationException>(() =>
            FoundationGameplayEconomicBootstrap.Enable(
                plain, Configuration(includeBlueOpening: false)));
        Assert.Same(before, plain.Snapshot);
        Assert.Equal(digest, plain.StateHash);
        Assert.Equal(11U, plain.StateHash.FormatVersion);
    }

    [Fact]
    public void BootstrapCannotReinitializeTreasuryOrRepeatAfterTurnStarts()
    {
        var plain = Preview();
        var pack = Configuration();
        var activated = FoundationGameplayEconomicBootstrap.Enable(
            plain, pack);
        var initial = activated.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            FoundationGameplayEconomicBootstrap.Enable(activated, pack));
        Assert.Same(initial, activated.Snapshot);
        activated.SetHumanReady(Red);
        Assert.NotNull(activated.TryCloseTurn());
        Assert.Throws<InvalidOperationException>(() =>
            FoundationGameplayEconomicBootstrap.Enable(activated, pack));
    }
}
