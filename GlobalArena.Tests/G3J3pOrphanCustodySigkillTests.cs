using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3pSupervisedOrphanCustodyTests
{
    private static string? CustodyCrashRoot =>
        Environment.GetEnvironmentVariable("GA_J3P_ROOT");

    [Fact]
    public void J3pPrepareCustodySigkillFixture()
    {
        var root = CustodyCrashRoot;
        if (root is null) return;
        var sourceName = ".ga-j3m-quarantine-" + Guid.NewGuid().ToString("N");
        var caseId = "case-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(root, ".ga-j3p-vault"));
        var source = Path.Combine(root, sourceName);
        Directory.CreateDirectory(Path.Combine(source, "replay"));
        File.WriteAllText(Path.Combine(source, "verified-version.zip"),
            "a quarantined archive, not an admitted game");
        File.WriteAllText(Path.Combine(source, "replay",
            "full-v9-head.gahead"), "forensic data only");
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(Path.Combine(root, "operator-public.pem"),
            signer.ExportSubjectPublicKeyInfoPem());
        using var custody = new EconomicV9SupervisedOrphanCustody(
            root, ".ga-j3p-vault",
            signer.ExportSubjectPublicKeyInfoPem(), new Isolation(),
            new EconomicV9LocalOrphanCustodyAuditSink(
                Path.Combine(root, "audit.jsonl")));
        var plan = custody.Plan(caseId, "j3p-game", 7, sourceName);
        File.WriteAllText(Path.Combine(root, "approval.json"),
            JsonSerializer.Serialize(Signed(plan, signer)));
    }

    [Fact]
    public void J3pCustodySigkillWorker()
    {
        var root = CustodyCrashRoot;
        if (root is null) return;
        var at = Enum.Parse<EconomicV9OrphanCustodyPhase>(
            Environment.GetEnvironmentVariable("GA_J3P_PHASE") ??
            throw new InvalidOperationException("Custody kill phase missing."));
        var approval = JsonSerializer.Deserialize<EconomicV9OrphanCustodyApproval>(
            File.ReadAllText(Path.Combine(root, "approval.json")))!;
        using var custody = new EconomicV9SupervisedOrphanCustody(
            root, ".ga-j3p-vault",
            File.ReadAllText(Path.Combine(root, "operator-public.pem")),
            new Isolation(),
            new EconomicV9LocalOrphanCustodyAuditSink(
                Path.Combine(root, "audit.jsonl")));
        custody.ArchiveOnlyObserved(approval, new Probe(phase =>
        {
            if (phase != at) return;
            using var signal = new FileStream(
                Path.Combine(root, "worker-ready"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None);
            signal.Write(Encoding.UTF8.GetBytes(phase.ToString()));
            signal.Flush(flushToDisk: true);
            Thread.Sleep(Timeout.Infinite);
        }));
        throw new InvalidOperationException(
            "Custody SIGKILL worker unexpectedly returned.");
    }

    [Fact]
    public void J3pRestartInspectsAndReconcilesCustodyAfterSigkill()
    {
        var root = CustodyCrashRoot;
        if (root is null) return;
        var at = Enum.Parse<EconomicV9OrphanCustodyPhase>(
            Environment.GetEnvironmentVariable("GA_J3P_PHASE") ??
            throw new InvalidOperationException("Custody kill phase missing."));
        Assert.Equal(at.ToString(), File.ReadAllText(
            Path.Combine(root, "worker-ready")));
        var approval = JsonSerializer.Deserialize<EconomicV9OrphanCustodyApproval>(
            File.ReadAllText(Path.Combine(root, "approval.json")))!;
        var source = Path.Combine(root, approval.Plan.QuarantineName);
        var archived = Path.Combine(root, ".ga-j3p-vault",
            approval.Plan.CaseId);
        var moved = at == EconomicV9OrphanCustodyPhase.DirectoryMoved;
        Assert.Equal(!moved, Directory.Exists(source));
        Assert.Equal(moved, Directory.Exists(archived));

        var lines = File.ReadAllLines(Path.Combine(root, "audit.jsonl"));
        Assert.Single(lines);
        Assert.Equal("Intent",
            JsonSerializer.Deserialize<EconomicV9OrphanCustodyAuditEntry>(
                lines[0])!.Event);

        using var custody = new EconomicV9SupervisedOrphanCustody(
            root, ".ga-j3p-vault",
            File.ReadAllText(Path.Combine(root, "operator-public.pem")),
            new Isolation(),
            new EconomicV9LocalOrphanCustodyAuditSink(
                Path.Combine(root, "audit.jsonl")));
        var outcome = custody.ArchiveOnly(approval);
        Assert.Equal(
            moved ? EconomicV9OrphanCustodyOutcome.ReconciledAfterInterruption
                  : EconomicV9OrphanCustodyOutcome.Archived, outcome);
        Assert.False(Directory.Exists(source));
        Assert.True(Directory.Exists(archived));
        Assert.True(File.Exists(Path.Combine(archived,
            "verified-version.zip")));
        var audit = File.ReadAllLines(Path.Combine(root, "audit.jsonl"));
        Assert.Equal(2 + (moved ? 0 : 1), audit.Length);
        Assert.Equal(moved ? "Reconciled" : "Completed",
            JsonSerializer.Deserialize<EconomicV9OrphanCustodyAuditEntry>(
                audit[^1])!.Event);
        Assert.False(Directory.Exists(Path.Combine(root, "game-save")));
    }
}
