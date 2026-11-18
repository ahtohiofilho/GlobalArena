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
    ulong? NaturalConstructionLaborMicro = null);

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
    public static PhysicalTransportInfrastructureRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<PhysicalTransportEdgeRuntimeRecord>());

    public bool IsInitialized { get; }

    /// <summary>
    /// Only complete all-edge construction data may promote transport to v13.
    /// Old v12 inventories remain valid and hash exactly as before.
    /// </summary>
    public bool HasNaturalConstructionLabor { get; }

    public IReadOnlyList<PhysicalTransportEdgeRuntimeRecord> Edges { get; }

    private PhysicalTransportInfrastructureRuntimeState(
        bool initialized, PhysicalTransportEdgeRuntimeRecord[] edges)
    {
        IsInitialized = initialized;
        HasNaturalConstructionLabor = initialized &&
            edges.Length > 0 && edges.All(row =>
                row.NaturalConstructionLaborMicro.HasValue);
        Edges = Array.AsReadOnly(edges);
    }

    public static PhysicalTransportInfrastructureRuntimeState CreateInitial(
        IEnumerable<PhysicalTransportEdgeRuntimeRecord> crossings)
    {
        ArgumentNullException.ThrowIfNull(crossings);
        var records = crossings.ToArray();
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
}
