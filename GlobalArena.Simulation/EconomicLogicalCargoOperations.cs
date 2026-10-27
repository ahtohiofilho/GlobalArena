using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Commercial HANDLING at an explicitly authorized facility. This enum
/// describes an operation, NEVER a canonical list of transport modes.
/// Transit through a node conveys neither operation automatically.
/// </summary>
public enum EconomicLogicalCargoOperation : byte
{
    Load = 1,
    Unload = 2
}

/// <summary>
/// Snapshot-local facility at a logical node for one economic point.
/// A facility may accept MANY incident route links; it is not a separate
/// terminal per geographic border, incoming direction or line segment.
/// </summary>
public sealed class EconomicLogicalCargoFacility
{
    public ulong FacilityId { get; }
    public EconomicPointId EconomicPointId { get; }
    public EconomicTransportLogicalNodeId NodeId { get; }
    public bool IsOperational { get; }

    public EconomicLogicalCargoFacility(
        ulong facilityId, EconomicPointId economicPointId,
        EconomicTransportLogicalNodeId nodeId, bool isOperational = true)
    {
        if (facilityId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        if (!economicPointId.IsValid || !nodeId.IsValid)
            throw new ArgumentException(
                "A facility requires a valid economic point and logical node.");
        FacilityId = facilityId;
        EconomicPointId = economicPointId;
        NodeId = nodeId;
        IsOperational = isOperational;
    }
}

/// <summary>
/// Independent permission for one facility to LOAD or UNLOAD with a
/// specific technology and catalogue version. No implied reciprocal
/// operation, cross-technology capability or modal fixed cost.
/// </summary>
public sealed class EconomicLogicalCargoPermission
{
    public ulong FacilityId { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public EconomicLogicalCargoOperation Operation { get; }

    public EconomicLogicalCargoPermission(
        ulong facilityId, ulong technologyTypeId,
        ulong technologyCatalogVersion,
        EconomicLogicalCargoOperation operation)
    {
        if (facilityId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        if (operation is not EconomicLogicalCargoOperation.Load and
            not EconomicLogicalCargoOperation.Unload)
            throw new ArgumentOutOfRangeException(nameof(operation));
        FacilityId = facilityId;
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        Operation = operation;
    }
}

/// <summary>
/// EXPLICIT authorization to hand off cargo at one facility from
/// technology/version A to DISTINCT technology/version B. Mere shared
/// logical-node connectivity never supplies this permission. This does
/// not automatically create a composed multi-technology service.
/// </summary>
public sealed class EconomicLogicalCargoTransferPermission
{
    public ulong FacilityId { get; }
    public ulong FromTechnologyTypeId { get; }
    public ulong FromTechnologyCatalogVersion { get; }
    public ulong ToTechnologyTypeId { get; }
    public ulong ToTechnologyCatalogVersion { get; }

    public EconomicLogicalCargoTransferPermission(
        ulong facilityId, ulong fromTechnologyTypeId,
        ulong fromTechnologyCatalogVersion,
        ulong toTechnologyTypeId, ulong toTechnologyCatalogVersion)
    {
        if (facilityId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        if (fromTechnologyTypeId == 0UL || toTechnologyTypeId == 0UL ||
            fromTechnologyCatalogVersion == 0UL ||
            toTechnologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(fromTechnologyTypeId));
        if (fromTechnologyTypeId == toTechnologyTypeId &&
            fromTechnologyCatalogVersion == toTechnologyCatalogVersion)
            throw new ArgumentException(
                "An explicit transfer must change the technology identity/version.");
        FacilityId = facilityId;
        FromTechnologyTypeId = fromTechnologyTypeId;
        FromTechnologyCatalogVersion = fromTechnologyCatalogVersion;
        ToTechnologyTypeId = toTechnologyTypeId;
        ToTechnologyCatalogVersion = toTechnologyCatalogVersion;
    }
}

/// <summary>
/// Caller-attested handling authorization for ONE snapshot, independently
/// of the registered physical link/endpoint authorization snapshot.
/// Nothing in WorldState is modified. A point-to-node anchor is NOT a
/// facility and cannot substitute for one.
/// </summary>
public sealed class EconomicLogicalCargoOperationsSnapshot
{
    private readonly Dictionary<ulong, EconomicLogicalCargoFacility> _facilities = new();
    private readonly HashSet<(ulong FacilityId, ulong Technology, ulong Version,
        EconomicLogicalCargoOperation Operation)> _operations = new();
    private readonly HashSet<(ulong FacilityId, ulong FromTechnology,
        ulong FromVersion, ulong ToTechnology, ulong ToVersion)> _transfers = new();

    public ulong SnapshotOrdinal { get; }

    public EconomicLogicalCargoOperationsSnapshot(
        ulong snapshotOrdinal,
        IEnumerable<EconomicLogicalCargoFacility> facilities,
        IEnumerable<EconomicLogicalCargoPermission> permissions,
        IEnumerable<EconomicLogicalCargoTransferPermission>? transfers = null)
    {
        if (snapshotOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(snapshotOrdinal));
        ArgumentNullException.ThrowIfNull(facilities);
        ArgumentNullException.ThrowIfNull(permissions);
        SnapshotOrdinal = snapshotOrdinal;

        foreach (var facility in facilities)
            if (facility is null || !_facilities.TryAdd(
                    facility.FacilityId, facility))
                throw new ArgumentException(
                    "Facility identities must be unique and valid.",
                    nameof(facilities));

        foreach (var grant in permissions)
            if (grant is null || !_facilities.ContainsKey(grant.FacilityId) ||
                !_operations.Add((
                    grant.FacilityId, grant.TechnologyTypeId,
                    grant.TechnologyCatalogVersion, grant.Operation)))
                throw new ArgumentException(
                    "Handling grants must refer to a unique existing facility.",
                    nameof(permissions));

        if (transfers is not null)
            foreach (var transfer in transfers)
                if (transfer is null ||
                    !_facilities.ContainsKey(transfer.FacilityId) ||
                    !_transfers.Add((
                        transfer.FacilityId, transfer.FromTechnologyTypeId,
                        transfer.FromTechnologyCatalogVersion,
                        transfer.ToTechnologyTypeId,
                        transfer.ToTechnologyCatalogVersion)))
                    throw new ArgumentException(
                        "Transfer grants must refer to a unique existing facility.",
                        nameof(transfers));
    }

    /// <summary>
    /// Deterministically selects the smallest operational facility ID
    /// that explicitly authorizes this exact commercial endpoint action.
    /// Multiple incoming lines at the same node never require separate
    /// facility records and never grant additional rights.
    /// </summary>
    public ulong RequireOperation(
        EconomicPointId economicPointId, EconomicTransportLogicalNodeId nodeId,
        ulong technologyTypeId, ulong technologyCatalogVersion,
        EconomicLogicalCargoOperation operation)
    {
        if (!economicPointId.IsValid || !nodeId.IsValid ||
            technologyTypeId == 0UL || technologyCatalogVersion == 0UL ||
            operation is not EconomicLogicalCargoOperation.Load and
                not EconomicLogicalCargoOperation.Unload)
            throw new ArgumentException("Invalid commercial handling request.");

        foreach (var facility in _facilities.Values.OrderBy(x => x.FacilityId))
            if (facility.IsOperational &&
                facility.EconomicPointId == economicPointId &&
                facility.NodeId == nodeId &&
                _operations.Contains((facility.FacilityId, technologyTypeId,
                    technologyCatalogVersion, operation)))
                return facility.FacilityId;

        throw new InvalidOperationException(
            "No compatible operational cargo facility permits the requested action.");
    }

    /// <summary>
    /// Validates a directional transfer, including incoming UNLOAD,
    /// outgoing LOAD and its explicit directed handoff authorization.
    /// This function does NOT join two services or set transfer costs.
    /// </summary>
    public void RequireTransfer(
        ulong facilityId, EconomicTransportLogicalNodeId nodeId,
        ulong fromTechnologyTypeId, ulong fromTechnologyCatalogVersion,
        ulong toTechnologyTypeId, ulong toTechnologyCatalogVersion)
    {
        if (!_facilities.TryGetValue(facilityId, out var facility) ||
            !facility.IsOperational || !nodeId.IsValid ||
            facility.NodeId != nodeId ||
            !_transfers.Contains((facilityId, fromTechnologyTypeId,
                fromTechnologyCatalogVersion, toTechnologyTypeId,
                toTechnologyCatalogVersion)) ||
            !_operations.Contains((facilityId, fromTechnologyTypeId,
                fromTechnologyCatalogVersion, EconomicLogicalCargoOperation.Unload)) ||
            !_operations.Contains((facilityId, toTechnologyTypeId,
                toTechnologyCatalogVersion, EconomicLogicalCargoOperation.Load)))
            throw new InvalidOperationException(
                "Handoff requires one operational facility and explicit " +
                "directional unload, load and transfer authorizations.");
    }
}

/// <summary>
/// Immutable reference provenance of a quoted service and its separately
/// authorized cargo loading and unloading facilities. No intermediate
/// nodes are treated as commercial stops.
/// </summary>
public sealed class EconomicOperationalLogicalTransportSelection
{
    public EconomicLogicalRegisteredServiceSelection RegisteredSelection { get; }
    public EconomicTurnFreightQuote Quote => RegisteredSelection.Quote;
    public ulong OperationsSnapshotOrdinal { get; }
    public ulong LoadFacilityId { get; }
    public ulong UnloadFacilityId { get; }

    internal EconomicOperationalLogicalTransportSelection(
        EconomicLogicalRegisteredServiceSelection registeredSelection,
        ulong operationsSnapshotOrdinal, ulong loadFacilityId,
        ulong unloadFacilityId)
    {
        RegisteredSelection = registeredSelection;
        OperationsSnapshotOrdinal = operationsSnapshotOrdinal;
        LoadFacilityId = loadFacilityId;
        UnloadFacilityId = unloadFacilityId;
    }
}

/// <summary>
/// Strict opt-in quote entrypoint combining versioned traversal registry,
/// physical link authorization AND independent endpoint cargo operations.
/// Missing cargo operations fail closed BEFORE calling a price function.
/// Existing provider-only resolution remains unchanged for reference uses.
/// </summary>
public sealed class EconomicOperationalLogicalTransportQuoteResolver
{
    public IReadOnlyList<EconomicOperationalLogicalTransportSelection> Resolve(
        ulong turnOrdinal,
        EconomicLogicalTransportProviderRegistry registry,
        EconomicLogicalTransportAuthorizationSnapshot physicalSnapshot,
        EconomicLogicalCargoOperationsSnapshot operationsSnapshot,
        IEnumerable<EconomicLogicalTransportServiceIntent> intents,
        IEconomicLogicalTransportItineraryPricing pricing)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(physicalSnapshot);
        ArgumentNullException.ThrowIfNull(operationsSnapshot);
        ArgumentNullException.ThrowIfNull(intents);
        ArgumentNullException.ThrowIfNull(pricing);
        if (physicalSnapshot.SnapshotOrdinal != operationsSnapshot.SnapshotOrdinal)
            throw new InvalidOperationException(
                "Physical and commercial operation snapshots must be identical.");

        var declared = intents.ToArray();
        if (declared.Any(x => x is null) ||
            declared.Select(x => x!.ServiceId).Distinct().Count() != declared.Length)
            throw new ArgumentException(
                "Declared commercial services must have distinct identities.",
                nameof(intents));

        // Validate ALL ACTIVE service endpoints BEFORE any candidate quote
        // is evaluated. This prevents a later unauthorized offer from
        // being hidden by an earlier successfully priced commercial offer.
        var facilitiesByService = new Dictionary<ulong, (ulong Load, ulong Unload)>();
        foreach (var intent in declared.OrderBy(x => x.ServiceId))
        {
            if (!intent.IsAvailable)
                continue;
            var load = operationsSnapshot.RequireOperation(
                intent.RouteKey.SourceEconomicPointId, intent.StartNode,
                intent.TechnologyTypeId, intent.TechnologyCatalogVersion,
                EconomicLogicalCargoOperation.Load);
            var unload = operationsSnapshot.RequireOperation(
                intent.RouteKey.DestinationEconomicPointId, intent.EndNode,
                intent.TechnologyTypeId, intent.TechnologyCatalogVersion,
                EconomicLogicalCargoOperation.Unload);
            facilitiesByService.Add(intent.ServiceId, (load, unload));
        }

        var quotes = new EconomicRegisteredLogicalTransportQuoteResolver()
            .Resolve(turnOrdinal, registry, physicalSnapshot, declared, pricing);
        return Array.AsReadOnly(quotes.Select(quote =>
        {
            var pair = facilitiesByService[quote.Selection.ServiceId];
            return new EconomicOperationalLogicalTransportSelection(
                quote, operationsSnapshot.SnapshotOrdinal,
                pair.Load, pair.Unload);
        }).ToArray());
    }
}
