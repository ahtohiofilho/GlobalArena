using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Read-only single-settlement projection regression. Cases vary repeated
/// projection calls, NOT synthetic buyer count; no claimed scalability to
/// a full active national economy. Worldgen is outside the timed region.
/// </summary>
public static class GaVs52AbstractPurchasingBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS52 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS52-ABSTRACT-PURCHASING-READONLY");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("WARMUPS=1;SAMPLES=3;PERFORMANCE_THRESHOLDS=none");
            var game = CanonicalPhysicalTacticalSession.Start(
                20261012UL, new GoldbergParameters(3, 0),
                new GoldbergParameters(18, 0),
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C,
                PhysicalFoodMarketMode.LocalCashSettlement,
                PhysicalWorkerMovementMode.TimedAdjacent,
                PhysicalCivilizationIdentityMode.BornWithPopulation);
            var buyer = new EconomicPointId(101);
            var supplier = new EconomicPointId(201);
            var food = new CommodityId(7);
            var flows = new EconomicProductionDemandResult(
                new[] { new EconomicSupplyFlow(supplier, food,
                    EconomicActivityKind.Agriculture, Unit) },
                new[] { new EconomicDemandFlow(buyer, food, Unit) });
            var anchors = new[] { new ExperimentalAbstractBuyerAnchor(
                buyer, game.InitialTile, new FoodTransportCostFraction(1, 1)) };
            var producerAnchors = new[] {
                new ExperimentalAbstractProducerAnchor(supplier, game.InitialTile)
            };
            var prices = new[] {
                new ExperimentalBuyerCommodityPrice(
                    buyer, food, new FoodTransportCostFraction(10, 1))
            };
            var policy = new ExperimentalAbstractPurchasingPolicy(
                "VS52-BENCHMARK-ABSTRACT-BASIC", 1,
                new FoodTransportCostFraction(5, 1),
                new FoodTransportCostFraction(5, 8));
            var projector = new ExperimentalAbstractPurchasingPowerProjector();
            var originalHash = game.CurrentHash;
            var originalSave = game.ExportSaveJson();

            foreach (var count in new[] { 8, 32, 128 })
            {
                var times = new double[3];
                var bytes = new long[3];
                for (var sample = -1; sample < 3; sample++)
                {
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    for (var i = 0; i < count; i++)
                    {
                        var p = projector.Project(game, flows, anchors,
                            producerAnchors, [], prices, policy);
                        if (p.Buyers.Count != 1 ||
                            p.Producers.Count != 1 ||
                            p.UnresolvedCarriers.Count != 0 ||
                            p.ResidentWorkersInWorld != 8 ||
                            p.TravellingWorkers != 0 ||
                            p.TotalTemporaryPurchasingUnits.CompareTo(
                                new FoodTransportCostFraction(40, 1)) != 0 ||
                            p.Buyers[0].TemporaryCycleBudget.SpendableCash.CompareTo(
                                new FoodTransportCostFraction(35, 1)) != 0)
                            throw new InvalidOperationException(
                                "Projected abstract capacity diverged from physical workers.");
                    }
                    var elapsed = Stopwatch.GetElapsedTime(started)
                        .TotalMilliseconds;
                    var consumed = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (sample >= 0)
                    {
                        times[sample] = elapsed;
                        bytes[sample] = consumed;
                    }
                }
                if (game.CurrentHash != originalHash ||
                    game.ExportSaveJson() != originalSave ||
                    game.Events.Count != 0)
                    throw new InvalidOperationException(
                        "The abstract purchasing projection modified the canonical world.");
                Console.WriteLine($"CASE=readonly-projections-{count};" +
                    $"physical_buyers=1;workers=8;" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={bytes.OrderBy(x => x).ElementAt(1)}");
            }
            Console.WriteLine("RESULT=PASS_GA_VS52_ABSTRACT_PURCHASING_BENCHMARK");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("RESULT=FAIL_GA_VS52_ABSTRACT_PURCHASING_BENCHMARK");
            return 1;
        }
#endif
    }
}
