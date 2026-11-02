using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs94PrimitiveMaterialRealizationBridgeTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveCommodityExecutionBoundary Boundary93,
        CanonicalSimultaneousFreightReference Freight,
        CanonicalRealizedProductionAllocation Trial82,
        CanonicalPrimitiveMaterialRealizationBridge Bridge) Build()
    {
        var (f, sites, _, boundary) =
            GaVs93PrimitiveCommodityExecutionBoundaryTests.Build();
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            f.Session, sites,
            GaVs67IndependentFreightComponentsTests.PrimitiveFreightPolicies(),
            "VS94-PURCHASING", 1, "VS94-CALIBRATION", 1, "VS94-NOMINAL-EXPERIMENT");
        var declarations = sites.Flows.SupplyFlows
            .Where(x => x.Activity is EconomicActivityKind.Forestry or
                EconomicActivityKind.Mining && x.RawQuantity > 0)
            .Take(12)
            .Select(x => {
                var amount = Math.Max(1UL, x.RawQuantity / 2);
                return new CanonicalRealizedProductionDeclaration(
                    x.EconomicPointId, x.CommodityId, x.Activity,
                    amount, amount / 4);
            }).ToArray();
        Assert.NotEmpty(declarations);
        var trial = CanonicalRealizedProductionAllocation.FromSites(
            f.Session, sites, freight, declarations,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        var bridge = CanonicalPrimitiveMaterialRealizationBridge
            .FromCertifiedTrial(f.Session, boundary, trial);
        return (f, sites, boundary, freight, trial, bridge);
    }

    [Fact]
    public void LocalMaterialTrialReconcilesRealSiteCapacityWithoutCreatingSales()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        Assert.Equal(2, bridge.Commodities.Count);
        Assert.True(bridge.IsOnlyHypotheticalPhysicalRealization);
        Assert.True(bridge.PreservesCanonicalFoodCashReceipts);
        Assert.False(bridge.ExposesRealWoodOrStoneSales);
        Assert.False(bridge.InventsWoodOrStoneMonetaryPrices);
        Assert.False(bridge.TreatsNotExtractedPotentialAsStock);
        Assert.False(bridge.ConvertsLocalTrialOffersIntoRealSales);
        Assert.False(bridge.CapsPhysicalDemandOrSalesByProjection);
        Assert.False(bridge.ModifiesWorldSaveWorkersOrStock);
        Assert.False(bridge.AuthorizesSettlementOrCredit);
        foreach (var commodity in bridge.Commodities)
        {
            Assert.Equal(commodity.PotentialRaw,
                commodity.HypotheticallyProducedRaw +
                commodity.PotentialNotExtractedRaw);
            Assert.Equal(commodity.HypotheticallyProducedRaw,
                commodity.DeliberatelyRetainedRaw +
                commodity.HypotheticallyRemotelyShippedRaw +
                commodity.HypotheticallyOfferedLocallyRaw);
            Assert.Null(commodity.ActuallyPurchasedRaw);
            Assert.Null(commodity.ActuallyPaidMicrocredits);
            Assert.Null(commodity.ExecutedNominalUnitId);
            Assert.False(commodity.IsActualMaterialMarketEstablished);
            var original = boundary.Commodities.Single(x =>
                x.Commodity == commodity.Commodity);
            Assert.Equal(original.ProjectedPrimitivePotentialRaw,
                commodity.PotentialRaw);
            Assert.Equal(original.ProjectedResidentDemandPressureRaw,
                commodity.ProjectedDemandPressureRaw);
        }
        Assert.All(bridge.Sources, source => {
            Assert.Equal(source.PotentialRaw,
                source.PotentialNotExtractedRaw +
                source.HypotheticallyProducedRaw);
            Assert.Equal(source.HypotheticallyProducedRaw,
                source.DeliberatelyRetainedRaw +
                source.HypotheticallyRemotelyShippedRaw +
                source.HypotheticallyOfferedLocallyRaw);
            Assert.False(source.HasCanonicalExecutedMaterialReceipt);
        });
        bridge.VerifyIndependent();
    }

    [Fact]
    public void TrialDoesNotEquateWorkerPotentialWithHypotheticallyExtractedOutput()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        Assert.True(bridge.Commodities.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.PotentialNotExtractedRaw) > 0);
        Assert.True(bridge.Commodities.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.HypotheticallyProducedRaw) > 0);
        Assert.All(trial.Sources, allocation => {
            var row = Assert.Single(bridge.Sources.Where(x =>
                x.Source == allocation.Source &&
                x.Commodity == allocation.Commodity &&
                x.Activity == allocation.Activity));
            Assert.Equal(new BigInteger(allocation.HypotheticallyProducedRaw),
                row.HypotheticallyProducedRaw);
            Assert.Equal(new BigInteger(allocation.PotentialNotExtractedRaw),
                row.PotentialNotExtractedRaw);
            Assert.Equal(new BigInteger(allocation.DeliberatelyRetainedRaw),
                row.DeliberatelyRetainedRaw);
            Assert.Equal(allocation.HypotheticallyRemotelyShippedRaw,
                row.HypotheticallyRemotelyShippedRaw);
            Assert.Equal(allocation.OfferedAndLocallyAbsorbedRaw,
                row.HypotheticallyOfferedLocallyRaw);
        });
        Assert.Equal(bridge.Sources.Count,
            sites.Flows.SupplyFlows.Count(x =>
                x.Activity is EconomicActivityKind.Forestry or
                    EconomicActivityKind.Mining));
    }

    [Fact]
    public void ExperimentalFoodProductionCannotBeMisrepresentedAsNewWoodOrStone()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        var food = sites.Flows.SupplyFlows.First(x =>
            x.Activity == EconomicActivityKind.Agriculture &&
            x.RawQuantity > 0);
        var withFood = trial.Declarations.Append(
            new CanonicalRealizedProductionDeclaration(
                food.EconomicPointId, food.CommodityId, food.Activity,
                1UL, 0UL)).ToArray();
        var mixedTrial = CanonicalRealizedProductionAllocation.FromSites(
            f.Session, sites, freight, withFood,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveMaterialRealizationBridge.FromCertifiedTrial(
                f.Session, boundary, mixedTrial));
    }

    [Fact]
    public void VS82StillRefusesToExtractMoreThanPotentialOrRetainMoreThanProduced()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        var item = trial.Declarations[0];
        Assert.Throws<ArgumentException>(() =>
            CanonicalRealizedProductionAllocation.FromSites(
                f.Session, sites, freight,
                [item with { ProducedRaw =
                    checked(sites.Flows.SupplyFlows.Single(x =>
                        x.EconomicPointId == item.Source &&
                        x.CommodityId == item.Commodity &&
                        x.Activity == item.Activity).RawQuantity + 1) }],
                Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion()));
        Assert.Throws<ArgumentException>(() =>
            CanonicalRealizedProductionAllocation.FromSites(
                f.Session, sites, freight,
                [item with {
                    DeliberatelyRetainedRaw = item.ProducedRaw + 1UL }],
                Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion()));
    }

    [Fact]
    public void SameLookingWorkerSiteSnapshotDoesNotSubstituteAuthenticatedOrigin()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        var anotherDiscovery = f.Session.DiscoverPrimitiveEconomicSites(
            new CanonicalPrimitiveSiteDiscoveryPolicy(
                new EconomicPrimitiveCommodityMapping(
                    new CommodityId(7), new CommodityId(8), new CommodityId(9)),
                new[] {
                    new CanonicalPrimitiveDemandRate(new CommodityId(7),
                        checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                    new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
                    new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
                }, new FoodTransportCostFraction(1, 1)));
        var otherFreight = CanonicalSimultaneousFreightReference.FromSites(
            f.Session, anotherDiscovery,
            GaVs67IndependentFreightComponentsTests.PrimitiveFreightPolicies(),
            "VS94-PURCHASING", 1, "VS94-CALIBRATION", 1, "VS94-NOMINAL-EXPERIMENT");
        var otherTrial = CanonicalRealizedProductionAllocation.FromSites(
            f.Session, anotherDiscovery, otherFreight, trial.Declarations,
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveMaterialRealizationBridge.FromCertifiedTrial(
                f.Session, boundary, otherTrial));
    }

    [Fact]
    public void SaveRestoreOrNextTickInvalidatesTheReadOnlyMaterialTrial()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            f.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPrimitiveMaterialRealizationBridge.FromCertifiedTrial(
                restored, boundary, trial));
        f.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            bridge.VerifyIndependent());
    }

    [Fact]
    public void NetworkRevisionInvalidatesTrialEvenIfSaveHasNotChanged()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            bridge.RequireCurrent(f.Session));
    }

    [Fact]
    public void IndependentReplayIsDeterministicWithoutActualMaterialReceipts()
    {
        var (f, sites, boundary, freight, trial, bridge) = Build();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var replay = CanonicalPrimitiveMaterialRealizationBridge
                .FromCertifiedTrial(f.Session, boundary, trial);
            Assert.Equal(bridge.Commodities, replay.Commodities);
            Assert.Equal(bridge.Sources, replay.Sources);
            replay.VerifyIndependent();
            Assert.Equal(save, f.Session.ExportSaveJson());
            Assert.Equal(hash, f.Session.CurrentHash);
        }
    }
}
