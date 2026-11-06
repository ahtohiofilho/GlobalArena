using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-OFFER-DELIVERY-01: local physical use from market custody is
/// authenticated; it never pretends to be a sale, freight or cash receipt.
/// </summary>
public sealed class GaEconOfferDelivery01Tests
{
    private static CanonicalPhysicalTacticalSession Legacy()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        Assert.NotEmpty(session.State.Cycles);
        return session;
    }

    private static CanonicalPhysicalTacticalSession Cash() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static CanonicalPhysicalSaveEnvelope Envelope(string value) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(value)!;

    private static CanonicalPhysicalSavePayload Payload(string value) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(value).PayloadJson)!;

    private static string Rewrite(string json,
        Func<CanonicalPhysicalSavePayload, CanonicalPhysicalSavePayload> change,
        int? version = null)
    {
        var old = Envelope(json);
        var payload = JsonSerializer.Serialize(change(Payload(json)));
        return JsonSerializer.Serialize(old with
        {
            Version = version ?? old.Version,
            PayloadJson = payload,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
        });
    }

    [Fact]
    public void CommittedWoodIsActuallyUsedOnceWithoutSecondStockDebit()
    {
        var s = Legacy();
        var tile = s.InitialTile;
        var wood = new CommodityId(3U);
        var before = s.StockAt(tile, wood);
        Assert.True(before > 2UL);
        var committed = s.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer, before);
        var afterCommit = s.StockAt(tile, wood);
        var hash = s.CurrentHash.HexDigest;
        var oldCycle = s.State.Cycles[^1];
        var use = s.UseCommittedLocalMaterial(
            committed.ActionOrdinal, CanonicalMaterialDispositionKind.FinalPhysicalUse);

        Assert.Equal(0UL, afterCommit);
        Assert.Equal(afterCommit, s.StockAt(tile, wood));
        Assert.NotEqual(hash, s.CurrentHash.HexDigest);
        Assert.Equal(oldCycle, s.State.Cycles[^1]);
        Assert.Equal(before, use.UsedRaw);
        Assert.Equal((ulong)before,
            use.SourceAttributions.Aggregate(0UL,
                (sum, x) => checked(sum + x.RawQuantity)));
        Assert.True(use.ProvesExecutedPhysicalMaterialUse);
        Assert.False(use.ProvesDeliveryToFinalEconomicBuyer);
        Assert.False(use.ProvesMonetarySale);
        Assert.False(use.HasExecutedTransport);
        Assert.False(use.CreatesNewPhysicalStock);
        Assert.False(use.AuthorizesPurchasingPowerIndexN);
        Assert.Single(s.ExecutedCommittedMaterialUses);
        Assert.Equal("use_committed_material", s.Events[^1].Kind);
        Assert.Equal(committed.ActionOrdinal, s.Events[^1].CommitmentOrdinal);
        Assert.Equal((byte)CanonicalMaterialDispositionKind.FinalPhysicalUse,
            s.Events[^1].MaterialUseKind);
        Assert.True(s.PhysicalStockCommitments.Single().Consumed);
        Assert.False(s.PhysicalStockCommitments.Single().Released);
        Assert.Equal(0UL, (ulong)s.CapturePhysicalStockCommitmentWitness()
            .LiveMarketOffersRaw);
        Assert.Equal(
            CanonicalPhysicalTacticalSession.ExecutedMarketMaterialUseEnvelopeFormat,
            Envelope(s.ExportSaveJson()).Version);
    }

    [Fact]
    public void StoneIntermediateUseHasExecutedSinkWithoutManufacturedOutput()
    {
        var s = Legacy();
        var stone = new CommodityId(2U);
        var before = s.StockAt(s.InitialTile, stone);
        var source = s.CommitPhysicalStock(s.InitialTile, stone,
            CanonicalPhysicalCommitmentKind.MarketOffer, before);
        var executed = s.UseCommittedLocalMaterial(
            source.ActionOrdinal, CanonicalMaterialDispositionKind.IntermediateInputUse);
        Assert.Equal(before, executed.UsedRaw);
        Assert.Equal(CanonicalMaterialDispositionKind.IntermediateInputUse,
            executed.Kind);
        Assert.Equal(0UL, s.StockAt(s.InitialTile, stone));
        Assert.False(executed.ProvesMonetarySale);
        Assert.Empty(s.ExecutedMaterialDispositions);
    }

    [Fact]
    public void MarketUseCannotDoubleSpendOrBeReleasedAfterExecution()
    {
        var s = Legacy();
        var tile = s.InitialTile;
        var wood = new CommodityId(3U);
        var before = s.StockAt(tile, wood);
        var offer = s.CommitPhysicalStock(tile, wood,
            CanonicalPhysicalCommitmentKind.MarketOffer, before);
        s.UseCommittedLocalMaterial(offer.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var hash = s.CurrentHash;
        var save = s.ExportSaveJson();

        Assert.Throws<InvalidOperationException>(() =>
            s.UseCommittedLocalMaterial(offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            s.ReleasePhysicalStockCommitment(offer.ActionOrdinal));
        Assert.Throws<InvalidOperationException>(() =>
            s.UsePhysicalMaterial(tile, wood,
                CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL));
        Assert.Equal(hash, s.CurrentHash);
        Assert.Equal(save, s.ExportSaveJson());
        Assert.Single(s.ExecutedCommittedMaterialUses);
    }

    [Fact]
    public void DeliberateRetentionAndFoodAreNotThisMaterialMarketSink()
    {
        var s = Legacy();
        var tile = s.InitialTile;
        var retained = s.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            s.UseCommittedLocalMaterial(retained.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        s.ReleasePhysicalStockCommitment(retained.ActionOrdinal);

        var c = Cash();
        Assert.NotNull(c.AdvanceMovementTurn());
        var food = c.StockAt(c.InitialTile, new CommodityId(1U));
        Assert.True(food > 0UL);
        var offer = c.CommitPhysicalStock(c.InitialTile, new CommodityId(1U),
            CanonicalPhysicalCommitmentKind.MarketOffer, food);
        Assert.Throws<InvalidOperationException>(() =>
            c.UseCommittedLocalMaterial(offer.ActionOrdinal,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.True(c.PhysicalStockCommitments.Single().IsLive);
        c.ReleasePhysicalStockCommitment(offer.ActionOrdinal);
        Assert.Equal(food, c.StockAt(c.InitialTile, new CommodityId(1U)));
    }

    [Fact]
    public void ConservationSurvivesFurtherCyclesAndUnrelatedUnreservedUse()
    {
        var s = Legacy();
        var tile = s.InitialTile;
        var wood = new CommodityId(3U);
        var stone = new CommodityId(2U);
        var beforeStone = s.StockAt(tile, stone);
        var offer = s.CommitPhysicalStock(tile, wood,
            CanonicalPhysicalCommitmentKind.MarketOffer,
            s.StockAt(tile, wood));
        var material = s.UsePhysicalMaterial(tile, stone,
            CanonicalMaterialDispositionKind.IntermediateInputUse, 1UL);
        var use = s.UseCommittedLocalMaterial(offer.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        Assert.Equal(beforeStone - 1UL, s.StockAt(tile, stone));
        Assert.Equal(1UL, material.UsedRaw);
        Assert.True(use.ProvesExecutedPhysicalMaterialUse);
        for (var i = 0; i < 6; i++) s.AdvanceMovementTurn();
        s.CapturePhysicalStockCommitmentWitness().VerifyIndependent();
        Assert.Single(s.ExecutedCommittedMaterialUses);
        Assert.Equal(2, s.State.Cycles.Count);
    }

    [Fact]
    public void NewSaveReplaysExactJournalAndHistoricalV31BytesRemainUnchanged()
    {
        var s = Legacy();
        var old = s.ExportSaveJson();
        var commitment = s.CommitPhysicalStock(s.InitialTile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer,
            s.StockAt(s.InitialTile, new CommodityId(3U)));
        var oldV31 = s.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.MarketStockCommitmentEnvelopeFormat,
            Envelope(oldV31).Version);
        Assert.False(Payload(oldV31).PhysicalCommitments!.Single().Consumed);

        var used = s.UseCommittedLocalMaterial(commitment.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var newer = s.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(newer);
        Assert.Equal(newer, replay.ExportSaveJson());
        Assert.Equal(s.CurrentHash, replay.CurrentHash);
        Assert.Equal(s.Events.ToArray(), replay.Events.ToArray());
        Assert.Equal(s.PhysicalStockCommitments.ToArray(),
            replay.PhysicalStockCommitments.ToArray());
        Assert.Single(replay.ExecutedCommittedMaterialUses);
        Assert.Equal(used.UsedRaw, replay.ExecutedCommittedMaterialUses[0].UsedRaw);
        Assert.Equal(used.SourceAttributions.ToArray(),
            replay.ExecutedCommittedMaterialUses[0].SourceAttributions.ToArray());

        Assert.Equal(oldV31,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldV31).ExportSaveJson());
        Assert.Equal(old,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(old).ExportSaveJson());
    }

    [Fact]
    public void ConsumedSummaryOrEnvelopeDowngradeIsRejectedWithRehashedPayload()
    {
        var s = Legacy();
        var lockA = s.CommitPhysicalStock(s.InitialTile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        s.UseCommittedLocalMaterial(lockA.ActionOrdinal,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var json = s.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                Rewrite(json, x => x with {
                    PhysicalCommitments = x.PhysicalCommitments!
                        .Select(y => y with { Consumed = false }).ToArray()
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                Rewrite(json, x => x,
                    CanonicalPhysicalTacticalSession.MarketStockCommitmentEnvelopeFormat)));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                Rewrite(json, x => x with {
                    Actions = x.Actions.Where(a => a.Kind != "use_committed_material").ToArray()
                })));
    }

    [Fact]
    public void InvalidKindAndUnknownCommitmentDoNotWriteNewHashOrSave()
    {
        var s = Legacy();
        var lockA = s.CommitPhysicalStock(s.InitialTile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var hash = s.CurrentHash;
        var save = s.ExportSaveJson();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            s.UseCommittedLocalMaterial(lockA.ActionOrdinal,
                (CanonicalMaterialDispositionKind)255));
        Assert.Throws<InvalidOperationException>(() =>
            s.UseCommittedLocalMaterial(9999,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Equal(hash, s.CurrentHash);
        Assert.Equal(save, s.ExportSaveJson());
    }
}
