using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs61LivePrimitiveTradeTests
{
    private const ulong Food = 7;
    private static readonly CommodityId FoodId = new((uint)Food);
    private static readonly CommodityId WoodId = new(8);
    private static readonly CommodityId StoneId = new(9);

    private static CanonicalLivePrimitiveTradeScenario Scenario(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote>? quotes = null,
        ulong quantum = 500_000)
    {
        var source = f.Flows.SupplyFlows.Single().EconomicPointId;
        var buyer = f.Flows.DemandFlows.Single().EconomicPointId;
        return new CanonicalLivePrimitiveTradeScenario(
            new FoodCommodityCatalog(new[]
            {
                new FoodCommodityDefinition(FoodId, "primitive-food", 1),
                new FoodCommodityDefinition(WoodId, "primitive-wood", 1),
                new FoodCommodityDefinition(StoneId, "primitive-stone", 1)
            }),
            new EconomicPrimitiveCommodityMapping(FoodId, WoodId, StoneId),
            new[] { new ExperimentalAbstractBuyerAnchor(buyer, f.Anchors[buyer],
                new FoodTransportCostFraction(1, 1)) },
            new[] { new ExperimentalAbstractProducerAnchor(source, f.Anchors[source]) },
            new[] { new CanonicalPrimitiveDemandRate(FoodId,
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)) },
            new[] { new ExperimentalBuyerCommodityPrice(buyer, FoodId,
                new FoodTransportCostFraction(1, 1)) },
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5, 1),
                new FoodTransportCostFraction(5, 8)),
            quotes ?? f.Quotes,
            f.Policy, f.Registry().NominalQuoteUnitId,
            new[] { new CanonicalPrimitiveTradeLane(source, buyer,
                FoodId, EconomicActivityKind.Agriculture, new[] { f.Option }) },
            new[] { f.Evidence() }, f.Congestion, quantum);
    }

    [Fact]
    public void OneSessionQueryDerivesProductionFromActualWorkersAndTacticalYield()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        var source = f.Flows.SupplyFlows.Single().EconomicPointId;
        var buyer = f.Flows.DemandFlows.Single().EconomicPointId;
        var sourceTile = f.Anchors[source];
        var buyerTile = f.Anchors[buyer];
        var agriculture = f.Session.State.Allocations.Single(x =>
            x.TileId == sourceTile && x.Activity == EconomicActivityKind.Agriculture);
        var rate = f.Session.PrimitiveYieldAt(sourceTile, EconomicActivityKind.Agriculture);
        Assert.True(rate > 0);

        var preview = f.Session.PreviewLivePrimitiveTrade(Scenario(f));
        var physicalSupply = Assert.Single(preview.Flows.SupplyFlows.Where(x =>
            x.CommodityId == FoodId));
        Assert.Equal(checked(agriculture.Workers * (ulong)rate), physicalSupply.RawQuantity);
        Assert.Equal(source, physicalSupply.EconomicPointId);
        var demand = Assert.Single(preview.Flows.DemandFlows);
        Assert.Equal(buyer, demand.EconomicPointId);
        Assert.Equal(checked(f.Session.WorkersAt(buyerTile) *
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker), demand.RawQuantity);
        Assert.Equal(f.Session.WorkersAt(buyerTile),
            Assert.Single(preview.Trade.Purchasing.Buyers).ResidentWorkers);
        Assert.False(preview.CanAuthorizeRealSettlement);
        Assert.False(preview.Trade.CanAuthorizeRealSettlement);
        preview.RequireCurrent(f.Session);
        Assert.Equal(hash, f.Session.CurrentHash);
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.Empty(typeof(CanonicalLivePrimitiveTradePreview).GetConstructors());
    }

    [Fact]
    public void ReusedScenarioRecalculatesProductionAfterActivityReassignment()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var first = f.Session.PreviewLivePrimitiveTrade(scenario);
        var initialSupply = first.Flows.SupplyFlows.Single(x => x.CommodityId == FoodId).RawQuantity;
        var initialDemand = Assert.Single(first.Flows.DemandFlows).RawQuantity;
        var sourceTile = f.Anchors[f.Flows.SupplyFlows.Single().EconomicPointId];

        f.Session.ReassignWorkers(sourceTile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1);
        Assert.Throws<InvalidOperationException>(() => first.RequireCurrent(f.Session));

        var save = f.Session.ExportSaveJson();
        var second = f.Session.PreviewLivePrimitiveTrade(scenario);
        Assert.Equal(checked(initialSupply - (ulong)f.Session.PrimitiveYieldAt(
            sourceTile, EconomicActivityKind.Agriculture)),
            second.Flows.SupplyFlows.Single(x => x.CommodityId == FoodId).RawQuantity);
        Assert.Equal(initialDemand, Assert.Single(second.Flows.DemandFlows).RawQuantity);
        Assert.Equal(save, f.Session.ExportSaveJson());
        second.RequireCurrent(f.Session);
    }

    [Fact]
    public void TransitExcludesTravellersAndArrivalAddsResidentDemandWithoutStaleInputs()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var before = f.Session.PreviewLivePrimitiveTrade(scenario);
        var oldSupply = before.Flows.SupplyFlows.Single(x => x.CommodityId == FoodId).RawQuantity;
        var oldDemand = Assert.Single(before.Flows.DemandFlows).RawQuantity;
        var source = f.Anchors[f.Flows.SupplyFlows.Single().EconomicPointId];
        var target = f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId];
        var unitYield = (ulong)f.Session.PrimitiveYieldAt(source, EconomicActivityKind.Agriculture);
        f.Session.OrderWorkerTransit(source, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1);
        Assert.Throws<InvalidOperationException>(() => before.RequireCurrent(f.Session));

        var inTransit = f.Session.PreviewLivePrimitiveTrade(scenario);
        Assert.Equal(checked(oldSupply - unitYield),
            inTransit.Flows.SupplyFlows.Single(x => x.CommodityId == FoodId).RawQuantity);
        Assert.Equal(oldDemand, Assert.Single(inTransit.Flows.DemandFlows).RawQuantity);
        Assert.Equal(1UL, inTransit.Trade.Purchasing.TravellingWorkers);

        f.Session.AdvanceMovementTurn();
        f.Session.AdvanceMovementTurn();
        var save = f.Session.ExportSaveJson();
        var arrived = f.Session.PreviewLivePrimitiveTrade(scenario);
        Assert.Equal(checked(oldDemand +
            CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker),
            Assert.Single(arrived.Flows.DemandFlows).RawQuantity);
        Assert.Equal(0UL, arrived.Trade.Purchasing.TravellingWorkers);
        Assert.Equal(3UL, Assert.Single(arrived.Trade.Purchasing.Buyers).ResidentWorkers);
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.True(arrived.Trade.Purchasing.TotalTemporaryPurchasingUnits.CompareTo(
            before.Trade.Purchasing.TotalTemporaryPurchasingUnits) > 0);
    }

    [Fact]
    public void NoLiveAgriculturalSupplyCannotBeReplacedByOldOfferedQuantity()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var source = f.Anchors[f.Flows.SupplyFlows.Single().EconomicPointId];
        var agriculturalWorkers = f.Session.State.Allocations.Single(x =>
            x.TileId == source && x.Activity == EconomicActivityKind.Agriculture).Workers;
        f.Session.ReassignWorkers(source, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, agriculturalWorkers);
        var save = f.Session.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            f.Session.PreviewLivePrimitiveTrade(scenario));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void ScenarioCopiesQuotesAndRejectsUnbuiltNetworkAndRevokedAccess()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var originals = f.Quotes.ToArray();
        var scenario = Scenario(f, originals);
        originals[0] = originals[0] with {
            NominalCommodityUnitPrice = new FoodTransportCostFraction(1000, 1)
        };
        var first = f.Session.PreviewLivePrimitiveTrade(scenario);
        var second = f.Session.PreviewLivePrimitiveTrade(scenario);
        Assert.Equal(first.Flows.SupplyFlows.Select(x => x.RawQuantity),
            second.Flows.SupplyFlows.Select(x => x.RawQuantity));
        var prior = f.Session.ExportSaveJson();
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => first.RequireCurrent(f.Session));
        Assert.ThrowsAny<Exception>(() => f.Session.PreviewLivePrimitiveTrade(scenario));
        Assert.NotEqual(prior, f.Session.ExportSaveJson());

        var withoutNetwork = new GaVs56BuiltNetworkInstitutionalRightsTests.Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            withoutNetwork.Session.PreviewLivePrimitiveTrade(Scenario(withoutNetwork)));
    }

    [Fact]
    public void RestoreRequiresNewPreviewButRebuildsTheSameLivePrimitiveFlows()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var scenario = Scenario(f);
        var old = f.Session.PreviewLivePrimitiveTrade(scenario);
        var save = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() => old.RequireCurrent(restored));
        var current = restored.PreviewLivePrimitiveTrade(scenario);
        Assert.Equal(old.Flows.SupplyFlows.Select(x => x.RawQuantity),
            current.Flows.SupplyFlows.Select(x => x.RawQuantity));
        Assert.Equal(old.Flows.DemandFlows.Select(x => x.RawQuantity),
            current.Flows.DemandFlows.Select(x => x.RawQuantity));
        Assert.Equal(save, restored.ExportSaveJson());
        current.RequireCurrent(restored);
    }
}
