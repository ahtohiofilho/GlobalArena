using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01aTests
{
    private static CanonicalPhysicalTacticalSession Cycle()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    [Fact]
    public void UncommittedPhysicalOutputIsCandidateNotImplicitRetentionOrSale()
    {
        var game = Cycle();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var snapshot = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        var origin = game.InitialTile.FineStrategicCellId.Value;

        foreach (var commodity in new uint[] { 1U, 2U, 3U })
        {
            var row = snapshot.ForTileCommodity(origin, commodity);
            var stock = game.StockAt(game.InitialTile, new CommodityId(commodity));
            Assert.Equal(new BigInteger(stock), row.UncommittedLocalRaw);
            Assert.Equal(BigInteger.Zero, row.AlreadyCommittedOfferRaw);
            Assert.Equal(BigInteger.Zero, row.DeliberatelyRetainedRaw);
            Assert.Equal(new BigInteger(stock), row.EligibleByDefaultRaw);
        }
        Assert.Equal(snapshot.EligibleByDefaultRaw, snapshot.TotalAccountedRaw);
        Assert.Equal(BigInteger.Zero, snapshot.DeliberatelyRetainedRaw);
        Assert.False(snapshot.CertifiesActualAutomaticOffers);
        Assert.False(snapshot.CertifiesExecutedSales);
        Assert.False(snapshot.AuthorizesSettlementOrRevenue);
        Assert.False(snapshot.ModifiesCanonicalState);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(before, game.ExportSaveJson());
        snapshot.VerifyCurrent();
    }

    [Fact]
    public void ExplicitOfferIsNotCountedTwiceAndRetentionStaysOut()
    {
        var game = Cycle();
        var tile = game.InitialTile;
        var id = tile.FineStrategicCellId.Value;
        var wood = game.StockAt(tile, new CommodityId(3U));
        var stone = game.StockAt(tile, new CommodityId(2U));
        Assert.True(wood > 0UL);
        Assert.True(stone > 0UL);

        var baseline = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        var initialTotal = baseline.TotalAccountedRaw;
        var reservedWood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, wood);
        game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, stone);
        Assert.Throws<InvalidOperationException>(baseline.VerifyCurrent);

        var snapshot = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        var w = snapshot.ForTileCommodity(id, 3U);
        var s = snapshot.ForTileCommodity(id, 2U);
        Assert.Equal(BigInteger.Zero, w.UncommittedLocalRaw);
        Assert.Equal(new BigInteger(wood), w.AlreadyCommittedOfferRaw);
        Assert.Equal(new BigInteger(wood), w.EligibleByDefaultRaw);
        Assert.Equal(BigInteger.Zero, s.EligibleByDefaultRaw);
        Assert.Equal(new BigInteger(stone), s.DeliberatelyRetainedRaw);
        Assert.Equal(initialTotal, snapshot.TotalAccountedRaw);
        Assert.False(snapshot.CertifiesExecutedSales);
        Assert.False(snapshot.AuthorizesSettlementOrRevenue);

        var save = game.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var replay = CanonicalDefaultMarketPoolPreflight.FromCurrent(restored);
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(snapshot.TotalAccountedRaw, replay.TotalAccountedRaw);
        Assert.Equal(snapshot.EligibleByDefaultRaw, replay.EligibleByDefaultRaw);
        Assert.Equal(snapshot.DeliberatelyRetainedRaw, replay.DeliberatelyRetainedRaw);
        Assert.Equal(snapshot.Rows.ToArray(), replay.Rows.ToArray());
        Assert.Contains(replay.Rows, x => x.FineTile == id &&
            x.Commodity == 3U &&
            x.AlreadyCommittedOfferRaw == new BigInteger(wood));
        Assert.Contains(restored.PhysicalStockCommitments, x =>
            x.ActionOrdinal == reservedWood.ActionOrdinal);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            replay.ForTileCommodity(id, 999U));
    }

    [Fact]
    public void PartialRetentionAndReleaseReclassifyExactSamePhysicalGoods()
    {
        var game = Cycle();
        var origin = game.InitialTile;
        var stone = game.StockAt(origin, new CommodityId(2U));
        var retained = stone / 2UL;
        Assert.True(retained > 0UL);
        var hold = game.CommitPhysicalStock(origin, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, retained);
        var snapshot = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        var row = snapshot.ForTileCommodity(origin.FineStrategicCellId.Value, 2U);
        Assert.Equal(new BigInteger(stone - retained), row.EligibleByDefaultRaw);
        Assert.Equal(new BigInteger(retained), row.DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(stone), row.TotalAccountedRaw);

        game.ReleasePhysicalStockCommitment(hold.ActionOrdinal);
        Assert.Throws<InvalidOperationException>(snapshot.VerifyCurrent);
        var released = CanonicalDefaultMarketPoolPreflight.FromCurrent(game)
            .ForTileCommodity(origin.FineStrategicCellId.Value, 2U);
        Assert.Equal(new BigInteger(stone), released.EligibleByDefaultRaw);
        Assert.Equal(BigInteger.Zero, released.DeliberatelyRetainedRaw);
    }

    [Fact]
    public void TurnZeroAndMultipleCyclesDoNotSynthesizeSales()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        var start = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        Assert.Equal(BigInteger.Zero, start.TotalAccountedRaw);
        Assert.False(start.CertifiesExecutedSales);
        for (var i = 0; i < 12; i++) game.AdvanceMovementTurn();
        var later = CanonicalDefaultMarketPoolPreflight.FromCurrent(game);
        Assert.True(later.TotalAccountedRaw > BigInteger.Zero);
        Assert.True(later.Rows.All(x => x.FineTile > 0UL));
        Assert.False(later.CertifiesExecutedSales);
        Assert.False(later.AuthorizesSettlementOrRevenue);
        Assert.Throws<InvalidOperationException>(start.VerifyCurrent);
    }
}
