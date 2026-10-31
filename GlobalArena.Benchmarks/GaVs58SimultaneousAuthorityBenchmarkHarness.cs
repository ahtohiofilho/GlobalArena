using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Repeats verification of ONE constructed fine-grid road, ONE institutionally
/// authorized service and ONE delivery. Does not measure general market scale.
/// </summary>
public static class GaVs58SimultaneousAuthorityBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS58 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS58-BUILT-INSTITUTIONAL-NETWORK");
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
            var from = session.InitialTile;
            var to = session.AdjacentWorkable(from)[0];
            session.OrderWorkerTransit(from, to,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2);
            session.AdvanceMovementTurn();
            session.AdvanceMovementTurn();
            var source = new EconomicPointId(201);
            var buyer = new EconomicPointId(101);
            var food = new CommodityId(7);
            var edge = session.FineWorld.StrategicTopology.Edges.First(x =>
                x.IncidentCellIds.Contains(from.FineStrategicCellId) &&
                x.IncidentCellIds.Contains(to.FineStrategicCellId));
            var asset = new EconomicTransportBuiltAsset(
                31, 101, new PhysicalTacticalEdgeId(
                    session.InitialTile.FineGoldbergParameters, edge.Id), 7, true);
            var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId> {
                [source] = from, [buyer] = to
            };
            var access = StrategicEdgeAccessSnapshot.AllOpen;
            var facts = new CanonicalLandEdgeFactsSnapshot(session);
            var infra = new ExperimentalLandInfrastructureSnapshot(
                session, facts, new[] { asset },
                new Dictionary<(ulong, ulong), ExperimentalLandInfrastructureKind> {
                    [(101UL, 7UL)] = ExperimentalLandInfrastructureKind.StoneRoad
                }, access, 7);
            var pricing = new ExperimentalFreightTechnologyPricing(
                101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
            var offer = new ExperimentalCostOptimalFreightItineraryDiscovery()
                .Discover(session, facts, infra, anchors, access,
                    new[] { asset },
                    new[] {
                        new EconomicFineTransportServiceIntent(
                            17, new StrategicEconomicRouteKey(source, buyer),
                            101, 7, 1_000_000)
                    }, new[] { pricing }).Single();
            var option = ExperimentalSharedFreightOption.FromBuiltOffer(
                offer, session, anchors, new[] { asset }, access);
            var flows = new EconomicProductionDemandResult(
                new[] { new EconomicSupplyFlow(source, food,
                    EconomicActivityKind.Agriculture, Unit) },
                new[] { new EconomicDemandFlow(buyer, food, Unit) });
            var catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(food, "food", 1)
            });
            var lanes = new[] {
                new ExperimentalSimultaneousFreightLane(
                    flows.SupplyFlows[0], flows.DemandFlows[0],
                    new[] { option })
            };
            var purchasing = new ExperimentalAbstractPurchasingPowerProjector()
                .Project(session, flows,
                    new[] {
                        new ExperimentalAbstractBuyerAnchor(
                            buyer, to, new FoodTransportCostFraction(1, 1))
                    },
                    new[] { new ExperimentalAbstractProducerAnchor(source, from) },
                    lanes,
                    new[] {
                        new ExperimentalBuyerCommodityPrice(
                            buyer, food, new FoodTransportCostFraction(1, 1))
                    },
                    new ExperimentalAbstractPurchasingPolicy(
                        "VS58-BENCH-PURCHASING", 1,
                        new FoodTransportCostFraction(5, 1),
                        new FoodTransportCostFraction(5, 8)));
            var policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS58-BENCH-CALIBRATION", 1,
                "VS58-BENCH-PURCHASING", 1,
                "VS40-NOMINAL", new FoodTransportCostFraction(1, 1));
            var quotes = new[] {
                new ExperimentalAbstractNominalDeliveredQuote(
                    buyer, food, new FoodTransportCostFraction(1, 1),
                    new FoodTransportCostFraction(1, 1), "VS40-NOMINAL")
            };
            var deliveries = new[] {
                new ExperimentalAbstractPhysicalFreightDelivery(
                    1, source, EconomicActivityKind.Agriculture,
                    buyer, food, Unit, option)
            };
            var operatorInstitution = new ExperimentalTransportInstitution(
                "CARRIER", ExperimentalTransportInstitutionKind.CommercialOperator);
            var roadOwner = new ExperimentalTransportInstitution(
                "PUBLIC-ROAD", ExperimentalTransportInstitutionKind.PublicAuthority);
            var beneficiary = new ExperimentalTransportInstitution(
                "COMMUNITY", ExperimentalTransportInstitutionKind.Community);
            var registry = new ExperimentalTransportInstitutionalRegistry(
                "VS58-BENCH-REGISTRY", 1, session.CurrentHash, 7,
                "VS58-BENCH-PURCHASING", 1,
                "VS58-BENCH-CALIBRATION", 1, "VS40-NOMINAL",
                new[] {
                    new ExperimentalTransportServiceEntitlement(
                        source, buyer, 17, operatorInstitution,
                        new[] {
                            new ExperimentalTransportFreightRevenueShare(
                                beneficiary, new FoodTransportCostFraction(1, 1))
                        })
                });
            var witness = new ExperimentalBuiltTransportNetworkWitness(
                "VS58-BENCH-NETWORK", 1, session.CurrentHash, 7,
                new[] { asset },
                new[] { new ExperimentalBuiltFreightServiceEvidence(
                    offer, option) },
                new[] { new ExperimentalBuiltFreightAssetGrant(
                    31, roadOwner, operatorInstitution,
                    "PUBLIC-ROAD-31-CARRIER", 7, 9) });
            var reconciler = new CanonicalSimultaneousTransportSelector();
            session.TransportNetwork.Construct(asset, roadOwner);
            session.TransportNetwork.SetEntitlement(registry.Entitlements.Single());
            session.TransportNetwork.IssueGrant(witness.AssetGrants.Single() with {
                ValidFromNetworkRevision = 3, ValidThroughNetworkRevision = 100 });
            var proof = session.TransportNetwork.Capture(witness.OfferedServices,
                registry.PurchasingPolicyId, registry.PurchasingPolicyVersion,
                registry.CalibrationId, registry.CalibrationVersion, registry.NominalQuoteUnitId);
            var congestion = new ExperimentalSharedFreightCongestionPolicy(
                1, 1_000_000);
            var hash = session.CurrentHash;
            var save = session.ExportSaveJson();

            for (var iCase = 0; iCase < 3; iCase++)
            {
                var count = new[] { 8, 32, 128 }[iCase];
                var times = new double[3];
                var allocations = new long[3];
                for (var sample = -1; sample < 3; sample++)
                {
                    var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    for (var i = 0; i < count; i++)
                    {
                        var selection = reconciler.Select(
                            session, purchasing, flows, catalog,
                            quotes, policy, "VS40-NOMINAL",
                            lanes, congestion,
                            proof, anchors, Unit / 2);
                        var result = selection.Audit;
                        if (result.CanAuthorizeRealSettlement ||
                            result.DistinctOperationalAssets != 1 ||
                            result.ValidatedServiceRights.Count != 1 ||
                            result.InstitutionalProjection.Lots.Count != 1 ||
                            result.InstitutionalProjection
                                .AbstractBuyerFreightCharges.CompareTo(
                                    result.InstitutionalProjection
                                        .AbstractInstitutionalFreightReceivables) != 0)
                            throw new InvalidOperationException(
                                "Built service institutional balance diverged.");
                    }
                    var elapsed = Stopwatch.GetElapsedTime(started)
                        .TotalMilliseconds;
                    var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
                    if (sample >= 0)
                    {
                        times[sample] = elapsed;
                        allocations[sample] = bytes;
                    }
                }
                if (session.CurrentHash != hash ||
                    session.ExportSaveJson() != save)
                    throw new InvalidOperationException(
                        "Read-only built route audit mutated world/save.");
                Console.WriteLine($"CASE=built-route-institutional-{count};" +
                    $"physical_buyers=1;lots=1;assets=1;services=1;" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
            }
            Console.WriteLine("RESULT=PASS_GA_VS58_BUILT_INSTITUTIONAL");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_GA_VS58_BUILT_INSTITUTIONAL");
            return 1;
        }
#endif
    }
}
