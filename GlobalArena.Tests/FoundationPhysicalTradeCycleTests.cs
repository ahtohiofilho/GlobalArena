using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Tests;

/// <summary>
/// Vertical-cycle contracts, not a substitute for independent ADR042/F072
/// certification or gameplay/Godot integration. All tests exercise real C#
/// simulation types, no fake clock, no hidden economic network tiers.
/// </summary>
public sealed class FoundationPhysicalTradeCycleTests
{
    private static PhysicalWorkerTradeCycle World(int passes = 4) => new(
        new[]
        {
            new FoundationEdge(1, 0, 1, 0.5d, 2d, OwnerCivilization: 1,
                ExternalTariff: 0.02d),
            new FoundationEdge(2, 1, 2, 0.5d, 2d, OwnerCivilization: 2,
                ExternalTariff: 0.02d)
        },
        new[]
        {
            new FoundationWorker(1, 1, 0, 0, 2d, FoundationWorkerActivity.Producing),
            new FoundationWorker(2, 2, 2, 1, 3d, FoundationWorkerActivity.Producing),
            new FoundationWorker(3, 1, 1, 0, 1d, FoundationWorkerActivity.Military),
            new FoundationWorker(4, 2, 2, 0, 0d, FoundationWorkerActivity.Other),
        },
        new Dictionary<int, double> { [0] = 1d, [1] = 0.5d },
        maximumAdjustmentPasses: passes,
        relativeOpportunityTolerance: 1e-4d,
        maximumStep: 0.25d,
        responseScale: 0.1d);

    [Fact]
    public void EveryWorkerDemandsAllCommoditiesAtExactlyOnePhysicalTile()
    {
        var turn = World().Advance();
        Assert.Equal(4, turn.Workers.Count);
        Assert.Equal(6, turn.Demand.Count); // 3 occupied tiles x 2 commodities
        Assert.Equal(1d, turn.Demand[(0, 0)]);
        Assert.Equal(0.5d, turn.Demand[(0, 1)]);
        Assert.Equal(1d, turn.Demand[(1, 0)]); // military still consumes
        Assert.Equal(0.5d, turn.Demand[(1, 1)]);
        Assert.Equal(2d, turn.Demand[(2, 0)]);
        Assert.Equal(1d, turn.Demand[(2, 1)]);
    }

    [Fact]
    public void AllPhysicalOutputSoldAndNoArtificialSupplies()
    {
        var turn = World().Advance();
        Assert.Equal(5d, turn.TotalProduction);
        Assert.Equal(5d, turn.TotalDelivered, 9);
        Assert.All(turn.Trades, trade => Assert.True(trade.Quantity >= 0d));
        Assert.Equal(2d, turn.Trades.Where(x => x.Commodity == 0).Sum(x => x.Quantity), 9);
        Assert.Equal(3d, turn.Trades.Where(x => x.Commodity == 1).Sum(x => x.Quantity), 9);
        Assert.False(turn.CertifiedGlobalEquilibrium);
        Assert.False(turn.FinancialSettlementImplemented);
    }

    [Fact]
    public void EachPositivePriceComesOnlyFromOriginalSquareRootLaw()
    {
        var turn = World().Advance();
        Assert.NotEmpty(turn.Prices);
        foreach (var (market, price) in turn.Prices)
        {
            Assert.True(turn.Delivered.TryGetValue(market, out var q) && q > 0d);
            Assert.Equal(Math.Sqrt(turn.Demand[market] / q), price, 12);
        }
    }

    [Fact]
    public void MilitaryMovementShiftsDemandWithoutDuplicatingOrCreatingSupply()
    {
        var world = World();
        var first = world.Advance();
        var moved = world.Advance(new[] { new FoundationWorkerOrder(3, NextTile: 2) });
        Assert.Equal(5d, moved.TotalProduction);
        Assert.Equal(5d, moved.TotalDelivered, 9);
        Assert.Equal(4d, first.Demand.Values.Sum());
        Assert.Equal(first.Demand.Values.Sum(), moved.Demand.Values.Sum(), 9);
        Assert.False(moved.Demand.ContainsKey((1, 0)));
        Assert.Equal(3d, moved.Demand[(2, 0)]);
        Assert.Equal(1.5d, moved.Demand[(2, 1)]);
        Assert.Equal(FoundationWorkerActivity.Traveling,
            moved.Workers.Single(w => w.Id == 3).Activity);
    }

    [Fact]
    public void MovementFromProducingWorkerStopsSupplyButNotDemand()
    {
        var world = World();
        world.Advance();
        var moved = world.Advance(new[] { new FoundationWorkerOrder(1, NextTile: 1) });
        Assert.Equal(3d, moved.TotalProduction);
        Assert.Equal(3d, moved.TotalDelivered, 9);
        Assert.Equal(4d, moved.Demand.Values.Sum(), 9);
        Assert.DoesNotContain(moved.Trades, t => t.Commodity == 0);
        Assert.DoesNotContain(moved.Prices.Keys, key => key.Commodity == 0);
    }

    [Fact]
    public void WorkerMayReturnToProductionAfterTravelWithoutNewDemand()
    {
        var world = World();
        world.Advance();
        world.Advance(new[] { new FoundationWorkerOrder(1, NextTile: 1) });
        var resumed = world.Advance(new[]
        {
            new FoundationWorkerOrder(1, NextActivity: FoundationWorkerActivity.Producing)
        });
        Assert.Equal(5d, resumed.TotalProduction);
        Assert.Equal(5d, resumed.TotalDelivered, 9);
        Assert.Equal(4d, resumed.Demand.Values.Sum(), 9);
        Assert.Equal(1, resumed.Workers.Single(w => w.Id == 1).Tile);
    }

    [Fact]
    public void EveryEdgeLoadEqualsExactlyTheSumOfActualTradesUsingThatEdge()
    {
        var turn = World().Advance();
        foreach (var (edgeId, load) in turn.EdgeLoads)
            Assert.Equal(turn.Trades
                .Where(trade => trade.EdgeIds.Contains(edgeId))
                .Sum(trade => trade.Quantity), load, 10);
    }

    [Fact]
    public void AllRoutesArePhysicallyAdjacentAndHaveCorrectEndpoint()
    {
        var turn = World().Advance();
        var edges = new Dictionary<int, (int A, int B)>
        {
            [1] = (0, 1), [2] = (1, 2)
        };
        foreach (var trade in turn.Trades)
        {
            var node = trade.Origin;
            var visited = new HashSet<int> { node };
            foreach (var edgeId in trade.EdgeIds)
            {
                var (a, b) = edges[edgeId];
                Assert.True(node == a || node == b);
                node = node == a ? b : a;
                Assert.True(visited.Add(node));
            }
            Assert.Equal(trade.Destination, node);
        }
    }

    [Fact]
    public void IdenticalInitialConditionsAndOrdersYieldIdenticalEconomicTurns()
    {
        var a = World(8);
        var b = World(8);
        for (var turn = 1; turn <= 3; turn++)
        {
            var orders = turn switch
            {
                2 => new[] { new FoundationWorkerOrder(3, NextTile: 2) },
                3 => new[] {
                    new FoundationWorkerOrder(3, NextActivity: FoundationWorkerActivity.Other) },
                _ => Array.Empty<FoundationWorkerOrder>()
            };
            var left = a.Advance(orders);
            var right = b.Advance(orders);
            Assert.Equal(left.TotalProduction, right.TotalProduction);
            Assert.Equal(left.TotalDelivered, right.TotalDelivered);
            Assert.Equal(left.MaxRelativeOpportunity, right.MaxRelativeOpportunity);
            Assert.Equal(left.Demand.OrderBy(x => x.Key.Tile)
                .ThenBy(x => x.Key.Commodity).ToArray(),
                right.Demand.OrderBy(x => x.Key.Tile)
                .ThenBy(x => x.Key.Commodity).ToArray());
            Assert.Equal(left.Trades.Select(x =>
                (x.Origin, x.Destination, x.Commodity, x.Seller,
                    x.Quantity, Path: string.Join(",", x.EdgeIds))).ToArray(),
                right.Trades.Select(x =>
                (x.Origin, x.Destination, x.Commodity, x.Seller,
                    x.Quantity, Path: string.Join(",", x.EdgeIds))).ToArray());
        }
    }

    [Fact]
    public void BudgetLimitsAdjustmentsWithoutMisrepresentingF072Certification()
    {
        var turn = World(passes: 1).Advance();
        Assert.InRange(turn.AdjustmentPasses, 0, 1);
        Assert.True(double.IsFinite(turn.MaxRelativeOpportunity));
        Assert.InRange(turn.MaxRelativeOpportunity, 0d, 1d);
        Assert.False(turn.CertifiedGlobalEquilibrium);
    }

    [Fact]
    public void DisconnectedDemandDoesNotReceiveInventedPhysicalSupplyOrPrice()
    {
        var world = new PhysicalWorkerTradeCycle(
            new[] { new FoundationEdge(1, 0, 1, 1d, 1d) },
            new[]
            {
                new FoundationWorker(1, 0, 0, 0, 2d, FoundationWorkerActivity.Producing),
                new FoundationWorker(2, 0, 9, 0, 0d, FoundationWorkerActivity.Military)
            },
            new Dictionary<int, double> { [0] = 1d });
        var turn = world.Advance();
        Assert.Equal(2d, turn.TotalDelivered, 9);
        Assert.False(turn.Prices.ContainsKey((9, 0)));
        Assert.False(turn.Delivered.ContainsKey((9, 0)));
        Assert.Equal(1d, turn.Demand[(9, 0)]);
    }

    [Fact]
    public void UnproducedCommodityRemainsDemandedButUnpriced()
    {
        var world = new PhysicalWorkerTradeCycle(
            new[] { new FoundationEdge(1, 0, 1, 1d, 1d) },
            new[] { new FoundationWorker(1, 0, 0, 0, 1d,
                FoundationWorkerActivity.Producing) },
            new Dictionary<int, double> { [0] = 1d, [1] = .1d });
        var turn = world.Advance();
        Assert.Equal(.1d, turn.Demand[(0, 1)], 10);
        Assert.DoesNotContain(turn.Prices.Keys, key => key.Commodity == 1);
        Assert.DoesNotContain(turn.Trades, trade => trade.Commodity == 1);
    }

    [Fact]
    public void InvalidOrDuplicatedMoveBatchLeavesPreviousStateIntact()
    {
        var world = World();
        world.Advance();
        Assert.Throws<ArgumentException>(() => world.Advance(new[]
        {
            new FoundationWorkerOrder(1, NextTile: 2)
        }));
        Assert.Throws<ArgumentException>(() => world.Advance(new[]
        {
            new FoundationWorkerOrder(3, NextTile: 2),
            new FoundationWorkerOrder(3, NextActivity: FoundationWorkerActivity.Other)
        }));
        var second = world.Advance();
        Assert.Equal(2, second.Turn);
        Assert.Equal(0, second.Workers.Single(w => w.Id == 1).Tile);
        Assert.Equal(1, second.Workers.Single(w => w.Id == 3).Tile);
    }

    [Fact]
    public void InvalidContractDoesNotSilentlyCreateSpecialTilesOrCongestionCeilings()
    {
        Assert.Throws<ArgumentException>(() => new PhysicalWorkerTradeCycle(
            new[] { new FoundationEdge(1, 0, 1, 1d, 0d) },
            new[] { new FoundationWorker(1, 0, 0, 0, 1d,
                FoundationWorkerActivity.Producing) },
            new Dictionary<int, double> { [0] = 1d }));
        Assert.Throws<ArgumentException>(() => new PhysicalWorkerTradeCycle(
            new[] { new FoundationEdge(1, 0, 1, 1d, 1d,
                ExternalTariff: 0.01d) }, // no external beneficiary owner
            new[] { new FoundationWorker(1, 0, 0, 0, 1d,
                FoundationWorkerActivity.Producing) },
            new Dictionary<int, double> { [0] = 1d }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            World(passes: 0));
    }

    [Fact]
    public void NoProductionDoesNotCreatePhantomTradesOrPrices()
    {
        var world = new PhysicalWorkerTradeCycle(
            new[] { new FoundationEdge(1, 0, 1, .5d, 1d) },
            new[] { new FoundationWorker(1, 0, 0, 0, 1d,
                FoundationWorkerActivity.Military) },
            new Dictionary<int, double> { [0] = 1d });
        var tick = world.Advance();
        Assert.Equal(0d, tick.TotalProduction);
        Assert.Empty(tick.Trades);
        Assert.Empty(tick.Prices);
        Assert.Single(tick.Demand);
    }
}
