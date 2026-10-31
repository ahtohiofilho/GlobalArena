using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs48AtomicCashSettlementTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const ulong Turn = 48;
    private const ulong Revision = 19;
    private const string StateHash = "LAB-WORLD-HASH-FROM-TRUSTED-SNAPSHOT-48";
    private static readonly CommodityId Food = new(7);

    private sealed class ValueCurve : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(100, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new((BigInteger)100 * q, Unit);
    }

    private sealed class Authority(bool allow) : IExperimentalCashSettlementAuthority
    {
        public bool CanSettle(
            ExperimentalCashAuthorityDecision decision,
            ExperimentalSelectedCashProposal selected) => allow;
    }

    private sealed class Fixture
    {
        public ExperimentalCashAuthorizationSnapshot Snapshot { get; }
        public ExperimentalSelectedCashProposal Selected { get; }
        public ExperimentalCashAuthorityDecision Decision { get; }
        public ExperimentalAuditedCashCandidateAuthorization Gate { get; } = new();

        public Fixture()
        {
            var s1 = new EconomicSupplyFlow(new EconomicPointId(1), Food,
                EconomicActivityKind.Agriculture, Unit);
            var s2 = new EconomicSupplyFlow(new EconomicPointId(2), Food,
                EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(new EconomicPointId(3),
                Food, 2 * Unit);
            var flows = new EconomicProductionDemandResult(
                new[] { s1, s2 }, new[] { demand });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Food, "food", 1)
            });
            var cal = new ExperimentalFreightTechnologyPricing(
                101, 7, 48, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            ExperimentalSimultaneousFreightLane Lane(
                EconomicSupplyFlow supply) => new(
                supply, demand, new[] {
                    ExperimentalSharedFreightOption.Synthetic(
                        10, new StrategicEconomicRouteKey(
                            supply.EconomicPointId, demand.EconomicPointId),
                        cal, 1_000_000, new[] { corridor })
                });
            var lanes = new[] { Lane(s1), Lane(s2) };
            var budgets = new[] { new ExperimentalBuyerCashBudget(
                demand.EconomicPointId,
                new FoodTransportCostFraction(107, 1),
                new FoodTransportCostFraction(5, 1)) };
            var prices = new[] { new ExperimentalBuyerCommodityPrice(
                demand.EconomicPointId, Food,
                new FoodTransportCostFraction(10, 1)) };
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                10, 1_000_000);
            var candidate = new ExperimentalSimultaneousFreightReference().Compare(
                flows, catalog,
                new[] { new EconomicTurnCommodityValueOffer(
                    demand.EconomicPointId, Food, new ValueCurve()) },
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
                Turn, StateHash, Revision,
                flows, catalog, lanes, budgets, prices, congestion);
            Selected = Gate.Select(Snapshot,
                new[] { new ExperimentalAuditedCandidate(1, candidate) },
                new ExperimentalAuditedSelectionPolicy("GA-TEST-48", 1,
                    ExperimentalAuditedSelectionRule.MaxDeliveredRawVolumeThenLowestFreight),
                Turn, StateHash, Revision);
            Decision = new ExperimentalCashAuthorityDecision(
                "TEST-AUTHENTICATED-ACTOR", "DECISION-VS48",
                true, Turn, Selected.SnapshotDigest, Selected.CandidateDigest,
                Selected.CandidateId, Selected.Policy.PolicyId,
                Selected.Policy.PolicyVersion);
        }

        public ExperimentalAtomicCashSettlementLab Lab(
            bool allowed = true,
            IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>? opening = null) =>
            new(Snapshot, opening ?? Selected.Audit.Opening,
                new Authority(allowed));

        public ExperimentalCashSettlementOutcome Commit(
            ExperimentalAtomicCashSettlementLab lab,
            ExperimentalCashAuthorityDecision? decision = null,
            ulong expectedVersion = 0, ulong currentTurn = Turn,
            string currentHash = StateHash, ulong networkRevision = Revision) =>
            lab.Commit(Selected, decision ?? Decision,
                currentTurn, currentHash, networkRevision, expectedVersion);
    }

    private static void Money(long expected, FoodTransportCostFraction actual) =>
        Assert.Equal(0, actual.CompareTo(
            new FoodTransportCostFraction(expected, 1)));

    [Fact]
    public void AtomicCompletePostingDebitsHouseholdAndCreditsExistingProducerAndCarrierBalances()
    {
        var f = new Fixture();
        var funded = f.Selected.Audit.Opening.ToDictionary(x => x.Key, x => x.Value);
        var priorProducer = ExperimentalCashAuditAccountKey.Producer(
            new EconomicPointId(1));
        var priorCarrier = ExperimentalCashAuditAccountKey.Operator(
            new EconomicPointId(1), new EconomicPointId(3), 10);
        funded[priorProducer] = new FoodTransportCostFraction(7, 1);
        funded[priorCarrier] = new FoodTransportCostFraction(9, 1);
        var lab = f.Lab(opening: funded);
        var before = lab.Inspect();
        Assert.Equal(0UL, before.StateRevision);
        var outcome = f.Commit(lab);
        Assert.False(outcome.WasAlreadyCommitted);
        var state = lab.Inspect();
        Assert.Equal(1UL, state.StateRevision);
        Assert.Equal("DECISION-VS48", state.CommittedDecisionId);
        Assert.Equal(4, state.Journal.Count);
        Money(123, outcome.Proof.OpeningMoney);
        Money(123, outcome.Proof.ClosingMoney);
        Money(5, state.Accounts[ExperimentalCashAuditAccountKey.Household(
            new EconomicPointId(3))]);
        Money(17, state.Accounts[priorProducer]);
        Money(50, state.Accounts[priorCarrier]);
        Money(10, state.Accounts[ExperimentalCashAuditAccountKey.Producer(
            new EconomicPointId(2))]);
        Money(41, state.Accounts[ExperimentalCashAuditAccountKey.Operator(
            new EconomicPointId(2), new EconomicPointId(3), 10)]);
        Assert.Equal(64, outcome.Proof.CommittedBalancesDigest.Length);
    }

    [Fact]
    public void SameDecisionReplayReturnsIdenticalProofWithoutDoubleCharging()
    {
        var f = new Fixture();
        var lab = f.Lab();
        var first = f.Commit(lab);
        var before = lab.Inspect();
        var second = f.Commit(lab);
        Assert.False(first.WasAlreadyCommitted);
        Assert.True(second.WasAlreadyCommitted);
        Assert.Equal(first.Proof, second.Proof);
        var after = lab.Inspect();
        Assert.Equal(1UL, after.StateRevision);
        Assert.Equal(before.Journal.ToArray(), after.Journal.ToArray());
        Assert.Equal(first.Proof.CommittedBalancesDigest,
            second.Proof.CommittedBalancesDigest);
    }

    [Fact]
    public void SameDecisionIdWithAlteredActorIsRejectedAndSecondDecisionCannotUseTheSameCycle()
    {
        var f = new Fixture();
        var lab = f.Lab();
        f.Commit(lab);
        var opening = lab.Inspect();
        Assert.Throws<InvalidOperationException>(() => f.Commit(lab,
            f.Decision with { ActorId = "ANOTHER-ACTOR" }));
        Assert.Throws<InvalidOperationException>(() => f.Commit(lab,
            f.Decision with { DecisionId = "SECOND-DECISION" },
            expectedVersion: 1));
        var after = lab.Inspect();
        Assert.Equal(opening.StateRevision, after.StateRevision);
        Assert.Equal(opening.Journal.ToArray(), after.Journal.ToArray());
    }

    [Fact]
    public void StaleCompareAndSwapRevisionRejectsWithoutAnyPartialJournal()
    {
        var f = new Fixture();
        var lab = f.Lab();
        Assert.Throws<InvalidOperationException>(() => f.Commit(
            lab, expectedVersion: 5));
        Assert.Empty(lab.Inspect().Journal);
        Assert.Equal(0UL, lab.Inspect().StateRevision);
        f.Commit(lab);
        Assert.Equal(1UL, lab.Inspect().StateRevision);
    }

    [Fact]
    public void NoInstitutionalPermissionOrNegativeDecisionMeansNoCommit()
    {
        var f = new Fixture();
        var denied = f.Lab(allowed: false);
        Assert.Throws<InvalidOperationException>(() => f.Commit(denied));
        Assert.Equal(0UL, denied.Inspect().StateRevision);
        Assert.Empty(denied.Inspect().Journal);
        var approvedPermission = f.Lab();
        Assert.Throws<InvalidOperationException>(() => f.Commit(
            approvedPermission, f.Decision with { Approves = false }));
        Assert.Empty(approvedPermission.Inspect().Journal);
        Assert.Throws<InvalidOperationException>(() => f.Commit(
            approvedPermission, f.Decision with {
                ExpectedCandidateDigest = new string('0', 64)
            }));
        Assert.Equal(0UL, approvedPermission.Inspect().StateRevision);
    }

    [Fact]
    public void ChangedTurnWorldOrNetworkVersionRejectsBeforeAnyPosting()
    {
        var f = new Fixture();
        var lab = f.Lab();
        Assert.Throws<InvalidOperationException>(() => f.Commit(
            lab, currentTurn: Turn + 1));
        Assert.Throws<InvalidOperationException>(() => f.Commit(
            lab, currentHash: "CHANGED"));
        Assert.Throws<InvalidOperationException>(() => f.Commit(
            lab, networkRevision: Revision + 1));
        Assert.Equal(0UL, lab.Inspect().StateRevision);
        Assert.Empty(lab.Inspect().Journal);
    }

    [Fact]
    public void MissingBuyerOpeningOrInsufficientPrefundingFailsAtomically()
    {
        var f = new Fixture();
        var empty = f.Lab(opening:
            new Dictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>());
        Assert.Throws<InvalidOperationException>(() => f.Commit(empty));
        Assert.Empty(empty.Inspect().Journal);

        var smaller = f.Selected.Audit.Opening.ToDictionary(
            x => x.Key, x => x.Value);
        smaller[ExperimentalCashAuditAccountKey.Household(new EconomicPointId(3))] =
            new FoodTransportCostFraction(106, 1);
        var lab = f.Lab(opening: smaller);
        var before = lab.Inspect();
        Assert.Throws<InvalidOperationException>(() => f.Commit(lab));
        Assert.Equal(before.StateRevision, lab.Inspect().StateRevision);
        Money(106, lab.Inspect().Accounts.Single().Value);
        Assert.Empty(lab.Inspect().Journal);
    }

    [Fact]
    public async Task SameDecisionConcurrentRetriesAreSerializedAndIdempotent()
    {
        var f = new Fixture();
        var lab = f.Lab();
        var results = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => f.Commit(lab)))
            .ToArray();
        await Task.WhenAll(results);
        Assert.Equal(1, results.Count(x => !x.Result.WasAlreadyCommitted));
        Assert.Equal(15, results.Count(x => x.Result.WasAlreadyCommitted));
        Assert.Equal(1UL, lab.Inspect().StateRevision);
        Assert.Equal(4, lab.Inspect().Journal.Count);
        Assert.Single(results.Select(x => x.Result.Proof).Distinct());
    }

    [Fact]
    public void ReturnedInspectionDoesNotExposeMutableBalancesOrJournal()
    {
        var f = new Fixture();
        var lab = f.Lab();
        var initial = lab.Inspect();
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>)initial.Accounts).Clear());
        f.Commit(lab);
        var view = lab.Inspect();
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ExperimentalCashAuditTransfer>)view.Journal).Clear());
        Assert.Equal(4, lab.Inspect().Journal.Count);
    }

    [Fact]
    public void AuthorizationFromAnotherSnapshotCannotCommitEvenWithMatchingNumericAccounts()
    {
        var f = new Fixture();
        var other = new Fixture();
        var lab = f.Lab();
        Assert.Throws<InvalidOperationException>(() => lab.Commit(
            other.Selected, other.Decision,
            Turn, StateHash, Revision, 0));
        Assert.Equal(0UL, lab.Inspect().StateRevision);
    }
}
