using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs80SupplyBoundFreightDiscoveryTests
{
    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Build() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer();

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static CanonicalSupplyBoundFreightReference SupplyBound(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture fixture,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveFreightPortfolio portfolio)
    {
        var food = new CommodityId(7);
        var policy = portfolio.Slices.Single(x =>
            x.Policy.Commodity == food &&
            x.Policy.Activity == EconomicActivityKind.Agriculture).Policy;
        return CanonicalSupplyBoundFreightReference.FromSites(
            fixture.Session, sites, policy, portfolio.PurchasingPolicyId,
            portfolio.PurchasingPolicyVersion, portfolio.CalibrationId,
            portfolio.CalibrationVersion, portfolio.NominalUnitId);
    }

    [Fact]
    public void RealFreightAuthorizationExposesEntireSourceQuantityNotLegacyBuyerDemand()
    {
        var (fixture, sites, portfolio) = Build();
        var snapshot = SupplyBound(fixture, sites, portfolio);
        var real = Assert.Single(snapshot.Services);
        var legacy = portfolio.CandidateUses.Single(x =>
            x.ServiceId == real.ServiceId &&
            x.Commodity == real.Commodity &&
            x.Activity == real.Activity);
        Assert.True(real.SourcePhysicalSupplyRaw > real.BuyerDemandPriceSignalRaw);
        Assert.Equal(Math.Min(real.SourcePhysicalSupplyRaw,
            real.BuyerDemandPriceSignalRaw), real.LegacyBuyerDemandCappedRaw);
        Assert.Equal(legacy.MaximumRawQuantity, real.LegacyBuyerDemandCappedRaw);
        Assert.Equal(real.SourcePhysicalSupplyRaw, real.NewSupplyBoundRaw);
        Assert.True(real.NewSupplyBoundRaw > legacy.MaximumRawQuantity);
        Assert.True(snapshot.RegisteredServicesInspected > 0);
        Assert.True(snapshot.UsesRealInstitutionalProof);
        Assert.True(snapshot.EnforcesSourcePhysicalQuantity);
        Assert.False(snapshot.CapsQuantityByBuyerDemand);
        Assert.False(snapshot.UsesExogenousMoneyBudget);
        Assert.False(snapshot.ProvesHardVehicleThroughput);
        Assert.False(snapshot.ComputesNetMonetaryRevenue);
        Assert.False(snapshot.CanAuthorizeRealSettlement);
        snapshot.VerifyIndependent();
    }

    [Fact]
    public void AboveLegacyBuyerDemandTrialUsesActualRoadAndEndogenousDestinationPrice()
    {
        var (fixture, sites, portfolio) = Build();
        var proof = SupplyBound(fixture, sites, portfolio);
        var row = Assert.Single(proof.Services);
        var oldMax = row.LegacyBuyerDemandCappedRaw;
        var quantity = row.NewSupplyBoundRaw;
        Assert.True(quantity > oldMax);
        var trial = proof.PreviewOneLot(row.ServiceId, quantity, Congestion());
        Assert.Equal(quantity, trial.RawQuantity);
        Assert.Equal(row.BuyerDemandPriceSignalRaw, trial.BuyerDemandPriceSignalRaw);
        Assert.True(trial.AuthenticatedNominalFreightCost.Numerator >= 0);
        var expected = CanonicalEndogenousLocalPriceComparison.Calculate(
            quantity, row.BuyerDemandPriceSignalRaw, 1);
        Assert.Equal(0, trial.GentleGrossRevenueIndex.CompareTo(expected.Revenue));
        Assert.Equal(0, trial.GentleAveragePriceIndex.CompareTo(expected.Average));
        var low = proof.PreviewOneLot(row.ServiceId, 1UL, Congestion());
        Assert.True(trial.GentleAveragePriceIndex.CompareTo(
            low.GentleAveragePriceIndex) <= 0);
        proof.VerifyIndependent();
    }

    [Fact]
    public void SourcePhysicalSupplyStillCapsTrialAndMissingServiceCannotBeInvented()
    {
        var (fixture, sites, portfolio) = Build();
        var proof = SupplyBound(fixture, sites, portfolio);
        var row = Assert.Single(proof.Services);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            proof.PreviewOneLot(row.ServiceId, 0, Congestion()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            proof.PreviewOneLot(row.ServiceId,
                checked(row.SourcePhysicalSupplyRaw + 1), Congestion()));
        Assert.Throws<InvalidOperationException>(() =>
            proof.PreviewOneLot(9999999UL, 1UL, Congestion()));
        Assert.Throws<ArgumentNullException>(() =>
            proof.PreviewOneLot(row.ServiceId, 1UL, null!));
        proof.VerifyIndependent();
    }

    [Fact]
    public void HistoricalFreightCapIsUntouchedBySupplyBoundDiscovery()
    {
        var (fixture, sites, portfolio) = Build();
        var before = portfolio.CandidateUses.Select(x =>
            (x.Source, x.Buyer, x.Commodity, x.Activity,
             x.ServiceId, x.MaximumRawQuantity)).ToArray();
        var save = fixture.Session.ExportSaveJson();
        var hash = fixture.Session.CurrentHash;
        var a = SupplyBound(fixture, sites, portfolio);
        var b = SupplyBound(fixture, sites, portfolio);
        Assert.Equal(a.Services, b.Services);
        Assert.Equal(before, portfolio.CandidateUses.Select(x =>
            (x.Source, x.Buyer, x.Commodity, x.Activity,
             x.ServiceId, x.MaximumRawQuantity)).ToArray());
        Assert.Equal(save, fixture.Session.ExportSaveJson());
        Assert.Equal(hash, fixture.Session.CurrentHash);
        Assert.False(portfolio.CanAuthorizeRealSettlement);
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void NominalFreightCostAndPriceIndicesAreDeterministicallyAuditable()
    {
        var (fixture, sites, portfolio) = Build();
        var proof = SupplyBound(fixture, sites, portfolio);
        var row = Assert.Single(proof.Services);
        var a = proof.PreviewOneLot(row.ServiceId, row.NewSupplyBoundRaw, Congestion());
        var b = proof.PreviewOneLot(row.ServiceId, row.NewSupplyBoundRaw, Congestion());
        Assert.Equal(a.ServiceId, b.ServiceId);
        Assert.Equal(a.RawQuantity, b.RawQuantity);
        Assert.Equal(0, a.AuthenticatedNominalFreightCost.CompareTo(
            b.AuthenticatedNominalFreightCost));
        Assert.Equal(0, a.GentleGrossRevenueIndex.CompareTo(
            b.GentleGrossRevenueIndex));
        Assert.Equal(0, a.SteepGrossRevenueIndex.CompareTo(
            b.SteepGrossRevenueIndex));
        proof.VerifyIndependent();
    }

    [Fact]
    public void SessionRestoreDoesNotInheritFreightProofAndNoInventoryWasCommitted()
    {
        var (fixture, sites, portfolio) = Build();
        var proof = SupplyBound(fixture, sites, portfolio);
        var save = fixture.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() => proof.RequireCurrent(restored));
        Assert.False(proof.CreatesActualRemoteConsumption);
        Assert.False(proof.CanAuthorizeRealSettlement);
        Assert.Equal(save, fixture.Session.ExportSaveJson());
        proof.VerifyIndependent();
    }
}
