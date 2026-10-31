using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Synthetic budget-constrained VS43 comparison with stable source/demand
/// identities, several buyers and shared infrastructure. It is NOT a live
/// cash ledger, production run or measured proof of economic equilibrium.
/// </summary>
public static class GaVs44CashBudgetBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(18, 0);
    private sealed class FlatValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong raw) =>
            new(100, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong raw) =>
            new((BigInteger)100 * raw, Unit);
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS44 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS44-SYNTHETIC-CASH-BUDGET-ECONOMIC-REFERENCE");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"ENV_CPUS={Environment.ProcessorCount}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var count in new[] { 4, 8, 12 })
                Measure(count);
            Console.WriteLine("RESULT=PASS_GA_VS44_CASH_BUDGET_BENCHMARK");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.WriteLine("RESULT=FAIL_GA_VS44_CASH_BUDGET_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int laneCount)
    {
        var item = new CommodityId(7);
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(item, "food", 1) });
        var calibration = new ExperimentalFreightTechnologyPricing(
            101, 7, 44, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var hub = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(1)), 101, 7);
        var supply = new List<EconomicSupplyFlow>();
        var demand = new List<EconomicDemandFlow>();
        var values = new List<EconomicTurnCommodityValueOffer>();
        var reservations = new List<EconomicTurnSourceReservation>();
        var candidates = new List<ExperimentalSimultaneousFreightLane>();
        for (var i = 0; i < laneCount; i++)
        {
            var source = new EconomicPointId(1UL + (ulong)i);
            var destination = new EconomicPointId(1001UL + (ulong)(i % 4));
            var s = new EconomicSupplyFlow(
                source, item, EconomicActivityKind.Agriculture, Unit);
            supply.Add(s);
            reservations.Add(new EconomicTurnSourceReservation(
                source, item, s.Activity, 1));
            if (demand.All(x => x.EconomicPointId != destination))
            {
                demand.Add(new EconomicDemandFlow(destination, item, 3 * Unit));
                values.Add(new EconomicTurnCommodityValueOffer(
                    destination, item, new FlatValue()));
            }
            var alternate = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(Fine,
                    new StrategicEdgeId(2UL + (ulong)(i % 7))), 101, 7);
            var route = new StrategicEconomicRouteKey(source, destination);
            candidates.Add(new ExperimentalSimultaneousFreightLane(s,
                demand.First(x => x.EconomicPointId == destination),
                new[] {
                    ExperimentalSharedFreightOption.Synthetic(
                        1, route, calibration, 1_000_000, new[] { hub }),
                    ExperimentalSharedFreightOption.Synthetic(
                        2, route, calibration, 2_000_000, new[] { alternate })
                }));
        }

        var flows = new EconomicProductionDemandResult(supply, demand);
        var budget = new ExperimentalBuyerCashSnapshot(
            demand.Select(x => new ExperimentalBuyerCashBudget(
                x.EconomicPointId, new FoodTransportCostFraction(42, 1),
                new FoodTransportCostFraction(2, 1))),
            demand.Select(x => new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(12, 1))));
        var policy = new ExperimentalSharedFreightCongestionPolicy(8, 1_000_000);
        var solver = new ExperimentalSimultaneousFreightReference();

        var times = new double[3];
        var bytes = new long[3];
        string? signature = null;
        for (var run = -1; run < 3; run++)
        {
            var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            var t = Stopwatch.GetTimestamp();
            var comparison = solver.Compare(
                flows, catalog, values, reservations, candidates, policy,
                Unit, maxLocalPasses: 4, maxExactConfigurations: 128,
                cashSnapshot: budget);
            var elapsed = Stopwatch.GetElapsedTime(t).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            if ((laneCount == 4) != (comparison.Exact is not null) ||
                comparison.Coordinated.NetEconomicValue.CompareTo(
                    comparison.Independent.NetEconomicValue) < 0 ||
                (comparison.Exact is not null &&
                    comparison.Coordinated.NetEconomicValue.CompareTo(
                        comparison.Exact.NetEconomicValue) > 0))
                throw new InvalidOperationException("VS44 failed its bounded cash reference gate.");
            foreach (var row in comparison.Coordinated.CashLedger)
            {
                if (row.TotalCommitment.CompareTo(row.SpendableCash) > 0 ||
                    row.ProtectedResidual.CompareTo(row.GrossCash) > 0)
                    throw new InvalidOperationException("Cash spent exceeds available budget.");
            }
            var current = comparison.Coordinated.NetEconomicValue.Numerator +
                "/" + comparison.Coordinated.NetEconomicValue.Denominator +
                ":" + string.Join(",", comparison.Coordinated.Trades.Select(x =>
                    x.Source.Value + ":" + x.Destination.Value + ":" +
                    x.ServiceId + ":" + x.RawQuantity)) +
                ":" + comparison.CandidateEvaluations;
            if (run >= 0)
            {
                if (signature is null) signature = current;
                else if (signature != current)
                    throw new InvalidOperationException(
                        "Budgeted market choices must be deterministic.");
                times[run] = elapsed;
                bytes[run] = allocated;
            }
            if (run == 2)
            {
                Console.WriteLine(
                    $"CASE=lanes-{laneCount};possible={comparison.PossibleConfigurations};" +
                    $"evaluations={comparison.CandidateEvaluations};" +
                    $"buyers={comparison.Coordinated.CashLedger.Count};" +
                    $"accepted_trades={comparison.Coordinated.Trades.Count};" +
                    $"exact_available={(comparison.Exact is null ? 0 : 1)};" +
                    $"independent_net={comparison.Independent.NetEconomicValue.Numerator}/" +
                        $"{comparison.Independent.NetEconomicValue.Denominator};" +
                    $"coordinated_net={comparison.Coordinated.NetEconomicValue.Numerator}/" +
                        $"{comparison.Coordinated.NetEconomicValue.Denominator};" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={bytes.OrderBy(x => x).ElementAt(1)}");
            }
        }
    }
}
