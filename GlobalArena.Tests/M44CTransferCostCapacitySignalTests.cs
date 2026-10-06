using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M44CTransferCostCapacitySignalTests
{
    [Fact]
    public void PolicyRejectsZeroCostPerHop()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicTransferPolicy(
                0UL,
                1UL,
                0UL,
                0UL));
    }

    [Fact]
    public void PolicyRejectsCostAboveFixedPointDenominator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicTransferPolicy(
                EconomicTransferPolicy.FixedPointDenominator + 1UL,
                1UL,
                0UL,
                0UL));
    }

    [Fact]
    public void PolicyRejectsZeroBaseCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicTransferPolicy(
                1UL,
                0UL,
                0UL,
                0UL));
    }

    [Fact]
    public void PolicyRejectsCongestionAboveFixedPointDenominator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicTransferPolicy(
                1UL,
                1UL,
                0UL,
                EconomicTransferPolicy.FixedPointDenominator + 1UL));
    }

    [Fact]
    public void PolicyAllowsZeroLogisticsIncrementAndZeroCongestion()
    {
        var policy =
            new EconomicTransferPolicy(
                100_000UL,
                10UL,
                0UL,
                0UL);

        Assert.Equal(
            0UL,
            policy.RawCapacityPerTradeLogisticsWorker);

        Assert.Equal(
            0UL,
            policy.RawCongestionUnitCostAtCapacity);
    }

    [Fact]
    public void PolicyExposesConfiguredValues()
    {
        var policy =
            new EconomicTransferPolicy(
                125_000UL,
                1000UL,
                25UL,
                300_000UL);

        Assert.Equal(
            125_000UL,
            policy.RawCostPerHop);

        Assert.Equal(
            1000UL,
            policy.RawBaseRouteCapacity);

        Assert.Equal(
            25UL,
            policy.RawCapacityPerTradeLogisticsWorker);

        Assert.Equal(
            300_000UL,
            policy.RawCongestionUnitCostAtCapacity);
    }

    [Fact]
    public void ResolverRejectsNullEconomy()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EconomicTransferSignalResolver()
                .Resolve(
                    null!,
                    Route(
                        1UL,
                        2UL,
                        1UL,
                        2UL),
                    Policy()));
    }

    [Fact]
    public void ResolverRejectsNullRoute()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EconomicTransferSignalResolver()
                .Resolve(
                    Economy(
                        Point(
                            1UL,
                            1UL,
                            0UL),
                        Point(
                            2UL,
                            2UL,
                            0UL)),
                    null!,
                    Policy()));
    }

    [Fact]
    public void ResolverRejectsNullPolicy()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EconomicTransferSignalResolver()
                .Resolve(
                    Economy(
                        Point(
                            1UL,
                            1UL,
                            0UL),
                        Point(
                            2UL,
                            2UL,
                            0UL)),
                    Route(
                        1UL,
                        2UL,
                        1UL,
                        2UL),
                    null!));
    }

    [Fact]
    public void ResolverRejectsUnknownSourcePoint()
    {
        Assert.Throws<KeyNotFoundException>(
            () => Resolve(
                Economy(
                    Point(
                        2UL,
                        2UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy()));
    }

    [Fact]
    public void ResolverRejectsUnknownDestinationPoint()
    {
        Assert.Throws<KeyNotFoundException>(
            () => Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy()));
    }

    [Fact]
    public void ResolverRejectsSourceAnchorMismatch()
    {
        Assert.Throws<ArgumentException>(
            () => Resolve(
                Economy(
                    Point(
                        1UL,
                        9UL,
                        0UL),
                    Point(
                        2UL,
                        2UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy()));
    }

    [Fact]
    public void ResolverRejectsDestinationAnchorMismatch()
    {
        Assert.Throws<ArgumentException>(
            () => Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        0UL),
                    Point(
                        2UL,
                        9UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy()));
    }

    [Fact]
    public void ZeroLogisticsWorkforcePreservesPositiveBaseCapacity()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        10UL),
                    Point(
                        2UL,
                        2UL,
                        20UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1000UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void SourceTradeLogisticsWorkforceIncreasesCapacity()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        10UL,
                        Logistics(
                            3UL)),
                    Point(
                        2UL,
                        2UL,
                        10UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1075UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void DestinationTradeLogisticsWorkforceIncreasesCapacity()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        10UL),
                    Point(
                        2UL,
                        2UL,
                        10UL,
                        Logistics(
                            4UL))),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1100UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void DistinctEndpointTradeLogisticsWorkforceAddsTogether()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        10UL,
                        Logistics(
                            3UL)),
                    Point(
                        2UL,
                        2UL,
                        10UL,
                        Logistics(
                            4UL))),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1175UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void SamePointRouteDoesNotDoubleCountLogisticsWorkforce()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        10UL,
                        Logistics(
                            3UL))),
                Route(
                    1UL,
                    1UL,
                    1UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1075UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void AgricultureAndMiningWorkforceDoNotIncreaseTransferCapacity()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        10UL,
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture,
                            3UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining,
                            4UL)),
                    Point(
                        2UL,
                        2UL,
                        10UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1000UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void UnallocatedWorkforceDoesNotIncreaseTransferCapacity()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        100UL),
                    Point(
                        2UL,
                        2UL,
                        200UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 1000UL,
                    capacityPerWorker: 25UL));

        Assert.Equal(
            1000UL,
            signal.RawEffectiveCapacity);
    }

    [Fact]
    public void EffectiveCapacityOverflowFailsFast()
    {
        Assert.Throws<OverflowException>(
            () => Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        1UL,
                        Logistics(
                            1UL)),
                    Point(
                        2UL,
                        2UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: ulong.MaxValue,
                    capacityPerWorker: 1UL)));
    }

    [Fact]
    public void BaseUnitCostEqualsHopCountTimesCostPerHop()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        0UL),
                    Point(
                        2UL,
                        4UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL,
                    3UL,
                    4UL),
                Policy(
                    costPerHop: 250_000UL));

        Assert.Equal(
            3,
            signal.HopCount);

        Assert.Equal(
            750_000UL,
            signal.RawBaseUnitCost);
    }

    [Fact]
    public void ZeroHopLocalRouteHasZeroStrategicBaseCost()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        0UL)),
                Route(
                    1UL,
                    1UL,
                    1UL),
                Policy(
                    costPerHop: 900_000UL));

        Assert.Equal(
            0,
            signal.HopCount);

        Assert.Equal(
            0UL,
            signal.RawBaseUnitCost);
    }

    [Fact]
    public void ZeroLoadMarginalCostEqualsBaseUnitCost()
    {
        var signal =
            StandardSignal();

        Assert.Equal(
            signal.RawBaseUnitCost,
            signal.GetRawMarginalUnitCost(
                0UL));
    }

    [Fact]
    public void FullCapacityMarginalCostAddsConfiguredCongestionScale()
    {
        var signal =
            StandardSignal();

        Assert.Equal(
            checked(
                signal.RawBaseUnitCost
                + 200_000UL),
            signal.GetRawMarginalUnitCost(
                signal.RawEffectiveCapacity));
    }

    [Fact]
    public void HalfCapacityMarginalCostUsesLinearFixedPointCongestion()
    {
        var signal =
            StandardSignal();

        Assert.Equal(
            checked(
                signal.RawBaseUnitCost
                + 100_000UL),
            signal.GetRawMarginalUnitCost(
                signal.RawEffectiveCapacity / 2UL));
    }

    [Fact]
    public void LoadAboveEffectiveCapacityIsRejected()
    {
        var signal =
            StandardSignal();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => signal.GetRawMarginalUnitCost(
                checked(
                    signal.RawEffectiveCapacity
                    + 1UL)));
    }

    [Fact]
    public void ZeroCongestionScaleKeepsMarginalCostAtBase()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        0UL),
                    Point(
                        2UL,
                        2UL,
                        0UL)),
                Route(
                    1UL,
                    2UL,
                    1UL,
                    2UL),
                Policy(
                    baseCapacity: 100UL,
                    congestionAtCapacity: 0UL));

        Assert.Equal(
            signal.RawBaseUnitCost,
            signal.GetRawMarginalUnitCost(
                signal.RawEffectiveCapacity));
    }

    [Fact]
    public void LocalZeroHopRouteCanStillExposeHandlingCongestion()
    {
        var signal =
            Resolve(
                Economy(
                    Point(
                        1UL,
                        1UL,
                        0UL)),
                Route(
                    1UL,
                    1UL,
                    1UL),
                Policy(
                    baseCapacity: 100UL,
                    congestionAtCapacity: 200_000UL));

        Assert.Equal(
            200_000UL,
            signal.GetRawMarginalUnitCost(
                100UL));
    }

    [Fact]
    public void RepeatedResolutionIsDeterministic()
    {
        var economy =
            Economy(
                Point(
                    1UL,
                    1UL,
                    10UL,
                    Logistics(
                        2UL)),
                Point(
                    2UL,
                    3UL,
                    10UL,
                    Logistics(
                        1UL)));

        var route =
            Route(
                1UL,
                2UL,
                1UL,
                2UL,
                3UL);

        var policy =
            Policy(
                costPerHop: 150_000UL,
                baseCapacity: 1000UL,
                capacityPerWorker: 20UL,
                congestionAtCapacity: 250_000UL);

        var first =
            Resolve(
                economy,
                route,
                policy);

        var second =
            Resolve(
                economy,
                route,
                policy);

        Assert.Equal(
            first.RouteKey,
            second.RouteKey);

        Assert.Equal(
            first.HopCount,
            second.HopCount);

        Assert.Equal(
            first.RawBaseUnitCost,
            second.RawBaseUnitCost);

        Assert.Equal(
            first.RawEffectiveCapacity,
            second.RawEffectiveCapacity);

        Assert.Equal(
            first.GetRawMarginalUnitCost(
                first.RawEffectiveCapacity),
            second.GetRawMarginalUnitCost(
                second.RawEffectiveCapacity));
    }

    private static EconomicTransferSignal StandardSignal()
    {
        return Resolve(
            Economy(
                Point(
                    1UL,
                    1UL,
                    0UL),
                Point(
                    2UL,
                    2UL,
                    0UL)),
            Route(
                1UL,
                2UL,
                1UL,
                2UL),
            Policy(
                costPerHop: 100_000UL,
                baseCapacity: 100UL,
                congestionAtCapacity: 200_000UL));
    }

    private static EconomicTransferSignal Resolve(
        EconomyRuntimeState economy,
        StrategicEconomicRoute route,
        EconomicTransferPolicy policy)
    {
        return new EconomicTransferSignalResolver()
            .Resolve(
                economy,
                route,
                policy);
    }

    private static EconomicTransferPolicy Policy(
        ulong costPerHop = 100_000UL,
        ulong baseCapacity = 1000UL,
        ulong capacityPerWorker = 10UL,
        ulong congestionAtCapacity = 200_000UL)
    {
        return new EconomicTransferPolicy(
            costPerHop,
            baseCapacity,
            capacityPerWorker,
            congestionAtCapacity);
    }

    private static EconomyRuntimeState Economy(
        params EconomicPointRuntimeState[] points)
    {
        return new EconomyRuntimeState(
            points);
    }

    private static EconomicPointRuntimeState Point(
        ulong id,
        ulong strategicCellId,
        ulong workforce,
        params EconomicActivityWorkforceAllocation[] allocations)
    {
        return new EconomicPointRuntimeState(
            new EconomicPointId(
                id),
            new CivilizationId(
                1UL),
            new StrategicCellId(
                strategicCellId),
            workforce,
            allocations);
    }

    private static EconomicActivityWorkforceAllocation Logistics(
        ulong workforce)
    {
        return new EconomicActivityWorkforceAllocation(
            EconomicActivityKind.TradeLogistics,
            workforce);
    }

    private static StrategicEconomicRoute Route(
        ulong sourcePointId,
        ulong destinationPointId,
        params ulong[] strategicCellIds)
    {
        if (strategicCellIds.Length == 0)
        {
            throw new ArgumentException(
                "Test route must contain at least one strategic cell.",
                nameof(strategicCellIds));
        }

        var cells =
            strategicCellIds
                .Select(
                    value =>
                        new StrategicCellId(
                            value))
                .ToArray();

        var edges =
            Enumerable
                .Range(
                    1,
                    Math.Max(
                        0,
                        cells.Length - 1))
                .Select(
                    value =>
                        new StrategicEdgeId(
                            (ulong)value))
                .ToArray();

        return new StrategicEconomicRoute(
            new StrategicEconomicRouteKey(
                new EconomicPointId(
                    sourcePointId),
                new EconomicPointId(
                    destinationPointId)),
            cells,
            edges);
    }
}
