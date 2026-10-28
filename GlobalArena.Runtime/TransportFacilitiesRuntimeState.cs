using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// One explicitly recorded directed transfer capability at a facility.
/// A change of technology is a logistical capability, not an item recipe.
/// </summary>
public sealed record TransportFacilityTransferCapability
{
    public ulong FromTechnologyTypeId { get; }
    public ulong ToTechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }

    public TransportFacilityTransferCapability(
        ulong fromTechnologyTypeId, ulong toTechnologyTypeId,
        ulong technologyCatalogVersion)
    {
        if (fromTechnologyTypeId == 0UL || toTechnologyTypeId == 0UL ||
            fromTechnologyTypeId == toTechnologyTypeId ||
            technologyCatalogVersion == 0UL)
            throw new ArgumentException(
                "A directed transfer requires two distinct technologies and a valid catalog version.");
        FromTechnologyTypeId = fromTechnologyTypeId;
        ToTechnologyTypeId = toTechnologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
    }
}

/// <summary>
/// Persistent identity/location/operational capability only. Recording a
/// facility is NOT a completed construction workflow, ownership grant or
/// player-visible inventory object. No fictional maintenance parts are made.
/// </summary>
public sealed class TransportFacilityRuntimeRecord
{
    public ulong FacilityId { get; }
    public PhysicalTacticalTileId Tile { get; }
    public bool IsOperational { get; }
    public IReadOnlyList<TransportFacilityTransferCapability> Transfers { get; }

    public TransportFacilityRuntimeRecord(
        ulong facilityId, PhysicalTacticalTileId tile, bool isOperational,
        IEnumerable<TransportFacilityTransferCapability> transfers)
    {
        if (facilityId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        if (!tile.IsValid)
            throw new ArgumentException("Facility needs a valid qualified fine tile.", nameof(tile));
        ArgumentNullException.ThrowIfNull(transfers);
        var canonical = transfers.ToArray();
        if (canonical.Any(x => x is null))
            throw new ArgumentException("Facility capabilities must not be null.", nameof(transfers));
        canonical = canonical
            .OrderBy(x => x.FromTechnologyTypeId)
            .ThenBy(x => x.ToTechnologyTypeId)
            .ThenBy(x => x.TechnologyCatalogVersion)
            .ToArray();
        for (var i = 1; i < canonical.Length; i++)
            if (canonical[i - 1].FromTechnologyTypeId == canonical[i].FromTechnologyTypeId &&
                canonical[i - 1].ToTechnologyTypeId == canonical[i].ToTechnologyTypeId &&
                canonical[i - 1].TechnologyCatalogVersion == canonical[i].TechnologyCatalogVersion)
                throw new ArgumentException("Duplicate directed transfer capability.", nameof(transfers));

        FacilityId = facilityId;
        Tile = tile;
        IsOperational = isOperational;
        Transfers = Array.AsReadOnly(canonical);
    }

    public bool CanTransfer(ulong fromTechnologyTypeId, ulong toTechnologyTypeId,
        ulong technologyCatalogVersion) =>
        IsOperational && Transfers.Any(x =>
            x.FromTechnologyTypeId == fromTechnologyTypeId &&
            x.ToTechnologyTypeId == toTechnologyTypeId &&
            x.TechnologyCatalogVersion == technologyCatalogVersion);
}

/// <summary>
/// Canonically ordered, immutable part of WorldState. Empty is the default.
/// It records status for later construction authority, but cannot attest to
/// a construction event because no construction action exists yet.
/// </summary>
public sealed class TransportFacilitiesRuntimeState
{
    public static TransportFacilitiesRuntimeState Empty { get; } =
        new(Array.Empty<TransportFacilityRuntimeRecord>());

    public IReadOnlyList<TransportFacilityRuntimeRecord> Facilities { get; }

    public TransportFacilitiesRuntimeState(
        IEnumerable<TransportFacilityRuntimeRecord> facilities)
    {
        ArgumentNullException.ThrowIfNull(facilities);
        var canonical = facilities.ToArray();
        if (canonical.Any(x => x is null))
            throw new ArgumentException("Facility records must not be null.", nameof(facilities));
        canonical = canonical.OrderBy(x => x.FacilityId).ToArray();
        for (var i = 1; i < canonical.Length; i++)
            if (canonical[i - 1].FacilityId == canonical[i].FacilityId)
                throw new ArgumentException("Duplicate transport facility identity.", nameof(facilities));
        Facilities = Array.AsReadOnly(canonical);
    }

    public bool TryFind(ulong facilityId, out TransportFacilityRuntimeRecord? facility)
    {
        facility = Facilities.FirstOrDefault(x => x.FacilityId == facilityId);
        return facility is not null;
    }
}
