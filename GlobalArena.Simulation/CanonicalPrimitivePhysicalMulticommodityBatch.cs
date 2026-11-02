using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A macro-level physical utilization hypothesis. 1000 means the complete
/// live primitive capacity is feasible, not that extraction has happened.
/// </summary>
public sealed record CanonicalPrimitivePhysicalUtilization(
    EconomicActivityKind Activity, int Permille);

/// <summary>
/// One authenticated inhabited economic point and primitive commodity.
/// Feasible is a trial physical quantity, NEVER produced inventory or a sale.
/// </summary>
public sealed record CanonicalPrimitivePhysicalSourcePlan(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong ResidentWorkers,
    ulong AssignedActivityWorkers, ulong PotentialRaw,
    ulong ExperimentallyFeasibleRaw, ulong CapacityNotExtractedRaw);

/// <summary>
/// The executed-food witness is intentionally kept in separate fields from
/// experimental production. Wood/stone execution and money remain unknown.
/// </summary>
public sealed record CanonicalPrimitivePhysicalCommodityPlan(
    CommodityId Commodity, EconomicActivityKind Activity,
    ulong AssignedActivityWorkers,
    BigInteger PotentialRaw, BigInteger ExperimentallyFeasibleRaw,
    BigInteger CapacityNotExtractedRaw, BigInteger ProjectedDemandPressureRaw,
    BigInteger? IndependentlyExecutedLocalPurchaseRaw,
    BigInteger? IndependentlyPaidMicrocredits,
    string? ExecutedNominalUnitId);

internal sealed record CanonicalPrimitivePhysicalBatchReconstruction(
    CanonicalPrimitivePhysicalSourcePlan[] Sources,
    CanonicalPrimitivePhysicalCommodityPlan[] Commodities);

/// <summary>
/// VS94: a whole-world, cycle-aligned physical realizability ledger for
/// food/wood/stone. Input comes only from authenticated live worker sites,
/// with the VS93 execution-evidence boundary as an independent verifier.
/// No arbitrarily limited list of 12 source pools, buyer-money assumption,
/// demand cap, inventory, actual extraction, freight or SETTLE.
/// </summary>
public sealed class CanonicalPrimitivePhysicalMulticommodityBatch
{
    public const int PermilleScale = 1000;

    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitiveCommodityExecutionBoundary _evidence;
    private readonly CanonicalPrimitivePhysicalUtilization[] _policies;
    private readonly CanonicalPrimitivePhysicalSourcePlan[] _sources;
    private readonly CanonicalPrimitivePhysicalCommodityPlan[] _commodities;

    public IReadOnlyList<CanonicalPrimitivePhysicalUtilization> Policies { get; }
    public IReadOnlyList<CanonicalPrimitivePhysicalSourcePlan> Sources { get; }
    public IReadOnlyList<CanonicalPrimitivePhysicalCommodityPlan> Commodities { get; }
    public ulong CompletedEconomicCycleMovementTurn =>
        _evidence.CompletedEconomicCycleMovementTurn;

    public bool IsReadOnlyPhysicalExperiment => true;
    public bool HasLivePhysicalWorkforceProvenance => true;
    public bool LimitsConsumptionToProjectedDemand => false;
    public bool CreatesInventoriesOrInvoluntaryStocks => false;
    public bool ExecutesProductionOrSales => false;
    public bool DerivesActualWoodOrStonePrices => false;
    public bool ImplementsAbstractPurchasingPower => false;
    public bool CanAuthorizeSettlement => false;

    private CanonicalPrimitivePhysicalMulticommodityBatch(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveCommodityExecutionBoundary evidence,
        CanonicalPrimitivePhysicalUtilization[] policies,
        CanonicalPrimitivePhysicalBatchReconstruction computed)
    {
        _session = session;
        _sites = sites;
        _evidence = evidence;
        _policies = (CanonicalPrimitivePhysicalUtilization[])policies.Clone();
        _sources = computed.Sources;
        _commodities = computed.Commodities;
        Policies = Array.AsReadOnly(_policies);
        Sources = Array.AsReadOnly(_sources);
        Commodities = Array.AsReadOnly(_commodities);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "VS94 physical batch belongs to a different session.");
        _sites.RequireCurrent(session);
        _evidence.RequireCurrent(session);
    }

    private static ulong Workers(
        CanonicalPrimitiveEconomicSite site,
        EconomicActivityKind activity) => activity switch
    {
        EconomicActivityKind.Agriculture => site.AgriculturalWorkers,
        EconomicActivityKind.Forestry => site.ForestryWorkers,
        EconomicActivityKind.Mining => site.MiningWorkers,
        _ => throw new InvalidOperationException(
            "Only certified primitive activities can consume physical capacity.")
    };

    private static CanonicalPrimitivePhysicalBatchReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveCommodityExecutionBoundary evidence,
        CanonicalPrimitivePhysicalUtilization[] policies)
    {
        sites.RequireCurrent(session);
        evidence.RequireCurrent(session);
        evidence.VerifyIndependent();

        var mapped = evidence.Commodities.ToDictionary(x => x.Activity);
        if (mapped.Count != 3 ||
            !mapped.ContainsKey(EconomicActivityKind.Agriculture) ||
            !mapped.ContainsKey(EconomicActivityKind.Forestry) ||
            !mapped.ContainsKey(EconomicActivityKind.Mining) ||
            mapped.Values.Select(x => x.Commodity).Distinct().Count() != 3)
            throw new InvalidOperationException(
                "VS94 requires three distinct VS93-certified primitive goods.");

        var rates = policies.ToDictionary(x => x.Activity, x => x.Permille);
        var rows = new List<CanonicalPrimitivePhysicalSourcePlan>();
        foreach (var site in sites.Sites.OrderBy(x => x.Point.Value))
        {
            foreach (var policy in policies)
            {
                var commodity = mapped[policy.Activity].Commodity;
                var flow = site.Supply.SingleOrDefault(x =>
                    x.CommodityId == commodity && x.Activity == policy.Activity);
                var capacity = flow?.RawQuantity ?? 0UL;
                var assigned = Workers(site, policy.Activity);
                if (capacity > 0UL && assigned == 0UL)
                    throw new InvalidOperationException(
                        "A positive physical supply requires workers on its source and activity.");
                var raw = (BigInteger)capacity * rates[policy.Activity] /
                    PermilleScale;
                var feasible = checked((ulong)raw);
                rows.Add(new(site.Point, commodity, policy.Activity,
                    site.ResidentWorkers, assigned, capacity, feasible,
                    checked(capacity - feasible)));
            }
        }

        // Check all source flows were included, even if the policy is sparse
        // or a future primitive mapping is changed. Never silently truncate.
        if (rows.Count != checked(sites.Sites.Count * 3) ||
            sites.Flows.SupplyFlows.Count != rows.Count(x => x.PotentialRaw > 0UL))
            throw new InvalidOperationException(
                "Physical production ledger omitted or invented a source flow.");

        var totals = new List<CanonicalPrimitivePhysicalCommodityPlan>();
        foreach (var policy in policies)
        {
            var witnessed = mapped[policy.Activity];
            var perCommodity = rows.Where(x =>
                x.Activity == policy.Activity).ToArray();
            var potential = perCommodity.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.PotentialRaw);
            var feasible = perCommodity.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.ExperimentallyFeasibleRaw);
            var unused = perCommodity.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.CapacityNotExtractedRaw);
            var workers = perCommodity.Aggregate(0UL,
                (sum, x) => checked(sum + x.AssignedActivityWorkers));
            if (potential != witnessed.ProjectedPrimitivePotentialRaw ||
                workers != witnessed.ResidentProducerWorkers ||
                feasible + unused != potential)
                throw new InvalidOperationException(
                    "Real-worker source production disagrees with VS93 provenance.");

            totals.Add(new(witnessed.Commodity, witnessed.Activity,
                workers, potential, feasible, unused,
                witnessed.ProjectedResidentDemandPressureRaw,
                witnessed.ActualLocalPurchasedRaw,
                witnessed.ActuallyPaidMicrocredits,
                witnessed.ExecutedNominalUnitId));
        }

        if (evidence.PhysicalResidentWorkers != sites.ResidentWorkers ||
            totals.Aggregate(0UL, (sum, x) =>
                checked(sum + x.AssignedActivityWorkers)) > sites.ResidentWorkers ||
            totals.Any(x => x.Commodity != mapped[x.Activity].Commodity))
            throw new InvalidOperationException(
                "VS94 cannot invent workers or change primitive commodity identities.");
        sites.RequireCurrent(session);
        evidence.RequireCurrent(session);
        return new(rows.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity).ToArray(),
            totals.OrderBy(x => x.Commodity.Value).ToArray());
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Reconstruct(_session, _sites, _evidence, _policies);
        if (!_sources.SequenceEqual(replay.Sources) ||
            !_commodities.SequenceEqual(replay.Commodities))
            throw new InvalidOperationException(
                "VS94 independent physical conservation replay diverged.");
        RequireCurrent(_session);
    }

    public static CanonicalPrimitivePhysicalMulticommodityBatch FromCertifiedEvidence(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery currentSites,
        CanonicalPrimitiveCommodityExecutionBoundary certifiedVs93,
        IEnumerable<CanonicalPrimitivePhysicalUtilization> utilization)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentSites);
        ArgumentNullException.ThrowIfNull(certifiedVs93);
        ArgumentNullException.ThrowIfNull(utilization);
        var p = utilization.ToArray();
        if (p.Length != 3 ||
            p.Any(x => x is null ||
                x.Activity is not (
                    EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or
                    EconomicActivityKind.Mining) ||
                x.Permille is < 0 or > PermilleScale) ||
            p.Select(x => x.Activity).Distinct().Count() != 3)
            throw new ArgumentException(
                "VS94 requires three unique primitive utilization rates in [0,1000].",
                nameof(utilization));
        var ordered = p.OrderBy(x => x.Activity).ToArray();
        var computed = Reconstruct(
            session, currentSites, certifiedVs93, ordered);
        var batch = new CanonicalPrimitivePhysicalMulticommodityBatch(
            session, currentSites, certifiedVs93, ordered, computed);
        batch.VerifyIndependent();
        return batch;
    }
}
