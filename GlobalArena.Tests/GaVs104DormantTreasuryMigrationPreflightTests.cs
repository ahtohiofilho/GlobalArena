using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs104DormantTreasuryMigrationPreflightTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public void RealHistoricalSaveModesPreviewV20WithoutChangingTheirSource(int version)
    {
        var session = CanonicalPhysicalTacticalSession.Start(
            cadenceMode: version >= 10
                ? PhysicalEconomicCadenceMode.AdaptiveThirty
                : PhysicalEconomicCadenceMode.LegacyFixedSix,
            geographyMode: version >= 11
                ? PhysicalTacticalGeographyMode.Canonical01C
                : PhysicalTacticalGeographyMode.LegacyV1,
            marketMode: version >= 12
                ? PhysicalFoodMarketMode.LocalCashSettlement
                : PhysicalFoodMarketMode.LegacyDirectConsumption,
            movementMode: version >= 13
                ? PhysicalWorkerMovementMode.TimedAdjacent
                : PhysicalWorkerMovementMode.LegacyInstant,
            civilizationMode: version >= 14
                ? PhysicalCivilizationIdentityMode.BornWithPopulation
                : PhysicalCivilizationIdentityMode.LegacyUnattributed);
        session.AdvanceMovementTurn();
        var original = session.ExportSaveJson();
        var originalHash = session.CurrentHash;
        var originalEvents = session.Events.ToArray();
        Assert.Equal(version,
            GaVs102IntegratedTreasurySaveFixture.Envelope(original).Version);

        var first = CanonicalDormantTreasuryMigrationPreflight.Inspect(original);
        var second = CanonicalDormantTreasuryMigrationPreflight.Inspect(original);

        Assert.Equal(first, second);
        Assert.Equal(version, first.SourceEnvelopeVersion);
        Assert.Equal(version, first.PhysicalPayloadVersion);
        Assert.Equal(20, first.CandidateEnvelopeVersion);
        Assert.Equal(originalEvents.Length, first.HistoricalActionCount);
        Assert.Equal(originalEvents.Length, first.ActivationAfterActionCount);
        Assert.Equal(first.SourceWorldRevision + 1UL, first.CandidateWorldRevision);
        Assert.Equal(originalHash.HexDigest, first.SourceWorldHashHex);
        Assert.NotEqual(first.SourceWorldHashHex, first.CandidateWorldHashHex);
        Assert.True(first.RequiresExplicitPlayerRegistration);
        Assert.False(first.AlreadyReserved);
        Assert.False(first.WritesAnySave);
        Assert.False(first.PostsRealStateRevenue);
        Assert.False(first.DebitsPopulationMoney);
        Assert.Equal(Digest(original), first.SourceArchiveSha256Hex);

        // The live game, original bytes and earlier journal are unchanged.
        Assert.Equal(original, session.ExportSaveJson());
        Assert.Equal(originalHash, session.CurrentHash);
        Assert.Equal(originalEvents, session.Events);
        Assert.Null(session.Game.Snapshot.TreasuryReservation);
        Assert.Equal(original, CanonicalPhysicalTacticalSession
            .RestoreSaveJson(original).ExportSaveJson());
    }

    [Fact]
    public void ProgressedPhysicalHistoryAndLegacyCashArePreservedInPreview()
    {
        var session = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var original = session.ExportSaveJson();
        var accounts = session.FoodCash!.Accounts.ToArray();
        var receipts = session.FoodCash.Receipts.ToArray();
        var actions = session.Events.ToArray();

        var preview = CanonicalDormantTreasuryMigrationPreflight.Inspect(original);
        Assert.Equal(20, preview.CandidateEnvelopeVersion);
        Assert.Equal(actions.Length, preview.ActivationAfterActionCount);
        Assert.Equal(session.Game.Snapshot.WorldBinding!.WorldSignatureSha256Hex,
            preview.WorldSignatureSha256Hex);
        Assert.NotEqual(preview.SourceArchiveSha256Hex,
            preview.CandidateArchiveSha256Hex);

        var independent = CanonicalPhysicalTacticalSession.RestoreSaveJson(original);
        independent.EnableDormantTreasuryReservation();
        var actualV20 = independent.ExportSaveJson();
        Assert.Equal(Digest(actualV20), preview.CandidateArchiveSha256Hex);
        Assert.Equal(actions, independent.Events);
        Assert.Equal(accounts, independent.FoodCash!.Accounts);
        Assert.Equal(receipts, independent.FoodCash.Receipts);
        Assert.Equal(original, session.ExportSaveJson());
        Assert.Equal(accounts, session.FoodCash.Accounts);
        Assert.Equal(receipts, session.FoodCash.Receipts);
        Assert.Null(session.Game.Snapshot.TreasuryReservation);
    }

    [Fact]
    public void ExistingV20IsInspectedIdempotentlyWithoutSecondActivation()
    {
        var session = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        session.EnableDormantTreasuryReservation();
        session.AdvanceMovementTurn();
        var original = session.ExportSaveJson();
        var originalHash = session.CurrentHash;
        var marker = GaVs102IntegratedTreasurySaveFixture.Payload(original)
            .TreasuryReservation!.ActivatedAfterActionCount;

        var preview = CanonicalDormantTreasuryMigrationPreflight.Inspect(original);

        Assert.True(preview.AlreadyReserved);
        Assert.False(preview.RequiresExplicitPlayerRegistration);
        Assert.Equal(20, preview.SourceEnvelopeVersion);
        Assert.Equal(20, preview.CandidateEnvelopeVersion);
        Assert.Equal(marker, preview.ActivationAfterActionCount);
        Assert.Equal(preview.SourceWorldRevision, preview.CandidateWorldRevision);
        Assert.Equal(preview.SourceWorldHashHex, preview.CandidateWorldHashHex);
        Assert.Equal(preview.SourceArchiveSha256Hex, preview.CandidateArchiveSha256Hex);
        Assert.Equal(original, session.ExportSaveJson());
        Assert.Equal(originalHash, session.CurrentHash);
        Assert.Equal(0UL, session.Game.Snapshot.TreasuryReservation!.SpendableBalanceMinorUnits);
        Assert.Equal(0, session.Game.Snapshot.TreasuryReservation.MonetaryReceiptCount);
    }

    [Fact]
    public void RehashedForgeryAndMalformedArchivesCannotProduceEvidence()
    {
        var session = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var original = session.ExportSaveJson();
        var invalidDigest = GaVs102IntegratedTreasurySaveFixture.Envelope(original)
            with { PayloadSha256Hex = new string('0', 64) };
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalDormantTreasuryMigrationPreflight.Inspect(
                System.Text.Json.JsonSerializer.Serialize(invalidDigest)));

        session.EnableDormantTreasuryReservation();
        var v20 = session.ExportSaveJson();
        var illicitBalance = GaVs102IntegratedTreasurySaveFixture.RewritePayload(
            v20, row => row with {
                TreasuryReservation = row.TreasuryReservation! with {
                    SpendableBalanceMinorUnits = 10UL
                }
            });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalDormantTreasuryMigrationPreflight.Inspect(illicitBalance));
        var illicitMarker = GaVs102IntegratedTreasurySaveFixture.RewritePayload(
            v20, row => row with {
                TreasuryReservation = row.TreasuryReservation! with {
                    ActivatedAfterActionCount = row.Actions.Length + 1
                }
            });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalDormantTreasuryMigrationPreflight.Inspect(illicitMarker));
        Assert.Equal(v20, session.ExportSaveJson());
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
