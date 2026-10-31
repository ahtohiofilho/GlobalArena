using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs50CrossProcessCheckpointTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CommodityId Food = new(7);
    private const string Hash = "VS50-EXTERNAL-TRUSTED-STATE";

    private sealed class Curve : IEconomicTurnCommodityValueCurve
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
            decision.ActorId == "INSTITUTION-50" && decision.Approves;
    }

    private sealed class Inject(ExperimentalCheckpointPublicationBoundary at)
        : IExperimentalCheckpointPublicationFault
    {
        public int Calls { get; private set; }
        public void OnBoundary(ExperimentalCheckpointPublicationBoundary boundary)
        {
            if (boundary != at) return;
            Calls++;
            throw new IOException("VS50 synthetic interruption at " + boundary);
        }
    }

    private sealed class Fixture
    {
        public ExperimentalCashAuthorizationSnapshot Snapshot { get; }
        public ExperimentalSelectedCashProposal Selected { get; }
        public ExperimentalCashAuthorityDecision Decision { get; }
        public IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> Opening { get; }

        public Fixture()
        {
            var source = new EconomicPointId(1);
            var buyer = new EconomicPointId(2);
            var supply = new EconomicSupplyFlow(source, Food,
                EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(buyer, Food, Unit);
            var flows = new EconomicProductionDemandResult(
                new[] { supply }, new[] { demand });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Food, "food", 1)
            });
            var technology = new ExperimentalFreightTechnologyPricing(
                101, 7, 50, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            var service = ExperimentalSharedFreightOption.Synthetic(
                10, new StrategicEconomicRouteKey(source, buyer),
                technology, 1_000_000, new[] { corridor });
            var lane = new ExperimentalSimultaneousFreightLane(
                supply, demand, new[] { service });
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                1, 1_000_000);
            var budgets = new[] {
                new ExperimentalBuyerCashBudget(
                    buyer, new FoodTransportCostFraction(30, 1),
                    new FoodTransportCostFraction(5, 1))
            };
            var prices = new[] { new ExperimentalBuyerCommodityPrice(
                buyer, Food, new FoodTransportCostFraction(10, 1)) };
            var allocated = new ExperimentalSimultaneousFreightReference()
                .Compare(flows, catalog,
                    new[] { new EconomicTurnCommodityValueOffer(
                        buyer, Food, new Curve()) },
                    new[] { new EconomicTurnSourceReservation(
                        source, Food, supply.Activity, 0) },
                    new[] { lane }, congestion, Unit,
                    maxExactConfigurations: 8,
                    cashSnapshot: new ExperimentalBuyerCashSnapshot(
                        budgets, prices)).Exact!;
            Snapshot = new ExperimentalCashAuthorizationSnapshot(
                50, Hash, 9, flows, catalog,
                new[] { lane }, budgets, prices, congestion);
            Selected = new ExperimentalAuditedCashCandidateAuthorization()
                .Select(Snapshot,
                    new[] { new ExperimentalAuditedCandidate(1, allocated) },
                    new ExperimentalAuditedSelectionPolicy(
                        "GA-50-EXPLICIT-SELECT", 1,
                        ExperimentalAuditedSelectionRule
                            .MaxDeliveredRawVolumeThenLowestFreight),
                    50, Hash, 9);
            Decision = new ExperimentalCashAuthorityDecision(
                "INSTITUTION-50", "DECISION-50", true, 50,
                Selected.SnapshotDigest, Selected.CandidateDigest,
                Selected.CandidateId, Selected.Policy.PolicyId,
                Selected.Policy.PolicyVersion);
            Opening = Selected.Audit.Opening;
        }

        public ExperimentalRecoverableCashCheckpointSandbox Create(
            string path, IExperimentalCheckpointPublicationFault? fault = null) =>
            ExperimentalRecoverableCashCheckpointSandbox.Create(
                path, Snapshot, Selected, Opening, new Authority(), fault);

        public ExperimentalRecoverableCashCheckpointSandbox Open(
            string path, IExperimentalCheckpointPublicationFault? fault = null) =>
            ExperimentalRecoverableCashCheckpointSandbox.Open(
                path, Snapshot, Selected, Opening, new Authority(), Decision, fault);

        public ExperimentalCashSettlementOutcome Commit(
            ExperimentalRecoverableCashCheckpointSandbox lab) =>
            lab.Commit(Decision, 50, Hash, 9, 0);
    }

    private sealed class LabDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "ga-vs50-checkpoint", Guid.NewGuid().ToString("N"));
        public string Target => Path.Combine(Root, "one.ga-vs49-lab.json");
        public LabDirectory() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    [Fact]
    public void BeforeReplaceInterruptionLeavesOriginalPendingCheckpointIntact()
    {
        using var directory = new LabDirectory();
        var f = new Fixture();
        var injected = new Inject(
            ExperimentalCheckpointPublicationBoundary.StageFlushedBeforeReplace);
        var lab = f.Create(directory.Target, injected);
        var original = File.ReadAllBytes(directory.Target);
        Assert.Throws<IOException>(() => f.Commit(lab));
        Assert.Equal(1, injected.Calls);
        Assert.Equal(original, File.ReadAllBytes(directory.Target));
        Assert.Equal(0UL, new Fixture().Open(directory.Target)
            .Inspect(f.Decision).StateRevision);
        Assert.Empty(Directory.GetFiles(directory.Root, "*.stage"));
        var repaired = new Fixture();
        var completed = repaired.Commit(repaired.Open(directory.Target));
        Assert.False(completed.WasAlreadyCommitted);
        Assert.Equal(1UL, repaired.Open(directory.Target)
            .Inspect(repaired.Decision).StateRevision);
    }

    [Fact]
    public void AfterReplaceInterruptionIsRecoveredAsCommittedAndNeverChargedAgain()
    {
        using var directory = new LabDirectory();
        var f = new Fixture();
        var injected = new Inject(
            ExperimentalCheckpointPublicationBoundary.FileReplacedBeforeReturn);
        var lab = f.Create(directory.Target, injected);
        Assert.Throws<IOException>(() => f.Commit(lab));
        Assert.Equal(1, injected.Calls);
        var committedBytes = File.ReadAllBytes(directory.Target);
        var restarted = new Fixture();
        var reopened = restarted.Open(directory.Target);
        Assert.Equal(1UL, reopened.Inspect(restarted.Decision).StateRevision);
        var replay = restarted.Commit(reopened);
        Assert.True(replay.WasAlreadyCommitted);
        Assert.Equal(committedBytes, File.ReadAllBytes(directory.Target));
        Assert.Equal(1UL, replay.Proof.CommittedStateRevision);
        Assert.Equal(0, replay.Proof.OpeningMoney.CompareTo(
            replay.Proof.ClosingMoney));
    }

    [Fact]
    public void CheckpointGateRefusesSecondWriterUntilTheOriginalLeaseIsReleased()
    {
        using var directory = new LabDirectory();
        var f = new Fixture();
        _ = f.Create(directory.Target);
        using (ExperimentalCheckpointProcessGate.Acquire(directory.Target))
        {
            Assert.Throws<IOException>(() =>
                ExperimentalCheckpointProcessGate.Acquire(
                    directory.Target, TimeSpan.FromMilliseconds(60)));
        }
        using (var reacquired = ExperimentalCheckpointProcessGate.Acquire(
            directory.Target, TimeSpan.FromSeconds(1)))
        {
            Assert.True(File.Exists(directory.Target + ".gate"));
        }
        var result = f.Commit(f.Open(directory.Target));
        Assert.False(result.WasAlreadyCommitted);
    }

    [Fact]
    public void AStaleGateFileAndAbandonedStageAreNotEvidenceOfACommit()
    {
        using var directory = new LabDirectory();
        var f = new Fixture();
        _ = f.Create(directory.Target);
        File.WriteAllText(directory.Target + ".orphan.stage",
            "NOT-COMMITTED");
        Assert.True(File.Exists(directory.Target + ".gate"));
        var reopened = new Fixture().Open(directory.Target);
        Assert.Equal(0UL, reopened.Inspect(f.Decision).StateRevision);
        Assert.False(f.Commit(reopened).WasAlreadyCommitted);
        Assert.True(File.Exists(directory.Target + ".gate"));
    }

    [Fact]
    public void InvalidLockTimeoutAndOfficialSavePathFailClosed()
    {
        using var directory = new LabDirectory();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentalCheckpointProcessGate.Acquire(
                directory.Target, TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentalCheckpointProcessGate.Acquire(
                directory.Target, TimeSpan.FromMinutes(2)));
        var official = Path.Combine(directory.Root, "world-save.json");
        File.WriteAllText(official, "CANONICAL-SAVE");
        Assert.Throws<ArgumentException>(() => new Fixture().Create(official));
        Assert.Equal("CANONICAL-SAVE", File.ReadAllText(official));
    }

    [Fact]
    public void PersistedCheckpointStillRequiresIndependentAuthorizationAfterRestart()
    {
        using var directory = new LabDirectory();
        var f = new Fixture();
        _ = f.Commit(f.Create(directory.Target));
        var other = new Fixture();
        var bad = other.Decision with { ActorId = "UNAUTHORIZED-50" };
        Assert.ThrowsAny<Exception>(() =>
            ExperimentalRecoverableCashCheckpointSandbox.Open(
                directory.Target, other.Snapshot, other.Selected,
                other.Opening, new Authority(), bad));
        var verified = other.Open(directory.Target);
        Assert.True(other.Commit(verified).WasAlreadyCommitted);
    }
}
