using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Repeated, one-physical-buyer abstract budget + native quote normalization
/// + VS45 alpha. 8/32/128 represent repetitions, NOT buyer population or
/// logistics scale. Does not price nonlinear aggregate freight ex post.
/// </summary>
public static class GaVs53MarketUnitCalibrationBenchmarkHarness
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
            Console.WriteLine("BENCHMARK=GA-VS53-EXPLICIT-ABSTRACT-MARKET-CALIBRATION");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("WARMUPS=1;SAMPLES=3;PERFORMANCE_THRESHOLDS=none");
            Measure();
            Console.WriteLine("RESULT=PASS_GA_VS53_ABSTRACT_MARKET_CALIBRATION");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("RESULT=FAIL_GA_VS53_ABSTRACT_MARKET_CALIBRATION");
            return 1;
        }
#endif
    }

    private static void Measure()
    {
        var session = CanonicalPhysicalTacticalSession.Start(
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
        var stone = new CommodityId(8);
        var flows = new EconomicProductionDemandResult(
            [
                new EconomicSupplyFlow(supplier, food,
                    EconomicActivityKind.Agriculture, 3 * Unit),
                new EconomicSupplyFlow(supplier, stone,
                    EconomicActivityKind.Mining, Unit)
            ],
            [
                new EconomicDemandFlow(buyer, food, 3 * Unit),
                new EconomicDemandFlow(buyer, stone, Unit)
            ]);
        var tech = new ExperimentalFreightTechnologyPricing(
            101, 7, 53, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var key = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(
                new GoldbergParameters(18, 0), new StrategicEdgeId(1)),
            101, 7);
        ExperimentalSimultaneousFreightLane Lane(
            CommodityId commodity, EconomicActivityKind activity,
            ulong qty, ulong id) =>
            new(new EconomicSupplyFlow(supplier, commodity, activity, qty),
                new EconomicDemandFlow(buyer, commodity, qty),
                [ExperimentalSharedFreightOption.Synthetic(
                    id, new StrategicEconomicRouteKey(supplier, buyer),
                    tech, 1_000_000, [key])]);
        var lanes = new[] {
            Lane(food, EconomicActivityKind.Agriculture, 3 * Unit, 17),
            Lane(stone, EconomicActivityKind.Mining, Unit, 18)
        };
        var nativePrices = new[] {
            new ExperimentalBuyerCommodityPrice(buyer, food,
                new FoodTransportCostFraction(4, 1)),
            new ExperimentalBuyerCommodityPrice(buyer, stone,
                new FoodTransportCostFraction(4, 1))
        };
        var nativeFreight = new[] {
            new ExperimentalNativeFreightPriceEstimate(supplier, buyer,
                food, 17, 53, new FoodTransportCostFraction(2, 1)),
            new ExperimentalNativeFreightPriceEstimate(supplier, buyer,
                stone, 18, 53, new FoodTransportCostFraction(2, 1))
        };
        var buying = new ExperimentalAbstractPurchasingPolicy(
            "GA-VS53-REF-RESIDENT", 1,
            new FoodTransportCostFraction(5, 1),
            new FoodTransportCostFraction(5, 8));
        var calibration = new ExperimentalAbstractMarketUnitCalibration(
            "GA-VS53-REF-GOODS-FREIGHT", 1,
            buying.PolicyId, buying.Version,
            "ABSTRACT-CYCLE-UNIT", "GOODS-QUOTE", "FREIGHT-QUOTE",
            new FoodTransportCostFraction(2, 1),
            new FoodTransportCostFraction(1, 1));
        var buyers = new[] { new ExperimentalAbstractBuyerAnchor(
            buyer, session.InitialTile, new FoodTransportCostFraction(1, 1)) };
        var producers = new[] {
            new ExperimentalAbstractProducerAnchor(supplier, session.InitialTile)
        };
        var originalHash = session.CurrentHash;
        var originalSave = session.ExportSaveJson();
        var projector = new ExperimentalAbstractMarketUnitCalibrator();

        foreach (var count in new[] { 8, 32, 128 })
        {
            var samples = new double[3];
            var allocations = new long[3];
            for (var iteration = -1; iteration < 3; iteration++)
            {
                var startBytes = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < count; i++)
                {
                    var preview = projector.Preview(session, flows, buyers,
                        producers, lanes, nativePrices, nativeFreight,
                        buying, calibration);
                    if (preview.Purchasing.Buyers.Count != 1 ||
                        preview.Purchasing.UnresolvedCarriers.Count != 2 ||
                        preview.DeliveredPrices.Count != 2 ||
                        preview.Purchasing.TravellingWorkers != 0 ||
                        preview.Alpha.Buyers[0].Alpha.CompareTo(
                            new FoodTransportCostFraction(7, 8)) != 0 ||
                        preview.Alpha.Buyers[0].ScaledEstimatedSpending.CompareTo(
                            new FoodTransportCostFraction(35, 1)) != 0)
                        throw new InvalidOperationException(
                            "Market quote calibration disagrees with physical resident purchasing or alpha.");
                }
                var elapsed = Stopwatch.GetElapsedTime(start)
                    .TotalMilliseconds;
                var allocated = GC.GetAllocatedBytesForCurrentThread() -
                    startBytes;
                if (iteration >= 0)
                {
                    samples[iteration] = elapsed;
                    allocations[iteration] = allocated;
                }
            }
            if (session.CurrentHash != originalHash ||
                session.ExportSaveJson() != originalSave ||
                session.Events.Count != 0)
                throw new InvalidOperationException(
                    "A read-only unit calibration mutated the canonical world.");
            Console.WriteLine(
                $"CASE=calibration-repetitions-{count};physical_buyers=1;" +
                $"native_goods=2;unresolved_carriers=2;" +
                $"median_ms={samples.OrderBy(x => x).ElementAt(1):F3};" +
                $"median_allocated_bytes=" +
                    $"{allocations.OrderBy(x => x).ElementAt(1)}");
        }
    }
}
