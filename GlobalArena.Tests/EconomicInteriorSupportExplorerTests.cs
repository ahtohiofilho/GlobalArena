using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicInteriorSupportExplorerTests
{
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId E = new(2UL);
    private static readonly EconomicPointId F = new(3UL);
    private static readonly EconomicPhysicalResourceId Shared = new(20UL);
    private static readonly EconomicPhysicalResourceId BranchA = new(21UL);
    private static readonly EconomicPhysicalResourceId BranchE = new(22UL);
    private static readonly EconomicPhysicalResourceId BranchF = new(23UL);
    private static readonly EconomicPhysicalResourceId Direct = new(24UL);
    private static readonly EconomicFoodPreferenceParameters Parameters =
        new(0.1, 2.8, 1.0);

    private sealed class FlatFreight : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms) =>
            new(BigInteger.One, BigInteger.One);
    }

    private static EconomicMultivarietyOrigin[] Origins() => new[]
    {
        new EconomicMultivarietyOrigin(A, 12.5),
        new EconomicMultivarietyOrigin(E, 15),
        new EconomicMultivarietyOrigin(F, 11.25)
    };

    private static EconomicMultivarietyBuyer[] Buyers() => new[]
    {
        new EconomicMultivarietyBuyer(A, 10, 50.4),
        new EconomicMultivarietyBuyer(E, 12, 60.48),
        new EconomicMultivarietyBuyer(F, 9, 45.36)
    };

    private static EconomicPhysicalResourceId Branch(EconomicPointId origin) =>
        origin == A ? BranchA : origin == E ? BranchE : BranchF;

    private static EconomicInteriorPhysicalMarketIncidence[] Routes(
        bool directFromAtoE = false)
    {
        var result = new List<EconomicInteriorPhysicalMarketIncidence>();
        foreach (var buyer in new[] { A, E, F })
            foreach (var origin in new[] { A, E, F })
            {
                var quote = EconomicTurnFreightQuote.FromForecast(
                    1UL, new StrategicEconomicRouteKey(origin, buyer),
                    BigInteger.One, new FlatFreight());
                var resources = buyer == origin
                    ? Array.Empty<EconomicPhysicalResourceId>()
                    : directFromAtoE && buyer == E && origin == A
                        ? new[] { Direct }
                        : new[] { Shared, Branch(origin) };
                result.Add(new EconomicInteriorPhysicalMarketIncidence(
                    new EconomicReclearingQuotedRoute(quote, resources),
                    1UL, buyer == origin ? 0.0 : 0.1));
            }
        return result.ToArray();
    }

    private static EconomicDensityResourceCostProfile[] Profiles() => new[]
    {
        new EconomicDensityResourceCostProfile(Shared, 100, 50, 50, 25),
        new EconomicDensityResourceCostProfile(BranchA, 30, 10, 30, 20),
        new EconomicDensityResourceCostProfile(BranchE, 30, 10, 30, 20),
        new EconomicDensityResourceCostProfile(BranchF, 30, 10, 30, 20),
        new EconomicDensityResourceCostProfile(Direct, 130, 40, 40, 20)
    };

    private static EconomicInteriorLogisticSupport Support(
        string key, bool direct = false,
        IEnumerable<EconomicInteriorPhysicalMarketIncidence>? routes = null,
        IEnumerable<EconomicDensityResourceCostProfile>? profiles = null) =>
        new(key, routes ?? Routes(direct), profiles ?? Profiles());

    private static EconomicInteriorSupportExplorationResult Explore(
        IEnumerable<EconomicInteriorLogisticSupport>? supports = null,
        string baseline = "closed",
        IEnumerable<EconomicMultivarietyBuyer>? buyers = null,
        IEnumerable<EconomicMultivarietyOrigin>? origins = null) =>
        new EconomicInteriorSupportExplorer().Explore(
            baseline, origins ?? Origins(), buyers ?? Buyers(),
            supports ?? new[] { Support("closed"), Support("direct", direct: true) },
            Parameters);

    [Fact]
    public void ResolvesAndIndependentlyAuditsClosedAndAlternativePhysicalSupports()
    {
        var result = Explore();
        Assert.Equal("closed", result.Baseline.Key);
        Assert.Equal(new[] { "closed", "direct" },
            result.AuditedSupports.Select(x => x.Key));
        var alt = result.AuditedSupports.Single(x => x.Key == "direct");
        var comparison = Assert.Single(result.Comparisons);
        Assert.Equal("closed", comparison.BaselineKey);
        Assert.Equal("direct", comparison.CandidateKey);

        Assert.DoesNotContain(Direct, result.Baseline.Observation.ResourceLoadsMicrograms.Keys);
        Assert.Contains(Direct, alt.Observation.ResourceLoadsMicrograms.Keys);
        Assert.True(alt.Observation.ResourceLoadsMicrograms[Direct].Sign > 0);
        Assert.True(alt.Observation.ResourceLoadsMicrograms[Shared] <
            result.Baseline.Observation.ResourceLoadsMicrograms[Shared]);

        Assert.Equal(3, comparison.BuyerUtilityChanges.Count);
        Assert.Equal(3, comparison.OriginReceiptChanges.Count);
        Assert.All(result.AuditedSupports, observation =>
        {
            Assert.True(double.IsFinite(observation.MaximumIndependentResidual));
            Assert.InRange(observation.MaximumIndependentResidual, 0, 1e-5);
            Assert.InRange(observation.Observation.Market.MaximumSupplyResidual, 0, 1e-7);
            Assert.InRange(observation.Observation.Market.MaximumBudgetResidual, 0, 1e-7);
        });
        Assert.True(comparison.SignedCostLowerNumerator *
            comparison.CostUpperDenominator <=
            comparison.SignedCostUpperNumerator *
            comparison.CostLowerDenominator);
    }

    [Fact]
    public void IdenticalPhysicalSupportsHaveNoPriceUtilityOrReceiptDifferences()
    {
        var r = Explore(new[]
        {
            Support("closed"),
            Support("clone")
        });
        var other = Assert.Single(r.Comparisons);
        Assert.Equal(EconomicDensityCostDeltaClassification.Indeterminate,
            other.RealCostSign);
        Assert.All(other.BuyerUtilityChanges.Values, change =>
            Assert.InRange(Math.Abs(change), 0.0, 1e-9));
        Assert.All(other.OriginReceiptChanges.Values, change =>
            Assert.InRange(Math.Abs(change), 0.0, 1e-9));
        Assert.True(other.NoBuyerUtilityLossWithinTolerance);
        Assert.True(other.NoOriginReceiptLossWithinTolerance);
    }

    [Fact]
    public void DeterministicAcrossScenarioAndInputPermutations()
    {
        var a = Explore();
        var b = Explore(new[]
        {
            Support("direct", direct: true, routes: Routes(true).Reverse(),
                profiles: Profiles().Reverse()),
            Support("closed", routes: Routes().Reverse(),
                profiles: Profiles().Reverse())
        }, buyers: Buyers().Reverse(), origins: Origins().Reverse());
        var ca = Assert.Single(a.Comparisons);
        var cb = Assert.Single(b.Comparisons);
        Assert.Equal(ca.SignedCostLowerNumerator, cb.SignedCostLowerNumerator);
        Assert.Equal(ca.CostLowerDenominator, cb.CostLowerDenominator);
        Assert.Equal(ca.SignedCostUpperNumerator, cb.SignedCostUpperNumerator);
        Assert.Equal(ca.CostUpperDenominator, cb.CostUpperDenominator);
        Assert.Equal(a.Baseline.Observation.Market.OriginPrices,
            b.Baseline.Observation.Market.OriginPrices);
        Assert.Equal(a.AuditedSupports[1].Observation.Market.OriginPrices,
            b.AuditedSupports[1].Observation.Market.OriginPrices);
    }

    [Fact]
    public void IncreasingSharedResourceCostChangesReportedMarketOutcome()
    {
        var costly = Profiles().Select(profile =>
            profile.ResourceId == Shared
                ? new EconomicDensityResourceCostProfile(Shared, 270, 50, 50, 25)
                : profile);
        var r = Explore(new[]
        {
            Support("closed"),
            Support("costly", profiles: costly)
        });
        var difference = Assert.Single(r.Comparisons);
        Assert.Contains(difference.BuyerUtilityChanges.Values,
            change => Math.Abs(change) > 1e-4);
        Assert.Contains(difference.OriginReceiptChanges.Values,
            change => Math.Abs(change) > 1e-4);
    }

    [Fact]
    public void DuplicateMissingOrUnpricedSupportsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => Explore(
            supports: new[] { Support("closed"), Support("closed") }));
        Assert.Throws<ArgumentException>(() => Explore(
            supports: new[] { Support("other"), Support("direct", direct: true) }));
        Assert.Throws<ArgumentException>(() => Explore(
            supports: new[] { Support("closed") }));
        Assert.Throws<ArgumentException>(() =>
            new EconomicInteriorLogisticSupport("Invalid Key", Routes(), Profiles()));
        Assert.Throws<ArgumentException>(() => Explore(new[]
        {
            Support("closed"),
            Support("invalid", direct: true,
                profiles: Profiles().Where(x => x.ResourceId != Direct))
        }));
    }

    [Fact]
    public void ExploringTwoSupportsDoesNotMutateWorldOrInventItemProduction()
    {
        var world = WorldState.CreateInitial();
        var before = new CanonicalWorldStateHasher().Compute(world);
        var r = Explore();
        Assert.Equal(2, r.AuditedSupports.Count);
        Assert.All(r.AuditedSupports, a =>
        {
            Assert.Equal(3, a.BuyerUtility.Count);
            Assert.Equal(3, a.OriginReceipts.Count);
        });
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }
}
