using GlobalArena.Runtime;
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

    internal static void AssertNaturalFreightHierarchy()
    {
        if (!(QaSeaEdgeCostMicro < QaLandEdgeCostMicro &&
            QaLandEdgeCostMicro < QaCoastEdgeCostMicro))
            throw new InvalidOperationException(
                "QA natural freight must strongly favor open sea and " +
                "discourage unnecessary port transitions.");
    }

    internal static FoundationGameplayEconomicConfiguration Profile() =>
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
                    new[] { CommodityOne, CommodityTwo },
                    Enum.GetValues<FoundationPhysicalDemandProfile>()
                        .SelectMany(activity =>
                            new[] { CommodityOne, CommodityTwo }.Select(item =>
                                new FoundationActivityCommodityDemandCoefficient(
                                    activity, item, 1_000_000UL)))),
                new FoundationCommodityCargoConversionPolicy(
                    "explicit-godot-economic-qa-only",
                    new[]
                    {
                        new FoundationCommodityCargoConversion(
                            CommodityOne, 1_000_000UL),
                        new FoundationCommodityCargoConversion(
                            CommodityTwo, 1_000_000UL)
                    }),
                new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                },
                maxProducerPasses: 64),
            new[]
            {
                (Red, 0UL),
                (Blue, 0UL)
            });

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
            bool enableConstructionPreview = false)
    {
        ArgumentNullException.ThrowIfNull(demo);
        AssertNaturalFreightHierarchy();
        if (demo.StateHash.FormatVersion != 11U ||
            demo.Snapshot.Workers.Workers.Count != 6 ||
            demo.Snapshot.Transport.IsInitialized ||
            demo.Snapshot.Treasury.IsInitialized)
            throw new InvalidOperationException(
                "R87 G18 visual QA requires the pristine six-worker fixture.");

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

        var world = demo.Session.GeneratedWorld;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            world, demo.Incidence);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, demo.Incidence, fine);
        var configuration = Profile();
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
        if (enableConstructionPreview)
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
        var host = new FoundationTacticalHumanAiTurnHost(
            demo.Session, demo.Incidence, demo.HumanCivilizations,
            // This fixture's AI is the existing resume-production policy.
            // Do not access simulation-internal bootstrap helpers from Godot.
            demo.AiCivilizations.ToDictionary(
                civ => civ,
                civ => (IFoundationTacticalAiTurnPolicy)
                    new FoundationTacticalAiResumeProductionPolicy()),
            enableMonetaryInfrastructure: true,
            settlementPolicy: configuration.Settlement,
            fineGeography: fine, fineSignature: identity,
            physicalTravelPolicy: enableTemporalFinancialVisualSmoke || enableConstructionPreview
                ? new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)
                : null,
            constructionMoneyMicroPerWholeWork: enableConstructionPreview ? 1_000_000UL : null);
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
