using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3pSupervisedOrphanCustodyTests
{
    private sealed class Isolation : IEconomicV9FormerWriterIsolationVerifier
    {
        public bool Fenced { get; set; } = true;
        public int Calls { get; private set; }
        public int DenyAtCall { get; set; }
        public bool IsIsolated(string gameId, long formerWriterEpoch)
        {
            Assert.Equal("j3p-game", gameId);
            Assert.Equal(7L, formerWriterEpoch);
            Calls++;
            return Fenced && Calls != DenyAtCall;
        }
    }

    private sealed class FailingAudit : IEconomicV9OrphanCustodyAuditSink
    {
        public string? FailEvent { get; set; }
        public List<EconomicV9OrphanCustodyAuditEntry> Entries { get; } = [];
        public void Append(EconomicV9OrphanCustodyAuditEntry entry)
        {
            if (entry.Event == FailEvent)
                throw new IOException("Injected audit storage failure.");
            Entries.Add(entry);
        }
    }

    private sealed class Probe : IEconomicV9OrphanCustodyProbe
    {
        private readonly Action<EconomicV9OrphanCustodyPhase> _on;
        public Probe(Action<EconomicV9OrphanCustodyPhase> on) => _on = on;
        public void OnPhase(EconomicV9OrphanCustodyPhase phase) => _on(phase);
    }

    private static (string Root, string Name, string CaseId) Fixture()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3p-" + Guid.NewGuid().ToString("N"));
        var name = ".ga-j3m-quarantine-" + Guid.NewGuid().ToString("N");
        var caseId = "case-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(root, ".ga-j3p-vault"));
        var source = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(source, "replay"));
        File.WriteAllText(Path.Combine(source, "verified-version.zip"),
            "fixture: this is NOT a valid gameplay archive");
        File.WriteAllText(Path.Combine(source, "replay",
            "full-v9-head.gahead"), "not a valid save head");
        return (root, name, caseId);
    }

    private static EconomicV9OrphanCustodyApproval Signed(
        EconomicV9OrphanCustodyPlan plan, ECDsa key,
        long? issued = null, long? expires = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var unsigned = new EconomicV9OrphanCustodyApproval(
            plan, "operator-incident-17", issued ?? now - 1,
            expires ?? now + 120, "");
        return unsigned with
        {
            SignatureBase64 = Convert.ToBase64String(key.SignData(
                EconomicV9SupervisedOrphanCustody.EncodeApproval(unsigned),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence))
        };
    }

    [Fact]
    public void J3pSignedOperatorArchiveAndSynchronousAuditAndIdempotentReconciliation()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var audit = Path.Combine(root, "custody.jsonl");
            var isolation = new Isolation();
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                isolation, new EconomicV9LocalOrphanCustodyAuditSink(audit));
            var plan = service.Plan(caseId, "j3p-game", 7, name);
            var authorization = Signed(plan, signer);

            Assert.Equal(EconomicV9OrphanCustodyOutcome.Archived,
                service.ArchiveOnly(authorization));
            Assert.False(Directory.Exists(Path.Combine(root, name)));
            Assert.True(File.Exists(Path.Combine(root,
                ".ga-j3p-vault", caseId, "verified-version.zip")));
            var lines = File.ReadAllLines(audit);
            Assert.Equal(2, lines.Length);
            Assert.Equal("Intent",
                JsonSerializer.Deserialize<EconomicV9OrphanCustodyAuditEntry>(
                    lines[0])!.Event);
            Assert.Equal("Completed",
                JsonSerializer.Deserialize<EconomicV9OrphanCustodyAuditEntry>(
                    lines[1])!.Event);
            Assert.Equal(EconomicV9OrphanCustodyOutcome.ReconciledAfterInterruption,
                service.ArchiveOnly(authorization));
            Assert.Equal("Reconciled",
                JsonSerializer.Deserialize<EconomicV9OrphanCustodyAuditEntry>(
                    File.ReadAllLines(audit)[2])!.Event);
            Assert.True(isolation.Calls >= 5);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3pRejectsForgedExpiredMutatedAndUnfencedApproval()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var isolation = new Isolation { Fenced = false };
            var audit = new FailingAudit();
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                isolation, audit);
            var plan = service.Plan(caseId, "j3p-game", 7, name);
            Assert.Throws<InvalidOperationException>(() =>
                service.ArchiveOnly(Signed(plan, other)));
            Assert.Throws<InvalidOperationException>(() =>
                service.ArchiveOnly(Signed(plan, signer,
                    issued: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1000,
                    expires: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 900)));
            Assert.Throws<InvalidOperationException>(() =>
                service.ArchiveOnly(Signed(plan, signer)));
            Assert.Empty(audit.Entries);
            isolation.Fenced = true;
            File.AppendAllText(Path.Combine(root, name,
                "verified-version.zip"), "modified after operator approval");
            Assert.Throws<InvalidDataException>(() =>
                service.ArchiveOnly(Signed(plan, signer)));
            Assert.True(Directory.Exists(Path.Combine(root, name)));
            Assert.False(Directory.Exists(Path.Combine(
                root, ".ga-j3p-vault", caseId)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3pAuditFailureAndInterruptedRenameRequireExplicitReconciliation()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var isolation = new Isolation();
            var audit = new FailingAudit { FailEvent = "Intent" };
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                isolation, audit);
            var approval = Signed(
                service.Plan(caseId, "j3p-game", 7, name), signer);
            Assert.Throws<IOException>(() => service.ArchiveOnly(approval));
            Assert.True(Directory.Exists(Path.Combine(root, name)));

            audit.FailEvent = "Completed";
            Assert.Throws<IOException>(() => service.ArchiveOnly(approval));
            Assert.False(Directory.Exists(Path.Combine(root, name)));
            Assert.True(Directory.Exists(Path.Combine(root,
                ".ga-j3p-vault", caseId)));
            Assert.Single(audit.Entries);
            Assert.Equal("Intent", audit.Entries[0].Event);

            audit.FailEvent = null;
            Assert.Equal(EconomicV9OrphanCustodyOutcome.ReconciledAfterInterruption,
                service.ArchiveOnly(approval));
            Assert.Equal("Reconciled", audit.Entries[1].Event);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3pFencingRevocationBetweenIntentAndRenameFailsClosed()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var isolation = new Isolation { DenyAtCall = 3 };
            var audit = new FailingAudit();
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                isolation, audit);
            var approval = Signed(
                service.Plan(caseId, "j3p-game", 7, name), signer);
            Assert.Throws<InvalidOperationException>(() => service.ArchiveOnly(approval));
            Assert.True(Directory.Exists(Path.Combine(root, name)));
            Assert.Equal("Intent", Assert.Single(audit.Entries).Event);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3pPostIntentInterruptionPreservesSourceAndRetry()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var audit = new FailingAudit();
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                new Isolation(), audit);
            var approval = Signed(service.Plan(caseId, "j3p-game", 7, name), signer);
            Assert.Throws<IOException>(() =>
                service.ArchiveOnlyObserved(approval, new Probe(phase =>
                {
                    if (phase == EconomicV9OrphanCustodyPhase.IntentPersisted)
                        throw new IOException("Simulated crash after fsynced intent.");
                })));
            Assert.True(Directory.Exists(Path.Combine(root, name)));
            Assert.False(Directory.Exists(Path.Combine(root,
                ".ga-j3p-vault", caseId)));
            Assert.Equal(EconomicV9OrphanCustodyOutcome.Archived,
                service.ArchiveOnly(approval));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3pRefusesDuplicateSourceAndVaultOrUnsafeSourceAlias()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                new Isolation(), new FailingAudit());
            var plan = service.Plan(caseId, "j3p-game", 7, name);
            var approval = Signed(plan, signer);
            Directory.CreateDirectory(Path.Combine(root, ".ga-j3p-vault", caseId));
            Assert.Throws<InvalidOperationException>(() =>
                service.ArchiveOnly(approval));
            Assert.True(Directory.Exists(Path.Combine(root, name)));
            Assert.Throws<ArgumentException>(() =>
                service.Plan(caseId, "j3p-game", 7, "../unsafe"));
            Assert.Throws<ArgumentException>(() =>
                service.Plan(caseId, "j3p-game", 7, "source"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3pSymlinkInsideQuarantineIsRejectedBeforeApproval()
    {
        if (OperatingSystem.IsWindows()) return;
        var (root, name, caseId) = Fixture();
        try
        {
            var external = Path.Combine(root, "outside");
            Directory.CreateDirectory(external);
            File.WriteAllText(Path.Combine(external, "secret"), "must not archive");
            File.CreateSymbolicLink(Path.Combine(root, name, "escape"), 
                Path.Combine(external, "secret"));
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var service = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", signer.ExportSubjectPublicKeyInfoPem(),
                new Isolation(), new FailingAudit());
            Assert.Throws<IOException>(() =>
                service.Plan(caseId, "j3p-game", 7, name));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
