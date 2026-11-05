using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS117: one atomic action turns physically held wood and stone into an
/// UNCOMMISSIONED material frame, without a fabricated industrial yield.
/// </summary>
public sealed class GaVs117ExecutedPrimitiveAssemblyTests
{
    private static CanonicalPhysicalTacticalSession Stocked()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(2U)) > 0UL);
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(3U)) > 0UL);
        return game;
    }

    private static CanonicalPhysicalSaveEnvelope Envelope(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)
            ?? throw new InvalidOperationException("Missing canonical envelope.");

    private static CanonicalPhysicalSavePayload Payload(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(json).PayloadJson)
            ?? throw new InvalidOperationException("Missing canonical payload.");

    private static string Tamper(string save,
        Func<CanonicalPhysicalSavePayload, CanonicalPhysicalSavePayload> edit,
        int? envelopeVersion = null)
    {
        var envelope = Envelope(save);
        var raw = JsonSerializer.Serialize(edit(Payload(save)));
        return JsonSerializer.Serialize(envelope with
        {
            Version = envelopeVersion ?? envelope.Version,
            PayloadJson = raw,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
        });
    }

    [Fact]
    public void AssemblyDebitsBothMaterialsOnceAndCreatesOneJournalBackedFrame()
    {
        var game = Stocked();
        var tile = game.InitialTile;
        var wood = new CommodityId(3U);
        var stone = new CommodityId(2U);
        var originalWood = game.StockAt(tile, wood);
        var originalStone = game.StockAt(tile, stone);
        var cycle = game.State.Cycles[^1];
        var worldHash = game.CurrentHash.HexDigest;
        var workers = game.State.TotalWorkers;
        var food = game.StockAt(tile, new CommodityId(1U));
        var w = Math.Max(1UL, originalWood / 3UL);
        var s = Math.Max(1UL, originalStone / 4UL);

        var frame = game.AssemblePrimitiveMaterialFrame(
            tile, CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, w, s);

        Assert.True(frame.HasRealConservedMaterialInputs);
        Assert.True(frame.HasJournalReconstructablePhysicalAssembly);
        Assert.False(frame.IsCommissionedOrProductiveFacility);
        Assert.False(frame.CreatesNewTradeableCommodity);
        Assert.False(frame.InventsConversionYieldOrLaborTime);
        Assert.False(frame.AuthenticatesIndividualFungibleLotIdentity);
        Assert.False(frame.HasProducedEconomicFinalValue);
        Assert.False(frame.CanAuthorizeRealPurchasingPower);

        Assert.Equal(originalWood - w, game.StockAt(tile, wood));
        Assert.Equal(originalStone - s, game.StockAt(tile, stone));
        Assert.Equal(food, game.StockAt(tile, new CommodityId(1U)));
        Assert.Equal(cycle, game.State.Cycles[^1]);
        Assert.Equal(workers, game.State.TotalWorkers);
        Assert.Equal(worldHash, frame.BeforeWorldHashHex);
        Assert.Equal(game.CurrentHash.HexDigest, frame.AfterWorldHashHex);
        Assert.Equal(w, frame.IncorporatedWoodRaw);
        Assert.Equal(s, frame.IncorporatedStoneRaw);
        Assert.Equal(w, frame.WoodSources.Aggregate(0UL,
            (total, r) => checked(total + r.RawQuantity)));
        Assert.Equal(s, frame.StoneSources.Aggregate(0UL,
            (total, r) => checked(total + r.RawQuantity)));
        Assert.All(frame.WoodSources, r => Assert.Equal(3U, r.Commodity));
        Assert.All(frame.StoneSources, r => Assert.Equal(2U, r.Commodity));
        Assert.Single(game.ExecutedPrimitiveAssemblies);
        Assert.Empty(game.ExecutedMaterialDispositions);

        var action = game.Events[^1];
        Assert.Equal("assemble_primitive", action.Kind);
        Assert.Equal(0UL, action.FoodRaw);
        Assert.Equal(0UL, action.Workers);
        Assert.Equal(0U, action.MaterialCommodity);
        Assert.Equal(0UL, action.MaterialRaw);
        Assert.Equal((byte)frame.Kind, action.AssemblyKind);
        Assert.Equal(w, action.AssemblyWoodRaw);
        Assert.Equal(s, action.AssemblyStoneRaw);
    }

    [Fact]
    public void InsufficientWoodOrStoneRejectsEntireTransactionWithoutStockOrJournalChange()
    {
        var game = Stocked();
        var tile = game.InitialTile;
        var wood = game.StockAt(tile, new CommodityId(3U));
        var stone = game.StockAt(tile, new CommodityId(2U));
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            game.AssemblePrimitiveMaterialFrame(
                tile, CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,
                wood + 1UL, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            game.AssemblePrimitiveMaterialFrame(
                tile, CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,
                1UL, stone + 1UL));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.ExecutedPrimitiveAssemblies);
    }

    [Fact]
    public void RefusesZeroInputsUnknownAssemblyAndUninhabitedLocationWithoutMutation()
    {
        var game = Stocked();
        var tile = game.InitialTile;
        var emptyTile = game.AdjacentWorkable(tile)[0];
        Assert.Equal(0UL, game.WorkersAt(emptyTile));
        var save = game.ExportSaveJson();

        Assert.Throws<ArgumentException>(() =>
            game.AssemblePrimitiveMaterialFrame(tile,
                CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 0UL, 1UL));
        Assert.Throws<ArgumentException>(() =>
            game.AssemblePrimitiveMaterialFrame(tile,
                CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 0UL));
        Assert.Throws<ArgumentException>(() =>
            game.AssemblePrimitiveMaterialFrame(tile,
                (CanonicalPrimitiveAssemblyKind)99, 1UL, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            game.AssemblePrimitiveMaterialFrame(emptyTile,
                CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL));
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.ExecutedPrimitiveAssemblies);
    }

    [Fact]
    public void MaterialAssemblyEnvelope23ReplaysIdenticalJournalAndPhysicalHoldings()
    {
        var game = Stocked();
        var oldSave = game.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.SaveFormat,
            Envelope(oldSave).Version);
        Assert.Equal(oldSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldSave).ExportSaveJson());

        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 2UL, 3UL);
        var save = game.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.MaterialAssemblyEnvelopeFormat,
            Envelope(save).Version);
        Assert.Contains("AssemblyWoodRaw", Envelope(save).PayloadJson);
        Assert.Contains("AssemblyStoneRaw", Envelope(save).PayloadJson);

        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var restored = Assert.Single(replay.ExecutedPrimitiveAssemblies);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(game.Events, replay.Events);
        Assert.Equal(game.State.Stocks, replay.State.Stocks);
        Assert.Equal(frame.ActionOrdinal, restored.ActionOrdinal);
        Assert.Equal(frame.WoodSources.ToArray(), restored.WoodSources.ToArray());
        Assert.Equal(frame.StoneSources.ToArray(), restored.StoneSources.ToArray());
        Assert.Equal(frame.AfterWorldHashHex, restored.AfterWorldHashHex);
    }

    [Fact]
    public void MixedMaterialUseAndAssemblyMaintainDistinctJournalEventsAndDoNotDoubleCount()
    {
        var game = Stocked();
        var tile = game.InitialTile;
        var originalWood = game.StockAt(tile, new CommodityId(3U));
        var originalStone = game.StockAt(tile, new CommodityId(2U));

        game.UsePhysicalMaterial(tile, new CommodityId(3U),
            CanonicalMaterialDispositionKind.IntermediateInputUse, 1UL);
        game.AssemblePrimitiveMaterialFrame(tile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 2UL, 3UL);
        game.UsePhysicalMaterial(tile, new CommodityId(2U),
            CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL);

        Assert.Equal(originalWood - 3UL,
            game.StockAt(tile, new CommodityId(3U)));
        Assert.Equal(originalStone - 4UL,
            game.StockAt(tile, new CommodityId(2U)));
        Assert.Equal(2, game.ExecutedMaterialDispositions.Count);
        Assert.Single(game.ExecutedPrimitiveAssemblies);
        var actions = game.Events.TakeLast(3).ToArray();
        Assert.Equal("material_use", actions[0].Kind);
        Assert.Equal("assemble_primitive", actions[1].Kind);
        Assert.Equal("material_use", actions[2].Kind);
        Assert.Equal(actions[0].AfterHashHex, actions[1].BeforeHashHex);
        Assert.Equal(actions[1].AfterHashHex, actions[2].BeforeHashHex);
        Assert.Equal(CanonicalPhysicalTacticalSession.MaterialAssemblyEnvelopeFormat,
            Envelope(game.ExportSaveJson()).Version);
        var replay = CanonicalPhysicalTacticalSession
            .RestoreSaveJson(game.ExportSaveJson());
        Assert.Equal(game.ExportSaveJson(), replay.ExportSaveJson());
        Assert.Equal(game.Events, replay.Events);
        Assert.Equal(game.ExecutedPrimitiveAssemblies[0].WoodSources.ToArray(),
            replay.ExecutedPrimitiveAssemblies[0].WoodSources.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DormantTreasuryBeforeOrAfterAssemblyReplaysUnderEnvelope24(
        bool treasuryFirst)
    {
        var game = Stocked();
        if (treasuryFirst)
            game.EnableDormantTreasuryReservation();
        game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL);
        if (!treasuryFirst)
            game.EnableDormantTreasuryReservation();

        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MaterialAssemblyWithTreasuryEnvelopeFormat,
            Envelope(save).Version);
        Assert.NotNull(Payload(save).TreasuryReservation);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(game.Events, restored.Events);
        Assert.Equal(game.ExecutedPrimitiveAssemblies[0].WoodSources.ToArray(),
            restored.ExecutedPrimitiveAssemblies[0].WoodSources.ToArray());
    }

    [Fact]
    public void OldMaterialOnlyEnvelope21KeepsExactHistoricalBytes()
    {
        var game = Stocked();
        game.UsePhysicalMaterial(game.InitialTile, new CommodityId(3U),
            CanonicalMaterialDispositionKind.FinalPhysicalUse, 1UL);
        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MaterialDispositionEnvelopeFormat,
            Envelope(save).Version);
        Assert.DoesNotContain("AssemblyWoodRaw", Envelope(save).PayloadJson);
        Assert.Equal(save,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(save).ExportSaveJson());
    }

    [Fact]
    public void AssemblyRemainsARecordedPhysicalFrameAcrossNewEconomicCycles()
    {
        var game = Stocked();
        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL);
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        game.CaptureLatestPhysicalLotWitness().VerifyIndependent();
        Assert.Equal(frame.AfterWorldHashHex,
            Assert.Single(game.ExecutedPrimitiveAssemblies).AfterWorldHashHex);
        var save = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.ExecutedPrimitiveAssemblies[0].StoneSources.ToArray(),
            replay.ExecutedPrimitiveAssemblies[0].StoneSources.ToArray());
        Assert.False(replay.ExecutedPrimitiveAssemblies[0].CanAuthorizeRealPurchasingPower);
    }

    [Fact]
    public void TamperedAmountsOrEnvelopeCannotPassRehashedReplay()
    {
        var game = Stocked();
        game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL);
        var save = game.ExportSaveJson();
        var changed = Tamper(save, p =>
        {
            var actions = p.Actions.ToArray();
            actions[^1] = actions[^1] with { AssemblyStoneRaw = 2UL };
            return p with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(changed));

        var replacedEnvelope = Tamper(save, p => p,
            CanonicalPhysicalTacticalSession.MaterialDispositionEnvelopeFormat);
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(replacedEnvelope));

        var missingAmount = Tamper(save, p =>
        {
            var actions = p.Actions.ToArray();
            actions[^1] = actions[^1] with { AssemblyWoodRaw = 0UL };
            return p with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(missingAmount));
    }
}
