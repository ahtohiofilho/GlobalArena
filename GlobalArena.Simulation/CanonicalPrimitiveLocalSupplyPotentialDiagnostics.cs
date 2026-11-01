using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Same-commodity, same-tile physical production is a potential source of
/// self-provision, not an allocation or proof of actual local consumption.
/// Planned incoming freight is reported separately and MUST NOT be added
/// to this potential without a shared source/corridor/budget allocation.
/// </summary>
public sealed record CanonicalPrimitiveLocalCommodityPotential(
    EconomicPointId Buyer,
    CommodityId Commodity,
    ulong PhysicalDemandRaw,
    ulong PhysicalLocalProductionRaw,
    ulong MaximumHypotheticalLocalUseRaw,
    FoodTransportCostFraction MaximumLocalCoverage,
    ulong BaselinePlannedFreightRaw,
    ulong RegionalPlannedFreightRaw);

public sealed record CanonicalPrimitiveLocalBuyerPotential(
    EconomicPointId Buyer,
    PhysicalTacticalTileId Tile,
    ulong ResidentWorkers,
    IReadOnlyList<CanonicalPrimitiveLocalCommodityPotential> Commodities);

public sealed record CanonicalPrimitiveLocalSupplyPotentialSnapshot(
    IReadOnlyList<CanonicalPrimitiveLocalBuyerPotential> Buyers,
    FoodTransportCostFraction MeanMaximumLocalCoverage,
    int BuyerCommodityPairsWithoutLocalProduction,
    int BuyerCommodityPairsWithFullLocalPotential);

/// <summary>
/// VS72: read-only, session-bound local production opportunity independent
/// of the two VS71 freight-selection hypotheses. Never treats production
/// capacity as inventory, delivery, consumption, or an executable trade.
/// </summary>
public sealed class CanonicalPrimitiveLocalSupplyPotentialDiagnostics
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitiveRegionalAccessDistribution _access;

    public CanonicalPrimitiveLocalSupplyPotentialSnapshot Snapshot { get; }
    public CanonicalPrimitiveRegionalAccessDistribution RegionalFreightAccess => _access;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;
    public bool IsActualConsumption => false;

    /// <summary>
    /// VS73 conditional, read-only source headroom given each already-selected
    /// VS71 freight hypothesis; does not order exports before residents.
    /// </summary>
    public CanonicalPrimitiveSourceReservationDiagnostics DiagnoseSourceReservations()
    {
        VerifyIndependent();
        return CanonicalPrimitiveSourceReservationDiagnostics.FromLocal(
            _session, _sites, this);
    }

    private CanonicalPrimitiveLocalSupplyPotentialDiagnostics(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveRegionalAccessDistribution access,
        CanonicalPrimitiveLocalSupplyPotentialSnapshot snapshot)
    {
        _session = session;
        _sites = sites;
        _access = access;
        Snapshot = snapshot;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Local supply potential belongs to another physical session.");
        _sites.RequireCurrent(session);
        _access.RequireCurrent(session);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private CanonicalPrimitiveLocalSupplyPotentialSnapshot Rebuild()
    {
        var baseline = _access.Baseline.Buyers.ToDictionary(x => x.Buyer);
        var regional = _access.Regional.Buyers.ToDictionary(x => x.Buyer);
        if (baseline.Count != _sites.Sites.Count ||
            regional.Count != _sites.Sites.Count)
            throw new InvalidOperationException(
                "Local potential must represent all current inhabited buyers.");

        var rows = new List<CanonicalPrimitiveLocalBuyerPotential>();
        var totalFraction = new FoodTransportCostFraction(0, 1);
        var pairCount = 0;
        var withoutProduction = 0;
        var potentiallyFull = 0;
        foreach (var site in _sites.Sites.OrderBy(x => x.Point.Value))
        {
            if (!baseline.TryGetValue(site.Point, out var uniform) ||
                !regional.TryGetValue(site.Point, out var calibrated) ||
                uniform.Tile != site.Tile || calibrated.Tile != site.Tile ||
                uniform.ResidentWorkers != site.ResidentWorkers ||
                calibrated.ResidentWorkers != site.ResidentWorkers)
                throw new InvalidOperationException(
                    "Local potential and regional freight refer to different physical buyers.");

            var before = uniform.Commodities.ToDictionary(x => x.Commodity);
            var after = calibrated.Commodities.ToDictionary(x => x.Commodity);
            if (before.Count != site.Demand.Count ||
                after.Count != site.Demand.Count)
                throw new InvalidOperationException(
                    "Local production comparison lost physical commodity demand.");

            var products = new List<CanonicalPrimitiveLocalCommodityPotential>();
            foreach (var demand in site.Demand.OrderBy(x => x.CommodityId.Value))
            {
                if (demand.RawQuantity == 0 ||
                    !before.TryGetValue(demand.CommodityId, out var first) ||
                    !after.TryGetValue(demand.CommodityId, out var second) ||
                    first.PhysicalDemandRaw != demand.RawQuantity ||
                    second.PhysicalDemandRaw != demand.RawQuantity)
                    throw new InvalidOperationException(
                        "Local potential must use the exact current physical demand.");

                // Supply is per commodity on this same inhabited tactical tile.
                // Different commodities cannot be combined into one raw quantity.
                var localSupply = site.Supply
                    .Where(x => x.CommodityId == demand.CommodityId)
                    .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));
                var hypotheticalUse = Math.Min(localSupply, demand.RawQuantity);
                var fraction = new FoodTransportCostFraction(
                    hypotheticalUse, demand.RawQuantity);
                if (localSupply == 0UL) withoutProduction++;
                if (hypotheticalUse == demand.RawQuantity) potentiallyFull++;
                pairCount++;
                totalFraction = Add(totalFraction, fraction);
                products.Add(new(
                    site.Point, demand.CommodityId, demand.RawQuantity,
                    localSupply, hypotheticalUse, fraction,
                    first.PlannedFreightRaw, second.PlannedFreightRaw));
            }

            if (products.Count == 0)
                throw new InvalidOperationException(
                    "Inhabited buyers must have physical commodity demand.");
            rows.Add(new(site.Point, site.Tile, site.ResidentWorkers,
                Array.AsReadOnly(products.ToArray())));
        }

        if (rows.Count == 0 || pairCount != _sites.Flows.DemandFlows.Count)
            throw new InvalidOperationException(
                "Local supply potential omitted current physical demand.");
        return new CanonicalPrimitiveLocalSupplyPotentialSnapshot(
            Array.AsReadOnly(rows.ToArray()),
            new FoodTransportCostFraction(
                totalFraction.Numerator, totalFraction.Denominator * pairCount),
            withoutProduction, potentiallyFull);
    }

    private static bool SameSnapshot(
        CanonicalPrimitiveLocalSupplyPotentialSnapshot a,
        CanonicalPrimitiveLocalSupplyPotentialSnapshot b)
    {
        if (a.Buyers.Count != b.Buyers.Count ||
            a.BuyerCommodityPairsWithoutLocalProduction !=
                b.BuyerCommodityPairsWithoutLocalProduction ||
            a.BuyerCommodityPairsWithFullLocalPotential !=
                b.BuyerCommodityPairsWithFullLocalPotential ||
            a.MeanMaximumLocalCoverage.CompareTo(b.MeanMaximumLocalCoverage) != 0)
            return false;

        foreach (var (left, right) in a.Buyers.Zip(b.Buyers))
        {
            if (left.Buyer != right.Buyer ||
                left.Tile != right.Tile ||
                left.ResidentWorkers != right.ResidentWorkers ||
                left.Commodities.Count != right.Commodities.Count)
                return false;
            foreach (var (x, y) in left.Commodities.Zip(right.Commodities))
                if (x.Buyer != y.Buyer || x.Commodity != y.Commodity ||
                    x.PhysicalDemandRaw != y.PhysicalDemandRaw ||
                    x.PhysicalLocalProductionRaw != y.PhysicalLocalProductionRaw ||
                    x.MaximumHypotheticalLocalUseRaw !=
                        y.MaximumHypotheticalLocalUseRaw ||
                    x.MaximumLocalCoverage.CompareTo(y.MaximumLocalCoverage) != 0 ||
                    x.BaselinePlannedFreightRaw != y.BaselinePlannedFreightRaw ||
                    x.RegionalPlannedFreightRaw != y.RegionalPlannedFreightRaw)
                    return false;
        }
        return true;
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _access.VerifyIndependent();
        if (!SameSnapshot(Snapshot, Rebuild()))
            throw new InvalidOperationException(
                "Local production opportunity failed independent physical replay.");
        RequireCurrent(_session);
    }

    internal static CanonicalPrimitiveLocalSupplyPotentialDiagnostics FromAccess(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveRegionalAccessDistribution access)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(access);
        access.VerifyIndependent();
        sites.RequireCurrent(session);
        var provisional = new CanonicalPrimitiveLocalSupplyPotentialDiagnostics(
            session, sites, access, null!);
        var snapshot = provisional.Rebuild();
        var result = new CanonicalPrimitiveLocalSupplyPotentialDiagnostics(
            session, sites, access, snapshot);
        result.VerifyIndependent();
        return result;
    }
}
