using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

internal static class GaVs102IntegratedTreasurySaveFixture
{
    internal static CanonicalPhysicalTacticalSession OpenLegacy() =>
        GaVs101DormantTreasuryFixture.StartSession();

    internal static CanonicalPhysicalSaveEnvelope Envelope(string save) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)
            ?? throw new InvalidOperationException("Missing physical envelope.");

    internal static CanonicalPhysicalSavePayload Payload(string save) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(save).PayloadJson)
            ?? throw new InvalidOperationException("Missing physical payload.");

    internal static string RewritePayload(string save,
        Func<CanonicalPhysicalSavePayload, CanonicalPhysicalSavePayload> edit,
        int? envelopeVersion = null)
    {
        var envelope = Envelope(save);
        var payloadJson = JsonSerializer.Serialize(edit(Payload(save)));
        return JsonSerializer.Serialize(envelope with {
            Version = envelopeVersion ?? envelope.Version,
            PayloadJson = payloadJson,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)))
        });
    }
}

public sealed class GaVs102IntegratedTreasurySaveTests
{
    [Fact]
    public void ExistingLegacySaveRoundTripsByteForByteUntilExplicitActivation()
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var original = source.ExportSaveJson();
        var envelope = GaVs102IntegratedTreasurySaveFixture.Envelope(original);
        Assert.InRange(envelope.Version,
            CanonicalPhysicalTacticalSession.SaveFormat,
            CanonicalPhysicalTacticalSession.TransportAuthorityEnvelopeFormat);
        Assert.Null(GaVs102IntegratedTreasurySaveFixture.Payload(original)
            .TreasuryReservation);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(original);
        Assert.Equal(original, restored.ExportSaveJson());
        Assert.Equal(source.CurrentHash, restored.CurrentHash);
        Assert.Equal(source.Events, restored.Events);
        Assert.Null(restored.Game.Snapshot.TreasuryReservation);
        Assert.Null(source.Game.Snapshot.TreasuryReservation);
    }

    [Fact]
    public void NewV20EnvelopeCarriesBoundWorldAndZeroMoneyWithoutChangingOldActionHashes()
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var oldSave = source.ExportSaveJson();
        var oldHash = source.CurrentHash;
        var beforeActions = source.Events.ToArray();
        var originalFoodAccounts = source.FoodCash!.Accounts.ToArray();
        source.EnableDormantTreasuryReservation();
        source.EnableDormantTreasuryReservation();
        Assert.Equal(beforeActions, source.Events);
        Assert.NotEqual(oldHash, source.CurrentHash);
        Assert.Equal(
            CanonicalWorldStateHasher.DormantTreasuryReservationHashFormatVersion,
            source.CurrentHash.FormatVersion);
        var json = source.ExportSaveJson();
        var envelope = GaVs102IntegratedTreasurySaveFixture.Envelope(json);
        var payload = GaVs102IntegratedTreasurySaveFixture.Payload(json);
        Assert.Equal(
            CanonicalPhysicalTacticalSession.IntegratedDormantTreasuryEnvelopeFormat,
            envelope.Version);
        Assert.InRange(payload.Version,
            CanonicalPhysicalTacticalSession.SaveFormat,
            CanonicalPhysicalTacticalSession.AutomaticCampSaveFormat);
        Assert.NotNull(payload.TreasuryReservation);
        Assert.Equal(beforeActions.Length,
            payload.TreasuryReservation!.ActivatedAfterActionCount);
        Assert.Equal(0UL, payload.TreasuryReservation.SpendableBalanceMinorUnits);
        Assert.Equal(0, payload.TreasuryReservation.MonetaryReceiptCount);
        Assert.Equal(source.Game.Snapshot.WorldBinding!.WorldSignatureSha256Hex,
            payload.TreasuryReservation.WorldSignatureSha256Hex);
        Assert.Equal(source.CurrentHash.HexDigest, payload.FinalHashHex);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(json, reopened.ExportSaveJson());
        Assert.Equal(source.CurrentHash, reopened.CurrentHash);
        Assert.Equal(source.Events, reopened.Events);
        Assert.Equal(originalFoodAccounts, reopened.FoodCash!.Accounts);
        Assert.Equal(oldSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldSave)
                .ExportSaveJson());
    }

    [Fact]
    public void HistoricalActionsBeforeActivationAndNewActionsAfterActivationReplay()
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var oldActions = source.Events.ToArray();
        Assert.NotEmpty(oldActions);
        source.EnableDormantTreasuryReservation();
        source.AdvanceMovementTurn();
        source.AdvanceMovementTurn();
        var json = source.ExportSaveJson();
        var payload = GaVs102IntegratedTreasurySaveFixture.Payload(json);
        Assert.Equal(oldActions.Length,
            payload.TreasuryReservation!.ActivatedAfterActionCount);
        Assert.Equal(oldActions, source.Events.Take(oldActions.Length));
        Assert.Equal(source.CurrentHash.HexDigest, payload.FinalHashHex);
        Assert.NotEqual(oldActions[^1].AfterHashHex,
            source.Events[^1].AfterHashHex);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(source.CurrentHash, restored.CurrentHash);
        Assert.Equal(source.Events, restored.Events);
        Assert.Equal(json, restored.ExportSaveJson());

        // Continue real canonical action replay after loading the V20 save.
        restored.AdvanceMovementTurn();
        var futureSave = restored.ExportSaveJson();
        var future = CanonicalPhysicalTacticalSession.RestoreSaveJson(futureSave);
        Assert.Equal(restored.Events, future.Events);
        Assert.Equal(restored.CurrentHash, future.CurrentHash);
        Assert.Equal(futureSave, future.ExportSaveJson());
    }

    [Fact]
    public void ReservationBeforeFirstActionCanSaveAndReplayAcrossHashBoundary()
    {
        var source = CanonicalPhysicalTacticalSession.Start();
        Assert.Empty(source.Events);
        var baseline = source.ExportSaveJson();
        source.EnableDormantTreasuryReservation();
        var justReserved = source.ExportSaveJson();
        Assert.Equal(0,
            GaVs102IntegratedTreasurySaveFixture.Payload(justReserved)
                .TreasuryReservation!.ActivatedAfterActionCount);
        Assert.Equal(justReserved,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(justReserved)
                .ExportSaveJson());
        source.AdvanceMovementTurn();
        source.AdvanceMovementTurn();
        var saved = source.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(source.CurrentHash, reopened.CurrentHash);
        Assert.Equal(saved, reopened.ExportSaveJson());
        Assert.Equal(baseline,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(baseline)
                .ExportSaveJson());
    }

    [Fact]
    public void LegacySaveExplicitlyUpgradesToV20WithoutChangingPriorActionMetadata()
    {
        var originalSession = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var oldArchive = originalSession.ExportSaveJson();
        var priorActions = originalSession.Events.ToArray();
        var afterLegacyLoad =
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldArchive);
        afterLegacyLoad.EnableDormantTreasuryReservation();
        var upgraded = afterLegacyLoad.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.IntegratedDormantTreasuryEnvelopeFormat,
            GaVs102IntegratedTreasurySaveFixture.Envelope(upgraded).Version);
        Assert.Equal(priorActions, afterLegacyLoad.Events);
        var twice = CanonicalPhysicalTacticalSession.RestoreSaveJson(upgraded);
        Assert.Equal(upgraded, twice.ExportSaveJson());
        Assert.Equal(priorActions, twice.Events);
        Assert.Equal(afterLegacyLoad.CurrentHash, twice.CurrentHash);
        Assert.Equal(oldArchive,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldArchive)
                .ExportSaveJson());
    }

    [Fact]
    public void CorruptedOrRehashedFalseTreasuryFieldsAreRejected()
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        source.EnableDormantTreasuryReservation();
        var json = source.ExportSaveJson();
        var env = GaVs102IntegratedTreasurySaveFixture.Envelope(json);
        var original = GaVs102IntegratedTreasurySaveFixture.Payload(json);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                JsonSerializer.Serialize(env with {
                    PayloadSha256Hex = new string('0', 64)
                })));
        foreach(var corrupt in new Func<CanonicalPhysicalSavePayload,
            CanonicalPhysicalSavePayload>[] {
            x => x with { TreasuryReservation = x.TreasuryReservation! with {
                SpendableBalanceMinorUnits = 1UL } },
            x => x with { TreasuryReservation = x.TreasuryReservation! with {
                MonetaryReceiptCount = 1 } },
            x => x with { TreasuryReservation = x.TreasuryReservation! with {
                SchemaVersion = 2U } },
            x => x with { TreasuryReservation = x.TreasuryReservation! with {
                ActivatedAfterActionCount = -1 } },
            x => x with { TreasuryReservation = x.TreasuryReservation! with {
                ActivatedAfterActionCount = x.Actions.Length + 1 } },
            x => x with { TreasuryReservation = x.TreasuryReservation! with {
                WorldSignatureSha256Hex = new string('a',64) } },
            x => x with { TreasuryReservation = null },
            x => x with { FinalHashHex = new string('0',64) },
            x => x with { Actions = null! }
        })
            Assert.ThrowsAny<Exception>(() =>
                CanonicalPhysicalTacticalSession.RestoreSaveJson(
                    GaVs102IntegratedTreasurySaveFixture.RewritePayload(
                        json, corrupt)));
        Assert.Equal(original,
            GaVs102IntegratedTreasurySaveFixture.Payload(json));
    }

    [Fact]
    public void LegacyEnvelopeRejectsSmuggledTreasuryAndVersion20RejectsMissingRow()
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var old = source.ExportSaveJson();
        var oldPayload = GaVs102IntegratedTreasurySaveFixture.Payload(old);
        source.EnableDormantTreasuryReservation();
        var modern = source.ExportSaveJson();
        var newPayload = GaVs102IntegratedTreasurySaveFixture.Payload(modern);
        Assert.NotNull(newPayload.TreasuryReservation);

        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                GaVs102IntegratedTreasurySaveFixture.RewritePayload(
                    old, x => x with {
                        TreasuryReservation = newPayload.TreasuryReservation
                    })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                GaVs102IntegratedTreasurySaveFixture.RewritePayload(
                    modern, x => x with { TreasuryReservation = null })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(
                GaVs102IntegratedTreasurySaveFixture.RewritePayload(
                    modern, x => x, envelopeVersion: 18)));
        Assert.Null(oldPayload.TreasuryReservation);
    }

    [Fact]
    public void SameV20ArchiveDeterministicallyReplaysEvenWithSeparateTransportJournal()
    {
        var original = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        original.EnableDormantTreasuryReservation();
        var saved = original.ExportSaveJson();
        var row = GaVs102IntegratedTreasurySaveFixture.Payload(saved)
            .TreasuryReservation!;
        Assert.Equal(original.Events.Count, row.ActivatedAfterActionCount);
        for(var i=0;i<3;i++)
        {
            var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
            Assert.Equal(saved, reopened.ExportSaveJson());
            Assert.Equal(original.CurrentHash, reopened.CurrentHash);
            Assert.Equal(original.Events, reopened.Events);
            Assert.Equal(0UL, reopened.Game.Snapshot
                .TreasuryReservation!.SpendableBalanceMinorUnits);
        }
    }

    [Fact]
    public void HashedWorldBindingAndSaveRemainImmutableWithoutExplicitActivation()
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var beforeSave = source.ExportSaveJson();
        var hash = source.CurrentHash;
        var accounts = source.FoodCash!.Accounts.ToArray();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(beforeSave);
        Assert.Equal(beforeSave, source.ExportSaveJson());
        Assert.Equal(hash, source.CurrentHash);
        Assert.Equal(beforeSave, restored.ExportSaveJson());
        Assert.Null(source.Game.Snapshot.TreasuryReservation);
        Assert.Null(restored.Game.Snapshot.TreasuryReservation);
        Assert.Equal(accounts, source.FoodCash.Accounts);
    }
}
