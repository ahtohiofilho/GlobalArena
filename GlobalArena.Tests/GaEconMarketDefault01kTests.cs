using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01kTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        return game;
    }

    [Fact]
    public void ActualRecipientMaterialUseConsumesCustodyOnceWithoutTouchingFreeStock()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var initialWood = game.StockAt(tile, new CommodityId(3U));
        var committed = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        var delivery = game.TransferMarketOfferToLocalRecipient(
            committed.ActionOrdinal, EconomicActivityKind.Forestry);
        var freeAfterDelivery = game.StockAt(tile, new CommodityId(3U));
        var receipt = game.UseLocalRecipientMaterial(
            committed.ActionOrdinal,
            CanonicalMaterialDispositionKind.IntermediateInputUse);

        Assert.Equal(initialWood - 4UL, freeAfterDelivery);
        Assert.Equal(freeAfterDelivery, game.StockAt(tile, new CommodityId(3U)));
        Assert.Equal(delivery.ActionOrdinal, receipt.OriginalTransferActionOrdinal);
        Assert.Equal(committed.ActionOrdinal, receipt.SourceCommitmentActionOrdinal);
        Assert.Equal(new BigInteger(4), new BigInteger(receipt.RawQuantity));
        Assert.Equal(EconomicActivityKind.Forestry, receipt.OriginalRecipientActivity);
        Assert.True(receipt.ActiveBeneficiaryWorkers > 0UL);
        Assert.True(receipt.ProvesExecutedMaterialUse);
        Assert.False(receipt.ProvesReturnOfSamePhysicalCustody);
        Assert.False(receipt.ProvesCommercialSale);
        Assert.False(receipt.ProvesLegalOwnershipTransfer);
        Assert.False(receipt.AuthorizesMonetaryRevenueOrTax);
        Assert.False(receipt.ExecutesRemoteFreight);
        Assert.False(receipt.ConsumesAggregateLegacyFood);
        Assert.All(receipt.SourceAttributions, x =>
        {
            Assert.Equal(CanonicalPhysicalLotCustodyKind.LocalRecipientCustody,
                x.Custody);
            Assert.Equal((ulong)committed.ActionOrdinal, x.CustodyId);
        });
        Assert.Equal(4UL, receipt.SourceAttributions.Aggregate(
            0UL, (sum, x) => checked(sum + x.RawQuantity)));
        Assert.True(game.PhysicalStockCommitments.Single().Transferred);
        Assert.False(game.PhysicalStockCommitments.Single().Consumed);
        Assert.Empty(game.ExecutedCommittedMaterialUses);
        Assert.Equal("use_local_recipient_material", game.Events[^1].Kind);

        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(BigInteger.Zero, coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalPendingOfferRaw);
        Assert.Equal(new BigInteger(4), coverage.TotalExecutedRecipientMaterialRaw);
        Assert.Equal(new BigInteger(4), coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(new BigInteger(4), coverage.TotalRealWorkforceMaterialRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalReturnedFromRecipientRaw);
        var status = coverage.Dispositions.Single();
        Assert.Equal(
            CanonicalMarketCustodyDisposition.ExecutedRecipientWorkforceMaterialUse,
            status.Disposition);
        Assert.Equal(receipt.ActionOrdinal, status.PhysicalUseActionOrdinal);
        Assert.Equal(receipt.ActionOrdinal, status.RecipientDispositionActionOrdinal);
        Assert.Equal(delivery.ActionOrdinal, status.PhysicalTransferActionOrdinal);
        Assert.True(status.ProvesRealPhysicalMaterialSink);
        Assert.False(status.ProvesExecutedCommercialSale);
        Assert.False(coverage.CanAuthorizeMonetaryRevenueOrTax);
        coverage.VerifyIndependent();

        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.RecipientCustodyDispositionEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(receipt.SourceAttributions.ToArray(),
            replay.ExecutedLocalRecipientCustodyDispositions.Single()
                .SourceAttributions.ToArray());
        Assert.Equal(coverage.Markets.ToArray(),
            CanonicalMarketExecutedPhysicalCoverage.FromCurrent(replay)
                .Markets.ToArray());
    }

    [Fact]
    public void ReturnRestoresExactlySamePhysicalVolumeAndAllowsOnlyNewOffer()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var before = game.StockAt(tile, new CommodityId(2U));
        var lockRecord = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var delivered = game.TransferMarketOfferToLocalRecipient(
            lockRecord.ActionOrdinal, EconomicActivityKind.Mining);
        Assert.Equal(before - 3UL, game.StockAt(tile, new CommodityId(2U)));

        var receipt = game.ReturnLocalRecipientCustody(lockRecord.ActionOrdinal);
        Assert.True(receipt.ProvesReturnOfSamePhysicalCustody);
        Assert.False(receipt.ProvesExecutedMaterialUse);
        Assert.Null(receipt.MaterialUseKind);
        Assert.Equal(0UL, receipt.ActiveBeneficiaryWorkers);
        Assert.Equal(delivered.ActionOrdinal, receipt.OriginalTransferActionOrdinal);
        Assert.Equal(before, game.StockAt(tile, new CommodityId(2U)));
        Assert.Equal("return_local_recipient_custody", game.Events[^1].Kind);
        Assert.Single(game.ExecutedLocalRecipientCustodyDispositions);
        Assert.True(game.PhysicalStockCommitments.Single().Transferred);
        Assert.False(game.PhysicalStockCommitments.Single().IsLive);

        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(BigInteger.Zero, view.TotalTransferredToRecipientRaw);
        Assert.Equal(new BigInteger(3), view.TotalReturnedFromRecipientRaw);
        Assert.Equal(BigInteger.Zero, view.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(BigInteger.Zero, view.TotalExecutedRecipientMaterialRaw);
        Assert.Equal(new BigInteger(3),
            view.Markets.Single(x => x.Commodity == 2U).ReleasedToFreeRaw);
        Assert.Equal(
            CanonicalMarketCustodyDisposition.ReturnedFromLocalRecipientCustody,
            view.Dispositions.Single().Disposition);
        Assert.False(view.Dispositions.Single().ProvesRealPhysicalMaterialSink);
        view.VerifyIndependent();

        var offeredAgain = game.CommitPhysicalStock(
            tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        Assert.NotEqual(lockRecord.ActionOrdinal, offeredAgain.ActionOrdinal);
        var current = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(new BigInteger(3), current.TotalPendingOfferRaw);
        Assert.Equal(new BigInteger(3), current.TotalReturnedFromRecipientRaw);
        Assert.Equal(BigInteger.Zero, current.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger(6), current.TotalHistoricallyOfferedRaw);
        Assert.False(current.ProvesCompletedMarketSales);
        var save = game.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(current.Markets.ToArray(),
            CanonicalMarketExecutedPhysicalCoverage.FromCurrent(restored)
                .Markets.ToArray());
    }

    [Fact]
    public void MixedUseReturnAndLiveRecipientStocksHaveNoDoubleCounting()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var a = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var b = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var c = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        game.TransferMarketOfferToLocalRecipient(a.ActionOrdinal,
            EconomicActivityKind.Mining);
        game.TransferMarketOfferToLocalRecipient(b.ActionOrdinal,
            EconomicActivityKind.Forestry);
        game.TransferMarketOfferToLocalRecipient(c.ActionOrdinal,
            EconomicActivityKind.Forestry);
        game.ReturnLocalRecipientCustody(a.ActionOrdinal);
        game.UseLocalRecipientMaterial(b.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);

        var view = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(game);
        Assert.Equal(new BigInteger(9), view.TotalHistoricallyOfferedRaw);
        Assert.Equal(new BigInteger(2), view.TotalReturnedFromRecipientRaw);
        Assert.Equal(new BigInteger(3), view.TotalExecutedRecipientMaterialRaw);
        Assert.Equal(new BigInteger(4), view.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, view.TotalPendingOfferRaw);
        Assert.False(view.ProvesCompletedMarketSales);
        Assert.False(view.CanAuthorizeMonetaryRevenueOrTax);
        view.VerifyIndependent();

        var save = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(view.Dispositions.ToArray(),
            CanonicalMarketExecutedPhysicalCoverage.FromCurrent(replay)
                .Dispositions.ToArray());
    }

    [Fact]
    public void AClosedRecipientCannotBeUsedReturnedOrReleasedTwice()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var wood = game.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        game.TransferMarketOfferToLocalRecipient(
            wood.ActionOrdinal, EconomicActivityKind.Forestry);
        game.UseLocalRecipientMaterial(wood.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;

        Assert.Throws<InvalidOperationException>(() =>
            game.UseLocalRecipientMaterial(wood.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            game.ReturnLocalRecipientCustody(wood.ActionOrdinal));
        Assert.Throws<InvalidOperationException>(() =>
            game.TransferMarketOfferToLocalRecipient(
                wood.ActionOrdinal, EconomicActivityKind.Forestry));
        Assert.Throws<InvalidOperationException>(() =>
            game.ReleasePhysicalStockCommitment(wood.ActionOrdinal));
        Assert.Throws<InvalidOperationException>(() =>
            game.UseCommittedLocalMaterial(wood.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            game.UseLocalRecipientMaterial(wood.ActionOrdinal,
                (CanonicalMaterialDispositionKind)255));
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
    }

    [Fact]
    public void HistoricalV37HandoffRemainsBitExactAndCannotBeMislabelledAsV39()
    {
        var game = Closed();
        var tile = game.InitialTile;
        var offer = game.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        game.TransferMarketOfferToLocalRecipient(
            offer.ActionOrdinal, EconomicActivityKind.Mining);
        var v37 = game.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.LocalRecipientCustodyEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(v37)!.Version);
        Assert.Equal(v37,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(v37).ExportSaveJson());

        var falselyVersioned = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(v37)!
                with { Version =
                    CanonicalPhysicalTacticalSession.RecipientCustodyDispositionEnvelopeFormat });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(falselyVersioned));

        game.ReturnLocalRecipientCustody(offer.ActionOrdinal);
        var v39 = game.ExportSaveJson();
        var forgedAsV37 = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(v39)!
                with { Version =
                    CanonicalPhysicalTacticalSession.LocalRecipientCustodyEnvelopeFormat });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(forgedAsV37));
        Assert.Equal(v39,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(v39).ExportSaveJson());
        Assert.NotEqual(v37, v39);
    }
}
