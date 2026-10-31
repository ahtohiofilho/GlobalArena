using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs62EmergentEconomicSiteDiscoveryTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static CanonicalPrimitiveSiteDiscoveryPolicy Policy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] {
            new CanonicalPrimitiveDemandRate(Food,
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(Wood, 125_000),
            new CanonicalPrimitiveDemandRate(Stone, 250_000)
        },
        new FoodTransportCostFraction(1, 1));

    private static CanonicalPrimitiveSiteDiscoveryPolicy FoodOnlyPolicy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] { new CanonicalPrimitiveDemandRate(Food,
            checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)) },
        new FoodTransportCostFraction(1, 1));

    private static FoodCommodityCatalog Catalog() => new(new[] {
        new FoodCommodityDefinition(Food, "primitive-food", 1),
        new FoodCommodityDefinition(Wood, "primitive-wood", 1),
        new FoodCommodityDefinition(Stone, "primitive-stone", 1)
    });

    [Fact]
    public void AllInhabitedTilesBecomeStableEconomicSitesWithoutManualBindings()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var before = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        var discovery = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var repeated = f.Session.DiscoverPrimitiveEconomicSites(Policy());

        Assert.Equal(2, discovery.Sites.Count);
        Assert.Equal(8UL, discovery.ResidentWorkers);
        Assert.Equal(0UL, discovery.TravellingWorkers);
        Assert.Equal(2, discovery.BuyerAnchors.Count);
        Assert.Equal(discovery.Sites.Count(x => x.Supply.Count > 0),
            discovery.ProducerAnchors.Count);
        Assert.Equal(6, discovery.Flows.DemandFlows.Count);
        Assert.True(discovery.Sites.Select(x => x.Point.Value).SequenceEqual(
            discovery.Sites.Select(x => x.Point.Value).OrderBy(x => x)));
        Assert.Equal(discovery.Sites.Select(x => (x.Point, x.Tile, x.ResidentWorkers)),
            repeated.Sites.Select(x => (x.Point, x.Tile, x.ResidentWorkers)));
        foreach (var site in discovery.Sites)
        {
            Assert.Equal(site.Tile.FineStrategicCellId.Value, site.Point.Value);
            Assert.Equal(f.Session.WorkersAt(site.Tile), site.ResidentWorkers);
            Assert.Equal(3, site.Demand.Count);
            foreach (var demand in site.Demand)
            {
                var rate = Policy().DemandRates.Single(x => x.Commodity == demand.CommodityId);
                Assert.Equal(checked(site.ResidentWorkers * (ulong)rate.RawPerResidentWorker),
                    demand.RawQuantity);
            }
            foreach (var supply in site.Supply)
            {
                var workers = f.Session.State.Allocations.Single(x =>
                    x.TileId == site.Tile && x.Activity == supply.Activity).Workers;
                Assert.Equal(checked(workers * (ulong)f.Session.PrimitiveYieldAt(
                    site.Tile, supply.Activity)), supply.RawQuantity);
            }
        }
        var delta = repeated.ChangesSince(discovery);
        Assert.Empty(delta.Added);
        Assert.Empty(delta.Removed);
        Assert.Empty(delta.Updated);
        Assert.Equal(hash, discovery.WorldHash);
        Assert.Equal(before, f.Session.ExportSaveJson());
        Assert.False(discovery.CanAuthorizeRealSettlement);
        discovery.RequireCurrent(f.Session);
        Assert.Empty(typeof(CanonicalPrimitiveSiteDiscovery).GetConstructors());
    }

    [Fact]
    public void ActivityReassignmentUpdatesOnlyAffectedSiteAndDoesNotInventPopulation()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var policy = Policy();
        var old = f.Session.DiscoverPrimitiveEconomicSites(policy);
        var initial = f.Session.InitialTile;
        f.Session.ReassignWorkers(initial, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1);
        Assert.Throws<InvalidOperationException>(() => old.RequireCurrent(f.Session));
        var save = f.Session.ExportSaveJson();
        var current = f.Session.DiscoverPrimitiveEconomicSites(policy);
        var delta = current.ChangesSince(old);
        Assert.Empty(delta.Added);
        Assert.Empty(delta.Removed);
        var changed = Assert.Single(delta.Updated);
        Assert.Equal(initial, changed.After.Tile);
        Assert.Equal(changed.Before.ResidentWorkers, changed.After.ResidentWorkers);
        Assert.Equal(changed.Before.AgriculturalWorkers - 1,
            changed.After.AgriculturalWorkers);
        Assert.Equal(changed.Before.ForestryWorkers + 1,
            changed.After.ForestryWorkers);
        Assert.Equal(changed.Before.Demand.Select(x => x.RawQuantity),
            changed.After.Demand.Select(x => x.RawQuantity));
        Assert.Equal(8UL, current.ResidentWorkers);
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void TransitAndArrivalRevealNewSiteOnlyAtPhysicalArrival()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var origin = f.Session.InitialTile;
        var inhabited = f.Session.DiscoverPrimitiveEconomicSites(Policy()).Sites
            .Select(x => x.Tile).ToHashSet();
        var destination = f.Session.AdjacentWorkable(origin)
            .First(x => !inhabited.Contains(x));
        var policy = Policy();
        var before = f.Session.DiscoverPrimitiveEconomicSites(policy);
        f.Session.OrderWorkerTransit(origin, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        var travelling = f.Session.DiscoverPrimitiveEconomicSites(policy);
        Assert.Equal(7UL, travelling.ResidentWorkers);
        Assert.Equal(1UL, travelling.TravellingWorkers);
        Assert.Null(travelling.At(CanonicalPrimitiveSiteDiscovery.SiteId(destination)));
        Assert.Empty(travelling.ChangesSince(before).Added);
        f.Session.AdvanceMovementTurn();
        f.Session.AdvanceMovementTurn();
        var save = f.Session.ExportSaveJson();
        var arrived = f.Session.DiscoverPrimitiveEconomicSites(policy);
        var added = Assert.Single(arrived.ChangesSince(travelling).Added);
        Assert.Equal(destination, added.Tile);
        Assert.Equal(CanonicalPrimitiveSiteDiscovery.SiteId(destination), added.Point);
        Assert.Equal(1UL, added.ResidentWorkers);
        Assert.Equal(1UL, added.ForestryWorkers);
        Assert.Equal(8UL, arrived.ResidentWorkers);
        Assert.Equal(0UL, arrived.TravellingWorkers);
        Assert.Equal(3, arrived.Sites.Count);
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void AbandonedTileDisappearsWithoutDeletingItsPhysicalIdOrCreatingCity()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var source = f.Session.InitialTile;
        var occupied = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var other = occupied.Sites.Single(x => x.Tile != source);
        var id = other.Point;
        f.Session.OrderWorkerTransit(other.Tile, source,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture,
            other.ForestryWorkers);
        var afterDeparture = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        Assert.Null(afterDeparture.At(id));
        Assert.Equal(id, Assert.Single(afterDeparture.ChangesSince(occupied).Removed).Point);
        Assert.Single(afterDeparture.Sites);
        Assert.Equal(other.ForestryWorkers, afterDeparture.TravellingWorkers);
        Assert.Empty(f.Session.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void RestoreKeepsPhysicalSiteIdsButRequiresNewSessionBoundDiscovery()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var policy = Policy();
        var original = f.Session.DiscoverPrimitiveEconomicSites(policy);
        var save = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() => original.RequireCurrent(restored));
        var again = restored.DiscoverPrimitiveEconomicSites(policy);
        Assert.Equal(original.Sites.Select(x => (x.Point, x.Tile, x.ResidentWorkers)),
            again.Sites.Select(x => (x.Point, x.Tile, x.ResidentWorkers)));
        Assert.Equal(original.Flows.SupplyFlows.Select(x =>
            (x.EconomicPointId, x.CommodityId, x.Activity, x.RawQuantity)),
            again.Flows.SupplyFlows.Select(x =>
                (x.EconomicPointId, x.CommodityId, x.Activity, x.RawQuantity)));
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() => again.ChangesSince(original));
    }

    [Fact]
    public void NetworkChangesInvalidateDiscoveryAndDifferentDemandPolicyRejectsDeltas()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var before = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var differing = f.Session.DiscoverPrimitiveEconomicSites(FoodOnlyPolicy());
        Assert.Throws<InvalidOperationException>(() => differing.ChangesSince(before));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(f.Session));
        var after = f.Session.DiscoverPrimitiveEconomicSites(Policy());
        var delta = after.ChangesSince(before);
        Assert.Empty(delta.Added);
        Assert.Empty(delta.Removed);
        Assert.Empty(delta.Updated);
    }

    [Fact]
    public void DiscoveryBindsRealConstructedServiceWithoutManualSiteList()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var discovered = f.Session.DiscoverPrimitiveEconomicSites(FoodOnlyPolicy());
        var originalSource = f.Flows.SupplyFlows.Single().EconomicPointId;
        var originalBuyer = f.Flows.DemandFlows.Single().EconomicPointId;
        var source = CanonicalPrimitiveSiteDiscovery.SiteId(f.Anchors[originalSource]);
        var buyer = CanonicalPrimitiveSiteDiscovery.SiteId(f.Anchors[originalBuyer]);
        var newRoute = new StrategicEconomicRouteKey(source, buyer);
        var offer = new EconomicTransportServiceOffer(77, newRoute,
            f.Offer.AssetPath, f.Offer.TransferAfterLeg,
            f.Offer.ExpectedLoadMicrograms, f.Offer.CostFunction);
        var anchorMap = discovered.BuyerAnchors.ToDictionary(x => x.Buyer, x => x.Tile);
        var option = ExperimentalSharedFreightOption.FromBuiltOffer(
            offer, f.Session, anchorMap, f.Session.TransportNetwork.InstalledAssets,
            f.Session.TransportNetwork.Access);
        var right = f.Registry().Entitlements.Single() with {
            Source = source, Destination = buyer, ServiceId = 77
        };
        f.Session.TransportNetwork.SetEntitlement(right);
        // The rights mutation intentionally invalidates the old discovery.
        Assert.Throws<InvalidOperationException>(() => discovered.RequireCurrent(f.Session));
        var live = f.Session.DiscoverPrimitiveEconomicSites(FoodOnlyPolicy());
        var save = f.Session.ExportSaveJson();
        var quote = f.Quotes[0] with { Buyer = buyer };
        var selection = live.PreviewTrade(Catalog(),
            new[] { new ExperimentalBuyerCommodityPrice(
                buyer, Food, new FoodTransportCostFraction(1, 1)) },
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5, 1),
                new FoodTransportCostFraction(5, 8)),
            new[] { quote }, f.Policy, f.Registry().NominalQuoteUnitId,
            new[] { new CanonicalPrimitiveTradeLane(source, buyer, Food,
                EconomicActivityKind.Agriculture, new[] { option }) },
            new[] { new ExperimentalBuiltFreightServiceEvidence(offer, option) },
            f.Congestion, 500_000);
        Assert.Equal(buyer, Assert.Single(selection.Trade.Purchasing.Buyers).Buyer);
        Assert.Equal(source, Assert.Single(selection.Trade.Purchasing.Producers).Producer);
        Assert.Equal(checked(f.Session.WorkersAt(f.Anchors[originalSource]) *
            (ulong)f.Session.PrimitiveYieldAt(f.Anchors[originalSource],
                EconomicActivityKind.Agriculture)),
            selection.Flows.SupplyFlows.Single(x => x.CommodityId == Food).RawQuantity);
        Assert.False(selection.CanAuthorizeRealSettlement);
        Assert.False(selection.Trade.CanAuthorizeRealSettlement);
        selection.RequireCurrent(f.Session);
        Assert.Equal(save, f.Session.ExportSaveJson());
        f.Session.ReassignWorkers(f.Anchors[originalSource], EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1);
        Assert.Throws<InvalidOperationException>(() => live.PreviewTrade(Catalog(),
            new[] { new ExperimentalBuyerCommodityPrice(
                buyer, Food, new FoodTransportCostFraction(1, 1)) },
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5, 1), new FoodTransportCostFraction(5, 8)),
            new[] { quote }, f.Policy, f.Registry().NominalQuoteUnitId,
            new[] { new CanonicalPrimitiveTradeLane(source, buyer, Food,
                EconomicActivityKind.Agriculture, new[] { option }) },
            new[] { new ExperimentalBuiltFreightServiceEvidence(offer, option) },
            f.Congestion, 500_000));
    }
}
