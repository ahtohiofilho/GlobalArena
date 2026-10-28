using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-attested evidence for ONE directed technology handoff capability
/// at a concrete, snapshot-local facility. A shared tile/node alone does not
/// authorize cargo transfer. This record is NOT a built-terminal WorldState
/// event, inventory item, construction recipe or institutional permission.
/// </summary>
public sealed class EconomicFineTransferFacilityEvidence
{
    public ulong FacilityId { get; }
    public EconomicPhysicalResourceId ResourceId { get; }
    public PhysicalTacticalTileId Tile { get; }
    public ulong FromTechnologyTypeId { get; }
    public ulong ToTechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public bool IsOperational { get; }

    public EconomicFineTransferFacilityEvidence(
        ulong facilityId, EconomicPhysicalResourceId resourceId,
        PhysicalTacticalTileId tile,
        ulong fromTechnologyTypeId, ulong toTechnologyTypeId,
        ulong technologyCatalogVersion, bool isOperational = true)
    {
        if (facilityId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        if (!resourceId.IsValid || !tile.IsValid)
            throw new ArgumentException("Facility requires a physical resource and qualified fine tile.");
        if (fromTechnologyTypeId == 0UL || toTechnologyTypeId == 0UL ||
            fromTechnologyTypeId == toTechnologyTypeId)
            throw new ArgumentException(
                "Transfer capability needs two distinct valid technologies.");
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));

        FacilityId = facilityId;
        ResourceId = resourceId;
        Tile = tile;
        FromTechnologyTypeId = fromTechnologyTypeId;
        ToTechnologyTypeId = toTechnologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        IsOperational = isOperational;
    }
}

/// <summary>
/// A single-cycle, caller-attested facility-capability snapshot, strictly
/// separate from real WorldState construction/persistence authority.
/// Resource identity is unique to a facility, while the SAME facility can
/// support several explicitly granted directed technology transitions.
/// No wildcard modes, default reciprocity, or implicit capacity is created.
/// </summary>
public sealed class EconomicFineTransferFacilityAuthorizationSnapshot
{
    private readonly Dictionary<
        (EconomicPhysicalResourceId Resource, ulong From, ulong To, ulong Version),
        EconomicFineTransferFacilityEvidence> _capabilities = new();

    public ulong TurnOrdinal { get; }

    public EconomicFineTransferFacilityAuthorizationSnapshot(
        ulong turnOrdinal,
        IEnumerable<EconomicFineTransferFacilityEvidence> evidence)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(evidence);
        TurnOrdinal = turnOrdinal;

        var facilityById = new Dictionary<ulong,
            (EconomicPhysicalResourceId Resource, PhysicalTacticalTileId Tile,
             bool IsOperational)>();
        var facilityByResource = new Dictionary<EconomicPhysicalResourceId, ulong>();
        foreach (var row in evidence)
        {
            if (row is null)
                throw new ArgumentException("Transfer evidence cannot contain null.", nameof(evidence));
            if (facilityById.TryGetValue(row.FacilityId, out var existing))
            {
                if (existing.Resource != row.ResourceId ||
                    existing.Tile != row.Tile ||
                    existing.IsOperational != row.IsOperational)
                    throw new ArgumentException(
                        "One facility identity cannot change resource, location or operational status within a snapshot.",
                        nameof(evidence));
            }
            else
                facilityById.Add(row.FacilityId,
                    (row.ResourceId, row.Tile, row.IsOperational));

            if (facilityByResource.TryGetValue(row.ResourceId, out var previous))
            {
                if (previous != row.FacilityId)
                    throw new ArgumentException(
                        "One physical resource cannot impersonate multiple transfer facilities.",
                        nameof(evidence));
            }
            else
                facilityByResource.Add(row.ResourceId, row.FacilityId);

            if (!_capabilities.TryAdd(
                (row.ResourceId, row.FromTechnologyTypeId,
                    row.ToTechnologyTypeId, row.TechnologyCatalogVersion), row))
                throw new ArgumentException(
                    "Duplicate transfer capability in a single evidence snapshot.",
                    nameof(evidence));
        }
    }

    public ulong RequireOperationalHandoff(
        EconomicPhysicalTransferResourceBinding binding,
        ulong fromTechnologyTypeId, ulong toTechnologyTypeId,
        ulong technologyCatalogVersion)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!_capabilities.TryGetValue(
            (binding.ResourceId, fromTechnologyTypeId,
                toTechnologyTypeId, technologyCatalogVersion), out var facility) ||
            !facility.IsOperational ||
            facility.Tile != binding.TransferTile)
            throw new InvalidOperationException(
                "No operational, correctly located facility grants this exact directed technology transfer.");
        return facility.FacilityId;
    }
}

/// <summary>
/// Optional strict handoff gate layered on Gate D1. The existing geographic
/// resolver STILL verifies route continuity, real fine-tile junction and
/// operational built crossing assets. Before it is permitted to quote,
/// this adapter checks that every available offered technology handoff
/// has explicit located facility evidence in the given turn snapshot.
///
/// The evidence itself is supplied by the caller: this is NOT proof that the
/// facility exists in authoritative WorldState. A future authoritative
/// facility registry must populate/verify these snapshot records.
/// No currency, item catalog, cargo operations or economic selection occurs.
/// </summary>
public sealed class EconomicFineAuthorizedServicePhysicalResourceAdapter
{
    private readonly EconomicFineServicePhysicalResourceAdapter _adapter = new();

    public IReadOnlyList<EconomicReclearingQuotedRoute> Resolve(
        ulong turnOrdinal,
        StrategicSurfaceGraph fineGeography,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> finePointAnchors,
        EconomicProductionDemandResult flows,
        StrategicEdgeAccessSnapshot access,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<EconomicTransportServiceOffer> offeredServices,
        IEnumerable<EconomicPhysicalAssetResourceBinding> assetResourceBindings,
        IEnumerable<EconomicPhysicalTransferResourceBinding> transferResourceBindings,
        EconomicFineTransferFacilityAuthorizationSnapshot facilitySnapshot)
    {
        ArgumentNullException.ThrowIfNull(fineGeography);
        ArgumentNullException.ThrowIfNull(finePointAnchors);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(offeredServices);
        ArgumentNullException.ThrowIfNull(assetResourceBindings);
        ArgumentNullException.ThrowIfNull(transferResourceBindings);
        ArgumentNullException.ThrowIfNull(facilitySnapshot);

        if (turnOrdinal == 0UL || facilitySnapshot.TurnOrdinal != turnOrdinal)
            throw new InvalidOperationException(
                "Transfer facility evidence must belong to the economic turn being evaluated.");

        var assets = installedAssets.ToArray();
        var offers = offeredServices.ToArray();
        var assetMappings = assetResourceBindings.ToArray();
        var transfers = transferResourceBindings.ToArray();
        if (assets.Any(x => x is null) || offers.Any(x => x is null) ||
            assetMappings.Any(x => x is null) || transfers.Any(x => x is null))
            throw new ArgumentException("Physical authorization input contains a null row.");

        var byAsset = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        foreach (var asset in assets)
            if (!byAsset.TryAdd(asset.AssetId, asset))
                throw new ArgumentException("Duplicate built asset ID.", nameof(installedAssets));

        var byService = new Dictionary<ulong, EconomicTransportServiceOffer>();
        foreach (var offer in offers)
            if (!byService.TryAdd(offer.ServiceId, offer))
                throw new ArgumentException("Duplicate offered service ID.", nameof(offeredServices));

        var byTransfer = new Dictionary<(ulong Service, int AfterLeg),
            EconomicPhysicalTransferResourceBinding>();
        foreach (var binding in transfers)
        {
            if (!byTransfer.TryAdd((binding.ServiceId, binding.AfterLegIndex), binding) ||
                !byService.TryGetValue(binding.ServiceId, out var offer) ||
                binding.AfterLegIndex >= offer.AssetPath.Count - 1 ||
                !offer.TransferAfterLeg.Contains(binding.AfterLegIndex))
                throw new ArgumentException(
                    "Transfer evidence must match one actual declared service handoff.",
                    nameof(transferResourceBindings));

            if (!byAsset.TryGetValue(offer.AssetPath[binding.AfterLegIndex],
                    out var preceding) ||
                !byAsset.TryGetValue(offer.AssetPath[binding.AfterLegIndex + 1],
                    out var following))
                throw new ArgumentException(
                    "Transfer cannot claim a service leg missing from installed assets.",
                    nameof(offeredServices));
            if (preceding.TechnologyCatalogVersion != following.TechnologyCatalogVersion ||
                preceding.TechnologyTypeId == following.TechnologyTypeId)
                throw new InvalidOperationException(
                    "A transfer cannot alias an unchanged technology or mixed catalog versions.");
            facilitySnapshot.RequireOperationalHandoff(binding,
                preceding.TechnologyTypeId, following.TechnologyTypeId,
                preceding.TechnologyCatalogVersion);
        }

        // Missing handoff evidence must fail before evaluating any freight
        // pricing function, even if another service looks commercially cheaper.
        foreach (var offer in offers)
        {
            if (!offer.IsAvailable)
                continue;
            var eligible = offer.AssetPath.All(id =>
                byAsset.TryGetValue(id, out var asset) &&
                asset.IsOperational && access.IsOpen(asset.GeographicEdgeId));
            if (!eligible)
                continue;
            foreach (var index in offer.TransferAfterLeg)
                if (!byTransfer.ContainsKey((offer.ServiceId, index)))
                    throw new InvalidOperationException(
                        "Available multimodal service lacks operational facility evidence.");
        }

        return _adapter.Resolve(
            turnOrdinal, fineGeography, finePointAnchors,
            flows, access, assets, offers, assetMappings, transfers);
    }
}
