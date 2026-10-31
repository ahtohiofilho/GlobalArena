using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Synthetic file-based single-process benchmark. A new temporary directory
/// is used per sample; preparation and candidate selection are not timed.
/// Measures disk-backed commit+reopen+replay, NOT production save I/O or
/// crash-safe distributed durability. No timing acceptance thresholds.
/// </summary>
public static class GaVs49RecoverableCheckpointBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private sealed class Authority : IExperimentalCashSettlementAuthority
    {
        public bool CanSettle(
            ExperimentalCashAuthorityDecision decision,
            ExperimentalSelectedCashProposal selected) =>
            decision.ActorId == "GA-VS49-BENCHMARK-AUTHORITY";
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS49 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS49-RECOVERABLE-CASH-CHECKPOINT");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var count in new[] { 8, 32, 128 })
                Measure(count);
            Console.WriteLine("RESULT=PASS_GA_VS49_RECOVERABLE_CHECKPOINT_BENCHMARK");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.WriteLine("RESULT=FAIL_GA_VS49_RECOVERABLE_CHECKPOINT_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int count)
    {
        var item = new CommodityId(7);
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(item, "food", 1)
        });
        var calibration = new ExperimentalFreightTechnologyPricing(
            101, 7, 49, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var congestion = new ExperimentalSharedFreightCongestionPolicy(
            1, 1_000_000);
        var fine = new GoldbergParameters(18, 0);
        var supplies = new List<EconomicSupplyFlow>();
        var demands = new List<EconomicDemandFlow>();
        var budgets = new List<ExperimentalBuyerCashBudget>();
        var prices = new List<ExperimentalBuyerCommodityPrice>();
        var lanes = new List<ExperimentalSimultaneousFreightLane>();
        var lots = new List<ExperimentalSharedFreightLot>();
        var deliveries = new List<ExperimentalBuyerCashDelivery>();
        var trades = new List<ExperimentalSimultaneousFreightDecision>();
        for (var i = 0; i < count; i++)
        {
            var source = new EconomicPointId((ulong)i + 1);
            var buyer = new EconomicPointId((ulong)i + 1001);
            var supply = new EconomicSupplyFlow(
                source, item, EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(buyer, item, Unit);
            supplies.Add(supply);
            demands.Add(demand);
            budgets.Add(new ExperimentalBuyerCashBudget(
                buyer, new FoodTransportCostFraction(200, 1),
                new FoodTransportCostFraction(5, 1)));
            prices.Add(new ExperimentalBuyerCommodityPrice(
                buyer, item, new FoodTransportCostFraction(10, 1)));
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(fine,
                    new StrategicEdgeId((ulong)(i / 8 + 1))), 101, 7);
            var service = ExperimentalSharedFreightOption.Synthetic(
                10, new StrategicEconomicRouteKey(source, buyer),
                calibration, 1_000_000, new[] { corridor });
            lanes.Add(new ExperimentalSimultaneousFreightLane(
                supply, demand, new[] { service }));
            lots.Add(new ExperimentalSharedFreightLot(
                (ulong)i + 1, 1_000_000, new[] { service }));
            deliveries.Add(new ExperimentalBuyerCashDelivery(
                buyer, item, Unit, 1_000_000, service));
            trades.Add(new ExperimentalSimultaneousFreightDecision(
                source, buyer, item, 10, Unit));
        }

        var flows = new EconomicProductionDemandResult(supplies, demands);
        var freight = ExperimentalSharedFreightBatchCost.Evaluate(
            lots, new int[count], congestion);
        var ledgers = new ExperimentalBuyerCashSnapshot(budgets, prices)
            .Evaluate(deliveries, freight.CorridorLoads, congestion);
        if (!ledgers.CanSettleAtSight)
            throw new InvalidOperationException(
                "Benchmark must have sufficient opening cash.");
        var allocation = new ExperimentalSimultaneousFreightAllocation(
            new ExperimentalSignedEconomicValue(0, 1),
            new FoodTransportCostFraction(0, 1),
            new FoodTransportCostFraction(0, 1),
            freight.TotalCost, trades, freight.CorridorLoads)
        { CashLedger = ledgers.Buyers };

        var snapshot = new ExperimentalCashAuthorizationSnapshot(
            49, "TRUSTED-BENCHMARK-STATE-49", 9,
            flows, catalog, lanes, budgets, prices, congestion);
        var selected = new ExperimentalAuditedCashCandidateAuthorization()
            .Select(snapshot, new[] {
                new ExperimentalAuditedCandidate(1, allocation)
            }, new ExperimentalAuditedSelectionPolicy(
                "GA-VS49-REFERENCE-SELECTION", 1,
                ExperimentalAuditedSelectionRule
                    .MaxDeliveredRawVolumeThenLowestFreight),
                49, snapshot.WorldStateHash, snapshot.NetworkRevision);
        var authority = new Authority();
        var decision = new ExperimentalCashAuthorityDecision(
            "GA-VS49-BENCHMARK-AUTHORITY", "BENCHMARK-DECISION-49",
            true, 49, selected.SnapshotDigest, selected.CandidateDigest,
            selected.CandidateId, selected.Policy.PolicyId,
            selected.Policy.PolicyVersion);

        var samples = new double[3];
        var allocated = new long[3];
        string? signature = null;
        for (var attempt = -1; attempt < 3; attempt++)
        {
            var folder = Path.Combine(Path.GetTempPath(), "ga-vs49-bench",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var filename = Path.Combine(folder,
                    "transaction.ga-vs49-lab.json");
                var lab = ExperimentalRecoverableCashCheckpointSandbox.Create(
                    filename, snapshot, selected, selected.Audit.Opening,
                    authority);
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                var committed = lab.Commit(decision, 49,
                    snapshot.WorldStateHash, snapshot.NetworkRevision, 0);
                var restored = ExperimentalRecoverableCashCheckpointSandbox.Open(
                    filename, snapshot, selected, selected.Audit.Opening,
                    authority, decision);
                var replay = restored.Commit(decision, 49,
                    snapshot.WorldStateHash, snapshot.NetworkRevision, 0);
                var inspected = restored.Inspect(decision);
                var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var bytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (committed.WasAlreadyCommitted || !replay.WasAlreadyCommitted ||
                    committed.Proof.CommittedBalancesDigest !=
                        replay.Proof.CommittedBalancesDigest ||
                    committed.Proof.AuditJournalDigest !=
                        replay.Proof.AuditJournalDigest ||
                    committed.Proof.DecisionId != replay.Proof.DecisionId ||
                    committed.Proof.OpeningMoney.CompareTo(
                        replay.Proof.OpeningMoney) != 0 ||
                    committed.Proof.ClosingMoney.CompareTo(
                        replay.Proof.ClosingMoney) != 0 ||
                    inspected.StateRevision != 1 ||
                    inspected.Journal.Count != count * 2 ||
                    inspected.Accounts.Count != count * 3 ||
                    committed.Proof.OpeningMoney.CompareTo(
                        committed.Proof.ClosingMoney) != 0)
                    throw new InvalidOperationException(
                        "VS49 disk persistence, cash conservation or replay failed.");
                var current = replay.Proof.CommittedBalancesDigest + ":" +
                    replay.Proof.AuditJournalDigest + ":" +
                    inspected.StateRevision;
                if (attempt >= 0)
                {
                    if (signature is null) signature = current;
                    else if (signature != current)
                        throw new InvalidOperationException(
                            "Recovered checkpoint results must repeat exactly.");
                    samples[attempt] = elapsed;
                    allocated[attempt] = bytes;
                }
                if (attempt == 2)
                    Console.WriteLine(
                        $"CASE=buyers-{count};transfers={inspected.Journal.Count};" +
                        $"accounts={inspected.Accounts.Count};" +
                        $"median_commit_reopen_replay_ms=" +
                            $"{samples.OrderBy(x => x).ElementAt(1):F3};" +
                        $"median_allocated_bytes=" +
                            $"{allocated.OrderBy(x => x).ElementAt(1)}");
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
