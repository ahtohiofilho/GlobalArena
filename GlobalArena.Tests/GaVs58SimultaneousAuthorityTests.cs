using GlobalArena.Simulation;
using GlobalArena.Runtime;

namespace GlobalArena.Tests;

public sealed class GaVs58SimultaneousAuthorityTests
{
    [Fact]
    public void TwoCommoditiesShareCorridorAndBuyerBudgetInOneSelection()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var source = f.Flows.SupplyFlows.Single().EconomicPointId;
        var buyer = f.Flows.DemandFlows.Single().EconomicPointId;
        var second = new CommodityId(8);
        var offer = new EconomicTransportServiceOffer(18, f.Offer.RouteKey,
            f.Offer.AssetPath, f.Offer.TransferAfterLeg, f.Offer.ExpectedLoadMicrograms, f.Offer.CostFunction);
        var option = ExperimentalSharedFreightOption.FromBuiltOffer(offer, f.Session,
            f.Anchors, f.Session.TransportNetwork.InstalledAssets, f.Access);
        var flows = new EconomicProductionDemandResult(
            f.Flows.SupplyFlows.Concat(new[] { new EconomicSupplyFlow(source, second, EconomicActivityKind.Agriculture, 1_000_000) }),
            f.Flows.DemandFlows.Concat(new[] { new EconomicDemandFlow(buyer, second, 1_000_000) }));
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(f.Flows.DemandFlows.Single().CommodityId, "first", 1),
            new FoodCommodityDefinition(second, "second", 1) });
        var lanes = f.Lanes.Concat(new[] { new ExperimentalSimultaneousFreightLane(
            flows.SupplyFlows[1], flows.DemandFlows[1], new[] { option }) }).ToArray();
        var purchasing = new ExperimentalAbstractPurchasingPowerProjector().Project(f.Session, flows,
            new[] { new ExperimentalAbstractBuyerAnchor(buyer, f.Anchors[buyer], new FoodTransportCostFraction(1, 1)) },
            new[] { new ExperimentalAbstractProducerAnchor(source, f.Anchors[source]) }, lanes,
            flows.DemandFlows.Select(x => new ExperimentalBuyerCommodityPrice(buyer, x.CommodityId, new FoodTransportCostFraction(1, 1))),
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1, new FoodTransportCostFraction(5, 1), new FoodTransportCostFraction(5, 8)));
        f.Session.TransportNetwork.SetEntitlement(f.Registry().Entitlements.Single() with { ServiceId = 18 });
        var proof = f.Session.TransportNetwork.Capture(new[] { f.Evidence(), new ExperimentalBuiltFreightServiceEvidence(offer, option) },
            "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1, f.Registry().NominalQuoteUnitId);
        var quotes = new[] { f.Quotes[0], f.Quotes[0] with { Commodity = second } };
        CanonicalTransportSelection Select(ExperimentalSimultaneousFreightLane[] ordered) =>
            new CanonicalSimultaneousTransportSelector().Select(f.Session, purchasing, flows, catalog,
                quotes, f.Policy, f.Registry().NominalQuoteUnitId, ordered, f.Congestion, proof, f.Anchors, 500_000);
        var result = Select(lanes);
        Assert.Equal(9, result.EvaluatedConfigurations);
        var independentlyVerified = new CanonicalSimultaneousTransportAuditor().Verify(
            f.Session, purchasing, flows, catalog, quotes, f.Policy, f.Registry().NominalQuoteUnitId,
            lanes.Reverse(), f.Congestion, proof, f.Anchors, 500_000,
            result.Deliveries, result.EvaluatedConfigurations);
        Assert.Equal(result.Deliveries, independentlyVerified.Deliveries);
        var scenario = new CanonicalTradeScenario(flows, catalog,
            purchasing.Buyers.Select(x => new ExperimentalAbstractBuyerAnchor(x.Buyer, x.Tile, x.ProductivityFactor)),
            purchasing.Producers.Select(x => new ExperimentalAbstractProducerAnchor(x.Producer, x.Tile)),
            flows.DemandFlows.Select(x => new ExperimentalBuyerCommodityPrice(buyer, x.CommodityId, new FoodTransportCostFraction(1, 1))),
            new ExperimentalAbstractPurchasingPolicy("VS56-PURCHASING", 1, new FoodTransportCostFraction(5, 1), new FoodTransportCostFraction(5, 8)),
            quotes, f.Policy, f.Registry().NominalQuoteUnitId, lanes,
            new[] { f.Evidence(), new ExperimentalBuiltFreightServiceEvidence(offer, option) }, f.Congestion, 500_000);
        Assert.Equal(result.Deliveries, f.Session.PreviewTrade(scenario).VerifiedSelection.Deliveries);
        var noncanonicalTie = result.Deliveries.Select(x => x with {
            RawQuantity = 1_500_000 - x.RawQuantity }).ToArray();
        Assert.Throws<InvalidOperationException>(() => new CanonicalSimultaneousTransportAuditor().Verify(
            f.Session, purchasing, flows, catalog, quotes, f.Policy, f.Registry().NominalQuoteUnitId,
            lanes, f.Congestion, proof, f.Anchors, 500_000, noncanonicalTie, 9));
        Assert.Equal(2, result.Deliveries.Count);
        Assert.Equal(result.Deliveries, Select(lanes.Reverse().ToArray()).Deliveries);
        var reconciliation = result.Audit.InstitutionalProjection.PhysicalReconciliation;
        Assert.Single(reconciliation.CorridorLoads);
        Assert.Equal(new System.Numerics.BigInteger(1_500_000), reconciliation.CorridorLoads.Single().Value);
        Assert.True(reconciliation.AllBuyersCanCoverHypotheticalCharges);
        var full = new CanonicalTransportFreightProjector().Project(f.Session, purchasing,
            flows, catalog, quotes, f.Policy, f.Registry().NominalQuoteUnitId, lanes,
            result.Deliveries.Select(x => x with { RawQuantity = 1_000_000 }),
            f.Congestion, proof, f.Anchors);
        Assert.False(full.InstitutionalProjection.PhysicalReconciliation.AllBuyersCanCoverHypotheticalCharges);
    }
    [Fact]
    public void ExpensiveGoodsSelectZeroWithoutSpendingProtectedH()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var result = Run(f, quotes: new[] { f.Quotes[0] with {
            NominalCommodityUnitPrice = new FoodTransportCostFraction(1000, 1) } });
        Assert.Empty(result.Deliveries);
        Assert.True(result.Audit.InstitutionalProjection.PhysicalReconciliation.AllBuyersCanCoverHypotheticalCharges);
    }

    [Fact]
    public void SelectedResultMustBeRevalidatedAfterNetworkChanges()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var result = Run(f);
        result.RequireCurrent(f.Session);
        f.Session.TransportNetwork.SetOperational(f.Asset.AssetId, false);
        Assert.Throws<InvalidOperationException>(() => result.RequireCurrent(f.Session));
    }
    internal static CanonicalTransportSelection Run(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        ulong quantum = 500_000, int limit = 4096, CanonicalTransportProof? proof = null,
        ExperimentalAbstractNominalDeliveredQuote[]? quotes = null) =>
        new CanonicalSimultaneousTransportSelector().Select(f.Session, f.Purchasing,
            f.Flows, f.Catalog, quotes ?? f.Quotes, f.Policy, f.Registry().NominalQuoteUnitId,
            f.Lanes, f.Congestion, proof ?? GaVs57SessionTransportAuthorityTests.Capture(f),
            f.Anchors, quantum, limit);

    [Fact]
    public void SelectsAffordableQuantityAndPreservesWorldAndProtectedH()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var save = f.Session.ExportSaveJson();
        var result = Run(f);
        Assert.Equal(3, result.EvaluatedConfigurations);
        Assert.Equal(1_000_000UL, Assert.Single(result.Deliveries).RawQuantity);
        Assert.True(result.Audit.InstitutionalProjection.PhysicalReconciliation.AllBuyersCanCoverHypotheticalCharges);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Theory]
    [InlineData(0UL, 4096)]
    [InlineData(1UL, 4096)]
    [InlineData(500000UL, 2)]
    [InlineData(500000UL, 100001)]
    public void SearchLimitsFailClosedWithoutPartialSelection(ulong quantum, int limit)
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var save = f.Session.ExportSaveJson();
        Assert.ThrowsAny<ArgumentException>(() => Run(f, quantum, limit));
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void ZeroTradeRemainsFeasibleWhenQuantumExceedsSupply()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var result = Run(f, 2_000_000);
        Assert.Empty(result.Deliveries);
        Assert.Equal(1, result.EvaluatedConfigurations);
        Assert.False(result.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void StaleAndRevokedAuthorityCannotBeHiddenByZeroTrade()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var proof = GaVs57SessionTransportAuthorityTests.Capture(f);
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => Run(f, proof: proof));
        Assert.Throws<InvalidOperationException>(() => Run(f, 2_000_000));
    }

    [Fact]
    public void DeterministicSelectionReauditsChosenBatch()
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var first = Run(f);
        var second = Run(f);
        Assert.Equal(first.Deliveries, second.Deliveries);
        Assert.NotSame(first.Audit, second.Audit);
        var r = first.Audit.InstitutionalProjection.PhysicalReconciliation;
        Assert.Equal(0, r.AbstractPhysicalFreight.CompareTo(
            first.Audit.InstitutionalProjection.AbstractInstitutionalFreightReceivables));
    }
}
