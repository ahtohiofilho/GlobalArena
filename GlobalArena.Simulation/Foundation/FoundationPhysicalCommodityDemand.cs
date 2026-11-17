using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Productive occupation drives the consumer's profile only while producing.
/// Travel, military and other work have distinct profiles independent of the
/// worker's dormant occupation. A person is NEVER counted in two profiles.
/// </summary>
public enum FoundationPhysicalDemandProfile : uint
{
    AgricultureProduction = 1,
    ForestryProduction = 2,
    MiningProduction = 3,
    Traveling = 4,
    Military = 5,
    Other = 6
}

/// <summary>
/// An explicitly supplied, strictly positive demand intensity for one
/// activity/commodity. Unit: demand micro-units per 1.0 worker multiplier.
/// This is NOT an adopted gameplay value or a money/price parameter.
/// </summary>
public sealed record FoundationActivityCommodityDemandCoefficient(
    FoundationPhysicalDemandProfile Profile, CommodityId Commodity,
    ulong IntensityMicroPerWorker);

/// <summary>
/// Complete externally-authored activity-by-commodity policy. No canned
/// weights, universal worker basket, fixed demand 40/70, inferred commodity
/// catalogue or automatic policy is constructed by the engine.
/// </summary>
public sealed class FoundationActivityCommodityDemandPolicy
{
    private static readonly FoundationPhysicalDemandProfile[] Profiles =
        Enum.GetValues<FoundationPhysicalDemandProfile>();

    private readonly Dictionary<
        (FoundationPhysicalDemandProfile Profile, CommodityId Commodity),
        ulong> _coefficients;
    public IReadOnlyList<CommodityId> Commodities { get; }

    public FoundationActivityCommodityDemandPolicy(
        IEnumerable<CommodityId> commodities,
        IEnumerable<FoundationActivityCommodityDemandCoefficient> coefficients)
    {
        ArgumentNullException.ThrowIfNull(commodities);
        ArgumentNullException.ThrowIfNull(coefficients);

        var ordered = commodities.ToArray();
        if (ordered.Length == 0 ||
            ordered.Any(item => !item.IsValid) ||
            ordered.Distinct().Count() != ordered.Length)
            throw new ArgumentException(
                "An explicit, nonempty, duplicate-free commodity catalogue is required.",
                nameof(commodities));
        Array.Sort(ordered, (a, b) => a.Value.CompareTo(b.Value));
        Commodities = Array.AsReadOnly(ordered);
        var known = ordered.ToHashSet();
        _coefficients = new Dictionary<
            (FoundationPhysicalDemandProfile, CommodityId), ulong>();
        foreach (var rule in coefficients)
        {
            if (rule is null || !Enum.IsDefined(rule.Profile) ||
                !rule.Commodity.IsValid || !known.Contains(rule.Commodity) ||
                rule.IntensityMicroPerWorker == 0UL)
                throw new ArgumentException(
                    "Every rule needs a known commodity, real activity and positive intensity.",
                    nameof(coefficients));
            if (!_coefficients.TryAdd(
                (rule.Profile, rule.Commodity), rule.IntensityMicroPerWorker))
                throw new ArgumentException(
                    "Duplicate activity/commodity policy entry.", nameof(coefficients));
        }
        if (_coefficients.Count != checked(Profiles.Length * ordered.Length) ||
            Profiles.Any(profile =>
                ordered.Any(item => !_coefficients.ContainsKey((profile, item)))))
            throw new ArgumentException(
                "Demand policy must cover every activity for every commodity, " +
                "with no implicit zero or fabricated default.", nameof(coefficients));
    }

    public ulong IntensityMicroPerWorker(
        FoundationPhysicalDemandProfile profile, CommodityId commodity) =>
        _coefficients.TryGetValue((profile, commodity), out var intensity)
            ? intensity
            : throw new InvalidOperationException(
                "No positive activity/commodity demand intensity was supplied.");
}

/// <summary>
/// Identifiable demand contributors at a physical location, retaining their
/// owner for accounting. WorkerCount is repeated per commodity because every
/// real worker demands each declared item. It is not a distinct-worker total
/// across commodities.
/// </summary>
public sealed record FoundationPhysicalCommodityDemandLine(
    PhysicalTacticalTileId Tile, CivilizationId Owner, CommodityId Commodity,
    int WorkerCount, ulong DemandMicro);

/// <summary>
/// Physical market demand is the sum across owners at the SAME fine tile.
/// It is not a market price, shipment, buyer wallet or delivered quantity.
/// </summary>
public sealed record FoundationPhysicalCommodityMarketDemand(
    PhysicalTacticalTileId Tile, CommodityId Commodity,
    int WorkerCount, ulong DemandMicro);

/// <summary>
/// Immutable read model. Supply is the exact existing physical capacity lots,
/// not actual sales, inventories or role-derived product substitutions.
/// </summary>
public sealed class FoundationPhysicalCommodityMarketInputs
{
    public IReadOnlyList<FoundationPhysicalCommodityDemandLine> DemandByCivilization { get; }
    public IReadOnlyList<FoundationPhysicalCommodityMarketDemand> MarketDemand { get; }
    public IReadOnlyList<FoundationPhysicalSupplyLot> SupplyLots { get; }
    public IReadOnlyList<CommodityId> Commodities { get; }
    public int DistinctWorkers { get; }
    public ulong TotalCommodityDemandMicro { get; }
    public ulong TotalActiveCapacityMicroPerTurn { get; }

    internal FoundationPhysicalCommodityMarketInputs(
        FoundationPhysicalCommodityDemandLine[] byCivilization,
        FoundationPhysicalCommodityMarketDemand[] markets,
        IReadOnlyList<FoundationPhysicalSupplyLot> supply,
        IReadOnlyList<CommodityId> commodities,
        int distinctWorkers, ulong demand, ulong capacity)
    {
        DemandByCivilization = Array.AsReadOnly(byCivilization);
        MarketDemand = Array.AsReadOnly(markets);
        SupplyLots = Array.AsReadOnly(supply.ToArray());
        Commodities = Array.AsReadOnly(commodities.ToArray());
        DistinctWorkers = distinctWorkers;
        TotalCommodityDemandMicro = demand;
        TotalActiveCapacityMicroPerTurn = capacity;
    }
}

/// <summary>
/// Expand authoritative worker demand into commodity-specific markets under
/// an EXPLICIT policy, while consuming the previously validated physical
/// projection from PR #383. Stateless: no journal, host or price mutation.
/// </summary>
public static class FoundationPhysicalCommodityDemandProjector
{
    private sealed class DemandBucket
    {
        public int WorkerCount;
        public ulong DemandMicro;
    }

    public static FoundationPhysicalCommodityMarketInputs Project(
        PhysicalWorkerRuntimeState census,
        FoundationActivityCommodityDemandPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var physical = FoundationPhysicalWorkforceEconomicProjector.Project(census);
        var byOwner = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner,
             CommodityId Commodity), DemandBucket>();
        var byMarket = new Dictionary<
            (PhysicalTacticalTileId Tile, CommodityId Commodity), DemandBucket>();
        ulong total = 0UL;
        foreach (var worker in census.Workers)
        {
            var tile = worker.TacticalTileId!.Value;
            var profile = SelectProfile(worker);
            foreach (var commodity in policy.Commodities)
            {
                var intensity = policy.IntensityMicroPerWorker(profile, commodity);
                // UInt128 avoids intermediate overflow while preserving the
                // exact fixed-point quotient. No floating point or floor
                // artificially added to a zero-demand market.
                UInt128 product = (UInt128)worker.DemandMultiplierMicro * intensity;
                UInt128 scaled = product / 1_000_000U;
                if (scaled == 0U)
                    throw new InvalidOperationException(
                        "Positive activity demand is below one representable micro-unit. " +
                        "Supply a viable policy; no hidden demand floor is applied.");
                if (scaled > ulong.MaxValue)
                    throw new OverflowException("Commodity demand exceeds ulong micro-units.");
                ulong amount = checked((ulong)scaled);
                total = checked(total + amount);

                var ownerKey = (tile, worker.Owner, commodity);
                if (!byOwner.TryGetValue(ownerKey, out var ownerBucket))
                    byOwner.Add(ownerKey, ownerBucket = new DemandBucket());
                ownerBucket.WorkerCount = checked(ownerBucket.WorkerCount + 1);
                ownerBucket.DemandMicro = checked(ownerBucket.DemandMicro + amount);

                var marketKey = (tile, commodity);
                if (!byMarket.TryGetValue(marketKey, out var marketBucket))
                    byMarket.Add(marketKey, marketBucket = new DemandBucket());
                marketBucket.WorkerCount = checked(marketBucket.WorkerCount + 1);
                marketBucket.DemandMicro = checked(marketBucket.DemandMicro + amount);
            }
        }

        var demandLines = byOwner
            .OrderBy(pair => pair.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(pair => pair.Key.Owner.Value)
            .ThenBy(pair => pair.Key.Commodity.Value)
            .Select(pair => new FoundationPhysicalCommodityDemandLine(
                pair.Key.Tile, pair.Key.Owner, pair.Key.Commodity,
                pair.Value.WorkerCount, pair.Value.DemandMicro))
            .ToArray();
        var markets = byMarket
            .OrderBy(pair => pair.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(pair => pair.Key.Commodity.Value)
            .Select(pair => new FoundationPhysicalCommodityMarketDemand(
                pair.Key.Tile, pair.Key.Commodity, pair.Value.WorkerCount,
                pair.Value.DemandMicro))
            .ToArray();

        // Every real worker must demand EACH explicitly declared commodity at
        // one and only one physical tile. Owner lines and owner-agnostic markets
        // reconcile down to the same demand, without phantom buyers.
        int commodityCount = policy.Commodities.Count;
        if (demandLines.Length != checked(physical.DemandSites.Count * commodityCount) ||
            demandLines.Sum(line => (long)line.WorkerCount) !=
                (long)census.Workers.Count * commodityCount ||
            markets.Sum(market => (long)market.WorkerCount) !=
                (long)census.Workers.Count * commodityCount ||
            demandLines.Aggregate(0UL, (sum, line) =>
                checked(sum + line.DemandMicro)) != total ||
            markets.Aggregate(0UL, (sum, market) =>
                checked(sum + market.DemandMicro)) != total ||
            physical.DemandSites.Any(site =>
                policy.Commodities.Any(commodity => !byOwner.TryGetValue(
                    (site.Tile, site.Owner, commodity), out var bucket) ||
                    bucket.WorkerCount != site.WorkerCount)))
            throw new InvalidOperationException(
                "Commodity projection duplicated or lost a physical worker's demand.");

        return new FoundationPhysicalCommodityMarketInputs(
            demandLines, markets, physical.SupplyLots, policy.Commodities,
            physical.WorkerCount, total, physical.TotalActiveCapacityMicroPerTurn);
    }

    public static FoundationPhysicalDemandProfile SelectProfile(
        PhysicalWorkerRuntimeRecord worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        return worker.Activity switch
        {
            PhysicalWorkerActivity.Producing => worker.Occupation switch
            {
                PhysicalWorkerOccupation.Agriculture =>
                    FoundationPhysicalDemandProfile.AgricultureProduction,
                PhysicalWorkerOccupation.Forestry =>
                    FoundationPhysicalDemandProfile.ForestryProduction,
                PhysicalWorkerOccupation.Mining =>
                    FoundationPhysicalDemandProfile.MiningProduction,
                _ => throw new InvalidOperationException(
                    "A producing worker requires a real productive occupation.")
            },
            PhysicalWorkerActivity.Traveling =>
                FoundationPhysicalDemandProfile.Traveling,
            PhysicalWorkerActivity.Military =>
                FoundationPhysicalDemandProfile.Military,
            PhysicalWorkerActivity.Other =>
                FoundationPhysicalDemandProfile.Other,
            _ => throw new InvalidOperationException(
                "Unknown authoritative worker demand activity.")
        };
    }
}
