using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicSingleVarietyCornerSupportProbeTests
{
    private static readonly EconomicPointId Origin = new(1UL);
    private static readonly EconomicPointId ExistingBuyer = new(2UL);
    private static readonly EconomicPointId OpeningBuyer = new(3UL);
    private static readonly EconomicPhysicalResourceId Shared = new(11UL);
    private static readonly EconomicPhysicalResourceId OldBranch = new(12UL);
    private static readonly EconomicPhysicalResourceId ColdDirect = new(13UL);
    private static readonly EconomicFoodPreferenceParameters Preferences =
        new(0.1, 2.8, 1.0);

    private sealed class SyntheticFreight : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms) =>
            new(BigInteger.One, BigInteger.One);
    }

    private static EconomicMultivarietyBuyer[] Buyers(double budget = 50) => new[]
    {
        new EconomicMultivarietyBuyer(ExistingBuyer, 10, budget),
        new EconomicMultivarietyBuyer(OpeningBuyer, 10, budget)
    };

    private static EconomicDensityResourceCostProfile[] Profiles() => new[]
    {
        new EconomicDensityResourceCostProfile(Shared, 400, 50, 2000, 15),
        new EconomicDensityResourceCostProfile(OldBranch, 100, 20, 400, 20),
        new EconomicDensityResourceCostProfile(ColdDirect, 160, 1100, 250, 24)
    };

    private static EconomicCornerQuotedService Quoted(
        EconomicPointId buyer, double tariff,
        params EconomicPhysicalResourceId[] path)
    {
        var quote = EconomicTurnFreightQuote.FromForecast(
            1UL, new StrategicEconomicRouteKey(Origin, buyer),
            BigInteger.Zero, new SyntheticFreight());
        return new EconomicCornerQuotedService(
            new EconomicReclearingQuotedRoute(quote, path), tariff);
    }

    private static EconomicSingleVarietyCornerSupport Baseline(
        double oldFreight = .3) =>
        new("closed", new[] { Quoted(ExistingBuyer, oldFreight, Shared, OldBranch) });

    private static EconomicSingleVarietyCornerSupport Candidate(
        double openingFreight = .15, double oldFreight = .3) =>
        new("opened", new[]
        {
            Quoted(ExistingBuyer, oldFreight, Shared, OldBranch),
            Quoted(OpeningBuyer, openingFreight, ColdDirect)
        });

    private static EconomicCornerOpeningObservation Evaluate(
        EconomicSingleVarietyCornerSupport? baseline = null,
        EconomicSingleVarietyCornerSupport? candidate = null,
        IEnumerable<EconomicDensityResourceCostProfile>? profiles = null,
        IEnumerable<EconomicMultivarietyBuyer>? buyers = null,
        double supply = 8,
        BigInteger? minimum = null) =>
        new EconomicSingleVarietyCornerSupportProbe().CompareOpening(
            1UL, Origin, supply, buyers ?? Buyers(),
            profiles ?? Profiles(), 1UL,
            baseline ?? Baseline(), candidate ?? Candidate(),
            OpeningBuyer, minimum ?? new BigInteger(2_000_000), Preferences);

    private static EconomicCornerBuyerObservation Buyer(
        EconomicAuditedCornerSupport support, EconomicPointId id) =>
        support.Buyers.Single(x => x.BuyerId == id);

    [Fact]
    public void ColdDirectRouteGetsFiniteTradeAndReclearsOriginPriceAndExistingBuyer()
    {
        var r = Evaluate();
        Assert.False(Buyer(r.Baseline, OpeningBuyer).IsConnected);
        Assert.Equal(0.0, Buyer(r.Baseline, OpeningBuyer).Quantity);
        Assert.Equal(50.0, Buyer(r.Baseline, OpeningBuyer).MonetaryResidual);
        Assert.True(Buyer(r.Candidate, OpeningBuyer).IsConnected);
        Assert.True(Buyer(r.Candidate, OpeningBuyer).Quantity > 2.0);
        Assert.True(Buyer(r.Candidate, ExistingBuyer).Quantity <
            Buyer(r.Baseline, ExistingBuyer).Quantity);
        Assert.True(r.Candidate.OriginPrice > 0);
        Assert.True(r.ObservedBatchMeetsMinimum);
        Assert.Equal(new BigInteger(Math.Round(
            Buyer(r.Candidate, OpeningBuyer).Quantity * 1_000_000.0,
            MidpointRounding.ToEven)), r.ObservedOpeningLoadMicrograms);

        Assert.DoesNotContain(ColdDirect, r.Baseline.LoadsMicrograms.Keys);
        Assert.Equal(r.ObservedOpeningLoadMicrograms,
            r.Candidate.LoadsMicrograms[ColdDirect]);
        Assert.Equal(r.ObservedOpeningLoadMicrograms,
            r.NewlyLoadedResources[ColdDirect]);
        Assert.True(r.Candidate.LoadsMicrograms[Shared] <
            r.Baseline.LoadsMicrograms[Shared]);
        Assert.True(r.BuyerUtilityChanges[OpeningBuyer] > 0);
        Assert.InRange(Math.Abs(r.Candidate.Sold - 8.0), 0, 1e-7);
        Assert.True(r.SignedRealCostLowerDenominator > 0);
        Assert.True(r.SignedRealCostUpperDenominator > 0);
        Assert.True(r.SignedRealCostLowerNumerator * r.SignedRealCostUpperDenominator <=
            r.SignedRealCostUpperNumerator * r.SignedRealCostLowerDenominator);
    }

    [Fact]
    public void VeryHighOpeningFreightLeavesNewRouteColdWithoutDividingByZero()
    {
        var r = Evaluate(candidate: Candidate(openingFreight: 100));
        Assert.True(Buyer(r.Candidate, OpeningBuyer).IsConnected);
        Assert.Equal(0.0, Buyer(r.Candidate, OpeningBuyer).Quantity);
        Assert.Equal(BigInteger.Zero, r.ObservedOpeningLoadMicrograms);
        Assert.False(r.ObservedBatchMeetsMinimum);
        Assert.Empty(r.NewlyLoadedResources);
        Assert.DoesNotContain(ColdDirect, r.Candidate.LoadsMicrograms.Keys);
        Assert.Equal(r.Baseline.OriginPrice, r.Candidate.OriginPrice);
        Assert.Equal(EconomicDensityCostDeltaClassification.Indeterminate,
            r.RealCostSign);
        Assert.Equal(BigInteger.Zero, r.SignedRealCostLowerNumerator);
        Assert.Equal(BigInteger.Zero, r.SignedRealCostUpperNumerator);
    }

    [Fact]
    public void ArbitraryFiniteLotThresholdDoesNotForcePurchases()
    {
        var feasible = Evaluate();
        var tooLarge = Evaluate(minimum: new BigInteger(9_000_000));
        Assert.Equal(feasible.ObservedOpeningLoadMicrograms,
            tooLarge.ObservedOpeningLoadMicrograms);
        Assert.False(tooLarge.ObservedBatchMeetsMinimum);
        Assert.True(feasible.ObservedBatchMeetsMinimum);
    }

    [Fact]
    public void CanObserveZeroOriginPriceAndUnsoldGoodsWithNoArtificialSales()
    {
        var r = Evaluate(
            baseline: Baseline(oldFreight: 100),
            candidate: Candidate(openingFreight: 100, oldFreight: 100),
            buyers: Buyers(budget: 2));
        Assert.Equal(0.0, r.Baseline.OriginPrice);
        Assert.Equal(0.0, r.Candidate.OriginPrice);
        Assert.Equal(0.0, r.Baseline.Sold);
        Assert.Equal(0.0, r.Candidate.Sold);
        Assert.Equal(8.0, r.Baseline.Unsold);
        Assert.Equal(8.0, r.Candidate.Unsold);
        Assert.Equal(0.0, r.Candidate.SellerReceipts);
        Assert.Equal(0.0, r.Candidate.FreightReceipts);
        Assert.Empty(r.Baseline.LoadsMicrograms);
        Assert.Empty(r.Candidate.LoadsMicrograms);
        Assert.Equal(0.0, r.BuyerUtilityChanges[OpeningBuyer]);
    }

    [Fact]
    public void PermutationOfBuyerProfileAndServiceInputsDoesNotChangeResult()
    {
        var a = Evaluate();
        var old = Baseline();
        var original = Candidate();
        var b = Evaluate(
            baseline: new EconomicSingleVarietyCornerSupport(
                "closed", old.Services.Reverse()),
            candidate: new EconomicSingleVarietyCornerSupport(
                "opened", original.Services.Reverse()),
            buyers: Buyers().Reverse(),
            profiles: Profiles().Reverse());
        Assert.Equal(a.Candidate.OriginPrice, b.Candidate.OriginPrice);
        Assert.Equal(a.ObservedOpeningLoadMicrograms, b.ObservedOpeningLoadMicrograms);
        Assert.Equal(a.SignedRealCostLowerNumerator,
            b.SignedRealCostLowerNumerator);
        Assert.Equal(a.SignedRealCostUpperNumerator,
            b.SignedRealCostUpperNumerator);
        Assert.Equal(a.Baseline.SellerReceipts, b.Baseline.SellerReceipts);
    }

    [Fact]
    public void InvalidOrUnpricedOpeningIsRejectedRatherThanInventingAccess()
    {
        Assert.Throws<ArgumentException>(() => Evaluate(
            baseline: Candidate(), candidate: Baseline()));
        Assert.Throws<ArgumentException>(() => Evaluate(
            candidate: new EconomicSingleVarietyCornerSupport("opened",
                Baseline().Services)));
        Assert.Throws<ArgumentException>(() => Evaluate(
            candidate: new EconomicSingleVarietyCornerSupport("opened", new[]
            {
                Quoted(ExistingBuyer, .3, Shared, OldBranch),
                Quoted(OpeningBuyer, .1) // nonlocal cannot teleport.
            })));
        Assert.Throws<ArgumentException>(() => Evaluate(
            profiles: Profiles().Where(p => p.ResourceId != ColdDirect)));
        Assert.Throws<ArgumentException>(() => Evaluate(
            candidate: new EconomicSingleVarietyCornerSupport("opened",
                Candidate().Services.Concat(Candidate().Services.Take(1)))));
        Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(minimum: BigInteger.Zero));
        Assert.Throws<ArgumentException>(() => Evaluate(
            buyers: Buyers().Concat(Buyers().Take(1))));
    }

    [Fact]
    public void ComparisonChangesNoWorldStateAndCreatesNoItemOrCashLedger()
    {
        var world = WorldState.CreateInitial();
        var before = new CanonicalWorldStateHasher().Compute(world);
        var r = Evaluate();
        Assert.Equal(2, r.Candidate.Buyers.Count);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }
}
