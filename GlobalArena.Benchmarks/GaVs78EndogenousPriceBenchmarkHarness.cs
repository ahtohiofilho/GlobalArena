using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// VS78 local-only benchmark of actual session-derived production and
/// resident demand-pressure, with NO cash budget or constructed freight.
/// Does not claim a full-world market or authorise remote settlement.
/// </summary>
public static class GaVs78EndogenousPriceBenchmarkHarness
{
    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS78 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS78-ENDOGENOUS-LOCAL-PRICE");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("WARMUPS=1;SAMPLES=3;PERFORMANCE_THRESHOLDS=none");

            var session = CanonicalPhysicalTacticalSession.Start(
                20261012UL, new GoldbergParameters(3, 0),
                new GoldbergParameters(18, 0),
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C,
                PhysicalFoodMarketMode.LocalCashSettlement,
                PhysicalWorkerMovementMode.TimedAdjacent,
                PhysicalCivilizationIdentityMode.BornWithPopulation);
            var food = new CommodityId(7);
            var wood = new CommodityId(8);
            var stone = new CommodityId(9);
            var policy = new CanonicalPrimitiveSiteDiscoveryPolicy(
                new EconomicPrimitiveCommodityMapping(food, wood, stone),
                new[] {
                    new CanonicalPrimitiveDemandRate(food,
                        checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                    new CanonicalPrimitiveDemandRate(wood, 125_000),
                    new CanonicalPrimitiveDemandRate(stone, 250_000)
                }, new FoodTransportCostFraction(1, 1));

            var originalSave = session.ExportSaveJson();
            var originalHash = session.CurrentHash;
            for (var testCase = 0; testCase < 3; testCase++)
            {
                var repetitions = new[] { 8, 32, 128 }[testCase];
                var times = new double[3];
                var allocations = new long[3];
                for (var sample = -1; sample < 3; sample++)
                {
                    var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    for (var i = 0; i < repetitions; i++)
                    {
                        var sites = session.DiscoverPrimitiveEconomicSites(policy);
                        var selected = sites.Flows.SupplyFlows
                            .Where(x => x.RawQuantity > 0)
                            .Take(3).Select(x =>
                                new CanonicalEndogenousProductionDeclaration(
                                    x.EconomicPointId, x.CommodityId,
                                    x.Activity, x.RawQuantity, 0UL)).ToArray();
                        if (selected.Length == 0)
                            throw new InvalidOperationException(
                                "VS78 benchmark has no real primitive producer.");
                        var result = CanonicalEndogenousLocalPriceComparison.FromSites(
                            session, sites, selected);
                        var expected = selected.Aggregate(0UL,
                            (sum, x) => checked(sum + x.ProducedRaw));
                        var observed = result.Sources.Aggregate(0UL,
                            (sum, x) => checked(sum + x.OfferedAndLocallyAbsorbedRaw));
                        if (expected != observed ||
                            result.Gentle.Commodities.Count == 0 ||
                            result.Steep.Commodities.Count == 0 ||
                            result.UsesAnExogenousMonetaryBudget ||
                            result.AppliesPhysicalDemandAsConsumptionCap ||
                            result.IsRemoteFreightPriceCalibrated ||
                            result.CanAuthorizeRealSettlement ||
                            result.IsActualConsumption ||
                            result.CreatesActualInventory)
                            throw new InvalidOperationException(
                                "VS78 endogenous pricing did not preserve local output or read-only boundary.");
                        result.VerifyIndependent();
                        result.RequireCurrent(session);
                    }
                    var elapsed = Stopwatch.GetElapsedTime(started)
                        .TotalMilliseconds;
                    var bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                    if (sample >= 0)
                    {
                        times[sample] = elapsed;
                        allocations[sample] = bytes;
                    }
                }
                if (session.CurrentHash != originalHash ||
                    session.ExportSaveJson() != originalSave)
                    throw new InvalidOperationException(
                        "Endogenous pricing changed the canonical physical session/save.");
                Console.WriteLine($"CASE=endogenous-local-price-{repetitions};" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
            }
            Console.WriteLine("RESULT=PASS_GA_VS78_ENDOGENOUS_PRICE");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_GA_VS78_ENDOGENOUS_PRICE");
            return 1;
        }
#endif
    }
}
