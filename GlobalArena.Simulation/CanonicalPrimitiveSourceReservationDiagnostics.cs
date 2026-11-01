using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Conditional physical headroom at one source, for ONE commodity and ONE
/// mutually exclusive VS71 scenario. Selected outbound freight is held fixed;
/// this is NOT a decision to privilege exports, local consumption, or imports.
/// </summary>
public sealed record CanonicalPrimitiveSourceCommodityHeadroom(
    EconomicPointId Source,
    CommodityId Commodity,
    ulong PhysicalProductionRaw,
    ulong PhysicalLocalDemandRaw,
    ulong SelectedOutboundFreightRaw,
    ulong UnreservedPhysicalProductionRaw,
    ulong MaximumLocalUseAfterOutboundRaw,
    FoodTransportCostFraction MaximumLocalCoverageAfterOutbound,
    ulong SelectedInboundFreightRaw);

public sealed record CanonicalPrimitiveSourceReservationScenario(
    string ScenarioId,
    IReadOnlyList<CanonicalPrimitiveSourceCommodityHeadroom> Sources,
    FoodTransportCostFraction MeanConditionalLocalCoverage,
    int SourceCommodityPairsWithSelectedOutbound,
    int SourceCommodityPairsWithLowerLocalPotentialThanVs72);

/// <summary>
/// VS73 bounded audit only: reconcile already-selected, physically
/// authenticated freight against the same live VS72 production inventory.
/// No consumption, price change, reoptimization, persistent reservation,
/// transaction, wages, debt, or settlement is created.
/// </summary>
public sealed class CanonicalPrimitiveSourceReservationDiagnostics
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitiveLocalSupplyPotentialDiagnostics _local;

    public CanonicalPrimitiveSourceReservationScenario Baseline { get; }
    public CanonicalPrimitiveSourceReservationScenario Regional { get; }
    public CanonicalPrimitiveRegionalAccessDistribution RegionalFreightAccess =>
        _local.RegionalFreightAccess;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;
    public bool IsActualConsumption => false;
    public bool IsPersistentReservation => false;

    /// <summary>
    /// VS74: check an explicitly proposed local-use vector against ONE
    /// already-audited VS71 freight scenario. No local cash affordability,
    /// settlement or priority policy is implied.
    /// </summary>
    public CanonicalPrimitiveJointPhysicalUsePreview PreviewJointPhysicalUse(
        string scenarioId,
        IEnumerable<CanonicalPrimitiveLocalUseProposal> proposedLocalUses)
    {
        VerifyIndependent();
        return CanonicalPrimitiveJointPhysicalUsePreview.FromSourceAudit(
            _session, _sites, this, scenarioId, proposedLocalUses);
    }

    private CanonicalPrimitiveSourceReservationDiagnostics(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveLocalSupplyPotentialDiagnostics local,
        CanonicalPrimitiveSourceReservationScenario? baseline,
        CanonicalPrimitiveSourceReservationScenario? regional)
    {
        _session = session;
        _sites = sites;
        _local = local;
        Baseline = baseline!;
        Regional = regional!;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Source headroom belongs to a different physical session.");
        _sites.RequireCurrent(session);
        _local.RequireCurrent(session);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private CanonicalPrimitiveSourceReservationScenario Rebuild(
        CanonicalPrimitiveFreightScenarioOutcome outcome,
        CanonicalPrimitiveRegionalAccessScenario access)
    {
        // Source/activity conservation matters even when two different
        // activities happen to produce the same commodity.
        var physicalByFlow = _sites.Sites
            .SelectMany(site => site.Supply.Select(flow => new {
                Source = site.Point, flow.CommodityId, flow.Activity, flow.RawQuantity
            }))
            .GroupBy(x => (x.Source, Commodity: x.CommodityId, x.Activity))
            .ToDictionary(x => x.Key,
                x => x.Aggregate(0UL, (sum, f) =>
                    checked(sum + f.RawQuantity)));
        var reservedByFlow = outcome.Selection.Lots
            .GroupBy(x => (x.Candidate.Source,
                x.Candidate.Commodity, x.Candidate.Activity))
            .ToDictionary(x => x.Key,
                x => x.Aggregate(0UL, (sum, lot) =>
                    checked(sum + lot.RawQuantity)));

        foreach (var (key, reserved) in reservedByFlow)
            if (!physicalByFlow.TryGetValue(key, out var produced) ||
                reserved > produced)
                throw new InvalidOperationException(
                    "Selected outbound lots exceed authentic source/activity supply.");

        var reservedBySource = reservedByFlow
            .GroupBy(x => (x.Key.Source, x.Key.Commodity))
            .ToDictionary(x => x.Key,
                x => x.Aggregate(0UL, (sum, flow) =>
                    checked(sum + flow.Value)));
        var incomingByBuyer = outcome.Selection.Lots
            .GroupBy(x => (x.Candidate.Buyer, x.Candidate.Commodity))
            .ToDictionary(x => x.Key,
                x => x.Aggregate(0UL, (sum, lot) =>
                    checked(sum + lot.RawQuantity)));
        var buyers = access.Buyers.ToDictionary(x => x.Buyer);
        var rows = new List<CanonicalPrimitiveSourceCommodityHeadroom>();
        var mean = new FoodTransportCostFraction(0, 1);
        var pairs = 0;
        var withOutbound = 0;
        var reduced = 0;
        foreach (var site in _local.Snapshot.Buyers.OrderBy(x => x.Buyer.Value))
        {
            if (!buyers.TryGetValue(site.Buyer, out var buyer) ||
                buyer.Tile != site.Tile ||
                buyer.ResidentWorkers != site.ResidentWorkers)
                throw new InvalidOperationException(
                    "VS73 scenario does not match the physical local buyer.");
            var expectedIncoming = buyer.Commodities.ToDictionary(x => x.Commodity);
            foreach (var product in site.Commodities.OrderBy(x => x.Commodity.Value))
            {
                var key = (site.Buyer, product.Commodity);
                reservedBySource.TryGetValue(key, out var outbound);
                incomingByBuyer.TryGetValue(key, out var inbound);
                if (!expectedIncoming.TryGetValue(product.Commodity, out var accessProduct) ||
                    accessProduct.PhysicalDemandRaw != product.PhysicalDemandRaw ||
                    accessProduct.PlannedFreightRaw != inbound ||
                    product.PhysicalDemandRaw == 0 ||
                    outbound > product.PhysicalLocalProductionRaw)
                    throw new InvalidOperationException(
                        "VS73 physical supply, planned freight or current demand disagree.");
                var remaining = checked(product.PhysicalLocalProductionRaw - outbound);
                var maximumLocal = Math.Min(product.PhysicalDemandRaw, remaining);
                var coverage = new FoodTransportCostFraction(
                    maximumLocal, product.PhysicalDemandRaw);
                if (outbound > 0UL) withOutbound++;
                if (maximumLocal < product.MaximumHypotheticalLocalUseRaw) reduced++;
                mean = Add(mean, coverage);
                pairs++;
                rows.Add(new(site.Buyer, product.Commodity,
                    product.PhysicalLocalProductionRaw, product.PhysicalDemandRaw,
                    outbound, remaining, maximumLocal, coverage, inbound));
            }
        }

        // Do not silently omit any origin/commodity with selected freight,
        // including a future commodity that has no local demand in VS72.
        var covered = rows.Select(x => (x.Source, x.Commodity)).ToHashSet();
        if (pairs == 0 ||
            rows.Count != _sites.Flows.DemandFlows.Count ||
            reservedBySource.Keys.Any(x => !covered.Contains(x)) ||
            incomingByBuyer.Keys.Any(x => !covered.Contains(x)))
            throw new InvalidOperationException(
                "VS73 headroom cannot omit a selected source or buyer commodity.");

        return new CanonicalPrimitiveSourceReservationScenario(
            outcome.Scenario.ScenarioId, Array.AsReadOnly(rows.ToArray()),
            new FoodTransportCostFraction(mean.Numerator,
                mean.Denominator * pairs), withOutbound, reduced);
    }

    private static bool SameScenario(
        CanonicalPrimitiveSourceReservationScenario a,
        CanonicalPrimitiveSourceReservationScenario b)
    {
        if (a.ScenarioId != b.ScenarioId ||
            a.Sources.Count != b.Sources.Count ||
            a.SourceCommodityPairsWithSelectedOutbound !=
                b.SourceCommodityPairsWithSelectedOutbound ||
            a.SourceCommodityPairsWithLowerLocalPotentialThanVs72 !=
                b.SourceCommodityPairsWithLowerLocalPotentialThanVs72 ||
            a.MeanConditionalLocalCoverage.CompareTo(
                b.MeanConditionalLocalCoverage) != 0)
            return false;

        foreach (var (x, y) in a.Sources.Zip(b.Sources))
            if (x.Source != y.Source || x.Commodity != y.Commodity ||
                x.PhysicalProductionRaw != y.PhysicalProductionRaw ||
                x.PhysicalLocalDemandRaw != y.PhysicalLocalDemandRaw ||
                x.SelectedOutboundFreightRaw != y.SelectedOutboundFreightRaw ||
                x.UnreservedPhysicalProductionRaw != y.UnreservedPhysicalProductionRaw ||
                x.MaximumLocalUseAfterOutboundRaw !=
                    y.MaximumLocalUseAfterOutboundRaw ||
                x.MaximumLocalCoverageAfterOutbound.CompareTo(
                    y.MaximumLocalCoverageAfterOutbound) != 0 ||
                x.SelectedInboundFreightRaw != y.SelectedInboundFreightRaw)
                return false;
        return true;
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _local.VerifyIndependent();
        var comparison = _local.RegionalFreightAccess.Comparison;
        var baseline = Rebuild(comparison.Baseline,
            _local.RegionalFreightAccess.Baseline);
        var regional = Rebuild(comparison.Outcomes.Single(x =>
            x.Scenario.ScenarioId == "regional-productivity"),
            _local.RegionalFreightAccess.Regional);
        if (!SameScenario(Baseline, baseline) ||
            !SameScenario(Regional, regional))
            throw new InvalidOperationException(
                "VS73 physical source reservation failed independent replay.");
        RequireCurrent(_session);
    }

    internal static CanonicalPrimitiveSourceReservationDiagnostics FromLocal(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveLocalSupplyPotentialDiagnostics local)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(local);
        local.VerifyIndependent();
        sites.RequireCurrent(session);
        var provisional = new CanonicalPrimitiveSourceReservationDiagnostics(
            session, sites, local, null, null);
        var comparison = local.RegionalFreightAccess.Comparison;
        var baseline = provisional.Rebuild(comparison.Baseline,
            local.RegionalFreightAccess.Baseline);
        var regional = provisional.Rebuild(comparison.Outcomes.Single(x =>
            x.Scenario.ScenarioId == "regional-productivity"),
            local.RegionalFreightAccess.Regional);
        var report = new CanonicalPrimitiveSourceReservationDiagnostics(
            session, sites, local, baseline, regional);
        report.VerifyIndependent();
        return report;
    }
}
