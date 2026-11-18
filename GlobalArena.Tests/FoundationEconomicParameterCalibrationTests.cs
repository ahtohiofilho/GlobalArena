using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Risk-controlled economic-parameter decisions. No test numbers are promoted
/// as production game balance, no precommit forecasts become spendable cash,
/// and different policy signatures must be observable even when the same
/// canonical WorldState hash is obtained.
/// </summary>
public sealed class FoundationEconomicParameterCalibrationTests
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
        uint redTaxBasisPoints = 1_000U,
        ulong agriculturalOneDemandMicro = 1_000_000UL,
        ulong commodityOneCargoMicro = 1_000_000UL,
        ulong landOperatingMicro = 1_000UL,
        ulong landCapitalMicro = 2_000_000UL,
        ulong blueOpeningMoneyMicro = 0UL) =>
        new(
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    landOperatingMicro, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                landCapitalMicro, 4_000_000UL, 6_000_000UL, 1_000_000UL),
            new FoundationAuthoritativeEconomicSettlementPolicy(
                new FoundationActivityCommodityDemandPolicy(
                    new[] { One, Two },
                    Enum.GetValues<FoundationPhysicalDemandProfile>()
                        .SelectMany(p => new[] { One, Two }.Select(c =>
                            new FoundationActivityCommodityDemandCoefficient(
                                p, c,
                                p == FoundationPhysicalDemandProfile.AgricultureProduction
                                    && c == One
                                    ? agriculturalOneDemandMicro
                                    : 1_000_000UL)))),
                new FoundationCommodityCargoConversionPolicy(
                    "ga-calibration-physical-cargo-test-only",
                    new[]
                    {
                        new FoundationCommodityCargoConversion(
                            One, commodityOneCargoMicro),
                        new FoundationCommodityCargoConversion(
                            Two, 1_000_000UL)
                    }),
                new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red,
                        redTaxBasisPoints),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                }),
            new[]
            {
                (Red, 0UL),
                (Blue, blueOpeningMoneyMicro)
            });

    [Fact]
    public void SameDeclaredPolicyAndWorkersProduceStableExactReadOnlyCalibration()
    {
        var host = Preview();
        var originalWorld = host.Snapshot;
        var originalHash = host.StateHash;
        var policy = Configuration();
        var first = FoundationEconomicParameterCalibration.Preview(
            originalWorld, host.Session.GeneratedWorld, host.Incidence,
            policy, "first");
        var second = FoundationEconomicParameterCalibration.Preview(
            originalWorld, host.Session.GeneratedWorld, host.Incidence,
            policy, "second");

        Assert.Equal(64, first.PolicyFingerprintSha256.Length);
        Assert.Equal(first.PolicyFingerprintSha256,
            second.PolicyFingerprintSha256);
        Assert.Equal(first.SourceWorldHash, second.SourceWorldHash);
        Assert.Equal(first.CandidateWorldHash, second.CandidateWorldHash);
        Assert.Equal(first.GrossSalesScaled, second.GrossSalesScaled);
        Assert.Equal(first.PhysicalFreightScaled, second.PhysicalFreightScaled);
        Assert.Equal(first.SellerTaxesScaled, second.SellerTaxesScaled);
        Assert.Equal(first.EdgeTraversalCargoMicro,
            second.EdgeTraversalCargoMicro);
        Assert.True(first.WouldSettleAllProducedGoods);
        Assert.True(first.ProducedGoodsMicro > 0UL);
        Assert.Equal(first.ProducedGoodsMicro,
            first.AllocatedGoodsMicro);
        Assert.True(first.GrossSalesScaled > BigInteger.Zero);
        Assert.Equal(first.GrossSalesScaled,
            first.PhysicalFreightScaled +
            first.ExternalTollsScaled +
            first.SellerTaxesScaled + first.SellerNetScaled);
        Assert.Equal(first.GrossSalesScaled,
            first.PhysicalFreightScaled +
            first.HypotheticalTreasuryCreditsScaled);
        Assert.False(first.IsCommitted);
        Assert.Equal(originalHash, host.StateHash);
        Assert.Same(originalWorld, host.Snapshot);
        Assert.False(host.Snapshot.Treasury.IsInitialized);
        Assert.Equal(11U, host.StateHash.FormatVersion);
        Assert.Equal(1UL, host.NextTurn.Value);
        Assert.Empty(host.ReadyCivilizations);
    }

    [Fact]
    public void PostRevenueTaxOnlyChangesNetAndTaxNotSalesOrCombinedCash()
    {
        var host = Preview();
        var a = Configuration(redTaxBasisPoints: 1_000U);
        var b = Configuration(redTaxBasisPoints: 2_000U);
        var compare = FoundationEconomicParameterCalibration.Compare(
            host.Snapshot, host.Session.GeneratedWorld, host.Incidence,
            a, b);

        Assert.NotEqual(compare.Baseline.PolicyFingerprintSha256,
            compare.Alternative.PolicyFingerprintSha256);
        // The canonical WorldState does not yet persist the selected
        // POLICY fingerprint: equal world hashes are not evidence of
        // identical demand/tax rules. Governance must track BOTH.
        Assert.Equal(compare.Baseline.CandidateWorldHash,
            compare.Alternative.CandidateWorldHash);
        Assert.Equal(BigInteger.Zero, compare.DemandMicroDelta);
        Assert.Equal(BigInteger.Zero, compare.AllocatedGoodsMicroDelta);
        Assert.Equal(BigInteger.Zero,
            compare.EdgeTraversalCargoMicroDelta);
        Assert.Equal(BigInteger.Zero, compare.GrossSalesScaledDelta);
        Assert.Equal(BigInteger.Zero, compare.PhysicalFreightScaledDelta);
        Assert.Equal(BigInteger.Zero,
            compare.HypotheticalTreasuryCreditsScaledDelta);
        Assert.True(compare.SellerTaxesScaledDelta > BigInteger.Zero);
        var first = compare.Baseline.Civilizations.Single(
            row => row.Civilization == Red);
        var second = compare.Alternative.Civilizations.Single(
            row => row.Civilization == Red);
        Assert.Equal(first.HypotheticalTreasuryCreditScaled,
            second.HypotheticalTreasuryCreditScaled);
        Assert.True(second.SellerTaxScaled > first.SellerTaxScaled);
        Assert.True(second.SellerNetScaled < first.SellerNetScaled);
        Assert.False(compare.IsCommitted);
    }

    [Fact]
    public void RealDemandVariationChangesDemandAndFingerprintNotWorkers()
    {
        var host = Preview();
        var before = host.StateHash;
        var comparison = FoundationEconomicParameterCalibration.Compare(
            host.Snapshot, host.Session.GeneratedWorld, host.Incidence,
            Configuration(),
            Configuration(agriculturalOneDemandMicro: 2_000_000UL));

        Assert.True(comparison.DemandMicroDelta > BigInteger.Zero);
        Assert.Equal(BigInteger.Zero, comparison.AllocatedGoodsMicroDelta);
        Assert.NotEqual(
            comparison.Baseline.PolicyFingerprintSha256,
            comparison.Alternative.PolicyFingerprintSha256);
        Assert.Equal(comparison.Baseline.CandidateWorldHash,
            comparison.Alternative.CandidateWorldHash);
        Assert.True(comparison.Baseline.WouldSettleAllProducedGoods);
        Assert.True(comparison.Alternative.WouldSettleAllProducedGoods);
        Assert.Equal(before, host.StateHash);
        Assert.False(host.Snapshot.Treasury.IsInitialized);
    }

    [Fact]
    public void CargoAndCapitalCoefficientsRemainSeparateDecisionLevers()
    {
        var host = Preview();
        var origin = host.StateHash;
        var cargo = FoundationEconomicParameterCalibration.Compare(
            host.Snapshot, host.Session.GeneratedWorld, host.Incidence,
            Configuration(),
            Configuration(commodityOneCargoMicro: 2_000_000UL));
        Assert.NotEqual(cargo.Baseline.PolicyFingerprintSha256,
            cargo.Alternative.PolicyFingerprintSha256);
        Assert.Equal(cargo.Baseline.CandidateWorldHash,
            cargo.Alternative.CandidateWorldHash);
        Assert.Equal(BigInteger.Zero, cargo.DemandMicroDelta);
        // Higher cargo equivalence may alter routing. Never assert a
        // phantom shipment exists in a same-locality scenario.

        var capex = FoundationEconomicParameterCalibration.Compare(
            host.Snapshot, host.Session.GeneratedWorld, host.Incidence,
            Configuration(), Configuration(landCapitalMicro: 4_000_000UL));
        Assert.NotEqual(capex.Baseline.PolicyFingerprintSha256,
            capex.Alternative.PolicyFingerprintSha256);
        Assert.NotEqual(capex.Baseline.CandidateWorldHash,
            capex.Alternative.CandidateWorldHash);
        Assert.Equal(BigInteger.Zero, capex.GrossSalesScaledDelta);
        Assert.Equal(BigInteger.Zero, capex.PhysicalFreightScaledDelta);
        Assert.Equal(BigInteger.Zero, capex.DemandMicroDelta);
        Assert.Equal(origin, host.StateHash);
    }

    [Fact]
    public void IncomparableCurrencyAndOpeningCapitalAreRejected()
    {
        var host = Preview();
        var original = host.Snapshot;
        var originalHash = host.StateHash;
        Assert.Throws<InvalidOperationException>(() =>
            FoundationEconomicParameterCalibration.Compare(
                original, host.Session.GeneratedWorld, host.Incidence,
                Configuration(),
                Configuration(blueOpeningMoneyMicro: 1UL)));
        Assert.Throws<ArgumentException>(() =>
            FoundationEconomicParameterCalibration.Preview(
                original, host.Session.GeneratedWorld, host.Incidence,
                Configuration(), ""));
        Assert.Equal(originalHash, host.StateHash);
        Assert.Same(original, host.Snapshot);
    }
}
