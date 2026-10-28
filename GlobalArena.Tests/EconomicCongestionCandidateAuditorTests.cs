using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicCongestionCandidateAuditorTests
{
    private static readonly EconomicPointId Origin = new(1UL);
    private static readonly EconomicPointId Destination = new(2UL);
    private static readonly CommodityId Grain = new(7U);
    private static readonly CommodityId Roots = new(8U);
    private static readonly StrategicEdgeId Edge = new(5UL);

    private static FoodStrategicEdgeCongestionProfile Profile(ulong scale = 10UL) =>
        new(Edge, 100UL, 0UL, scale);

    private static FoodCommodityCatalog Catalog() => new(new[]
    {
        new FoodCommodityDefinition(Grain, "grain", 1_000UL),
        new FoodCommodityDefinition(Roots, "roots", 2_000UL)
    });

    private static EconomicProductionDemandResult Flows(
        ulong grainSupply = 5UL, ulong rootsSupply = 3UL) => new(
        new[]
        {
            new EconomicSupplyFlow(Origin, Grain, EconomicActivityKind.Agriculture, grainSupply),
            new EconomicSupplyFlow(Origin, Roots, EconomicActivityKind.Agriculture, rootsSupply)
        },
        new[]
        {
            new EconomicDemandFlow(Destination, Grain, 5UL),
            new EconomicDemandFlow(Destination, Roots, 3UL)
        });

    private static EconomicCongestionMarketResult Candidate(
        FoodStrategicEdgeCongestionProfile profile,
        BigInteger? reportedLoad = null,
        FoodTransportCostFraction? firstIncrement = null,
        FoodTransportCostFraction? secondIncrement = null,
        FoodStrategicEdgeCongestionProfile? reportingProfile = null)
    {
        // Raw 5 grain units + 3 roots units on the SAME resource:
        // 5*1000 + 3*2000 = 11,000 micrograms.
        var grainMass = new BigInteger(5_000);
        var totalMass = new BigInteger(11_000);
        var grainCost = profile.EvaluateTotalCost(grainMass);
        var totalCost = profile.EvaluateTotalCost(totalMass);
        var rootsIncrement = new FoodTransportCostFraction(
            totalCost.Numerator * grainCost.Denominator -
                grainCost.Numerator * totalCost.Denominator,
            totalCost.Denominator * grainCost.Denominator);

        var selections = new[]
        {
            new EconomicCongestionMarketSelection(
                1UL, Origin, EconomicActivityKind.Agriculture, Destination,
                Grain, 5UL, 9_000_000UL, 0UL,
                firstIncrement ?? grainCost, new[] { Edge }),
            new EconomicCongestionMarketSelection(
                2UL, Origin, EconomicActivityKind.Agriculture, Destination,
                Roots, 3UL, 9_000_000UL, 0UL,
                secondIncrement ?? rootsIncrement, new[] { Edge })
        };
        return new EconomicCongestionMarketResult(selections,
            new FoodStrategicEdgeCongestionResult(new[]
            {
                new FoodStrategicEdgeCongestionEntry(
                    reportingProfile ?? profile, reportedLoad ?? totalMass)
            }));
    }

    private static void Audit(EconomicCongestionMarketResult candidate,
        ulong grainSupply = 5UL,
        FoodStrategicEdgeCongestionProfile? declaredProfile = null) =>
        new EconomicCongestionCandidateAuditor().Audit(
            Flows(grainSupply: grainSupply), Catalog(),
            new[] { declaredProfile ?? Profile() }, candidate);

    [Fact]
    public void AcceptsSharedEdgeWithTwoFoodsAndExactIntegratedIncrement()
    {
        Audit(Candidate(Profile()));
    }

    [Fact]
    public void RejectsAReportedLoadThatDoesNotIncludeBothFoods()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Audit(Candidate(Profile(), reportedLoad: new BigInteger(5_000))));
    }

    [Fact]
    public void RejectsAReportEvaluatedAgainstAnUndeclaredCostCurve()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Audit(Candidate(Profile(), reportingProfile: Profile(20UL))));
    }

    [Fact]
    public void RejectsAFalseZeroIncrementEvenIfTheFinalEdgeReportIsCorrect()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Audit(Candidate(Profile(), firstIncrement:
                new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One))));
    }

    [Fact]
    public void RejectsShiftedTrancheChargesEvenWhenGrandTotalStillBalances()
    {
        var profile = Profile();
        var originalFirst = profile.EvaluateTotalCost(new BigInteger(5_000));
        var total = profile.EvaluateTotalCost(new BigInteger(11_000));
        var originalSecond = new FoodTransportCostFraction(
            total.Numerator * originalFirst.Denominator -
                originalFirst.Numerator * total.Denominator,
            total.Denominator * originalFirst.Denominator);

        // A dishonest candidate swaps the two valid positive charges.
        // Its final edge load and TOTAL real resource cost are still exact;
        // only the attribution to the individual economic decisions is false.
        var falsified = Candidate(profile,
            firstIncrement: originalSecond,
            secondIncrement: originalFirst);
        Assert.Throws<InvalidOperationException>(() => Audit(falsified));
    }

    [Fact]
    public void RejectsAnUnchargedFirstTrancheHiddenByLaterFullCost()
    {
        var profile = Profile();
        var total = profile.EvaluateTotalCost(new BigInteger(11_000));
        var falsified = Candidate(profile,
            firstIncrement: new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One),
            secondIncrement: total);
        Assert.Throws<InvalidOperationException>(() => Audit(falsified));
    }

    [Fact]
    public void RejectsTradeThatExceedsAvailableGoods()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Audit(Candidate(Profile()), grainSupply: 4UL));
    }

    [Fact]
    public void RejectsMissingResourceCostProfile()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicCongestionCandidateAuditor().Audit(
                Flows(), Catalog(), Array.Empty<FoodStrategicEdgeCongestionProfile>(),
                Candidate(Profile())));
    }
}
