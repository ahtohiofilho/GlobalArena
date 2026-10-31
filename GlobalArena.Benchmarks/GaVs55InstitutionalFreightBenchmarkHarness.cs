using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// One buyer, one service and one institutionally divided freight lot.
/// 8/32/128 are repetitions of the SAME read-only scenario, not entities.
/// </summary>
public static class GaVs55InstitutionalFreightBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    private sealed class BenchAuthority :
        IExperimentalTransportInstitutionalRegistryAuthority
    {
        public bool AcceptReadOnlyRegistry(
            ExperimentalTransportInstitutionalRegistry registry) =>
            string.Equals(registry.RegistryId, "VS55-BENCH-REGISTRY",
                StringComparison.Ordinal) &&
            registry.RegistryVersion == 1;
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS55 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS55-INSTITUTIONAL-FREIGHT");
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
            var buyer = new EconomicPointId(101);
            var supplier = new EconomicPointId(201);
            var food = new CommodityId(7);
            var flows = new EconomicProductionDemandResult(
                new[] {
                    new EconomicSupplyFlow(supplier, food,
                        EconomicActivityKind.Agriculture, Unit)
                },
                new[] { new EconomicDemandFlow(buyer, food, Unit) });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(food, "food", 1)
            });
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                    new StrategicEdgeId(1)), 101, 7);
            var option = ExperimentalSharedFreightOption.Synthetic(
                17, new StrategicEconomicRouteKey(supplier, buyer),
                new ExperimentalFreightTechnologyPricing(
                    101, 7, 55, ExperimentalLandInfrastructureKind.StoneRoad,
                    1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
                1_000_000, new[] { corridor });
            var lanes = new[] {
                new ExperimentalSimultaneousFreightLane(
                    flows.SupplyFlows[0], flows.DemandFlows[0], new[] { option })
            };
            var purchasing = new ExperimentalAbstractPurchasingPowerProjector()
                .Project(session, flows,
                    new[] {
                        new ExperimentalAbstractBuyerAnchor(
                            buyer, session.InitialTile,
                            new FoodTransportCostFraction(1, 1))
                    },
                    new[] {
                        new ExperimentalAbstractProducerAnchor(
                            supplier, session.InitialTile)
                    },
                    lanes,
                    new[] {
                        new ExperimentalBuyerCommodityPrice(
                            buyer, food, new FoodTransportCostFraction(1, 1))
                    },
                    new ExperimentalAbstractPurchasingPolicy(
                        "VS55-BENCH-PURCHASING", 1,
                        new FoodTransportCostFraction(5, 1),
                        new FoodTransportCostFraction(5, 8)));
            var policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS55-BENCH-CALIBRATION", 1,
                "VS55-BENCH-PURCHASING", 1, "VS40-NOMINAL",
                new FoodTransportCostFraction(1, 1));
            var quotes = new[] {
                new ExperimentalAbstractNominalDeliveredQuote(
                    buyer, food, new FoodTransportCostFraction(1, 1),
                    new FoodTransportCostFraction(1, 1), "VS40-NOMINAL")
            };
            var shipments = new[] {
                new ExperimentalAbstractPhysicalFreightDelivery(
                    1, supplier, EconomicActivityKind.Agriculture,
                    buyer, food, Unit, option)
            };
            var operatorInstitution = new ExperimentalTransportInstitution(
                "OPERATOR", ExperimentalTransportInstitutionKind.CommercialOperator);
            var cooperative = new ExperimentalTransportInstitution(
                "COOPERATIVE", ExperimentalTransportInstitutionKind.Cooperative);
            var community = new ExperimentalTransportInstitution(
                "COMMUNITY", ExperimentalTransportInstitutionKind.Community);
            var registry = new ExperimentalTransportInstitutionalRegistry(
                "VS55-BENCH-REGISTRY", 1, session.CurrentHash, 7,
                "VS55-BENCH-PURCHASING", 1,
                "VS55-BENCH-CALIBRATION", 1, "VS40-NOMINAL",
                new[] {
                    new ExperimentalTransportServiceEntitlement(
                        supplier, buyer, 17, operatorInstitution,
                        new[] {
                            new ExperimentalTransportFreightRevenueShare(
                                cooperative, new FoodTransportCostFraction(2, 3)),
                            new ExperimentalTransportFreightRevenueShare(
                                community, new FoodTransportCostFraction(1, 3))
                        })
                });
            var reconciler = new ExperimentalTransportInstitutionalFreightProjector();
            var authority = new BenchAuthority();
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                1, 1_000_000);
            var initialHash = session.CurrentHash;
            var initialSave = session.ExportSaveJson();
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
                        var result = reconciler.Project(
                            session, purchasing, flows, catalog, quotes, policy,
                            "VS40-NOMINAL", lanes, shipments, congestion,
                            registry, authority);
                        if (result.CanAuthorizeRealSettlement ||
                            result.Lots.Count != 1 ||
                            result.InstitutionalReceivables.Count != 2 ||
                            result.AbstractBuyerFreightCharges.CompareTo(
                                new FoodTransportCostFraction(2, 1)) != 0 ||
                            result.AbstractInstitutionalFreightReceivables.CompareTo(
                                new FoodTransportCostFraction(2, 1)) != 0)
                            throw new InvalidOperationException(
                                "Institutional revenue and physical freight diverged.");
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
                if (session.CurrentHash != initialHash ||
                    session.ExportSaveJson() != initialSave ||
                    session.Events.Count != 0)
                    throw new InvalidOperationException(
                        "Read-only institutional rights changed physical world state.");
                Console.WriteLine($"CASE=institutional-projections-{count};" +
                    $"physical_buyers=1;lots=1;operators=1;beneficiaries=2;" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
            }
            Console.WriteLine("RESULT=PASS_GA_VS55_INSTITUTIONAL_FREIGHT");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("RESULT=FAIL_GA_VS55_INSTITUTIONAL_FREIGHT");
            return 1;
        }
#endif
    }
}
