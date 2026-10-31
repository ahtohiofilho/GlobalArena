using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs63AuthorizedFreightOfferDiscoveryTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);
    private static FoodCommodityCatalog Catalog() => new(new[] {
        new FoodCommodityDefinition(Food, "primitive-food", 1),
        new FoodCommodityDefinition(Wood, "primitive-wood", 1),
        new FoodCommodityDefinition(Stone, "primitive-stone", 1)
    });
    private static CanonicalPrimitiveSiteDiscoveryPolicy SitePolicy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] { new CanonicalPrimitiveDemandRate(Food,
            checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)) },
        new FoodTransportCostFraction(1, 1));

    private static CanonicalEligibleFreightDiscoveryPolicy FreightPolicy(int max = 128) => new(
        Food, EconomicActivityKind.Agriculture,
        new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
        ExperimentalLandInfrastructureKind.StoneRoad, Catalog(), max);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        EconomicPointId Source, EconomicPointId Buyer) Prepared()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var originalSource = f.Flows.SupplyFlows.Single().EconomicPointId;
        var originalBuyer = f.Flows.DemandFlows.Single().EconomicPointId;
        var source = CanonicalPrimitiveSiteDiscovery.SiteId(f.Anchors[originalSource]);
        var buyer = CanonicalPrimitiveSiteDiscovery.SiteId(f.Anchors[originalBuyer]);
        var right = f.Registry().Entitlements.Single() with {
            Source = source, Destination = buyer, ServiceId = 77
        };
        f.Session.TransportNetwork.SetEntitlement(right);
        return (f, f.Session.DiscoverPrimitiveEconomicSites(SitePolicy()), source, buyer);
    }

    private static CanonicalEligibleFreightOfferDiscovery Discover(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites, int maximum = 128) =>
        session.DiscoverEligiblePrimitiveFreight(sites, FreightPolicy(maximum),
            "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1,
            "GA-VS56-NOMINAL-LAB");

    [Fact]
    public void FindsOnlyRegisteredBuiltAndGrantedRoutesWithoutEnumeratingAllSitePairs()
    {
        var (f, sites, source, buyer) = Prepared();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        var found = Discover(f.Session, sites);
        var service = Assert.Single(found.Services);
        Assert.Equal(77UL, service.Offer.ServiceId);
        Assert.Equal(source, service.Entitlement.Source);
        Assert.Equal(buyer, service.Entitlement.Destination);
        Assert.Equal(31UL, Assert.Single(service.Offer.AssetPath));
        Assert.Equal(1, found.EligibleEconomicIntents);
        Assert.Equal(2, found.InspectedRegisteredServices);
        Assert.Equal(1, found.SourceTreeBuilds);
        Assert.True(found.ExaminedArcs > 0);
        Assert.Equal(0, found.UnreachablePhysicalServices);
        Assert.Equal(77UL, Assert.Single(Assert.Single(found.Lanes).Services).ServiceId);
        Assert.Equal(Math.Min(
            sites.At(source)!.Supply.Single(x => x.CommodityId == Food).RawQuantity,
            sites.At(buyer)!.Demand.Single(x => x.CommodityId == Food).RawQuantity),
            service.MaximumRawQuantity);
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.Equal(hash, f.Session.CurrentHash);
        Assert.False(found.CanAuthorizeRealSettlement);
        Assert.Empty(typeof(CanonicalEligibleFreightOfferDiscovery).GetConstructors());
        found.RequireCurrent(f.Session);
    }

    [Fact]
    public void DiscoveredServiceFeedsVs60AuditedTradeWithoutDeclaredLaneOrOffer()
    {
        var (f, sites, source, buyer) = Prepared();
        var found = Discover(f.Session, sites);
        var save = f.Session.ExportSaveJson();
        var quote = f.Quotes[0] with { Buyer = buyer };
        var preview = found.PreviewTrade(Catalog(),
            new[] { new ExperimentalBuyerCommodityPrice(
                buyer, Food, new FoodTransportCostFraction(1, 1)) },
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5, 1),
                new FoodTransportCostFraction(5, 8)),
            new[] { quote }, f.Policy, "GA-VS56-NOMINAL-LAB",
            f.Congestion, 500_000);
        Assert.Equal(buyer, Assert.Single(preview.Trade.Purchasing.Buyers).Buyer);
        Assert.Equal(source, Assert.Single(preview.Trade.Purchasing.Producers).Producer);
        Assert.False(preview.CanAuthorizeRealSettlement);
        Assert.False(preview.Trade.CanAuthorizeRealSettlement);
        preview.RequireCurrent(f.Session);
        found.RequireCurrent(f.Session);
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void UnmatchedLegacyRightsDoNotInventAServiceOrFreeRoad()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var save = f.Session.ExportSaveJson();
        var result = Discover(f.Session, sites);
        Assert.Empty(result.Services);
        Assert.Empty(result.Lanes);
        Assert.Equal(1, result.InspectedRegisteredServices);
        Assert.Equal(0, result.EligibleEconomicIntents);
        Assert.Equal(0, result.SourceTreeBuilds);
        Assert.Throws<InvalidOperationException>(() => result.PreviewTrade(Catalog(),
            Array.Empty<ExperimentalBuyerCommodityPrice>(),
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5, 1),
                new FoodTransportCostFraction(5, 8)),
            Array.Empty<ExperimentalAbstractNominalDeliveredQuote>(),
            f.Policy, "GA-VS56-NOMINAL-LAB", f.Congestion, 500_000));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void AccessClosureMakesRealRouteUnreachableAndInvalidatesOldResult()
    {
        var (f, sites, _, _) = Prepared();
        var approved = Discover(f.Session, sites);
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => approved.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() => Discover(f.Session, sites));
        var refreshed = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var noAccess = Discover(f.Session, refreshed);
        Assert.Empty(noAccess.Services);
        Assert.Equal(1, noAccess.UnreachablePhysicalServices);
        Assert.False(noAccess.CanAuthorizeRealSettlement);
        noAccess.RequireCurrent(f.Session);
    }

    [Fact]
    public void RevokedGrantCannotBeMistakenForInstitutionalAuthorization()
    {
        var (f, sites, _, _) = Prepared();
        var before = Discover(f.Session, sites);
        f.Session.TransportNetwork.RevokeGrant(f.Asset.AssetId,
            f.Grant().AuthorizedOperator);
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(f.Session));
        var current = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        Assert.ThrowsAny<Exception>(() => Discover(f.Session, current));
    }

    [Fact]
    public void BoundAndRestoreRejectForeignOrStaleInventories()
    {
        var (f, sites, _, _) = Prepared();
        var found = Discover(f.Session, sites);
        Assert.Throws<InvalidOperationException>(() => Discover(f.Session, sites, maximum: 1));
        var saved = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => found.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() => Discover(restored, sites));
        var fresh = restored.DiscoverPrimitiveEconomicSites(SitePolicy());
        var rebuilt = Discover(restored, fresh);
        Assert.Equal(found.Services.Select(x => (x.Offer.ServiceId, x.MaximumRawQuantity)),
            rebuilt.Services.Select(x => (x.Offer.ServiceId, x.MaximumRawQuantity)));
        Assert.Equal(saved, restored.ExportSaveJson());
    }

    [Fact]
    public void NewWorkerAllocationInvalidatesOfferInventoryAndRecomputesCapacities()
    {
        var (f, sites, source, _) = Prepared();
        var found = Discover(f.Session, sites);
        Assert.Single(found.Services);
        var origin = sites.At(source)!.Tile;
        var agriculturalWorkers = sites.At(source)!.AgriculturalWorkers;
        f.Session.ReassignWorkers(origin, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, agriculturalWorkers);
        Assert.Throws<InvalidOperationException>(() => found.RequireCurrent(f.Session));
        var fresh = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var newer = Discover(f.Session, fresh);
        Assert.Empty(newer.Services);
        Assert.Equal(0, newer.EligibleEconomicIntents);
        Assert.False(newer.CanAuthorizeRealSettlement);
    }
}
