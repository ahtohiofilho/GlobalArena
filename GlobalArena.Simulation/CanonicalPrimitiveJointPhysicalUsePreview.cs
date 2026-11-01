using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A caller-proposed self-provision quantity, NOT a transaction. The source
/// and the resident buyer are the same authenticated inhabited economic point.
/// Activity identifies a physical source; goods remain commodity-specific.
/// </summary>
public sealed record CanonicalPrimitiveLocalUseProposal(
    EconomicPointId Point, CommodityId Commodity,
    EconomicActivityKind Activity, ulong RawQuantity);

public sealed record CanonicalPrimitiveJointSourceUseRow(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong PhysicalProductionRaw,
    ulong SelectedOutboundFreightRaw, ulong ProposedLocalUseRaw,
    ulong UnallocatedPhysicalProductionRaw);

public sealed record CanonicalPrimitiveJointBuyerUseRow(
    EconomicPointId Buyer, CommodityId Commodity, ulong PhysicalDemandRaw,
    ulong SelectedInboundFreightRaw, ulong ProposedLocalUseRaw,
    ulong UnmetPhysicalDemandRaw);

internal sealed record CanonicalPrimitiveJointPhysicalUseSnapshot(
    IReadOnlyList<CanonicalPrimitiveJointSourceUseRow> Sources,
    IReadOnlyList<CanonicalPrimitiveJointBuyerUseRow> Buyers);

/// <summary>
/// VS74: physical-only preflight of an EXPLICIT trial local-use vector against
/// unchanged, independently audited VS71 freight lots of ONE scenario.
/// No new price, financial policy, market selector, precedence, consumption,
/// stock, shipment or settlement. The freight-only H budget is previously
/// audited; local purchasing affordability is deliberately NOT determined.
/// </summary>
public sealed class CanonicalPrimitiveJointPhysicalUsePreview
{
    public const int MaximumLocalProposals = 12;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitiveSourceReservationDiagnostics _sourceAudit;
    private readonly CanonicalPrimitiveLocalUseProposal[] _proposals;
    private readonly CanonicalPrimitiveJointPhysicalUseSnapshot _snapshot;

    public string ScenarioId { get; }
    public IReadOnlyList<CanonicalPrimitiveLocalUseProposal> ProposedLocalUses { get; }
    public IReadOnlyList<CanonicalPrimitiveJointSourceUseRow> Sources =>
        _snapshot.Sources;
    public IReadOnlyList<CanonicalPrimitiveJointBuyerUseRow> Buyers =>
        _snapshot.Buyers;
    public bool IsPhysicallyFeasible => true;
    public bool IsFreightBudgetIndependentlyAudited => true;
    public bool IsLocalPurchaseBudgetEvaluated => false;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsActualConsumption => false;
    public bool IsCommitted => false;
    public bool IsPersistentReservation => false;

    /// <summary>
    /// VS75 compares two local-cash accounting hypotheses and observes
    /// coverage without selecting a monetary or social-market policy.
    /// </summary>
    public CanonicalPrimitiveLocalBudgetShadowDiagnostics
        DiagnoseLocalBudgetHypotheses()
    {
        VerifyIndependent();
        return CanonicalPrimitiveLocalBudgetShadowDiagnostics.FromTrial(
            _session, this, _sourceAudit);
    }

    private CanonicalPrimitiveJointPhysicalUsePreview(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveSourceReservationDiagnostics sourceAudit,
        string scenarioId,
        CanonicalPrimitiveLocalUseProposal[] proposals,
        CanonicalPrimitiveJointPhysicalUseSnapshot snapshot)
    {
        _session = session;
        _sites = sites;
        _sourceAudit = sourceAudit;
        ScenarioId = scenarioId;
        _proposals = (CanonicalPrimitiveLocalUseProposal[])proposals.Clone();
        ProposedLocalUses = Array.AsReadOnly(_proposals);
        _snapshot = snapshot;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Physical-use preflight belongs to another physical session.");
        _sites.RequireCurrent(session);
        _sourceAudit.RequireCurrent(session);
    }

    private static CanonicalPrimitiveFreightScenarioOutcome Outcome(
        CanonicalPrimitiveSourceReservationDiagnostics sourceAudit,
        string scenarioId)
    {
        if (scenarioId != sourceAudit.Baseline.ScenarioId &&
            scenarioId != sourceAudit.Regional.ScenarioId)
            throw new ArgumentException(
                "Only the existing authenticated VS71 scenario identifiers are valid.",
                nameof(scenarioId));
        return sourceAudit.RegionalFreightAccess.Comparison.Outcomes.Single(
            x => x.Scenario.ScenarioId == scenarioId);
    }

    private static CanonicalPrimitiveJointPhysicalUseSnapshot Rebuild(
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveFreightScenarioOutcome outcome,
        IReadOnlyList<CanonicalPrimitiveLocalUseProposal> proposals)
    {
        var physicalSources = sites.Flows.SupplyFlows
            .ToDictionary(x => (Source: x.EconomicPointId,
                Commodity: x.CommodityId, x.Activity), x => x.RawQuantity);
        var physicalBuyers = sites.Flows.DemandFlows
            .ToDictionary(x => (Buyer: x.EconomicPointId,
                Commodity: x.CommodityId), x => x.RawQuantity);
        var selectedSources = new Dictionary<
            (EconomicPointId Source, CommodityId Commodity,
             EconomicActivityKind Activity), BigInteger>();
        var selectedBuyers = new Dictionary<
            (EconomicPointId Buyer, CommodityId Commodity), BigInteger>();
        foreach (var lot in outcome.Selection.Lots)
        {
            var s = (lot.Candidate.Source, lot.Candidate.Commodity,
                lot.Candidate.Activity);
            var b = (lot.Candidate.Buyer, lot.Candidate.Commodity);
            selectedSources.TryGetValue(s, out var prevSource);
            selectedBuyers.TryGetValue(b, out var prevBuyer);
            selectedSources[s] = prevSource + lot.RawQuantity;
            selectedBuyers[b] = prevBuyer + lot.RawQuantity;
        }

        var localSources = new Dictionary<
            (EconomicPointId Source, CommodityId Commodity,
             EconomicActivityKind Activity), BigInteger>();
        var localBuyers = new Dictionary<
            (EconomicPointId Buyer, CommodityId Commodity), BigInteger>();
        foreach (var proposal in proposals)
        {
            var s = (proposal.Point, proposal.Commodity, proposal.Activity);
            var b = (proposal.Point, proposal.Commodity);
            if (!physicalSources.ContainsKey(s) ||
                !physicalBuyers.ContainsKey(b))
                throw new ArgumentException(
                    "Local use must originate from physical production and demand on the SAME inhabited tile.",
                    nameof(proposals));
            localSources[s] = proposal.RawQuantity;
            localBuyers.TryGetValue(b, out var prevBuyer);
            localBuyers[b] = prevBuyer + proposal.RawQuantity;
        }

        // Never join different goods' raw quantities. Source capacity is
        // activity-specific; local buyer demand is commodity-specific.
        var sourceRows = new List<CanonicalPrimitiveJointSourceUseRow>();
        foreach (var (key, physical) in physicalSources
                     .OrderBy(x => x.Key.Source.Value)
                     .ThenBy(x => x.Key.Commodity.Value)
                     .ThenBy(x => x.Key.Activity))
        {
            selectedSources.TryGetValue(key, out var freight);
            localSources.TryGetValue(key, out var local);
            var residual = new BigInteger(physical) - freight - local;
            if (residual < 0)
                throw new ArgumentException(
                    "Local use plus selected freight exceeds physical source/activity supply.",
                    nameof(proposals));
            sourceRows.Add(new(key.Source, key.Commodity, key.Activity,
                physical, checked((ulong)freight), checked((ulong)local),
                checked((ulong)residual)));
        }
        var buyerRows = new List<CanonicalPrimitiveJointBuyerUseRow>();
        foreach (var (key, physical) in physicalBuyers
                     .OrderBy(x => x.Key.Buyer.Value)
                     .ThenBy(x => x.Key.Commodity.Value))
        {
            selectedBuyers.TryGetValue(key, out var freight);
            localBuyers.TryGetValue(key, out var local);
            var residual = new BigInteger(physical) - freight - local;
            if (residual < 0)
                throw new ArgumentException(
                    "Local use plus selected incoming freight exceeds physical resident commodity demand.",
                    nameof(proposals));
            buyerRows.Add(new(key.Buyer, key.Commodity, physical,
                checked((ulong)freight), checked((ulong)local),
                checked((ulong)residual)));
        }
        if (selectedSources.Keys.Any(x => !physicalSources.ContainsKey(x)) ||
            selectedBuyers.Keys.Any(x => !physicalBuyers.ContainsKey(x)))
            throw new InvalidOperationException(
                "Selected freight refers to unauthenticated physical flows.");
        return new CanonicalPrimitiveJointPhysicalUseSnapshot(
            Array.AsReadOnly(sourceRows.ToArray()),
            Array.AsReadOnly(buyerRows.ToArray()));
    }

    private static bool SameSnapshot(
        CanonicalPrimitiveJointPhysicalUseSnapshot a,
        CanonicalPrimitiveJointPhysicalUseSnapshot b) =>
        a.Sources.SequenceEqual(b.Sources) &&
        a.Buyers.SequenceEqual(b.Buyers);

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _sourceAudit.VerifyIndependent();
        var replay = Rebuild(_sites, Outcome(_sourceAudit, ScenarioId), _proposals);
        if (!SameSnapshot(_snapshot, replay))
            throw new InvalidOperationException(
                "Joint physical-use preflight failed independent conservation replay.");
        RequireCurrent(_session);
    }

    internal static CanonicalPrimitiveJointPhysicalUsePreview FromSourceAudit(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveSourceReservationDiagnostics sourceAudit,
        string scenarioId,
        IEnumerable<CanonicalPrimitiveLocalUseProposal> input)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(sourceAudit);
        ArgumentNullException.ThrowIfNull(input);
        sourceAudit.VerifyIndependent();
        sites.RequireCurrent(session);
        var proposals = input.ToArray();
        if (proposals.Length > MaximumLocalProposals ||
            proposals.Any(x => x is null || x.RawQuantity == 0UL) ||
            proposals.Select(x => (x.Point, x.Commodity, x.Activity))
                .Distinct().Count() != proposals.Length)
            throw new ArgumentException(
                "Preflight accepts up to 12 positive, unique local source/activity proposals.",
                nameof(input));
        var ordered = proposals.OrderBy(x => x.Point.Value)
            .ThenBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity).ToArray();
        var scenario = Outcome(sourceAudit, scenarioId);
        var snapshot = Rebuild(sites, scenario, ordered);
        var result = new CanonicalPrimitiveJointPhysicalUsePreview(
            session, sites, sourceAudit, scenarioId, ordered, snapshot);
        result.VerifyIndependent();
        return result;
    }
}
