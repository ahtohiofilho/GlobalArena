using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// Reuses the actual closed V12 food cycles, current VS93 evidence, VS94
/// physical capacities and authenticated VS81 freight for every VS96 case.
/// </summary>
internal static class GaVs96CertifiedFixture
{
    internal sealed record Data(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitivePhysicalMulticommodityBatch Physical,
        CanonicalSimultaneousFreightReference Freight,
        CanonicalPrimitivePhysicalDispositionTrial Disposition);

    internal static Data Build(
        long woodDemandRaw = 125_000,
        int foodUtilization = 800,
        int woodUtilization = 650,
        int stoneUtilization = 1000,
        int localFood = 250,
        int localWood = 300,
        int localStone = 400,
        bool withRemote = true)
    {
        var (fixture, _, _, sample) = GaVs92ExecutedFoodSampleAuditTests.Build();
        var food = new CommodityId(7);
        var wood = new CommodityId(8);
        var stone = new CommodityId(9);
        var policy = new CanonicalPrimitiveSiteDiscoveryPolicy(
            new EconomicPrimitiveCommodityMapping(food, wood, stone),
            new[] {
                new CanonicalPrimitiveDemandRate(food,
                    checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                new CanonicalPrimitiveDemandRate(wood, woodDemandRaw),
                new CanonicalPrimitiveDemandRate(stone, 250_000)
            }, new FoodTransportCostFraction(1, 1));
        var sites = fixture.Session.DiscoverPrimitiveEconomicSites(policy);
        var evidence = CanonicalPrimitiveCommodityExecutionBoundary
            .FromCertifiedEvidence(fixture.Session, sites, sample);
        var physical = CanonicalPrimitivePhysicalMulticommodityBatch
            .FromCertifiedEvidence(fixture.Session, sites, evidence,
                new[] {
                    new CanonicalPrimitivePhysicalUtilization(
                        EconomicActivityKind.Agriculture, foodUtilization),
                    new CanonicalPrimitivePhysicalUtilization(
                        EconomicActivityKind.Forestry, woodUtilization),
                    new CanonicalPrimitivePhysicalUtilization(
                        EconomicActivityKind.Mining, stoneUtilization)
                });
        var pricing = new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(food, "food", 1),
            new FoodCommodityDefinition(wood, "wood", 1),
            new FoodCommodityDefinition(stone, "stone", 1)
        });
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            fixture.Session, sites, new[] {
                new CanonicalEligibleFreightDiscoveryPolicy(
                    food, EconomicActivityKind.Agriculture, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad, catalog),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    wood, EconomicActivityKind.Forestry, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad, catalog),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    stone, EconomicActivityKind.Mining, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad, catalog)
            }, "VS56-PURCHASING", 1,
            "VS56-CALIBRATION", 1, "GA-VS56-NOMINAL-LAB");

        var lots = withRemote ? freight.Offers.Where(x =>
            x.Commodity == food || x.Commodity == wood)
            .Select(o => {
                var source = physical.Sources.Single(x =>
                    x.Source == o.Source && x.Commodity == o.Commodity &&
                    x.Activity == o.Activity);
                var q = Math.Min(1_000UL, source.ExperimentallyFeasibleRaw / 4UL);
                if (q == 0)
                    throw new InvalidOperationException(
                        "Positive reference freight scenario requires real feasible source capacity.");
                return new CanonicalSimultaneousFreightLot(o, q);
            }).ToArray() : Array.Empty<CanonicalSimultaneousFreightLot>();
        if (withRemote && lots.Length != 2)
            throw new InvalidOperationException(
                "VS96 fixture requires both actual food and wood freight rights.");
        var disposition = CanonicalPrimitivePhysicalDispositionTrial
            .FromCertifiedPlans(fixture.Session, sites, physical, freight,
                new[] {
                    new CanonicalPrimitiveLocalPhysicalUsePolicy(
                        EconomicActivityKind.Agriculture, localFood),
                    new CanonicalPrimitiveLocalPhysicalUsePolicy(
                        EconomicActivityKind.Forestry, localWood),
                    new CanonicalPrimitiveLocalPhysicalUsePolicy(
                        EconomicActivityKind.Mining, localStone)
                }, lots, fixture.Congestion);
        return new Data(fixture, sites, physical, freight, disposition);
    }

    internal static CanonicalPrimitiveSimultaneousAbsorptionTrial Observe(
        Data d) => CanonicalPrimitiveSimultaneousAbsorptionTrial
            .FromCertifiedDisposition(d.Fixture.Session, d.Sites, d.Disposition);
}

public sealed class GaVs96SimultaneousMarketAbsorptionTests
{
    [Fact]
    public void AllMarketsMergeLocalAndRealServiceArrivalsWithNoDoubleCount()
    {
        var d = GaVs96CertifiedFixture.Build();
        var before = d.Fixture.Session.ExportSaveJson();
        var result = GaVs96CertifiedFixture.Observe(d);

        Assert.Equal(d.Sites.Flows.DemandFlows.Count, result.Markets.Count);
        Assert.Equal(3, result.Commodities.Count);
        Assert.True(result.UsesAuthenticatedVs95LocalAndRemoteFlows);
        Assert.True(result.HypotheticallyAbsorbsAllProposedPhysicalQuantity);
        Assert.False(result.TreatsDemandSignalAsPhysicalConsumptionCap);
        Assert.False(result.CreatesUnintentionalInventory);
        Assert.False(result.InventsUnplannedExtraction);
        Assert.False(result.ReportsNominalWoodOrStonePrices);
        Assert.False(result.ConvertsRelativePriceToRealMicrocredits);
        Assert.False(result.ClaimsHypotheticalAbsorptionAsExecutedSale);
        Assert.False(result.ImplementsAbstractPurchasingPower);
        Assert.False(result.MutatesCanonicalWorldOrSave);
        Assert.False(result.CanAuthorizeSettlement);

        var expectedLocal = d.Disposition.Sources.GroupBy(x =>
            (Market: x.Source, x.Commodity)).ToDictionary(
            g => g.Key, g => g.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ProposedLocalUseRaw));
        var expectedRemote = d.Disposition.ProposedRemoteLots.GroupBy(x =>
            (Market: x.Offer.Buyer, x.Offer.Commodity)).ToDictionary(
            g => g.Key, g => g.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.RawQuantity));
        foreach (var m in result.Markets)
        {
            expectedLocal.TryGetValue((m.Market, m.Commodity), out var local);
            expectedRemote.TryGetValue((m.Market, m.Commodity), out var remote);
            Assert.Equal(local, m.ProposedLocalAbsorptionRaw);
            Assert.Equal(remote, m.ProposedRemoteAbsorptionRaw);
            Assert.Equal(local + remote, m.TotalHypotheticallyAbsorbedRaw);
            Assert.Equal(d.Sites.Flows.DemandFlows.Single(x =>
                x.EconomicPointId == m.Market &&
                x.CommodityId == m.Commodity).RawQuantity,
                (ulong)m.BasalResidentDemandSignalRaw);
            var mild = CanonicalEndogenousLocalPriceComparison.Calculate(
                m.TotalHypotheticallyAbsorbedRaw, m.BasalResidentDemandSignalRaw, 1);
            var steep = CanonicalEndogenousLocalPriceComparison.Calculate(
                m.TotalHypotheticallyAbsorbedRaw, m.BasalResidentDemandSignalRaw, 2);
            Assert.Equal(mild.Average, m.GentleAveragePriceIndex);
            Assert.Equal(mild.Last, m.GentleLastMarginalPriceIndex);
            Assert.Equal(mild.Revenue, m.GentleGrossRevenueIndex);
            Assert.Equal(steep.Average, m.SteepAveragePriceIndex);
            Assert.Equal(steep.Last, m.SteepLastMarginalPriceIndex);
            Assert.Equal(steep.Revenue, m.SteepGrossRevenueIndex);
            Assert.True(m.GentleGrossRevenueIndex.CompareTo(
                m.SteepGrossRevenueIndex) >= 0);
        }
        Assert.Contains(result.Markets, x =>
            x.ProposedLocalAbsorptionRaw > 0 &&
            x.ProposedRemoteAbsorptionRaw > 0);
        foreach (var row in result.Commodities)
        {
            var original = d.Disposition.Commodities.Single(x =>
                x.Commodity == row.Commodity);
            Assert.Equal(original.TotalPotentialRaw, row.PotentialRaw);
            Assert.Equal(original.TotalFeasibleRaw, row.ExperimentallyFeasibleRaw);
            Assert.Equal(original.ProposedLocalUseRaw, row.LocallyAbsorbedRaw);
            Assert.Equal(original.ProposedRemoteDeliveryRaw, row.RemotelyAbsorbedRaw);
            Assert.Equal(original.TotalNotExtractedRaw, row.NeverExtractedRaw);
            Assert.Equal(row.PotentialRaw,
                row.LocallyAbsorbedRaw + row.RemotelyAbsorbedRaw +
                    row.NeverExtractedRaw);
        }
        result.VerifyIndependent();
        Assert.Equal(before, d.Fixture.Session.ExportSaveJson());
    }

    [Fact]
    public void AllPlannedOfferIsAbsorbedEvenWhenAboveResidentDemandSignal()
    {
        var d = GaVs96CertifiedFixture.Build(
            woodDemandRaw: 1, localWood: 1000, withRemote: false);
        var result = GaVs96CertifiedFixture.Observe(d);
        var wood = result.Markets.Where(x => x.Commodity == new CommodityId(8))
            .ToArray();
        Assert.Contains(wood, x =>
            x.TotalHypotheticallyAbsorbedRaw > x.BasalResidentDemandSignalRaw);
        Assert.All(wood, x => Assert.Equal(x.ProposedLocalAbsorptionRaw,
            x.TotalHypotheticallyAbsorbedRaw));
        Assert.False(result.TreatsDemandSignalAsPhysicalConsumptionCap);
        result.VerifyIndependent();
    }

    [Fact]
    public void NoOffersProduceZeroMarketIndicesNotFictitiousStockOrMoney()
    {
        var d = GaVs96CertifiedFixture.Build(
            foodUtilization: 0, woodUtilization: 0, stoneUtilization: 0,
            withRemote: false);
        var result = GaVs96CertifiedFixture.Observe(d);
        Assert.All(result.Markets, x =>
        {
            Assert.Equal(BigInteger.Zero, x.TotalHypotheticallyAbsorbedRaw);
            Assert.Equal(CanonicalEndogenousPriceIndex.Zero,
                x.GentleGrossRevenueIndex);
            Assert.Equal(CanonicalEndogenousPriceIndex.Zero,
                x.SteepGrossRevenueIndex);
        });
        Assert.All(result.Commodities, x =>
        {
            Assert.Equal(BigInteger.Zero, x.LocallyAbsorbedRaw);
            Assert.Equal(BigInteger.Zero, x.RemotelyAbsorbedRaw);
            Assert.Equal(x.PotentialRaw, x.NeverExtractedRaw);
        });
        result.VerifyIndependent();
    }

    [Fact]
    public void CashFoodReceiptsRemainObservationalAndWoodStoneSettlementUnknown()
    {
        var d = GaVs96CertifiedFixture.Build();
        var result = GaVs96CertifiedFixture.Observe(d);
        foreach (var x in result.Commodities)
        {
            var certified = d.Disposition.Commodities.Single(y =>
                y.Commodity == x.Commodity);
            Assert.Equal(certified.IndependentlyExecutedLocalFoodPurchaseRaw,
                x.IndependentExecutedFoodPurchaseRaw);
            Assert.Equal(certified.IndependentlyExecutedFoodPaymentMicrocredits,
                x.IndependentExecutedFoodPaymentMicrocredits);
            if (x.Activity == EconomicActivityKind.Agriculture)
                Assert.True(x.IndependentExecutedFoodPaymentMicrocredits > 0);
            else
            {
                Assert.Null(x.IndependentExecutedFoodPurchaseRaw);
                Assert.Null(x.IndependentExecutedFoodPaymentMicrocredits);
            }
        }
        result.VerifyIndependent();
    }

    [Fact]
    public void ForeignSamePolicyDiscoveryIsNotValidVs95Authority()
    {
        var d = GaVs96CertifiedFixture.Build();
        var other = d.Fixture.Session.DiscoverPrimitiveEconomicSites(
            new CanonicalPrimitiveSiteDiscoveryPolicy(
                new EconomicPrimitiveCommodityMapping(
                    new CommodityId(7), new CommodityId(8), new CommodityId(9)),
                new[] {
                    new CanonicalPrimitiveDemandRate(new CommodityId(7),
                        checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                    new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
                    new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
                }, new FoodTransportCostFraction(1, 1)));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveSimultaneousAbsorptionTrial
                .FromCertifiedDisposition(d.Fixture.Session, other, d.Disposition));
    }

    [Fact]
    public void ForeignSaveChangedNetworkAndNextTurnCannotReuseMarketProof()
    {
        var d = GaVs96CertifiedFixture.Build();
        var result = GaVs96CertifiedFixture.Observe(d);
        var originalSave = d.Fixture.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(originalSave);
        Assert.Throws<InvalidOperationException>(() =>
            result.RequireCurrent(restored));
        d.Fixture.Session.TransportNetwork.SetAccess(
            d.Fixture.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            result.VerifyIndependent());
        var e = GaVs96CertifiedFixture.Build();
        var other = GaVs96CertifiedFixture.Observe(e);
        e.Fixture.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            other.RequireCurrent(e.Fixture.Session));
    }

    [Fact]
    public void RepeatedIndependentObservationIsReadOnlyAndDeterministic()
    {
        var d = GaVs96CertifiedFixture.Build();
        var initial = GaVs96CertifiedFixture.Observe(d);
        var save = d.Fixture.Session.ExportSaveJson();
        var hash = d.Fixture.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var repeat = GaVs96CertifiedFixture.Observe(d);
            Assert.Equal(initial.Markets, repeat.Markets);
            Assert.Equal(initial.Commodities, repeat.Commodities);
            repeat.VerifyIndependent();
            Assert.Equal(save, d.Fixture.Session.ExportSaveJson());
            Assert.Equal(hash, d.Fixture.Session.CurrentHash);
        }
    }
}
