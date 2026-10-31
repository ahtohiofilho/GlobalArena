using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Repeated conversion + alpha scenarios for one physical buyer, not a
/// multi-city scalability or market-clearing benchmark. Worldgen excluded.
/// </summary>
public static class GaVs53AbstractPriceFreightCalibrationBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS53 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS53-ABSTRACT-PRICE-FREIGHT-CALIBRATION");
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
            var producer = new EconomicPointId(201);
            var food = new CommodityId(7);
            var stone = new CommodityId(8);
            var flows = new EconomicProductionDemandResult(
                new[] {
                    new EconomicSupplyFlow(producer, food,
                        EconomicActivityKind.Agriculture, Unit)
                },
                new[] {
                    new EconomicDemandFlow(buyer, food, 3 * Unit),
                    new EconomicDemandFlow(buyer, stone, Unit)
                });
            var prices = new[] {
                new ExperimentalBuyerCommodityPrice(
                    buyer, food, new FoodTransportCostFraction(4, 1)),
                new ExperimentalBuyerCommodityPrice(
                    buyer, stone, new FoodTransportCostFraction(4, 1))
            };
            var purchasing = new ExperimentalAbstractPurchasingPowerProjector()
                .Project(game, flows,
                    new[] {
                        new ExperimentalAbstractBuyerAnchor(
                            buyer, game.InitialTile,
                            new FoodTransportCostFraction(1, 1))
                    },
                    new[] {
                        new ExperimentalAbstractProducerAnchor(
                            producer, game.InitialTile)
                    },
                    Array.Empty<ExperimentalSimultaneousFreightLane>(),
                    prices,
                    new ExperimentalAbstractPurchasingPolicy(
                        "VS53-BENCH-PURCHASING", 1,
                        new FoodTransportCostFraction(5, 1),
                        new FoodTransportCostFraction(5, 8)));
            var quotes = new[] {
                new ExperimentalAbstractNominalDeliveredQuote(
                    buyer, food, new FoodTransportCostFraction(4, 1),
                    new FoodTransportCostFraction(1, 1), "VS40-NOMINAL"),
                new ExperimentalAbstractNominalDeliveredQuote(
                    buyer, stone, new FoodTransportCostFraction(4, 1),
                    new FoodTransportCostFraction(1, 1), "VS40-NOMINAL")
            };
            var policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS53-BENCH-CALIBRATION", 1,
                "VS53-BENCH-PURCHASING", 1, "VS40-NOMINAL",
                new FoodTransportCostFraction(2, 1));
            var calibrator = new ExperimentalAbstractPriceFreightCalibrator();
            var hash = game.CurrentHash;
            var saved = game.ExportSaveJson();

            foreach (var count in new[] { 8, 32, 128 })
            {
                var times = new double[3];
                var allocations = new long[3];
                for (var sample = -1; sample < 3; sample++)
                {
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    for (var i = 0; i < count; i++)
                    {
                        var result = calibrator.Project(
                            purchasing, flows, quotes, policy);
                        var plan = result.AlphaDemand.Buyers.Single();
                        if (result.CanAuthorizeRealSettlement ||
                            result.Quotes.Count != 2 ||
                            result.TotalBaselineCommodityUnits.CompareTo(
                                new FoodTransportCostFraction(32, 1)) != 0 ||
                            result.TotalBaselineFreightUnits.CompareTo(
                                new FoodTransportCostFraction(8, 1)) != 0 ||
                            result.TotalBaselineDeliveredUnits.CompareTo(
                                new FoodTransportCostFraction(40, 1)) != 0 ||
                            result.TotalSpendablePurchasingUnits.CompareTo(
                                new FoodTransportCostFraction(35, 1)) != 0 ||
                            plan.Alpha.CompareTo(
                                new FoodTransportCostFraction(7, 8)) != 0)
                            throw new InvalidOperationException(
                                "Calibrated nominal prices and freight diverged.");
                    }
                    var elapsed = Stopwatch.GetElapsedTime(started)
                        .TotalMilliseconds;
                    var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (sample >= 0)
                    {
                        times[sample] = elapsed;
                        allocations[sample] = bytes;
                    }
                }
                if (game.CurrentHash != hash ||
                    game.ExportSaveJson() != saved || game.Events.Count != 0)
                    throw new InvalidOperationException(
                        "Read-only calibration modified world state.");

                Console.WriteLine($"CASE=calibration-projections-{count};" +
                    $"physical_buyers=1;workers=8;" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
            }
            Console.WriteLine("RESULT=PASS_GA_VS53_ABSTRACT_PRICE_FREIGHT_CALIBRATION");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("RESULT=FAIL_GA_VS53_ABSTRACT_PRICE_FREIGHT_CALIBRATION");
            return 1;
        }
#endif
    }
}
