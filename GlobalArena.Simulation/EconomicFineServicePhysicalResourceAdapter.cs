using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit one-to-one binding between a built transport asset identity and
/// the cost-bearing physical resource representing its use. A strategic
/// geographic edge is not a resource and is never silently cast into one.
/// </summary>
public sealed class EconomicPhysicalAssetResourceBinding
{
    public ulong AssetId { get; }
    public EconomicPhysicalResourceId ResourceId { get; }

    public EconomicPhysicalAssetResourceBinding(
        ulong assetId, EconomicPhysicalResourceId resourceId)
    {
        if (assetId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(assetId));
        if (!resourceId.IsValid)
            throw new ArgumentException("Invalid physical resource.", nameof(resourceId));
        AssetId = assetId;
        ResourceId = resourceId;
    }
}

/// <summary>
/// Explicit cost-bearing transfer facility at an actual technology change
/// between consecutive service legs, at the exact validated fine tile.
/// It does not itself construct a facility or grant permission to operate.
/// </summary>
public sealed class EconomicPhysicalTransferResourceBinding
{
    public ulong ServiceId { get; }
    public int AfterLegIndex { get; }
    public EconomicPhysicalResourceId ResourceId { get; }
    public PhysicalTacticalTileId TransferTile { get; }

    public EconomicPhysicalTransferResourceBinding(
        ulong serviceId, int afterLegIndex,
        EconomicPhysicalResourceId resourceId,
        PhysicalTacticalTileId transferTile)
    {
        if (serviceId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(serviceId));
        if (afterLegIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(afterLegIndex));
        if (!resourceId.IsValid)
            throw new ArgumentException("Transfer requires a valid resource.", nameof(resourceId));
        if (!transferTile.IsValid)
            throw new ArgumentException("Transfer requires a valid fine tile.", nameof(transferTile));

        ServiceId = serviceId;
        AfterLegIndex = afterLegIndex;
        ResourceId = resourceId;
        TransferTile = transferTile;
    }
}

/// <summary>
/// Reuses the EXISTING fine-geography service quote resolver as the source
/// of continuity, technology-handoff, access, and asset-operational checks.
/// Maps ONLY its validated selected services to independently declared
/// physical resources for read-only market/cost scenario exploration.
/// Does not change infrastructure, invent a service, grant access, select an
/// economic regime, or create financial obligations.
/// </summary>
public sealed class EconomicFineServicePhysicalResourceAdapter
{
    private readonly EconomicTransportServiceQuoteResolver _resolver = new();

    public IReadOnlyList<EconomicReclearingQuotedRoute> Resolve(
        ulong turnOrdinal,
        StrategicSurfaceGraph fineGeography,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> finePointAnchors,
        EconomicProductionDemandResult flows,
        StrategicEdgeAccessSnapshot access,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<EconomicTransportServiceOffer> offeredServices,
        IEnumerable<EconomicPhysicalAssetResourceBinding> assetResourceBindings,
        IEnumerable<EconomicPhysicalTransferResourceBinding> transferResourceBindings)
    {
        ArgumentNullException.ThrowIfNull(fineGeography);
        ArgumentNullException.ThrowIfNull(finePointAnchors);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(offeredServices);
        ArgumentNullException.ThrowIfNull(assetResourceBindings);
        ArgumentNullException.ThrowIfNull(transferResourceBindings);

        var assets = installedAssets.ToArray();
        var offers = offeredServices.ToArray();
        var assetMapping = assetResourceBindings.ToArray();
        var transfers = transferResourceBindings.ToArray();
        if (assets.Any(x => x is null) ||
            offers.Any(x => x is null) ||
            assetMapping.Any(x => x is null) ||
            transfers.Any(x => x is null))
            throw new ArgumentException("Physical service snapshot cannot contain null entries.");

        var assetsById = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        foreach (var asset in assets)
            if (!assetsById.TryAdd(asset.AssetId, asset))
                throw new ArgumentException("Duplicate built asset identity.", nameof(installedAssets));

        var mappedAssets = new Dictionary<ulong, EconomicPhysicalResourceId>();
        var mappedResources = new HashSet<EconomicPhysicalResourceId>();
        foreach (var item in assetMapping)
            if (!assetsById.ContainsKey(item.AssetId) ||
                !mappedAssets.TryAdd(item.AssetId, item.ResourceId) ||
                !mappedResources.Add(item.ResourceId))
                throw new ArgumentException(
                    "Each declared built asset must have its own unique physical resource identity.",
                    nameof(assetResourceBindings));

        // A transfer resource is a distinct facility, not an alias for an
        // existing built crossing. Shared transfer resources across services
        // are permitted only when bound to the SAME fine tile.
        var byTransfer = new Dictionary<(ulong ServiceId, int AfterLegIndex),
            EconomicPhysicalTransferResourceBinding>();
        var transferLocations = new Dictionary<EconomicPhysicalResourceId, PhysicalTacticalTileId>();
        foreach (var transfer in transfers)
        {
            if (!byTransfer.TryAdd((transfer.ServiceId, transfer.AfterLegIndex), transfer) ||
                mappedResources.Contains(transfer.ResourceId))
                throw new ArgumentException(
                    "Transfer binding is duplicate or aliases a built asset resource.",
                    nameof(transferResourceBindings));
            if (transferLocations.TryGetValue(transfer.ResourceId, out var previous))
            {
                if (previous != transfer.TransferTile)
                    throw new ArgumentException(
                        "One transfer facility cannot exist at two fine tiles.",
                        nameof(transferResourceBindings));
            }
            else
                transferLocations.Add(transfer.ResourceId, transfer.TransferTile);
        }

        // Revalidation happens for every call, against the CURRENT graph,
        // fine-point anchors, closed corridors and built operational assets.
        // A prior quote or a guessed asset path cannot bypass these checks.
        var selections = _resolver.Resolve(
            turnOrdinal, fineGeography, finePointAnchors,
            flows, access, assets, offers);
        var selectedServiceIds = selections.Select(x => x.ServiceId).ToHashSet();
        if (byTransfer.Keys.Any(key => !selectedServiceIds.Contains(key.ServiceId)))
            throw new ArgumentException(
                "Transfer was declared for a non-selected or unavailable service.",
                nameof(transferResourceBindings));

        var result = new List<EconomicReclearingQuotedRoute>();
        var geographicEdges = fineGeography.StrategicTopology.Edges;
        var fineResolution = fineGeography.StrategicTopology.Parameters;
        foreach (var selected in selections)
        {
            var routeResources = new List<EconomicPhysicalResourceId>();
            var seenResources = new HashSet<EconomicPhysicalResourceId>();
            var start = finePointAnchors[selected.Quote.RouteKey.SourceEconomicPointId];
            var currentCell = start.FineStrategicCellId;
            var assetIds = selected.AssetIds;

            for (var index = 0; index < assetIds.Count; index++)
            {
                var assetId = assetIds[index];
                if (!mappedAssets.TryGetValue(assetId, out var resource))
                    throw new InvalidOperationException(
                        "A selected physical itinerary has no cost-bearing resource binding.");
                if (!seenResources.Add(resource))
                    throw new InvalidOperationException(
                        "One itinerary attempts to count the same physical resource twice.");
                routeResources.Add(resource);

                // The upstream quote resolver already validated contiguity.
                // Reconstruct only the fine transfer location for each
                // TECHNOLOGY handoff, never invent a transfer at a mere node.
                var asset = assetsById[assetId];
                var edge = geographicEdges[checked((int)(asset.GeographicEdgeId.Value - 1UL))];
                var ends = edge.IncidentCellIds;
                if (currentCell == ends[0])
                    currentCell = ends[1];
                else if (currentCell == ends[1])
                    currentCell = ends[0];
                else
                    throw new InvalidOperationException(
                        "Validated service no longer follows its fine geography snapshot.");

                if (index == assetIds.Count - 1)
                    continue;

                var transferKey = (selected.ServiceId, index);
                var changesTechnology = selected.TechnologyTypeIds[index] !=
                    selected.TechnologyTypeIds[index + 1];
                var hasTransfer = byTransfer.TryGetValue(transferKey, out var transfer);
                if (changesTechnology != hasTransfer)
                    throw new InvalidOperationException(
                        "Every technology change requires one explicitly located transfer facility; same-technology passage does not.");
                if (!hasTransfer)
                    continue;

                if (transfer!.TransferTile.FineGoldbergParameters != fineResolution ||
                    transfer.TransferTile.FineStrategicCellId != currentCell)
                    throw new InvalidOperationException(
                        "Transfer facility is not at the real fine-topology junction.");
                if (!seenResources.Add(transfer.ResourceId))
                    throw new InvalidOperationException(
                        "One itinerary attempts to use a physical resource twice.");
                routeResources.Add(transfer.ResourceId);
            }
            result.Add(new EconomicReclearingQuotedRoute(
                selected.Quote, routeResources));
        }

        // A stale or irrelevant transfer declaration must not be treated as
        // an unused cost-free economic option.
        foreach (var key in byTransfer.Keys)
        {
            var found = selections.Any(s =>
                s.ServiceId == key.ServiceId &&
                key.AfterLegIndex < s.AssetIds.Count - 1 &&
                s.TechnologyTypeIds[key.AfterLegIndex] !=
                s.TechnologyTypeIds[key.AfterLegIndex + 1]);
            if (!found)
                throw new ArgumentException(
                    "Transfer exists without a matching service technology handoff.",
                    nameof(transferResourceBindings));
        }
        return Array.AsReadOnly(result.ToArray());
    }
}
