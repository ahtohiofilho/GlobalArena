using GlobalArena.Runtime;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// LAB ONLY. These coefficient and tax numbers are copied from the
/// existing v15 C# regression fixture: they are NOT approved balance
/// decisions and are NEVER selected for ordinary production gameplay.
/// This explicitly selected Godot CI scenario creates a differentiated
/// real producer census solely to render genuine freight routes.
/// </summary>
internal static class GaExplicitEconomicQaConfiguration
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId CommodityOne = new(1U);
    private static readonly CommodityId CommodityTwo = new(2U);

    // R89 experimental logistics invariant: at zero investment, long
    // sea passages are cheaper than overland haulage. Port transitions
    // remain deliberately expensive, discouraging unnecessary land/sea
    // switching. Keep this isolated from production balancing.
    internal const ulong QaLandEdgeCostMicro = 250_000UL;
    internal const ulong QaCoastEdgeCostMicro = 500_000UL;
    internal const ulong QaSeaEdgeCostMicro = 25_000UL;
    // All commodities in this explicit QA profile have the same cargo
    // conversion. Share the value with the read-only acceptance diagnostic.
    internal const ulong QaCargoMicroPerWholeCommodity = 1_000_000UL;

    internal static void AssertNaturalFreightHierarchy()
    {
        if (!(QaSeaEdgeCostMicro < QaLandEdgeCostMicro &&
            QaLandEdgeCostMicro < QaCoastEdgeCostMicro))
            throw new InvalidOperationException(
                "QA natural freight must strongly favor open sea and " +
                "discourage unnecessary port transitions.");
    }

    // Explicit opt-in acceptance fixture, never selected by the normal launcher.
    internal static string? TradeFixtureMode()
    {
        var mode = System.Environment.GetEnvironmentVariable("GA_NEXT_R2313_TRADE_FIXTURE");
        if (string.IsNullOrEmpty(mode)) return null;
        if (mode != "remote" && mode != "local")
            throw new InvalidOperationException("Trade fixture must be remote or local.");
        return mode;
    }

    internal static bool BoundedTradePreviewEnabled =>
        System.Environment.GetEnvironmentVariable("GA_NEXT_BOUNDED_TRADE_PREVIEW") == "1";

    internal static bool UnitOpeningPreviewEnabled =>
        System.Environment.GetEnvironmentVariable("GA_NEXT_R2318_UNIT_OPENING_PREVIEW") == "1";

    internal static bool DemographicPreviewEnabled =>
        System.Environment.GetEnvironmentVariable("GA_NEXT_DEMOGRAPHIC_PREVIEW") == "1";

    internal static IReadOnlyDictionary<CropId, CommodityId> CropItems() =>
        CropCatalog.All.ToDictionary(c => c.Id, c => new CommodityId((uint)c.Id + 2U));

    internal static FoundationGameplayEconomicConfiguration Profile(bool crops = false, bool localEconomy = false, bool boundedTrade = false, bool unitDemand = false) =>
        new(
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(QaLandEdgeCostMicro, 1_000_000UL),
                new FoundationNaturalTransportCostBand(QaCoastEdgeCostMicro, 500_000UL),
                new FoundationNaturalTransportCostBand(QaSeaEdgeCostMicro, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL),
            new FoundationAuthoritativeEconomicSettlementPolicy(
                new FoundationActivityCommodityDemandPolicy(
                    MarketItems(crops),
                    Enum.GetValues<FoundationPhysicalDemandProfile>()
                        .SelectMany(activity =>
                            MarketItems(crops).Select(item =>
                                new FoundationActivityCommodityDemandCoefficient(
                                    activity, item, !unitDemand && crops && item.Value > 2U ? 166_667UL : 1_000_000UL)))),
                new FoundationCommodityCargoConversionPolicy(
                    "explicit-godot-economic-qa-only",
                    MarketItems(crops).Select(item =>
                        new FoundationCommodityCargoConversion(item, QaCargoMicroPerWholeCommodity))),
                new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                },
                maxProducerPasses: 64, useTemporalProgressiveDistribution: localEconomy,
                requireSafeProgressiveRoutes: localEconomy, useBoundedSafeProgressiveRoutes: boundedTrade),
            new[]
            {
                (Red, 0UL),
                (Blue, 0UL)
            });

    private static CommodityId[] MarketItems(bool crops) => crops
        ? new[] { CommodityOne, CommodityTwo }.Concat(CropItems().Values).ToArray()
        : new[] { CommodityOne, CommodityTwo };

    /// <summary>
    /// Deterministic, explicit QA specialization. No invented worker IDs,
    /// locations, production capacities, work, stores or extra demand.
    /// It only changes the two existing demo commodities before turn 1,
    /// forcing a real interregional market test. NEVER call in normal mode.
    /// </summary>
    /// <summary>
    /// R87 isolated, QA-only G18 opening: the SAME canonical fine physical
    /// field is signed, used for economic freight, and handed to Godot.
    /// This does not change the normal gameplay economy configuration.
    /// </summary>
    internal static (
        FoundationTacticalHumanAiTurnHost Host,
        FoundationFineFirstPhysicalGeography Geography,
        FoundationFineFirstWorldgenIdentity Identity) PrepareFineFirst(
            FoundationTacticalHumanAiTurnHost demo,
            bool enableTemporalFinancialVisualSmoke = false,
            bool enableConstructionPreview = false,
            bool enablePersistentOrders = false, bool enableCropPreview = false, bool enableLocalEconomy = false)
    {
        ArgumentNullException.ThrowIfNull(demo);
        AssertNaturalFreightHierarchy();
        var tradeFixture = TradeFixtureMode();
        bool growth = DemographicPreviewEnabled;
        bool unitOpening = UnitOpeningPreviewEnabled;
        if (unitOpening && (!BoundedTradePreviewEnabled || !growth || !enableLocalEconomy || tradeFixture is not null))
            throw new InvalidOperationException("Unit opening requires bounded demographic local economy, without trade fixture.");
        if (BoundedTradePreviewEnabled && (!growth || !enableLocalEconomy || tradeFixture is not null))
            throw new InvalidOperationException("Bounded trade preview requires the normal demographic local economy.");
        if (growth && (!enableLocalEconomy || !enablePersistentOrders || tradeFixture is not null))
            throw new InvalidOperationException("Demographic preview requires the signed persistent local economy, without trade acceptance fixture.");
        if (tradeFixture is not null && (!enableLocalEconomy ||
            System.Environment.GetEnvironmentVariable("GA_NEXT_R213_UI_SMOKE") != "1"))
            throw new InvalidOperationException("R231.3 trade fixture is acceptance-only and requires the eight-item local economy smoke.");
        if (enableLocalEconomy && (!enableCropPreview || !enablePersistentOrders || !enableConstructionPreview))
            throw new InvalidOperationException("Local economy preview requires signed persistent crop and construction QA.");
        if (enableCropPreview && !enablePersistentOrders) throw new InvalidOperationException("Crop preview requires persistent orders.");
        if (enablePersistentOrders && !enableConstructionPreview)
            throw new InvalidOperationException("Persistent preview requires construction preview.");
        if (demo.StateHash.FormatVersion != 11U ||
            demo.Snapshot.Workers.Workers.Count != 6 ||
            demo.Snapshot.Transport.IsInitialized ||
            demo.Snapshot.Treasury.IsInitialized)
            throw new InvalidOperationException(
                "R87 G18 visual QA requires the pristine six-worker fixture.");

        // Select existing canonical identities before economic initialization and queue deployment.
        // Legacy six-worker acceptance scenarios remain unchanged.
        var openingWorkers = demo.Snapshot.Workers.Workers.AsEnumerable();
        if (unitOpening)
            openingWorkers = openingWorkers.GroupBy(w => w.Owner)
                .Select(g => g.OrderBy(w => w.Id.Value).First()).OrderBy(w => w.Id.Value);
        var specialized = new PhysicalWorkerRuntimeState(
            openingWorkers.Select(w =>
                new PhysicalWorkerRuntimeRecord(
                    w.Id, w.Owner, w.CellId,
                    w.Owner == Red ? CommodityOne : CommodityTwo,
                    w.OutputMicroPerTurn, w.Activity,
                    w.DemandMultiplierMicro, w.TacticalTileId,
                    w.Occupation)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        demo.Session.AcceptSnapshot(demo.Snapshot.WithWorkers(specialized));

        var world = demo.Session.GeneratedWorld;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            world, demo.Incidence);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, demo.Incidence, fine);
        var configuration = Profile(enableCropPreview, enableLocalEconomy, BoundedTradePreviewEnabled, unitOpening);
        // QA-only provisional equal basic-resource yields; existing commodity 1/2 are wood/stone only in this experiment.
        var basic = enableLocalEconomy ? new FoundationBasicResourceProductionPolicy(CommodityOne, CommodityTwo, 1_000_000UL, 1_000_000UL) : null;
        var agriculture = enableCropPreview ? new FoundationAgriculturalProductionPolicy(fine, demo.Incidence, CropItems(), 2_000_000UL, basic) : null;
        var shallowMode = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_QA_SHALLOW_PORTS") == "1";
        FoundationExperimentalShallowFreightPolicy? shallow = null;
        if (shallowMode)
        {
            // R119 mild-p25 A/B: same G18 fine-first sea-depth quantile
            // determines the research cutoff, with NO invented meter scale.
            // Explicit QA-only switch, absent in normal gameplay.
            var sortedSeaDepths = fine.FineElevationRaw
                .Where(elevation => elevation <= fine.SeaLevelRaw)
                .Select(elevation => fine.SeaLevelRaw - elevation)
                .OrderBy(depth => depth).ToArray();
            if (sortedSeaDepths.Length == 0)
                throw new InvalidOperationException(
                    "Shallow port QA requires an actual navigable sea.");
            long threshold = sortedSeaDepths[
                (sortedSeaDepths.Length - 1) / 4];
            if (threshold <= 0)
                throw new InvalidOperationException(
                    "Shallow port QA requires a positive relative depth.");
            shallow = new FoundationExperimentalShallowFreightPolicy(
                threshold, 10U, 3U, 20U);
            global::Godot.GD.Print($"GA_NEXT_R120_SHALLOW_PORT_AB_ENABLED " +
                $"threshold_raw={threshold} coast_operating_pct=10 " +
                "sea_operating_pct=3 port_construction_pct=20 " +
                "depth_is_not_meters=true experimental=true");
        }
        else
            global::Godot.GD.Print("GA_NEXT_R120_SHALLOW_PORT_AB_BASELINE enabled=false");
        var opening = FoundationFineFirstEconomicCycleGate.OpenNewEconomy(
            demo.Snapshot, world, demo.Incidence, fine, identity,
            configuration, shallow);
        demo.Session.AcceptFineFirstOpening(
            opening, demo.Incidence, fine, identity);
        if (enableTemporalFinancialVisualSmoke || enableConstructionPreview)
        {
            // R179 opt-in on the SAME signed G18 opening and existing worker
            // census. Required by the R167/R170 travel and financial journal;
            // never installed in ordinary or legacy visual QA sessions.
            demo.Session.AcceptSnapshot(
                FoundationPersistedEdgeTravelTransitions.DeployEmpty(
                    demo.Session.Snapshot));
        }
        if (enableConstructionPreview && !unitOpening)
        {
            // QA-only one existing idle builder; retain all identities and locations.
            var snapshot = demo.Session.Snapshot;
            var builder = snapshot.Workers.Workers.First(w => w.Owner == Red && w.Occupation == PhysicalWorkerOccupation.Forestry);
            var workers = new PhysicalWorkerRuntimeState(snapshot.Workers.Workers.Select(w => w.Id == builder.Id
                ? w.WithTacticalAssignment(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned) : w),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
            demo.Session.AcceptSnapshot(snapshot.WithWorkers(workers).WithConstructions(
                new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>())));
        }
        if (tradeFixture is not null)
        {
            // Keep the signed fine-first world, canonical edge costs, six real
            // identities and eight-item policy. Only the opening placements
            // differ. Both cells share a chart so the positive edge is resident.
            var incidence = demo.Incidence;
            var crossing = incidence.FineTopology.Edges
                .Where(e => fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                    (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground)
                .OrderBy(e => e.Id.Value)
                .First(e => incidence.TileIncidences[(int)e.IncidentCellIds[0].Value - 1].IncidentCoarseCellIds
                    .Intersect(incidence.TileIncidences[(int)e.IncidentCellIds[1].Value - 1].IncidentCoarseCellIds).Any());
            var source = crossing.IncidentCellIds[0];
            var destination = tradeFixture == "remote" ? crossing.IncidentCellIds[1] : source;
            var snapshot = demo.Snapshot;
            var census = snapshot.Workers.Workers.Select(w =>
            {
                var cell = w.Owner == Red ? source : destination;
                var region = incidence.TileIncidences[(int)cell.Value - 1].IncidentCoarseCellIds
                    .OrderBy(id => id.Value).First();
                return w.WithTacticalLocationAndActivity(
                    new PhysicalTacticalTileId(incidence.Refinement.FineParameters, cell), region, w.Activity);
            });
            demo.Session.AcceptSnapshot(snapshot.WithWorkers(new PhysicalWorkerRuntimeState(census,
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true)));
            global::Godot.GD.Print($"GA_NEXT_R2313_FIXTURE mode={tradeFixture} source={source.Value} destination={destination.Value} workers={snapshot.Workers.Workers.Count} policy=unchanged");
        }
        if (enableConstructionPreview && unitOpening)
            demo.Session.AcceptSnapshot(demo.Snapshot.WithConstructions(
                new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>())));
        if (enablePersistentOrders)
            demo.Session.AcceptSnapshot(demo.Snapshot.DeployInitialWorkerOrders());
        var host = new FoundationTacticalHumanAiTurnHost(
            demo.Session, demo.Incidence, demo.HumanCivilizations,
            // This fixture's AI is the existing resume-production policy.
            // Do not access simulation-internal bootstrap helpers from Godot.
            demo.AiCivilizations.ToDictionary(
                civ => civ,
                civ => enablePersistentOrders ? (IFoundationTacticalAiTurnPolicy)new FoundationTacticalAiHoldPolicy() :
                    new FoundationTacticalAiResumeProductionPolicy()),
            enableMonetaryInfrastructure: true,
            settlementPolicy: configuration.Settlement,
            fineGeography: fine, fineSignature: identity,
            physicalTravelPolicy: enableTemporalFinancialVisualSmoke || enableConstructionPreview
                ? new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)
                : null,
            constructionMoneyMicroPerWholeWork: enableConstructionPreview ? 1_000_000UL : null,
            enableDemographicGrowth: growth, agriculture: agriculture,
            operationalPolicy: enablePersistentOrders && System.Environment.GetEnvironmentVariable("GA_NEXT_OPERATIONAL_PREVIEW") == "1"
                ? new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL) : null,
            enableExperimentalLocalEconomy: enableLocalEconomy, enableDefaultAutomaticBase: enableLocalEconomy);
        if (enableLocalEconomy) global::Godot.GD.Print("GA_NEXT_R22813C_LOCAL_ECONOMY_READY experimental=true automatic_default=true commodities=8");
        return (host, fine, identity);
    }

    internal static FoundationTacticalHumanAiTurnHost Prepare(
        FoundationTacticalHumanAiTurnHost demo)
    {
        ArgumentNullException.ThrowIfNull(demo);
        if (demo.StateHash.FormatVersion != 11U ||
            demo.Snapshot.Workers.Workers.Count != 6 ||
            demo.Snapshot.Workers.Workers.Count(w => w.Owner == Red) != 3 ||
            demo.Snapshot.Workers.Workers.Count(w => w.Owner == Blue) != 3)
            throw new InvalidOperationException(
                "QA economic trade needs the exact six real worker fixture.");
        var specialized = new PhysicalWorkerRuntimeState(
            demo.Snapshot.Workers.Workers.Select(w =>
                new PhysicalWorkerRuntimeRecord(
                    w.Id, w.Owner, w.CellId,
                    w.Owner == Red ? CommodityOne : CommodityTwo,
                    w.OutputMicroPerTurn, w.Activity,
                    w.DemandMultiplierMicro, w.TacticalTileId,
                    w.Occupation)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        demo.Session.AcceptSnapshot(demo.Snapshot.WithWorkers(specialized));
        return FoundationGameplayEconomicBootstrap.Enable(
            demo, Profile());
    }
}
