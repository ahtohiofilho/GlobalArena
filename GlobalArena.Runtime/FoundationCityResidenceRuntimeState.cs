using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Stable city identity. A city is not its tile or a legacy economic point.
/// </summary>
public readonly record struct FoundationCityId
{
    private readonly ulong _value;
    public bool IsValid => _value != 0UL;
    public ulong Value => IsValid ? _value :
        throw new InvalidOperationException("Uninitialized city identity.");
    public FoundationCityId(ulong value)
    {
        if (value == 0UL) throw new ArgumentOutOfRangeException(nameof(value));
        _value = value;
    }
}

public sealed record FoundationCityRuntimeRecord(
    FoundationCityId Id, CivilizationId Owner, PhysicalTacticalTileId Tile);

/// <summary>
/// A stable HOME city, not an assertion that the individual is physically
/// at the city. Physical position continues to live in the worker census.
/// </summary>
public sealed record FoundationWorkerResidenceRuntimeRecord(
    PhysicalWorkerId Worker, FoundationCityId HomeCity);

/// <summary>
/// Optional v16 canonical residence ledger. No implicit cities derived from
/// map tiles and no separate numeric city population that may drift out of
/// sync with the worker census. Population is the count of HOME assignments.
/// </summary>
public sealed class FoundationCityResidenceRuntimeState
{
    public static FoundationCityResidenceRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<FoundationCityRuntimeRecord>(),
            Array.Empty<FoundationWorkerResidenceRuntimeRecord>());

    public bool IsInitialized { get; }
    public IReadOnlyList<FoundationCityRuntimeRecord> Cities { get; }
    public IReadOnlyList<FoundationWorkerResidenceRuntimeRecord> Residences { get; }

    public FoundationCityResidenceRuntimeState(
        IEnumerable<FoundationCityRuntimeRecord> cities,
        IEnumerable<FoundationWorkerResidenceRuntimeRecord> residences)
        : this(true,
            (cities ?? throw new ArgumentNullException(nameof(cities))).ToArray(),
            (residences ?? throw new ArgumentNullException(nameof(residences))).ToArray())
    {
    }

    private FoundationCityResidenceRuntimeState(bool initialized,
        FoundationCityRuntimeRecord[] cities,
        FoundationWorkerResidenceRuntimeRecord[] residences)
    {
        if (!initialized && (cities.Length != 0 || residences.Length != 0))
            throw new ArgumentException("Uninitialized residence ledger must be empty.");
        if (cities.Any(c => c is null || !c.Id.IsValid || !c.Owner.IsValid ||
                !c.Tile.IsValid) ||
            residences.Any(r => r is null || !r.Worker.IsValid ||
                !r.HomeCity.IsValid))
            throw new ArgumentException("City and residence records must have valid identities.");
        var orderedCities = cities.OrderBy(c => c.Id.Value).ToArray();
        var orderedResidents = residences.OrderBy(r => r.Worker.Value).ToArray();
        if (orderedCities.Select(c => c.Id).Distinct().Count() != orderedCities.Length ||
            orderedCities.Select(c => (c.Owner, c.Tile)).Distinct().Count() !=
                orderedCities.Length ||
            orderedResidents.Select(r => r.Worker).Distinct().Count() !=
                orderedResidents.Length ||
            orderedResidents.Any(r => !orderedCities.Any(c => c.Id == r.HomeCity)))
            throw new ArgumentException(
                "Canonical city IDs, owner+tile keys, worker residences and references must be unique and complete.");
        if (initialized && orderedCities.Length == 0)
            throw new ArgumentException("Initialized settlement ledger requires one or more cities.");

        IsInitialized = initialized;
        Cities = Array.AsReadOnly(orderedCities);
        Residences = Array.AsReadOnly(orderedResidents);
    }

    public IReadOnlyList<(FoundationCityRuntimeRecord City, ulong Population)>
        PopulationByResidence()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("No authoritative cities were initialized.");
        var counts = Residences.GroupBy(x => x.HomeCity)
            .ToDictionary(g => g.Key, g => checked((ulong)g.LongCount()));
        return Array.AsReadOnly(Cities.Select(c =>
            (c, counts.GetValueOrDefault(c.Id))).ToArray());
    }
}
