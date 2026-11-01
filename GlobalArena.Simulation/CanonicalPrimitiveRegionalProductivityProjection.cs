using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A declared unit-normalization reference for one physical primitive flow.
/// Distinct commodities cannot be added directly as if their raw units were
/// interchangeable; only ratios to declared per-worker references are added.
/// </summary>
public sealed record CanonicalPrimitiveProductivityReference(
    CommodityId Commodity,
    EconomicActivityKind Activity,
    ulong ReferenceRawPerWorker);

public sealed record CanonicalPrimitiveRegionalPurchasingRow(
    EconomicPointId Buyer,
    PhysicalTacticalTileId Tile,
    ulong ResidentWorkers,
    FoodTransportCostFraction ReferenceNormalizedOutputPerResident,
    FoodTransportCostFraction PurchasingProductivityFactor);

/// <summary>
/// An immutable, session-bound, read-only calibration certificate derived
/// exclusively from emergent inhabited sites and actual current supply flows.
/// This is NOT a wage table, bank balance, saved population state, nor
/// permanent purchasing policy; it is an explicit laboratory hypothesis.
/// </summary>
public sealed class CanonicalPrimitiveRegionalProductivityProjection
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    public IReadOnlyList<CanonicalPrimitiveRegionalPurchasingRow> Buyers { get; }
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    private CanonicalPrimitiveRegionalProductivityProjection(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveRegionalPurchasingRow[] buyers)
    {
        _session = session;
        _sites = sites;
        Buyers = Array.AsReadOnly(buyers);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Regional purchasing calibration belongs to another physical session.");
        _sites.RequireCurrent(session);
    }

    public ExperimentalAbstractPurchasingPolicy ApplyTo(
        ExperimentalAbstractPurchasingPolicy referencePolicy)
    {
        ArgumentNullException.ThrowIfNull(referencePolicy);
        RequireCurrent(_session);
        if (referencePolicy.RegionalProductivity is not null)
            throw new ArgumentException(
                "Regional factors cannot be compounded or silently counted twice.",
                nameof(referencePolicy));
        return new ExperimentalAbstractPurchasingPolicy(
            referencePolicy.PolicyId, referencePolicy.Version,
            referencePolicy.BasePurchasingUnitsPerResident,
            referencePolicy.ProtectedInvisibleUnitsPerResident, this);
    }

    public static CanonicalPrimitiveRegionalProductivityProjection FromSites(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalPrimitiveProductivityReference> references,
        FoodTransportCostFraction minimumFactor,
        FoodTransportCostFraction maximumFactor)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(minimumFactor);
        ArgumentNullException.ThrowIfNull(maximumFactor);
        sites.RequireCurrent(session);
        if (minimumFactor.Numerator <= 0 ||
            maximumFactor.CompareTo(minimumFactor) < 0 ||
            maximumFactor.CompareTo(new FoodTransportCostFraction(16, 1)) > 0)
            throw new ArgumentException(
                "Explicit positive productivity factor bounds must lie within 16x.");
        var declarations = references.ToArray();
        if (declarations.Length is < 1 or > 12 ||
            declarations.Any(x => x is null ||
                !x.Commodity.IsValid ||
                x.Activity is not (EconomicActivityKind.Agriculture or
                    EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
                x.ReferenceRawPerWorker == 0UL) ||
            declarations.Select(x => (x.Commodity, x.Activity)).Distinct().Count() !=
                declarations.Length)
            throw new ArgumentException(
                "Every physical product/activity requires one positive normalized reference.",
                nameof(references));
        var normalizers = declarations.ToDictionary(
            x => (x.Commodity, x.Activity), x => x.ReferenceRawPerWorker);
        var usedFlows = sites.Flows.SupplyFlows
            .Select(x => (x.CommodityId, x.Activity)).Distinct().ToHashSet();
        if (!normalizers.Keys.ToHashSet().SetEquals(usedFlows))
            throw new ArgumentException(
                "Reference normalization must cover exactly the current productive commodity/activity flows.",
                nameof(references));
        var results = new List<CanonicalPrimitiveRegionalPurchasingRow>();
        foreach (var site in sites.Sites.OrderBy(x => x.Point.Value))
        {
            if (site.ResidentWorkers == 0UL)
                throw new InvalidOperationException(
                    "Regional purchasing calibration requires inhabited physical sites.");
            var score = new FoodTransportCostFraction(0, 1);
            foreach (var flow in site.Supply)
            {
                var baseline = normalizers[(flow.CommodityId, flow.Activity)];
                score = ExperimentalCashMoney.Add(score,
                    new FoodTransportCostFraction(
                        (BigInteger)flow.RawQuantity,
                        (BigInteger)baseline * site.ResidentWorkers));
            }
            // Output per resident may be zero; the laboratory floor avoids
            // silently excluding people from consumption or demand.
            var factor = score.CompareTo(minimumFactor) < 0
                ? minimumFactor : score.CompareTo(maximumFactor) > 0
                    ? maximumFactor : score;
            results.Add(new(
                site.Point, site.Tile, site.ResidentWorkers, score, factor));
        }
        if (!results.Select(x => x.Buyer).ToHashSet()
                .SetEquals(sites.BuyerAnchors.Select(x => x.Buyer)))
            throw new InvalidOperationException(
                "Every residential buyer must have one physically grounded factor.");
        var result = new CanonicalPrimitiveRegionalProductivityProjection(
            session, sites, results.ToArray());
        result.RequireCurrent(session);
        return result;
    }
}
