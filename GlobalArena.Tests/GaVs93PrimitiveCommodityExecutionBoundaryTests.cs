using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs93PrimitiveCommodityExecutionBoundaryTests
{
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
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalExecutedFoodSampleAudit Audit92,
        CanonicalPrimitiveCommodityExecutionBoundary Boundary) Build()
    {
        var (f, _, _, audit) = GaVs92ExecutedFoodSampleAuditTests.Build();
        var sites = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var boundary = CanonicalPrimitiveCommodityExecutionBoundary
            .FromCertifiedEvidence(f.Session, sites, audit);
        return (f, sites, audit, boundary);
    }

    [Fact]
    public void RealFoodCashEvidenceNeverAuthorizesWoodOrStoneSettlement()
    {
        var (f, sites, audit, boundary) = Build();
        Assert.Equal(3, boundary.Commodities.Count);
        Assert.Equal(f.Session.State.MovementTurn,
            boundary.CompletedEconomicCycleMovementTurn);
        Assert.Equal(sites.ResidentWorkers, boundary.PhysicalResidentWorkers);
        Assert.True(boundary.UsesOnlyCanonicallyExecutedFoodCashReceipts);
        Assert.False(boundary.TreatsUnextractedPotentialAsExecutedQuantity);
        Assert.False(boundary.UsesHypotheticalVs78PriceAsActualPayment);
        Assert.False(boundary.EstablishesPaidPricesForWoodOrStone);
        Assert.False(boundary.MeasuresAllConsumerDemandAsActualSales);
        Assert.False(boundary.ImplementsAbstractPurchasingPower);
        Assert.False(boundary.ChangesActualProductionOrWorkerAssignments);
        Assert.False(boundary.ModifiesCanonicalWorldOrSave);
        Assert.False(boundary.InventsStocksOrCredit);
        Assert.False(boundary.CanAuthorizeSettlement);

        var food = boundary.Commodities.Single(x =>
            x.Activity == EconomicActivityKind.Agriculture);
        Assert.Equal(new CommodityId(7), food.Commodity);
        Assert.Equal(CanonicalCommodityExecutionEvidenceKind
            .ExecutedLocalCashFoodWithPaidPrice, food.EvidenceKind);
        Assert.True(food.HasAuthenticatedExecutedCashPrice);
        Assert.True(food.HasProofOfActualExecutedSales);
        Assert.False(food.IsExecutedCashEvidenceUnavailable);
        Assert.Equal("GA-LEGACY-FOOD-CASH-MICROCREDITS",
            food.ExecutedNominalUnitId);
        Assert.True(food.ActuallyPaidMicrocredits > 0);
        Assert.True(food.ActualLocalPurchasedRaw > 0);

        foreach (var x in boundary.Commodities.Where(x =>
            x.Activity != EconomicActivityKind.Agriculture))
        {
            Assert.True(x.IsExecutedCashEvidenceUnavailable);
            Assert.False(x.HasAuthenticatedExecutedCashPrice);
            Assert.False(x.HasProofOfActualExecutedSales);
            Assert.Null(x.ActualLocalPurchasedRaw);
            Assert.Null(x.ActuallyPaidMicrocredits);
            Assert.Null(x.PurchasedFoodRawWithPositivePayment);
            Assert.Null(x.ExecutedNominalUnitId);
            Assert.Equal(CanonicalCommodityExecutionEvidenceKind
                .HypotheticalOnlyNoCanonicalExecutedMarket, x.EvidenceKind);
        }
        boundary.VerifyIndependent();
    }

    [Fact]
    public void ProjectedCommodityPotentialAndBaselineDemandArePhysicalWorkerDerivedNotPurchased()
    {
        var (f, sites, _, boundary) = Build();
        foreach (var row in boundary.Commodities)
        {
            var potential = sites.Flows.SupplyFlows
                .Where(x => x.CommodityId == row.Commodity &&
                    x.Activity == row.Activity)
                .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
            var demand = sites.Flows.DemandFlows
                .Where(x => x.CommodityId == row.Commodity)
                .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
            Assert.Equal(potential, row.ProjectedPrimitivePotentialRaw);
            Assert.Equal(demand, row.ProjectedResidentDemandPressureRaw);
            Assert.True(row.ProjectedResidentDemandPressureRaw >= 0);
            Assert.True(row.ProjectedPrimitivePotentialRaw >= 0);
            var workers = sites.Sites.Aggregate(0UL, (sum, x) => checked(sum +
                (row.Activity switch {
                    EconomicActivityKind.Agriculture => x.AgriculturalWorkers,
                    EconomicActivityKind.Forestry => x.ForestryWorkers,
                    EconomicActivityKind.Mining => x.MiningWorkers,
                    _ => 0UL
                })));
            Assert.Equal(workers, row.ResidentProducerWorkers);
        }
        Assert.Equal(new[] { new CommodityId(7), new CommodityId(8),
            new CommodityId(9) }, boundary.Commodities.Select(x => x.Commodity));
        Assert.True(boundary.Commodities.All(x =>
            x.ProjectedResidentDemandPressureRaw > 0));
    }

    [Fact]
    public void ExecutedFoodReceiptsReconcileExactlyWhileOtherMarketsRemainUnknown()
    {
        var (f, _, _, boundary) = Build();
        var food = boundary.Commodities[0];
        var witness = CanonicalExecutedLocalFoodPriceWitness
            .FromLatestRealSettlement(f.Session);
        Assert.Equal(new BigInteger(witness.TotalActualLocalFoodRaw),
            food.ActualLocalPurchasedRaw);
        Assert.Equal(witness.TotalActualPaymentsMicrocredits,
            food.ActuallyPaidMicrocredits);
        Assert.Equal(witness.ExecutedReceipts.Aggregate(
            BigInteger.Zero, (sum, r) =>
                sum + (r.ActuallyPaidMicrocredits > 0UL
                    ? r.ExecutedFoodRaw : 0UL)),
            food.PurchasedFoodRawWithPositivePayment);
        Assert.True(food.ActualLocalPurchasedRaw >=
            food.PurchasedFoodRawWithPositivePayment);
        Assert.All(boundary.Commodities.Skip(1), x =>
        {
            Assert.Null(x.ActualLocalPurchasedRaw);
            Assert.Null(x.ActuallyPaidMicrocredits);
        });
        boundary.VerifyIndependent();
    }

    [Fact]
    public void CrossSessionRestorationDoesNotTransplantAnyMulticommodityExecutionProof()
    {
        var (f, sites, audit, boundary) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            boundary.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveCommodityExecutionBoundary.FromCertifiedEvidence(
                restored, sites, audit));
    }

    [Fact]
    public void SubsequentMovementInvalidatesExecutionBoundaryAndCachedPrimitiveFlows()
    {
        var (f, sites, audit, boundary) = Build();
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            boundary.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            boundary.VerifyIndependent());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveCommodityExecutionBoundary.FromCertifiedEvidence(
                f.Session, sites, audit));
    }

    [Fact]
    public void NetworkRevisionInvalidatesExecutedCommodityEvidence()
    {
        var (f, sites, audit, boundary) = Build();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            boundary.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveCommodityExecutionBoundary.FromCertifiedEvidence(
                f.Session, sites, audit));
    }

    [Fact]
    public void RepeatedIndependentEvidenceReconstructionDoesNotMutateSaveOrWorkerProduction()
    {
        var (f, sites, audit, boundary) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var replay = CanonicalPrimitiveCommodityExecutionBoundary
                .FromCertifiedEvidence(f.Session, sites, audit);
            Assert.Equal(boundary.Commodities, replay.Commodities);
            Assert.Equal(boundary.PhysicalResidentWorkers,
                replay.PhysicalResidentWorkers);
            replay.VerifyIndependent();
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
