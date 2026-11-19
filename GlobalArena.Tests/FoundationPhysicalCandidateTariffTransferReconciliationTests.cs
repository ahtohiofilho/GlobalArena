using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalCandidateTariffTransferReconciliationTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CivilizationId Green = new(3UL);

    private static PhysicalTacticalEdgeId Edge(ulong number) =>
        new(new GoldbergParameters(2, 0), new StrategicEdgeId(number));

    private static CivilizationTreasuryRuntimeState Treasury() =>
        CivilizationTreasuryRuntimeState.CreateOpening(new[]
        {
            (Red, 50_000_000UL), (Blue, 0UL), (Green, 1_000_000UL)
        });

    [Fact]
    public void TwoWayTollsPreserveAllSalesCreditsAndNeverCreateMoney()
    {
        var treasury = Treasury();
        var redBefore = treasury.Get(Red);
        var blueBefore = treasury.Get(Blue);
        var gross = new Dictionary<CivilizationId, BigInteger>
        {
            [Red] = GlobalArenaMoneyScale.FromMoneyMicro(40_000UL),
            [Blue] = GlobalArenaMoneyScale.FromMoneyMicro(10_000UL),
            [Green] = BigInteger.Zero
        };
        var proposed = new[]
        {
            new FoundationPhysicalCandidateTariffTransferReconciliation.CandidateDebit(
                Edge(1UL), Blue, Red, 3_000_000UL, 2_000UL),
            new FoundationPhysicalCandidateTariffTransferReconciliation.CandidateDebit(
                Edge(2UL), Red, Green, 2_000_000UL, 1_000UL)
        };
        var evidence = FoundationPhysicalCandidateTariffTransferReconciliation
            .Reconcile(treasury, gross, proposed);
        var red = evidence.Accounts.Single(x => x.Civilization == Red);
        var blue = evidence.Accounts.Single(x => x.Civilization == Blue);
        var green = evidence.Accounts.Single(x => x.Civilization == Green);

        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(8_000UL),
            evidence.TotalTollTransferredScaled);
        Assert.Equal(evidence.TotalGrossSalesScaled,
            evidence.TotalNetCandidateCreditsScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(40_000UL),
            red.GrossSalesCreditScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(2_000UL),
            red.TollOutflowScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(6_000UL),
            red.TollInflowScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(44_000UL),
            red.NetCandidateCreditScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(4_000UL),
            blue.NetCandidateCreditScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(2_000UL),
            green.NetCandidateCreditScaled);
        Assert.Equal(redBefore, treasury.Get(Red));
        Assert.Equal(blueBefore, treasury.Get(Blue));
        Assert.Equal(evidence, FoundationPhysicalCandidateTariffTransferReconciliation
            .Reconcile(treasury, gross, proposed));
    }

    [Fact]
    public void RejectsDoubleTollUnknownOwnerSelfChargeAndUnfundedSeller()
    {
        var treasury = Treasury();
        var gross = new Dictionary<CivilizationId, BigInteger>
        {
            [Red] = BigInteger.Zero,
            [Blue] = GlobalArenaMoneyScale.FromMoneyMicro(2_000UL),
            [Green] = BigInteger.Zero
        };
        var ordinary = new FoundationPhysicalCandidateTariffTransferReconciliation
            .CandidateDebit(Edge(1UL), Blue, Red, 1_000_000UL, 1_000UL);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                treasury, gross, new[] { ordinary, ordinary }));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                treasury, gross, new[]
                {
                    ordinary with { Owner = Blue }
                }));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                treasury, gross, new[]
                {
                    ordinary with { Owner = new CivilizationId(99UL) }
                }));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                treasury, gross, new[]
                {
                    ordinary with { TariffMicroPerWholeCargoUnit = 3_000UL }
                }));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                treasury, new Dictionary<CivilizationId, BigInteger>
                {
                    [Red] = BigInteger.Zero, [Blue] = BigInteger.Zero
                }, new[] { ordinary }));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                treasury, gross, new[]
                {
                    ordinary with { CargoMicro = 0UL }
                }));
    }

    [Fact]
    public void SubMicroCargoRoundingIsDeterministicAndZeroTariffCandidateIsForbidden()
    {
        var gross = new Dictionary<CivilizationId, BigInteger>
        {
            [Red] = BigInteger.Zero, [Blue] = BigInteger.One,
            [Green] = BigInteger.Zero
        };
        var microToll = new FoundationPhysicalCandidateTariffTransferReconciliation
            .CandidateDebit(Edge(1UL), Blue, Red, 1UL, 1UL);
        var evidence = FoundationPhysicalCandidateTariffTransferReconciliation
            .Reconcile(Treasury(), gross, new[] { microToll });
        Assert.Equal(BigInteger.Zero, evidence.TotalTollTransferredScaled);
        Assert.Equal(BigInteger.One, evidence.TotalNetCandidateCreditsScaled);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalCandidateTariffTransferReconciliation.Reconcile(
                Treasury(), gross, new[]
                {
                    microToll with { TariffMicroPerWholeCargoUnit = 0UL }
                }));
    }
}
