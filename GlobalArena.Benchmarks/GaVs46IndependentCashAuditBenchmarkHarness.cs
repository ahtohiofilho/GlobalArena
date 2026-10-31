using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Deterministic synthetic invoices for the independent VS46 audit. The
/// fixture prepares physical loads and original VS44 cash reports OUTSIDE
/// timing. This measures independent reconciliation, not settlement or
/// simultaneous market search. No timing thresholds or fake production data.
/// </summary>
public static class GaVs46IndependentCashAuditBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(18, 0);

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS46 requires a Release benchmark.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS46-SYNTHETIC-INDEPENDENT-CASH-AUDIT");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var count in new[] { 8, 32, 128 })
                Measure(count);
            Console.WriteLine("RESULT=PASS_GA_VS46_INDEPENDENT_CASH_AUDIT_BENCHMARK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_GA_VS46_INDEPENDENT_CASH_AUDIT_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int count)
    {
        var commodity = new CommodityId(7);
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(commodity, "food", 1)
        });
        var calibration = new ExperimentalFreightTechnologyPricing(
            101, 7, 46, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var policy = new ExperimentalSharedFreightCongestionPolicy(
            1, 1_000_000);
        var supplies = new List<EconomicSupplyFlow>();
        var demands = new List<EconomicDemandFlow>();
        var lanes = new List<ExperimentalSimultaneousFreightLane>();
        var budgets = new List<ExperimentalBuyerCashBudget>();
        var prices = new List<ExperimentalBuyerCommodityPrice>();
        var lots = new List<ExperimentalSharedFreightLot>();
        var trades = new List<ExperimentalSimultaneousFreightDecision>();
        var deliveries = new List<ExperimentalBuyerCashDelivery>();
        for (var i = 0; i < count; i++)
        {
            var source = new EconomicPointId(1UL + (ulong)i);
            var buyer = new EconomicPointId(1001UL + (ulong)i);
            var supply = new EconomicSupplyFlow(
                source, commodity, EconomicActivityKind.Agriculture, Unit);
            var demand = new EconomicDemandFlow(buyer, commodity, Unit);
            supplies.Add(supply);
            demands.Add(demand);
            budgets.Add(new ExperimentalBuyerCashBudget(buyer,
                new FoodTransportCostFraction(32, 1),
                new FoodTransportCostFraction(5, 1)));
            prices.Add(new ExperimentalBuyerCommodityPrice(
                buyer, commodity, new FoodTransportCostFraction(10, 1)));
            var resource = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(Fine,
                    new StrategicEdgeId(1UL + (ulong)(i / 4))), 101, 7);
            var service = ExperimentalSharedFreightOption.Synthetic(
                10, new StrategicEconomicRouteKey(source, buyer),
                calibration, 1_000_000, new[] { resource });
            lanes.Add(new ExperimentalSimultaneousFreightLane(
                supply, demand, new[] { service }));
            trades.Add(new ExperimentalSimultaneousFreightDecision(
                source, buyer, commodity, 10, Unit));
            lots.Add(new ExperimentalSharedFreightLot(
                (ulong)(i + 1), 1_000_000, new[] { service }));
            deliveries.Add(new ExperimentalBuyerCashDelivery(
                buyer, commodity, Unit, 1_000_000, service));
        }
        var flows = new EconomicProductionDemandResult(supplies, demands);
        var freight = ExperimentalSharedFreightBatchCost.Evaluate(
            lots, new int[count], policy);
        var original = new ExperimentalBuyerCashSnapshot(budgets, prices)
            .Evaluate(deliveries, freight.CorridorLoads, policy);
        if (!original.CanSettleAtSight)
            throw new InvalidOperationException(
                "Benchmark opening cash must cover synthetic invoices.");
        var candidate = new ExperimentalSimultaneousFreightAllocation(
            new ExperimentalSignedEconomicValue(0, 1),
            new FoodTransportCostFraction(0, 1),
            new FoodTransportCostFraction(0, 1),
            freight.TotalCost, trades, freight.CorridorLoads)
        { CashLedger = original.Buyers };
        var auditor = new ExperimentalIndependentCashSettlementAuditor();
        var times = new double[3];
        var allocated = new long[3];
        string? signature = null;
        for (var run = -1; run < 3; run++)
        {
            var beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
            var now = Stopwatch.GetTimestamp();
            var report = auditor.Audit(candidate, flows, catalog, lanes,
                budgets, prices, policy);
            var ms = Stopwatch.GetElapsedTime(now).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - beforeAlloc;
            if (report.Transfers.Count != count * 2 ||
                report.Buyers.Count != count ||
                report.TotalOpening.CompareTo(report.TotalClosing) != 0 ||
                report.TotalFreight.CompareTo(freight.TotalCost) != 0 ||
                report.TotalCommodityPurchases.CompareTo(
                    new FoodTransportCostFraction(count * 10, 1)) != 0)
                throw new InvalidOperationException(
                    "Independent reconciliation failed synthetic conservation checks.");
            var current = report.TotalClosing.Numerator + "/" +
                report.TotalClosing.Denominator + ":" +
                report.TotalFreight.Numerator + "/" +
                report.TotalFreight.Denominator + ":" +
                string.Join(",", report.Buyers.Select(x =>
                    x.Buyer.Value + "=" + x.ClosingGrossCash.Numerator));
            if (run >= 0)
            {
                if (signature is null) signature = current;
                else if (signature != current)
                    throw new InvalidOperationException(
                        "Independent audit must remain deterministic.");
                times[run] = ms;
                allocated[run] = bytes;
            }
            if (run == 2)
                Console.WriteLine(
                    $"CASE=buyers-{count};transfers={report.Transfers.Count};" +
                    $"corridors={count / 4};" +
                    $"opening={report.TotalOpening.Numerator}/" +
                        $"{report.TotalOpening.Denominator};" +
                    $"closing={report.TotalClosing.Numerator}/" +
                        $"{report.TotalClosing.Denominator};" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocated.OrderBy(x => x).ElementAt(1)}");
        }
    }
}
