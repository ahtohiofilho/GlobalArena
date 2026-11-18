using System.Collections;
using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Natural, unbuilt connectivity is a physical crossing, not an absent road.
/// An unowned constructed crossing differs from a natural crossing, and only
/// an explicitly owned crossing can collect an external seller toll.
/// </summary>
public enum PhysicalTransportOwnershipKind : uint
{
    NoInfrastructure = 2U,
    UnownedInfrastructure = 3U,
    Owned = 4U
}

/// <summary>
/// ONE immutable canonical fine crossing. Natural base transport cost and
/// reference cargo capacity are explicit, never inferred from a missing edge.
/// Cargo units and toll money are the same fixed-point units as the physical
/// turn-locked freight coordinator. No market price is stored here.
/// V13 optionally records explicit NATURAL construction labor per crossing;
/// v12 callers leave it null to preserve the exact former wire format.
/// </summary>
public sealed record PhysicalTransportEdgeRuntimeRecord(
    PhysicalTacticalEdgeId Edge,
    PhysicalTransportOwnershipKind OwnershipKind,
    CivilizationId? InfrastructureOwner,
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong CargoMicroAtReferenceLoad,
    ulong ExternalTariffMicroPerWholeCargoUnit,
    ulong? NaturalConstructionLaborMicro = null,
    ulong? CommittedConstructionLaborMicro = null);

/// <summary>
/// Complete and immutable, world-seeded physical-transport inventory.
/// Uninitialized is distinct from a traversable natural graph. A fully
/// initialized world MUST contain every canonical fine crossing exactly once;
/// WorldState verifies this against the authoritative incidence topology.
/// No infrastructure construction/upgrade/takeover is allowed through this
/// seed-only schema deployment API; those require future journaled turn events.
/// </summary>
public sealed class PhysicalTransportInfrastructureRuntimeState
{
    // A construction event changes one edge. Keep the physical records
    // immutable and share them across historical states/replay. Only one
    // 256-entry progress page and a small page-reference table are cloned.
    // This replaces the former O(all edges) array/record copy per worker.
    private const int ProgressPageSize = 256;

    private readonly PhysicalTransportEdgeRuntimeRecord[] _physicalEdges;
    private readonly ulong[]?[]? _constructionPages;

    public static PhysicalTransportInfrastructureRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<PhysicalTransportEdgeRuntimeRecord>());

    public bool IsInitialized { get; }

    /// <summary>Only a complete edge schema promotes a world to v13.</summary>
    public bool HasNaturalConstructionLabor { get; }

    /// <summary>
    /// The first ACCEPTED worker event opts into v14. The full logical
    /// ledger reads zero on untouched edges; no zero-filled E-record array.
    /// </summary>
    public bool HasCommittedConstructionLabor { get; }

    public IReadOnlyList<PhysicalTransportEdgeRuntimeRecord> Edges { get; }

    // Internal regression hooks: prove an event shares the immutable fine
    // geometry and allocates only the touched progress pages. No caller
    // can use these to modify authoritative records or worker locations.
    internal bool SharesCanonicalPhysicalStorageWith(
        PhysicalTransportInfrastructureRuntimeState other) =>
        other is not null && ReferenceEquals(
            _physicalEdges, other._physicalEdges);

    internal int AllocatedConstructionPageCount =>
        _constructionPages?.Count(page => page is not null) ?? 0;

    private PhysicalTransportInfrastructureRuntimeState(
        bool initialized, PhysicalTransportEdgeRuntimeRecord[] edges)
    {
        _physicalEdges = edges;
        IsInitialized = initialized;
        HasNaturalConstructionLabor = initialized &&
            edges.Length > 0 && edges.All(row =>
                row.NaturalConstructionLaborMicro.HasValue);
        HasCommittedConstructionLabor = false;
        Edges = Array.AsReadOnly(edges);
    }

    // Only ApplyJournaledWork can invoke this: the original topology and
    // physical fields were already validated during the one-time seeding.
    private PhysicalTransportInfrastructureRuntimeState(
        PhysicalTransportInfrastructureRuntimeState source,
        ulong[]?[] pages)
    {
        _physicalEdges = source._physicalEdges;
        _constructionPages = pages;
        IsInitialized = source.IsInitialized;
        HasNaturalConstructionLabor = source.HasNaturalConstructionLabor;
        HasCommittedConstructionLabor = true;
        Edges = new ProgressedEdgeView(_physicalEdges, pages);
    }

    private sealed class ProgressedEdgeView :
        IReadOnlyList<PhysicalTransportEdgeRuntimeRecord>
    {
        private readonly PhysicalTransportEdgeRuntimeRecord[] _physical;
        private readonly ulong[]?[] _pages;

        public ProgressedEdgeView(
            PhysicalTransportEdgeRuntimeRecord[] physical, ulong[]?[] pages)
        {
            _physical = physical;
            _pages = pages;
        }

        public int Count => _physical.Length;

        public PhysicalTransportEdgeRuntimeRecord this[int index]
        {
            get
            {
                var physical = _physical[index]; // checked by array bounds
                var page = _pages[index / ProgressPageSize];
                ulong committed = page is null
                    ? 0UL : page[index % ProgressPageSize];
                return physical with
                {
                    CommittedConstructionLaborMicro = committed
                };
            }
        }

        public IEnumerator<PhysicalTransportEdgeRuntimeRecord> GetEnumerator()
        {
            for (int index = 0; index < _physical.Length; index++)
                yield return this[index];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static PhysicalTransportInfrastructureRuntimeState CreateInitial(
        IEnumerable<PhysicalTransportEdgeRuntimeRecord> crossings)
    {
        ArgumentNullException.ThrowIfNull(crossings);
        var records = crossings.ToArray();
        if (records.Any(row => row?.CommittedConstructionLaborMicro is not null))
            throw new ArgumentException(
                "Committed construction progress is NEVER supplied during " +
                "world seeding; only accepted journal events can record work.",
                nameof(crossings));
        if (records.Length == 0)
            throw new ArgumentException(
                "An initialized physical world needs explicit natural or built " +
                "transport evidence for every fine crossing.", nameof(crossings));

        bool anyConstruction = records.Any(row =>
            row?.NaturalConstructionLaborMicro.HasValue == true);
        if (anyConstruction && records.Any(row =>
                row?.NaturalConstructionLaborMicro is not > 0UL))
            throw new ArgumentException(
                "V13 natural construction labor must be explicitly positive " +
                "on EVERY fine crossing. Mixed v12/v13 records are invalid.",
                nameof(crossings));

        var seen = new HashSet<PhysicalTacticalEdgeId>();
        GoldbergParameters? fine = null;
        foreach (var row in records)
        {
            if (row is null || !row.Edge.IsValid ||
                !Enum.IsDefined(row.OwnershipKind) ||
                row.FreePhysicalCostMicroPerWholeCargoUnit == 0UL ||
                row.CargoMicroAtReferenceLoad == 0UL ||
                !seen.Add(row.Edge) ||
                (row.OwnershipKind == PhysicalTransportOwnershipKind.Owned
                    ? row.InfrastructureOwner is not { IsValid: true }
                    : row.InfrastructureOwner.HasValue ||
                      row.ExternalTariffMicroPerWholeCargoUnit != 0UL))
                throw new ArgumentException(
                    "Each physical crossing must be unique, have positive " +
                    "natural freight and capacity, and valid ownership/toll.",
                    nameof(crossings));
            fine ??= row.Edge.FineGoldbergParameters;
            if (row.Edge.FineGoldbergParameters != fine.Value)
                throw new ArgumentException(
                    "Physical transport cannot mix incompatible fine worlds.",
                    nameof(crossings));
        }

        Array.Sort(records, (a, b) =>
            a.Edge.FineStrategicEdgeId.Value.CompareTo(
                b.Edge.FineStrategicEdgeId.Value));
        return new PhysicalTransportInfrastructureRuntimeState(true, records);
    }

    // Canonical IDs are sorted by the already validated one-time initializer.
    // Lookup of one physical edge therefore costs O(log E), not O(E).
    private int FindCanonicalEdgeIndex(PhysicalTacticalEdgeId crossing)
    {
        if (!crossing.IsValid || _physicalEdges.Length == 0 ||
            crossing.FineGoldbergParameters !=
                _physicalEdges[0].Edge.FineGoldbergParameters)
            return -1;

        ulong wanted = crossing.FineStrategicEdgeId.Value;
        int left = 0, right = _physicalEdges.Length - 1;
        while (left <= right)
        {
            int middle = left + (right - left) / 2;
            ulong current = _physicalEdges[middle].Edge.FineStrategicEdgeId.Value;
            if (current == wanted) return middle;
            if (current < wanted) left = middle + 1;
            else right = middle - 1;
        }
        return -1;
    }

    /// <summary>
    /// Read-only indexed crossing lookup, preserving the v12/v13/v14
    /// logical record projection. No phantom edge or implicit zero tariff.
    /// </summary>
    public bool TryGetEdge(PhysicalTacticalEdgeId crossing,
        out PhysicalTransportEdgeRuntimeRecord? record)
    {
        int index = FindCanonicalEdgeIndex(crossing);
        if (index < 0)
        {
            record = null;
            return false;
        }
        record = Edges[index];
        return true;
    }

    /// <summary>
    /// Domain primitive used only after validation by the real-worker event
    /// executor. A first event activates v14 without physically copying
    /// every crossing. Existing snapshots retain immutable old progress.
    /// </summary>
    public PhysicalTransportInfrastructureRuntimeState ApplyJournaledWork(
        PhysicalTacticalEdgeId crossing, ulong creditedLaborMicro)
    {
        if (!IsInitialized || !HasNaturalConstructionLabor ||
            !crossing.IsValid || creditedLaborMicro == 0UL)
            throw new InvalidOperationException(
                "Journaled construction requires a v13/v14 world, " +
                "one real crossing and strictly positive physical work.");

        int index = FindCanonicalEdgeIndex(crossing);
        if (index < 0 || _physicalEdges[index].OwnershipKind !=
                PhysicalTransportOwnershipKind.NoInfrastructure)
            throw new InvalidOperationException(
                "Cannot build on a missing or already owned/built crossing.");

        var target = _physicalEdges[index];
        int pageIndex = index / ProgressPageSize;
        int offset = index % ProgressPageSize;
        ulong already = _constructionPages?[pageIndex] is { } stored
            ? stored[offset] : 0UL;
        ulong required = target.NaturalConstructionLaborMicro!.Value;
        if (already >= required || creditedLaborMicro > required - already)
            throw new InvalidOperationException(
                "Journaled construction cannot exceed the natural " +
                "requirement or credit work to an already complete edge.");

        // Clone one shallow outer page directory and exactly one data page.
        // No canonical physical row or unrelated progress page is mutated.
        var nextPages = _constructionPages is null
            ? new ulong[]?[( _physicalEdges.Length + ProgressPageSize - 1) /
                ProgressPageSize]
            : (ulong[]?[])_constructionPages.Clone();
        var previousPage = nextPages[pageIndex];
        var changedPage = previousPage is null
            ? new ulong[ProgressPageSize]
            : (ulong[])previousPage.Clone();
        changedPage[offset] = checked(already + creditedLaborMicro);
        nextPages[pageIndex] = changedPage;
        return new PhysicalTransportInfrastructureRuntimeState(this, nextPages);
    }
}
