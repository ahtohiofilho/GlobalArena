using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs74JointLocalFreightPhysicalPreflightTests
{
    private static CanonicalPrimitiveSourceReservationDiagnostics Diagnose(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites, long price = 1)
    {
        var region = portfolio.CalibrateRegionalPurchasing(
            GaVs70RegionalPhysicalPurchasingProductivityTests.References(sites),
            new FoodTransportCostFraction(1, 2),
            new FoodTransportCostFraction(4, 1));
        var prices = sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(price, 1))).ToArray();
        return portfolio.DiagnoseRegionalFreightAccess(
            portfolio.CandidateUses, prices,
            new ExperimentalAbstractPurchasingPolicy(
                "VS56-PURCHASING", 1,
                new FoodTransportCostFraction(5000, 1),
                new FoodTransportCostFraction(1, 1)),
            new ExperimentalSharedFreightCongestionPolicy(1, 1_000_000),
            region, 100_000UL).DiagnoseLocalSupplyPotential()
                .DiagnoseSourceReservations();
    }

    private static CanonicalPrimitiveLocalUseProposal[] EligibleSingleUnit(
        CanonicalPrimitiveSiteDiscovery sites)
    {
        var site = sites.Sites.First(x => x.Supply.Any(s =>
            s.RawQuantity > 0UL && x.Demand.Any(d =>
                d.CommodityId == s.CommodityId && d.RawQuantity > 0UL)));
        var source = site.Supply.First(s => s.RawQuantity > 0UL &&
            site.Demand.Any(d => d.CommodityId == s.CommodityId &&
                d.RawQuantity > 0UL));
        return [new CanonicalPrimitiveLocalUseProposal(
            site.Point, source.CommodityId, source.Activity, 1UL)];
    }

    [Fact]
    public void EmptyLocalTrialRetainsExactlyTheAlreadyAuditedVs71Freight()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var before = fixture.Session.ExportSaveJson();
        var audit = Diagnose(portfolio, sites);
        foreach (var scenario in new[] { audit.Baseline, audit.Regional })
        {
            var receipt = audit.PreviewJointPhysicalUse(
                scenario.ScenarioId, Array.Empty<CanonicalPrimitiveLocalUseProposal>());
            Assert.Empty(receipt.ProposedLocalUses);
            Assert.Equal(sites.Flows.SupplyFlows.Count, receipt.Sources.Count);
            Assert.Equal(sites.Flows.DemandFlows.Count, receipt.Buyers.Count);
            Assert.All(receipt.Sources, x =>
            {
                Assert.Equal(0UL, x.ProposedLocalUseRaw);
                Assert.Equal(x.PhysicalProductionRaw,
                    x.SelectedOutboundFreightRaw +
                    x.UnallocatedPhysicalProductionRaw);
            });
            Assert.All(receipt.Buyers, x =>
            {
                Assert.Equal(0UL, x.ProposedLocalUseRaw);
                Assert.Equal(x.PhysicalDemandRaw,
                    x.SelectedInboundFreightRaw + x.UnmetPhysicalDemandRaw);
            });
            Assert.True(receipt.IsPhysicallyFeasible);
            Assert.True(receipt.IsFreightBudgetIndependentlyAudited);
            Assert.False(receipt.IsLocalPurchaseBudgetEvaluated);
            Assert.False(receipt.CanAuthorizeRealSettlement);
            Assert.False(receipt.IsActualConsumption);
            Assert.False(receipt.IsCommitted);
            Assert.False(receipt.IsPersistentReservation);
            receipt.VerifyIndependent();
        }
        Assert.Equal(before, fixture.Session.ExportSaveJson());
        Assert.Empty(typeof(CanonicalPrimitiveJointPhysicalUsePreview)
            .GetConstructors());
    }

    [Fact]
    public void LocallyProposedProductionIsPossibleWithoutRoadOrPaidFreight()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var audit = Diagnose(portfolio, sites, price: 1_000_000);
        var proposal = Assert.Single(EligibleSingleUnit(sites));
        foreach (var scenario in new[] { audit.Baseline, audit.Regional })
        {
            Assert.Equal(0, scenario.SourceCommodityPairsWithSelectedOutbound);
            var receipt = audit.PreviewJointPhysicalUse(
                scenario.ScenarioId, [proposal]);
            var source = Assert.Single(receipt.Sources, x =>
                x.Source == proposal.Point &&
                x.Commodity == proposal.Commodity &&
                x.Activity == proposal.Activity);
            var buyer = Assert.Single(receipt.Buyers, x =>
                x.Buyer == proposal.Point && x.Commodity == proposal.Commodity);
            Assert.Equal(1UL, source.ProposedLocalUseRaw);
            Assert.Equal(1UL, buyer.ProposedLocalUseRaw);
            Assert.Equal(0UL, source.SelectedOutboundFreightRaw);
            Assert.Equal(0UL, buyer.SelectedInboundFreightRaw);
            Assert.Equal(source.PhysicalProductionRaw - 1UL,
                source.UnallocatedPhysicalProductionRaw);
            Assert.Equal(buyer.PhysicalDemandRaw - 1UL,
                buyer.UnmetPhysicalDemandRaw);
            Assert.False(receipt.IsLocalPurchaseBudgetEvaluated);
            receipt.VerifyIndependent();
        }
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void RejectsProposalsWhichOveruseSamePhysicalSourceOrResidentDemand()
    {
        var (_, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var audit = Diagnose(portfolio, sites, price: 1_000_000);
        var proposal = Assert.Single(EligibleSingleUnit(sites));
        var source = Assert.Single(sites.At(proposal.Point)!.Supply,
            x => x.CommodityId == proposal.Commodity &&
                 x.Activity == proposal.Activity);
        var demand = Assert.Single(sites.At(proposal.Point)!.Demand,
            x => x.CommodityId == proposal.Commodity);
        var excess = checked(Math.Min(source.RawQuantity, demand.RawQuantity) + 1UL);
        Assert.Throws<ArgumentException>(() => audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId, [proposal with { RawQuantity = excess }]));
        Assert.Throws<ArgumentException>(() => audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId, [proposal, proposal]));
        Assert.Throws<ArgumentException>(() => audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId, [proposal with { RawQuantity = 0UL }]));
        Assert.Throws<ArgumentException>(() => audit.PreviewJointPhysicalUse(
            "unverified-new-market", [proposal]));
        var unknown = new CanonicalPrimitiveLocalUseProposal(
            proposal.Point, proposal.Commodity,
            (EconomicActivityKind)int.MaxValue, 1UL);
        Assert.Throws<ArgumentException>(() => audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId, [unknown]));
        Assert.Throws<ArgumentException>(() => audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId,
            Enumerable.Repeat(proposal, 13).ToArray()));
    }

    [Fact]
    public void LocalTrialCannotReuseUnitsAlreadyAssignedToSelectedExports()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var audit = Diagnose(portfolio, sites);
        var lots = audit.RegionalFreightAccess.Comparison.Baseline
            .Selection.Lots;
        Assert.NotEmpty(lots);
        var exported = lots[0].Candidate;
        var supplyFlow = Assert.Single(sites.At(exported.Source)!.Supply,
            x => x.CommodityId == exported.Commodity &&
                x.Activity == exported.Activity);
        var totalExported = lots.Where(x =>
                x.Candidate.Source == exported.Source &&
                x.Candidate.Commodity == exported.Commodity &&
                x.Candidate.Activity == exported.Activity)
            .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));
        Assert.True(totalExported > 0UL);
        Assert.True(totalExported <= supplyFlow.RawQuantity);
        var proposedLocalRaw = checked(supplyFlow.RawQuantity -
            totalExported + 1UL);
        var rejected = new CanonicalPrimitiveLocalUseProposal(
            exported.Source, exported.Commodity,
            exported.Activity, proposedLocalRaw);
        Assert.Throws<ArgumentException>(() =>
            audit.PreviewJointPhysicalUse(audit.Baseline.ScenarioId, [rejected]));
        // A failed hypothesis never mutates world, budgets or planned freight.
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
        Assert.Equal(totalExported, lots.Where(x =>
                x.Candidate.Source == exported.Source &&
                x.Candidate.Commodity == exported.Commodity &&
                x.Candidate.Activity == exported.Activity)
            .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity)));
    }

    [Fact]
    public void ProposedLocalUseIsCopiedAndDeterministicUnderInputReordering()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var audit = Diagnose(portfolio, sites, price: 1_000_000);
        var proposals = sites.Sites.SelectMany(s => s.Supply
            .Where(p => p.RawQuantity > 0UL && s.Demand.Any(d =>
                d.CommodityId == p.CommodityId && d.RawQuantity > 0UL))
            .Select(p => new CanonicalPrimitiveLocalUseProposal(
                s.Point, p.CommodityId, p.Activity, 1UL)))
            .Take(2).ToArray();
        Assert.Equal(2, proposals.Length);
        var normal = audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId, proposals);
        var reversed = audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId, proposals.Reverse());
        Assert.Equal(normal.ProposedLocalUses, reversed.ProposedLocalUses);
        Assert.Equal(normal.Sources, reversed.Sources);
        Assert.Equal(normal.Buyers, reversed.Buyers);
        proposals[0] = proposals[0] with { RawQuantity = 999999UL };
        normal.VerifyIndependent();
        reversed.VerifyIndependent();
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void FreightOnlyVs73ReceiptDoesNotImplyLocalEconomicAffordability()
    {
        var (_, sites, portfolio, _, _) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var audit = Diagnose(portfolio, sites);
        var receipt = audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId,
            Array.Empty<CanonicalPrimitiveLocalUseProposal>());
        Assert.Equal(sites.Flows.SupplyFlows.Count, receipt.Sources.Count);
        Assert.Equal(sites.Flows.DemandFlows.Count, receipt.Buyers.Count);
        Assert.False(receipt.IsLocalPurchaseBudgetEvaluated);
        Assert.False(receipt.CanAuthorizeRealSettlement);
        receipt.VerifyIndependent();
    }

    [Fact]
    public void NetworkChangeOrRestoredSaveInvalidatesJointPhysicalPreview()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var audit = Diagnose(portfolio, sites);
        var receipt = audit.PreviewJointPhysicalUse(
            audit.Baseline.ScenarioId,
            Array.Empty<CanonicalPrimitiveLocalUseProposal>());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            receipt.RequireCurrent(restored));
        fixture.Session.TransportNetwork.SetAccess(
            fixture.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() =>
            receipt.RequireCurrent(fixture.Session));
        Assert.Throws<InvalidOperationException>(() =>
            receipt.VerifyIndependent());
    }
}
