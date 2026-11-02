using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs95PhysicalDispositionTrialTests
{
    private static CanonicalPrimitiveSiteDiscoveryPolicy SitePolicy(
        long woodDemand = 125_000) => new(
        new EconomicPrimitiveCommodityMapping(
            new CommodityId(7), new CommodityId(8), new CommodityId(9)),
        new[] {
            new CanonicalPrimitiveDemandRate(new CommodityId(7),
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(new CommodityId(8), woodDemand),
            new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static CanonicalPrimitivePhysicalUtilization[] Production(
        int food = 800, int wood = 650, int stone = 1000) => new[] {
            new CanonicalPrimitivePhysicalUtilization(
                EconomicActivityKind.Agriculture, food),
            new CanonicalPrimitivePhysicalUtilization(
                EconomicActivityKind.Forestry, wood),
            new CanonicalPrimitivePhysicalUtilization(
                EconomicActivityKind.Mining, stone)
        };

    private static CanonicalPrimitiveLocalPhysicalUsePolicy[] Local(
        int food = 250, int wood = 300, int stone = 400) => new[] {
            new CanonicalPrimitiveLocalPhysicalUsePolicy(
                EconomicActivityKind.Agriculture, food),
            new CanonicalPrimitiveLocalPhysicalUsePolicy(
                EconomicActivityKind.Forestry, wood),
            new CanonicalPrimitiveLocalPhysicalUsePolicy(
                EconomicActivityKind.Mining, stone)
        };

    private static CanonicalSimultaneousFreightReference Freight(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites)
    {
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(new CommodityId(7), "food", 1),
            new FoodCommodityDefinition(new CommodityId(8), "wood", 1),
            new FoodCommodityDefinition(new CommodityId(9), "stone", 1)
        });
        var rate = new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
        return CanonicalSimultaneousFreightReference.FromSites(
            session, sites,
            new[] {
                new CanonicalEligibleFreightDiscoveryPolicy(
                    new CommodityId(7), EconomicActivityKind.Agriculture,
                    rate, ExperimentalLandInfrastructureKind.StoneRoad, catalog),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    new CommodityId(8), EconomicActivityKind.Forestry,
                    rate, ExperimentalLandInfrastructureKind.StoneRoad, catalog),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    new CommodityId(9), EconomicActivityKind.Mining,
                    rate, ExperimentalLandInfrastructureKind.StoneRoad, catalog)
            }, "VS56-PURCHASING", 1,
            "VS56-CALIBRATION", 1, "GA-VS56-NOMINAL-LAB");
    }

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitivePhysicalMulticommodityBatch Physical,
        CanonicalSimultaneousFreightReference Freight) Build(
        int food = 800, int wood = 650, int stone = 1000)
    {
        var (f, _, _, sample) = GaVs92ExecutedFoodSampleAuditTests.Build();
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var evidence = CanonicalPrimitiveCommodityExecutionBoundary
            .FromCertifiedEvidence(f.Session, sites, sample);
        var physical = CanonicalPrimitivePhysicalMulticommodityBatch
            .FromCertifiedEvidence(f.Session, sites, evidence,
                Production(food, wood, stone));
        return (f, sites, physical, Freight(f.Session, sites));
    }

    private static CanonicalPrimitivePhysicalDispositionTrial Trial(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitivePhysicalMulticommodityBatch physical,
        CanonicalSimultaneousFreightReference freight,
        IEnumerable<CanonicalPrimitiveLocalPhysicalUsePolicy> local,
        params CanonicalSimultaneousFreightLot[] lots) =>
        CanonicalPrimitivePhysicalDispositionTrial.FromCertifiedPlans(
            f.Session, sites, physical, freight, local, lots, f.Congestion);

    private static CanonicalSimultaneousFreightLot[] TwoLots(
        CanonicalPrimitivePhysicalMulticommodityBatch physical,
        CanonicalSimultaneousFreightReference freight)
    {
        var offers = new[] {
            Assert.Single(freight.Offers, x => x.Commodity == new CommodityId(7)),
            Assert.Single(freight.Offers, x => x.Commodity == new CommodityId(8))
        };
        return offers.Select(o => {
            var source = physical.Sources.Single(x =>
                x.Source == o.Source && x.Commodity == o.Commodity &&
                x.Activity == o.Activity);
            var q = Math.Min(10_000UL, source.ExperimentallyFeasibleRaw / 4UL);
            Assert.True(q > 0UL);
            return new CanonicalSimultaneousFreightLot(o, q);
        }).ToArray();
    }

    [Fact]
    public void FullLocalUseHasExactPhysicalConservationWithoutInventedStock()
    {
        var (f, sites, physical, freight) = Build();
        var save = f.Session.ExportSaveJson();
        var trial = Trial(f, sites, physical, freight, Local(1000, 1000, 1000));
        Assert.Equal(physical.Sources.Count, trial.Sources.Count);
        Assert.Equal(3, trial.Commodities.Count);
        Assert.Empty(trial.Buyers);
        Assert.Null(trial.InstitutionalFreightPreflight);
        Assert.False(trial.RecordsUnallocatedMaterialsAsInventory);
        Assert.False(trial.CreatesActualExtractionOrDelivery);
        Assert.False(trial.ChargesActualFreightPayment);
        Assert.False(trial.ComputesWoodOrStoneNominalPrices);
        Assert.False(trial.ImplementsAbstractPurchasingPower);
        Assert.False(trial.CanAuthorizeSettlement);
        Assert.True(trial.UsesOnlyLiveWorkforceCapacity);
        Assert.True(trial.VerifiesInstitutionalFreightRights);
        Assert.True(trial.AllowsPlannedPhysicalUseAboveDemandPressure);
        foreach (var source in trial.Sources)
        {
            Assert.Equal(source.FeasibleRaw, source.ProposedLocalUseRaw);
            Assert.Equal(BigInteger.Zero, source.ProposedRemoteDeliveryRaw);
            Assert.Equal(0UL, source.RemainingFeasibleNotExtractedRaw);
            Assert.Equal(source.InfeasibleNotExtractedRaw, source.TotalNotExtractedRaw);
            Assert.Equal(new BigInteger(source.PotentialRaw),
                (BigInteger)source.ProposedLocalUseRaw +
                    source.ProposedRemoteDeliveryRaw + source.TotalNotExtractedRaw);
        }
        Assert.All(trial.Commodities, x =>
            Assert.Equal(x.TotalPotentialRaw,
                x.ProposedLocalUseRaw + x.ProposedRemoteDeliveryRaw +
                    x.TotalNotExtractedRaw));
        Assert.All(trial.Commodities.Where(x =>
            x.Activity != EconomicActivityKind.Agriculture), x =>
        {
            Assert.Null(x.IndependentlyExecutedLocalFoodPurchaseRaw);
            Assert.Null(x.IndependentlyExecutedFoodPaymentMicrocredits);
        });
        trial.VerifyIndependent();
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void TwoDistinctLiveFreightLotsCompeteWithLocalUseForSameFinitePools()
    {
        var (f, sites, physical, freight) = Build();
        var lots = TwoLots(physical, freight);
        var save = f.Session.ExportSaveJson();
        var trial = Trial(f, sites, physical, freight, Local(), lots);
        Assert.NotNull(trial.InstitutionalFreightPreflight);
        Assert.Equal(2, trial.ProposedRemoteLots.Count);
        Assert.Equal(2, trial.Buyers.Count);
        Assert.All(lots, lot =>
        {
            var source = trial.Sources.Single(x =>
                x.Source == lot.Offer.Source &&
                x.Commodity == lot.Offer.Commodity &&
                x.Activity == lot.Offer.Activity);
            Assert.Equal(new BigInteger(lot.RawQuantity),
                source.ProposedRemoteDeliveryRaw);
            Assert.True(source.ProposedLocalUseRaw > 0UL);
            Assert.True((BigInteger)source.ProposedLocalUseRaw +
                source.ProposedRemoteDeliveryRaw <= source.FeasibleRaw);
            Assert.True(source.TotalNotExtractedRaw >=
                source.InfeasibleNotExtractedRaw);
            var buyer = trial.Buyers.Single(x =>
                x.Buyer == lot.Offer.Buyer &&
                x.Commodity == lot.Offer.Commodity);
            Assert.Equal(new BigInteger(lot.RawQuantity),
                buyer.ProposedRemoteArrivalRaw);
            Assert.Equal(new BigInteger(lot.Offer.BuyerPriceDemandSignalRaw),
                buyer.ProjectedDemandPressureRaw);
        });
        Assert.Single(trial.InstitutionalFreightPreflight!.CorridorLoads);
        Assert.False(trial.InstitutionalFreightPreflight.ProducesActualDeliveryOrInventory);
        Assert.False(trial.InstitutionalFreightPreflight.CanAuthorizeRealSettlement);
        trial.VerifyIndependent();
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void ExplicitRemoteShipmentCannotUseTheSameFullLocalProductionTwice()
    {
        var (f, sites, physical, freight) = Build();
        var lots = TwoLots(physical, freight);
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight,
                Local(1000, 1000, 1000), lots));
        var successful = Trial(f, sites, physical, freight, Local(0, 0, 0), lots);
        Assert.All(successful.Sources, x =>
            Assert.Equal(0UL, x.ProposedLocalUseRaw));
        successful.VerifyIndependent();
    }

    [Fact]
    public void ZeroFeasibleCapacityDoesNotPermitShipmentsOrLocalExtraction()
    {
        var (f, sites, physical, freight) = Build(0, 0, 0);
        var empty = Trial(f, sites, physical, freight,
            Local(1000, 1000, 1000));
        Assert.All(empty.Sources, x => {
            Assert.Equal(0UL, x.ProposedLocalUseRaw);
            Assert.Equal(x.PotentialRaw, x.TotalNotExtractedRaw);
        });
        var offer = Assert.Single(freight.Offers, x =>
            x.Commodity == new CommodityId(7));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, Local(0, 0, 0),
                new CanonicalSimultaneousFreightLot(offer, 1UL)));
    }

    [Fact]
    public void UnauthorizedForgedDuplicateAndZeroRemoteOffersAreRejected()
    {
        var (f, sites, physical, freight) = Build();
        var offer = Assert.Single(freight.Offers, x =>
            x.Commodity == new CommodityId(7));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, Local(0, 0, 0),
                new CanonicalSimultaneousFreightLot(offer with { }, 1UL)));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, Local(0, 0, 0),
                new CanonicalSimultaneousFreightLot(offer, 1UL),
                new CanonicalSimultaneousFreightLot(offer, 1UL)));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, Local(0, 0, 0),
                new CanonicalSimultaneousFreightLot(offer, 0UL)));
    }

    [Fact]
    public void InvalidAndDuplicateLocalPoliciesFailClosed()
    {
        var (f, sites, physical, freight) = Build();
        var p = Local();
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, p.Take(2)));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight,
                p.Take(2).Concat([p[0]])));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, Local(-1, 300, 400)));
        Assert.Throws<ArgumentException>(() =>
            Trial(f, sites, physical, freight, Local(250, 1001, 400)));
    }

    [Fact]
    public void PermutedLocalPoliciesAndLotsHaveExactlyTheSameTrial()
    {
        var (f, sites, physical, freight) = Build();
        var lots = TwoLots(physical, freight);
        var a = Trial(f, sites, physical, freight, Local(), lots);
        var b = Trial(f, sites, physical, freight, Local().Reverse(),
            lots.Reverse().ToArray());
        Assert.Equal(a.Sources, b.Sources);
        Assert.Equal(a.Commodities, b.Commodities);
        Assert.Equal(a.Buyers, b.Buyers);
        Assert.Equal(a.LocalPolicies, b.LocalPolicies);
        Assert.Equal(a.ProposedRemoteLots, b.ProposedRemoteLots);
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void ForeignPolicyForeignSessionNextTurnOrNetworkMutationNeverReuseProof()
    {
        var (f, sites, physical, freight) = Build();
        var trial = Trial(f, sites, physical, freight, Local());
        var differentPolicy = f.Session.DiscoverPrimitiveEconomicSites(
            SitePolicy(woodDemand: 300_000));
        Assert.Throws<InvalidOperationException>(() =>
            Trial(f, differentPolicy, physical, freight, Local()));
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            trial.RequireCurrent(restored));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            trial.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            trial.VerifyIndependent());
    }

    [Fact]
    public void NextMovementTurnInvalidatesPreviousCyclePlan()
    {
        var (f, sites, physical, freight) = Build();
        var trial = Trial(f, sites, physical, freight, Local());
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            trial.RequireCurrent(f.Session));
    }

    [Fact]
    public void ReadOnlyScenarioChangesNoSavedWorldOrExecutedFoodWitness()
    {
        var (f, sites, physical, freight) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        foreach (var rate in new[] { 0, 100, 250, 500, 900, 1000 })
        {
            var plan = Trial(f, sites, physical, freight,
                Local(rate, rate, rate));
            Assert.All(plan.Sources, x =>
                Assert.Equal(new BigInteger(x.PotentialRaw),
                    (BigInteger)x.ProposedLocalUseRaw +
                    x.ProposedRemoteDeliveryRaw + x.TotalNotExtractedRaw));
            Assert.Equal(physical.Commodities.Select(x =>
                x.IndependentlyExecutedLocalPurchaseRaw),
                plan.Commodities.Select(x =>
                x.IndependentlyExecutedLocalFoodPurchaseRaw));
            plan.VerifyIndependent();
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
