using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Real worker output -> audited same-cycle sold-and-consumed goods ->
/// exact civilization cash -> optional NEXT-turn capital investment.
/// No buyer wallets, physical stocks or compulsory builder workers.
/// </summary>
public sealed class FoundationAuthoritativeEconomicSalesSettlementTests
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
                    .SelectMany(p => new[] { Grain, Wood }.Select(item =>
                        new FoundationActivityCommodityDemandCoefficient(
                            p, item, 1_000_000UL)))),
            new FoundationCommodityCargoConversionPolicy(
                "authoritative-sales-cargo", new[]
                {
                    new FoundationCommodityCargoConversion(
                        Grain, 1_000_000UL),
                    new FoundationCommodityCargoConversion(
                        Wood, 1_000_000UL)
                }),
            new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Blue, 500U)
            },
            maxProducerPasses: 64);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        FoundationAuthoritativeEconomicSettlementPolicy Policy,
        PhysicalTacticalEdgeId Edge);

    private static Scenario Create(bool enabled = true,
        ulong redOpeningMoneyMicro = 0UL)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var treasury = CivilizationTreasuryRuntimeState.CreateOpening(
            new[] { (Red, redOpeningMoneyMicro), (Blue, 0UL) });
        var physical = fixture.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, fixture.Session.GeneratedWorld, fixture.Incidence)
            .DeployInitialTreasury(treasury);
        fixture.Session.AcceptSnapshot(physical);
        var policy = Policy();
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence,
            new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            },
            enableMonetaryInfrastructure: true,
            settlementPolicy: enabled ? policy : null);
        return new Scenario(host, policy,
            seeded.Transport.Edges[0].Edge);
    }

    [Fact]
    public void FirstClosedCycleSellsAllRealProductionAndCreditsZeroOpeningCash()
    {
        var s = Create();
        var initial = s.Host.Snapshot;
        Assert.Equal(BigInteger.Zero,
            initial.Treasury.Get(Red).CashScaled);
        Assert.Equal(BigInteger.Zero,
            initial.Treasury.Get(Blue).CashScaled);
        var demandBefore = FoundationPhysicalWorkforceEconomicProjector
            .Project(initial.Workers);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        var settled = Assert.IsType<FoundationEconomicSalesSettledEvent>(
            receipt!.EconomicSettlement);
        Assert.Equal(1UL, settled.EconomicCycleOrdinal);
        Assert.True(settled.ProducedGoodsMicro > 0UL);
        Assert.Equal(settled.ProducedGoodsMicro,
            settled.SoldAndConsumedGoodsMicro);
        Assert.True(settled.GrossSalesScaled > BigInteger.Zero);
        Assert.True(settled.CreditedToTreasuriesScaled > BigInteger.Zero);
        Assert.Equal(settled.GrossSalesScaled,
            settled.PhysicalFreightScaled +
            settled.CreditedToTreasuriesScaled);
        Assert.Equal(settled.GrossSalesScaled,
            settled.PhysicalFreightScaled +
            settled.ExternalTollsScaled +
            settled.SellerTaxesScaled + settled.SellerNetScaled);
        Assert.Equal(settled.CreditedToTreasuriesScaled,
            settled.Credits.Aggregate(BigInteger.Zero,
                (sum, row) => sum + row.CashCreditScaled));

        Assert.True(receipt.ReplayMatched);
        Assert.Empty(receipt.Events);
        Assert.Empty(receipt.InfrastructureInvestments);
        Assert.Equal(1, receipt.ExecutedEvents);
        Assert.Equal(15U, receipt.Hash.FormatVersion);
        Assert.Equal(receipt.Hash, s.Host.StateHash);
        Assert.Equal(initial.Revision + 1UL, s.Host.Snapshot.Revision);
        Assert.Equal(1UL, s.Host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(s.Policy.Cargo.CargoUnitCode,
            s.Host.Snapshot.Treasury.LastSettledCargoUnitCode);
        Assert.Equal(initial.Transport.Edges.Count,
            s.Host.Snapshot.Treasury.LastSettledFreightQuotes.Count);
        foreach (var credit in settled.Credits)
        {
            var account = s.Host.Snapshot.Treasury.Get(credit.Civilization);
            Assert.Equal(credit.CashCreditScaled, account.CashScaled);
            Assert.Equal(credit.CashCreditScaled,
                account.TotalSettledEconomicIncomeScaled);
            Assert.Equal(BigInteger.Zero,
                account.TotalInfrastructureInvestmentScaled);
        }
        Assert.Same(initial.Workers, s.Host.Snapshot.Workers);
        Assert.Same(initial.Transport, s.Host.Snapshot.Transport);
        var demandAfter = FoundationPhysicalWorkforceEconomicProjector
            .Project(s.Host.Snapshot.Workers);
        Assert.Equal(demandBefore.DemandSites.ToArray(),
            demandAfter.DemandSites.ToArray());
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void NextTurnMaySpendRealSettledReceiptsButNotUnclosedProspects()
    {
        var s = Create();
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(Red, s.Edge, 1UL));
        s.Host.SetHumanReady(Red);
        var first = s.Host.TryCloseTurn();
        Assert.NotNull(first);
        BigInteger earned = s.Host.Snapshot.Treasury.Get(Red).CashScaled;
        Assert.True(earned > GlobalArenaMoneyScale.FromMoneyMicro(1UL));
        Assert.Equal(15U, first!.Hash.FormatVersion);
        s.Host.QueueHumanMonetaryInvestment(Red, s.Edge, 1UL);
        Assert.Equal(earned, s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        s.Host.SetHumanReady(Red);
        var second = s.Host.TryCloseTurn();
        Assert.NotNull(second);
        Assert.True(second!.ReplayMatched);
        Assert.Equal(2UL,
            second.EconomicSettlement!.EconomicCycleOrdinal);
        Assert.Equal(2UL,
            s.Host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Single(second.InfrastructureInvestments);
        Assert.Equal(1UL,
            s.Host.Snapshot.Transport.Edges.Single(
                row => row.Edge == s.Edge).CommittedInvestmentMoneyMicro);
        Assert.Equal(earned - GlobalArenaMoneyScale.FromMoneyMicro(1UL) +
            second.EconomicSettlement.Credits.Single(
                row => row.Civilization == Red).CashCreditScaled,
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(1UL),
            s.Host.Snapshot.Treasury.Get(Red)
                .TotalInfrastructureInvestmentScaled);
        Assert.Equal(15U, s.Host.StateHash.FormatVersion);
        Assert.Equal(3UL, s.Host.NextTurn.Value);
    }

    [Fact]
    public void ForgedIncomeAndDuplicateJournalAreRejectedByIndependentReplay()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        var ev = receipt!.EconomicSettlement!;
        var executor = new FoundationEconomicSalesSettlementEventExecutor(
            s.Host.Session.GeneratedWorld, s.Host.Incidence, s.Policy);
        var context = new SimulationContext(
            ev.Id.Turn, new SimulationSeed(712UL));
        var independent = executor.Execute(before, ev, context);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(independent));

        var forged = new FoundationEconomicSalesSettledEvent(
            ev.Id, ev.SourceWorldHash, ev.ExpectedWorldRevision,
            ev.EconomicCycleOrdinal, ev.ProducedGoodsMicro,
            ev.SoldAndConsumedGoodsMicro,
            ev.GrossSalesScaled + BigInteger.One,
            ev.PhysicalFreightScaled, ev.SellerTaxesScaled,
            ev.ExternalTollsScaled, ev.SellerNetScaled,
            ev.CreditedToTreasuriesScaled, ev.CargoUnitCode, ev.Credits);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(before, forged, context));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(independent, ev, context));
        Assert.Equal(15U, new CanonicalWorldStateHasher()
            .Compute(independent).FormatVersion);
    }

    [Fact]
    public void SalesRemainUncommittedWhenEconomicPhaseIsDisabled()
    {
        var s = Create(enabled: false);
        var before = s.Host.Snapshot;
        Assert.Equal(13U, s.Host.StateHash.FormatVersion);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Null(receipt!.EconomicSettlement);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(0, receipt.ExecutedEvents);
        Assert.Equal(13U, receipt.Hash.FormatVersion);
        Assert.Equal(BigInteger.Zero,
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.False(s.Host.Snapshot.Treasury.HasClosedEconomicCycle);
        Assert.Same(before.Workers, s.Host.Snapshot.Workers);
        Assert.Same(before.Transport, s.Host.Snapshot.Transport);
    }

    [Fact]
    public void EconomicPolicyCannotCreateMissingTreasuryOrAssumeTaxDefaults()
    {
        var s = Create();
        Assert.Throws<ArgumentException>(() =>
            new FoundationAuthoritativeEconomicSettlementPolicy(
                s.Policy.Demand, s.Policy.Cargo,
                new[] { new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U) }));
        Assert.Throws<ArgumentException>(() =>
            new FoundationAuthoritativeEconomicSettlementPolicy(
                s.Policy.Demand, s.Policy.Cargo,
                s.Policy.TaxRates, maxProducerPasses: 0));
        Assert.Equal(0UL,
            s.Host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
    }
}
