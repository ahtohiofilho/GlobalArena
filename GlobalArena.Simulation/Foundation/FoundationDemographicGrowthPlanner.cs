using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// An explicitly identified inhabited city and its current WHOLE-worker
/// population. City identity is not a strategic cell ID, a tile ID or a
/// legacy economic point. Until canonical city persistence is introduced,
/// the caller must supply verified city membership and population.
/// </summary>
public sealed record FoundationDemographicCityPopulation(
    ulong CityId,
    CivilizationId Owner,
    PhysicalTacticalTileId Tile,
    ulong Population);

public sealed record FoundationDemographicBirth(
    ulong Sequence,
    ulong CityId,
    CivilizationId Owner,
    PhysicalTacticalTileId Tile);

public sealed record FoundationDemographicCityAllocation(
    ulong CityId,
    CivilizationId Owner,
    PhysicalTacticalTileId Tile,
    ulong Before,
    ulong Added,
    ulong After);

public sealed class FoundationDemographicGrowthPlan
{
    public string BaselineIdentity { get; }
    public ulong Turn { get; }
    public int SurvivingCivilizations { get; }
    public IReadOnlyList<FoundationDemographicBirth> Births { get; }
    public IReadOnlyList<FoundationDemographicCityAllocation> Cities { get; }
    public bool IsCommitted => false;

    internal FoundationDemographicGrowthPlan(
        string baselineIdentity, ulong turn, int survivors,
        FoundationDemographicBirth[] births,
        FoundationDemographicCityAllocation[] cities)
    {
        BaselineIdentity = baselineIdentity;
        Turn = turn;
        SurvivingCivilizations = survivors;
        Births = Array.AsReadOnly(births);
        Cities = Array.AsReadOnly(cities);
    }
}

/// <summary>
/// R162: exact, deterministic WORLDWIDE growth. Each turn creates as many
/// birth slots as there are surviving civilizations; every slot is sampled
/// independently from all existing cities, with dynamic weights equal to
/// city population AFTER prior births within the same turn.
///
/// This is a read-only demographic allocation, NOT a worker insertion, job
/// assignment, city foundation or settlement event.
/// </summary>
public static class FoundationDemographicGrowthPlanner
{
    public static FoundationDemographicGrowthPlan Plan(
        WorldState baseline,
        IReadOnlyList<FoundationDemographicCityPopulation> cities,
        IReadOnlyList<CivilizationId> survivingCivilizations,
        ulong turn)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(survivingCivilizations);
        if (baseline.WorldBinding is null ||
            !baseline.Workers.HasAuthoritativeTacticalLocations ||
            !baseline.Workers.HasAuthoritativeOccupations ||
            !baseline.Workers.IsInitialized || turn == 0UL)
            throw new ArgumentException(
                "Demographic projection requires a bound physical workforce and a positive turn.");
        var roster = baseline.Civilizations.Civilizations.ToHashSet();
        var survivors = survivingCivilizations.OrderBy(x => x.Value).ToArray();
        if (survivors.Length == 0 ||
            survivors.Any(x => !x.IsValid || !roster.Contains(x)) ||
            survivors.Distinct().Count() != survivors.Length)
            throw new ArgumentException(
                "Survivors must be a nonempty, unique subset of real civilizations.",
                nameof(survivingCivilizations));

        var alive = survivors.ToHashSet();
        var sorted = cities.OrderBy(c => c?.CityId ?? 0UL).ToArray();
        if (sorted.Length == 0 ||
            sorted.Any(c => c is null || c.CityId == 0UL ||
                !c.Owner.IsValid || !alive.Contains(c.Owner) ||
                !c.Tile.IsValid || c.Population == 0UL) ||
            sorted.Select(c => c.CityId).Distinct().Count() != sorted.Length ||
            sorted.Select(c => (c.Owner, c.Tile)).Distinct().Count() != sorted.Length)
            throw new ArgumentException(
                "Cities must have unique identities, valid surviving owners and positive population.",
                nameof(cities));

        var fine = baseline.Workers.Workers.FirstOrDefault()?.TacticalTileId
            ?.FineGoldbergParameters;
        if (fine is null ||
            sorted.Any(c => c.Tile.FineGoldbergParameters != fine.Value))
            throw new ArgumentException(
                "Every demographic city must be on this exact physical tactical topology.",
                nameof(cities));

        // R162 does not infer a city from workers standing nearby: a
        // settlement has its own explicit identity. The supplied counts
        // must cover the entire canonical worker census at CITY sites.
        // Actual roaming/visiting populations need a residence ledger in a
        // later phase; do not silently conflate location and residence.
        var counts = baseline.Workers.Workers
            .GroupBy(w => (Owner: w.Owner, Tile: w.TacticalTileId!.Value))
            .ToDictionary(g => g.Key, g => checked((ulong)g.LongCount()));
        if (sorted.Any(c => !counts.TryGetValue((c.Owner, c.Tile),
                out ulong n) || n != c.Population) ||
            counts.Count != sorted.Length)
            throw new ArgumentException(
                "Provisional city populations must reconcile exactly to real co-located workers; " +
                "residence for roaming workers is not yet modeled.",
                nameof(cities));

        string identity = new CanonicalWorldStateHasher().Compute(baseline)
            .ToString() ?? throw new InvalidOperationException("Missing canonical hash.");
        ulong[] populations = sorted.Select(c => c.Population).ToArray();
        ulong[] added = new ulong[sorted.Length];
        ulong total = 0UL;
        foreach (ulong p in populations)
            total = checked(total + p);
        var births = new List<FoundationDemographicBirth>(survivors.Length);
        for (int draw = 0; draw < survivors.Length; draw++)
        {
            ulong selected = SampleBounded(identity, turn, (ulong)draw, total);
            int index = 0;
            while (selected >= populations[index])
            {
                selected -= populations[index];
                index++;
            }
            var city = sorted[index];
            births.Add(new FoundationDemographicBirth(
                checked((ulong)draw + 1UL), city.CityId, city.Owner, city.Tile));
            populations[index] = checked(populations[index] + 1UL);
            added[index] = checked(added[index] + 1UL);
            total = checked(total + 1UL);
        }
        var allocations = sorted.Select((city, i) =>
            new FoundationDemographicCityAllocation(
                city.CityId, city.Owner, city.Tile,
                city.Population, added[i], populations[i])).ToArray();
        if (allocations.Aggregate(0UL, (sum, c) => checked(sum + c.Added)) !=
                checked((ulong)survivors.Length) ||
            births.Count != survivors.Length)
            throw new InvalidOperationException("Demographic growth must conserve the exact birth budget.");
        return new FoundationDemographicGrowthPlan(identity, turn,
            survivors.Length, births.ToArray(), allocations);
    }

    // SHA-256 counter stream with rejection sampling: no modulo bias,
    // language/runtime RNG dependence, floating point or mutable RNG state.
    private static ulong SampleBounded(
        string baselineHash, ulong turn, ulong draw, ulong exclusiveUpperBound)
    {
        if (exclusiveUpperBound == 0UL)
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
        ulong threshold = unchecked(0UL - exclusiveUpperBound) %
            exclusiveUpperBound;
        for (ulong retry = 0UL; ; retry = checked(retry + 1UL))
        {
            byte[] prefix = Encoding.UTF8.GetBytes(
                "GA-NEXT-R162-DEMOGRAPHY-V1|" + baselineHash);
            byte[] input = new byte[checked(prefix.Length + 24)];
            prefix.CopyTo(input, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(
                input.AsSpan(prefix.Length, 8), turn);
            BinaryPrimitives.WriteUInt64LittleEndian(
                input.AsSpan(prefix.Length + 8, 8), draw);
            BinaryPrimitives.WriteUInt64LittleEndian(
                input.AsSpan(prefix.Length + 16, 8), retry);
            ulong sample = BinaryPrimitives.ReadUInt64LittleEndian(
                SHA256.HashData(input).AsSpan(0, 8));
            if (sample >= threshold)
                return sample % exclusiveUpperBound;
        }
    }
}
