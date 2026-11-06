using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-ECON-BENEFICIARY-01: attest the real local WORKFORCE receiving a
/// physically executed use. No invented buyer, transfer or remote freight.
/// </summary>
public sealed class GaEconBeneficiary01Tests
{
    private static CanonicalPhysicalTacticalSession Legacy()
    {
        var session = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++) session.AdvanceMovementTurn();
        Assert.NotEmpty(session.State.Cycles);
        return session;
    }

    private static CanonicalPhysicalTacticalSession Cash()
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

    private static string RehashAction(
        string json, Func<CanonicalPhysicalAction, CanonicalPhysicalAction> edit)
    {
        var envelope = Envelope(json);
        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            envelope.PayloadJson)!;
        var actions = payload.Actions.Select((x, i) =>
            i == payload.Actions.Length - 1 ? edit(x) : x).ToArray();
        var payloadJson = JsonSerializer.Serialize(payload with { Actions = actions });
        return JsonSerializer.Serialize(envelope with
        {
            PayloadJson = payloadJson,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)))
        });
    }

    private static CanonicalPhysicalStockCommitment CommitWood(
        CanonicalPhysicalTacticalSession session, ulong? amount = null) =>
        session.CommitPhysicalStock(
            session.InitialTile, new CommodityId(3U),
            CanonicalPhysicalCommitmentKind.MarketOffer,
            amount ?? session.StockAt(session.InitialTile, new CommodityId(3U)));

    [Fact]
    public void ExecutedMaterialUseIdentifiesARealLocalWorkgroup()
    {
        var session = Legacy();
        var tile = session.InitialTile;
        var workerGroup = session.State.Allocations.Single(x =>
            x.TileId == tile && x.Activity == EconomicActivityKind.Forestry);
        Assert.True(workerGroup.Workers > 0UL);
        var before = session.StockAt(tile, new CommodityId(3U));
        var lockA = CommitWood(session);
        var afterLock = session.StockAt(tile, new CommodityId(3U));

        var use = session.UseCommittedLocalMaterialForWorkforce(
            lockA.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        var action = session.Events[^1];

        Assert.Equal(before, use.UsedRaw);
        Assert.Equal(afterLock, session.StockAt(tile, new CommodityId(3U)));
        Assert.Equal("use_committed_material_for_workers", action.Kind);
        Assert.Equal(tile.FineStrategicCellId.Value, action.Source);
        Assert.Equal(action.Source, action.Destination);
        Assert.Equal((byte)EconomicActivityKind.Forestry, action.ToJob);
        Assert.Equal(0, action.FromJob);
        Assert.Equal(workerGroup.Workers, action.Workers);
        Assert.Equal(EconomicActivityKind.Forestry, use.BeneficiaryActivity);
        Assert.Equal(workerGroup.Workers, use.BeneficiaryWorkers);
        Assert.True(use.ProvesLocalWorkforceGroup);
        Assert.False(use.ProvesNamedIndividualOrLegalBuyer);
        Assert.False(use.ProvesRemoteDelivery);
        Assert.False(use.ProvesMonetarySale);
        Assert.False(use.ProvesDeliveryToFinalEconomicBuyer);
        Assert.False(use.HasExecutedTransport);
        Assert.Single(session.ExecutedCommittedMaterialUses);
        Assert.True(session.PhysicalStockCommitments.Single().Consumed);
        Assert.Equal(0UL,
            (ulong)session.CapturePhysicalStockCommitmentWitness().LiveMarketOffersRaw);
        Assert.Equal(
            CanonicalPhysicalTacticalSession.ExecutedWorkforceMaterialUseEnvelopeFormat,
            Envelope(session.ExportSaveJson()).Version);
    }

    [Fact]
    public void MissingLocalActivityCannotPretendToReceiveMaterial()
    {
        var session = Legacy();
        var tile = session.InitialTile;
        var group = session.State.Allocations.Single(x =>
            x.TileId == tile && x.Activity == EconomicActivityKind.Forestry);
        var lockA = CommitWood(session);
        session.ReassignWorkers(tile, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, group.Workers);
        Assert.DoesNotContain(session.State.Allocations, x =>
            x.TileId == tile && x.Activity == EconomicActivityKind.Forestry);
        var save = session.ExportSaveJson();
        var hash = session.CurrentHash;

        Assert.Throws<InvalidOperationException>(() =>
            session.UseCommittedLocalMaterialForWorkforce(
                lockA.ActionOrdinal, EconomicActivityKind.Forestry,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.True(session.PhysicalStockCommitments.Single().IsLive);
    }

    [Fact]
    public void SamePhysicalOfferCannotBeConsumedAgainOrReleased()
    {
        var session = Legacy();
        var lockA = CommitWood(session, 1UL);
        var first = session.UseCommittedLocalMaterialForWorkforce(
            lockA.ActionOrdinal, EconomicActivityKind.Agriculture,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var save = session.ExportSaveJson();
        var hash = session.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            session.UseCommittedLocalMaterialForWorkforce(
                lockA.ActionOrdinal, EconomicActivityKind.Agriculture,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            session.UseCommittedLocalMaterial(
                lockA.ActionOrdinal, CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            session.ReleasePhysicalStockCommitment(lockA.ActionOrdinal));
        Assert.Single(session.ExecutedCommittedMaterialUses);
        Assert.Equal(1UL, first.UsedRaw);
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(save, session.ExportSaveJson());
    }

    [Fact]
    public void Save35RestoresExactRecordedWorkerActivityAndPopulation()
    {
        var session = Legacy();
        var lockA = CommitWood(session, 1UL);
        var receipt = session.UseCommittedLocalMaterialForWorkforce(
            lockA.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.IntermediateInputUse);
        var save = session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(session.CurrentHash, restored.CurrentHash);
        Assert.Equal(session.Events.ToArray(), restored.Events.ToArray());
        Assert.Equal(session.PhysicalStockCommitments.ToArray(),
            restored.PhysicalStockCommitments.ToArray());
        Assert.Equal(receipt.BeneficiaryActivity,
            restored.ExecutedCommittedMaterialUses.Single().BeneficiaryActivity);
        Assert.Equal(receipt.BeneficiaryWorkers,
            restored.ExecutedCommittedMaterialUses.Single().BeneficiaryWorkers);
        Assert.Equal(receipt.SourceAttributions.ToArray(),
            restored.ExecutedCommittedMaterialUses.Single().SourceAttributions.ToArray());
        restored.CapturePhysicalStockCommitmentWitness().VerifyIndependent();
    }

    [Fact]
    public void OlderSave33AndOlderV31RemainByteIdenticalOnReplay()
    {
        var session = Legacy();
        var initialSave = session.ExportSaveJson();
        var lockA = CommitWood(session, 1UL);
        var v31 = session.ExportSaveJson();
        session.UseCommittedLocalMaterial(
            lockA.ActionOrdinal, CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var v33 = session.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.ExecutedMarketMaterialUseEnvelopeFormat,
            Envelope(v33).Version);
        Assert.False(session.ExecutedCommittedMaterialUses.Single()
            .ProvesLocalWorkforceGroup);
        Assert.Equal(initialSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(initialSave).ExportSaveJson());
        Assert.Equal(v31,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(v31).ExportSaveJson());
        Assert.Equal(v33,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(v33).ExportSaveJson());
    }

    [Fact]
    public void NewActionCannotBeDowngradedByTamperingWithChecksum()
    {
        var session = Legacy();
        var lockA = CommitWood(session, 1UL);
        session.UseCommittedLocalMaterialForWorkforce(
            lockA.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        var save = session.ExportSaveJson();
        var original = Envelope(save);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                JsonSerializer.Serialize(original with
                {
                    Version =
                        CanonicalPhysicalTacticalSession.ExecutedMarketMaterialUseEnvelopeFormat
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                RehashAction(save, x => x with { Workers = 0UL })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                RehashAction(save, x => x with { ToJob = 0 })));
    }

    [Fact]
    public void CashModeUsesItsActualWorkerGroupWithoutCreatingCashPayments()
    {
        var session = Cash();
        var tile = session.InitialTile;
        var wood = session.StockAt(tile, new CommodityId(3U));
        Assert.True(wood > 0UL);
        var accounts = session.State.FoodCash!.Accounts.ToArray();
        var receipts = session.State.FoodCash.Receipts.ToArray();
        var originalCycle = session.State.Cycles[^1];
        var lockA = CommitWood(session, 1UL);
        var use = session.UseCommittedLocalMaterialForWorkforce(
            lockA.ActionOrdinal, EconomicActivityKind.Forestry,
            CanonicalMaterialDispositionKind.FinalPhysicalUse);
        Assert.True(use.ProvesLocalWorkforceGroup);
        Assert.False(use.ProvesMonetarySale);
        Assert.Equal(originalCycle, session.State.Cycles[^1]);
        Assert.Equal(accounts, session.State.FoodCash!.Accounts.ToArray());
        Assert.Equal(receipts, session.State.FoodCash.Receipts.ToArray());
        var save = session.ExportSaveJson();
        Assert.Equal(save,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(save).ExportSaveJson());
    }

    [Fact]
    public void UnknownJobOrRetentionCannotBeRelabelledAsWorkforceDelivery()
    {
        var session = Legacy();
        var lockA = CommitWood(session, 1UL);
        var retained = session.CommitPhysicalStock(
            session.InitialTile, new CommodityId(2U),
            CanonicalPhysicalCommitmentKind.DeliberateRetention, 1UL);
        var before = session.ExportSaveJson();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.UseCommittedLocalMaterialForWorkforce(
                lockA.ActionOrdinal, (EconomicActivityKind)255,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Throws<InvalidOperationException>(() =>
            session.UseCommittedLocalMaterialForWorkforce(
                retained.ActionOrdinal, EconomicActivityKind.Forestry,
                CanonicalMaterialDispositionKind.FinalPhysicalUse));
        Assert.Equal(before, session.ExportSaveJson());
    }
}
