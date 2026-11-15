using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Real, canonical Goldberg world and physical worker economic execution.
/// No synthetic market hierarchy, no F072/finances claims, no WorldState write.
/// </summary>
public sealed class GoldbergFoundationSessionTests
{
    private static (CanonicalGameSession Canonical, GoldbergFoundationSession Economy,
        int Start, int Neighbor, int Remote) Create(int passes = 5)
    {
        var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        var graph = canonical.GeneratedWorld.StrategicSurfaceGraph;
        var start = checked((int)graph.GetCellId(0).Value);
        var neighbor = checked((int)graph.GetCellId(
            graph.GetNeighborIndexes(0)[0]).Value);
        var remote = checked((int)graph.GetCellId(graph.NodeCount / 2).Value);
        return (canonical, new GoldbergFoundationSession(canonical,
            new[]
            {
                new FoundationWorker(1, 1, start, 0, 1d,
                    FoundationWorkerActivity.Producing),
                new FoundationWorker(2, 2, remote, 1, 2d,
                    FoundationWorkerActivity.Producing),
                new FoundationWorker(3, 1, neighbor, 0, 0d,
                    FoundationWorkerActivity.Military),
                new FoundationWorker(4, 1, neighbor, 0, 1d,
                    FoundationWorkerActivity.Producing),
                new FoundationWorker(5, 2, start, 1, 1d,
                    FoundationWorkerActivity.Traveling)
            },
            new Dictionary<int, double> { [0] = 1d, [1] = .5d },
            maximumAdjustmentPasses: passes),
            start, neighbor, remote);
    }

    [Fact]
    public void UsesTheActualCanonicalGoldbergSphereAndOneBasedIdentifiers()
    {
        var (canonical, economy, start, neighbor, remote) = Create();
        Assert.Equal(92, economy.WorldNodeCount);
        Assert.Equal(270, economy.WorldEdgeCount);
        Assert.Equal(canonical.Snapshot.WorldBinding, economy.WorldBinding);
        Assert.Equal(canonical.GeneratedWorld.Request.StrategicParameters.StrategicCellCount,
            checked((ulong)economy.WorldNodeCount));
        Assert.Contains(canonical.GeneratedWorld.StrategicTopology.Edges, edge =>
            edge.IncidentCellIds[0].Value == checked((ulong)start) &&
            edge.IncidentCellIds[1].Value == checked((ulong)neighbor) ||
            edge.IncidentCellIds[1].Value == checked((ulong)start) &&
            edge.IncidentCellIds[0].Value == checked((ulong)neighbor));
        Assert.InRange(remote, 1, economy.WorldNodeCount);
        Assert.True(economy.Workers.All(w => w.Tile >= 1 &&
            w.Tile <= economy.WorldNodeCount));
    }

    [Fact]
    public void FirstTickTradesAllRealOutputWhileMilitaryAndTravelersStillDemand()
    {
        var (canonical, economy, start, neighbor, remote) = Create();
        var authoritative = canonical.Snapshot;
        var result = economy.Advance();

        Assert.Equal(1, result.Turn);
        Assert.Equal(4d, result.TotalProduction);
        Assert.Equal(4d, result.TotalDelivered, 9);
        Assert.Equal(3, result.Demand.Keys.Select(key => key.Tile).Distinct().Count());
        Assert.Equal(5d, result.Demand
            .Where(x => x.Key.Commodity == 0).Sum(x => x.Value), 9);
        Assert.Equal(2.5d, result.Demand
            .Where(x => x.Key.Commodity == 1).Sum(x => x.Value), 9);
        Assert.Equal(2d, result.Demand[(start, 0)]);
        Assert.Equal(2d, result.Demand[(neighbor, 0)]);
        Assert.Equal(1d, result.Demand[(remote, 0)]);
        Assert.False(result.CertifiedGlobalEquilibrium);
        Assert.False(result.FinancialSettlementImplemented);

        Assert.Same(authoritative, canonical.Snapshot);
        Assert.Equal(0UL, canonical.Snapshot.Revision);
        Assert.Empty(canonical.Snapshot.Economy.EconomicPoints);
    }

    [Fact]
    public void RealStrategicAdjacentMovementChangesDemandExactlyOnce()
    {
        var (_, economy, start, neighbor, _) = Create();
        economy.Advance();
        var moved = economy.Advance(new[]
        {
            new FoundationWorkerOrder(5, NextTile: neighbor)
        });
        Assert.Equal(2, moved.Turn);
        Assert.Equal(4d, moved.TotalProduction);
        Assert.Equal(4d, moved.TotalDelivered, 9);
        Assert.Equal(1d, moved.Demand[(start, 0)]);
        Assert.Equal(3d, moved.Demand[(neighbor, 0)]);
        Assert.Equal(5d, moved.Demand
            .Where(x => x.Key.Commodity == 0).Sum(x => x.Value), 9);
        Assert.Equal(FoundationWorkerActivity.Traveling,
            moved.Workers.Single(w => w.Id == 5).Activity);
        var inspected = economy.InspectTile(new StrategicCellId(checked((ulong)neighbor)));
        Assert.Equal(2, inspected.EconomicTurn);
        Assert.Equal(3, inspected.Workers.Count);
        Assert.Equal(3d, inspected.Demand[0]);
    }

    [Fact]
    public void PhysicalWorkerCanStartProducingAfterTravelOnTheGoldbergSurface()
    {
        var (_, economy, _, neighbor, _) = Create();
        economy.Advance();
        economy.Advance(new[] { new FoundationWorkerOrder(5, NextTile: neighbor) });
        var production = economy.Advance(new[]
        {
            new FoundationWorkerOrder(5, NextActivity: FoundationWorkerActivity.Producing)
        });
        Assert.Equal(3, production.Turn);
        Assert.Equal(5d, production.TotalProduction);
        Assert.Equal(5d, production.TotalDelivered, 9);
        Assert.Equal(3d, production.Demand[(neighbor, 0)]);
        Assert.Equal(1.5d, production.Demand[(neighbor, 1)]);
        Assert.Equal(5d, production.Demand
            .Where(x => x.Key.Commodity == 0).Sum(x => x.Value), 9);
    }

    [Fact]
    public void EveryCargoEdgeIsAnOriginalPhysicalGoldbergEdge()
    {
        var (canonical, economy, _, _, _) = Create();
        var originalEdges = canonical.GeneratedWorld.StrategicTopology.Edges
            .ToDictionary(x => checked((int)x.Id.Value));
        var tick = economy.Advance();
        Assert.NotEmpty(tick.Trades);
        foreach (var trade in tick.Trades)
        {
            var at = trade.Origin;
            var visited = new HashSet<int> { at };
            foreach (var edgeId in trade.EdgeIds)
            {
                var edge = originalEdges[edgeId];
                var a = checked((int)edge.IncidentCellIds[0].Value);
                var b = checked((int)edge.IncidentCellIds[1].Value);
                Assert.True(at == a || at == b);
                at = at == a ? b : a;
                Assert.True(visited.Add(at));
            }
            Assert.Equal(trade.Destination, at);
        }
        foreach (var (edgeId, load) in tick.EdgeLoads)
            Assert.Equal(tick.Trades
                .Where(t => t.EdgeIds.Contains(edgeId))
                .Sum(t => t.Quantity), load, 10);
    }

    [Fact]
    public void ReadOnlyInspectorOnEmptyOrOccupiedCanonicalTileNeverMakesMarket()
    {
        var (_, economy, start, _, _) = Create();
        var unused = new StrategicCellId(92UL);
        var before = economy.InspectTile(new StrategicCellId(checked((ulong)start)));
        Assert.Equal(0, before.EconomicTurn);
        Assert.Empty(before.Prices);
        Assert.Empty(before.Demand);
        Assert.Equal(2, before.Workers.Count);

        var tick = economy.Advance();
        var occupied = economy.InspectTile(new StrategicCellId(checked((ulong)start)));
        Assert.Equal(tick.Turn, occupied.EconomicTurn);
        Assert.Equal(2d, occupied.Demand[0]);
        Assert.True(occupied.Prices.All(x => x.Value > 0d));
        var empty = economy.InspectTile(unused);
        Assert.Empty(empty.Workers);
        Assert.Empty(empty.Prices);
        Assert.Empty(empty.Demand);
        Assert.Equal(tick.Turn, economy.EconomicTurn);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            economy.InspectTile(new StrategicCellId(93UL)));
    }

    [Fact]
    public void InvalidWorldLocationAndNonadjacentMovementFailBeforePublication()
    {
        var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        Assert.Throws<ArgumentException>(() => new GoldbergFoundationSession(
            canonical,
            new[] { new FoundationWorker(1, 0, 93, 0, 1d,
                FoundationWorkerActivity.Producing) },
            new Dictionary<int, double> { [0] = 1d }));

        var (_, economy, start, _, remote) = Create();
        Assert.Throws<ArgumentException>(() => economy.Advance(new[]
        {
            new FoundationWorkerOrder(1, NextTile: remote)
        }));
        Assert.Equal(0, economy.EconomicTurn);
        Assert.Equal(start, economy.Workers.Single(w => w.Id == 1).Tile);
        Assert.Null(economy.LastEconomicTurn);
    }

    [Fact]
    public void RefusesToPresentStalePreviewAfterAuthoritativeSnapshotReplacement()
    {
        var (canonical, economy, start, _, _) = Create();
        economy.Advance();
        canonical.AcceptSnapshot(canonical.Snapshot.AdvanceRevision());
        Assert.Throws<InvalidOperationException>(() => economy.Advance());
        Assert.Throws<InvalidOperationException>(() =>
            economy.InspectTile(new StrategicCellId(checked((ulong)start))));
        Assert.Throws<InvalidOperationException>(() =>
            economy.CalibrateInitialMarket());
    }

    [Fact]
    public void TwoIdenticallyGeneratedWorldsReplayPhysicalAndEconomicDecisions()
    {
        var (_, one, _, oneNeighbor, _) = Create();
        var (_, two, _, twoNeighbor, _) = Create();
        Assert.Equal(oneNeighbor, twoNeighbor);
        Assert.Equal(one.WorldBinding, two.WorldBinding);

        for (var turn = 1; turn <= 3; turn++)
        {
            FoundationWorkerOrder[] orders = turn switch
            {
                2 => new[] { new FoundationWorkerOrder(5, NextTile: oneNeighbor) },
                3 => new[] { new FoundationWorkerOrder(
                    5, NextActivity: FoundationWorkerActivity.Producing) },
                _ => Array.Empty<FoundationWorkerOrder>()
            };
            var first = one.Advance(orders);
            var second = two.Advance(orders);
            Assert.Equal(first.Turn, second.Turn);
            Assert.Equal(first.TotalProduction, second.TotalProduction);
            Assert.Equal(first.TotalDelivered, second.TotalDelivered);
            Assert.Equal(first.MaxRelativeOpportunity, second.MaxRelativeOpportunity);
            Assert.Equal(first.Prices.OrderBy(x => x.Key.Tile)
                .ThenBy(x => x.Key.Commodity).ToArray(),
                second.Prices.OrderBy(x => x.Key.Tile)
                    .ThenBy(x => x.Key.Commodity).ToArray());
            Assert.Equal(first.Trades.Select(x =>
                (x.Origin, x.Destination, x.Commodity, x.Seller,
                    x.Quantity, Route: string.Join(",", x.EdgeIds))).ToArray(),
                second.Trades.Select(x =>
                (x.Origin, x.Destination, x.Commodity, x.Seller,
                    x.Quantity, Route: string.Join(",", x.EdgeIds))).ToArray());
        }
    }

    [Fact]
    public void OptionalWarmupRetainsClockAndOriginalCanonicalSnapshot()
    {
        var (canonical, economy, _, _, _) = Create();
        var original = canonical.Snapshot;
        var first = economy.CalibrateInitialMarket();
        Assert.Equal(0, economy.EconomicTurn);
        Assert.Null(economy.LastEconomicTurn);
        Assert.True(economy.InitialMarketCalibrated);
        Assert.Equal(4d, first.TotalProduction);
        Assert.Equal(4d, first.TotalDelivered, 9);
        Assert.Same(original, canonical.Snapshot);
        Assert.Throws<InvalidOperationException>(() =>
            economy.CalibrateInitialMarket());
        var actual = economy.Advance();
        Assert.Equal(1, actual.Turn);
        Assert.Equal(4d, actual.TotalProduction);
        Assert.Equal(4d, actual.TotalDelivered, 9);
        Assert.Same(original, canonical.Snapshot);
    }
}
