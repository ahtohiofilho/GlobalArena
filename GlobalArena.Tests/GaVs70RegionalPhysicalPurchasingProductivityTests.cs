using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs70RegionalPhysicalPurchasingProductivityTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);
    private static readonly FoodTransportCostFraction Lower = new(1, 2);
    private static readonly FoodTransportCostFraction Upper = new(4, 1);

    private static ExperimentalAbstractPurchasingPolicy Purchasing() => new(
        "VS56-PURCHASING", 1,
        new FoodTransportCostFraction(5000, 1),
        new FoodTransportCostFraction(5, 8));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(1, 1))).ToArray();

    private static CanonicalPrimitiveProductivityReference[] References(
        CanonicalPrimitiveSiteDiscovery sites) =>
        sites.Flows.SupplyFlows.Select(x =>
            new CanonicalPrimitiveProductivityReference(
                x.CommodityId, x.Activity,
                x.CommodityId == Food ? 1_000_000UL :
                x.CommodityId == Wood ? 100_000UL : 50_000UL))
            .DistinctBy(x => (x.Commodity, x.Activity))
            .ToArray();

    [Fact]
    public void ProductivityIsPhysicalPerResidentAndComparableOnlyAfterCommodityNormalization()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var before = f.Session.ExportSaveJson();
        var references = References(sites);
        var regional = portfolio.CalibrateRegionalPurchasing(references, Lower, Upper);
        Assert.Equal(sites.Sites.Count, regional.Buyers.Count);
        Assert.Equal(sites.ResidentWorkers,
            (ulong)regional.Buyers.Sum(x => (long)x.ResidentWorkers));
        Assert.Equal(
            sites.Sites.OrderBy(x => x.Point.Value).Select(x => x.Point),
            regional.Buyers.Select(x => x.Buyer));
        var referenceByFlow = references.ToDictionary(
            x => (x.Commodity, x.Activity), x => x.ReferenceRawPerWorker);
        foreach (var buyer in regional.Buyers)
        {
            var site = sites.At(buyer.Buyer)!;
            var computed = new FoodTransportCostFraction(0, 1);
            foreach (var flow in site.Supply)
            {
                var denominator =
                    (BigInteger)referenceByFlow[(flow.CommodityId, flow.Activity)] *
                    site.ResidentWorkers;
                computed = ExperimentalSharedFreightBatchCost.Add(
                    computed, new FoodTransportCostFraction(flow.RawQuantity, denominator));
            }
            Assert.Equal(0,
                buyer.ReferenceNormalizedOutputPerResident.CompareTo(computed));
            var expected = computed.CompareTo(Lower) < 0 ? Lower :
                computed.CompareTo(Upper) > 0 ? Upper : computed;
            Assert.Equal(0, buyer.PurchasingProductivityFactor.CompareTo(expected));
            Assert.True(buyer.PurchasingProductivityFactor.CompareTo(Lower) >= 0);
            Assert.True(buyer.PurchasingProductivityFactor.CompareTo(Upper) <= 0);
            Assert.Equal(site.ResidentWorkers, buyer.ResidentWorkers);
        }
        Assert.True(regional.Buyers.Select(x => x.PurchasingProductivityFactor)
            .DistinctBy(x => (x.Numerator, x.Denominator)).Count() > 1);
        Assert.Equal(before, f.Session.ExportSaveJson());
        Assert.False(regional.CanAuthorizeRealSettlement);
        Assert.False(regional.IsCommitted);
        regional.RequireCurrent(f.Session);
        Assert.Throws<InvalidOperationException>(() =>
            regional.RequireCurrent(CanonicalPhysicalTacticalSession.RestoreSaveJson(before)));
    }

    [Fact]
    public void RegionalFactorsChangeOnlyBuyersGrossPowerNotResidentsHOrPhysicalCargo()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var baselinePolicy = Purchasing();
        var regional = portfolio.CalibrateRegionalPurchasing(
            References(sites), Lower, Upper);
        var regionalPolicy = regional.ApplyTo(baselinePolicy);
        var projector = new ExperimentalAbstractPurchasingPowerProjector();
        ExperimentalAbstractPurchasingProjection Project(
            ExperimentalAbstractPurchasingPolicy p) =>
            projector.Project(f.Session, sites.Flows, sites.BuyerAnchors,
                sites.ProducerAnchors,
                Array.Empty<ExperimentalSimultaneousFreightLane>(),
                Prices(sites), p);
        var normal = Project(baselinePolicy);
        var changed = Project(regionalPolicy);
        Assert.Equal(normal.ResidentWorkersInWorld, changed.ResidentWorkersInWorld);
        Assert.Equal(normal.ResidentWorkersInProjectedBuyers,
            changed.ResidentWorkersInProjectedBuyers);
        foreach (var expected in normal.Buyers)
        {
            var actual = Assert.Single(changed.Buyers, x => x.Buyer == expected.Buyer);
            var factor = Assert.Single(regional.Buyers, x => x.Buyer == expected.Buyer)
                .PurchasingProductivityFactor;
            Assert.Equal(expected.Tile, actual.Tile);
            Assert.Equal(expected.ResidentWorkers, actual.ResidentWorkers);
            Assert.Equal(0, actual.TemporaryCycleBudget.ProtectedResidual.CompareTo(
                expected.TemporaryCycleBudget.ProtectedResidual));
            Assert.Equal(0, actual.TemporaryCycleBudget.GrossCash.CompareTo(
                new FoodTransportCostFraction(
                    expected.TemporaryCycleBudget.GrossCash.Numerator * factor.Numerator,
                    expected.TemporaryCycleBudget.GrossCash.Denominator * factor.Denominator)));
        }
        Assert.Equal(normal.UnprojectedResidentWorkers,
            changed.UnprojectedResidentWorkers);
        Assert.Throws<ArgumentException>(() => regional.ApplyTo(regionalPolicy));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void RegionalProductivityRunsThroughExistingVs68Vs67Vs66AndVs65WithoutMutation()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var save = f.Session.ExportSaveJson();
        var regional = portfolio.CalibrateRegionalPurchasing(
            References(sites), Lower, Upper);
        var result = portfolio.CompareRegionalPurchasing(
            portfolio.CandidateUses, Prices(sites), Purchasing(), Congestion(),
            regional, 100_000UL);
        Assert.Equal(2, result.Outcomes.Count);
        Assert.Equal(new[] { "baseline", "regional-productivity" },
            result.Outcomes.Select(x => x.Scenario.ScenarioId));
        var standard = result.Baseline.Selection;
        var altered = result.Outcomes.Single(
            x => x.Scenario.ScenarioId == "regional-productivity").Selection;
        Assert.Equal(3, standard.Lots.Count);
        Assert.Equal(3, altered.Lots.Count);
        Assert.True(altered.Preview!.IsBudgetFeasible);
        Assert.Equal(standard.Lots, altered.Lots);
        Assert.Equal(0,
            standard.Preview!.Freight.TotalCost.CompareTo(altered.Preview.Freight.TotalCost));
        Assert.Equal(0,
            standard.Preview.Freight.TotalCost.CompareTo(
                standard.Preview.Buyers.TotalBuyerFreight));
        Assert.Equal(0,
            altered.Preview.Freight.TotalCost.CompareTo(
                altered.Preview.Buyers.TotalBuyerFreight));
        Assert.All(altered.Preview.Purchasing.Buyers, x =>
            Assert.Equal(0, x.TemporaryCycleBudget.ProtectedResidual.CompareTo(
                Assert.Single(standard.Preview.Purchasing.Buyers,
                    y => y.Buyer == x.Buyer)
                    .TemporaryCycleBudget.ProtectedResidual)));
        Assert.Equal(save, f.Session.ExportSaveJson());
        result.VerifyRecomputed();
        result.RequireCurrent(f.Session);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.IsCommitted);
    }

    [Fact]
    public void DifferentCommodityReferenceDoesNotInventWorkersOrTradeAssets()
    {
        var (f, sites, portfolio, _, _) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var original = portfolio.CalibrateRegionalPurchasing(
            References(sites), Lower, Upper);
        var modifiedReferences = References(sites).Select(x =>
            x.Commodity == Food ? x with {
                ReferenceRawPerWorker = x.ReferenceRawPerWorker * 2UL
            } : x).ToArray();
        var modified = portfolio.CalibrateRegionalPurchasing(
            modifiedReferences, Lower, Upper);
        Assert.Equal(original.Buyers.Select(x => x.Buyer),
            modified.Buyers.Select(x => x.Buyer));
        Assert.Equal(original.Buyers.Select(x => x.ResidentWorkers),
            modified.Buyers.Select(x => x.ResidentWorkers));
        Assert.Contains(original.Buyers.Zip(modified.Buyers),
            x => x.First.ReferenceNormalizedOutputPerResident.CompareTo(
                x.Second.ReferenceNormalizedOutputPerResident) != 0);
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
        original.RequireCurrent(f.Session);
        modified.RequireCurrent(f.Session);
    }

    [Fact]
    public void MissingDuplicateInvalidOrMismatchedPhysicalReferencesFailClosed()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var references = References(sites);
        Assert.Throws<ArgumentException>(() =>
            portfolio.CalibrateRegionalPurchasing(references.Skip(1), Lower, Upper));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CalibrateRegionalPurchasing(references.Concat(
                references.Take(1)), Lower, Upper));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CalibrateRegionalPurchasing(
                references.Select(x => x with { ReferenceRawPerWorker = 0UL }),
                Lower, Upper));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CalibrateRegionalPurchasing(references,
                new FoodTransportCostFraction(0, 1), Upper));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CalibrateRegionalPurchasing(references,
                new FoodTransportCostFraction(5, 1), Upper));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CalibrateRegionalPurchasing(references,
                Lower, new FoodTransportCostFraction(17, 1)));
        var regional = portfolio.CalibrateRegionalPurchasing(references, Lower, Upper);
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => regional.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            regional.ApplyTo(Purchasing()));
        Assert.Throws<InvalidOperationException>(() =>
            portfolio.CalibrateRegionalPurchasing(references, Lower, Upper));
    }
}
