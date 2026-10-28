using System.Numerics;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicFiniteLotCostExplorerTests
{
    private static readonly StrategicEdgeId Shared = new(11UL);
    private static readonly StrategicEdgeId OldBranch = new(12UL);
    private static readonly StrategicEdgeId Alternative = new(13UL);

    private static FoodStrategicEdgeCongestionProfile[] Profiles(
        ulong alternativeBase = 2UL) => new[]
    {
        new FoodStrategicEdgeCongestionProfile(Shared, 10UL, 1UL, 8UL),
        new FoodStrategicEdgeCongestionProfile(OldBranch, 10UL, 1UL, 8UL),
        new FoodStrategicEdgeCongestionProfile(Alternative, 50UL, alternativeBase, 1UL)
    };

    private static Dictionary<StrategicEdgeId, BigInteger> Loads() => new()
    {
        [Shared] = 60_000_000,
        [OldBranch] = 40_000_000,
        [Alternative] = BigInteger.Zero
    };

    private static EconomicFiniteLotCostComparison Compare(
        IEnumerable<FoodStrategicEdgeCongestionProfile>? profiles = null,
        IEnumerable<StrategicEdgeId>? from = null,
        IEnumerable<StrategicEdgeId>? to = null,
        BigInteger? lot = null,
        IReadOnlyDictionary<StrategicEdgeId, BigInteger>? loads = null) =>
        new EconomicFiniteLotCostExplorer().Compare(
            loads ?? Loads(), profiles ?? Profiles(),
            from ?? new[] { Shared, OldBranch },
            to ?? new[] { Alternative },
            lot ?? new BigInteger(10_000_000));

    [Fact]
    public void FiniteLotCanReduceRealCostWithInitiallyUnusedAlternativeWithoutChangingSource()
    {
        var loads = Loads();
        var after = Compare(loads: loads);

        Assert.True(after.SignedCostDeltaNumerator.Sign < 0);
        Assert.Equal(new BigInteger(60_000_000), loads[Shared]);
        Assert.Equal(new BigInteger(40_000_000), loads[OldBranch]);
        Assert.Equal(BigInteger.Zero, loads[Alternative]);
        Assert.Equal(new BigInteger(50_000_000),
            after.HypotheticalLoadsMicrograms[Shared]);
        Assert.Equal(new BigInteger(30_000_000),
            after.HypotheticalLoadsMicrograms[OldBranch]);
        Assert.Equal(new BigInteger(10_000_000),
            after.HypotheticalLoadsMicrograms[Alternative]);
        Assert.True(after.CostDeltaDenominator.Sign > 0);
    }

    [Fact]
    public void EvaluatesUnfavorableProposalsWithoutRejectingOrSettlingThem()
    {
        // The calculator is not a policy gate. It must return a positive
        // real-cost change, not reject it or invent a financial debt.
        var unfavorable = Compare(profiles: Profiles(alternativeBase: 100_000UL));
        Assert.True(unfavorable.SignedCostDeltaNumerator.Sign > 0);
    }

    [Fact]
    public void SharedEdgeInBothItinerariesRemainsAtItsOriginalLoad()
    {
        var result = Compare(from: new[] { Shared, OldBranch },
            to: new[] { Shared, Alternative });
        Assert.Equal(new BigInteger(60_000_000),
            result.HypotheticalLoadsMicrograms[Shared]);
        Assert.Equal(new BigInteger(30_000_000),
            result.HypotheticalLoadsMicrograms[OldBranch]);
        Assert.Equal(new BigInteger(10_000_000),
            result.HypotheticalLoadsMicrograms[Alternative]);
    }

    [Fact]
    public void OrderOfProfileAndPathEnumerationDoesNotChangeExactDelta()
    {
        var a = Compare();
        var b = Compare(profiles: Profiles().Reverse(),
            from: new[] { OldBranch, Shared });
        Assert.Equal(a.SignedCostDeltaNumerator, b.SignedCostDeltaNumerator);
        Assert.Equal(a.CostDeltaDenominator, b.CostDeltaDenominator);
        Assert.Equal(a.HypotheticalLoadsMicrograms[Shared], b.HypotheticalLoadsMicrograms[Shared]);
    }

    [Fact]
    public void SourcePathMustActuallyContainEnoughAggregatePhysicalMass()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Compare(lot: new BigInteger(50_000_000)));
    }

    [Fact]
    public void MissingOrRepeatedEdgesFailClosed()
    {
        Assert.Throws<ArgumentException>(() => Compare(
            profiles: Profiles().Skip(1)));
        Assert.Throws<ArgumentException>(() => Compare(
            to: new[] { Alternative, Alternative }));
    }

    [Fact]
    public void ZeroBatchAndNegativePhysicalLoadAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Compare(lot: BigInteger.Zero));
        var invalid = Loads();
        invalid[OldBranch] = -1;
        Assert.Throws<ArgumentException>(() => Compare(loads: invalid));
    }

    [Fact]
    public void RepeatedCallsDoNotMutateTheInputOrEachOthersOutput()
    {
        var inputs = Loads();
        var a = Compare(loads: inputs);
        var b = Compare(loads: inputs);
        Assert.Equal(a.SignedCostDeltaNumerator, b.SignedCostDeltaNumerator);
        Assert.Equal(a.CostDeltaDenominator, b.CostDeltaDenominator);
        Assert.Equal(BigInteger.Zero, inputs[Alternative]);
        Assert.Equal(new BigInteger(10_000_000),
            a.HypotheticalLoadsMicrograms[Alternative]);
    }
}
