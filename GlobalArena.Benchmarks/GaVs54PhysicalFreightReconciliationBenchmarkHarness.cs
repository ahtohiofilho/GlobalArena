using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// One physical settlement, one buyer, two goods/lots sharing one VS42
/// corridor. Counts repeated reconciliations, NOT distinct buyers or routes.
/// </summary>
public static class GaVs54PhysicalFreightReconciliationBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS54 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS54-PHYSICAL-FREIGHT-ABSTRACT");
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
                        EconomicActivityKind.Agriculture, Unit),
                    new EconomicSupplyFlow(producer, stone,
                        EconomicActivityKind.Mining, Unit)
                },
                new[] {
                    new EconomicDemandFlow(buyer, food, Unit),
                    new EconomicDemandFlow(buyer, stone, Unit)
                });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(food, "food", 1),
                new FoodCommodityDefinition(stone, "stone", 1)
            });
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            var tech = new ExperimentalFreightTechnologyPricing(
                101, 7, 54, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
            var route = new StrategicEconomicRouteKey(producer, buyer);
            var opt1 = ExperimentalSharedFreightOption.Synthetic(
                17, route, tech, 1_000_000, new[] { corridor });
            var opt2 = ExperimentalSharedFreightOption.Synthetic(
                18, route, tech, 1_000_000, new[] { corridor });
            var lanes = new[] {
                new ExperimentalSimultaneousFreightLane(
                    flows.SupplyFlows[0], flows.DemandFlows[0], new[] { opt1 }),
                new ExperimentalSimultaneousFreightLane(
                    flows.SupplyFlows[1], flows.DemandFlows[1], new[] { opt2 })
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
                    lanes,
                    new[] {
                        new ExperimentalBuyerCommodityPrice(
                            buyer, food, new FoodTransportCostFraction(1, 1)),
                        new ExperimentalBuyerCommodityPrice(
                            buyer, stone, new FoodTransportCostFraction(1, 1))
                    },
                    new ExperimentalAbstractPurchasingPolicy(
                        "VS54-BENCH-PURCHASING", 1,
                        new FoodTransportCostFraction(5, 1),
                        new FoodTransportCostFraction(5, 8)));
            var quotes = new[] {
                new ExperimentalAbstractNominalDeliveredQuote(
                    buyer, food, new FoodTransportCostFraction(1, 1),
                    new FoodTransportCostFraction(1, 1), "VS40-NOMINAL"),
                new ExperimentalAbstractNominalDeliveredQuote(
                    buyer, stone, new FoodTransportCostFraction(1, 1),
                    new FoodTransportCostFraction(1, 1), "VS40-NOMINAL")
            };
            var policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS54-BENCH-CALIBRATION", 1,
                "VS54-BENCH-PURCHASING", 1, "VS40-NOMINAL",
                new FoodTransportCostFraction(1, 1));
            var selected = new[] {
                new ExperimentalAbstractPhysicalFreightDelivery(
                    1, producer, EconomicActivityKind.Agriculture,
                    buyer, food, Unit, opt1),
                new ExperimentalAbstractPhysicalFreightDelivery(
                    2, producer, EconomicActivityKind.Mining,
                    buyer, stone, Unit, opt2)
            };
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                1, 1_000_000);
            var reconciler = new ExperimentalAbstractPhysicalFreightReconciler();
            var initialHash = game.CurrentHash;
            var initialSave = game.ExportSaveJson();

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
                        var result = reconciler.Project(purchasing, flows,
                            catalog, quotes, policy, "VS40-NOMINAL",
                            selected, congestion);
                        if (result.CanAuthorizeRealSettlement ||
                            !result.AllBuyersCanCoverHypotheticalCharges ||
                            result.Deliveries.Count != 2 ||
                            result.CorridorLoads.Count != 1 ||
                            result.NominalPhysicalFreight.CompareTo(
                                new FoodTransportCostFraction(10, 1)) != 0 ||
                            result.AbstractDeliveredCharges.CompareTo(
                                new FoodTransportCostFraction(12, 1)) != 0)
                            throw new InvalidOperationException(
                                "VS54 shared freight/abstract charge reconciliation diverged.");
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
                if (game.CurrentHash != initialHash ||
                    game.ExportSaveJson() != initialSave ||
                    game.Events.Count != 0)
                    throw new InvalidOperationException(
                        "Read-only physical freight reconciliation mutated the world.");
                Console.WriteLine($"CASE=physical-reconciliations-{count};" +
                    $"physical_buyers=1;lots=2;corridors=1;" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
            }
            Console.WriteLine("RESULT=PASS_GA_VS54_PHYSICAL_FREIGHT_RECONCILIATION");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("RESULT=FAIL_GA_VS54_PHYSICAL_FREIGHT_RECONCILIATION");
            return 1;
        }
#endif
    }
}
