using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs94PhysicalMulticommodityBatchTests
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

    private static CanonicalPrimitivePhysicalUtilization[] Rates(
        int food = 500, int wood = 750, int stone = 1000) => new[] {
            new CanonicalPrimitivePhysicalUtilization(
                EconomicActivityKind.Agriculture, food),
            new CanonicalPrimitivePhysicalUtilization(
                EconomicActivityKind.Forestry, wood),
            new CanonicalPrimitivePhysicalUtilization(
                EconomicActivityKind.Mining, stone)
        };

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveCommodityExecutionBoundary Evidence,
        CanonicalPrimitivePhysicalMulticommodityBatch Batch) Build(
        int food = 500, int wood = 750, int stone = 1000)
    {
        var (f, _, _, sample) = GaVs92ExecutedFoodSampleAuditTests.Build();
        var sites = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var evidence = CanonicalPrimitiveCommodityExecutionBoundary
            .FromCertifiedEvidence(f.Session, sites, sample);
        var batch = CanonicalPrimitivePhysicalMulticommodityBatch
            .FromCertifiedEvidence(f.Session, sites, evidence,
                Rates(food, wood, stone));
        return (f, sites, evidence, batch);
    }

    [Fact]
    public void LiveWholeWorldLedgerConservesEachCommodityAndEachSource()
    {
        var (f, sites, evidence, batch) = Build();
        Assert.Equal(sites.Sites.Count * 3, batch.Sources.Count);
        Assert.Equal(3, batch.Commodities.Count);
        Assert.True(batch.IsReadOnlyPhysicalExperiment);
        Assert.True(batch.HasLivePhysicalWorkforceProvenance);
        Assert.False(batch.LimitsConsumptionToProjectedDemand);
        Assert.False(batch.CreatesInventoriesOrInvoluntaryStocks);
        Assert.False(batch.ExecutesProductionOrSales);
        Assert.False(batch.DerivesActualWoodOrStonePrices);
        Assert.False(batch.ImplementsAbstractPurchasingPower);
        Assert.False(batch.CanAuthorizeSettlement);
        Assert.Equal(f.Session.State.MovementTurn,
            batch.CompletedEconomicCycleMovementTurn);
        foreach (var row in batch.Sources)
        {
            Assert.Equal(row.PotentialRaw,
                checked(row.ExperimentallyFeasibleRaw +
                    row.CapacityNotExtractedRaw));
            Assert.True(row.AssignedActivityWorkers <= row.ResidentWorkers);
            Assert.Equal(
                (ulong)((BigInteger)row.PotentialRaw *
                    batch.Policies.Single(x => x.Activity == row.Activity).Permille /
                    CanonicalPrimitivePhysicalMulticommodityBatch.PermilleScale),
                row.ExperimentallyFeasibleRaw);
        }
        foreach (var commodity in batch.Commodities)
        {
            var witnessed = evidence.Commodities.Single(x =>
                x.Commodity == commodity.Commodity);
            Assert.Equal(witnessed.ProjectedPrimitivePotentialRaw,
                commodity.PotentialRaw);
            Assert.Equal(witnessed.ProjectedResidentDemandPressureRaw,
                commodity.ProjectedDemandPressureRaw);
            Assert.Equal(witnessed.ResidentProducerWorkers,
                commodity.AssignedActivityWorkers);
            Assert.Equal(commodity.PotentialRaw,
                commodity.ExperimentallyFeasibleRaw +
                    commodity.CapacityNotExtractedRaw);
            Assert.Equal(commodity.ExperimentallyFeasibleRaw,
                batch.Sources.Where(x => x.Commodity == commodity.Commodity)
                    .Aggregate(BigInteger.Zero,
                        (sum, x) => sum + x.ExperimentallyFeasibleRaw));
            Assert.Equal(witnessed.ActualLocalPurchasedRaw,
                commodity.IndependentlyExecutedLocalPurchaseRaw);
            Assert.Equal(witnessed.ActuallyPaidMicrocredits,
                commodity.IndependentlyPaidMicrocredits);
            Assert.Equal(witnessed.ExecutedNominalUnitId,
                commodity.ExecutedNominalUnitId);
        }
        batch.VerifyIndependent();
    }

    [Fact]
    public void ZeroAndFullUtilizationArePhysicalNotDemandOrCashLimit()
    {
        var (_, _, evidence, zero) = Build(0, 0, 0);
        Assert.All(zero.Sources, x => {
            Assert.Equal(0UL, x.ExperimentallyFeasibleRaw);
            Assert.Equal(x.PotentialRaw, x.CapacityNotExtractedRaw);
        });
        Assert.All(zero.Commodities, x =>
            Assert.Equal(BigInteger.Zero, x.ExperimentallyFeasibleRaw));
        var (_, _, _, full) = Build(1000, 1000, 1000);
        Assert.All(full.Sources, x => {
            Assert.Equal(x.PotentialRaw, x.ExperimentallyFeasibleRaw);
            Assert.Equal(0UL, x.CapacityNotExtractedRaw);
        });
        Assert.All(full.Commodities, x =>
            Assert.Equal(x.PotentialRaw, x.ExperimentallyFeasibleRaw));
        Assert.All(full.Commodities, x =>
            Assert.True(x.ProjectedDemandPressureRaw >= 0));
        // Positive actual cash-food receipts remain independent of an
        // experimental zero-utilization scenario.
        Assert.Equal(evidence.Commodities[0].ActualLocalPurchasedRaw,
            zero.Commodities.Single(x =>
                x.Activity == EconomicActivityKind.Agriculture)
                .IndependentlyExecutedLocalPurchaseRaw);
    }

    [Fact]
    public void InvalidOrMissingPrimitiveActivityPoliciesAreRejected()
    {
        var (f, sites, evidence, _) = Build();
        var duplicate = Rates();
        duplicate[1] = duplicate[0];
        Assert.Throws<ArgumentException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, sites, evidence, duplicate));
        Assert.Throws<ArgumentException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, sites, evidence,
                    Rates().Take(2)));
        Assert.Throws<ArgumentException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, sites, evidence,
                    Rates(-1, 750, 1000)));
        Assert.Throws<ArgumentException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, sites, evidence,
                    Rates(500, 1001, 1000)));
    }

    [Fact]
    public void DifferentRateOrderHasIdenticalIndependentDeterministicLedger()
    {
        var (f, sites, evidence, batch) = Build();
        var other = CanonicalPrimitivePhysicalMulticommodityBatch
            .FromCertifiedEvidence(f.Session, sites, evidence,
                Rates().Reverse());
        Assert.Equal(batch.Sources, other.Sources);
        Assert.Equal(batch.Commodities, other.Commodities);
        Assert.Equal(batch.Policies, other.Policies);
        other.VerifyIndependent();
    }

    [Fact]
    public void ForeignSaveOrNextTurnInvalidatesCycleBoundPhysicalBatch()
    {
        var (f, sites, evidence, batch) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            batch.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(restored, sites, evidence, Rates()));
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            batch.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            batch.VerifyIndependent());
    }

    [Fact]
    public void NetworkChangeInvalidatesBatchWithoutProducingOrSelling()
    {
        var (f, sites, evidence, batch) = Build();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            batch.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, sites, evidence, Rates()));
    }

    [Fact]
    public void DifferentLiveSiteDiscoveryCannotBorrowVs93CertifiedEvidence()
    {
        var (f, _, evidence, _) = Build();
        var substitute = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, substitute, evidence, Rates()));
    }

    [Fact]
    public void MultipleRateScenariosNeverMutateSaveOrChangeActualCashProof()
    {
        var (f, sites, evidence, reference) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        foreach (var rate in new[] { 0, 123, 500, 999, 1000 })
        {
            var scenario = CanonicalPrimitivePhysicalMulticommodityBatch
                .FromCertifiedEvidence(f.Session, sites, evidence,
                    Rates(rate, rate, rate));
            scenario.VerifyIndependent();
            Assert.Equal(reference.Commodities.Select(x =>
                    x.IndependentlyExecutedLocalPurchaseRaw),
                scenario.Commodities.Select(x =>
                    x.IndependentlyExecutedLocalPurchaseRaw));
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
