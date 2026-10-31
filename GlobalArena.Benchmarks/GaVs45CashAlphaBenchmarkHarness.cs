using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Synthetic, deterministic budget-planning workload. This measures
/// proportional alpha projection, NOT the full economic solver, freight
/// routing or actual V14 save replay. No wall-time acceptance threshold.
/// </summary>
public static class GaVs45CashAlphaBenchmarkHarness
{
    private const ulong RawUnit = EconomicTransferPolicy.FixedPointDenominator;

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS45 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS45-SYNTHETIC-CASH-ALPHA");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var count in new[] { 8, 32, 128 })
                Measure(count);
            Console.WriteLine("RESULT=PASS_GA_VS45_CASH_ALPHA_BENCHMARK");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.WriteLine("RESULT=FAIL_GA_VS45_CASH_ALPHA_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int buyerCount)
    {
        var demands = new List<EconomicDemandFlow>();
        var budgets = new List<ExperimentalBuyerCashBudget>();
        var prices = new List<ExperimentalDeliveredDemandEstimate>();
        for (var buyerNumber = 1; buyerNumber <= buyerCount; buyerNumber++)
        {
            var buyer = new EconomicPointId((ulong)buyerNumber);
            var gross = new FoodTransportCostFraction(40, 1);
            var h = new FoodTransportCostFraction(5, 1);
            budgets.Add(new ExperimentalBuyerCashBudget(buyer, gross, h));
            for (uint c = 1; c <= 4; c++)
            {
                var commodity = new CommodityId(c);
                demands.Add(new EconomicDemandFlow(
                    buyer, commodity, RawUnit));
                prices.Add(new ExperimentalDeliveredDemandEstimate(
                    buyer, commodity, new FoodTransportCostFraction(10, 1),
                    new FoodTransportCostFraction(1, 1)));
            }
        }
        var flows = new EconomicProductionDemandResult(
            Array.Empty<EconomicSupplyFlow>(), demands);
        var projector = new ExperimentalCashDemandAlphaPlanner();
        var elapsed = new double[3];
        var allocations = new long[3];
        string? signature = null;
        for (var run = -1; run < 3; run++)
        {
            var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            var beforeTime = Stopwatch.GetTimestamp();
            var plan = projector.Project(flows, budgets, prices);
            var ms = Stopwatch.GetElapsedTime(beforeTime).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            if (plan.Buyers.Count != buyerCount ||
                plan.ProjectedDemand.Count != 4 * buyerCount ||
                plan.Buyers.Any(b => b.ScaledEstimatedSpending.CompareTo(
                    b.SpendableBudget) > 0) ||
                plan.Buyers.Any(b => b.Alpha.CompareTo(
                    new FoodTransportCostFraction(35, 44)) != 0))
                throw new InvalidOperationException(
                    "Synthetic VS45 alpha result violates conservation or cash limits.");
            var current = string.Join("|", plan.Buyers.Select(b =>
                b.Buyer.Value + ":" + b.Alpha.Numerator + "/" +
                b.Alpha.Denominator + ":" + b.ScaledEstimatedSpending.Numerator +
                "/" + b.ScaledEstimatedSpending.Denominator));
            if (run >= 0)
            {
                if (signature is null) signature = current;
                else if (signature != current)
                    throw new InvalidOperationException(
                        "VS45 alpha budget forecasts must be deterministic.");
                elapsed[run] = ms;
                allocations[run] = bytes;
            }
            if (run == 2)
                Console.WriteLine(
                    $"CASE=buyers-{buyerCount};goods_per_buyer=4;" +
                    $"alpha=35/44;demand_rows={plan.ProjectedDemand.Count};" +
                    $"median_ms={elapsed.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
        }
    }
}
