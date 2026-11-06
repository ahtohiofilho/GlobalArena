using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaEconMarketDefault01jTests
{
    private static CanonicalPhysicalTacticalSession Closed()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        return session;
    }

    [Fact]
    public void RealLocalRecipientTakesExclusivePhysicalCustodyWithoutSaleOrConsumption()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var originalWood = session.StockAt(tile, new CommodityId(3U));
        Assert.True(originalWood > 4UL);
        var lockRecord = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        var before = session.ExportSaveJson();
        var beforeHash = session.CurrentHash;
        var beforePool = CanonicalDefaultMarketPoolPreflight.FromCurrent(session);

        var receipt = session.TransferMarketOfferToLocalRecipient(
            lockRecord.ActionOrdinal, EconomicActivityKind.Forestry);

        Assert.Equal(lockRecord.ActionOrdinal, receipt.SourceCommitmentActionOrdinal);
        Assert.Equal(4UL, receipt.RawQuantity);
        Assert.Equal(3U, receipt.Commodity);
        Assert.Equal(tile.FineStrategicCellId.Value, receipt.FineTile);
        Assert.Equal(EconomicActivityKind.Forestry, receipt.RecipientActivity);
        Assert.True(receipt.RecipientWorkers > 0UL);
        Assert.True(receipt.ProvesPhysicalCustodyHandoff);
        Assert.True(receipt.ProvesRealLocalRecipientGroup);
        Assert.False(receipt.ProvesActualConsumption);
        Assert.False(receipt.ProvesExecutedCommercialSale);
        Assert.False(receipt.ProvesLegalOwnershipTransfer);
        Assert.False(receipt.ExecutesRemoteFreight);
        Assert.False(receipt.AuthorizesPriceOrRevenuePosting);
        Assert.False(receipt.AuthorizesNewPurchasingPower);
        Assert.False(receipt.ChangesFoodNutrition);

        Assert.NotEqual(beforeHash, session.CurrentHash);
        Assert.Equal(originalWood - 4UL, session.StockAt(tile, new CommodityId(3U)));
        Assert.Empty(session.ExecutedCommittedMaterialUses);
        Assert.Single(session.ExecutedLocalRecipientCustodyTransfers);
        Assert.True(session.PhysicalStockCommitments.Single().Transferred);
        Assert.False(session.PhysicalStockCommitments.Single().IsLive);
        Assert.False(session.PhysicalStockCommitments.Single().Consumed);
        Assert.False(session.PhysicalStockCommitments.Single().Released);
        Assert.Equal("transfer_market_offer_local_recipient", session.Events[^1].Kind);
        Assert.Equal(receipt.ActionOrdinal, session.Events.Count);
        Assert.Equal(4UL, receipt.SourceAttributions.Aggregate(0UL,
            (sum, lot) => checked(sum + lot.RawQuantity)));
        Assert.All(receipt.SourceAttributions, lot =>
        {
            Assert.Equal(CanonicalPhysicalLotCustodyKind.MarketCommitment,
                lot.Custody);
            Assert.Equal((ulong)lockRecord.ActionOrdinal, lot.CustodyId);
        });

        session.CapturePhysicalStockCommitmentWitness().VerifyIndependent();
        var afterPool = CanonicalDefaultMarketPoolPreflight.FromCurrent(session);
        Assert.Equal(beforePool.EligibleByDefaultRaw - 4,
            afterPool.EligibleByDefaultRaw);
        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(session);
        Assert.Equal(new BigInteger(4),
            coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalPendingOfferRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(beforePool.EligibleByDefaultRaw - 4,
            coverage.TotalEligibleAwaitingDestinationRaw);
        var disposition = coverage.Dispositions.Single();
        Assert.Equal(
            CanonicalMarketCustodyDisposition.TransferredToRealLocalRecipient,
            disposition.Disposition);
        Assert.Equal(receipt.ActionOrdinal, disposition.PhysicalTransferActionOrdinal);
        Assert.Equal(EconomicActivityKind.Forestry, disposition.BeneficiaryActivity);
        Assert.True(disposition.BeneficiaryWorkers > 0UL);
        Assert.False(disposition.ProvesExecutedCommercialSale);
        Assert.False(disposition.ProvesRealPhysicalMaterialSink);
        Assert.False(coverage.ProvesCompletedMarketSales);
        Assert.False(coverage.CanAuthorizeMonetaryRevenueOrTax);
        coverage.VerifyIndependent();

        var saved = session.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(saved)!;
        Assert.Equal(CanonicalPhysicalTacticalSession.LocalRecipientCustodyEnvelopeFormat,
            envelope.Version);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(saved, restored.ExportSaveJson());
        Assert.Equal(session.CurrentHash, restored.CurrentHash);
        Assert.Equal(session.Events.ToArray(), restored.Events.ToArray());
        Assert.Equal(receipt.SourceAttributions.ToArray(),
            restored.ExecutedLocalRecipientCustodyTransfers.Single()
                .SourceAttributions.ToArray());
        Assert.Equal(coverage.Dispositions.ToArray(),
            CanonicalMarketExecutedPhysicalCoverage.FromCurrent(restored)
                .Dispositions.ToArray());
        Assert.NotEqual(before, saved);
    }

    [Fact]
    public void TransferIsExclusiveWithReleaseConsumptionAndSecondTransfer()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var lockRecord = session.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        session.TransferMarketOfferToLocalRecipient(
            lockRecord.ActionOrdinal, EconomicActivityKind.Mining);
        var saved = session.ExportSaveJson();
        var hash = session.CurrentHash;

        Assert.Throws<InvalidOperationException>(() =>
            session.TransferMarketOfferToLocalRecipient(
                lockRecord.ActionOrdinal, EconomicActivityKind.Mining));
        Assert.Throws<InvalidOperationException>(() =>
            session.ReleasePhysicalStockCommitment(lockRecord.ActionOrdinal));
        Assert.Throws<InvalidOperationException>(() =>
            session.UseCommittedLocalMaterialForWorkforce(
                lockRecord.ActionOrdinal, EconomicActivityKind.Mining,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalStagedLocalMaterialUseBatch.Prepare(session, new[]
            {
                new CanonicalLocalWorkforceMaterialUseOrder(
                    lockRecord.ActionOrdinal, EconomicActivityKind.Mining,
                    CanonicalMaterialDispositionKind.FinalPhysicalUse)
            }));
        Assert.Equal(saved, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.Single(session.ExecutedLocalRecipientCustodyTransfers);
    }

    [Fact]
    public void ExistingMaterialUseAndNewRecipientHandoffReplayInSameNewEnvelope()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var wood = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 2UL);
        var stone = session.CommitPhysicalStock(tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);

        var sink = session.UseCommittedLocalMaterialForWorkforce(
            wood.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        var handoff = session.TransferMarketOfferToLocalRecipient(
            stone.ActionOrdinal, EconomicActivityKind.Mining);
        var save = session.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.LocalRecipientCustodyEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version);

        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(session.CurrentHash, replay.CurrentHash);
        Assert.Equal(sink.UsedRaw,
            replay.ExecutedCommittedMaterialUses.Single().UsedRaw);
        Assert.Equal(handoff.RawQuantity,
            replay.ExecutedLocalRecipientCustodyTransfers.Single().RawQuantity);
        var coverage = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(replay);
        Assert.Equal(new BigInteger(2),
            coverage.TotalExecutedPhysicalMaterialRaw);
        Assert.Equal(new BigInteger(3),
            coverage.TotalTransferredToRecipientRaw);
        Assert.Equal(new BigInteger(5),
            coverage.TotalHistoricallyOfferedRaw);
        Assert.Equal(BigInteger.Zero, coverage.TotalPendingOfferRaw);
        Assert.False(coverage.ProvesCompletedMarketSales);
    }

    [Fact]
    public void RetainedAndAggregateFoodCannotBeTransferredToMaterialGroup()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var retained = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 2UL);
        var food = session.StockAt(tile, new CommodityId(1U));
        CanonicalPhysicalStockCommitment? foodCommitment = null;
        if (food > 0UL)
            foodCommitment = session.CommitPhysicalStock(
                tile, new CommodityId(1U),
                CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var save = session.ExportSaveJson();

        Assert.Throws<InvalidOperationException>(() =>
            session.TransferMarketOfferToLocalRecipient(
                retained.ActionOrdinal, EconomicActivityKind.Forestry));
        Assert.Throws<InvalidOperationException>(() =>
            session.TransferMarketOfferToLocalRecipient(
                9999, EconomicActivityKind.Forestry));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.TransferMarketOfferToLocalRecipient(
                retained.ActionOrdinal, (EconomicActivityKind)254));
        if (foodCommitment is not null)
            Assert.Throws<InvalidOperationException>(() =>
                session.TransferMarketOfferToLocalRecipient(
                    foodCommitment.ActionOrdinal, EconomicActivityKind.Agriculture));
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Empty(session.ExecutedLocalRecipientCustodyTransfers);
    }

    [Fact]
    public void LegacyMarketSaveRemainsBitExactAndInvalidEnvelopeFailsClosed()
    {
        var session = Closed();
        var tile = session.InitialTile;
        session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 3UL);
        var old = session.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.MarketStockCommitmentEnvelopeFormat,
            JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(old)!.Version);
        Assert.Equal(old,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(old).ExportSaveJson());

        var first = session.PhysicalStockCommitments.Single();
        session.TransferMarketOfferToLocalRecipient(
            first.ActionOrdinal, EconomicActivityKind.Forestry);
        var transferred = session.ExportSaveJson();
        var original = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            transferred)!;
        var wrong = JsonSerializer.Serialize(original with
        {
            Version = CanonicalPhysicalTacticalSession.ExecutedWorkforceMaterialUseEnvelopeFormat
        });
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(wrong));
        Assert.Equal(transferred, session.ExportSaveJson());
    }

    [Fact]
    public void PreviouslyTransferredGoodsAreNotOfferedAgainByAutomaticBatch()
    {
        var session = Closed();
        var tile = session.InitialTile;
        var initial = session.StockAt(tile, new CommodityId(3U));
        var hold = session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 4UL);
        session.TransferMarketOfferToLocalRecipient(
            hold.ActionOrdinal, EconomicActivityKind.Forestry);
        var current = session.ExportSaveJson();
        var previousHash = session.CurrentHash;
        var pending = CanonicalDefaultMarketPoolPreflight.FromCurrent(session);
        var staged = CanonicalStagedAutomaticMarketOfferBatch.Prepare(session);
        var after = CanonicalMarketExecutedPhysicalCoverage.FromCurrent(
            staged.PreparedSession);

        Assert.Equal(new BigInteger(4), after.TotalTransferredToRecipientRaw);
        Assert.Equal(new BigInteger(4), after.TotalHistoricallyOfferedRaw -
            after.TotalPendingOfferRaw);
        Assert.Equal(new BigInteger(initial - 4UL),
            CanonicalDefaultMarketPoolPreflight.FromCurrent(
                staged.PreparedSession).ForTileCommodity(
                    tile.FineStrategicCellId.Value, 3U)
                .AlreadyCommittedOfferRaw);
        Assert.Equal(pending.EligibleByDefaultRaw,
            staged.TotalEligibleRaw);
        Assert.Equal(current, session.ExportSaveJson());
        Assert.Equal(previousHash, session.CurrentHash);
    }
}
