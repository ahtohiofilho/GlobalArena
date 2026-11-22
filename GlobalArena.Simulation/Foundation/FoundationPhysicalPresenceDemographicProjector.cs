using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A worker spends this fraction of ONE turn associated with this physical
/// node. During edge travel, time is attributed to the departure node until
/// arrival. Productive effort is only the usable remainder after travel.
/// The slices are INPUT evidence from a future authoritative time/movement
/// integrator; this projector never fabricates a path or travel duration.
/// </summary>
public sealed record FoundationWorkerTemporalNodeSlice(
    PhysicalWorkerId Worker,
    PhysicalTacticalTileId Node,
    uint PresenceMicro,
    uint ProductiveEffortMicro,
    PhysicalWorkerActivity DemandActivity);

public sealed record FoundationPhysicalPresenceBucket(
    CivilizationId Owner,
    PhysicalTacticalTileId Node,
    ulong PresenceMicro,
    ulong ProductiveEffortMicro);

public sealed record FoundationPhysicalPresenceDemand(
    CivilizationId Owner,
    PhysicalTacticalTileId Node,
    CommodityId Commodity,
    ulong DemandMicro);

public sealed class FoundationPhysicalTemporalPresenceReport
{
    public string BaselineIdentity { get; }
    public string ScheduleDigest { get; }
    public IReadOnlyList<FoundationPhysicalPresenceBucket> Buckets { get; }
    public IReadOnlyList<FoundationPhysicalPresenceDemand> Demand { get; }
    public ulong WorkerTurnMicro { get; }
    public bool IsCommitted => false;

    internal FoundationPhysicalTemporalPresenceReport(
        string baseline, string scheduleDigest,
        FoundationPhysicalPresenceBucket[] buckets,
        FoundationPhysicalPresenceDemand[] demand, ulong workerTurnMicro)
    {
        BaselineIdentity = baseline;
        ScheduleDigest = scheduleDigest;
        Buckets = Array.AsReadOnly(buckets);
        Demand = Array.AsReadOnly(demand);
        WorkerTurnMicro = workerTurnMicro;
    }
}

/// <summary>
/// The world has a fixed number of birth SLOTS equal to surviving
/// civilizations. A slot is drawn from PHYSICAL worker-time by
/// (civilization, node), including temporary construction sites, pass-through
/// locations and places without a formal city. This is not a home census.
/// </summary>
public sealed record FoundationPhysicalPresenceBirth(
    ulong Sequence,
    CivilizationId Owner,
    PhysicalTacticalTileId NatalNode,
    FoundationCityId? NatalCity);

public sealed class FoundationPhysicalPresenceGrowthPlan
{
    public string BaselineIdentity { get; }
    public string ScheduleDigest { get; }
    public ulong Turn { get; }
    public IReadOnlyList<FoundationPhysicalPresenceBirth> Births { get; }
    public bool IsCommitted => false;

    internal FoundationPhysicalPresenceGrowthPlan(
        string baseline, string digest, ulong turn,
        FoundationPhysicalPresenceBirth[] births)
    {
        BaselineIdentity = baseline;
        ScheduleDigest = digest;
        Turn = turn;
        Births = Array.AsReadOnly(births);
    }
}

public static class FoundationPhysicalPresenceDemographicProjector
{
    public const uint WholeTurnMicro = 1_000_000U;

    /// <summary>
    /// Crossing an edge may consume the first 40% of a turn. The remainder
    /// is usable productive effort at the destination on that SAME turn.
    /// Each slice's demand is located where the time is attributed.
    /// </summary>
    public static IReadOnlyList<FoundationWorkerTemporalNodeSlice>
        ArrivalWithinTurn(PhysicalWorkerId worker,
            PhysicalTacticalTileId departure, PhysicalTacticalTileId arrival,
            uint remainingTravelMicro)
    {
        if (!worker.IsValid || !departure.IsValid || !arrival.IsValid ||
            departure == arrival ||
            departure.FineGoldbergParameters != arrival.FineGoldbergParameters ||
            remainingTravelMicro == 0U ||
            remainingTravelMicro > WholeTurnMicro)
            throw new ArgumentException("Arrival requires a real adjacent-edge time fragment and compatible distinct physical nodes.");
        var segments = new List<FoundationWorkerTemporalNodeSlice>
        {
            new(worker, departure, remainingTravelMicro, 0U,
                PhysicalWorkerActivity.Traveling)
        };
        uint usable = WholeTurnMicro - remainingTravelMicro;
        if (usable > 0U)
            segments.Add(new FoundationWorkerTemporalNodeSlice(
                worker, arrival, usable, usable,
                PhysicalWorkerActivity.Producing));
        return Array.AsReadOnly(segments.ToArray());
    }

    public static FoundationPhysicalTemporalPresenceReport Project(
        WorldState baseline,
        IReadOnlyList<FoundationWorkerTemporalNodeSlice> slices,
        FoundationActivityCommodityDemandPolicy demandPolicy)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        if (baseline.WorldBinding is null ||
            !baseline.Workers.HasAuthoritativeTacticalLocations ||
            !baseline.Workers.HasAuthoritativeOccupations ||
            slices.Count == 0)
            throw new ArgumentException(
                "Physical temporal presence needs a real bound v11 workforce and nonempty worker-time evidence.");

        var workers = baseline.Workers.Workers.ToDictionary(w => w.Id);
        var fines = workers.Values.FirstOrDefault()?.TacticalTileId
            ?.FineGoldbergParameters;
        if (workers.Count == 0 || fines is null)
            throw new ArgumentException("No physical workforce available.");

        if (slices.Any(s => s is null))
            throw new ArgumentException("Worker-time evidence may not contain null segments.", nameof(slices));
        var ordered = slices.OrderBy(s => s.Worker.Value)
            .ThenBy(s => s.Node.FineStrategicCellId.Value)
            .ThenBy(s => (uint)s.DemandActivity)
            .ThenBy(s => s.PresenceMicro)
            .ThenBy(s => s.ProductiveEffortMicro).ToArray();

        var times = new Dictionary<PhysicalWorkerId, ulong>();
        var grouped = new Dictionary<
            (CivilizationId Owner, PhysicalTacticalTileId Node),
            (ulong Presence, ulong Effort)>();
        var demandNumerators = new Dictionary<
            (PhysicalWorkerId Worker, CivilizationId Owner,
             PhysicalTacticalTileId Node, CommodityId Item), UInt128>();

        // A canonical digest makes two different time histories with the same
        // worker positions distinguishable in a deterministic lottery.
        var evidence = new StringBuilder("GA-NEXT-R164-PRESENCE-V1|");
        foreach (var slice in ordered)
        {
            if (slice is null || !workers.TryGetValue(slice.Worker, out var person) ||
                !slice.Node.IsValid || slice.Node.FineGoldbergParameters != fines.Value ||
                slice.Node.FineStrategicCellId.Value > fines.Value.StrategicCellCount ||
                slice.PresenceMicro == 0 ||
                slice.PresenceMicro > WholeTurnMicro ||
                slice.ProductiveEffortMicro > slice.PresenceMicro ||
                !Enum.IsDefined(slice.DemandActivity) ||
                (slice.ProductiveEffortMicro > 0 &&
                    slice.DemandActivity != PhysicalWorkerActivity.Producing) ||
                (slice.DemandActivity == PhysicalWorkerActivity.Producing &&
                    person.Occupation is not (PhysicalWorkerOccupation.Agriculture or
                        PhysicalWorkerOccupation.Forestry or
                        PhysicalWorkerOccupation.Mining)))
                throw new ArgumentException(
                    "Temporal slices must identify valid people, physical nodes, time and effort.",
                    nameof(slices));

            times[slice.Worker] = checked(
                times.GetValueOrDefault(slice.Worker) + slice.PresenceMicro);
            var key = (person.Owner, slice.Node);
            var old = grouped.GetValueOrDefault(key);
            grouped[key] = (
                checked(old.Presence + slice.PresenceMicro),
                checked(old.Effort + slice.ProductiveEffortMicro));

            var profile = slice.DemandActivity switch
            {
                PhysicalWorkerActivity.Producing => person.Occupation switch
                {
                    PhysicalWorkerOccupation.Agriculture =>
                        FoundationPhysicalDemandProfile.AgricultureProduction,
                    PhysicalWorkerOccupation.Forestry =>
                        FoundationPhysicalDemandProfile.ForestryProduction,
                    PhysicalWorkerOccupation.Mining =>
                        FoundationPhysicalDemandProfile.MiningProduction,
                    _ => throw new ArgumentException("Invalid producer occupation.")
                },
                PhysicalWorkerActivity.Traveling =>
                    FoundationPhysicalDemandProfile.Traveling,
                PhysicalWorkerActivity.Military =>
                    FoundationPhysicalDemandProfile.Military,
                _ => FoundationPhysicalDemandProfile.Other
            };
            foreach (var item in demandPolicy.Commodities)
            {
                // Keep the full integer numerator across segments from
                // the same worker in one market, rounding only ONCE per
                // worker/node/commodity. No fractional fictitious buyer.
                UInt128 quantity = (UInt128)person.DemandMultiplierMicro *
                    demandPolicy.IntensityMicroPerWorker(profile, item) *
                    slice.PresenceMicro;
                var demandKey = (slice.Worker, person.Owner, slice.Node, item);
                demandNumerators[demandKey] = checked(
                    demandNumerators.GetValueOrDefault(demandKey) + quantity);
            }
            evidence.Append(slice.Worker.Value).Append(':')
                .Append(slice.Node.FineStrategicCellId.Value).Append(':')
                .Append(slice.PresenceMicro).Append(':')
                .Append(slice.ProductiveEffortMicro).Append(':')
                .Append((uint)slice.DemandActivity).Append('|');
        }
        if (times.Count != workers.Count ||
            times.Any(x => x.Value != WholeTurnMicro) ||
            workers.Keys.Any(x => !times.ContainsKey(x)))
            throw new ArgumentException(
                "Exactly one full turn of presence must be accounted for every existing worker.",
                nameof(slices));

        var buckets = grouped.OrderBy(x => x.Key.Owner.Value)
            .ThenBy(x => x.Key.Node.FineStrategicCellId.Value)
            .Select(x => new FoundationPhysicalPresenceBucket(
                x.Key.Owner, x.Key.Node, x.Value.Presence, x.Value.Effort))
            .ToArray();
        var demandRows = demandNumerators
            .Select(x => new
            {
                x.Key.Owner, x.Key.Node, x.Key.Item,
                Micro = checked((ulong)(x.Value /
                    ((UInt128)WholeTurnMicro * WholeTurnMicro)))
            })
            .GroupBy(x => (x.Owner, x.Node, x.Item))
            .OrderBy(x => x.Key.Owner.Value)
            .ThenBy(x => x.Key.Node.FineStrategicCellId.Value)
            .ThenBy(x => x.Key.Item.Value)
            .Select(g => new FoundationPhysicalPresenceDemand(
                g.Key.Owner, g.Key.Node, g.Key.Item,
                g.Aggregate(0UL, (sum, x) => checked(sum + x.Micro))))
            .ToArray();
        string baselineHash = new CanonicalWorldStateHasher().Compute(baseline)
            .ToString() ?? throw new InvalidOperationException("Missing world hash.");
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(evidence.ToString())));
        return new FoundationPhysicalTemporalPresenceReport(
            baselineHash, digest, buckets, demandRows,
            checked((ulong)workers.Count * WholeTurnMicro));
    }

    public static FoundationPhysicalPresenceGrowthPlan PlanBirths(
        WorldState baseline,
        FoundationPhysicalTemporalPresenceReport presence,
        IReadOnlyList<CivilizationId> surviving,
        ulong turn)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(surviving);
        if (turn == 0 || presence.IsCommitted ||
            presence.BaselineIdentity !=
                new CanonicalWorldStateHasher().Compute(baseline).ToString())
            throw new ArgumentException(
                "Births require time evidence from this exact unchanged canonical world.");
        var roster = baseline.Civilizations.Civilizations.ToHashSet();
        var owners = surviving.OrderBy(c => c.Value).ToArray();
        if (owners.Length == 0 ||
            owners.Any(x => !x.IsValid || !roster.Contains(x)) ||
            owners.Distinct().Count() != owners.Length)
            throw new ArgumentException(
                "Survivors must be distinct, valid, existing civilizations.");
        var alive = owners.ToHashSet();
        var eligible = presence.Buckets
            .Where(x => alive.Contains(x.Owner) && x.PresenceMicro > 0UL)
            .OrderBy(x => x.Owner.Value)
            .ThenBy(x => x.Node.FineStrategicCellId.Value).ToArray();
        if (eligible.Length == 0)
            throw new ArgumentException("No living worker physical presence for births.");

        ulong[] weights = eligible.Select(x => x.PresenceMicro).ToArray();
        ulong total = 0UL;
        foreach (var w in weights) total = checked(total + w);
        string key = presence.BaselineIdentity + "|" + presence.ScheduleDigest;
        var cities = baseline.CityResidences.IsInitialized
            ? baseline.CityResidences.Cities.ToDictionary(
                c => (c.Owner, c.Tile), c => c.Id)
            : new Dictionary<(CivilizationId, PhysicalTacticalTileId), FoundationCityId>();
        var results = new FoundationPhysicalPresenceBirth[owners.Length];
        for (int i = 0; i < results.Length; i++)
        {
            ulong n = Draw(key, turn, (ulong)i, total);
            int index = 0;
            while (n >= weights[index])
            {
                n -= weights[index];
                index++;
            }
            var target = eligible[index];
            results[i] = new FoundationPhysicalPresenceBirth(
                (ulong)i + 1UL, target.Owner, target.Node,
                cities.TryGetValue((target.Owner, target.Node), out var city)
                    ? city : null);
            weights[index] = checked(weights[index] + WholeTurnMicro);
            total = checked(total + WholeTurnMicro);
        }
        return new FoundationPhysicalPresenceGrowthPlan(
            presence.BaselineIdentity, presence.ScheduleDigest,
            turn, results);
    }

    private static ulong Draw(string source, ulong turn, ulong ordinal,
        ulong bound)
    {
        if (bound == 0UL) throw new ArgumentOutOfRangeException(nameof(bound));
        ulong threshold = unchecked(0UL - bound) % bound;
        for (ulong retry = 0; ; retry = checked(retry + 1UL))
        {
            byte[] text = Encoding.UTF8.GetBytes(
                "GA-NEXT-R164-BIRTH-V1|" + source);
            byte[] bytes = new byte[checked(text.Length + 24)];
            text.CopyTo(bytes, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(
                bytes.AsSpan(text.Length, 8), turn);
            BinaryPrimitives.WriteUInt64LittleEndian(
                bytes.AsSpan(text.Length + 8, 8), ordinal);
            BinaryPrimitives.WriteUInt64LittleEndian(
                bytes.AsSpan(text.Length + 16, 8), retry);
            var number = BinaryPrimitives.ReadUInt64LittleEndian(
                SHA256.HashData(bytes).AsSpan(0, 8));
            if (number >= threshold) return number % bound;
        }
    }
}
