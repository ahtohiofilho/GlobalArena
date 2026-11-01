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
public static class GaVs60SessionTradePreviewBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    public static int Run(bool livePrimitive = false, bool discoverSites = false, bool discoverFreight = false, bool discoverPortfolio = false, bool jointBudget = false, bool selectJointFreight = false, bool selectComponents = false, bool compareScenarios = false, bool compareLocalPrices = false, bool compareRegionalPurchasing = false, bool diagnoseRegionalAccess = false, bool diagnoseLocalSupply = false, bool diagnoseSourceReservations = false, bool previewJointPhysicalUse = false, bool diagnoseLocalBudgetShadow = false, bool selectIntegratedLocalFreight = false, bool auditExplicitOfferGap = false, bool compareEndogenousRemote = false, bool compareSupplyBoundFreight = false, bool auditSimultaneousFreight = false, bool reconcileRealizedProduction = false, bool projectWorkforceRealization = false, bool respondToFrozenMarketPrice = false, bool witnessRealEconomicCycle = false, bool inspectHistoricPriceSidecar = false, bool observeExecutedCashFoodPrice = false)
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS60 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine(observeExecutedCashFoodPrice ? "BENCHMARK=GA-VS87-EXECUTED-CASH-FOOD-PRICE" : inspectHistoricPriceSidecar ? "BENCHMARK=GA-VS86-PRICE-HISTORY-SIDECAR" : witnessRealEconomicCycle ? "BENCHMARK=GA-VS85-REAL-ECONOMIC-CYCLE-WITNESS" : respondToFrozenMarketPrice ? "BENCHMARK=GA-VS84-LAGGED-PRODUCTION-RESPONSE" : projectWorkforceRealization ? "BENCHMARK=GA-VS83-WORKFORCE-REALIZATION" : reconcileRealizedProduction ? "BENCHMARK=GA-VS82-REALIZED-PRODUCTION" : auditSimultaneousFreight ? "BENCHMARK=GA-VS81-SIMULTANEOUS-FREIGHT" : compareSupplyBoundFreight ? "BENCHMARK=GA-VS80-SUPPLY-BOUND-FREIGHT" : compareEndogenousRemote ? "BENCHMARK=GA-VS79-ENDOGENOUS-REMOTE-FREIGHT" : auditExplicitOfferGap ? "BENCHMARK=GA-VS77-EXPLICIT-OFFER-GAP" : selectIntegratedLocalFreight ? "BENCHMARK=GA-VS76-INTEGRATED-LOCAL-FREIGHT" : diagnoseLocalBudgetShadow ? "BENCHMARK=GA-VS75-LOCAL-BUDGET-SHADOW" : previewJointPhysicalUse ? "BENCHMARK=GA-VS74-JOINT-PHYSICAL-USE" : diagnoseSourceReservations ? "BENCHMARK=GA-VS73-SOURCE-PHYSICAL-HEADROOM" : diagnoseLocalSupply ? "BENCHMARK=GA-VS72-LOCAL-SUPPLY-POTENTIAL" : diagnoseRegionalAccess ? "BENCHMARK=GA-VS71-REGIONAL-ACCESS-DISTRIBUTION" : compareRegionalPurchasing ? "BENCHMARK=GA-VS70-PHYSICAL-REGIONAL-PURCHASING" : compareLocalPrices ? "BENCHMARK=GA-VS69-LOCAL-BUYER-PRICE-IMPACT" : compareScenarios ? "BENCHMARK=GA-VS68-SHADOW-ECONOMIC-SCENARIOS" : selectComponents ? "BENCHMARK=GA-VS67-INDEPENDENT-FREIGHT-COMPONENTS" : selectJointFreight ? "BENCHMARK=GA-VS66-BOUNDED-JOINT-FREIGHT-SELECTION" : jointBudget ? "BENCHMARK=GA-VS65-JOINT-PRIMITIVE-FREIGHT-BUDGET" : discoverPortfolio ? "BENCHMARK=GA-VS64-PRIMITIVE-FREIGHT-PORTFOLIO" : discoverFreight ? "BENCHMARK=GA-VS63-INSTITUTIONALLY-AUTHORIZED-FREIGHT" : discoverSites ? "BENCHMARK=GA-VS62-EMERGENT-ECONOMIC-SITES" : livePrimitive ? "BENCHMARK=GA-VS61-LIVE-PRIMITIVE-SESSION" : "BENCHMARK=GA-VS60-BUILT-INSTITUTIONAL-NETWORK");
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
                        "VS60-BENCH-PURCHASING", 1,
                        new FoodTransportCostFraction(5, 1),
                        new FoodTransportCostFraction(5, 8)));
            var policy = new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS60-BENCH-CALIBRATION", 1,
                "VS60-BENCH-PURCHASING", 1,
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
                "VS60-BENCH-REGISTRY", 1, session.CurrentHash, 7,
                "VS60-BENCH-PURCHASING", 1,
                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL",
                new[] {
                    new ExperimentalTransportServiceEntitlement(
                        source, buyer, 17, operatorInstitution,
                        new[] {
                            new ExperimentalTransportFreightRevenueShare(
                                beneficiary, new FoodTransportCostFraction(1, 1))
                        })
                });
            var witness = new ExperimentalBuiltTransportNetworkWitness(
                "VS60-BENCH-NETWORK", 1, session.CurrentHash, 7,
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
            var scenario = new CanonicalTradeScenario(flows, catalog,
                new[] { new ExperimentalAbstractBuyerAnchor(buyer, to, new FoodTransportCostFraction(1, 1)) },
                new[] { new ExperimentalAbstractProducerAnchor(source, from) },
                new[] { new ExperimentalBuyerCommodityPrice(buyer, food, new FoodTransportCostFraction(1, 1)) },
                new ExperimentalAbstractPurchasingPolicy("VS60-BENCH-PURCHASING", 1,
                    new FoodTransportCostFraction(5, 1), new FoodTransportCostFraction(5, 8)),
                quotes, policy, "VS40-NOMINAL", lanes, witness.OfferedServices, congestion, Unit / 2);            var liveScenario = new CanonicalLivePrimitiveTradeScenario(
                new FoodCommodityCatalog(new[] {
                    new FoodCommodityDefinition(food, "food", 1),
                    new FoodCommodityDefinition(new CommodityId(8), "wood", 1),
                    new FoodCommodityDefinition(new CommodityId(9), "stone", 1)
                }),
                new EconomicPrimitiveCommodityMapping(food, new CommodityId(8), new CommodityId(9)),
                new[] { new ExperimentalAbstractBuyerAnchor(buyer, to, new FoodTransportCostFraction(1, 1)) },
                new[] { new ExperimentalAbstractProducerAnchor(source, from) },
                new[] { new CanonicalPrimitiveDemandRate(food,
                    checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)) },
                new[] { new ExperimentalBuyerCommodityPrice(buyer, food, new FoodTransportCostFraction(1, 1)) },
                new ExperimentalAbstractPurchasingPolicy("VS60-BENCH-PURCHASING", 1,
                    new FoodTransportCostFraction(5, 1), new FoodTransportCostFraction(5, 8)),
                quotes, policy, "VS40-NOMINAL",
                new[] { new CanonicalPrimitiveTradeLane(source, buyer, food,
                    EconomicActivityKind.Agriculture, new[] { option }) },
                witness.OfferedServices, congestion, Unit / 2);
            var discoveryPolicy = new CanonicalPrimitiveSiteDiscoveryPolicy(
                new EconomicPrimitiveCommodityMapping(food,
                    new CommodityId(8), new CommodityId(9)),
                new[] {
                    new CanonicalPrimitiveDemandRate(food,
                        checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                    new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
                    new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
                }, new FoodTransportCostFraction(1, 1));
            if (discoverPortfolio || jointBudget || selectJointFreight || selectComponents || compareScenarios || compareLocalPrices || compareRegionalPurchasing || diagnoseRegionalAccess || diagnoseLocalSupply || diagnoseSourceReservations || previewJointPhysicalUse || diagnoseLocalBudgetShadow || selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                session.ReassignWorkers(from, EconomicActivityKind.Agriculture,
                    EconomicActivityKind.Forestry, 1);
            if (discoverFreight || discoverPortfolio || jointBudget || selectJointFreight || selectComponents || compareScenarios || compareLocalPrices || compareRegionalPurchasing || diagnoseRegionalAccess || diagnoseLocalSupply || diagnoseSourceReservations || previewJointPhysicalUse || diagnoseLocalBudgetShadow || selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                session.TransportNetwork.SetEntitlement(registry.Entitlements.Single() with {
                    Source = CanonicalPrimitiveSiteDiscovery.SiteId(from),
                    Destination = CanonicalPrimitiveSiteDiscovery.SiteId(to),
                    ServiceId = 77
                });
            var baselineDiscovery = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
            var freightPolicy = new CanonicalEligibleFreightDiscoveryPolicy(
                food, EconomicActivityKind.Agriculture, pricing,
                ExperimentalLandInfrastructureKind.StoneRoad,
                new FoodCommodityCatalog(new[] {
                    new FoodCommodityDefinition(food, "food", 1)
                }));
            var portfolioProfiles = new[] {
                freightPolicy,
                new CanonicalEligibleFreightDiscoveryPolicy(
                    new CommodityId(8), EconomicActivityKind.Forestry, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad,
                    new FoodCommodityCatalog(new[] {
                        new FoodCommodityDefinition(new CommodityId(8), "wood", 1)
                    })),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    new CommodityId(9), EconomicActivityKind.Mining, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad,
                    new FoodCommodityCatalog(new[] {
                        new FoodCommodityDefinition(new CommodityId(9), "stone", 1)
                    }))
            };
            CanonicalCycleStampedPriceReceipt? receipt85 = null;
            CanonicalPrimitiveSiteDiscovery? currentSites85 = null;
            CanonicalSimultaneousFreightReference? currentFreight85 = null;
            string? sidecar86 = null;
            CanonicalPhysicalTacticalSession? reopened86 = null;
            if (witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
            {
                // End this real economic cycle AFTER the authenticated worker
                // reassignment; its closing tick is the VS85 freeze boundary.
                if (session.AdvanceMovementTurn() is null)
                    throw new InvalidOperationException(
                        "GA-VS85 requires a cycle-closing physical tick before freezing the quote.");
                var sourceSites85 = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                var sourceFreight85 = CanonicalSimultaneousFreightReference.FromSites(
                    session, sourceSites85, portfolioProfiles,
                    "VS60-BENCH-PURCHASING", 1,
                    "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                var activities85 = sourceSites85.Flows.SupplyFlows
                    .Where(x => x.RawQuantity > 0)
                    .Select(x => x.Activity).Distinct();
                var prior85 = CanonicalWorkforceRealizationProjection.FromSites(
                    session, sourceSites85, sourceFreight85,
                    activities85.Select(x =>
                        new CanonicalWorkforceActivityRealizationPolicy(
                            x, 500, 100)),
                    Array.Empty<CanonicalSimultaneousFreightLot>(), congestion);
                var feedback85 = CanonicalLaggedProductionResponse.FromFrozenProjection(
                    session, sourceSites85, sourceFreight85, prior85, 100,
                    Array.Empty<CanonicalSimultaneousFreightLot>(), congestion);
                receipt85 = CanonicalCycleStampedPriceReceipt.Capture(
                    session, feedback85);
                var actuallySettled85 = session.AdvanceMovementTurn();
                if (actuallySettled85 is null)
                    throw new InvalidOperationException(
                        "GA-VS85 benchmark requires the next real recorded economic cycle.");
                currentSites85 = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                currentFreight85 = CanonicalSimultaneousFreightReference.FromSites(
                    session, currentSites85, portfolioProfiles,
                    "VS60-BENCH-PURCHASING", 1,
                    "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
            }
            if (inspectHistoricPriceSidecar)
            {
                var proof86 = receipt85!.ApplyAfterNextSettledCycle(
                    session, currentSites85!, currentFreight85!,
                    Array.Empty<CanonicalSimultaneousFreightLot>(), congestion);
                sidecar86 = CanonicalEconomicPriceHistorySidecar.Export(
                    session, proof86);
                reopened86 = CanonicalPhysicalTacticalSession.RestoreSaveJson(
                    session.ExportSaveJson());
                var prelim = CanonicalEconomicPriceHistorySidecar.Inspect(
                    reopened86, sidecar86);
                if (!prelim.HasValidCanonicalChronology ||
                    prelim.SidecarChecksumIsDigitalSignature ||
                    prelim.ConfirmsPricesWereActuallyPaid ||
                    prelim.IndependentlyProvesArchivedPriceQuotation ||
                    prelim.CanApplyProductionOrFreight ||
                    prelim.CanAuthorizeRealSettlement ||
                    prelim.ModifiesCanonicalSave ||
                    prelim.Observations.Count != receipt85.HypotheticalPriorLocalPriceSignals.Count)
                    throw new InvalidOperationException(
                        "GA-VS86 price history must remain inspection-only across restored real cycles.");
            }
            CanonicalPhysicalTacticalSession? restored87 = null;
            if (observeExecutedCashFoodPrice)
            {
                if (session.AdvanceMovementTurn() is null)
                    throw new InvalidOperationException(
                        "GA-VS87 requires an actual canonical cash food settlement.");
                restored87 = CanonicalPhysicalTacticalSession.RestoreSaveJson(
                    session.ExportSaveJson());
                var initial87 = CanonicalExecutedLocalFoodPriceWitness
                    .FromLatestRealSettlement(restored87);
                if (!initial87.HasExecutedPriceObservation ||
                    initial87.ExecutedReceipts.Count == 0 ||
                    initial87.TotalActualLocalFoodRaw == 0 ||
                    initial87.EstablishesRealPricesForOtherCommodities ||
                    initial87.CanAuthorizeFutureSettlement)
                    throw new InvalidOperationException(
                        "GA-VS87 benchmark could not find actual executed local food cash receipts.");
            }
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
                        if (observeExecutedCashFoodPrice)
                        {
                            var price87 = CanonicalExecutedLocalFoodPriceWitness
                                .FromLatestRealSettlement(restored87!);
                            price87.VerifyIndependent();
                            if (!price87.HasExecutedPriceObservation ||
                                price87.TotalActualLocalFoodRaw == 0 ||
                                price87.EstablishesRealPricesForOtherCommodities ||
                                price87.ChangesEconomicSettlement ||
                                price87.CanAuthorizeFutureSettlement)
                                throw new InvalidOperationException(
                                    "GA-VS87 actual cash-food price certificate diverged.");
                            continue;
                        }
                        if (inspectHistoricPriceSidecar)
                        {
                            var inspected = CanonicalEconomicPriceHistorySidecar.Inspect(
                                reopened86!, sidecar86!);
                            if (!inspected.HasValidCanonicalChronology ||
                                inspected.SidecarChecksumIsDigitalSignature ||
                                inspected.ConfirmsPricesWereActuallyPaid ||
                                inspected.IndependentlyProvesArchivedPriceQuotation ||
                                inspected.CanApplyProductionOrFreight ||
                                inspected.CanAuthorizeRealSettlement ||
                                inspected.Observations.Count < 2 ||
                                inspected.Observations.Any(x =>
                                    x.NextUtilizationPermille is < 0 or > 1000))
                                throw new InvalidOperationException(
                                    "GA-VS86 observational sidecar independent check failed.");
                            continue;
                        }
                        if (witnessRealEconomicCycle)
                        {
                            var proof85 = receipt85!.ApplyAfterNextSettledCycle(
                                session, currentSites85!, currentFreight85!,
                                Array.Empty<CanonicalSimultaneousFreightLot>(),
                                congestion);
                            if (!proof85.ProvesActualChronologicalEconomicCycle ||
                                proof85.ProvesRealMarketSaleOrPayment ||
                                proof85.CreatesInventoriesOrMoney ||
                                proof85.ExecutesWorkforceReassignment ||
                                proof85.CanAuthorizeRealSettlement ||
                                proof85.NextRealCompletedEconomicCycleTurn !=
                                    session.State.MovementTurn ||
                                proof85.HypotheticalNextProduction.Allocation is null ||
                                proof85.HypotheticalNextProduction.Sources.Count < 2 ||
                                proof85.HypotheticalNextProduction.Sources.Any(x =>
                                    x.ActualActivityWorkers == 0 ||
                                    x.DerivedProducedRaw > x.PotentialRaw))
                                throw new InvalidOperationException(
                                    "GA-VS85 authentic next economic-cycle price witness failed.");
                            proof85.VerifyIndependent();
                            proof85.RequireCurrent(session);
                            continue;
                        }
                        if (respondToFrozenMarketPrice)
                        {
                            var sites84 = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var freight84 = CanonicalSimultaneousFreightReference.FromSites(
                                session, sites84, portfolioProfiles,
                                "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var activities84 = sites84.Flows.SupplyFlows
                                .Where(x => x.RawQuantity > 0)
                                .Select(x => x.Activity).Distinct().ToArray();
                            var rates84 = activities84.Select(x =>
                                new CanonicalWorkforceActivityRealizationPolicy(
                                    x, 500, 100)).ToArray();
                            var prior84 = CanonicalWorkforceRealizationProjection.FromSites(
                                session, sites84, freight84, rates84,
                                Array.Empty<CanonicalSimultaneousFreightLot>(),
                                congestion);
                            var feedback84 = CanonicalLaggedProductionResponse.FromFrozenProjection(
                                session, sites84, freight84, prior84, 100,
                                Array.Empty<CanonicalSimultaneousFreightLot>(),
                                congestion);
                            if (feedback84.Signals.Count != rates84.Length ||
                                feedback84.Signals.Any(x =>
                                    x.PreviouslyLocallyAbsorbedRaw <= 0 ||
                                    x.NextUtilizationPermille is < 0 or > 1000 ||
                                    x.BoundedUtilizationDeltaPermille is < -100 or > 100 ||
                                    x.UnchangedRetentionPermille != 100) ||
                                feedback84.IsRealHistoricalCycleAdvance ||
                                feedback84.UsesFuturePricesToDecideCurrentProduction ||
                                feedback84.ComputesNominalNetProfit ||
                                feedback84.InventsRemoteBuyersOrBuyerBudget ||
                                feedback84.CapsProductionByResidentDemand ||
                                feedback84.CreatesInvoluntaryStockOrCredit ||
                                feedback84.ChangesRealWorkerAssignments ||
                                feedback84.CanAuthorizeRealSettlement ||
                                feedback84.NextProjection.Allocation is null)
                                throw new InvalidOperationException(
                                    "GA-VS84 frozen-price macro feedback failed scope or physical proof.");
                            feedback84.VerifyIndependent();
                            feedback84.RequireCurrent(session);
                            continue;
                        }
                        if (projectWorkforceRealization)
                        {
                            var vs83Sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var vs83Freight = CanonicalSimultaneousFreightReference.FromSites(
                                session, vs83Sites, portfolioProfiles,
                                "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var vs83Rates = vs83Sites.Flows.SupplyFlows
                                .Where(x => x.RawQuantity > 0)
                                .Select(x => x.Activity).Distinct()
                                .Select(x => new CanonicalWorkforceActivityRealizationPolicy(
                                    x, 800, 100)).ToArray();
                            var vs83Carriers = vs83Freight.Offers.Take(2).ToArray();
                            if (vs83Carriers.Length != 2)
                                throw new InvalidOperationException(
                                    "GA-VS83 benchmark requires two real staffed productive source commodities.");
                            var vs83Lots = vs83Carriers.Select(x =>
                                new CanonicalSimultaneousFreightLot(
                                    x, checked((ulong)((System.Numerics.BigInteger)x.SourcePotentialRaw *
                                        800 / 1000 / 4)))).ToArray();
                            var vs83Report = CanonicalWorkforceRealizationProjection.FromSites(
                                session, vs83Sites, vs83Freight,
                                vs83Rates, vs83Lots, congestion);
                            if (!vs83Report.UsesPhysicalResidentWorkers ||
                                vs83Report.CreatesWorkersOrCities ||
                                vs83Report.AllocatesWorkPerIndividualWorker ||
                                vs83Report.UsesArtificialBuyerBudget ||
                                vs83Report.CreatesInvoluntaryStock ||
                                vs83Report.CapsTradeByBuyerDemand ||
                                vs83Report.ExecutesActualProductionOrSale ||
                                vs83Report.ComputesMoneyProfit ||
                                vs83Report.CanAuthorizeRealSettlement ||
                                vs83Report.Sources.Count < 2 ||
                                vs83Report.Allocation is null ||
                                vs83Report.Allocation.RemoteMarkets.Count != 2 ||
                                vs83Report.Sources.Any(x =>
                                    x.ActualActivityWorkers == 0 ||
                                    x.DerivedProducedRaw == 0 ||
                                    x.PotentialNotExtractedRaw == 0))
                                throw new InvalidOperationException(
                                    "GA-VS83 actual worker-based realization audit or scope failed.");
                            vs83Report.VerifyIndependent();
                            vs83Report.RequireCurrent(session);
                            continue;
                        }
                        if (reconcileRealizedProduction)
                        {
                            var vs82Sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var vs82Reference = CanonicalSimultaneousFreightReference.FromSites(
                                session, vs82Sites, portfolioProfiles,
                                "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var vs82Offers = vs82Reference.Offers.Take(2).ToArray();
                            if (vs82Offers.Length != 2)
                                throw new InvalidOperationException(
                                    "GA-VS82 benchmark requires two authenticated productive pools.");
                            var vs82Produced = vs82Offers.Select(x =>
                                Math.Min(10_000UL, x.SourcePotentialRaw)).ToArray();
                            if (vs82Produced.Any(x => x < 4UL))
                                throw new InvalidOperationException(
                                    "GA-VS82 benchmark needs positive unextracted and local quantities.");
                            var vs82Declarations = vs82Offers.Select((x, idx) =>
                                new CanonicalRealizedProductionDeclaration(
                                    x.Source, x.Commodity, x.Activity,
                                    vs82Produced[idx], vs82Produced[idx] / 4UL)).ToArray();
                            var vs82Lots = vs82Offers.Select((x, idx) =>
                                new CanonicalSimultaneousFreightLot(
                                    x, vs82Produced[idx] / 2UL)).ToArray();
                            var vs82Report = CanonicalRealizedProductionAllocation.FromSites(
                                session, vs82Sites, vs82Reference,
                                vs82Declarations, vs82Lots, congestion);
                            if (vs82Report.Sources.Count != 2 ||
                                vs82Report.LocalMarkets.Count != 2 ||
                                vs82Report.RemoteMarkets.Count != 2 ||
                                vs82Report.FreightAudit is null ||
                                vs82Report.AuthenticatedNominalFreightCost.Numerator < 0 ||
                                vs82Report.TreatsUnextractedPotentialAsInventory ||
                                vs82Report.CreatesInvoluntaryStock ||
                                vs82Report.CreatesActualConsumption ||
                                vs82Report.UsesExogenousBuyerMoney ||
                                vs82Report.CapsConsumptionByDemandRaw ||
                                vs82Report.ProvesHardFreightThroughput ||
                                vs82Report.ComputesMonetaryNetRevenue ||
                                vs82Report.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "GA-VS82 realized physical production/revenue/cost audit scope failed.");
                            vs82Report.VerifyIndependent();
                            vs82Report.RequireCurrent(session);
                            continue;
                        }
                        if (auditSimultaneousFreight)
                        {
                            var vs81Sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var vs81Proof = CanonicalSimultaneousFreightReference.FromSites(
                                session, vs81Sites, portfolioProfiles,
                                "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var vs81Options = vs81Proof.Offers.Take(2).ToArray();
                            if (vs81Options.Length != 2)
                                throw new InvalidOperationException(
                                    "GA-VS81 benchmark requires two authenticated commodities.");
                            var vs81Lots = vs81Options.Select(x =>
                                new CanonicalSimultaneousFreightLot(
                                    x, Math.Min(x.SourcePotentialRaw, 10_000UL))).ToArray();
                            var vs81Audit = vs81Proof.Audit(vs81Lots, congestion);
                            if (vs81Audit.Lots.Count != 2 ||
                                vs81Audit.CorridorLoads.Count == 0 ||
                                vs81Audit.TotalAuthenticatedNominalFreightCost.Numerator < 0 ||
                                vs81Audit.UsesExogenousMonetaryBudget ||
                                vs81Audit.CapsShipmentsByConsumerDemand ||
                                vs81Audit.ProvesHardThroughput ||
                                vs81Audit.ComputesNetMonetaryRevenue ||
                                vs81Audit.CanAuthorizeRealSettlement ||
                                vs81Audit.ProducesActualDeliveryOrInventory)
                                throw new InvalidOperationException(
                                    "GA-VS81 joint authenticated freight audit violated conservation or read-only scope.");
                            vs81Audit.VerifyIndependent();
                            vs81Audit.RequireCurrent(session);
                            continue;
                        }
                        if (compareSupplyBoundFreight)
                        {
                            var supplySites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var supplyBoundReference = CanonicalSupplyBoundFreightReference.FromSites(
                                session, supplySites, portfolioProfiles[0],
                                "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            if (supplyBoundReference.Services.Count == 0 ||
                                supplyBoundReference.UsesExogenousMoneyBudget ||
                                supplyBoundReference.CapsQuantityByBuyerDemand ||
                                !supplyBoundReference.UsesRealInstitutionalProof ||
                                !supplyBoundReference.EnforcesSourcePhysicalQuantity ||
                                supplyBoundReference.ProvesHardVehicleThroughput ||
                                supplyBoundReference.ComputesNetMonetaryRevenue ||
                                supplyBoundReference.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "GA-VS80 supply-only authenticated freight reference violated scope.");
                            var row = supplyBoundReference.Services.First();
                            var trial = supplyBoundReference.PreviewOneLot(
                                row.ServiceId, row.NewSupplyBoundRaw, congestion);
                            if (trial.RawQuantity != row.SourcePhysicalSupplyRaw ||
                                trial.BuyerDemandPriceSignalRaw != row.BuyerDemandPriceSignalRaw ||
                                trial.AuthenticatedNominalFreightCost.Numerator < 0)
                                throw new InvalidOperationException(
                                    "GA-VS80 physical envelope or nominal authenticated freight diverged.");
                            supplyBoundReference.VerifyIndependent();
                            supplyBoundReference.RequireCurrent(session);
                            continue;
                        }
                        if (compareEndogenousRemote)
                        {
                            var remoteSites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var authenticatedPortfolio = session.DiscoverPrimitiveFreightPortfolio(
                                remoteSites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var freightCandidate = authenticatedPortfolio.CandidateUses.First();
                            var sourceFlow = remoteSites.Flows.SupplyFlows.Single(x =>
                                x.EconomicPointId == freightCandidate.Source &&
                                x.CommodityId == freightCandidate.Commodity &&
                                x.Activity == freightCandidate.Activity);
                            var raw = Math.Min(100_000UL, Math.Min(
                                freightCandidate.MaximumRawQuantity, sourceFlow.RawQuantity));
                            if (raw == 0)
                                throw new InvalidOperationException(
                                    "VS79 authenticated freight has zero physical freight capacity.");
                            var compared = CanonicalEndogenousFreightPriceComparison.FromPortfolio(
                                authenticatedPortfolio, session, freightCandidate, raw, congestion);
                            if (compared.Gentle.RawQuantity != raw ||
                                compared.Steep.RawQuantity != raw ||
                                compared.UsesExogenousBuyerBudget ||
                                compared.AddsAnIndependentConsumerDemandCap ||
                                !compared.UsesLegacyFreightCandidateQuantityBound ||
                                compared.SupportsUnboundedRemoteConsumption ||
                                compared.CanCalculateMonetaryNetRevenue ||
                                compared.IsNominalFreightConvertedIntoPriceIndex ||
                                compared.CanAuthorizeRealSettlement ||
                                compared.IsDispatchOrSettlement)
                                throw new InvalidOperationException(
                                    "VS79 remote price/cost boundary or conservation failed.");
                            compared.VerifyIndependent();
                            compared.RequireCurrent(session);
                            continue;
                        }
                        if (selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var freight = portfolio.CandidateUses.First();
                            var localSite = sites.At(freight.Source)!;
                            var supply = localSite.Supply.Single(x =>
                                x.CommodityId == freight.Commodity &&
                                x.Activity == freight.Activity);
                            var demand = localSite.Demand.Single(x =>
                                x.CommodityId == freight.Commodity);
                            var quantum = Math.Min(100_000UL, Math.Min(
                                freight.MaximumRawQuantity,
                                Math.Min(supply.RawQuantity, demand.RawQuantity)));
                            if (quantum == 0)
                                throw new InvalidOperationException(
                                    "VS76 reference setup has no positive local/freight common quantum.");
                            var local = new CanonicalPrimitiveLocalUseProposal(
                                freight.Source, freight.Commodity,
                                freight.Activity, Math.Min(supply.RawQuantity, demand.RawQuantity));
                            var posted = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                                    x.CommodityId, new FoodTransportCostFraction(1, 1)));
                            var selection = portfolio.SelectJointLocalFreight(
                                [local], [freight], posted,
                                new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion, quantum);
                            if (selection.EvaluatedConfigurations != 4 ||
                                selection.PossibleConfigurations != 4 ||
                                selection.Lots.Count == 0 ||
                                selection.CanAuthorizeRealSettlement ||
                                selection.IsActualConsumption || selection.IsCommitted)
                                throw new InvalidOperationException(
                                    "VS76 integrated bounded market reference failed.");
                            selection.VerifyIndependent();
                            selection.RequireCurrent(session);
                            if (auditExplicitOfferGap)
                            {
                                var declaredSourceOffer = new CanonicalPrimitiveExplicitSourceOffer(
                                    freight.Source, freight.Commodity, freight.Activity,
                                    supply.RawQuantity);
                                var receipt = selection.DiagnoseExplicitOfferAbsorption([declaredSourceOffer]);
                                var row = receipt.Snapshot.Sources.Single(x =>
                                    x.Source == freight.Source &&
                                    x.Commodity == freight.Commodity &&
                                    x.Activity == freight.Activity);
                                if (row.HypotheticallyOfferedRaw != supply.RawQuantity ||
                                    row.Vs76AllocatedRaw > row.HypotheticallyOfferedRaw ||
                                    receipt.IsActualSale || receipt.IsActualInventory ||
                                    receipt.IsMarginalPriceCurveCalibrated ||
                                    receipt.CanAuthorizeRealSettlement)
                                    throw new InvalidOperationException(
                                        "VS77 explicit offer residual audit implied an unauthorized sale.");
                                receipt.VerifyIndependent();
                            }
                            continue;
                        }
                        if (diagnoseRegionalAccess || diagnoseLocalSupply || diagnoseSourceReservations || previewJointPhysicalUse || diagnoseLocalBudgetShadow || selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var references = sites.Flows.SupplyFlows.Select(x =>
                                new CanonicalPrimitiveProductivityReference(
                                    x.CommodityId, x.Activity,
                                    x.CommodityId == food ? 1_000_000UL :
                                    x.CommodityId == new CommodityId(8) ? 100_000UL : 50_000UL))
                                .DistinctBy(x => (x.Commodity, x.Activity));
                            var regional = portfolio.CalibrateRegionalPurchasing(
                                references, new FoodTransportCostFraction(1, 2),
                                new FoodTransportCostFraction(4, 1));
                            var prices = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(
                                    x.EconomicPointId, x.CommodityId,
                                    new FoodTransportCostFraction(1, 1)));
                            var accessDiagnostics = portfolio.DiagnoseRegionalFreightAccess(
                                portfolio.CandidateUses, prices,
                                new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion, regional, 100_000UL);
                            if (accessDiagnostics.Baseline.Buyers.Count != sites.Sites.Count ||
                                accessDiagnostics.Regional.Buyers.Count != sites.Sites.Count ||
                                accessDiagnostics.Baseline.Buyers.Sum(x => x.Commodities.Count) !=
                                    sites.Flows.DemandFlows.Count ||
                                accessDiagnostics.Comparison.Baseline.Selection.Lots.Count != 3 ||
                                accessDiagnostics.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Regional access diagnostic lost residents, demand or auditable freight.");
                            accessDiagnostics.RequireCurrent(session);
                            if (diagnoseLocalSupply || diagnoseSourceReservations || previewJointPhysicalUse || diagnoseLocalBudgetShadow || selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                            {
                                var local = accessDiagnostics.DiagnoseLocalSupplyPotential();
                                if (local.Snapshot.Buyers.Count != sites.Sites.Count ||
                                    local.Snapshot.Buyers.Sum(x => x.Commodities.Count) !=
                                        sites.Flows.DemandFlows.Count ||
                                    local.CanAuthorizeRealSettlement || local.IsActualConsumption)
                                    throw new InvalidOperationException(
                                        "Local physical production opportunity lost demand or became settlement.");
                                local.VerifyIndependent();
                                if (diagnoseSourceReservations || previewJointPhysicalUse || diagnoseLocalBudgetShadow || selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                                {
                                    var headroom = local.DiagnoseSourceReservations();
                                    if (headroom.Baseline.Sources.Count !=
                                            sites.Flows.DemandFlows.Count ||
                                        headroom.Regional.Sources.Count !=
                                            sites.Flows.DemandFlows.Count ||
                                        headroom.CanAuthorizeRealSettlement ||
                                        headroom.IsActualConsumption ||
                                        headroom.IsPersistentReservation)
                                        throw new InvalidOperationException(
                                            "VS73 physical reservation reconciliation failed.");
                                    headroom.VerifyIndependent();
                                    if (previewJointPhysicalUse || diagnoseLocalBudgetShadow || selectIntegratedLocalFreight || auditExplicitOfferGap || compareEndogenousRemote || compareSupplyBoundFreight || auditSimultaneousFreight || reconcileRealizedProduction || projectWorkforceRealization || respondToFrozenMarketPrice || witnessRealEconomicCycle || inspectHistoricPriceSidecar || observeExecutedCashFoodPrice)
                                    {
                                        var trial = headroom.PreviewJointPhysicalUse(
                                            headroom.Baseline.ScenarioId,
                                            Array.Empty<CanonicalPrimitiveLocalUseProposal>());
                                        if (!trial.IsPhysicallyFeasible ||
                                            trial.Sources.Count != sites.Flows.SupplyFlows.Count ||
                                            trial.Buyers.Count != sites.Flows.DemandFlows.Count ||
                                            trial.IsLocalPurchaseBudgetEvaluated ||
                                            trial.CanAuthorizeRealSettlement)
                                            throw new InvalidOperationException(
                                                "VS74 conditional physical trial lost provenance or implied settlement.");
                                        trial.VerifyIndependent();
                                        if (diagnoseLocalBudgetShadow)
                                        {
                                            var shadow = trial.DiagnoseLocalBudgetHypotheses();
                                            if (shadow.Snapshot.Buyers.Count != sites.Sites.Count ||
                                                shadow.IsMarketAccountingSelected ||
                                                shadow.IsFreeLocalUseAuthorized ||
                                                shadow.IsCoverageScoreAnApprovedSocialPolicy ||
                                                shadow.CanAuthorizeRealSettlement)
                                                throw new InvalidOperationException(
                                                    "VS75 local budget shadow improperly selected an economic policy.");
                                            shadow.VerifyIndependent();
                                        }
                                    }
                                }
                            }
                            continue;
                        }
                        if (compareRegionalPurchasing)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var references = sites.Flows.SupplyFlows.Select(x =>
                                new CanonicalPrimitiveProductivityReference(
                                    x.CommodityId, x.Activity, x.CommodityId == food
                                        ? 1_000_000UL : x.CommodityId == new CommodityId(8)
                                            ? 100_000UL : 50_000UL))
                                .DistinctBy(x => (x.Commodity, x.Activity));
                            var regional = portfolio.CalibrateRegionalPurchasing(
                                references, new FoodTransportCostFraction(1, 2),
                                new FoodTransportCostFraction(4, 1));
                            var prices = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(
                                    x.EconomicPointId, x.CommodityId,
                                    new FoodTransportCostFraction(1, 1)));
                            var regionalComparison = portfolio.CompareRegionalPurchasing(
                                portfolio.CandidateUses, prices,
                                new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion, regional, 100_000UL);
                            if (regionalComparison.Outcomes.Count != 2 ||
                                regionalComparison.Baseline.Selection.Lots.Count != 3 ||
                                regionalComparison.Outcomes.Single(x =>
                                    x.Scenario.ScenarioId == "regional-productivity")
                                    .Selection.Lots.Count != 3 ||
                                regionalComparison.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Physical regional purchasing projection diverged.");
                            regionalComparison.RequireCurrent(session);
                            continue;
                        }
                        if (compareLocalPrices)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var prices = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(
                                    x.EconomicPointId, x.CommodityId,
                                    new FoodTransportCostFraction(1, 1))).ToArray();
                            var localBuyer = portfolio.CandidateUses[0].Buyer;
                            var compared = portfolio.CompareLocalizedBuyerPrices(
                                portfolio.CandidateUses, prices,
                                new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion, new[] {
                                    new CanonicalPrimitiveLocalPriceShock(
                                        "food-expensive", localBuyer, food,
                                        new FoodTransportCostFraction(1_000_000, 1)),
                                    new CanonicalPrimitiveLocalPriceShock(
                                        "wood-expensive", localBuyer, new CommodityId(8),
                                        new FoodTransportCostFraction(1_000_000, 1))
                                }, 100_000UL);
                            if (compared.Impacts.Count != 2 ||
                                compared.Scenarios.Baseline.Selection.Lots.Count != 3 ||
                                compared.Impacts.Any(x =>
                                    x.VerifiedUnchangedComponents != 0 ||
                                    x.CoupledComponentIndices.Count != 1) ||
                                compared.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Local market sensitivity must retain the shared buyer H coupling.");
                            compared.RequireCurrent(session);
                            continue;
                        }
                        if (compareScenarios)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            ExperimentalBuyerCommodityPrice[] Posted(long unit) =>
                                sites.Flows.DemandFlows.Select(x =>
                                    new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                                        x.CommodityId, new FoodTransportCostFraction(unit, 1)))
                                .ToArray();
                            ExperimentalAbstractPurchasingPolicy Budget(long protectedH) =>
                                new("VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(protectedH, 1));
                            var cases = new[] {
                                new CanonicalPrimitiveFreightScenario(
                                    "baseline", Posted(1), Budget(1), congestion),
                                new CanonicalPrimitiveFreightScenario(
                                    "price-shock", Posted(1_000_000), Budget(1), congestion),
                                new CanonicalPrimitiveFreightScenario(
                                    "H-shock", Posted(1), Budget(5000), congestion)
                            };
                            var compared = portfolio.CompareFreightScenarios(
                                portfolio.CandidateUses, cases, "baseline", 100_000UL);
                            if (compared.Outcomes.Count != 3 ||
                                compared.Baseline.Selection.Lots.Count != 3 ||
                                compared.Outcomes.Any(x =>
                                    x.Scenario.ScenarioId != "baseline" &&
                                    x.Selection.Lots.Count != 0) ||
                                compared.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Mutually exclusive shadow purchasing scenarios diverged.");
                            compared.RequireCurrent(session);
                            continue;
                        }
                        if (selectComponents)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var posted = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                                    x.CommodityId, new FoodTransportCostFraction(1, 1)));
                            var selection = portfolio.SelectIndependentFreightComponents(
                                portfolio.CandidateUses, posted,
                                new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion, 100_000UL);
                            if (selection.IndependentComponentCount != 1 ||
                                selection.Lots.Count != 3 ||
                                selection.Preview is null ||
                                !selection.Preview.IsBudgetFeasible ||
                                selection.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Independence partition must preserve physically coupled three-product batch.");
                            selection.RequireCurrent(session);
                            continue;
                        }
                        if (selectJointFreight)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var posted = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                                    x.CommodityId, new FoodTransportCostFraction(1, 1)));
                            var selection = portfolio.SelectJointFreight(
                                portfolio.CandidateUses, posted,
                                new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion, 100_000UL);
                            if (selection.Lots.Count != 3 ||
                                selection.EvaluatedConfigurations != 8 ||
                                selection.BudgetFeasibleConfigurations != 8 ||
                                selection.Preview is null ||
                                !selection.Preview.IsBudgetFeasible ||
                                selection.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Bounded independent multi-product freight optimum diverged.");
                            selection.RequireCurrent(session);
                            continue;
                        }
                        if (jointBudget)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var lots = portfolio.CandidateUses.Select(x =>
                                new CanonicalPrimitiveJointFreightLot(
                                    x, Math.Min(x.MaximumRawQuantity, 100_000UL))).ToArray();
                            var posted = sites.Flows.DemandFlows.Select(x =>
                                new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                                    x.CommodityId, new FoodTransportCostFraction(1, 1)));
                            var forecast = portfolio.PreviewJointBudget(
                                lots, posted, new ExperimentalAbstractPurchasingPolicy(
                                    "VS60-BENCH-PURCHASING", 1,
                                    new FoodTransportCostFraction(5000, 1),
                                    new FoodTransportCostFraction(5, 8)),
                                congestion);
                            if (forecast.Lots.Count != 3 ||
                                !forecast.IsBudgetFeasible ||
                                forecast.CanAuthorizeRealSettlement ||
                                forecast.Freight.CorridorLoads.Count != 1 ||
                                forecast.Buyers.TotalBuyerFreight.CompareTo(
                                    forecast.Freight.TotalCost) != 0)
                                throw new InvalidOperationException(
                                    "Simultaneous physical corridor and abstract H forecast diverged.");
                            forecast.RequireCurrent(session);
                            continue;
                        }
                        if (discoverPortfolio)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var portfolio = session.DiscoverPrimitiveFreightPortfolio(
                                sites, portfolioProfiles, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            if (portfolio.CandidateUses.Count != 3 ||
                                portfolio.SharedServices.Count != 1 ||
                                portfolio.SharedServices[0].ResourceId != 77 ||
                                portfolio.SharedServices[0].CompetingUses.Count != 3 ||
                                portfolio.SharedAssets.Count != 1 ||
                                portfolio.SharedAssets[0].ResourceId != 31 ||
                                portfolio.SharedAssets[0].CompetingUses.Count != 3 ||
                                portfolio.TotalEligibleIntents != 3 ||
                                portfolio.CanCombineIndependentTradePreviews ||
                                portfolio.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Portfolio shared freight conflict diagnostics diverged.");
                            portfolio.RequireCurrent(session);
                            continue;
                        }
                        if (discoverFreight)
                        {
                            var sites = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var discovered = session.DiscoverEligiblePrimitiveFreight(
                                sites, freightPolicy, "VS60-BENCH-PURCHASING", 1,
                                "VS60-BENCH-CALIBRATION", 1, "VS40-NOMINAL");
                            var right = discovered.Services.Single();
                            if (right.Offer.ServiceId != 77 ||
                                discovered.Lanes.Count != 1 ||
                                discovered.InspectedRegisteredServices != 2 ||
                                discovered.SourceTreeBuilds != 1 ||
                                discovered.CanAuthorizeRealSettlement)
                                throw new InvalidOperationException(
                                    "Institutional freight offer inventory diverged.");
                            discovered.RequireCurrent(session);
                            continue;
                        }
                        if (discoverSites)
                        {
                            var discovered = session.DiscoverPrimitiveEconomicSites(discoveryPolicy);
                            var delta = discovered.ChangesSince(baselineDiscovery);
                            if (discovered.CanAuthorizeRealSettlement ||
                                discovered.Sites.Count != 2 ||
                                discovered.ResidentWorkers != 8UL ||
                                discovered.Flows.DemandFlows.Count != 6 ||
                                delta.Added.Count != 0 || delta.Removed.Count != 0 ||
                                delta.Updated.Count != 0)
                                throw new InvalidOperationException(
                                    "Discovered physical site inventory diverged.");
                            continue;
                        }
                        var preview = livePrimitive
                            ? session.PreviewLivePrimitiveTrade(liveScenario).Trade
                            : session.PreviewTrade(scenario);
                        var result = preview.VerifiedSelection.Audit;
                        if (result.CanAuthorizeRealSettlement ||
                            result.DistinctOperationalAssets != 1 ||
                            result.ValidatedServiceRights.Count != 1 ||
                            (!livePrimitive && result.InstitutionalProjection.Lots.Count != 1) ||
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
            Console.WriteLine(observeExecutedCashFoodPrice ? "RESULT=PASS_GA_VS87_EXECUTED_CASH_FOOD_PRICE" : inspectHistoricPriceSidecar ? "RESULT=PASS_GA_VS86_PRICE_HISTORY_SIDECAR" : witnessRealEconomicCycle ? "RESULT=PASS_GA_VS85_REAL_ECONOMIC_CYCLE_WITNESS" : respondToFrozenMarketPrice ? "RESULT=PASS_GA_VS84_LAGGED_PRICE_RESPONSE" : projectWorkforceRealization ? "RESULT=PASS_GA_VS83_WORKFORCE_REALIZATION" : reconcileRealizedProduction ? "RESULT=PASS_GA_VS82_REALIZED_PRODUCTION" : auditSimultaneousFreight ? "RESULT=PASS_GA_VS81_SIMULTANEOUS_FREIGHT" : compareSupplyBoundFreight ? "RESULT=PASS_GA_VS80_SUPPLY_BOUND_FREIGHT" : compareEndogenousRemote ? "RESULT=PASS_GA_VS79_ENDOGENOUS_FREIGHT" : auditExplicitOfferGap ? "RESULT=PASS_GA_VS77_OFFER_GAP" : selectIntegratedLocalFreight ? "RESULT=PASS_GA_VS76_INTEGRATED" : diagnoseLocalBudgetShadow ? "RESULT=PASS_GA_VS75_BUDGET_SHADOW" : previewJointPhysicalUse ? "RESULT=PASS_GA_VS74_JOINT_PHYSICAL" : diagnoseSourceReservations ? "RESULT=PASS_GA_VS73_SOURCE_HEADROOM" : diagnoseLocalSupply ? "RESULT=PASS_GA_VS72_LOCAL_SUPPLY" : diagnoseRegionalAccess ? "RESULT=PASS_GA_VS71_REGIONAL_ACCESS" : compareRegionalPurchasing ? "RESULT=PASS_GA_VS70_REGIONAL_PURCHASING" : compareLocalPrices ? "RESULT=PASS_GA_VS69_LOCAL_PRICES" : compareScenarios ? "RESULT=PASS_GA_VS68_SCENARIOS" : selectComponents ? "RESULT=PASS_GA_VS67_COMPONENTS" : selectJointFreight ? "RESULT=PASS_GA_VS66_JOINT_SELECTION" : jointBudget ? "RESULT=PASS_GA_VS65_JOINT_BUDGET" : discoverPortfolio ? "RESULT=PASS_GA_VS64_FREIGHT_PORTFOLIO" : discoverFreight ? "RESULT=PASS_GA_VS63_AUTHORIZED_FREIGHT" : discoverSites ? "RESULT=PASS_GA_VS62_SITE_DISCOVERY" : livePrimitive ? "RESULT=PASS_GA_VS61_LIVE_PRIMITIVE" : "RESULT=PASS_GA_VS60_BUILT_INSTITUTIONAL");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine(observeExecutedCashFoodPrice ? "RESULT=FAIL_GA_VS87_EXECUTED_CASH_FOOD_PRICE" : inspectHistoricPriceSidecar ? "RESULT=FAIL_GA_VS86_PRICE_HISTORY_SIDECAR" : witnessRealEconomicCycle ? "RESULT=FAIL_GA_VS85_REAL_ECONOMIC_CYCLE_WITNESS" : respondToFrozenMarketPrice ? "RESULT=FAIL_GA_VS84_LAGGED_PRICE_RESPONSE" : projectWorkforceRealization ? "RESULT=FAIL_GA_VS83_WORKFORCE_REALIZATION" : reconcileRealizedProduction ? "RESULT=FAIL_GA_VS82_REALIZED_PRODUCTION" : auditSimultaneousFreight ? "RESULT=FAIL_GA_VS81_SIMULTANEOUS_FREIGHT" : compareSupplyBoundFreight ? "RESULT=FAIL_GA_VS80_SUPPLY_BOUND_FREIGHT" : compareEndogenousRemote ? "RESULT=FAIL_GA_VS79_ENDOGENOUS_FREIGHT" : auditExplicitOfferGap ? "RESULT=FAIL_GA_VS77_OFFER_GAP" : selectIntegratedLocalFreight ? "RESULT=FAIL_GA_VS76_INTEGRATED" : diagnoseLocalBudgetShadow ? "RESULT=FAIL_GA_VS75_BUDGET_SHADOW" : previewJointPhysicalUse ? "RESULT=FAIL_GA_VS74_JOINT_PHYSICAL" : diagnoseSourceReservations ? "RESULT=FAIL_GA_VS73_SOURCE_HEADROOM" : diagnoseLocalSupply ? "RESULT=FAIL_GA_VS72_LOCAL_SUPPLY" : diagnoseRegionalAccess ? "RESULT=FAIL_GA_VS71_REGIONAL_ACCESS" : compareRegionalPurchasing ? "RESULT=FAIL_GA_VS70_REGIONAL_PURCHASING" : compareLocalPrices ? "RESULT=FAIL_GA_VS69_LOCAL_PRICES" : compareScenarios ? "RESULT=FAIL_GA_VS68_SCENARIOS" : selectComponents ? "RESULT=FAIL_GA_VS67_COMPONENTS" : selectJointFreight ? "RESULT=FAIL_GA_VS66_JOINT_SELECTION" : jointBudget ? "RESULT=FAIL_GA_VS65_JOINT_BUDGET" : discoverPortfolio ? "RESULT=FAIL_GA_VS64_FREIGHT_PORTFOLIO" : discoverFreight ? "RESULT=FAIL_GA_VS63_AUTHORIZED_FREIGHT" : discoverSites ? "RESULT=FAIL_GA_VS62_SITE_DISCOVERY" : livePrimitive ? "RESULT=FAIL_GA_VS61_LIVE_PRIMITIVE" : "RESULT=FAIL_GA_VS60_BUILT_INSTITUTIONAL");
            return 1;
        }
#endif
    }
}
