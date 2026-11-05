using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs116ExecutedMaterialDispositionTests
{
    private static CanonicalPhysicalTacticalSession WithWoodAndStone()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        Assert.NotEmpty(session.State.Cycles);
        Assert.True(session.StockAt(session.InitialTile, new CommodityId(2U)) > 0UL);
        Assert.True(session.StockAt(session.InitialTile, new CommodityId(3U)) > 0UL);
        return session;
    }

    private static CanonicalPhysicalSaveEnvelope Envelope(string value) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(value)!;

    private static CanonicalPhysicalSavePayload Payload(string value) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(value).PayloadJson)!;

    private static string Rewrite(
        string original, Func<CanonicalPhysicalSavePayload,
            CanonicalPhysicalSavePayload> change, int? version = null)
    {
        var envelope = Envelope(original);
        var payloadJson = JsonSerializer.Serialize(change(Payload(original)));
        return JsonSerializer.Serialize(envelope with
        {
            Version = version ?? envelope.Version,
            PayloadJson = payloadJson,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)))
        });
    }

    [Fact]
    public void ExecutedFinalUseReallyDebitsWoodAndKeepsEconomicCycleCounters()
    {
        var session = WithWoodAndStone();
        var tile = session.InitialTile;
        var wood = new CommodityId(3U);
        var start = session.StockAt(tile, wood);
        var stone = session.StockAt(tile, new CommodityId(2U));
        var oldCycle = session.State.Cycles[^1];
        var oldHash = session.CurrentHash.HexDigest;
        var oldWorkers = session.State.TotalWorkers;
        var amount = Math.Max(1UL, start / 2UL);

        var use = session.UsePhysicalMaterial(
            tile, wood, CanonicalMaterialDispositionKind.FinalPhysicalUse, amount);

        Assert.Equal(start - amount, session.StockAt(tile, wood));
        Assert.Equal(stone, session.StockAt(tile, new CommodityId(2U)));
        Assert.Equal(oldCycle, session.State.Cycles[^1]);
        Assert.Equal(oldWorkers, session.State.TotalWorkers);
        Assert.Equal(oldCycle.MovementTurn, session.State.MovementTurn);
        Assert.NotEqual(oldHash, session.CurrentHash.HexDigest);
        Assert.True(use.ProvesActualMaterialDebit);
        Assert.False(use.ProvesConstructionOrFinishedManufacturing);
        Assert.False(use.ProvesOriginalFungibleLotIdentity);
        Assert.False(use.CreatesMoneyOrCredit);
        Assert.False(use.CanAuthorizeRealPurchasingPower);
        Assert.Equal(amount, use.SourceAttributions.Aggregate(
            0UL, (total, row) => checked(total + row.RawQuantity)));

        var action = session.Events[^1];
        Assert.Equal("material_use", action.Kind);
        Assert.Equal(0UL, action.FoodRaw);
        Assert.Equal(3U, action.MaterialCommodity);
        Assert.Equal((byte)CanonicalMaterialDispositionKind.FinalPhysicalUse,
            action.MaterialUseKind);
        Assert.Equal(amount, action.MaterialRaw);
        Assert.Equal(action.AfterHashHex, use.AfterWorldHashHex);
        Assert.Equal(action.BeforeHashHex, use.BeforeWorldHashHex);
        Assert.Single(session.ExecutedMaterialDispositions);
    }

    [Fact]
    public void IntermediateInputConsumesRealStoneWithoutFabricatingOutput()
    {
        var session = WithWoodAndStone();
        var tile = session.InitialTile;
        var stone = new CommodityId(2U);
        var oldStock = session.StockAt(tile, stone);
        var wood = session.StockAt(tile, new CommodityId(3U));
        var cycleCount = session.State.Cycles.Count;
        var beforeHash = session.CurrentHash;
        var amount = Math.Max(1UL, oldStock / 4UL);
        var record = session.UsePhysicalMaterial(
            tile, stone, CanonicalMaterialDispositionKind.IntermediateInputUse,
            amount);

        Assert.Equal(oldStock - amount, session.StockAt(tile, stone));
        Assert.Equal(wood, session.StockAt(tile, new CommodityId(3U)));
        Assert.Equal(cycleCount, session.State.Cycles.Count);
        Assert.NotEqual(beforeHash, session.CurrentHash);
        Assert.Equal(CanonicalMaterialDispositionKind.IntermediateInputUse,
            record.Kind);
        Assert.False(record.ProvesConstructionOrFinishedManufacturing);
        Assert.False(record.CanAuthorizeRealPurchasingPower);
    }

    [Fact]
    public void TwoDifferentMaterialUsesHaveIndividualEventsWithoutDuplicateDepletion()
    {
        var session = WithWoodAndStone();
        var wood = new CommodityId(3U);
        var stone = new CommodityId(2U);
        var tile = session.InitialTile;
        var w = session.StockAt(tile, wood);
        var s = session.StockAt(tile, stone);
        session.UsePhysicalMaterial(tile, wood,
            CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL);
        session.UsePhysicalMaterial(tile, stone,
            CanonicalMaterialDispositionKind.IntermediateInputUse, 1UL);
        Assert.Equal(w - 1UL, session.StockAt(tile, wood));
        Assert.Equal(s - 1UL, session.StockAt(tile, stone));
        Assert.Equal(2, session.ExecutedMaterialDispositions.Count);
        Assert.Equal(3U, session.ExecutedMaterialDispositions[0].Commodity);
        Assert.Equal(2U, session.ExecutedMaterialDispositions[1].Commodity);
        Assert.Equal(session.Events[^2].AfterHashHex,
            session.Events[^1].BeforeHashHex);
        Assert.Equal(session.Events[^2].AfterHashHex,
            session.ExecutedMaterialDispositions[1].BeforeWorldHashHex);
    }

    [Fact]
    public void RefusesZeroInvalidCommodityInvalidUseOrInsufficientRealMaterial()
    {
        var session = WithWoodAndStone();
        var tile = session.InitialTile;
        var stone = new CommodityId(2U);
        var before = session.ExportSaveJson();
        var count = session.Events.Count;
        Assert.Throws<ArgumentException>(() =>
            session.UsePhysicalMaterial(tile, stone,
                CanonicalMaterialDispositionKind.FinalPhysicalUse, 0UL));
        Assert.Throws<ArgumentException>(() =>
            session.UsePhysicalMaterial(tile, new CommodityId(1U),
                CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL));
        Assert.Throws<ArgumentException>(() =>
            session.UsePhysicalMaterial(tile, stone,
                (CanonicalMaterialDispositionKind)99, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            session.UsePhysicalMaterial(tile, stone,
                CanonicalMaterialDispositionKind.FinalPhysicalUse,
                checked(session.StockAt(tile, stone) + 1UL)));
        Assert.Equal(before, session.ExportSaveJson());
        Assert.Equal(count, session.Events.Count);
        Assert.Empty(session.ExecutedMaterialDispositions);
    }

    [Fact]
    public void ExistingSavesRemainUnchangedAndMaterialSaveUsesEnvelope21()
    {
        var session = WithWoodAndStone();
        var before = session.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.SaveFormat,
            Envelope(before).Version);
        Assert.DoesNotContain("MaterialCommodity", Envelope(before).PayloadJson);
        var restoredOld = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(before, restoredOld.ExportSaveJson());

        session.UsePhysicalMaterial(session.InitialTile, new CommodityId(3U),
            CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL);
        var save = session.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.MaterialDispositionEnvelopeFormat,
            Envelope(save).Version);
        Assert.Contains("MaterialCommodity", Envelope(save).PayloadJson);
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(session.CurrentHash, reopened.CurrentHash);
        Assert.Equal(session.State.Stocks, reopened.State.Stocks);
        Assert.Equal(session.Events, reopened.Events);
        Assert.Equal(session.ExecutedMaterialDispositions.Count,
            reopened.ExecutedMaterialDispositions.Count);
        Assert.Equal(session.ExecutedMaterialDispositions[0].UsedRaw,
            reopened.ExecutedMaterialDispositions[0].UsedRaw);
        Assert.Equal(session.ExecutedMaterialDispositions[0].AfterWorldHashHex,
            reopened.ExecutedMaterialDispositions[0].AfterWorldHashHex);
        Assert.Equal(session.ExecutedMaterialDispositions[0].SourceAttributions,
            reopened.ExecutedMaterialDispositions[0].SourceAttributions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TreasuryBeforeOrAfterMaterialActionUsesVersion22AndReplays(bool treasuryFirst)
    {
        var session = WithWoodAndStone();
        if (treasuryFirst)
            session.EnableDormantTreasuryReservation();

        session.UsePhysicalMaterial(session.InitialTile, new CommodityId(2U),
            CanonicalMaterialDispositionKind.IntermediateInputUse, 1UL);
        if (!treasuryFirst)
            session.EnableDormantTreasuryReservation();
        var save = session.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MaterialWithTreasuryEnvelopeFormat,
            Envelope(save).Version);
        Assert.NotNull(Payload(save).TreasuryReservation);
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(session.CurrentHash, reopened.CurrentHash);
        Assert.Equal(session.Events, reopened.Events);
        Assert.Equal(session.ExecutedMaterialDispositions[0].SourceAttributions,
            reopened.ExecutedMaterialDispositions[0].SourceAttributions);
    }

    [Fact]
    public void PhysicalMaterialUseRemainsInHistoryAcrossFutureEconomicCycles()
    {
        var session = WithWoodAndStone();
        var record = session.UsePhysicalMaterial(
            session.InitialTile, new CommodityId(3U),
            CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL);
        for (var i = 0; i < 6; i++)
            session.AdvanceMovementTurn();
        var witness = session.CaptureLatestPhysicalLotWitness();
        witness.VerifyIndependent();
        Assert.Single(session.ExecutedMaterialDispositions);
        Assert.Equal(record.AfterWorldHashHex,
            session.ExecutedMaterialDispositions[0].AfterWorldHashHex);
        var save = session.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(session.CurrentHash, reopened.CurrentHash);
        Assert.Equal(session.ExecutedMaterialDispositions[0].SourceAttributions,
            reopened.ExecutedMaterialDispositions[0].SourceAttributions);
    }

    [Fact]
    public void CorruptedMaterialEvidenceFailsEvenIfEnvelopeChecksumIsRewritten()
    {
        var session = WithWoodAndStone();
        session.UsePhysicalMaterial(session.InitialTile, new CommodityId(2U),
            CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL);
        var save = session.ExportSaveJson();
        var missingQuantity = Rewrite(save, p =>
        {
            var actions = p.Actions.ToArray();
            actions[^1] = actions[^1] with { MaterialRaw = 0UL };
            return p with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(missingQuantity));

        var unauthorizedEnvelope = Rewrite(save, p => p,
            CanonicalPhysicalTacticalSession.SaveFormat);
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(unauthorizedEnvelope));
    }

    [Fact]
    public void CannotSpendStoneAtUninhabitedOrForeignTile()
    {
        var session = WithWoodAndStone();
        var outside = session.AdjacentWorkable(session.InitialTile)[0];
        var before = session.ExportSaveJson();
        Assert.Equal(0UL, session.WorkersAt(outside));
        Assert.Throws<InvalidOperationException>(() =>
            session.UsePhysicalMaterial(outside, new CommodityId(2U),
                CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL));
        Assert.Equal(before, session.ExportSaveJson());
    }
}
