using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01D: a staged automatic offer batch must generate
/// REAL, journaled physical custody and never fabricate an executed sale.
/// </summary>
public sealed class GaEconMarketDefault01dTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        Assert.Single(game.State.Cycles);
        return game;
    }

    [Fact]
    public void AllFreeLocalGoodsAreCommittedAutomaticallyWithoutTouchingOriginal()
    {
        var original = Closed();
        var originalSave = original.ExportSaveJson();
        var originalHash = original.CurrentHash;
        var before = CanonicalDefaultMarketPoolPreflight.FromCurrent(original);
        Assert.True(before.EligibleByDefaultRaw > 0);
        var expectedRows = before.Rows.Count(x => x.UncommittedLocalRaw > 0);
        var result = CanonicalStagedAutomaticMarketOfferBatch.Prepare(original);
        var staged = result.PreparedSession;

        Assert.Equal(originalHash.HexDigest, result.OriginalHashHex);
        Assert.Equal(originalHash, original.CurrentHash);
        Assert.Equal(originalSave, original.ExportSaveJson());
        Assert.Equal(before.EligibleByDefaultRaw, result.NewlyOfferedRaw);
        Assert.Equal(before.EligibleByDefaultRaw, result.TotalEligibleRaw);
        Assert.Equal(before.DeliberatelyRetainedRaw, result.ExplicitRetentionRaw);
        Assert.Equal(expectedRows, result.NewOfferCommitments.Count);
        Assert.True(result.AuthenticatesMarketOfferCustody);
        Assert.False(result.ExecutesCommercialSales);
        Assert.False(result.TransfersOwnershipOrMoney);
        Assert.False(result.ActivatesNewGameplayRuleset);
        Assert.All(result.NewOfferCommitments, x =>
            Assert.Equal(CanonicalPhysicalCommitmentKind.MarketOffer, x.Kind));
        Assert.All(result.NewOfferCommitments, x => Assert.True(x.IsLive));

        var after = CanonicalDefaultMarketPoolPreflight.FromCurrent(staged);
        Assert.Equal(before.TotalAccountedRaw, after.TotalAccountedRaw);
        Assert.Equal(before.EligibleByDefaultRaw, after.EligibleByDefaultRaw);
        Assert.Equal(before.DeliberatelyRetainedRaw, after.DeliberatelyRetainedRaw);
        Assert.All(after.Rows, x =>
            Assert.Equal(BigInteger.Zero, x.UncommittedLocalRaw));
        Assert.Equal(before.EligibleByDefaultRaw,
            staged.CapturePhysicalStockCommitmentWitness().LiveMarketOffersRaw);
        Assert.Equal(original.State.Cycles.ToArray(), staged.State.Cycles.ToArray());
        Assert.Equal(original.Events.Count + expectedRows, staged.Events.Count);
        Assert.All(staged.Events.Skip(original.Events.Count), x =>
            Assert.Equal("commit_physical_stock", x.Kind));

        var saved = staged.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(saved, reopened.ExportSaveJson());
        Assert.Equal(staged.CurrentHash, reopened.CurrentHash);
        Assert.Equal(staged.PhysicalStockCommitments.ToArray(),
            reopened.PhysicalStockCommitments.ToArray());
    }

    [Fact]
    public void ExistingOfferAndDeliberateRetentionStayInSeparateCustody()
    {
        var original = Closed();
        var tile = original.InitialTile;
        var wood = original.StockAt(tile, new CommodityId(3U));
        Assert.True(wood > 20UL);
        var heldRaw = wood / 4UL;
        var offeredRaw = wood / 4UL;
        var hold = original.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, heldRaw);
        var priorOffer = original.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, offeredRaw);
        var before = CanonicalDefaultMarketPoolPreflight.FromCurrent(original);
        var sourceSave = original.ExportSaveJson();
        var result = CanonicalStagedAutomaticMarketOfferBatch.Prepare(original);
        var staged = result.PreparedSession;
        var after = CanonicalDefaultMarketPoolPreflight.FromCurrent(staged);
        var woodAfter = after.ForTileCommodity(
            tile.FineStrategicCellId.Value, 3U);

        Assert.Equal(sourceSave, original.ExportSaveJson());
        Assert.Equal(new BigInteger(heldRaw), woodAfter.DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(wood - heldRaw), woodAfter.EligibleByDefaultRaw);
        Assert.Equal(new BigInteger(wood - heldRaw), woodAfter.AlreadyCommittedOfferRaw);
        Assert.Equal(BigInteger.Zero, woodAfter.UncommittedLocalRaw);
        Assert.Equal(before.TotalAccountedRaw, after.TotalAccountedRaw);
        Assert.Equal(before.EligibleByDefaultRaw, after.EligibleByDefaultRaw);
        Assert.Equal(before.DeliberatelyRetainedRaw, after.DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(heldRaw), result.ExplicitRetentionRaw);
        Assert.Equal(1, staged.PhysicalStockCommitments.Count(x =>
            x.ActionOrdinal == priorOffer.ActionOrdinal));
        Assert.Equal(1, staged.PhysicalStockCommitments.Count(x =>
            x.ActionOrdinal == hold.ActionOrdinal));
        Assert.False(result.ExecutesCommercialSales);
    }

    [Fact]
    public void SecondBatchIsIdempotentUntilNewProductionOrRelease()
    {
        var original = Closed();
        var first = CanonicalStagedAutomaticMarketOfferBatch.Prepare(original);
        var staged = first.PreparedSession;
        var frozenHash = staged.CurrentHash;
        var frozenSave = staged.ExportSaveJson();

        var second = CanonicalStagedAutomaticMarketOfferBatch.Prepare(staged);
        Assert.Empty(second.NewOfferCommitments);
        Assert.Equal(BigInteger.Zero, second.NewlyOfferedRaw);
        Assert.Equal(frozenHash, second.PreparedSession.CurrentHash);
        Assert.Equal(frozenSave, second.PreparedSession.ExportSaveJson());
        Assert.False(second.ExecutesCommercialSales);
        Assert.Equal(frozenHash, staged.CurrentHash);
        Assert.Equal(frozenSave, staged.ExportSaveJson());
    }

    [Fact]
    public void RejectsUnclosedCycleWithoutAnySourceMutation()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        var before = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalStagedAutomaticMarketOfferBatch.Prepare(game));
        Assert.Equal(before, game.ExportSaveJson());

        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        game.AdvanceMovementTurn();
        var during = game.ExportSaveJson();
        var hash = game.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalStagedAutomaticMarketOfferBatch.Prepare(game));
        Assert.Equal(during, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
    }

    [Fact]
    public void ReleasingRetentionRestoresEligibilityOnlyInNextStagedBatch()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var stone = game.StockAt(tile, new CommodityId(2U));
        Assert.True(stone > 0UL);
        var retained = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, stone);
        var first = CanonicalStagedAutomaticMarketOfferBatch.Prepare(game);
        var beforeRelease = CanonicalDefaultMarketPoolPreflight.FromCurrent(
            first.PreparedSession).ForTileCommodity(
                tile.FineStrategicCellId.Value, 2U);
        Assert.Equal(BigInteger.Zero, beforeRelease.EligibleByDefaultRaw);
        Assert.Equal(new BigInteger(stone), beforeRelease.DeliberatelyRetainedRaw);

        first.PreparedSession.ReleasePhysicalStockCommitment(retained.ActionOrdinal);
        var second = CanonicalStagedAutomaticMarketOfferBatch.Prepare(
            first.PreparedSession);
        var afterRelease = CanonicalDefaultMarketPoolPreflight.FromCurrent(
            second.PreparedSession).ForTileCommodity(
                tile.FineStrategicCellId.Value, 2U);
        Assert.Equal(BigInteger.Zero, afterRelease.DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(stone), afterRelease.AlreadyCommittedOfferRaw);
        Assert.Equal(BigInteger.Zero, afterRelease.UncommittedLocalRaw);
        Assert.False(second.ExecutesCommercialSales);
    }
}
