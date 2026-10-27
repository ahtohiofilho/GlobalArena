using System.Numerics;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Intent to discover ONE single-technology service on a fine geography
/// snapshot. This is an offered service identity, not permission to invent
/// services, infrastructure, transit or technology handoffs.
/// </summary>
public sealed class EconomicFineTransportServiceIntent
{
    public ulong ServiceId { get; }
    public StrategicEconomicRouteKey RouteKey { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public BigInteger ExpectedLoadMicrograms { get; }
    public bool IsAvailable { get; }

    public EconomicFineTransportServiceIntent(
        ulong serviceId, StrategicEconomicRouteKey routeKey,
        ulong technologyTypeId, ulong technologyCatalogVersion,
        BigInteger expectedLoadMicrograms, bool isAvailable = true)
    {
        if (serviceId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(serviceId));
        if (!routeKey.SourceEconomicPointId.IsValid ||
            !routeKey.DestinationEconomicPointId.IsValid)
            throw new ArgumentException("Service points must be valid.", nameof(routeKey));
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        if (expectedLoadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));
        ServiceId = serviceId;
        RouteKey = routeKey;
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        ExpectedLoadMicrograms = expectedLoadMicrograms;
        IsAvailable = isAvailable;
    }
}

/// <summary>
/// Supplies the price function for the WHOLE discovered physical itinerary,
/// including its topology, assets and technology as needed by the caller's
/// ruleset. No prices, transport-mode tariffs or discounts are inferred from
/// hop count. Invoked only for an actually connected eligible service.
/// </summary>
public interface IEconomicFineTransportItineraryPricing
{
    IEconomicTurnFreightCostFunction CreateForItinerary(
        EconomicFineTransportServiceIntent intent,
        IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets);
}

/// <summary>
/// Opt-in, topology-local reference discovery: one CANONICAL MINIMUM-LEG
/// built-asset itinerary per explicitly authorized single-technology intent.
/// For equal leg counts it chooses lexicographically smallest asset-ID path.
///
/// This is reachability/candidate generation, NOT lowest commercial cost
/// routing, a global multimodal pathfinder or full physical travel simulation.
/// A cheaper longer path can exist; caller-defined price functions govern
/// actual freight and remain separate from this hop-minimal reference.
/// </summary>
public sealed class EconomicFineTransportServiceItineraryDiscovery
{
    public IReadOnlyList<EconomicTransportServiceOffer> Discover(
        StrategicSurfaceGraph fineGeography,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> finePointAnchors,
        StrategicEdgeAccessSnapshot access,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<EconomicFineTransportServiceIntent> serviceIntents,
        IEconomicFineTransportItineraryPricing pricing,
        EconomicFineTransportPathCache? physicalPathCache = null,
        EconomicFineTransportPathReuseDiagnostics? reuseDiagnostics = null)
    {
        reuseDiagnostics?.Reset();
        ArgumentNullException.ThrowIfNull(fineGeography);
        ArgumentNullException.ThrowIfNull(finePointAnchors);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(serviceIntents);
        ArgumentNullException.ThrowIfNull(pricing);

        var resolution = fineGeography.StrategicTopology.Parameters;
        var anchors = new Dictionary<EconomicPointId, int>();
        foreach (var (point, tile) in finePointAnchors)
        {
            if (!point.IsValid || !tile.IsValid ||
                tile.FineGoldbergParameters != resolution)
                throw new ArgumentException(
                    "Economic anchor belongs to a different fine geography.",
                    nameof(finePointAnchors));
            anchors.Add(point, fineGeography.GetNodeIndex(tile.FineStrategicCellId));
        }

        var edges = fineGeography.StrategicTopology.Edges.ToDictionary(x => x.Id);
        foreach (var closed in access.ClosedEdgeIds)
            if (!edges.ContainsKey(closed))
                throw new ArgumentException(
                    "Closed edge is foreign to the fine geography.", nameof(access));

        var assets = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        var adjacency = Enumerable.Range(0, fineGeography.NodeCount)
            .Select(_ => new List<Link>()).ToArray();
        foreach (var asset in installedAssets)
        {
            if (asset is null ||
                asset.FineGeographicEdgeId is not PhysicalTacticalEdgeId fineId ||
                fineId.FineGoldbergParameters != resolution ||
                !edges.TryGetValue(asset.GeographicEdgeId, out var edge) ||
                !assets.TryAdd(asset.AssetId, asset))
                throw new ArgumentException(
                    "Assets must be unique and explicitly built on this fine topology.",
                    nameof(installedAssets));

            if (!asset.IsOperational || !access.IsOpen(asset.GeographicEdgeId))
                continue;

            var first = fineGeography.GetNodeIndex(edge.IncidentCellIds[0]);
            var second = fineGeography.GetNodeIndex(edge.IncidentCellIds[1]);
            adjacency[first].Add(new Link(second, asset.AssetId,
                asset.TechnologyTypeId, asset.TechnologyCatalogVersion));
            adjacency[second].Add(new Link(first, asset.AssetId,
                asset.TechnologyTypeId, asset.TechnologyCatalogVersion));
        }

        foreach (var neighbors in adjacency)
            neighbors.Sort(static (a, b) =>
            {
                var order = a.AssetId.CompareTo(b.AssetId);
                return order != 0 ? order : a.NextNode.CompareTo(b.NextNode);
            });

        var intents = serviceIntents.ToArray();
        if (intents.Any(x => x is null) ||
            intents.Select(x => x!.ServiceId).Distinct().Count() != intents.Length)
            throw new ArgumentException(
                "Service intents must have unique non-null identities.",
                nameof(serviceIntents));

        foreach (var intent in intents)
            if (!anchors.ContainsKey(intent.RouteKey.SourceEconomicPointId) ||
                !anchors.ContainsKey(intent.RouteKey.DestinationEconomicPointId))
                throw new ArgumentException(
                    "Service intent is missing a fine economic point anchor.",
                    nameof(serviceIntents));

        // Validation above always runs, even on a cache hit. Graph identity
        // includes every geographic edge and its endpoints, so a changed
        // topological adjacency never borrows an old route by numeric ID.
        // Availability signatures are PER TECHNOLOGY: changes to another
        // mode's assets do not invalidate a physically independent service.
        var topologySignature = physicalPathCache is null ? string.Empty :
            BuildTopologySignature(resolution, edges);
        var availableSignatures = physicalPathCache is null
            ? null
            : assets.Values.Where(x =>
                    x.IsOperational && access.IsOpen(x.GeographicEdgeId))
                .GroupBy(x => (
                    x.TechnologyTypeId, x.TechnologyCatalogVersion))
                .ToDictionary(g => g.Key, g => string.Join(";", g
                    .OrderBy(x => x.AssetId)
                    .Select(x => x.AssetId + ":" + x.GeographicEdgeId.Value)));
        var committed = physicalPathCache is null
            ? null
            : new Dictionary<EconomicFineTransportPathCache.PathKey,
                EconomicFineTransportPathCache.CachedPhysicalPath>();
        var offers = new List<EconomicTransportServiceOffer>();
        foreach (var intent in intents.OrderBy(x => x.ServiceId))
        {
            if (!intent.IsAvailable)
                continue;

            var start = anchors[intent.RouteKey.SourceEconomicPointId];
            var end = anchors[intent.RouteKey.DestinationEconomicPointId];
            if (start == end)
                continue; // No phantom zero-leg transport service.

            var key = new EconomicFineTransportPathCache.PathKey(
                start, end, intent.TechnologyTypeId,
                intent.TechnologyCatalogVersion);
            var signature = availableSignatures is null ? string.Empty :
                availableSignatures.GetValueOrDefault(
                    (intent.TechnologyTypeId, intent.TechnologyCatalogVersion),
                    string.Empty);
            ulong[]? path;
            if (physicalPathCache is not null &&
                physicalPathCache.TryReuse(
                    key, topologySignature, signature, out var reused))
            {
                path = reused;
                if (reuseDiagnostics is not null)
                    reuseDiagnostics.ReusedPaths++;
            }
            else
            {
                path = DiscoverMinLegPath(
                    start, end, intent.TechnologyTypeId,
                    intent.TechnologyCatalogVersion, adjacency);
                if (reuseDiagnostics is not null)
                    reuseDiagnostics.SearchedPaths++;
            }
            if (path is null)
            {
                if (reuseDiagnostics is not null)
                    reuseDiagnostics.UnreachableIntents++;
                continue;
            }
            // Every cache entry must match the fully validated CURRENT
            // topology, per-technology constructed/operational/open assets
            // and the CURRENT endpoint anchors. No freight is cached.
            if (committed is not null)
                committed[key] = new EconomicFineTransportPathCache.CachedPhysicalPath(
                    topologySignature, signature, (ulong[])path.Clone());

            var itinerary = Array.AsReadOnly(
                path.Select(assetId => assets[assetId]).ToArray());
            var priceFunction = pricing.CreateForItinerary(intent, itinerary)
                ?? throw new InvalidOperationException(
                    "Connected service requires an explicit cost function.");

            offers.Add(new EconomicTransportServiceOffer(
                intent.ServiceId, intent.RouteKey, path,
                Array.Empty<int>(), intent.ExpectedLoadMicrograms,
                priceFunction));
        }
        // Commit only after all service offers and pricing factories have
        // successfully completed. An exception must not publish half a cache.
        if (physicalPathCache is not null && committed is not null)
            physicalPathCache.Replace(committed);
        return Array.AsReadOnly(offers.ToArray());
    }

    private static string BuildTopologySignature(
        GoldbergParameters resolution,
        IReadOnlyDictionary<StrategicEdgeId, StrategicEdge> edges)
    {
        var sb = new StringBuilder();
        sb.Append(resolution.M).Append(':').Append(resolution.N).Append('|');
        foreach (var edge in edges.Values.OrderBy(x => x.Id.Value))
        {
            sb.Append(edge.Id.Value).Append(':');
            foreach (var cell in edge.IncidentCellIds.OrderBy(x => x.Value))
                sb.Append(cell.Value).Append(',');
            sb.Append(';');
        }
        return sb.ToString();
    }

    private static ulong[]? DiscoverMinLegPath(
        int start, int end, ulong technologyTypeId, ulong catalogVersion,
        IReadOnlyList<List<Link>> adjacency)
    {
        var parents = Enumerable.Repeat(-1, adjacency.Count).ToArray();
        var parentAssets = new ulong[adjacency.Count];
        var visited = new bool[adjacency.Count];
        var queue = new Queue<int>();
        visited[start] = true;
        queue.Enqueue(start);

        while (queue.Count > 0 && !visited[end])
        {
            var current = queue.Dequeue();
            foreach (var link in adjacency[current])
            {
                if (link.TechnologyTypeId != technologyTypeId ||
                    link.TechnologyCatalogVersion != catalogVersion ||
                    visited[link.NextNode])
                    continue;

                visited[link.NextNode] = true;
                parents[link.NextNode] = current;
                parentAssets[link.NextNode] = link.AssetId;
                queue.Enqueue(link.NextNode);
                if (link.NextNode == end)
                    break;
            }
        }

        if (!visited[end])
            return null;

        var reversed = new List<ulong>();
        for (var node = end; node != start; node = parents[node])
        {
            if (parents[node] < 0 || parentAssets[node] == 0UL)
                throw new InvalidOperationException(
                    "Discovered service has a broken predecessor chain.");
            reversed.Add(parentAssets[node]);
        }
        reversed.Reverse();
        return reversed.ToArray();
    }

    private readonly record struct Link(
        int NextNode, ulong AssetId,
        ulong TechnologyTypeId, ulong TechnologyCatalogVersion);
}
