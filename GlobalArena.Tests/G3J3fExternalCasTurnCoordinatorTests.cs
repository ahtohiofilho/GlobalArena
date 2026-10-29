using System.Security.Cryptography;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    // Test-only linearizable authority. Production requires a genuinely
    // independent durable CAS implementation, not this in-memory fixture.
    private sealed class J3fTestTrustedStore : IEconomicV9TrustedAnchorStore
    {
        private readonly object _gate = new();
        private EconomicV9ExternalAnchorSnapshot _snapshot;

        public bool RejectPrepare { get; set; }
        public bool RejectFinalize { get; set; }
        public bool ThrowAfterDurablePrepare { get; set; }
        public int Prepares { get; private set; }
        public int Finalizes { get; private set; }

        public J3fTestTrustedStore(EconomicV9ExternalAnchor initial)
        {
            _snapshot = new EconomicV9ExternalAnchorSnapshot(initial);
        }

        public EconomicV9ExternalAnchorSnapshot ReadSnapshot()
        {
            lock (_gate) return _snapshot;
        }

        public bool TryPrepare(
            EconomicV9ExternalAnchorSnapshot expected,
            EconomicV9PreparedExternalAdvance requested)
        {
            lock (_gate)
            {
                if (RejectPrepare || _snapshot != expected ||
                    _snapshot.Prepared is not null ||
                    requested.Before != expected.Confirmed)
                    return false;
                _snapshot = new EconomicV9ExternalAnchorSnapshot(
                    expected.Confirmed, requested);
                Prepares++;
                if (ThrowAfterDurablePrepare)
                    throw new IOException(
                        "Simulated lost prepare acknowledgment after durable CAS.");
                return true;
            }
        }

        public bool TryFinalize(EconomicV9PreparedExternalAdvance expected)
        {
            lock (_gate)
            {
                if (RejectFinalize || _snapshot.Prepared != expected)
                    return false;
                _snapshot = new EconomicV9ExternalAnchorSnapshot(expected.After);
                Finalizes++;
                return true;
            }
        }
    }

    private static EconomicV9ExternalAnchor J3fInitialAnchor(
        EconomicEcologicalCheckpoint checkpoint) =>
        new(checkpoint.Sha256Hex, new string('0', 64), 0UL, 0UL);

    [Fact]
    public void J3fMixedAndEcologicalCommitsAreExternallyConfirmedInOrder()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, initial, generated, new HostAuthority());
        var authority = new J3fTestTrustedStore(J3fInitialAnchor(initial));
        var coordinator = new EconomicV9ExternalCasTurnCoordinator(
            generated, authority);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3f-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial))
            {
                var mixed = coordinator.CommitMixed(
                    host, Context(), certificate, Demand(), Holds(), seal);
                Assert.Equal(mixed.ProposedCheckpointSha256Hex,
                    authority.ReadSnapshot().Confirmed.CheckpointSha256Hex);
                Assert.Null(authority.ReadSnapshot().Prepared);
                Assert.Equal(host.AcceptedJournalSha256Hex,
                    authority.ReadSnapshot().Confirmed.JournalSha256Hex);
                Assert.Equal(1UL, host.CommitSequence);

                var ecological = coordinator.CommitEcological(
                    host, 2UL, Demand(), Holds());
                Assert.Equal(ecological.ProposedNext.Sha256Hex,
                    authority.ReadSnapshot().Confirmed.CheckpointSha256Hex);
                Assert.Equal(host.AcceptedJournalSha256Hex,
                    authority.ReadSnapshot().Confirmed.JournalSha256Hex);
                Assert.Equal(2UL, authority.ReadSnapshot().Confirmed.Sequence);
                Assert.Equal(2UL, authority.ReadSnapshot().Confirmed.Turn);
                Assert.Equal(2, authority.Prepares);
                Assert.Equal(2, authority.Finalizes);
                Assert.Null(authority.ReadSnapshot().Prepared);
            }
            using var restored =
                EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
                    folder, generated, authority.ReadSnapshot(),
                    historicalVerifier: seal);
            Assert.Equal(2UL, restored.CommitSequence);
            Assert.Equal(2UL, restored.LastAppliedTurn);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3fRejectedPrepareCannotPublishAnyLocalFiles()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var authority = new J3fTestTrustedStore(J3fInitialAnchor(initial))
        {
            RejectPrepare = true
        };
        var coordinator = new EconomicV9ExternalCasTurnCoordinator(
            generated, authority);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3f-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial);
            var headPath = Path.Combine(folder, "full-v9-head.gahead");
            var originalHead = File.ReadAllBytes(headPath);
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.CommitEcological(host, 1UL, Demand(), Holds()));
            Assert.Equal(originalHead, File.ReadAllBytes(headPath));
            Assert.Equal(0UL, host.CommitSequence);
            Assert.Empty(Directory.GetFiles(folder, "*.gajournal"));
            Assert.Empty(Directory.GetFiles(folder, "*.gamixed"));
            Assert.Null(authority.ReadSnapshot().Prepared);
            Assert.Equal(0, authority.Prepares);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3fLostPrepareAcknowledgmentStaysBlockedWithoutPublication()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var authority = new J3fTestTrustedStore(J3fInitialAnchor(initial))
        {
            ThrowAfterDurablePrepare = true
        };
        var coordinator = new EconomicV9ExternalCasTurnCoordinator(
            generated, authority);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3f-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial))
            {
                Assert.Throws<IOException>(() =>
                    coordinator.CommitEcological(
                        host, 1UL, Demand(), Holds()));
                Assert.Equal(0UL, host.CommitSequence);
                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.CommitEcological(
                        host, 1UL, Demand(), Holds()));
            }
            var pending = authority.ReadSnapshot();
            Assert.NotNull(pending.Prepared);
            Assert.Equal(initial.Sha256Hex,
                pending.Confirmed.CheckpointSha256Hex);
            Assert.Equal(
                EconomicV9ExternalReconciliationState.PreparedWithoutLocalPublication,
                EconomicCompleteV9DurableSaveHost.InspectExternalReconciliation(
                    folder, generated, pending).State);
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.FinalizeRecoveredPublished(folder));
            Assert.Equal(0, authority.Finalizes);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3fFailedFinalizeRequiresFullReplayAndExternalRecoveryCas()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, initial, generated, new HostAuthority());
        var authority = new J3fTestTrustedStore(J3fInitialAnchor(initial))
        {
            RejectFinalize = true
        };
        var coordinator = new EconomicV9ExternalCasTurnCoordinator(
            generated, authority);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3f-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.CommitMixed(host, Context(),
                        certificate, Demand(), Holds(), seal));
                Assert.Equal(1UL, host.CommitSequence);
                Assert.NotNull(authority.ReadSnapshot().Prepared);
                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.CommitEcological(host, 2UL, Demand(), Holds()));
            }
            var pending = authority.ReadSnapshot();
            Assert.NotNull(pending.Prepared);
            Assert.Equal(
                EconomicV9ExternalReconciliationState.LocalPublicationAwaitingExternalConfirmation,
                EconomicCompleteV9DurableSaveHost.InspectExternalReconciliation(
                    folder, generated, pending, historicalVerifier: seal).State);
            Assert.Throws<InvalidOperationException>(() =>
                EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
                    folder, generated, pending, historicalVerifier: seal));
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.FinalizeRecoveredPublished(
                    folder, historicalVerifier: seal));
            authority.RejectFinalize = false;
            var confirmed = new EconomicV9ExternalCasTurnCoordinator(
                generated, authority).FinalizeRecoveredPublished(
                    folder, historicalVerifier: seal);
            Assert.Null(confirmed.Prepared);
            Assert.Equal(pending.Prepared!.After, confirmed.Confirmed);
            Assert.Equal(1, authority.Finalizes);
            using var hostRecovered =
                EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
                    folder, generated, confirmed, historicalVerifier: seal);
            Assert.Equal(1UL, hostRecovered.CommitSequence);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void J3fStaleExternalAuthorityAndBadSignatureDoNotPrepare()
    {
        var generated = Generate();
        var initial = Initial(generated);
        var seal = Seal(generated);
        var certificate = Issue(seal, initial, generated, new HostAuthority());
        var wrongAnchor = new EconomicV9ExternalAnchor(
            new string('F', 64), new string('0', 64), 0UL, 0UL);
        var stale = new J3fTestTrustedStore(wrongAnchor);
        var coordinator = new EconomicV9ExternalCasTurnCoordinator(
            generated, stale);
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-j3f-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var host = EconomicCompleteV9DurableSaveHost.Create(
                folder, generated, initial);
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.CommitMixed(host, Context(),
                    certificate, Demand(), Holds(), seal));
            Assert.Equal(0, stale.Prepares);

            var validAuthority = new J3fTestTrustedStore(
                J3fInitialAnchor(initial));
            var authenticated = new EconomicV9ExternalCasTurnCoordinator(
                generated, validAuthority);
            var alteredCertificate = (byte[])certificate.Clone();
            alteredCertificate[alteredCertificate.Length / 2] ^= 1;
            Assert.ThrowsAny<Exception>(() =>
                authenticated.CommitMixed(host, Context(),
                    alteredCertificate, Demand(), Holds(), seal));
            Assert.Equal(0, validAuthority.Prepares);
            Assert.Equal(0UL, host.CommitSequence);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
