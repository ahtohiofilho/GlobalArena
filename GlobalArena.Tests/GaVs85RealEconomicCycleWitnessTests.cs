using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs85RealEconomicCycleWitnessTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static CanonicalPrimitiveSiteDiscoveryPolicy Policy() => new(
        new EconomicPrimitiveCommodityMapping(
            new CommodityId(7), new CommodityId(8), new CommodityId(9)),
        new[] {
            new CanonicalPrimitiveDemandRate(new CommodityId(7),
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
            new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveFreightPortfolio Portfolio,
        CanonicalCycleStampedPriceReceipt Receipt) Capture()
    {
        var (f, _, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        // SharedBuyer reassigned a worker in the same movement turn as
        // settlement; seal a NEW actual cycle-closing tick before freezing.
        Assert.NotNull(f.Session.AdvanceMovementTurn());
        var sites = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var priorFreight = Freight(f.Session, sites, portfolio);
        var rates = sites.Flows.SupplyFlows
            .Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct().Select(x =>
                new CanonicalWorkforceActivityRealizationPolicy(x, 500, 100))
            .ToArray();
        var previous = CanonicalWorkforceRealizationProjection.FromSites(
            f.Session, sites, priorFreight, rates,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var feedback = CanonicalLaggedProductionResponse.FromFrozenProjection(
            f.Session, sites, priorFreight, previous, 100,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        return (f, portfolio,
            CanonicalCycleStampedPriceReceipt.Capture(f.Session, feedback));
    }

    private static CanonicalSimultaneousFreightReference Freight(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveFreightPortfolio portfolio) =>
        CanonicalSimultaneousFreightReference.FromSites(
            session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);

    private static (
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalSimultaneousFreightReference Freight) Current(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        CanonicalPrimitiveFreightPortfolio portfolio)
    {
        var sites = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        return (sites, Freight(f.Session, sites, portfolio));
    }

    [Fact]
    public void SameTurnWorkerActionAfterSettlementCannotMasqueradeAsCycleClosingTick()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var priorFreight = Freight(f.Session, sites, portfolio);
        var policies = sites.Flows.SupplyFlows
            .Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct()
            .Select(x => new CanonicalWorkforceActivityRealizationPolicy(
                x, 500, 0)).ToArray();
        var old = CanonicalWorkforceRealizationProjection.FromSites(
            f.Session, sites, priorFreight, policies,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var feedback = CanonicalLaggedProductionResponse.FromFrozenProjection(
            f.Session, sites, priorFreight, old, 100,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        Assert.Equal(f.Session.State.MovementTurn,
            f.Session.State.Cycles[^1].MovementTurn);
        Assert.Equal("reassign", f.Session.Events[^1].Kind);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalCycleStampedPriceReceipt.Capture(f.Session, feedback));
    }

    [Fact]
    public void OneActualCompletedEconomicCycleIsRequiredAndStampedWithoutRealMarketClaim()
    {
        var (f, portfolio, receipt) = Capture();
        var initialTurn = f.Session.State.MovementTurn;
        var oldHash = receipt.SourceWorldHashHex;
        Assert.Equal(initialTurn, receipt.SourceCompletedEconomicCycleTurn);
        Assert.True(receipt.HasGenuineCompletedSourceCycle);
        Assert.False(receipt.QuotesAreActualExecutedMarketPrices);
        Assert.False(receipt.PersistsIntoCanonicalSave);
        Assert.False(receipt.CreatesMoneyOrCredit);
        Assert.False(receipt.CanAuthorizeSettlement);
        var (beforeSites, beforeFreight) = Current(f, portfolio);
        Assert.Throws<InvalidOperationException>(() =>
            receipt.ApplyAfterNextSettledCycle(f.Session,
                beforeSites, beforeFreight,
                Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion()));

        var nextCycle = f.Session.AdvanceMovementTurn();
        Assert.NotNull(nextCycle);
        Assert.Equal(initialTurn + 1UL, nextCycle!.MovementTurn);
        var (nextSites, nextFreight) = Current(f, portfolio);
        var beforeProofSave = f.Session.ExportSaveJson();
        var result = receipt.ApplyAfterNextSettledCycle(
            f.Session, nextSites, nextFreight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        Assert.True(result.ProvesActualChronologicalEconomicCycle);
        Assert.False(result.ProvesRealMarketSaleOrPayment);
        Assert.False(result.CreatesInventoriesOrMoney);
        Assert.False(result.ExecutesWorkforceReassignment);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.NotEqual(oldHash, result.NextRealCompletedEconomicCycleWorldHashHex);
        Assert.Equal(nextCycle.MovementTurn, result.NextRealCompletedEconomicCycleTurn);
        Assert.Equal(receipt.FrozenNextActivityPolicies,
            result.HypotheticalNextProduction.Policies);
        Assert.NotNull(result.HypotheticalNextProduction.Allocation);
        Assert.All(result.HypotheticalNextProduction.Sources, x => {
            Assert.True(x.ActualActivityWorkers > 0);
            Assert.True(x.DerivedProducedRaw <= x.PotentialRaw);
        });
        result.VerifyIndependent();
        Assert.Equal(beforeProofSave, f.Session.ExportSaveJson());
    }

    [Fact]
    public void AnotherPhysicalMovementTurnInvalidatesCompletedCycleProjection()
    {
        var (f, portfolio, receipt) = Capture();
        f.Session.AdvanceMovementTurn();
        var (sites, freight) = Current(f, portfolio);
        var trial = receipt.ApplyAfterNextSettledCycle(f.Session, sites,
            freight, Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            trial.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            receipt.ApplyAfterNextSettledCycle(f.Session,
                f.Session.DiscoverPrimitiveEconomicSites(Policy()),
                Freight(f.Session,
                    f.Session.DiscoverPrimitiveEconomicSites(Policy()), portfolio),
                Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion()));
    }

    [Fact]
    public void ReceiptCannotApplyToRestoredIdenticalSaveOrDifferentSession()
    {
        var (f, portfolio, receipt) = Capture();
        f.Session.AdvanceMovementTurn();
        var (sites, freight) = Current(f, portfolio);
        var projection = receipt.ApplyAfterNextSettledCycle(f.Session,
            sites, freight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        var restoredSites = restored.DiscoverPrimitiveEconomicSites(Policy());
        Assert.Throws<InvalidOperationException>(() =>
            receipt.ApplyAfterNextSettledCycle(restored, restoredSites,
                freight, Array.Empty<CanonicalSimultaneousFreightLot>(),
                Congestion()));
        Assert.Throws<InvalidOperationException>(() =>
            projection.RequireCurrent(restored));
    }

    [Fact]
    public void LocalPriceWitnessDoesNotSurviveExtraWorkerReassignment()
    {
        var (f, portfolio, receipt) = Capture();
        f.Session.ReassignWorkers(f.Session.InitialTile,
            EconomicActivityKind.Mining, EconomicActivityKind.Forestry, 1);
        f.Session.AdvanceMovementTurn();
        var (sites, freight) = Current(f, portfolio);
        Assert.Throws<InvalidOperationException>(() =>
            receipt.ApplyAfterNextSettledCycle(f.Session, sites, freight,
                Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion()));
    }

    [Fact]
    public void BoundedNextRealProductionCannotShipMoreThanCertifiedDerivedOutput()
    {
        var (f, portfolio, receipt) = Capture();
        f.Session.AdvanceMovementTurn();
        var (sites, freight) = Current(f, portfolio);
        var trial = receipt.ApplyAfterNextSettledCycle(f.Session,
            sites, freight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var candidate = freight.Offers.Single(x =>
            x.Commodity == new CommodityId(7));
        var produced = trial.HypotheticalNextProduction.Sources.Single(x =>
            x.Source == candidate.Source && x.Commodity == candidate.Commodity &&
            x.Activity == candidate.Activity).DerivedProducedRaw;
        Assert.True(candidate.SourcePotentialRaw > produced);
        Assert.Throws<ArgumentException>(() =>
            receipt.ApplyAfterNextSettledCycle(f.Session, sites, freight,
                new [] { new CanonicalSimultaneousFreightLot(
                    candidate, produced + 1UL) }, Congestion()));
        var accepted = receipt.ApplyAfterNextSettledCycle(f.Session,
            sites, freight,
            new [] { new CanonicalSimultaneousFreightLot(candidate, produced) },
            Congestion());
        Assert.Equal(new BigInteger(produced), Assert.Single(
            accepted.HypotheticalNextProduction.Allocation!.RemoteMarkets)
            .TotalHypotheticalDeliveredRaw);
        accepted.VerifyIndependent();
    }

    [Fact]
    public void DuplicateReadOnlyApplicationsAreDeterministicAndDoNotMutateRealCycle()
    {
        var (f, portfolio, receipt) = Capture();
        f.Session.AdvanceMovementTurn();
        var (sites, freight) = Current(f, portfolio);
        var save = f.Session.ExportSaveJson();
        var a = receipt.ApplyAfterNextSettledCycle(f.Session,
            sites, freight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var b = receipt.ApplyAfterNextSettledCycle(f.Session,
            sites, freight,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        Assert.Equal(a.NextRealCompletedEconomicCycleWorldHashHex,
            b.NextRealCompletedEconomicCycleWorldHashHex);
        Assert.Equal(a.HypotheticalNextProduction.Policies,
            b.HypotheticalNextProduction.Policies);
        Assert.Equal(a.HypotheticalNextProduction.Sources,
            b.HypotheticalNextProduction.Sources);
        Assert.Equal(save, f.Session.ExportSaveJson());
        a.VerifyIndependent();
        b.VerifyIndependent();
    }
}
