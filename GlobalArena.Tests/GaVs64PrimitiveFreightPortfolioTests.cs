using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs64PrimitiveFreightPortfolioTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static FoodCommodityCatalog Catalog(ulong foodGrams = 1) => new(
        new[] {
            new FoodCommodityDefinition(Food, "food", foodGrams),
            new FoodCommodityDefinition(Wood, "wood", 1),
            new FoodCommodityDefinition(Stone, "stone", 1)
        });

    private static CanonicalPrimitiveSiteDiscoveryPolicy DiscoveryPolicy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] {
            new CanonicalPrimitiveDemandRate(Food,
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(Wood, 125_000),
            new CanonicalPrimitiveDemandRate(Stone, 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static CanonicalEligibleFreightDiscoveryPolicy FreightProfile(
        CommodityId commodity, EconomicActivityKind activity,
        ulong technologyId = 101, ulong foodGrams = 1,
        ExperimentalLandInfrastructureKind infrastructure =
            ExperimentalLandInfrastructureKind.StoneRoad)
    {
        var pricing = new ExperimentalFreightTechnologyPricing(
            technologyId, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
        return new(commodity, activity, pricing, infrastructure, Catalog(foodGrams));
    }

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        EconomicPointId Source, EconomicPointId Buyer) Prepared()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var sourceTile = f.Anchors[f.Flows.SupplyFlows.Single().EconomicPointId];
        var buyerTile = f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId];
        var source = CanonicalPrimitiveSiteDiscovery.SiteId(sourceTile);
        var buyer = CanonicalPrimitiveSiteDiscovery.SiteId(buyerTile);
        f.Session.TransportNetwork.SetEntitlement(
            f.Registry().Entitlements.Single() with {
                Source = source, Destination = buyer, ServiceId = 77
            });
        f.Session.ReassignWorkers(sourceTile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1);
        var sites = f.Session.DiscoverPrimitiveEconomicSites(DiscoveryPolicy());
        return (f, sites, source, buyer);
    }

    private static CanonicalPrimitiveFreightPortfolio Discover(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalEligibleFreightDiscoveryPolicy> profiles) =>
        session.DiscoverPrimitiveFreightPortfolio(
            sites, profiles, "VS56-PURCHASING", 1,
            "VS56-CALIBRATION", 1, "GA-VS56-NOMINAL-LAB");

    [Fact]
    public void MultipleCommoditiesShareOneRealCarrierWithoutDoubleCountingItsRights()
    {
        var (f, sites, source, buyer) = Prepared();
        var save = f.Session.ExportSaveJson();
        var portfolio = Discover(f.Session, sites, new[] {
            FreightProfile(Wood, EconomicActivityKind.Forestry),
            FreightProfile(Food, EconomicActivityKind.Agriculture),
            FreightProfile(Stone, EconomicActivityKind.Mining)
        });
        Assert.Equal(new[] { Food, Wood, Stone },
            portfolio.Slices.Select(x => x.Policy.Commodity));
        Assert.Equal(new[] { Food, Wood },
            portfolio.CandidateUses.Select(x => x.Commodity));
        var service = Assert.Single(portfolio.SharedServices);
        Assert.Equal(77UL, service.ResourceId);
        Assert.Equal(new[] { Food, Wood },
            service.CompetingUses.Select(x => x.Commodity));
        var asset = Assert.Single(portfolio.SharedAssets);
        Assert.Equal(31UL, asset.ResourceId);
        Assert.Equal(new[] { Food, Wood },
            asset.CompetingUses.Select(x => x.Commodity));
        Assert.All(portfolio.CandidateUses, row => {
            Assert.Equal(77UL, row.ServiceId);
            Assert.Equal(source, row.Source);
            Assert.Equal(buyer, row.Buyer);
            Assert.True(row.MaximumRawQuantity > 0);
        });
        Assert.Empty(portfolio.Slices.Single(x =>
            x.Policy.Commodity == Stone).Discovery.Services);
        Assert.Equal(2, portfolio.RegisteredRightsInspectedPerSlice);
        Assert.Equal(2, portfolio.TotalEligibleIntents);
        Assert.Equal(2, portfolio.TotalSourceTreeBuilds);
        Assert.Equal(0, portfolio.TotalUnreachablePhysicalServices);
        Assert.True(portfolio.TotalExaminedArcs > 0);
        Assert.False(portfolio.CanAuthorizeRealSettlement);
        Assert.False(portfolio.CanCombineIndependentTradePreviews);
        portfolio.RequireCurrent(f.Session);
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.Empty(typeof(CanonicalPrimitiveFreightPortfolio).GetConstructors());
    }

    [Fact]
    public void DeclarationOrderNeverAffectsCandidateOrConflictOrdering()
    {
        var (f, sites, _, _) = Prepared();
        var food = FreightProfile(Food, EconomicActivityKind.Agriculture);
        var wood = FreightProfile(Wood, EconomicActivityKind.Forestry);
        var forward = Discover(f.Session, sites, new[] { food, wood });
        var reverse = Discover(f.Session, sites, new[] { wood, food });
        Assert.Equal(forward.CandidateUses, reverse.CandidateUses);
        Assert.Equal(forward.SharedServices.Select(x => x.ResourceId),
            reverse.SharedServices.Select(x => x.ResourceId));
        Assert.Equal(forward.SharedAssets.Select(x => x.ResourceId),
            reverse.SharedAssets.Select(x => x.ResourceId));
        forward.RequireCurrent(f.Session);
        reverse.RequireCurrent(f.Session);
    }

    [Fact]
    public void UnregisteredRoutesRemainUnavailableAcrossEveryProfile()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var sites = f.Session.DiscoverPrimitiveEconomicSites(DiscoveryPolicy());
        var before = f.Session.ExportSaveJson();
        var inventory = Discover(f.Session, sites, new[] {
            FreightProfile(Food, EconomicActivityKind.Agriculture),
            FreightProfile(Wood, EconomicActivityKind.Forestry)
        });
        Assert.Empty(inventory.CandidateUses);
        Assert.Empty(inventory.SharedServices);
        Assert.Empty(inventory.SharedAssets);
        Assert.Equal(0, inventory.TotalEligibleIntents);
        Assert.Equal(0, inventory.TotalSourceTreeBuilds);
        Assert.Equal(before, f.Session.ExportSaveJson());
    }

    [Fact]
    public void DuplicateInconsistentOrUnboundedProfilesAreRejectedBeforeRouteDiscovery()
    {
        var (f, sites, _, _) = Prepared();
        var food = FreightProfile(Food, EconomicActivityKind.Agriculture);
        Assert.Throws<ArgumentException>(() =>
            Discover(f.Session, sites, Array.Empty<CanonicalEligibleFreightDiscoveryPolicy>()));
        Assert.Throws<ArgumentException>(() => Discover(f.Session, sites, [food, food]));
        Assert.Throws<ArgumentException>(() => Discover(f.Session, sites, [
            food, FreightProfile(Food, EconomicActivityKind.Agriculture, 102, foodGrams: 2)
        ]));
        Assert.Throws<ArgumentException>(() => Discover(f.Session, sites, [
            FreightProfile(Food, EconomicActivityKind.Agriculture,
                infrastructure: ExperimentalLandInfrastructureKind.Highway)
        ]));
        var tooMany = Enumerable.Range(101, CanonicalPrimitiveFreightPortfolio.MaximumProfiles + 1)
            .Select(i => FreightProfile(Food, EconomicActivityKind.Agriculture,
                technologyId: (ulong)i));
        Assert.Throws<ArgumentException>(() => Discover(f.Session, sites, tooMany));
    }

    [Fact]
    public void PhysicalNetworkChangeAndSaveRestoreInvalidateEveryPortfolioSnapshot()
    {
        var (f, sites, _, _) = Prepared();
        var profiles = new[] {
            FreightProfile(Food, EconomicActivityKind.Agriculture),
            FreightProfile(Wood, EconomicActivityKind.Forestry)
        };
        var inventory = Discover(f.Session, sites, profiles);
        var saved = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => inventory.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() =>
            Discover(restored, sites, profiles));
        var restoredSites = restored.DiscoverPrimitiveEconomicSites(DiscoveryPolicy());
        var restoredInventory = Discover(restored, restoredSites, profiles);
        Assert.Equal(inventory.CandidateUses, restoredInventory.CandidateUses);
        Assert.Equal(saved, restored.ExportSaveJson());
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => inventory.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            Discover(f.Session, sites, profiles));
        var newSites = f.Session.DiscoverPrimitiveEconomicSites(DiscoveryPolicy());
        var unavailable = Discover(f.Session, newSites, profiles);
        Assert.Empty(unavailable.CandidateUses);
        Assert.Equal(2, unavailable.TotalUnreachablePhysicalServices);
        Assert.False(unavailable.CanCombineIndependentTradePreviews);
    }

    [Fact]
    public void ActivityReallocationRecomputesOnlyRealAvailableCommodityUses()
    {
        var (f, sites, source, _) = Prepared();
        var food = FreightProfile(Food, EconomicActivityKind.Agriculture);
        var wood = FreightProfile(Wood, EconomicActivityKind.Forestry);
        var initial = Discover(f.Session, sites, [food, wood]);
        Assert.Equal(new[] { Food, Wood },
            initial.CandidateUses.Select(x => x.Commodity));
        var ag = sites.At(source)!.AgriculturalWorkers;
        f.Session.ReassignWorkers(sites.At(source)!.Tile,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, ag);
        Assert.Throws<InvalidOperationException>(() => initial.RequireCurrent(f.Session));
        var updatedSites = f.Session.DiscoverPrimitiveEconomicSites(DiscoveryPolicy());
        var updated = Discover(f.Session, updatedSites, [food, wood]);
        Assert.Equal(new[] { Wood },
            updated.CandidateUses.Select(x => x.Commodity));
        Assert.Empty(updated.SharedServices);
        Assert.Empty(updated.SharedAssets);
    }
}
