using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// GA-VS51: KILL A REAL CHILD PROCESS while it holds the VS50 file gate at
/// the last stage-flush boundary or after File.Replace. Parent then checks
/// the exact persisted state with a freshly reconstructed VS46-49 snapshot.
///
/// The test probes OS process-death recovery, not power-loss durability,
/// directory fsync, storage hardware failure or production-game saves.
/// </summary>
public static class GaVs51AbruptCheckpointRecoveryHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const ulong Turn = 51;
    private const ulong Network = 9;
    private const string WorldHash = "GA-VS51-TRUSTED-SYNTHETIC-WORLD";

    private sealed class ValueCurve : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong raw) =>
            new(100, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong raw) =>
            new((BigInteger)100 * raw, Unit);
    }

    private sealed class Authority : IExperimentalCashSettlementAuthority
    {
        public bool CanSettle(ExperimentalCashAuthorityDecision decision,
            ExperimentalSelectedCashProposal selected) =>
            decision.Approves && decision.ActorId == "GA-VS51-LAB-INSTITUTION";
    }

    private sealed class FrozenAtBoundary(
        string markerPath,
        ExperimentalCheckpointPublicationBoundary stopAt)
        : IExperimentalCheckpointPublicationFault
    {
        public void OnBoundary(ExperimentalCheckpointPublicationBoundary where)
        {
            if (where != stopAt) return;
            // Marker means the storage API has reached the specified
            // publication boundary AND the .gate is still held.
            File.WriteAllText(markerPath, where.ToString());
            Thread.Sleep(Timeout.Infinite);
            throw new UnreachableException(
                "The parent must kill the deliberately blocked child.");
        }
    }

    private sealed class Fixture
    {
        public ExperimentalCashAuthorizationSnapshot Snapshot { get; }
        public ExperimentalSelectedCashProposal Selection { get; }
        public ExperimentalCashAuthorityDecision Decision { get; }
        public IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> Opening { get; }
        public IExperimentalCashSettlementAuthority Permission { get; } =
            new Authority();

        public Fixture()
        {
            var food = new CommodityId(7);
            var source = new EconomicPointId(1);
            var buyer = new EconomicPointId(2);
            var supply = new EconomicSupplyFlow(
                source, food, EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(buyer, food, Unit);
            var flows = new EconomicProductionDemandResult(
                new[] { supply }, new[] { demand });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(food, "food", 1)
            });
            var technology = new ExperimentalFreightTechnologyPricing(
                101, 7, 51, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            var option = ExperimentalSharedFreightOption.Synthetic(
                10, new StrategicEconomicRouteKey(source, buyer),
                technology, 1_000_000, new[] { corridor });
            var lane = new ExperimentalSimultaneousFreightLane(
                supply, demand, new[] { option });
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                1, 1_000_000);
            var budget = new ExperimentalBuyerCashBudget(
                buyer, new FoodTransportCostFraction(30, 1),
                new FoodTransportCostFraction(5, 1));
            var price = new ExperimentalBuyerCommodityPrice(
                buyer, food, new FoodTransportCostFraction(10, 1));
            var allocation = new ExperimentalSimultaneousFreightReference()
                .Compare(flows, catalog,
                    new[] { new EconomicTurnCommodityValueOffer(
                        buyer, food, new ValueCurve()) },
                    new[] { new EconomicTurnSourceReservation(
                        source, food, supply.Activity, 0) },
                    new[] { lane }, congestion, Unit,
                    maxExactConfigurations: 8,
                    cashSnapshot: new ExperimentalBuyerCashSnapshot(
                        new[] { budget }, new[] { price })).Exact!;
            Snapshot = new ExperimentalCashAuthorizationSnapshot(
                Turn, WorldHash, Network, flows, catalog,
                new[] { lane }, new[] { budget }, new[] { price }, congestion);
            Selection = new ExperimentalAuditedCashCandidateAuthorization()
                .Select(Snapshot,
                    new[] { new ExperimentalAuditedCandidate(1, allocation) },
                    new ExperimentalAuditedSelectionPolicy(
                        "GA-VS51-AUTHORIZED-LAB-SELECT", 1,
                        ExperimentalAuditedSelectionRule
                            .MaxDeliveredRawVolumeThenLowestFreight),
                    Turn, WorldHash, Network);
            Decision = new ExperimentalCashAuthorityDecision(
                "GA-VS51-LAB-INSTITUTION", "GA-VS51-DECISION", true,
                Turn, Selection.SnapshotDigest, Selection.CandidateDigest,
                Selection.CandidateId, Selection.Policy.PolicyId,
                Selection.Policy.PolicyVersion);
            Opening = Selection.Audit.Opening;
        }

        public ExperimentalRecoverableCashCheckpointSandbox Create(string path) =>
            ExperimentalRecoverableCashCheckpointSandbox.Create(
                path, Snapshot, Selection, Opening, Permission);

        public ExperimentalRecoverableCashCheckpointSandbox Open(
            string path, IExperimentalCheckpointPublicationFault? fault = null) =>
            ExperimentalRecoverableCashCheckpointSandbox.Open(
                path, Snapshot, Selection, Opening, Permission, Decision, fault);

        public ExperimentalCashSettlementOutcome Commit(
            ExperimentalRecoverableCashCheckpointSandbox checkpoint) =>
            checkpoint.Commit(Decision, Turn, WorldHash, Network, 0);
    }

    /// <summary>
    /// Child starts with a new fixture; its only write is to the private
    /// .ga-vs49-lab.json file supplied by the parent. It blocks at the
    /// requested boundary until externally terminated by Process.Kill.
    /// </summary>
    public static int RunWorker(string[] args)
    {
        if (args.Length != 4 || args[0] != "--vs51-crash-worker")
            return 4;
        try
        {
            var boundary = args[3] switch
            {
                "before" => ExperimentalCheckpointPublicationBoundary
                    .StageFlushedBeforeReplace,
                "after" => ExperimentalCheckpointPublicationBoundary
                    .FileReplacedBeforeReturn,
                _ => throw new ArgumentException("Unknown termination boundary.")
            };
            var fixture = new Fixture();
            var checkpoint = fixture.Open(args[1],
                new FrozenAtBoundary(args[2], boundary));
            _ = fixture.Commit(checkpoint);
            return 5; // The process must not successfully leave the boundary.
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 6;
        }
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS51 crash-recovery harness requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS51-REAL-CHILD-PROCESS-KILL-RECOVERY");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("BOUNDARIES=stage-flushed;file-replaced");
            Console.WriteLine("WARMUPS=1;SAMPLES=3;PERFORMANCE_THRESHOLDS=none");
            foreach (var stage in new[] { "before", "after" })
                Measure(stage);
            Console.WriteLine("RESULT=PASS_GA_VS51_ABRUPT_TERMINATION_RECOVERY");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("RESULT=FAIL_GA_VS51_ABRUPT_TERMINATION_RECOVERY");
            return 1;
        }
#endif
    }

    private static void Measure(string stage)
    {
        var observed = new double[3];
        for (var iteration = -1; iteration < 3; iteration++)
        {
            var folder = Path.Combine(Path.GetTempPath(),
                "ga-vs51-killed-worker", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var checkpointPath = Path.Combine(
                    folder, "cash.ga-vs49-lab.json");
                var markerPath = Path.Combine(folder, "child-ready.marker");
                var fixture = new Fixture();
                _ = fixture.Create(checkpointPath);
                var before = File.ReadAllBytes(checkpointPath);

                var watch = Stopwatch.StartNew();
                KillAtBoundary(checkpointPath, markerPath, stage);
                // The child has been reaped; its OS file handle must have
                // been released even though no C# Dispose ran in the child.
                using (ExperimentalCheckpointProcessGate.Acquire(
                    checkpointPath, TimeSpan.FromSeconds(2)))
                {
                    var expectedMarker = stage == "before"
                        ? nameof(ExperimentalCheckpointPublicationBoundary
                            .StageFlushedBeforeReplace)
                        : nameof(ExperimentalCheckpointPublicationBoundary
                            .FileReplacedBeforeReturn);
                    if (File.ReadAllText(markerPath) != expectedMarker)
                        throw new InvalidDataException(
                            "Child did not stop at the intended publication boundary.");
                }

                // Reconstruct trust inputs in the surviving parent, not
                // by deserializing financial authority out of the checkpoint.
                var restarted = new Fixture();
                var reopened = restarted.Open(checkpointPath);
                var inspected = reopened.Inspect(restarted.Decision);
                if (stage == "before")
                {
                    if (inspected.StateRevision != 0 ||
                        inspected.Journal.Count != 0 ||
                        !before.SequenceEqual(File.ReadAllBytes(checkpointPath)) ||
                        Directory.GetFiles(folder, "*.stage").Length == 0)
                        throw new InvalidDataException(
                            "Pre-publication process death must retain the exact pending state and orphan a nonauthoritative stage.");
                    var first = restarted.Commit(reopened);
                    if (first.WasAlreadyCommitted)
                        throw new InvalidOperationException(
                            "A pending transaction must be committed once after crash.");
                }
                else
                {
                    if (inspected.StateRevision != 1 ||
                        inspected.Journal.Count != 2 ||
                        before.SequenceEqual(File.ReadAllBytes(checkpointPath)))
                        throw new InvalidDataException(
                            "Post-publication process death must restore the fully settled journal.");
                }

                var persistedBytes = File.ReadAllBytes(checkpointPath);
                var replay = restarted.Commit(
                    new Fixture().Open(checkpointPath));
                if (!replay.WasAlreadyCommitted ||
                    replay.Proof.CommittedStateRevision != 1 ||
                    replay.Proof.OpeningMoney.CompareTo(
                        replay.Proof.ClosingMoney) != 0 ||
                    !persistedBytes.SequenceEqual(
                        File.ReadAllBytes(checkpointPath)))
                    throw new InvalidOperationException(
                        "A recovered committed decision must not double debit or rewrite the file.");
                var final = new Fixture().Open(checkpointPath)
                    .Inspect(restarted.Decision);
                var household = ExperimentalCashAuditAccountKey.Household(
                    new EconomicPointId(2));
                if (final.StateRevision != 1 ||
                    final.Journal.Count != 2 ||
                    !final.Accounts.TryGetValue(household, out var balance) ||
                    balance.CompareTo(new FoodTransportCostFraction(18, 1)) != 0)
                    throw new InvalidOperationException(
                        "The recovered household, producer and carrier balances must be auditable and H protected.");
                var elapsed = watch.Elapsed.TotalMilliseconds;
                if (iteration >= 0) observed[iteration] = elapsed;
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        Console.WriteLine($"CASE=kill-{stage};" +
            $"independent_process_killed=PASS;recovered=PASS;" +
            $"duplicate_charge_rejected=PASS;" +
            $"median_kill_recover_replay_ms=" +
                $"{observed.OrderBy(x => x).ElementAt(1):F3}");
    }

    private static void KillAtBoundary(
        string checkpoint, string marker, string boundary)
    {
        var dll = typeof(GaVs51AbruptCheckpointRecoveryHarness).Assembly.Location;
        using var child = new Process();
        child.StartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        child.StartInfo.ArgumentList.Add(dll);
        child.StartInfo.ArgumentList.Add("--vs51-crash-worker");
        child.StartInfo.ArgumentList.Add(checkpoint);
        child.StartInfo.ArgumentList.Add(marker);
        child.StartInfo.ArgumentList.Add(boundary);
        if (!child.Start())
            throw new InvalidOperationException(
                "Could not start an independent crash-test worker.");

        try
        {
            var ready = Stopwatch.StartNew();
            while (!File.Exists(marker) && ready.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (child.HasExited)
                    throw new InvalidOperationException(
                        "Crash-test worker exited before its target boundary, code " +
                        child.ExitCode);
                Thread.Sleep(10);
            }
            if (!File.Exists(marker) || child.HasExited)
                throw new TimeoutException(
                    "Child never reached the requested boundary with the gate held.");
            // An independently live child MUST still hold the sidecar.
            try
            {
                using var unexpected = ExperimentalCheckpointProcessGate.Acquire(
                    checkpoint, TimeSpan.FromMilliseconds(35));
                throw new InvalidOperationException(
                    "The child lost its checkpoint gate while suspended.");
            }
            catch (IOException)
            {
                // Expected FileShare.None gate contention.
            }
            child.Kill(entireProcessTree: true);
            if (!child.WaitForExit(10_000))
                throw new TimeoutException("Killed worker did not exit.");
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit(10_000);
            }
        }
    }
}
