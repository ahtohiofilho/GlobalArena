using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs47AuditedCashAuthorizationTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CommodityId Commodity = new(7);
    private const string StateHash = "SHA256-EXTERNAL-V17-ORIGINAL-01";
    private const ulong CurrentTurn = 7UL;
    private const ulong NetworkRevision = 19UL;

    private sealed class FlatValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(100, 1);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new((BigInteger)100 * q, Unit);
    }

    private sealed class Fixture
    {
        public EconomicProductionDemandResult Flows { get; }
        public FoodCommodityCatalog Catalog { get; }
        public ExperimentalSharedFreightCongestionPolicy Congestion { get; } =
            new(10, 1_000_000);
        public ExperimentalSimultaneousFreightLane[] Lanes { get; }
        public ExperimentalBuyerCashBudget[] Budgets { get; }
        public ExperimentalBuyerCommodityPrice[] Prices { get; }
        public ExperimentalSimultaneousFreightAllocation Full { get; }
        public ExperimentalSimultaneousFreightAllocation Partial { get; }
        public ExperimentalSimultaneousFreightAllocation OtherPartial { get; }
        public ExperimentalCashAuthorizationSnapshot Snapshot { get; }
        public ExperimentalAuditedCashCandidateAuthorization Gate { get; } = new();

        public Fixture()
        {
            var s1 = new EconomicSupplyFlow(new EconomicPointId(1), Commodity,
                EconomicActivityKind.Agriculture, Unit);
            var s2 = new EconomicSupplyFlow(new EconomicPointId(2), Commodity,
                EconomicActivityKind.Agriculture, Unit);
            var d = new EconomicDemandFlow(new EconomicPointId(3),
                Commodity, 2 * Unit);
            Flows = new EconomicProductionDemandResult(
                new[] { s1, s2 }, new[] { d });
            Catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Commodity, "food", 1)
            });
            var cal = new ExperimentalFreightTechnologyPricing(
                101, 7, 47, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            ExperimentalSharedFreightOption Service(
                EconomicPointId source) =>
                ExperimentalSharedFreightOption.Synthetic(
                    10, new StrategicEconomicRouteKey(source, d.EconomicPointId),
                    cal, 1_000_000, new[] { corridor });
            Lanes = new[] {
                new ExperimentalSimultaneousFreightLane(
                    s1, d, new[] { Service(s1.EconomicPointId) }),
                new ExperimentalSimultaneousFreightLane(
                    s2, d, new[] { Service(s2.EconomicPointId) })
            };
            Budgets = new[] { new ExperimentalBuyerCashBudget(
                d.EconomicPointId, new FoodTransportCostFraction(107, 1),
                new FoodTransportCostFraction(5, 1)) };
            Prices = new[] { new ExperimentalBuyerCommodityPrice(
                d.EconomicPointId, Commodity,
                new FoodTransportCostFraction(10, 1)) };
            Snapshot = new ExperimentalCashAuthorizationSnapshot(
                CurrentTurn, StateHash, NetworkRevision, Flows, Catalog,
                Lanes, Budgets, Prices, Congestion);
            ExperimentalSimultaneousFreightAllocation Solve(
                IEnumerable<ExperimentalSimultaneousFreightLane> offers) =>
                new ExperimentalSimultaneousFreightReference().Compare(
                    Flows, Catalog, new[] { new EconomicTurnCommodityValueOffer(
                        d.EconomicPointId, Commodity, new FlatValue()) },
                    new[] {
                        new EconomicTurnSourceReservation(
                            s1.EconomicPointId, Commodity, s1.Activity, 0),
                        new EconomicTurnSourceReservation(
                            s2.EconomicPointId, Commodity, s2.Activity, 0)
                    }, offers, Congestion, Unit,
                    maxExactConfigurations: 16,
                    cashSnapshot: new ExperimentalBuyerCashSnapshot(
                        Budgets, Prices)).Exact!;

            Full = Solve(Lanes);
            Partial = Solve(new[] { Lanes[0] });
            OtherPartial = Solve(new[] { Lanes[1] });
        }

        public ExperimentalAuditedSelectionPolicy Policy(
            ExperimentalAuditedSelectionRule rule =
                ExperimentalAuditedSelectionRule.MaxDeliveredRawVolumeThenLowestFreight) =>
            new("GA-ECON-SELECT-LAB", 1UL, rule);

        public ExperimentalSelectedCashProposal Select(
            IEnumerable<ExperimentalAuditedCandidate> candidates,
            ExperimentalAuditedSelectionPolicy? policy = null) =>
            Gate.Select(Snapshot, candidates, policy ?? Policy(),
                CurrentTurn, StateHash, NetworkRevision);

        public ExperimentalCashAuthorityDecision Decision(
            ExperimentalSelectedCashProposal selection, bool approves = true) =>
            new("GA-INSTITUTIONAL-TEST-ACTOR", "decision-47-A",
                approves, CurrentTurn,
                selection.SnapshotDigest, selection.CandidateDigest,
                selection.CandidateId,
                selection.Policy.PolicyId, selection.Policy.PolicyVersion);

        public ExperimentalCashAuthorizationReceipt Authorize(
            ExperimentalSelectedCashProposal selection,
            ExperimentalCashAuthorityDecision? decision = null) =>
            Gate.Authorize(selection, decision ?? Decision(selection),
                CurrentTurn, StateHash, NetworkRevision);
    }

    [Fact]
    public void SelectsHighestAuditedDeliveredQuantityNotClaimedWelfare()
    {
        var f = new Fixture();
        var counterfeitWelfare = f.Partial with {
            NetEconomicValue = new ExperimentalSignedEconomicValue(
                BigInteger.Pow(10, 30), 1)
        };
        var result = f.Select(new[] {
            new ExperimentalAuditedCandidate(40, counterfeitWelfare),
            new ExperimentalAuditedCandidate(20, f.Full)
        });
        Assert.Equal(20UL, result.CandidateId);
        Assert.Equal(new BigInteger(2 * Unit), result.DeliveredRawQuantity);
        Assert.Equal(0, result.Audit.TotalFreight.CompareTo(
            new FoodTransportCostFraction(82, 1)));
        Assert.Equal(0, result.Audit.TotalOpening.CompareTo(
            result.Audit.TotalClosing));
        var certificate = f.Authorize(result);
        Assert.Equal(result.CandidateDigest, certificate.CandidateDigest);
        Assert.Equal(result.SnapshotDigest, certificate.SnapshotDigest);
        Assert.Equal("decision-47-A", certificate.DecisionId);
        Assert.Equal(CurrentTurn, certificate.TurnOrdinal);
        Assert.Equal(NetworkRevision, certificate.NetworkRevision);
        Assert.Equal("GA-ECON-SELECT-LAB", certificate.PolicyId);
        Assert.Equal(64, certificate.AuditJournalDigest.Length);
    }

    [Fact]
    public void EqualVolumeSelectionUsesAuditedFreightAndCanonicalIdTie()
    {
        var f = new Fixture();
        var minFreight = f.Policy(
            ExperimentalAuditedSelectionRule.LowestFreightAtEqualRawVolume);
        var one = f.Select(new[] {
            new ExperimentalAuditedCandidate(9, f.Partial),
            new ExperimentalAuditedCandidate(3, f.OtherPartial)
        }, minFreight);
        Assert.Equal(3UL, one.CandidateId);
        var otherOrder = f.Select(new[] {
            new ExperimentalAuditedCandidate(3, f.OtherPartial),
            new ExperimentalAuditedCandidate(9, f.Partial)
        }, minFreight);
        Assert.Equal(one.CandidateDigest, otherOrder.CandidateDigest);
        Assert.Equal(one.CandidateId, otherOrder.CandidateId);
        Assert.Throws<InvalidOperationException>(() =>
            f.Select(new[] {
                new ExperimentalAuditedCandidate(1, f.Partial),
                new ExperimentalAuditedCandidate(2, f.Full)
            }, minFreight));
    }

    [Fact]
    public void AuditRejectsInvalidCandidateEvenWhenAnotherCandidateIsValid()
    {
        var f = new Fixture();
        var bad = f.Full with {
            SharedFreightCost = new FoodTransportCostFraction(1, 1)
        };
        Assert.Throws<InvalidOperationException>(() => f.Select(new[] {
            new ExperimentalAuditedCandidate(1, f.Full),
            new ExperimentalAuditedCandidate(2, bad)
        }));
        Assert.Throws<ArgumentException>(() => f.Select(new[] {
            new ExperimentalAuditedCandidate(1, f.Full),
            new ExperimentalAuditedCandidate(1, f.Partial)
        }));
        Assert.Throws<ArgumentException>(() => f.Select(
            Array.Empty<ExperimentalAuditedCandidate>()));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAuditedSelectionPolicy("", 0,
                ExperimentalAuditedSelectionRule.LowestFreightAtEqualRawVolume));
    }

    [Fact]
    public void StaleTurnWorldHashOrNetworkRevisionInvalidateSelectionAndAuthorization()
    {
        var f = new Fixture();
        var selection = f.Select(new[] {
            new ExperimentalAuditedCandidate(1, f.Full)
        });
        Assert.Throws<InvalidOperationException>(() =>
            f.Gate.Select(f.Snapshot,
                new[] { new ExperimentalAuditedCandidate(1, f.Full) },
                f.Policy(), CurrentTurn + 1, StateHash, NetworkRevision));
        Assert.Throws<InvalidOperationException>(() =>
            f.Gate.Select(f.Snapshot,
                new[] { new ExperimentalAuditedCandidate(1, f.Full) },
                f.Policy(), CurrentTurn, "STATE_CHANGED", NetworkRevision));
        Assert.Throws<InvalidOperationException>(() =>
            f.Gate.Authorize(selection, f.Decision(selection),
                CurrentTurn, StateHash, NetworkRevision + 1));
        Assert.Throws<InvalidOperationException>(() =>
            f.Gate.Authorize(selection, f.Decision(selection),
                CurrentTurn + 1, StateHash, NetworkRevision));
        Assert.Throws<InvalidOperationException>(() =>
            f.Gate.Authorize(selection, f.Decision(selection),
                CurrentTurn, "STATE_CHANGED", NetworkRevision));
    }

    [Fact]
    public void ApprovalMustExplicitlyMatchCandidatePolicySnapshotAndActor()
    {
        var f = new Fixture();
        var selection = f.Select(new[] {
            new ExperimentalAuditedCandidate(42, f.Full)
        });
        var decision = f.Decision(selection);
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with { Approves = false }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with { ActorId = "" }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with { DecisionId = "" }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with {
                ExpectedPolicyVersion = 2UL
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with {
                ExpectedPolicyId = "UNKNOWN-POLICY"
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with {
                ExpectedCandidateId = 404UL
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with {
                ExpectedCandidateDigest = new string('0', 64)
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with {
                ExpectedSnapshotDigest = new string('0', 64)
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selection, decision with {
                ExpectedTurnOrdinal = CurrentTurn - 1
            }));
    }

    [Fact]
    public void ReadOnlySelectionAndAuthorizationKeepOriginalInputsUnchanged()
    {
        var f = new Fixture();
        var trades = f.Full.Trades.ToArray();
        var beforeBudgets = f.Budgets.Select(x =>
            x.GrossCash.Numerator.ToString()).ToArray();
        var beforeFlows = f.Flows.DemandFlows.Select(x => x.RawQuantity).ToArray();
        var selection = f.Select(new[] {
            new ExperimentalAuditedCandidate(1, f.Full),
            new ExperimentalAuditedCandidate(2, f.Partial)
        });
        var receipt1 = f.Authorize(selection);
        var receipt2 = f.Authorize(selection);
        Assert.Equal(receipt1, receipt2); // deterministic pure id, not used-once credential
        Assert.Equal(trades, f.Full.Trades);
        Assert.Equal(beforeBudgets, f.Budgets.Select(x =>
            x.GrossCash.Numerator.ToString()));
        Assert.Equal(beforeFlows, f.Flows.DemandFlows.Select(x => x.RawQuantity));
    }

    [Fact]
    public void CandidateMutationAfterSelectionIsDetectedByIndependentAudit()
    {
        var f = new Fixture();
        var adjustableTrades = f.Full.Trades.ToList();
        var candidate = f.Full with { Trades = adjustableTrades };
        var selected = f.Select(new[] {
            new ExperimentalAuditedCandidate(1, candidate)
        });
        adjustableTrades[0] = adjustableTrades[0] with {
            RawQuantity = 2 * Unit
        };
        Assert.Throws<InvalidOperationException>(() =>
            f.Authorize(selected));
    }

    [Fact]
    public void SnapshotDigestBindsCashPricesAndVersionedNetworkPolicy()
    {
        var f = new Fixture();
        var original = f.Snapshot.ContentDigest;
        var alteredPrice = new ExperimentalCashAuthorizationSnapshot(
            CurrentTurn, StateHash, NetworkRevision, f.Flows, f.Catalog,
            f.Lanes, f.Budgets, new[] {
                new ExperimentalBuyerCommodityPrice(
                    new EconomicPointId(3), Commodity,
                    new FoodTransportCostFraction(11, 1))
            }, f.Congestion);
        Assert.NotEqual(original, alteredPrice.ContentDigest);
        var alteredNetwork = new ExperimentalCashAuthorizationSnapshot(
            CurrentTurn, StateHash, NetworkRevision + 1, f.Flows, f.Catalog,
            f.Lanes, f.Budgets, f.Prices, f.Congestion);
        Assert.NotEqual(original, alteredNetwork.ContentDigest);
        var alteredBudget = new ExperimentalCashAuthorizationSnapshot(
            CurrentTurn, StateHash, NetworkRevision, f.Flows, f.Catalog,
            f.Lanes, new[] {
                new ExperimentalBuyerCashBudget(new EconomicPointId(3),
                    new FoodTransportCostFraction(108, 1),
                    new FoodTransportCostFraction(5, 1))
            }, f.Prices, f.Congestion);
        Assert.NotEqual(original, alteredBudget.ContentDigest);
        var reordering = new ExperimentalCashAuthorizationSnapshot(
            CurrentTurn, StateHash, NetworkRevision, f.Flows, f.Catalog,
            f.Lanes.Reverse(), f.Budgets.Reverse(), f.Prices.Reverse(),
            f.Congestion);
        Assert.Equal(original, reordering.ContentDigest);
    }
}
