using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Synthetic one-cycle SETTLE-only workload with many independent buyers
/// and groups of eight sharing each corridor. Constructing the offers,
/// cash invoices and institutional selection is OUTSIDE the measurement.
/// No data from the real V14 economy is mutated or used to claim a SLA.
/// </summary>
public static class GaVs48AtomicSettlementBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private sealed class Granted : IExperimentalCashSettlementAuthority
    {
        public bool CanSettle(
            ExperimentalCashAuthorityDecision decision,
            ExperimentalSelectedCashProposal selected) =>
            decision.ActorId == "BENCHMARK-INSTITUTION";
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS48 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS48-SYNTHETIC-ATOMIC-CASH-SETTLE");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var n in new[] { 8, 32, 128 })
                Measure(n);
            Console.WriteLine("RESULT=PASS_GA_VS48_ATOMIC_CASH_SETTLEMENT_BENCHMARK");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.WriteLine("RESULT=FAIL_GA_VS48_ATOMIC_CASH_SETTLEMENT_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int count)
    {
        var food = new CommodityId(7);
        var fine = new GoldbergParameters(18, 0);
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(food, "food", 1)
        });
        var calibration = new ExperimentalFreightTechnologyPricing(
            101, 7, 48, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var policy = new ExperimentalSharedFreightCongestionPolicy(1, 1_000_000);
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
            var source = new EconomicPointId((ulong)i + 1UL);
            var buyer = new EconomicPointId((ulong)i + 1_001UL);
            var supply = new EconomicSupplyFlow(
                source, food, EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(buyer, food, Unit);
            supplies.Add(supply);
            demands.Add(demand);
            budgets.Add(new ExperimentalBuyerCashBudget(
                buyer, new FoodTransportCostFraction(200, 1),
                new FoodTransportCostFraction(5, 1)));
            prices.Add(new ExperimentalBuyerCommodityPrice(
                buyer, food, new FoodTransportCostFraction(10, 1)));
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(fine,
                    new StrategicEdgeId((ulong)(i / 8 + 1))), 101, 7);
            var option = ExperimentalSharedFreightOption.Synthetic(
                10, new StrategicEconomicRouteKey(source, buyer),
                calibration, 1_000_000, new[] { corridor });
            lanes.Add(new ExperimentalSimultaneousFreightLane(
                supply, demand, new[] { option }));
            trades.Add(new ExperimentalSimultaneousFreightDecision(
                source, buyer, food, 10, Unit));
            lots.Add(new ExperimentalSharedFreightLot(
                (ulong)i + 1UL, 1_000_000, new[] { option }));
            deliveries.Add(new ExperimentalBuyerCashDelivery(
                buyer, food, Unit, 1_000_000, option));
        }

        var flows = new EconomicProductionDemandResult(supplies, demands);
        var freight = ExperimentalSharedFreightBatchCost.Evaluate(
            lots, new int[count], policy);
        var budget = new ExperimentalBuyerCashSnapshot(budgets, prices);
        var ledgers = budget.Evaluate(deliveries, freight.CorridorLoads, policy);
        if (!ledgers.CanSettleAtSight)
            throw new InvalidOperationException(
                "Synthetic benchmark has insufficient household cash.");
        var allocation = new ExperimentalSimultaneousFreightAllocation(
            new ExperimentalSignedEconomicValue(0, 1),
            new FoodTransportCostFraction(0, 1),
            new FoodTransportCostFraction(0, 1),
            freight.TotalCost, trades, freight.CorridorLoads)
        { CashLedger = ledgers.Buyers };
        var snapshot = new ExperimentalCashAuthorizationSnapshot(
            48, "BENCHMARK-WORLD-HASH-48", 9, flows, catalog,
            lanes, budgets, prices, policy);
        var policySelect = new ExperimentalAuditedSelectionPolicy(
            "GA-BENCHMARK-SELECTION", 1,
            ExperimentalAuditedSelectionRule.MaxDeliveredRawVolumeThenLowestFreight);
        var selected = new ExperimentalAuditedCashCandidateAuthorization()
            .Select(snapshot, new[] {
                new ExperimentalAuditedCandidate(1, allocation)
            }, policySelect, 48, snapshot.WorldStateHash, snapshot.NetworkRevision);
        var decision = new ExperimentalCashAuthorityDecision(
            "BENCHMARK-INSTITUTION", "DECISION-48",
            true, 48, selected.SnapshotDigest, selected.CandidateDigest,
            selected.CandidateId, selected.Policy.PolicyId,
            selected.Policy.PolicyVersion);

        var times = new double[3];
        var bytes = new long[3];
        string? signature = null;
        for (var iteration = -1; iteration < 3; iteration++)
        {
            var lab = new ExperimentalAtomicCashSettlementLab(
                snapshot, selected.Audit.Opening, new Granted());
            var initialBytes = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.GetTimestamp();
            var result = lab.Commit(selected, decision,
                48, snapshot.WorldStateHash, snapshot.NetworkRevision, 0);
            var elapsed = Stopwatch.GetElapsedTime(timer).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - initialBytes;
            var replay = lab.Commit(selected, decision,
                48, snapshot.WorldStateHash, snapshot.NetworkRevision, 0);
            var final = lab.Inspect();
            if (result.WasAlreadyCommitted || !replay.WasAlreadyCommitted ||
                result.Proof != replay.Proof ||
                final.StateRevision != 1 ||
                final.Journal.Count != count * 2 ||
                result.Proof.OpeningMoney.CompareTo(
                    result.Proof.ClosingMoney) != 0 ||
                final.Accounts.Count != count * 3)
                throw new InvalidOperationException(
                    "Atomic settlement, replay protection or money conservation failed.");

            var current = result.Proof.CommittedBalancesDigest + ":" +
                result.Proof.AuditJournalDigest + ":" + final.StateRevision;
            if (iteration >= 0)
            {
                if (signature is null) signature = current;
                else if (!string.Equals(signature, current, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Deterministic synthetic settlement changed between runs.");
                times[iteration] = elapsed;
                bytes[iteration] = allocated;
            }
            if (iteration == 2)
                Console.WriteLine(
                    $"CASE=buyers-{count};transfers={final.Journal.Count};" +
                    $"account_count={final.Accounts.Count};" +
                    $"median_commit_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={bytes.OrderBy(x => x).ElementAt(1)}");
        }
    }
}
