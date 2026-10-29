using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS01: test a visible, deterministic Neolithic interaction loop.
/// This is an interactive LOCAL probe, not a committed economic turn.
/// </summary>
public sealed class GaVs01NeolithicInteractionTests
{
    [Fact]
    public void Vs01WorldStartsWithOneSettlementAndEightResidentWorkers()
    {
        var session = new EconomicNeolithicInteractionSession();
        Assert.Single(session.CurrentWorld.Economy.EconomicPoints);
        var point = Assert.Single(session.CurrentWorld.Economy.EconomicPoints);
        Assert.Equal(8UL, point.Workforce);
        Assert.Equal(8UL,
            Assert.Single(session.CurrentWorld.Population.PointPopulations).Residents);
        Assert.Equal(3, point.ActivityAllocations.Count);
        Assert.Equal(1, session.CurrentWorld.Territory.Controls.Count);
        Assert.NotEmpty(session.AvailableLandNeighbors(point.Id));
        Assert.Equal(0UL, session.CompletedPreviewTurns);
    }

    [Fact]
    public void Vs01BasicFoundingActuallyChangesTerritoryAndConservesAllWorkers()
    {
        var session = new EconomicNeolithicInteractionSession();
        var original = session.CurrentWorld;
        var origin = original.Economy.EconomicPoints.Single().Id;
        var before = session.CurrentHash;
        var tile = session.AvailableLandNeighbors(origin)[0];
        var founded = session.Found(origin, tile, 3UL);
        var next = session.CurrentWorld;

        Assert.Equal(2UL, founded.Value);
        Assert.Equal(1UL, next.Revision);
        Assert.Equal(2, next.Economy.EconomicPoints.Count);
        Assert.True(next.Territory.TryGetController(tile, out var controller));
        Assert.Equal(new CivilizationId(1UL), controller);
        Assert.Equal(5UL, Assert.Single(next.Economy.EconomicPoints,
            p => p.Id == origin).Workforce);
        Assert.Equal(3UL, Assert.Single(next.Economy.EconomicPoints,
            p => p.Id == founded).Workforce);
        Assert.Equal(8UL, next.Economy.EconomicPoints.Aggregate(
            0UL, (total, p) => total + p.Workforce));
        Assert.Equal(8UL, next.Population.PointPopulations.Aggregate(
            0UL, (total, p) => total + p.Residents));
        Assert.NotEqual(before, session.CurrentHash);
        Assert.NotSame(original, next);
        Assert.False(original.Territory.TryGetController(tile, out _));
    }

    [Fact]
    public void Vs01SupplyDemandProjectionReflectsWorkersAfterFounding()
    {
        var session = new EconomicNeolithicInteractionSession();
        var origin = session.CurrentWorld.Economy.EconomicPoints[0].Id;
        var initialHash = session.CurrentHash;
        var initial = session.PreviewGeographicEconomy();

        Assert.Equal(4_000_000UL,
            Assert.Single(initial.DemandFlows).RawQuantity);
        Assert.Equal(initialHash, session.CurrentHash);

        var tile = session.AvailableLandNeighbors(origin)[0];
        session.Found(origin, tile, 3UL);
        var beforePreview = session.CurrentHash;
        var next = session.AdvancePreview();
        Assert.Equal(1UL, session.CompletedPreviewTurns);
        Assert.Equal(2, next.DemandFlows.Count);
        Assert.Equal(4_000_000UL, next.DemandFlows.Aggregate(
            0UL, (sum, d) => checked(sum + d.RawQuantity)));
        Assert.All(next.SupplyFlows, row => Assert.True(row.RawQuantity > 0UL));
        Assert.Equal(beforePreview, session.CurrentHash);
        Assert.Equal(1UL, session.CurrentWorld.Revision);
        session.AdvancePreview();
        Assert.Equal(2UL, session.CompletedPreviewTurns);
        Assert.Equal(beforePreview, session.CurrentHash);
    }

    [Fact]
    public void Vs01SameSeedAndSameOrdersProduceIdenticalGeographicFlowsAndHash()
    {
        var first = new EconomicNeolithicInteractionSession(0UL);
        var second = new EconomicNeolithicInteractionSession(0UL);

        var id = new EconomicPointId(1UL);
        var firstTile = first.AvailableLandNeighbors(id)[0];
        var secondTile = second.AvailableLandNeighbors(id)[0];
        Assert.Equal(firstTile, secondTile);

        first.Found(id, firstTile, 3UL);
        second.Found(id, secondTile, 3UL);
        Assert.Equal(first.CurrentHash, second.CurrentHash);

        var firstFlows = first.PreviewGeographicEconomy();
        var secondFlows = second.PreviewGeographicEconomy();
        Assert.Equal(firstFlows.SupplyFlows.Select(x =>
            (x.EconomicPointId, x.CommodityId, x.RawQuantity)),
            secondFlows.SupplyFlows.Select(x =>
                (x.EconomicPointId, x.CommodityId, x.RawQuantity)));
        Assert.Equal(firstFlows.DemandFlows.Select(x =>
            (x.EconomicPointId, x.CommodityId, x.RawQuantity)),
            secondFlows.DemandFlows.Select(x =>
                (x.EconomicPointId, x.CommodityId, x.RawQuantity)));
    }

    [Fact]
    public void Vs01CannotMoveMoreWorkersOrFoundOccupiedOrWaterCell()
    {
        var session = new EconomicNeolithicInteractionSession();
        var id = new EconomicPointId(1UL);
        var tile = session.AvailableLandNeighbors(id)[0];
        var before = session.CurrentHash;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.Found(id, tile, 8UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.Found(id, tile, 0UL));
        Assert.Equal(before, session.CurrentHash);

        session.Found(id, tile, 2UL);
        var after = session.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            session.Found(id, tile, 1UL));
        Assert.Equal(after, session.CurrentHash);
    }
}
