using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

internal static class GaVs101DormantTreasuryFixture
{
    internal static CanonicalPhysicalTacticalSession StartSession() =>
        GaVs100TreasuryDryRunFixture.Build().Source.Source.Physical.Fixture.Session;

    internal static string RewritePayload(string archive,
        Func<CanonicalDormantTreasuryArchivePayload,
            CanonicalDormantTreasuryArchivePayload> edit)
    {
        var envelope = JsonSerializer.Deserialize<
            CanonicalDormantTreasuryArchiveEnvelope>(archive)!;
        var payload = JsonSerializer.Deserialize<
            CanonicalDormantTreasuryArchivePayload>(envelope.PayloadJson)!;
        var json = JsonSerializer.Serialize(edit(payload));
        return JsonSerializer.Serialize(envelope with {
            PayloadJson = json,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json)))
        });
    }
}

public sealed class GaVs101DormantTreasuryPersistenceTests
{
    [Fact]
    public void OptionalTreasuryReservationHasNewHashWithoutChangingLegacySnapshot()
    {
        var session = GaVs101DormantTreasuryFixture.StartSession();
        var beforeSave = session.ExportSaveJson();
        var baseline = session.Game.Snapshot;
        var hasher = new CanonicalWorldStateHasher();
        var before = hasher.Compute(baseline);
        Assert.Null(baseline.TreasuryReservation);
        Assert.NotEqual(
            CanonicalWorldStateHasher.DormantTreasuryReservationHashFormatVersion,
            before.FormatVersion);

        var withReserve = CanonicalDormantTreasuryPersistenceCodec.Reserve(baseline);
        var after = hasher.Compute(withReserve);
        Assert.NotSame(baseline, withReserve);
        Assert.Equal(baseline.Revision, withReserve.Revision);
        Assert.Equal(baseline.PhysicalEconomy, withReserve.PhysicalEconomy);
        Assert.Equal(baseline.WorldBinding, withReserve.WorldBinding);
        Assert.Equal(
            CanonicalWorldStateHasher.DormantTreasuryReservationHashFormatVersion,
            after.FormatVersion);
        Assert.NotEqual(before.HexDigest, after.HexDigest);
        Assert.Equal(0UL, withReserve.TreasuryReservation!.SpendableBalanceMinorUnits);
        Assert.Equal(0, withReserve.TreasuryReservation.MonetaryReceiptCount);
        Assert.True(withReserve.TreasuryReservation.IsDormant);
        Assert.False(withReserve.TreasuryReservation.AcceptsRealIssuance);
        Assert.False(withReserve.TreasuryReservation.DebitsPopulationAccounts);

        Assert.Equal(before, hasher.Compute(baseline));
        Assert.Equal(before, session.CurrentHash);
        Assert.Equal(beforeSave, session.ExportSaveJson());
    }

    [Fact]
    public void SeparateCanonicalSegmentRoundTripsOnExactBaseline()
    {
        var session = GaVs101DormantTreasuryFixture.StartSession();
        var baseline = session.Game.Snapshot;
        var before = session.ExportSaveJson();
        var encoded = CanonicalDormantTreasuryPersistenceCodec.Export(baseline);
        Assert.Equal(encoded,
            CanonicalDormantTreasuryPersistenceCodec.Export(baseline));
        var restored = CanonicalDormantTreasuryPersistenceCodec.Restore(
            baseline, encoded);
        var expected = CanonicalDormantTreasuryPersistenceCodec.Reserve(baseline);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(expected), hasher.Compute(restored));
        Assert.Equal(expected.TreasuryReservation, restored.TreasuryReservation);
        Assert.Equal(baseline.WorldBinding, restored.WorldBinding);
        Assert.Equal(baseline.Revision, restored.Revision);
        Assert.Equal(0UL, restored.TreasuryReservation!.SpendableBalanceMinorUnits);
        Assert.Equal(0, restored.TreasuryReservation.MonetaryReceiptCount);
        Assert.Equal(baseline.Economy, restored.Economy);
        Assert.Equal(baseline.Population, restored.Population);
        Assert.Equal(before, session.ExportSaveJson());
    }

    [Fact]
    public void ChangesInPhysicalSnapshotRevisionOrWorldRejectOldArchive()
    {
        var session = GaVs101DormantTreasuryFixture.StartSession();
        var baseline = session.Game.Snapshot;
        var archive = CanonicalDormantTreasuryPersistenceCodec.Export(baseline);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                baseline.AdvanceRevision(), archive));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                CanonicalPhysicalTacticalSession.Start(seed: 204013UL)
                    .Game.Snapshot, archive));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                CanonicalDormantTreasuryPersistenceCodec.Reserve(baseline),
                archive));
    }

    [Fact]
    public void RejectTamperedDigestAndSignedButInvalidBalanceOrReceipts()
    {
        var session = GaVs101DormantTreasuryFixture.StartSession();
        var baseline = session.Game.Snapshot;
        var archive = CanonicalDormantTreasuryPersistenceCodec.Export(baseline);
        var raw = JsonSerializer.Deserialize<
            CanonicalDormantTreasuryArchiveEnvelope>(archive)!;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                baseline, JsonSerializer.Serialize(raw with {
                    PayloadSha256Hex = new string('0',64)
                })));
        foreach (var corruption in new Func<
            CanonicalDormantTreasuryArchivePayload,
            CanonicalDormantTreasuryArchivePayload>[] {
                x => x with { SpendableBalanceMinorUnits = 1 },
                x => x with { MonetaryReceiptCount = 1 },
                x => x with { SchemaVersion = 2 },
                x => x with { BaselineWorldRevision = x.BaselineWorldRevision + 1 },
                x => x with { BaselineWorldHashHex = new string('0',64) },
                x => x with { WorldSignatureSha256Hex = new string('F',64) },
                x => x with { ReservedWorldHashHex = new string('0',64) }
            })
        {
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalDormantTreasuryPersistenceCodec.Restore(baseline,
                    GaVs101DormantTreasuryFixture.RewritePayload(
                        archive, corruption)));
        }
    }

    [Fact]
    public void RejectUnknownFieldsOversizedArchivesAndUnsupportedFormat()
    {
        var baseline = GaVs101DormantTreasuryFixture.StartSession().Game.Snapshot;
        var original = CanonicalDormantTreasuryPersistenceCodec.Export(baseline);
        var env = JsonSerializer.Deserialize<
            CanonicalDormantTreasuryArchiveEnvelope>(original)!;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                baseline, JsonSerializer.Serialize(env with {
                    EnvelopeVersion = 2
                })));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                baseline, original.Replace(
                    "\"EnvelopeVersion\":1",
                    "\"UnknownIssuanceAuthority\":true,\"EnvelopeVersion\":1")));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Restore(
                baseline, new string('x',
                    CanonicalDormantTreasuryPersistenceCodec.MaximumArchiveUtf8Bytes + 1)));
    }

    [Fact]
    public void ReservationIsPreservedByImmutableWorldStateChangesAndRevision()
    {
        var baseline = GaVs101DormantTreasuryFixture.StartSession().Game.Snapshot;
        var w = CanonicalDormantTreasuryPersistenceCodec.Reserve(baseline);
        var changed = w.WithPhysicalEconomy(w.PhysicalEconomy!)
            .WithCivilizations(w.Civilizations)
            .WithTerritory(w.Territory)
            .WithDiplomacy(w.Diplomacy)
            .WithEconomy(w.Economy)
            .WithPopulation(w.Population)
            .WithWarfare(w.Warfare);
        Assert.Equal(w.TreasuryReservation, changed.TreasuryReservation);
        Assert.Equal(w.TreasuryReservation,
            w.AdvanceRevision().TreasuryReservation);
        Assert.Equal(w, w.WithTreasuryReservation(w.TreasuryReservation!));
        Assert.NotEqual(
            new CanonicalWorldStateHasher().Compute(w),
            new CanonicalWorldStateHasher().Compute(w.AdvanceRevision()));
    }

    [Fact]
    public void NoTreasuryCanBeAllocatedToUnboundOrNonphysicalWorld()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryPersistenceCodec.Reserve(
                WorldState.CreateInitial()));
        var baseline = GaVs101DormantTreasuryFixture.StartSession().Game.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalTreasuryReservationRuntimeState.RestoreDormant(
                baseline.WorldBinding!, 1,
                baseline.WorldBinding!.WorldSignatureSha256Hex, 500, 0));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalTreasuryReservationRuntimeState.RestoreDormant(
                baseline.WorldBinding!, 1,
                baseline.WorldBinding!.WorldSignatureSha256Hex, 0, 1));
    }

    [Fact]
    public void ReservationCannotAlterLegacyFoodCashOrAdmitRealRevenueFromVs100()
    {
        var session = GaVs101DormantTreasuryFixture.StartSession();
        var before = session.ExportSaveJson();
        var cash = session.FoodCash!.Accounts.ToArray();
        var state = session.Game.Snapshot;
        var withTreasury = CanonicalDormantTreasuryPersistenceCodec.Reserve(state);
        var save = CanonicalDormantTreasuryPersistenceCodec.Export(state);
        var restored = CanonicalDormantTreasuryPersistenceCodec.Restore(state, save);
        Assert.Equal(0UL, withTreasury.TreasuryReservation!.SpendableBalanceMinorUnits);
        Assert.Equal(0UL, restored.TreasuryReservation!.SpendableBalanceMinorUnits);
        Assert.Equal(before, session.ExportSaveJson());
        Assert.Equal(cash, session.FoodCash.Accounts);
    }
}
