using System.Numerics;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicFiniteLotDensityCostExplorerTests
{
    private static readonly EconomicPhysicalResourceId Ad = new(11UL);
    private static readonly EconomicPhysicalResourceId De = new(12UL);
    private static readonly EconomicPhysicalResourceId Df = new(13UL);
    private static readonly EconomicPhysicalResourceId TransferD = new(14UL);
    private static readonly EconomicPhysicalResourceId DirectAe = new(15UL);

    // GA-ECON-004B reference parameters, expressed in 0.001 raw cost
    // units and whole physical grams. Calibration is a test example,
    // not a universal value for any real transport technology.
    private static EconomicDensityResourceCostProfile[] Profiles() => new[]
    {
        new EconomicDensityResourceCostProfile(Ad, 400UL, 50UL, 2_000UL, 15UL),
        new EconomicDensityResourceCostProfile(De, 100UL, 20UL, 400UL, 20UL),
        new EconomicDensityResourceCostProfile(Df, 150UL, 20UL, 400UL, 20UL),
        new EconomicDensityResourceCostProfile(TransferD, 60UL, 15UL, 300UL, 15UL),
        new EconomicDensityResourceCostProfile(DirectAe, 160UL, 1_100UL, 250UL, 24UL)
    };

    private static Dictionary<EconomicPhysicalResourceId, BigInteger> Loads(bool otherTradeToF = true) => new()
    {
        [Ad] = otherTradeToF ? 10_000_000 : 6_000_000,
        [De] = 6_000_000,
        [Df] = otherTradeToF ? 4_000_000 : 0,
        [TransferD] = 6_000_000,
        [DirectAe] = 0
    };

    private static EconomicFiniteLotDensityCostComparison Compare(
        BigInteger lot,
        bool otherTradeToF = true,
        IEnumerable<EconomicDensityResourceCostProfile>? profiles = null,
        IEnumerable<EconomicPhysicalResourceId>? from = null,
        IEnumerable<EconomicPhysicalResourceId>? to = null,
        IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger>? original = null) =>
        new EconomicFiniteLotDensityCostExplorer().Compare(
            original ?? Loads(otherTradeToF),
            profiles ?? Profiles(),
            from ?? new[] { Ad, De, TransferD },
            to ?? new[] { DirectAe },
            lot);

    private static double Lower(EconomicFiniteLotDensityCostComparison x) =>
        (double)x.SignedCostDeltaLowerNumerator / (double)x.CostDeltaLowerDenominator;

    private static double Upper(EconomicFiniteLotDensityCostComparison x) =>
        (double)x.SignedCostDeltaUpperNumerator / (double)x.CostDeltaUpperDenominator;

    [Fact]
    public void TinyBatchWorsensCostButFiniteSixGramBatchImprovesIt()
    {
        var tiny = Compare(BigInteger.One);
        var full = Compare(6_000_000);
        Assert.Equal(EconomicDensityCostDeltaClassification.CertifiedCostIncrease,
            tiny.CostSign);
        Assert.Equal(EconomicDensityCostDeltaClassification.CertifiedCostDecrease,
            full.CostSign);
        Assert.InRange(Lower(tiny), 0.0021, 0.0023);
        Assert.InRange(Upper(full), -0.2091, -0.2087);
        Assert.Equal(new BigInteger(4_000_000),
            full.HypotheticalLoadsMicrograms[Ad]);
        Assert.Equal(BigInteger.Zero,
            full.HypotheticalLoadsMicrograms[TransferD]);
        Assert.Equal(new BigInteger(6_000_000),
            full.HypotheticalLoadsMicrograms[DirectAe]);
        Assert.Equal(new BigInteger(4_000_000),
            full.HypotheticalLoadsMicrograms[Df]);
    }

    [Fact]
    public void SharedTrafficCanReverseTheDecisionWithoutAnyFinancialCredit()
    {
        var withTraffic = Compare(6_000_000, otherTradeToF: true);
        var withoutTraffic = Compare(6_000_000, otherTradeToF: false);
        Assert.Equal(EconomicDensityCostDeltaClassification.CertifiedCostDecrease,
            withTraffic.CostSign);
        Assert.Equal(EconomicDensityCostDeltaClassification.CertifiedCostIncrease,
            withoutTraffic.CostSign);
    }

    [Fact]
    public void ZeroFlowCostsExactlyZeroAndIrrationalRootsHaveCertifiedBounds()
    {
        var direct = Profiles().Single(x => x.ResourceId == DirectAe);
        var zero = direct.EvaluateTotalCostBounds(BigInteger.Zero);
        Assert.Equal(BigInteger.Zero, zero.Lower.Numerator);
        Assert.Equal(BigInteger.Zero, zero.Upper.Numerator);
        var twoGrams = direct.EvaluateTotalCostBounds(2_000_000);
        Assert.True(twoGrams.Lower.CompareTo(twoGrams.Upper) < 0);
        var truth = .16 * 2 + 2 * 1.1 * Math.Sqrt(2)
            + .25 * Math.Pow(2, 3) / (3 * 24 * 24);
        var low = (double)twoGrams.Lower.Numerator / (double)twoGrams.Lower.Denominator;
        var up = (double)twoGrams.Upper.Numerator / (double)twoGrams.Upper.Denominator;
        Assert.True(low <= truth + 1e-12);
        Assert.True(up >= truth - 1e-12);
    }

    [Fact]
    public void InputPermutationAndRepeatedCallsGiveIdenticalRationalBounds()
    {
        var before = Loads();
        var one = Compare(6_000_000, original: before);
        var two = Compare(6_000_000, original: before,
            profiles: Profiles().Reverse(),
            from: new[] { TransferD, De, Ad });
        Assert.Equal(one.SignedCostDeltaLowerNumerator,
            two.SignedCostDeltaLowerNumerator);
        Assert.Equal(one.CostDeltaLowerDenominator,
            two.CostDeltaLowerDenominator);
        Assert.Equal(one.SignedCostDeltaUpperNumerator,
            two.SignedCostDeltaUpperNumerator);
        Assert.Equal(one.CostDeltaUpperDenominator,
            two.CostDeltaUpperDenominator);
        Assert.Equal(BigInteger.Zero, before[DirectAe]);
        Assert.Equal(new BigInteger(10_000_000), before[Ad]);
    }

    [Fact]
    public void UnchangedSharedResourceCancelsWithoutIntroducingRootUncertainty()
    {
        var input = Loads();
        var c = Compare(1_000_000,
            from: new[] { Ad, De }, to: new[] { Ad, DirectAe }, original: input);
        Assert.Equal(new BigInteger(10_000_000),
            c.HypotheticalLoadsMicrograms[Ad]);
        Assert.Equal(new BigInteger(5_000_000),
            c.HypotheticalLoadsMicrograms[De]);
        Assert.Equal(new BigInteger(1_000_000),
            c.HypotheticalLoadsMicrograms[DirectAe]);
    }

    [Fact]
    public void RejectsInsufficientBatchAndInvalidOrMissingProfiles()
    {
        Assert.Throws<InvalidOperationException>(() => Compare(7_000_000));
        Assert.Throws<ArgumentException>(() => Compare(1_000_000,
            profiles: Profiles().Where(x => x.ResourceId != De)));
        Assert.Throws<ArgumentException>(() => Compare(1_000_000,
            profiles: Profiles().Concat(Profiles().Take(1))));
        Assert.Throws<ArgumentException>(() => Compare(1_000_000,
            to: new[] { DirectAe, DirectAe }));
    }

    [Fact]
    public void RejectsNonpositiveBatchAndNegativePhysicalLoad()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Compare(BigInteger.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compare(-1));
        var negative = Loads();
        negative[Df] = -1;
        Assert.Throws<ArgumentException>(() => Compare(1, original: negative));
    }

    [Fact]
    public void SquareRootBoundsAndHugeCostRemainDeterministic()
    {
        var profile = Profiles().Single(x => x.ResourceId == DirectAe);
        var huge = BigInteger.Pow(10, 40);
        var a = profile.EvaluateTotalCostBounds(huge);
        var b = profile.EvaluateTotalCostBounds(huge);
        Assert.Equal(a.Lower.Numerator, b.Lower.Numerator);
        Assert.Equal(a.Lower.Denominator, b.Lower.Denominator);
        Assert.Equal(a.Upper.Numerator, b.Upper.Numerator);
        Assert.Equal(a.Upper.Denominator, b.Upper.Denominator);
        Assert.True(a.Lower.Numerator.Sign > 0);
    }
}
