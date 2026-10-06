using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class M44DMarketAllocationSettlementTests
{
    [Fact]
    public void PolicyRejectsZeroInitialValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicMarketAllocationPolicy(
                0UL,
                0UL,
                1,
                1));
    }

    [Fact]
    public void PolicyRejectsFloorAboveInitialValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicMarketAllocationPolicy(
                100UL,
                101UL,
                1,
                1));
    }

    [Fact]
    public void PolicyRejectsZeroDemandBands()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicMarketAllocationPolicy(
                100UL,
                0UL,
                0,
                1));
    }

    [Fact]
    public void PolicyRejectsTooManyDemandBands()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicMarketAllocationPolicy(
                100UL,
                0UL,
                EconomicMarketAllocationPolicy.MaxBandCount + 1,
                1));
    }

    [Fact]
    public void PolicyRejectsZeroTransferBands()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicMarketAllocationPolicy(
                100UL,
                0UL,
                1,
                0));
    }

    [Fact]
    public void PolicyRejectsTooManyTransferBands()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicMarketAllocationPolicy(
                100UL,
                0UL,
                1,
                EconomicMarketAllocationPolicy.MaxBandCount + 1));
    }

    [Fact]
    public void TradeAllocationRejectsNonPositiveUnitReturn()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EconomicTradeAllocation(
                PointId(1UL),
                EconomicActivityKind.Agriculture,
                PointId(2UL),
                Commodity(1U),
                10UL,
                100_000UL,
                100_000UL));
    }

    [Fact]
    public void TradeAllocationComputesFixedPointSettlement()
    {
        var allocation =
            new EconomicTradeAllocation(
                PointId(1UL),
                EconomicActivityKind.Agriculture,
                PointId(2UL),
                Commodity(1U),
                10UL,
                900_000UL,
                200_000UL);

        Assert.Equal(
            7UL,
            allocation.RawNetSettlementReturn);

        Assert.Equal(
            9UL,
            allocation.RawGrossSettlementValue);

        Assert.Equal(
            2UL,
            allocation.RawTransferSettlementCost);

        Assert.Equal(
            700_000UL,
            allocation.RawUnitNetReturn);
    }

    [Fact]
    public void EmptySupplyAndDemandProducesEmptyResult()
    {
        var result =
            Resolve(
                ProductionDemand(),
                Array.Empty<EconomicTransferSignal>(),
                Policy());

        Assert.Empty(
            result.Allocations);

        Assert.Equal(
            0UL,
            result.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void MissingTransferSignalProducesNoAllocation()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                Array.Empty<EconomicTransferSignal>(),
                Policy());

        Assert.Empty(
            result.Allocations);
    }

    [Fact]
    public void DuplicateTransferSignalsAreRejected()
    {
        var input =
            ProductionDemand(
                supplies: new[]
                {
                    Supply(
                        1UL,
                        1U,
                        10UL)
                },
                demands: new[]
                {
                    Demand(
                        2UL,
                        1U,
                        10UL)
                });

        var signal =
            Signal(
                1UL,
                2UL,
                100UL,
                0UL,
                0UL);

        Assert.Throws<ArgumentException>(
            () => Resolve(
                input,
                new[]
                {
                    signal,
                    signal
                },
                Policy()));
    }

    [Fact]
    public void TransferSignalSourceMustHaveSupply()
    {
        Assert.Throws<ArgumentException>(
            () => Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        3UL,
                        2UL,
                        10UL,
                        0UL,
                        0UL)
                },
                Policy()));
    }

    [Fact]
    public void TransferSignalDestinationMustHaveDemand()
    {
        Assert.Throws<ArgumentException>(
            () => Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        3UL,
                        10UL,
                        0UL,
                        0UL)
                },
                Policy()));
    }

    [Fact]
    public void ProfitableSingleRouteAllocatesMinimumOfSupplyDemandAndCapacity()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            80UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        60UL,
                        100_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL,
                    demandBands: 1,
                    transferBands: 1));

        Assert.Equal(
            60UL,
            result.TotalRawAllocatedQuantity);

        Assert.Single(
            result.Allocations);
    }

    [Fact]
    public void UnprofitableRouteDoesNotAllocate()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        10UL,
                        900_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 500_000UL,
                    floorValue: 500_000UL));

        Assert.Empty(
            result.Allocations);
    }

    [Fact]
    public void EqualValueAndTransferCostDoesNotAllocate()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        10UL,
                        500_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 500_000UL,
                    floorValue: 500_000UL));

        Assert.Empty(
            result.Allocations);
    }

    [Fact]
    public void SupplyQuantityIsNeverExceeded()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            15UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            100UL),
                        Demand(
                            3UL,
                            1U,
                            100UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        10_000UL,
                        0UL),
                    Signal(
                        1UL,
                        3UL,
                        100UL,
                        10_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Equal(
            15UL,
            result.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void DemandQuantityIsNeverExceeded()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            100UL),
                        Supply(
                            4UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            12UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        10_000UL,
                        0UL),
                    Signal(
                        4UL,
                        2UL,
                        100UL,
                        10_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Equal(
            12UL,
            result.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void RouteCapacityIsSharedAcrossSourceActivities()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            50UL,
                            EconomicActivityKind.Agriculture),
                        Supply(
                            1UL,
                            1U,
                            50UL,
                            EconomicActivityKind.Mining)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            100UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        30UL,
                        0UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Equal(
            30UL,
            result.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void CommodityPartitionsUseTransferCapacityIndependently()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            20UL),
                        Supply(
                            1UL,
                            2U,
                            20UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            20UL),
                        Demand(
                            2UL,
                            2U,
                            20UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        10UL,
                        0UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Equal(
            20UL,
            result.TotalRawAllocatedQuantity);

        Assert.Equal(
            2,
            result.Allocations.Count);
    }

    [Fact]
    public void DemandBandsReduceMarginalValueDeterministically()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            100UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        450_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 1_000_000UL,
                    floorValue: 200_000UL,
                    demandBands: 4,
                    transferBands: 1));

        Assert.Equal(
            50UL,
            result.TotalRawAllocatedQuantity);

        Assert.Equal(
            new ulong[]
            {
                800_000UL,
                600_000UL
            },
            result.Allocations
                .Select(
                    allocation =>
                        allocation.RawUnitValue)
                .ToArray());
    }

    [Fact]
    public void TransferBandsIncreaseMarginalCostAndCanStopAllocation()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            100UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        100_000UL,
                        800_000UL)
                },
                Policy(
                    initialValue: 550_000UL,
                    floorValue: 550_000UL,
                    demandBands: 1,
                    transferBands: 4));

        Assert.Equal(
            50UL,
            result.TotalRawAllocatedQuantity);

        Assert.Equal(
            new ulong[]
            {
                300_000UL,
                500_000UL
            },
            result.Allocations
                .Select(
                    allocation =>
                        allocation.RawUnitTransferCost)
                .Order()
                .ToArray());
    }

    [Fact]
    public void CheapestReachableSourceWinsWhenSupplyIsAbundant()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            100UL),
                        Supply(
                            3UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            50UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        100_000UL,
                        0UL),
                    Signal(
                        3UL,
                        2UL,
                        100UL,
                        300_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Single(
            result.Allocations);

        Assert.Equal(
            PointId(1UL),
            result.Allocations[0].SourceEconomicPointId);
    }

    [Fact]
    public void CanonicalTieChoosesLowerSourcePointFirst()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            5UL,
                            1U,
                            100UL),
                        Supply(
                            1UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            50UL)
                    }),
                new[]
                {
                    Signal(
                        5UL,
                        2UL,
                        100UL,
                        100_000UL,
                        0UL),
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        100_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Single(
            result.Allocations);

        Assert.Equal(
            PointId(1UL),
            result.Allocations[0].SourceEconomicPointId);
    }

    [Fact]
    public void CanonicalDestinationTieChoosesLowerDestinationPointFirst()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            50UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            4UL,
                            1U,
                            50UL),
                        Demand(
                            2UL,
                            1U,
                            50UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        4UL,
                        50UL,
                        100_000UL,
                        0UL),
                    Signal(
                        1UL,
                        2UL,
                        50UL,
                        100_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Single(
            result.Allocations);

        Assert.Equal(
            PointId(2UL),
            result.Allocations[0].DestinationEconomicPointId);
    }

    [Fact]
    public void AgricultureAndMiningRemainDistinctAllocationSources()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL,
                            EconomicActivityKind.Agriculture),
                        Supply(
                            1UL,
                            1U,
                            10UL,
                            EconomicActivityKind.Mining)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            20UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        20UL,
                        0UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Equal(
            2,
            result.Allocations.Count);

        Assert.Contains(
            result.Allocations,
            allocation =>
                allocation.SourceActivity
                == EconomicActivityKind.Agriculture);

        Assert.Contains(
            result.Allocations,
            allocation =>
                allocation.SourceActivity
                == EconomicActivityKind.Mining);
    }

    [Fact]
    public void SamePointTradeCanSettleWithZeroTransferCost()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            1UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        1UL,
                        10UL,
                        0UL,
                        0UL)
                },
                Policy(
                    initialValue: 500_000UL,
                    floorValue: 500_000UL));

        var allocation =
            Assert.Single(
                result.Allocations);

        Assert.Equal(
            0UL,
            allocation.RawUnitTransferCost);

        Assert.Equal(
            5UL,
            allocation.RawGrossSettlementValue);

        Assert.Equal(
            5UL,
            allocation.RawNetSettlementReturn);
    }

    [Fact]
    public void InputOrderingDoesNotChangeCanonicalResult()
    {
        var supplies =
            new[]
            {
                Supply(
                    4UL,
                    1U,
                    20UL),
                Supply(
                    1UL,
                    1U,
                    20UL)
            };

        var demands =
            new[]
            {
                Demand(
                    3UL,
                    1U,
                    20UL),
                Demand(
                    2UL,
                    1U,
                    20UL)
            };

        var signals =
            new[]
            {
                Signal(
                    4UL,
                    3UL,
                    20UL,
                    200_000UL,
                    0UL),
                Signal(
                    1UL,
                    2UL,
                    20UL,
                    100_000UL,
                    0UL),
                Signal(
                    4UL,
                    2UL,
                    20UL,
                    250_000UL,
                    0UL),
                Signal(
                    1UL,
                    3UL,
                    20UL,
                    150_000UL,
                    0UL)
            };

        var first =
            Resolve(
                ProductionDemand(
                    supplies,
                    demands),
                signals,
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 600_000UL,
                    demandBands: 2,
                    transferBands: 2));

        var second =
            Resolve(
                ProductionDemand(
                    supplies.Reverse(),
                    demands.Reverse()),
                signals.Reverse(),
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 600_000UL,
                    demandBands: 2,
                    transferBands: 2));

        Assert.Equal(
            first.Allocations.Count,
            second.Allocations.Count);

        for (var index = 0;
             index < first.Allocations.Count;
             index++)
        {
            var left =
                first.Allocations[index];

            var right =
                second.Allocations[index];

            Assert.Equal(
                left.CommodityId,
                right.CommodityId);

            Assert.Equal(
                left.SourceEconomicPointId,
                right.SourceEconomicPointId);

            Assert.Equal(
                left.SourceActivity,
                right.SourceActivity);

            Assert.Equal(
                left.DestinationEconomicPointId,
                right.DestinationEconomicPointId);

            Assert.Equal(
                left.RawQuantity,
                right.RawQuantity);

            Assert.Equal(
                left.RawUnitValue,
                right.RawUnitValue);

            Assert.Equal(
                left.RawUnitTransferCost,
                right.RawUnitTransferCost);
        }
    }

    [Fact]
    public void ResultCanonicalizesAllocationOrder()
    {
        var highValue =
            new EconomicTradeAllocation(
                PointId(1UL),
                EconomicActivityKind.Agriculture,
                PointId(2UL),
                Commodity(1U),
                1UL,
                900_000UL,
                100_000UL);

        var lowValue =
            new EconomicTradeAllocation(
                PointId(1UL),
                EconomicActivityKind.Agriculture,
                PointId(2UL),
                Commodity(1U),
                1UL,
                800_000UL,
                100_000UL);

        var result =
            new EconomicMarketAllocationResult(
                new[]
                {
                    lowValue,
                    highValue
                });

        Assert.Equal(
            900_000UL,
            result.Allocations[0].RawUnitValue);

        Assert.Equal(
            800_000UL,
            result.Allocations[1].RawUnitValue);
    }

    [Fact]
    public void ResultRejectsDuplicateCanonicalAllocationKeys()
    {
        var first =
            new EconomicTradeAllocation(
                PointId(1UL),
                EconomicActivityKind.Agriculture,
                PointId(2UL),
                Commodity(1U),
                1UL,
                900_000UL,
                100_000UL);

        var second =
            new EconomicTradeAllocation(
                PointId(1UL),
                EconomicActivityKind.Agriculture,
                PointId(2UL),
                Commodity(1U),
                2UL,
                900_000UL,
                100_000UL);

        Assert.Throws<ArgumentException>(
            () => new EconomicMarketAllocationResult(
                new[]
                {
                    first,
                    second
                }));
    }

    [Fact]
    public void SettlementTotalsEqualAllocationTotals()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        10UL,
                        100_000UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        Assert.Equal(
            result.Allocations.Aggregate(
                0UL,
                (total, allocation) =>
                    checked(
                        total
                        + allocation.RawGrossSettlementValue)),
            result.TotalRawGrossSettlementValue);

        Assert.Equal(
            result.Allocations.Aggregate(
                0UL,
                (total, allocation) =>
                    checked(
                        total
                        + allocation.RawTransferSettlementCost)),
            result.TotalRawTransferSettlementCost);

        Assert.Equal(
            result.Allocations.Aggregate(
                0UL,
                (total, allocation) =>
                    checked(
                        total
                        + allocation.RawNetSettlementReturn)),
            result.TotalRawNetSettlementReturn);
    }

    [Fact]
    public void EveryEmittedAllocationHasStrictlyPositiveUnitNetReturn()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            100UL),
                        Supply(
                            3UL,
                            1U,
                            100UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            100UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        100UL,
                        100_000UL,
                        600_000UL),
                    Signal(
                        3UL,
                        2UL,
                        100UL,
                        200_000UL,
                        500_000UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 300_000UL,
                    demandBands: 4,
                    transferBands: 4));

        Assert.All(
            result.Allocations,
            allocation =>
                Assert.True(
                    allocation.RawUnitValue
                    > allocation.RawUnitTransferCost));
    }

    [Fact]
    public void RouteWithNoCommodityOverlapIsIgnoredWithoutChangingOtherCommodity()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL),
                        Supply(
                            3UL,
                            2U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL),
                        Demand(
                            4UL,
                            3U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        10UL,
                        0UL,
                        0UL),
                    Signal(
                        3UL,
                        4UL,
                        10UL,
                        0UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL));

        var allocation =
            Assert.Single(
                result.Allocations);

        Assert.Equal(
            Commodity(1U),
            allocation.CommodityId);
    }

    [Fact]
    public void DemandBandCountGreaterThanQuantityStillPreservesExactDemand()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            3UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            3UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        3UL,
                        0UL,
                        0UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL,
                    demandBands: 64,
                    transferBands: 64));

        Assert.Equal(
            3UL,
            result.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void TransferBandCountGreaterThanCapacityStillPreservesExactCapacity()
    {
        var result =
            Resolve(
                ProductionDemand(
                    supplies: new[]
                    {
                        Supply(
                            1UL,
                            1U,
                            10UL)
                    },
                    demands: new[]
                    {
                        Demand(
                            2UL,
                            1U,
                            10UL)
                    }),
                new[]
                {
                    Signal(
                        1UL,
                        2UL,
                        3UL,
                        0UL,
                        100_000UL)
                },
                Policy(
                    initialValue: 900_000UL,
                    floorValue: 900_000UL,
                    demandBands: 64,
                    transferBands: 64));

        Assert.Equal(
            3UL,
            result.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void ResolverRejectsNullProductionDemand()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EconomicMarketAllocationResolver()
                .Resolve(
                    null!,
                    Array.Empty<EconomicTransferSignal>(),
                    Policy()));
    }

    [Fact]
    public void ResolverRejectsNullTransferSignals()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EconomicMarketAllocationResolver()
                .Resolve(
                    ProductionDemand(),
                    null!,
                    Policy()));
    }

    [Fact]
    public void ResolverRejectsNullPolicy()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EconomicMarketAllocationResolver()
                .Resolve(
                    ProductionDemand(),
                    Array.Empty<EconomicTransferSignal>(),
                    null!));
    }

    [Fact]
    public void ResolverRejectsNullTransferSignalEntry()
    {
        Assert.Throws<ArgumentException>(
            () => new EconomicMarketAllocationResolver()
                .Resolve(
                    ProductionDemand(
                        supplies: new[]
                        {
                            Supply(
                                1UL,
                                1U,
                                1UL)
                        },
                        demands: new[]
                        {
                            Demand(
                                2UL,
                                1U,
                                1UL)
                        }),
                    new EconomicTransferSignal[]
                    {
                        null!
                    },
                    Policy()));
    }

    private static EconomicMarketAllocationResult Resolve(
        EconomicProductionDemandResult productionDemand,
        IEnumerable<EconomicTransferSignal> signals,
        EconomicMarketAllocationPolicy policy)
    {
        return new EconomicMarketAllocationResolver()
            .Resolve(
                productionDemand,
                signals,
                policy);
    }

    private static EconomicProductionDemandResult ProductionDemand(
        IEnumerable<EconomicSupplyFlow>? supplies = null,
        IEnumerable<EconomicDemandFlow>? demands = null)
    {
        return new EconomicProductionDemandResult(
            supplies
                ?? Array.Empty<EconomicSupplyFlow>(),
            demands
                ?? Array.Empty<EconomicDemandFlow>());
    }

    private static EconomicSupplyFlow Supply(
        ulong pointId,
        uint commodityId,
        ulong quantity,
        EconomicActivityKind activity =
            EconomicActivityKind.Agriculture)
    {
        return new EconomicSupplyFlow(
            PointId(
                pointId),
            Commodity(
                commodityId),
            activity,
            quantity);
    }

    private static EconomicDemandFlow Demand(
        ulong pointId,
        uint commodityId,
        ulong quantity)
    {
        return new EconomicDemandFlow(
            PointId(
                pointId),
            Commodity(
                commodityId),
            quantity);
    }

    private static EconomicTransferSignal Signal(
        ulong sourcePointId,
        ulong destinationPointId,
        ulong capacity,
        ulong baseUnitCost,
        ulong congestionAtCapacity)
    {
        return new EconomicTransferSignal(
            new StrategicEconomicRouteKey(
                PointId(
                    sourcePointId),
                PointId(
                    destinationPointId)),
            sourcePointId == destinationPointId
                ? 0
                : 1,
            baseUnitCost,
            capacity,
            congestionAtCapacity);
    }

    private static EconomicMarketAllocationPolicy Policy(
        ulong initialValue = 900_000UL,
        ulong floorValue = 900_000UL,
        int demandBands = 1,
        int transferBands = 1)
    {
        return new EconomicMarketAllocationPolicy(
            initialValue,
            floorValue,
            demandBands,
            transferBands);
    }

    private static EconomicPointId PointId(
        ulong value)
    {
        return new EconomicPointId(
            value);
    }

    private static CommodityId Commodity(
        uint value)
    {
        return new CommodityId(
            value);
    }
}
