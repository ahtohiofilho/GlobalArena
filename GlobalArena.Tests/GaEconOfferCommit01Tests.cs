using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-OFFER-COMMIT-01: a market commitment and a retention are REAL
/// stock custody transfers, never a sale, payment, consumption or N.
/// </summary>
public sealed class GaEconOfferCommit01Tests
{
    private static CanonicalPhysicalTacticalSession ClosedLegacy()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        Assert.NotEmpty(session.State.Cycles);
        return session;
    }

    private static CanonicalPhysicalTacticalSession ClosedCash()
    {
        var session = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);
        Assert.NotNull(session.AdvanceMovementTurn());
        return session;
    }

    private static CanonicalPhysicalSaveEnvelope Envelope(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)!;

    private static CanonicalPhysicalSavePayload Payload(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(json).PayloadJson)!;

    private static string Rewrite(
        string json, Func<CanonicalPhysicalSavePayload,
            CanonicalPhysicalSavePayload> change, int? envelopeVersion = null)
    {
        var env = Envelope(json);
        var payloadJson = JsonSerializer.Serialize(change(Payload(json)));
        return JsonSerializer.Serialize(env with
        {
            Version = envelopeVersion ?? env.Version,
            PayloadJson = payloadJson,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)))
        });
    }

    [Fact]
    public void OfferCommitMovesActualWoodOutOfAvailableLocalStock()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        var wood = new CommodityId(3U);
        var available = session.StockAt(tile, wood);
        var before = session.CurrentHash;
        var cycle = session.State.Cycles[^1];
        var initialSave = session.ExportSaveJson();

        var record = session.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer, available);
        var witness = session.CapturePhysicalStockCommitmentWitness();

        Assert.Equal(0UL, session.StockAt(tile, wood));
        Assert.NotEqual(before, session.CurrentHash);
        Assert.NotEqual(initialSave, session.ExportSaveJson());
        Assert.Equal(cycle, session.State.Cycles[^1]);
        Assert.Equal(available, record.RawQuantity);
        Assert.True(record.IsLive);
        Assert.True(record.IsExecutedMarketOfferCommitment);
        Assert.False(record.IsExecutedSale);
        Assert.False(record.CreatesMoneyOrPhysicalGoods);
        Assert.Equal(available, (ulong)witness.LiveMarketOffersRaw);
        Assert.Equal(0UL, (ulong)witness.LiveRetentionsRaw);
        Assert.False(witness.HasExecutedMarketSales);
        Assert.False(witness.IssuesCurrencyOrBuyerWallets);
        Assert.False(witness.AuthenticatesNOrPriceCurve);
        Assert.Single(session.PhysicalStockCommitments);
        Assert.Equal("commit_physical_stock", session.Events[^1].Kind);
        Assert.Equal(3U, session.Events[^1].CommitmentCommodity);
        Assert.Equal(available, session.Events[^1].CommitmentRaw);
        Assert.Equal((byte)CanonicalPhysicalCommitmentKind.MarketOffer,
            session.Events[^1].CommitmentKind);
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MarketStockCommitmentEnvelopeFormat,
            Envelope(session.ExportSaveJson()).Version);
        Assert.Throws<InvalidOperationException>(() =>
            session.UsePhysicalMaterial(tile, wood,
                CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL));
    }

    [Fact]
    public void ReleasedOfferRestoresExactPhysicalStockAndBlocksDoubleRelease()
    {
        var session = ClosedLegacy();
        var wood = new CommodityId(3U);
        var tile = session.InitialTile;
        var before = session.StockAt(tile, wood);
        var reserved = session.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer, before / 2UL);
        var afterLock = session.CapturePhysicalStockCommitmentWitness();
        var restored = session.ReleasePhysicalStockCommitment(
            reserved.ActionOrdinal);

        Assert.Equal(before, session.StockAt(tile, wood));
        Assert.True(restored.Released);
        Assert.False(restored.IsLive);
        Assert.False(restored.IsExecutedMarketOfferCommitment);
        Assert.Equal(0UL,
            (ulong)session.CapturePhysicalStockCommitmentWitness().LiveMarketOffersRaw);
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MarketStockCommitmentEnvelopeFormat,
            Envelope(session.ExportSaveJson()).Version);
        Assert.Throws<InvalidOperationException>(afterLock.VerifyIndependent);
        Assert.Throws<InvalidOperationException>(() =>
            session.ReleasePhysicalStockCommitment(reserved.ActionOrdinal));
        session.CapturePhysicalStockCommitmentWitness().VerifyIndependent();
    }

    [Fact]
    public void DeliberateRetentionIsExecutedPhysicalCustodyNotUnsoldMarketSupply()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        var stone = new CommodityId(2U);
        var before = session.StockAt(tile, stone);
        var retained = session.CommitPhysicalStock(
            tile, stone, CanonicalPhysicalCommitmentKind.DeliberateRetention,
            before);

        Assert.Equal(0UL, session.StockAt(tile, stone));
        Assert.True(retained.IsExecutedRetention);
        Assert.False(retained.IsExecutedMarketOfferCommitment);
        var snapshot = session.CapturePhysicalStockCommitmentWitness();
        Assert.Equal(0UL, (ulong)snapshot.LiveMarketOffersRaw);
        Assert.Equal(before, (ulong)snapshot.LiveRetentionsRaw);
        Assert.Throws<InvalidOperationException>(() =>
            session.UsePhysicalMaterial(tile, stone,
                CanonicalMaterialDispositionKind.IntermediateInputUse, 1UL));
        session.ReleasePhysicalStockCommitment(retained.ActionOrdinal);
        Assert.Equal(before, session.StockAt(tile, stone));
    }

    [Fact]
    public void TwoCommitmentsCanNeverDebitTheSamePhysicalGoods()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        var wood = new CommodityId(3U);
        var available = session.StockAt(tile, wood);
        var first = session.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer,
            available);
        var hash = session.CurrentHash;
        var save = session.ExportSaveJson();

        Assert.Throws<InvalidOperationException>(() =>
            session.CommitPhysicalStock(tile, wood,
                CanonicalPhysicalCommitmentKind.DeliberateRetention, 1UL));
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Single(session.PhysicalStockCommitments);
        session.ReleasePhysicalStockCommitment(first.ActionOrdinal);
        Assert.Equal(available, session.StockAt(tile, wood));
    }

    [Fact]
    public void MarketOfferAndRetentionUseDistinctExclusiveCustody()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        var wood = new CommodityId(3U);
        var stone = new CommodityId(2U);
        var w = session.StockAt(tile, wood);
        var s = session.StockAt(tile, stone);
        var a = session.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer, w);
        var b = session.CommitPhysicalStock(
            tile, stone, CanonicalPhysicalCommitmentKind.DeliberateRetention, s);
        var witness = session.CapturePhysicalStockCommitmentWitness();
        Assert.Equal(w, (ulong)witness.LiveMarketOffersRaw);
        Assert.Equal(s, (ulong)witness.LiveRetentionsRaw);
        Assert.Equal(2, witness.Commitments.Count);
        Assert.NotEqual(a.ActionOrdinal, b.ActionOrdinal);
        Assert.Equal(0UL, session.StockAt(tile, wood));
        Assert.Equal(0UL, session.StockAt(tile, stone));
        session.ReleasePhysicalStockCommitment(b.ActionOrdinal);
        Assert.Equal(w, (ulong)session.CapturePhysicalStockCommitmentWitness()
            .LiveMarketOffersRaw);
        Assert.Equal(s, session.StockAt(tile, stone));
        session.ReleasePhysicalStockCommitment(a.ActionOrdinal);
        Assert.Equal(w, session.StockAt(tile, wood));
    }

    [Fact]
    public void ReservedFoodIsNotAvailableForResidentConsumptionUntilReleased()
    {
        var session = ClosedCash();
        var tile = session.InitialTile;
        var food = new CommodityId(1U);
        var stock = session.StockAt(tile, food);
        Assert.True(stock > 0UL);
        var locked = session.CommitPhysicalStock(
            tile, food, CanonicalPhysicalCommitmentKind.MarketOffer, stock);
        Assert.Equal(0UL, session.StockAt(tile, food));
        Assert.NotNull(session.AdvanceMovementTurn());
        var proof = session.CapturePhysicalStockCommitmentWitness();
        Assert.Equal(stock, (ulong)proof.LiveMarketOffersRaw);
        Assert.True(session.StockAt(tile, food) >= 0UL);
        session.ReleasePhysicalStockCommitment(locked.ActionOrdinal);
        Assert.True(session.StockAt(tile, food) >= stock);
    }

    [Fact]
    public void ReplayPreservesCommitmentAndReleaseAndHistoricalSaves()
    {
        var baseline = ClosedLegacy();
        var untouched = baseline.ExportSaveJson();
        Assert.Equal(untouched, baseline.ExportSaveJson());
        var tile = baseline.InitialTile;
        var wood = new CommodityId(3U);
        var quantity = baseline.StockAt(tile, wood);
        var lockA = baseline.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer, quantity);
        var lockB = baseline.CommitPhysicalStock(
            tile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 1UL);
        baseline.ReleasePhysicalStockCommitment(lockB.ActionOrdinal);
        var save = baseline.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(baseline.CurrentHash, replay.CurrentHash);
        Assert.Equal(baseline.PhysicalStockCommitments.ToArray(),
            replay.PhysicalStockCommitments.ToArray());
        var original = baseline.CapturePhysicalStockCommitmentWitness();
        var reconstructed = replay.CapturePhysicalStockCommitmentWitness();
        Assert.Equal(original.LiveMarketOffersRaw,
            reconstructed.LiveMarketOffersRaw);
        Assert.Equal(original.LiveRetentionsRaw,
            reconstructed.LiveRetentionsRaw);
        Assert.Throws<InvalidOperationException>(() =>
            original.RequireCurrent(replay));
        replay.ReleasePhysicalStockCommitment(lockA.ActionOrdinal);
        Assert.Equal(quantity, replay.StockAt(tile, wood));

        var old = CanonicalPhysicalTacticalSession.RestoreSaveJson(untouched);
        Assert.Equal(untouched, old.ExportSaveJson());
    }

    [Fact]
    public void MarketCommitmentsSurviveAdditionalRealEconomicCycles()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        var wood = new CommodityId(3U);
        var amount = session.StockAt(tile, wood);
        var lockA = session.CommitPhysicalStock(
            tile, wood, CanonicalPhysicalCommitmentKind.MarketOffer, amount);
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        var snapshot = session.CapturePhysicalStockCommitmentWitness();
        Assert.Equal(amount, (ulong)snapshot.LiveMarketOffersRaw);
        Assert.Equal(2, session.State.Cycles.Count);
        var save = session.ExportSaveJson();
        var loaded = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, loaded.ExportSaveJson());
        Assert.Equal(amount, (ulong)loaded.CapturePhysicalStockCommitmentWitness()
            .LiveMarketOffersRaw);
        loaded.ReleasePhysicalStockCommitment(lockA.ActionOrdinal);
        Assert.True(loaded.StockAt(tile, wood) >= amount);
    }

    [Fact]
    public void CashModeCommitmentUsesNewVersionWithoutChangingCashLedger()
    {
        var session = ClosedCash();
        var tile = session.InitialTile;
        var food = new CommodityId(1U);
        var quantity = session.StockAt(tile, food);
        Assert.True(quantity > 0UL);
        var accounts = session.State.FoodCash!.Accounts.ToArray();
        var receipts = session.State.FoodCash.Receipts.ToArray();
        var lockA = session.CommitPhysicalStock(tile, food,
            CanonicalPhysicalCommitmentKind.MarketOffer, quantity);
        Assert.Equal(accounts, session.State.FoodCash!.Accounts.ToArray());
        Assert.Equal(receipts, session.State.FoodCash.Receipts.ToArray());
        var save = session.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MarketStockCommitmentEnvelopeFormat,
            Envelope(save).Version);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(quantity,
            (ulong)replay.CapturePhysicalStockCommitmentWitness()
                .LiveMarketOffersRaw);
        replay.ReleasePhysicalStockCommitment(lockA.ActionOrdinal);
        Assert.True(replay.StockAt(tile, food) >= quantity);
    }

    [Fact]
    public void FakeCommittedQuantityOrVersionIsRejectedEvenWithRehashedEnvelope()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        session.CommitPhysicalStock(tile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer, 1UL);
        var save = session.ExportSaveJson();

        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                Rewrite(save, p => p with {
                    PhysicalCommitments = p.PhysicalCommitments!.Select(
                        x => x with { RawQuantity = x.RawQuantity + 1UL }).ToArray()
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                Rewrite(save, p => p, envelopeVersion:
                    CanonicalPhysicalTacticalSession.MaterialDispositionEnvelopeFormat)));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                Rewrite(save, p => p with {
                    PhysicalCommitments = null
                })));
    }

    [Fact]
    public void InvalidAmountsKindsAndUnknownReleaseDoNotMutateStocksOrHashes()
    {
        var session = ClosedLegacy();
        var tile = session.InitialTile;
        var wood = new CommodityId(3U);
        var available = session.StockAt(tile, wood);
        var before = session.ExportSaveJson();
        var hash = session.CurrentHash;
        Assert.Throws<ArgumentException>(() =>
            session.CommitPhysicalStock(tile, wood,
                CanonicalPhysicalCommitmentKind.MarketOffer, 0UL));
        Assert.Throws<ArgumentException>(() =>
            session.CommitPhysicalStock(tile, wood,
                (CanonicalPhysicalCommitmentKind)255, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            session.CommitPhysicalStock(tile, wood,
                CanonicalPhysicalCommitmentKind.MarketOffer,
                available + 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            session.ReleasePhysicalStockCommitment(9999));
        Assert.Equal(before, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
    }
}
