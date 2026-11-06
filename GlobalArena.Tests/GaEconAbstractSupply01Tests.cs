using System.Numerics;
using GlobalArena.Economy;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-ABSTRACT-SUPPLY-01: physical supply is from executed production,
/// P is the real population, and D=P*N does not cap physical offer.
/// Experimental retention, curve and N never become canonical settlement.
/// </summary>
public sealed class GaEconAbstractSupply01Tests
{
    private const ulong Scale = 1_000_000UL;
    private static decimal ExamplePrice(decimal ratio) => 2m / (1m + ratio);

    private static CanonicalPhysicalTacticalSession ClosedLegacy()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 5; i++)
            Assert.Null(session.AdvanceMovementTurn());
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    private static CanonicalPhysicalTacticalSession ClosedCash()
    {
        var session = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    private static CanonicalAbstractExecutedSupplyShadow Quote(
        CanonicalPhysicalTacticalSession session,
        AbstractPurchasingPowerIndex? n = null,
        ulong retention = 0UL) =>
        CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
            session, commodity: 1U, explicitlyRetainedOfProductionRaw: retention,
            rawUnitsPerEconomicUnit: Scale,
            candidatePurchasingPower: n ?? AbstractPurchasingPowerIndex.Neutral,
            experimentalPriceCurve: ExamplePrice);

    [Fact]
    public void BeforeExecutedCycleThereIsNoPhysicalOfferEvidence()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        Assert.Throws<InvalidOperationException>(() => Quote(session));
        for (var i = 0; i < 5; i++)
            session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() => Quote(session));
    }

    [Fact]
    public void ActualFoodProductionAndPopulationDriveTheShadowQuote()
    {
        var session = ClosedLegacy();
        var physical = session.CaptureLatestExecutedPhysicalCycleProvenance();
        var candidate = Quote(session);
        candidate.VerifyIndependent();

        Assert.Equal(physical.CycleOrdinal, candidate.CycleOrdinal);
        Assert.Equal(physical.ClosingMovementTurn, candidate.ClosingMovementTurn);
        Assert.Equal(physical.ClosingWorldHashHex, candidate.ClosingWorldHashHex);
        Assert.Equal(physical.TotalRealPopulation, candidate.RealPopulation);
        Assert.True(candidate.ActuallyProducedRaw > 0);
        var foodSources = physical.Production.Where(x => x.Commodity == 1U).ToArray();
        Assert.Equal(foodSources.Select(x => x.ProducedRaw).Aggregate(
            BigInteger.Zero, (a, x) => a + x), candidate.ActuallyProducedRaw);
        Assert.Equal(foodSources.Length, candidate.ExecutedProductionSources.Count);
        Assert.All(candidate.ExecutedProductionSources, x =>
        {
            Assert.Equal(1U, x.Commodity);
            Assert.True(x.ProducedRaw > 0UL);
            Assert.True(x.SourceFineTile > 0UL);
        });
        Assert.Equal(candidate.ActuallyProducedRaw, candidate.CandidateOfferedRaw);
        Assert.Equal((decimal)candidate.ActuallyProducedRaw / Scale,
            candidate.CandidateQuote.Offered);
        Assert.Equal((decimal)physical.TotalRealPopulation,
            candidate.CandidateQuote.EffectiveDemand);
        Assert.Equal(candidate.CandidateQuote.Price *
            candidate.CandidateQuote.EffectiveDemand,
            candidate.CandidateQuote.ReferenceRevenue);

        Assert.True(candidate.HasAuthenticatedExecutedProduction);
        Assert.False(candidate.AuthenticatesActualMarketOffer);
        Assert.False(candidate.AuthenticatesFinalUseOrCrossCycleStockVintage);
        Assert.False(candidate.AuthenticatesPurchasingPowerN);
        Assert.False(candidate.HasApprovedPriceCurve);
        Assert.False(candidate.CapsPhysicalSupplyByEffectiveDemand);
        Assert.False(candidate.UsesLegacyCashBalances);
        Assert.False(candidate.ChangesPhysicalStocksOrCurrency);
        Assert.False(candidate.MutatesCanonicalSaveOrHash);
        Assert.False(candidate.CanAuthorizeExecutedSettlement);
        Assert.False(candidate.CandidateQuote.IsExecutedSettlement);
    }

    [Fact]
    public void PurchasingPowerChangesPriceSignalNotActualOfferOrPopulation()
    {
        var session = ClosedLegacy();
        var weak = Quote(session,
            AbstractPurchasingPowerIndex.FromRelativeCapacity(0.000001m));
        var neutral = Quote(session);
        var strong = Quote(session,
            AbstractPurchasingPowerIndex.FromRelativeCapacity(3m));

        Assert.Equal(weak.ActuallyProducedRaw, neutral.ActuallyProducedRaw);
        Assert.Equal(neutral.ActuallyProducedRaw, strong.ActuallyProducedRaw);
        Assert.Equal(weak.CandidateQuote.Offered, strong.CandidateQuote.Offered);
        Assert.Equal(weak.RealPopulation, strong.RealPopulation);
        Assert.Equal(weak.RealPopulation * 0.000001m,
            weak.CandidateQuote.EffectiveDemand);
        Assert.Equal(weak.RealPopulation * 3m,
            strong.CandidateQuote.EffectiveDemand);
        Assert.True(weak.CandidateQuote.Offered >
            weak.CandidateQuote.EffectiveDemand);
        Assert.True(weak.CandidateQuote.SupplyDemandRatio >
            strong.CandidateQuote.SupplyDemandRatio);
        Assert.True(weak.CandidateQuote.Price <
            strong.CandidateQuote.Price);
        Assert.False(weak.CanAuthorizeExecutedSettlement);
    }

    [Fact]
    public void ExplicitRetentionIsNotInventedUnsoldStock()
    {
        var session = ClosedLegacy();
        var gross = Quote(session);
        var retained = checked((ulong)gross.ActuallyProducedRaw / 2UL);
        var reduced = Quote(session, retention: retained);
        var allRetained = Quote(session,
            retention: checked((ulong)gross.ActuallyProducedRaw));

        Assert.Equal(gross.ActuallyProducedRaw, reduced.ActuallyProducedRaw);
        Assert.Equal(new BigInteger(retained), reduced.CandidateRetentionRaw);
        Assert.Equal(gross.ActuallyProducedRaw - retained,
            reduced.CandidateOfferedRaw);
        Assert.True(reduced.CandidateQuote.Offered <
            gross.CandidateQuote.Offered);
        Assert.Equal(BigInteger.Zero, allRetained.CandidateOfferedRaw);
        Assert.False(allRetained.CandidateQuote.HasOffer);
        Assert.Equal(0m, allRetained.CandidateQuote.Price);
        Assert.Equal(0m, allRetained.CandidateQuote.ReferenceRevenue);
    }

    [Fact]
    public void CashMarketCanBeObservedWithoutBorrowingItsBalancesOrFoodQuota()
    {
        var session = ClosedCash();
        var cashBefore = session.State.FoodCash!;
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var accounts = cashBefore.Accounts.ToArray();
        var receipts = cashBefore.Receipts.ToArray();

        var shadow = Quote(session,
            AbstractPurchasingPowerIndex.FromRelativeCapacity(2m));
        Assert.Equal(session.State.TotalWorkers, shadow.RealPopulation);
        Assert.Equal(session.State.TotalWorkers * 2m,
            shadow.CandidateQuote.EffectiveDemand);
        Assert.Equal((decimal)shadow.ActuallyProducedRaw / Scale,
            shadow.CandidateQuote.Offered);
        Assert.False(shadow.UsesLegacyCashBalances);
        Assert.False(shadow.CanAuthorizeExecutedSettlement);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(accounts, session.State.FoodCash!.Accounts.ToArray());
        Assert.Equal(receipts, session.State.FoodCash.Receipts.ToArray());
    }

    [Fact]
    public void SaveReplayReconstructsSameActualSourcesAndCandidateQuote()
    {
        var session = ClosedLegacy();
        var original = Quote(session);
        var payload = session.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(payload);
        var restored = Quote(replay);

        Assert.Equal(session.CurrentHash, replay.CurrentHash);
        Assert.Equal(payload, replay.ExportSaveJson());
        Assert.Equal(original.ExecutedProductionSources.ToArray(),
            restored.ExecutedProductionSources.ToArray());
        Assert.Equal(original.ActuallyProducedRaw, restored.ActuallyProducedRaw);
        Assert.Equal(original.CandidateQuote, restored.CandidateQuote);
        Assert.Throws<InvalidOperationException>(() =>
            original.RequireCurrent(replay));
        restored.VerifyIndependent();
    }

    [Fact]
    public void OffCadenceMovementAndWorkforceChangesInvalidateSourceWitness()
    {
        var session = ClosedLegacy();
        var old = Quote(session);
        Assert.Null(session.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(old.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() => Quote(session));

        var changed = ClosedLegacy();
        var before = Quote(changed);
        changed.ReassignWorkers(changed.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 1UL);
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(changed));
        Assert.Throws<InvalidOperationException>(() => Quote(changed));
    }

    [Fact]
    public void UnknownCommodityInvalidUnitsAndOverRetentionFailClosed()
    {
        var session = ClosedLegacy();
        var original = Quote(session);
        var gross = checked((ulong)original.ActuallyProducedRaw);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
                session, 0U, 0UL, Scale,
                AbstractPurchasingPowerIndex.Neutral, ExamplePrice));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
                session, 1U, 0UL, 0UL,
                AbstractPurchasingPowerIndex.Neutral, ExamplePrice));
        Assert.Throws<ArgumentException>(() =>
            CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
                session, 999U, 0UL, Scale,
                AbstractPurchasingPowerIndex.Neutral, ExamplePrice));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Quote(session, retention: checked(gross + 1UL)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
                session, 1U, 0UL, Scale,
                default, ExamplePrice));
        Assert.Throws<ArgumentNullException>(() =>
            CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
                session, 1U, 0UL, Scale,
                AbstractPurchasingPowerIndex.Neutral, null!));
    }

    [Fact]
    public void ExperimentalCurveCannotRewriteTheCanonicalSession()
    {
        var session = ClosedLegacy();
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();
        var calls = 0;
        decimal Curve(decimal x)
        {
            calls++;
            return 2m / (1m + x);
        }

        var quote = CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle(
            session, 1U, 0UL, Scale, AbstractPurchasingPowerIndex.Neutral, Curve);
        Assert.Equal(1, calls);
        quote.VerifyIndependent();
        Assert.Equal(1, calls);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
    }
}
