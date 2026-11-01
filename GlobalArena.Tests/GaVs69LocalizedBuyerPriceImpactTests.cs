using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs69LocalizedBuyerPriceImpactTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(1, 1))).ToArray();

    private static ExperimentalAbstractPurchasingPolicy Purchasing() => new(
        "VS56-PURCHASING", 1,
        new FoodTransportCostFraction(5000, 1),
        new FoodTransportCostFraction(5, 8));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static CanonicalPrimitiveLocalPriceShock Shock(
        string id, EconomicPointId buyer, CommodityId item, long price) =>
        new(id, buyer, item, new FoodTransportCostFraction(price, 1));

    private static CanonicalPrimitiveLocalPriceComparison Compare(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidates,
        IEnumerable<CanonicalPrimitiveLocalPriceShock> shocks,
        ulong quantum = 100_000, int quanta = 1, int maxCases = 4096) =>
        portfolio.CompareLocalizedBuyerPrices(candidates, Prices(sites),
            Purchasing(), Congestion(), shocks, quantum, quanta, maxCases);

    [Fact]
    public void PriceShockToOneRealBuyerLeavesOtherIndependentRoadAndResidentMarketUnchanged()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var before = f.Session.ExportSaveJson();
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        Assert.True(quantum > 0UL);
        var original = Prices(sites);
        var result = portfolio.CompareLocalizedBuyerPrices(
            new[] { wood, food }, original, Purchasing(), Congestion(),
            new[] {
                Shock("A-food", food.Buyer, Food, 1_000_000),
                Shock("B-wood", wood.Buyer, Wood, 1_000_000)
            }.Reverse(), quantum, 8, 10);

        Assert.Equal(new[] { "A-food", "B-wood" },
            result.Impacts.Select(x => x.Shock.ScenarioId));
        Assert.Equal(3, result.Scenarios.Outcomes.Count);
        Assert.Equal(2, result.Scenarios.Baseline.Selection.IndependentComponentCount);
        Assert.Equal(81, result.Scenarios.Baseline.Selection.PossibleGlobalConfigurations);
        Assert.Equal(18, result.Scenarios.Baseline.Selection.EvaluatedComponentConfigurations);
        Assert.Equal(new[] { Food, Wood },
            result.Scenarios.Baseline.Selection.Lots.Select(x => x.Candidate.Commodity));

        var shockA = result.Impacts[0];
        var shockB = result.Impacts[1];
        Assert.Single(shockA.CoupledComponentIndices);
        Assert.Single(shockB.CoupledComponentIndices);
        Assert.NotEqual(shockA.CoupledComponentIndices[0],
            shockB.CoupledComponentIndices[0]);
        Assert.Equal(1, shockA.VerifiedUnchangedComponents);
        Assert.Equal(1, shockB.VerifiedUnchangedComponents);

        Assert.DoesNotContain(shockA.Outcome.Selection.Lots,
            x => x.Candidate.Commodity == Food);
        Assert.Contains(shockA.Outcome.Selection.Lots,
            x => ReferenceEquals(x.Candidate, wood));
        Assert.DoesNotContain(shockB.Outcome.Selection.Lots,
            x => x.Candidate.Commodity == Wood);
        Assert.Contains(shockB.Outcome.Selection.Lots,
            x => ReferenceEquals(x.Candidate, food));
        Assert.Equal(before, f.Session.ExportSaveJson());
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.False(result.IsCommitted);
        result.RequireCurrent(f.Session);
        result.VerifyLocalizedImpacts();
        Assert.Empty(typeof(CanonicalPrimitiveLocalPriceComparison).GetConstructors());
    }

    [Fact]
    public void PriceOfUntradedProductInThirdBuyerDoesNotChangeEitherIndependentSelection()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        var source = CanonicalPrimitiveSiteDiscovery.SiteId(f.Session.InitialTile);
        var result = Compare(portfolio, sites, new[] { food, wood },
            new[] { Shock("source-stone", source, Stone, 1_000_000) },
            quantum, 8, 10);
        var impact = Assert.Single(result.Impacts);
        Assert.Empty(impact.CoupledComponentIndices);
        Assert.Equal(2, impact.VerifiedUnchangedComponents);
        Assert.Equal(result.Scenarios.Baseline.Selection.Lots,
            impact.Outcome.Selection.Lots);
        Assert.Equal(0, result.Scenarios.Baseline.Selection.TotalBuyerCommitment.CompareTo(
            impact.Outcome.Selection.TotalBuyerCommitment));
        Assert.Equal(0, impact.Outcome.FulfilledDemandDirectionVsBaseline);
        result.VerifyLocalizedImpacts();
    }

    [Fact]
    public void SharedBuyerAndOneCorridorAreNeverFalselySeparatedByCommodity()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var food = Assert.Single(portfolio.CandidateUses, x => x.Commodity == Food);
        var wood = Assert.Single(portfolio.CandidateUses, x => x.Commodity == Wood);
        var result = Compare(portfolio, sites, portfolio.CandidateUses,
            new[] { Shock("food-expensive", food.Buyer, Food, 1_000_000) });
        Assert.Single(result.Scenarios.Baseline.Selection.Components);
        var impact = Assert.Single(result.Impacts);
        Assert.Equal(new[] { 0 }, impact.CoupledComponentIndices);
        Assert.Equal(0, impact.VerifiedUnchangedComponents);
        Assert.Contains(result.Scenarios.Baseline.Selection.Lots,
            x => ReferenceEquals(x.Candidate, food));
        Assert.Contains(result.Scenarios.Baseline.Selection.Lots,
            x => ReferenceEquals(x.Candidate, wood));
        Assert.DoesNotContain(impact.Outcome.Selection.Lots,
            x => ReferenceEquals(x.Candidate, food));
        result.VerifyLocalizedImpacts();
        result.RequireCurrent(f.Session);
    }

    [Fact]
    public void CallerMutationAndReorderedShocksDoNotChangeAuditedLocality()
    {
        var (f, sites, portfolio, food, wood) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var quantum = Math.Min(food.MaximumRawQuantity, wood.MaximumRawQuantity) / 8UL;
        var prices = Prices(sites);
        var shocks = new[] {
            Shock("A", food.Buyer, Food, 1_000_000),
            Shock("B", wood.Buyer, Wood, 1_000_000)
        };
        var left = portfolio.CompareLocalizedBuyerPrices(
            new[] { wood, food }, prices, Purchasing(), Congestion(),
            shocks, quantum, 8, 10);
        var right = Compare(portfolio, sites, new[] { food, wood },
            shocks.Reverse(), quantum, 8, 10);
        Assert.Equal(left.Impacts.Select(x => x.Shock.ScenarioId),
            right.Impacts.Select(x => x.Shock.ScenarioId));
        Assert.Equal(left.Impacts.Select(x => x.VerifiedUnchangedComponents),
            right.Impacts.Select(x => x.VerifiedUnchangedComponents));
        Assert.Equal(left.Scenarios.Baseline.Selection.Lots,
            right.Scenarios.Baseline.Selection.Lots);

        prices[0] = new ExperimentalBuyerCommodityPrice(
            prices[0].Buyer, prices[0].Commodity,
            new FoodTransportCostFraction(1_000_000, 1));
        shocks[0] = Shock("FAKE", food.Buyer, Food, 9);
        left.VerifyLocalizedImpacts();
        right.VerifyLocalizedImpacts();
        Assert.Equal("A", left.Impacts[0].Shock.ScenarioId);
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void ForeignIncompleteDuplicateAndOversizeShocksFailClosed()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var candidate = portfolio.CandidateUses[0];
        var shock = Shock("price-A", candidate.Buyer, candidate.Commodity, 2);
        var prices = Prices(sites);

        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites, portfolio.CandidateUses,
                Array.Empty<CanonicalPrimitiveLocalPriceShock>()));
        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites, portfolio.CandidateUses,
                Enumerable.Range(0, 6).Select(i =>
                    Shock($"test-{i}", candidate.Buyer, candidate.Commodity, 2))));
        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites, portfolio.CandidateUses,
                new[] { shock, shock }));
        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites, portfolio.CandidateUses,
                new[] { shock with { ScenarioId = "baseline" } }));
        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites, portfolio.CandidateUses,
                new[] { Shock("foreign", new EconomicPointId(999_999),
                    candidate.Commodity, 2) }));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CompareLocalizedBuyerPrices(portfolio.CandidateUses,
                prices.Skip(1), Purchasing(), Congestion(),
                new[] { shock }, 100_000));
        Assert.Throws<ArgumentException>(() =>
            portfolio.CompareLocalizedBuyerPrices(portfolio.CandidateUses,
                prices.Concat(prices.Take(1)), Purchasing(), Congestion(),
                new[] { shock }, 100_000));
        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites,
                new[] { candidate with { } }, new[] { shock }));
        Assert.Throws<ArgumentException>(() =>
            Compare(portfolio, sites, portfolio.CandidateUses,
                new[] { shock }, 100_000, 8, 7));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void NetworkChangeAndRestoreInvalidateTheWholeRegionalComparison()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var candidate = portfolio.CandidateUses[0];
        var result = Compare(portfolio, sites, portfolio.CandidateUses,
            new[] { Shock("A", candidate.Buyer, candidate.Commodity, 2) });
        var saved = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => result.RequireCurrent(restored));
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => result.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() => result.VerifyLocalizedImpacts());
    }
}
