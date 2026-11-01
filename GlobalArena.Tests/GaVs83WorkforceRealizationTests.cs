using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs83WorkforceRealizationTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalSimultaneousFreightReference Reference) Build()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var reference = CanonicalSimultaneousFreightReference.FromSites(
            f.Session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        return (f, sites, reference);
    }

    private static CanonicalWorkforceActivityRealizationPolicy[] Rates(
        CanonicalPrimitiveSiteDiscovery sites, int utilization, int retention = 0) =>
        sites.Flows.SupplyFlows.Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct()
            .Select(x => new CanonicalWorkforceActivityRealizationPolicy(
                x, utilization, retention)).ToArray();

    private static CanonicalWorkforceRealizationProjection Project(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference reference,
        IEnumerable<CanonicalWorkforceActivityRealizationPolicy> policies,
        params CanonicalSimultaneousFreightLot[] lots) =>
        CanonicalWorkforceRealizationProjection.FromSites(
            f.Session, sites, reference, policies, lots, Congestion());

    [Fact]
    public void FullUtilizationIsBoundToCurrentResidentActivityWorkersAndPhysicalCapacity()
    {
        var (f, sites, proof) = Build();
        var before = f.Session.ExportSaveJson();
        var a = Project(f, sites, proof, Rates(sites, 1000));
        Assert.NotNull(a.Allocation);
        Assert.Equal(sites.Flows.SupplyFlows.Count(x => x.RawQuantity > 0),
            a.Sources.Count);
        Assert.All(a.Sources, x => {
            Assert.True(x.ActualActivityWorkers > 0);
            Assert.Equal(x.PotentialRaw, x.DerivedProducedRaw);
            Assert.Equal(0UL, x.PotentialNotExtractedRaw);
            Assert.Equal(0UL, x.DeliberatelyRetainedRaw);
            Assert.Equal(1000, x.UtilizationPermille);
        });
        Assert.All(a.Allocation!.Sources, x =>
            Assert.Equal(new BigInteger(x.HypotheticallyProducedRaw),
                x.OfferedAndLocallyAbsorbedRaw));
        Assert.Empty(a.Allocation.RemoteMarkets);
        Assert.Equal(BigInteger.Zero,
            a.Allocation.AuthenticatedNominalFreightCost.Numerator);
        Assert.True(a.UsesPhysicalResidentWorkers);
        Assert.False(a.CreatesWorkersOrCities);
        Assert.False(a.AllocatesWorkPerIndividualWorker);
        Assert.False(a.UsesArtificialBuyerBudget);
        Assert.False(a.CreatesInvoluntaryStock);
        Assert.False(a.CapsTradeByBuyerDemand);
        Assert.False(a.CanAuthorizeRealSettlement);
        a.VerifyIndependent();
        Assert.Equal(before, f.Session.ExportSaveJson());
    }

    [Fact]
    public void HalfUtilizationAndQuarterRetentionRoundDownExactlyAndLeavePotentialUnextracted()
    {
        var (f, sites, proof) = Build();
        var a = Project(f, sites, proof, Rates(sites, 500, 250));
        Assert.NotNull(a.Allocation);
        Assert.All(a.Sources, x => {
            Assert.Equal(x.PotentialRaw / 2UL, x.DerivedProducedRaw);
            Assert.Equal(x.DerivedProducedRaw / 4UL,
                x.DeliberatelyRetainedRaw);
            Assert.Equal(x.PotentialRaw - x.DerivedProducedRaw,
                x.PotentialNotExtractedRaw);
        });
        Assert.All(a.Allocation!.Sources, x => {
            Assert.Equal(new BigInteger(x.HypotheticallyProducedRaw -
                x.DeliberatelyRetainedRaw), x.OfferedAndLocallyAbsorbedRaw);
            Assert.Equal(x.PotentialRaw - x.HypotheticallyProducedRaw,
                x.PotentialNotExtractedRaw);
        });
        a.VerifyIndependent();
    }

    [Fact]
    public void ZeroUtilizationDoesNotCreateProducedMaterialsInventoryOrMarket()
    {
        var (f, sites, proof) = Build();
        var a = Project(f, sites, proof, Rates(sites, 0));
        Assert.Null(a.Allocation);
        Assert.NotEmpty(a.Sources);
        Assert.All(a.Sources, x => {
            Assert.Equal(0UL, x.DerivedProducedRaw);
            Assert.Equal(x.PotentialRaw, x.PotentialNotExtractedRaw);
            Assert.Equal(0UL, x.DeliberatelyRetainedRaw);
        });
        Assert.False(a.CreatesInvoluntaryStock);
        Assert.False(a.ExecutesActualProductionOrSale);
        a.VerifyIndependent();
    }

    [Fact]
    public void RemoteShipmentOverRealizedWorkerOutputFailsRatherThanUsingPotential()
    {
        var (f, sites, proof) = Build();
        var offer = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        Assert.True(offer.SourcePotentialRaw > 10);
        var activityRates = Rates(sites, 0)
            .Select(x => x.Activity == offer.Activity
                ? x with { UtilizationPermille = 500 }
                : x).ToArray();
        var produced = offer.SourcePotentialRaw / 2UL;
        Assert.True(produced > 1);
        Assert.Throws<ArgumentException>(() => Project(
            f, sites, proof, activityRates, new(offer, produced + 1UL)));
        var a = Project(f, sites, proof,
            activityRates, new(offer, produced));
        Assert.Equal(new BigInteger(produced), Assert.Single(
            a.Allocation!.RemoteMarkets).TotalHypotheticalDeliveredRaw);
        Assert.False(a.CapsTradeByBuyerDemand);
        Assert.False(a.ComputesMoneyProfit);
        a.VerifyIndependent();
    }

    [Fact]
    public void InvalidOrMissingActivityPolicyDoesNotInventWorkforce()
    {
        var (f, sites, proof) = Build();
        var policies = Rates(sites, 1000);
        Assert.Throws<ArgumentException>(() => Project(f, sites, proof,
            policies.Take(1).Concat([policies[0]])));
        Assert.Throws<ArgumentException>(() => Project(f, sites, proof,
            policies.Take(1).Select(x => x with { UtilizationPermille = 1001 })));
        Assert.Throws<ArgumentException>(() => Project(f, sites, proof,
            policies.Take(1).Select(x => x with { RetentionPermille = -1 })));
        Assert.Throws<ArgumentException>(() => Project(f, sites, proof,
            policies.Take(1)));
        var fake = new CanonicalWorkforceActivityRealizationPolicy(
            (EconomicActivityKind)999, 1000, 0);
        Assert.Throws<ArgumentException>(() => Project(f, sites, proof,
            policies.Concat([fake])));
        var empty = Project(f, sites, proof, Rates(sites, 0));
        var food = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        Assert.Throws<ArgumentException>(() => Project(f, sites, proof,
            Rates(sites, 0), new(food, 1UL)));
        empty.VerifyIndependent();
    }

    [Fact]
    public void RealWorkerPolicyAndFreightRemainDeterministicAcrossOrderingAndSaveRestore()
    {
        var (f, sites, proof) = Build();
        var save = f.Session.ExportSaveJson();
        var food = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        var wood = proof.Offers.Single(x => x.Commodity == new CommodityId(8));
        var rates = Rates(sites, 800, 100);
        var fq = food.SourcePotentialRaw * 8UL / 10UL / 4UL;
        var wq = wood.SourcePotentialRaw * 8UL / 10UL / 4UL;
        Assert.True(fq > 0 && wq > 0);
        var a = Project(f, sites, proof, rates,
            new(food, fq), new(wood, wq));
        var b = Project(f, sites, proof, rates.Reverse(),
            new(wood, wq), new(food, fq));
        Assert.Equal(a.Sources, b.Sources);
        Assert.Equal(a.Allocation!.Sources, b.Allocation!.Sources);
        Assert.Equal(a.Allocation.LocalMarkets, b.Allocation.LocalMarkets);
        Assert.Equal(0, a.Allocation.AuthenticatedNominalFreightCost.CompareTo(
            b.Allocation.AuthenticatedNominalFreightCost));
        Assert.Equal(save, f.Session.ExportSaveJson());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() => a.RequireCurrent(restored));
        a.VerifyIndependent();
        b.VerifyIndependent();
    }
}
