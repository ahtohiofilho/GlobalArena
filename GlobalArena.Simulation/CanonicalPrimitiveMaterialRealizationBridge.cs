using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A VS82 local amount is only offered in an experimental allocation.
/// "Locally absorbed" in VS82 is NOT an executed purchase or receipt.
/// </summary>
public sealed record CanonicalPrimitiveMaterialTrialSource(
    EconomicPointId Source,
    CommodityId Commodity,
    EconomicActivityKind Activity,
    BigInteger PotentialRaw,
    BigInteger HypotheticallyProducedRaw,
    BigInteger PotentialNotExtractedRaw,
    BigInteger DeliberatelyRetainedRaw,
    BigInteger HypotheticallyRemotelyShippedRaw,
    BigInteger HypotheticallyOfferedLocallyRaw)
{
    public bool HasCanonicalExecutedMaterialReceipt => false;
}

public sealed record CanonicalPrimitiveMaterialTrialCommodity(
    CommodityId Commodity,
    EconomicActivityKind Activity,
    int PotentialSourceCount,
    int ExplicitlyRealizedSourceCount,
    BigInteger PotentialRaw,
    BigInteger HypotheticallyProducedRaw,
    BigInteger PotentialNotExtractedRaw,
    BigInteger DeliberatelyRetainedRaw,
    BigInteger HypotheticallyRemotelyShippedRaw,
    BigInteger HypotheticallyOfferedLocallyRaw,
    BigInteger ProjectedDemandPressureRaw,
    BigInteger? ActuallyPurchasedRaw,
    BigInteger? ActuallyPaidMicrocredits,
    string? ExecutedNominalUnitId)
{
    public bool IsActualMaterialMarketEstablished => false;
}

internal sealed record CanonicalPrimitiveMaterialTrialReconstruction(
    CanonicalPrimitiveMaterialTrialSource[] Sources,
    CanonicalPrimitiveMaterialTrialCommodity[] Commodities,
    string WorldHashHex);

/// <summary>
/// VS94: reuse VS82's independently verified physical realization and
/// authenticated trial freight, while retaining VS93's strict separation
/// between potential/experimental allocations and REAL paid receipts.
/// Material availability is an observation in a TRIAL, never persisted stock.
/// </summary>
public sealed class CanonicalPrimitiveMaterialRealizationBridge
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveCommodityExecutionBoundary _evidence93;
    private readonly CanonicalRealizedProductionAllocation _trial82;
    private readonly CanonicalPrimitiveMaterialTrialSource[] _sources;
    private readonly CanonicalPrimitiveMaterialTrialCommodity[] _commodities;
    private readonly string _worldHashHex;

    public IReadOnlyList<CanonicalPrimitiveMaterialTrialSource> Sources { get; }
    public IReadOnlyList<CanonicalPrimitiveMaterialTrialCommodity> Commodities { get; }
    public bool IsOnlyHypotheticalPhysicalRealization => true;
    public bool PreservesCanonicalFoodCashReceipts => true;
    public bool ExposesRealWoodOrStoneSales => false;
    public bool InventsWoodOrStoneMonetaryPrices => false;
    public bool TreatsNotExtractedPotentialAsStock => false;
    public bool ConvertsLocalTrialOffersIntoRealSales => false;
    public bool CapsPhysicalDemandOrSalesByProjection => false;
    public bool ModifiesWorldSaveWorkersOrStock => false;
    public bool AuthorizesSettlementOrCredit => false;

    private CanonicalPrimitiveMaterialRealizationBridge(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveCommodityExecutionBoundary evidence93,
        CanonicalRealizedProductionAllocation trial82,
        CanonicalPrimitiveMaterialTrialReconstruction built)
    {
        _session = session;
        _evidence93 = evidence93;
        _trial82 = trial82;
        _worldHashHex = built.WorldHashHex;
        _sources = built.Sources.ToArray();
        _commodities = built.Commodities.ToArray();
        Sources = Array.AsReadOnly(_sources);
        Commodities = Array.AsReadOnly(_commodities);
    }

    private static CanonicalPrimitiveMaterialTrialReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveCommodityExecutionBoundary evidence93,
        CanonicalRealizedProductionAllocation trial82)
    {
        evidence93.RequireCurrent(session);
        evidence93.VerifyIndependent();
        trial82.RequireCurrent(session);
        trial82.VerifyIndependent();

        // Two independent discoveries with equal-looking amounts cannot
        // silently substitute a different mapping, network or price policy.
        if (!ReferenceEquals(evidence93.SourceSites, trial82.SourceSites))
            throw new InvalidOperationException(
                "The VS82 material trial and VS93 real-market evidence must share the exact same certified workforce discovery.");

        var sites = evidence93.SourceSites;
        sites.RequireCurrent(session);
        var kinds = evidence93.Commodities.Where(x =>
            x.Activity == EconomicActivityKind.Forestry ||
            x.Activity == EconomicActivityKind.Mining)
            .ToArray();
        if (kinds.Length != 2 ||
            kinds.Select(x => x.Commodity).Distinct().Count() != 2 ||
            kinds.Any(x => !x.IsExecutedCashEvidenceUnavailable))
            throw new InvalidOperationException(
                "VS94 requires separate wood and stone projected markets with NO fabricated real paid receipts.");

        var byKey = sites.Flows.SupplyFlows.Where(x =>
            x.Activity == EconomicActivityKind.Forestry ||
            x.Activity == EconomicActivityKind.Mining)
            .ToDictionary(x =>
                (x.EconomicPointId, x.CommodityId, x.Activity),
                x => x.RawQuantity);
        if (byKey.Count != sites.Flows.SupplyFlows.Count(x =>
            x.Activity == EconomicActivityKind.Forestry ||
            x.Activity == EconomicActivityKind.Mining))
            throw new InvalidOperationException(
                "Duplicate material source capacity cannot be allocated twice.");

        var declared = trial82.Sources.ToDictionary(x =>
            (x.Source, x.Commodity, x.Activity));
        if (declared.Count == 0 || declared.Count != trial82.Sources.Count ||
            declared.Keys.Any(x =>
                !kinds.Any(k => k.Commodity == x.Commodity &&
                    k.Activity == x.Activity) ||
                !byKey.ContainsKey(x)) ||
            trial82.Declarations.Count != declared.Count ||
            trial82.Declarations.Any(x =>
                !declared.ContainsKey((x.Source, x.Commodity, x.Activity))))
            throw new InvalidOperationException(
                "VS94 requires 1..12 explicit VS82 realizations for real wood/stone capacity only; food cash must remain separate.");

        var perSource = new List<CanonicalPrimitiveMaterialTrialSource>();
        foreach (var (key, cap) in byKey.OrderBy(x => x.Key.Item1.Value)
            .ThenBy(x => x.Key.Item2.Value).ThenBy(x => x.Key.Item3))
        {
            declared.TryGetValue(key, out var allocation);
            var produced = allocation is null ? BigInteger.Zero :
                allocation.HypotheticallyProducedRaw;
            var notExtracted = allocation is null ? new BigInteger(cap) :
                allocation.PotentialNotExtractedRaw;
            var retained = allocation is null ? BigInteger.Zero :
                allocation.DeliberatelyRetainedRaw;
            var shipped = allocation?.HypotheticallyRemotelyShippedRaw ??
                BigInteger.Zero;
            var offered = allocation?.OfferedAndLocallyAbsorbedRaw ??
                BigInteger.Zero;

            if (produced < 0 || notExtracted < 0 || retained < 0 ||
                shipped < 0 || offered < 0 ||
                (BigInteger)cap != produced + notExtracted ||
                produced != retained + shipped + offered ||
                (allocation is not null &&
                    allocation.PotentialRaw != cap))
                throw new InvalidOperationException(
                    "Physical material conservation violated: potential = unextracted + produced; produced = retained + hypothetical shipped + hypothetical local offer.");
            perSource.Add(new CanonicalPrimitiveMaterialTrialSource(
                key.Item1, key.Item2, key.Item3, cap, produced, notExtracted,
                retained, shipped, offered));
        }

        var aggregates = new List<CanonicalPrimitiveMaterialTrialCommodity>();
        foreach (var kind in kinds.OrderBy(x => x.Commodity.Value))
        {
            var rows = perSource.Where(x =>
                x.Commodity == kind.Commodity && x.Activity == kind.Activity)
                .ToArray();
            var potential = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.PotentialRaw);
            var produced = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.HypotheticallyProducedRaw);
            var unextracted = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.PotentialNotExtractedRaw);
            var retained = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.DeliberatelyRetainedRaw);
            var shipped = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.HypotheticallyRemotelyShippedRaw);
            var offered = rows.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.HypotheticallyOfferedLocallyRaw);
            if (potential != kind.ProjectedPrimitivePotentialRaw ||
                potential != produced + unextracted ||
                produced != retained + shipped + offered)
                throw new InvalidOperationException(
                    "Material trial does not reconcile with the certified VS93 primitive capacity.");
            aggregates.Add(new CanonicalPrimitiveMaterialTrialCommodity(
                kind.Commodity, kind.Activity, rows.Length,
                rows.Count(x => x.HypotheticallyProducedRaw > 0), potential,
                produced, unextracted, retained, shipped, offered,
                kind.ProjectedResidentDemandPressureRaw,
                null, null, null));
        }
        evidence93.RequireCurrent(session);
        trial82.RequireCurrent(session);
        return new CanonicalPrimitiveMaterialTrialReconstruction(
            perSource.ToArray(), aggregates.ToArray(),
            session.CurrentHash.HexDigest);
    }

    public static CanonicalPrimitiveMaterialRealizationBridge FromCertifiedTrial(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveCommodityExecutionBoundary evidence93,
        CanonicalRealizedProductionAllocation hypotheticalTrial82)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(evidence93);
        ArgumentNullException.ThrowIfNull(hypotheticalTrial82);
        var rebuilt = Reconstruct(session, evidence93, hypotheticalTrial82);
        var report = new CanonicalPrimitiveMaterialRealizationBridge(
            session, evidence93, hypotheticalTrial82, rebuilt);
        report.VerifyIndependent();
        return report;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session) ||
            _worldHashHex != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "Physical material trial evidence is stale or belongs to another session.");
        _evidence93.RequireCurrent(session);
        _trial82.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var rebuilt = Reconstruct(_session, _evidence93, _trial82);
        if (!_sources.SequenceEqual(rebuilt.Sources) ||
            !_commodities.SequenceEqual(rebuilt.Commodities) ||
            _worldHashHex != rebuilt.WorldHashHex)
            throw new InvalidOperationException(
                "Independent VS94 material trial conservation diverged from VS82 and certified VS93 physical provenance.");
        RequireCurrent(_session);
    }
}
