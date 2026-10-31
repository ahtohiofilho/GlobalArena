using System.Numerics;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs49RecoverableCashCheckpointTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const ulong Turn = 49;
    private const ulong Network = 23;
    private const string Hash = "TRUSTED-LAB-STATE-HASH-49";
    private static readonly CommodityId Food = new(7);

    private sealed class FlatValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong raw) =>
            new(100, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong raw) =>
            new((BigInteger)100 * raw, Unit);
    }

    private sealed class Permission(bool allow = true) : IExperimentalCashSettlementAuthority
    {
        public bool CanSettle(
            ExperimentalCashAuthorityDecision decision,
            ExperimentalSelectedCashProposal selected) =>
            allow && decision.ActorId == "EXPLICIT-TEST-INSTITUTION";
    }

    private sealed class Fixture
    {
        public ExperimentalCashAuthorizationSnapshot Snapshot { get; }
        public ExperimentalSelectedCashProposal Selected { get; }
        public ExperimentalCashAuthorityDecision Decision { get; }
        public Dictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> Opening { get; }

        public Fixture(bool extraExistingMoney = true)
        {
            var s1 = new EconomicSupplyFlow(new EconomicPointId(1),
                Food, EconomicActivityKind.Agriculture, Unit);
            var s2 = new EconomicSupplyFlow(new EconomicPointId(2),
                Food, EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(
                new EconomicPointId(3), Food, 2 * Unit);
            var flows = new EconomicProductionDemandResult(
                new[] { s1, s2 }, new[] { demand });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Food, "food", 1)
            });
            var cal = new ExperimentalFreightTechnologyPricing(
                101, 7, 49, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            ExperimentalSimultaneousFreightLane Offer(
                EconomicSupplyFlow supply) => new(supply, demand, new[] {
                    ExperimentalSharedFreightOption.Synthetic(
                        10, new StrategicEconomicRouteKey(
                            supply.EconomicPointId, demand.EconomicPointId),
                        cal, 1_000_000, new[] { corridor })
                });
            var lanes = new[] { Offer(s1), Offer(s2) };
            var budgets = new[] { new ExperimentalBuyerCashBudget(
                demand.EconomicPointId, new FoodTransportCostFraction(107, 1),
                new FoodTransportCostFraction(5, 1)) };
            var prices = new[] { new ExperimentalBuyerCommodityPrice(
                demand.EconomicPointId, Food,
                new FoodTransportCostFraction(10, 1)) };
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                10, 1_000_000);
            var allocation = new ExperimentalSimultaneousFreightReference().Compare(
                flows, catalog,
                new[] { new EconomicTurnCommodityValueOffer(
                    demand.EconomicPointId, Food, new FlatValue()) },
                new[] {
                    new EconomicTurnSourceReservation(
                        s1.EconomicPointId, Food, s1.Activity, 0),
                    new EconomicTurnSourceReservation(
                        s2.EconomicPointId, Food, s2.Activity, 0)
                }, lanes, congestion, Unit,
                maxExactConfigurations: 16,
                cashSnapshot: new ExperimentalBuyerCashSnapshot(
                    budgets, prices)).Exact!;
            Snapshot = new ExperimentalCashAuthorizationSnapshot(
                Turn, Hash, Network, flows, catalog, lanes,
                budgets, prices, congestion);
            Selected = new ExperimentalAuditedCashCandidateAuthorization().Select(
                Snapshot,
                new[] { new ExperimentalAuditedCandidate(1, allocation) },
                new ExperimentalAuditedSelectionPolicy("VS49-INSTITUTION", 1,
                    ExperimentalAuditedSelectionRule
                        .MaxDeliveredRawVolumeThenLowestFreight),
                Turn, Hash, Network);
            Decision = new ExperimentalCashAuthorityDecision(
                "EXPLICIT-TEST-INSTITUTION", "DELIBERATION-VS49",
                true, Turn, Selected.SnapshotDigest, Selected.CandidateDigest,
                Selected.CandidateId,
                Selected.Policy.PolicyId, Selected.Policy.PolicyVersion);
            Opening = Selected.Audit.Opening.ToDictionary(x => x.Key, x => x.Value);
            if (extraExistingMoney)
            {
                Opening[ExperimentalCashAuditAccountKey.Producer(
                    new EconomicPointId(1))] = new FoodTransportCostFraction(7, 1);
                Opening[ExperimentalCashAuditAccountKey.Operator(
                    new EconomicPointId(1), demand.EconomicPointId, 10)] =
                        new FoodTransportCostFraction(9, 1);
            }
        }

        public ExperimentalRecoverableCashCheckpointSandbox Create(string path,
            bool allow = true) =>
            ExperimentalRecoverableCashCheckpointSandbox.Create(
                path, Snapshot, Selected, Opening, new Permission(allow));

        public ExperimentalRecoverableCashCheckpointSandbox Open(string path,
            bool allow = true,
            ExperimentalCashAuthorityDecision? decision = null,
            IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>? opening = null) =>
            ExperimentalRecoverableCashCheckpointSandbox.Open(
                path, Snapshot, Selected, opening ?? Opening,
                new Permission(allow), decision ?? Decision);

        public ExperimentalCashSettlementOutcome Commit(
            ExperimentalRecoverableCashCheckpointSandbox lab,
            ExperimentalCashAuthorityDecision? decision = null,
            ulong revision = 0,
            ulong turn = Turn, string worldHash = Hash, ulong network = Network) =>
            lab.Commit(decision ?? Decision, turn, worldHash, network, revision);
    }

    private sealed class TempFolder : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(
            Path.GetTempPath(), "ga-vs49-checkpoint-tests",
            Guid.NewGuid().ToString("N"));
        public string PathToCheckpoint => Path.Combine(
            DirectoryPath, "economic.ga-vs49-lab.json");
        public TempFolder() => Directory.CreateDirectory(DirectoryPath);
        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private static void Money(long expected, FoodTransportCostFraction actual) =>
        Assert.Equal(0, actual.CompareTo(
            new FoodTransportCostFraction(expected, 1)));

    [Fact]
    public void SaveCloseReopenFreshSnapshotAndReplayNeverDoubleDebit()
    {
        using var files = new TempFolder();
        var first = new Fixture();
        var file = first.Create(files.PathToCheckpoint);
        var pending = file.Inspect(first.Decision);
        Assert.Equal(0UL, pending.StateRevision);
        Assert.Empty(pending.Journal);
        var committed = first.Commit(file);
        Assert.False(committed.WasAlreadyCommitted);
        Money(123, committed.Proof.OpeningMoney);
        Money(123, committed.Proof.ClosingMoney);

        // Simulate application restart: new fixture/snapshot/selection and
        // a newly opened persistent checkpoint object, no shared VS48 state.
        var fresh = new Fixture();
        var reopened = fresh.Open(files.PathToCheckpoint);
        var state = reopened.Inspect(fresh.Decision);
        Assert.Equal(1UL, state.StateRevision);
        Assert.Equal("DELIBERATION-VS49", state.CommittedDecisionId);
        Assert.Equal(4, state.Journal.Count);
        Money(5, state.Accounts[ExperimentalCashAuditAccountKey.Household(
            new EconomicPointId(3))]);
        Money(17, state.Accounts[ExperimentalCashAuditAccountKey.Producer(
            new EconomicPointId(1))]);
        Money(50, state.Accounts[ExperimentalCashAuditAccountKey.Operator(
            new EconomicPointId(1), new EconomicPointId(3), 10)]);
        var beforeBytes = File.ReadAllBytes(files.PathToCheckpoint);
        var replay = fresh.Commit(reopened);
        Assert.True(replay.WasAlreadyCommitted);
        Assert.Equal(committed.Proof, replay.Proof);
        Assert.Equal(beforeBytes, File.ReadAllBytes(files.PathToCheckpoint));
        Assert.Equal(1UL, reopened.Inspect(fresh.Decision).StateRevision);
    }

    [Fact]
    public void OpenPendingSnapshotAfterRestartCanCommitOnce()
    {
        using var files = new TempFolder();
        var original = new Fixture();
        _ = original.Create(files.PathToCheckpoint);
        var restarted = new Fixture();
        var pending = restarted.Open(files.PathToCheckpoint);
        Assert.Equal(0UL, pending.Inspect(restarted.Decision).StateRevision);
        var result = restarted.Commit(pending);
        Assert.False(result.WasAlreadyCommitted);
        Assert.Equal(1UL, pending.Inspect(restarted.Decision).StateRevision);
        Assert.True(restarted.Commit(pending).WasAlreadyCommitted);
    }

    [Fact]
    public void SameDecisionConflictingActorAndSecondDecisionAreRejectedAfterReopen()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        f.Commit(f.Create(files.PathToCheckpoint));
        var opened = new Fixture();
        var sandbox = opened.Open(files.PathToCheckpoint);
        var bytes = File.ReadAllBytes(files.PathToCheckpoint);
        Assert.ThrowsAny<Exception>(() => opened.Commit(sandbox,
            opened.Decision with { ActorId = "FOREIGN-INSTITUTION" }));
        Assert.ThrowsAny<Exception>(() => opened.Commit(sandbox,
            opened.Decision with { DecisionId = "ANOTHER-DECISION" }, revision: 1));
        Assert.Equal(bytes, File.ReadAllBytes(files.PathToCheckpoint));
    }

    [Fact]
    public void StaleTurnWorldNetworkOrCasCannotPublishAnyCheckpoint()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        var sandbox = f.Create(files.PathToCheckpoint);
        var before = File.ReadAllBytes(files.PathToCheckpoint);
        Assert.Throws<InvalidOperationException>(() =>
            f.Commit(sandbox, turn: Turn + 1));
        Assert.Throws<InvalidOperationException>(() =>
            f.Commit(sandbox, worldHash: "WRONG-WORLD"));
        Assert.Throws<InvalidOperationException>(() =>
            f.Commit(sandbox, network: Network + 1));
        Assert.Throws<InvalidOperationException>(() =>
            f.Commit(sandbox, revision: 2));
        Assert.Equal(before, File.ReadAllBytes(files.PathToCheckpoint));
        Assert.Equal(0UL, sandbox.Inspect(f.Decision).StateRevision);
    }

    [Fact]
    public void NoAuthorityOrInsufficientOpeningCashFailsBeforeDiskPublication()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        var denied = f.Create(files.PathToCheckpoint, allow: false);
        var before = File.ReadAllBytes(files.PathToCheckpoint);
        Assert.Throws<InvalidOperationException>(() => f.Commit(denied));
        Assert.Equal(before, File.ReadAllBytes(files.PathToCheckpoint));
        var lower = new Fixture();
        var changed = lower.Opening.ToDictionary(x => x.Key, x => x.Value);
        changed[ExperimentalCashAuditAccountKey.Household(
            new EconomicPointId(3))] = new FoodTransportCostFraction(106, 1);
        Assert.Throws<InvalidDataException>(() =>
            lower.Open(files.PathToCheckpoint, opening: changed));
        Assert.Equal(before, File.ReadAllBytes(files.PathToCheckpoint));
    }

    [Fact]
    public void CorruptedOrFabricatedCommittedCheckpointsFailClosed()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        f.Commit(f.Create(files.PathToCheckpoint));
        var original = File.ReadAllText(files.PathToCheckpoint, Encoding.UTF8);
        Assert.Contains("107/1", original);
        File.WriteAllText(files.PathToCheckpoint,
            original.Replace("107/1", "999/1"), Encoding.UTF8);
        Assert.Throws<InvalidDataException>(() =>
            new Fixture().Open(files.PathToCheckpoint));
    }

    [Fact]
    public void TruncatedOrModifiedSchemaAndStagedOrphansAreNotAccepted()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        _ = f.Create(files.PathToCheckpoint);
        var untouched = File.ReadAllBytes(files.PathToCheckpoint);
        var orphan = files.PathToCheckpoint + ".old-crash.stage";
        File.WriteAllText(orphan, "{ not committed }");
        Assert.Equal(0UL, new Fixture().Open(files.PathToCheckpoint)
            .Inspect(f.Decision).StateRevision);
        Assert.Equal(untouched, File.ReadAllBytes(files.PathToCheckpoint));
        File.WriteAllText(files.PathToCheckpoint, "{\"Schema\":49");
        Assert.Throws<InvalidDataException>(() =>
            new Fixture().Open(files.PathToCheckpoint));
        Assert.Throws<IOException>(() => f.Create(files.PathToCheckpoint));
    }

    [Fact]
    public async Task IndependentObjectsSharingSameFileSerializeConcurrentCommitAttempts()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        var a = f.Create(files.PathToCheckpoint);
        var b = new Fixture().Open(files.PathToCheckpoint);
        var decisions = Enumerable.Range(0, 16).Select(i =>
            Task.Run(() => i % 2 == 0
                ? f.Commit(a)
                : new Fixture().Commit(b))).ToArray();
        var outcomes = await Task.WhenAll(decisions);
        Assert.Single(outcomes.Where(x => !x.WasAlreadyCommitted));
        Assert.Equal(15, outcomes.Count(x => x.WasAlreadyCommitted));
        Assert.Single(outcomes.Select(x => x.Proof).Distinct());
        Assert.Equal(1UL, new Fixture().Open(files.PathToCheckpoint)
            .Inspect(f.Decision).StateRevision);
    }

    [Fact]
    public void FilesCannotAccidentallyOverwriteGameSavesOrExistingSandboxState()
    {
        using var files = new TempFolder();
        var f = new Fixture();
        var officialSave = Path.Combine(files.DirectoryPath, "ga-save-v17.json");
        File.WriteAllText(officialSave, "ACTUAL-SAVE-BYTES");
        Assert.Throws<ArgumentException>(() => f.Create(officialSave));
        Assert.Equal("ACTUAL-SAVE-BYTES", File.ReadAllText(officialSave));
        _ = f.Create(files.PathToCheckpoint);
        Assert.Throws<IOException>(() => f.Create(files.PathToCheckpoint));
        Assert.Throws<FileNotFoundException>(() =>
            new Fixture().Open(Path.Combine(files.DirectoryPath,
                "missing.ga-vs49-lab.json")));
    }
}
