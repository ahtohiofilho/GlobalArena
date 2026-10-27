using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-owned, opt-in cache of PHYSICAL fine-grid paths only. Stores no
/// quotes, forecasts, functions, market valuations or financial trades.
/// Not authoritative WorldState and not safe for concurrent use.
/// </summary>
public sealed class EconomicFineTransportPathCache
{
    private Dictionary<PathKey, CachedPhysicalPath> _prior = new();
    private string _previousTopology = string.Empty;
    private StrategicSurfaceGraph? _previousGraph;

    public int CachedPathCount => _prior.Count;

    // One full-topology comparison PER snapshot, not per market/service.
    internal bool IsSameImmutableGraph(StrategicSurfaceGraph graph) =>
        ReferenceEquals(_previousGraph, graph);

    internal string CurrentTopologySignature => _previousTopology;

    internal bool HasMatchingTopology(string topologySignature) =>
        string.Equals(_previousTopology, topologySignature,
            StringComparison.Ordinal);

    internal bool TryReuse(
        PathKey key, bool topologyMatches,
        AvailableAsset[] currentAssets, out ulong[] path)
    {
        path = Array.Empty<ulong>();
        if (!topologyMatches || !_prior.TryGetValue(key, out var cached))
            return false;

        // Current and prior arrays are sorted by unique asset IDs. A
        // deletion-only graph cannot create a shorter/lexically better route.
        var former = cached.AvailableAssets;
        var a = 0;
        var b = 0;
        while (b < currentAssets.Length)
        {
            if (a >= former.Length)
                return false;
            if (former[a].AssetId < currentAssets[b].AssetId)
            {
                a++;
                continue;
            }
            if (former[a] != currentAssets[b])
                return false;
            a++;
            b++;
        }

        // Even with pure removals, every previous chosen leg must survive.
        // Binary search avoids allocating a hash set for each market path.
        foreach (var assetId in cached.AssetIds)
        {
            var lo = 0;
            var hi = currentAssets.Length - 1;
            var found = false;
            while (lo <= hi)
            {
                var mid = lo + (hi - lo) / 2;
                var item = currentAssets[mid].AssetId;
                if (item == assetId)
                {
                    found = true;
                    break;
                }
                if (item < assetId)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
            if (!found)
                return false;
        }
        path = (ulong[])cached.AssetIds.Clone();
        return true;
    }

    internal void Replace(
        StrategicSurfaceGraph geography, string topologySignature,
        Dictionary<PathKey, CachedPhysicalPath> committed)
    {
        // A StrategicSurfaceGraph captures an immutable StrategicTopology
        // and readonly node/edge arrays; identical object identity can
        // therefore avoid repeating its content signature computation.
        _previousGraph = geography;
        _previousTopology = topologySignature;
        _prior = committed;
    }

    internal readonly record struct PathKey(
        ulong StartCellId, ulong EndCellId,
        ulong TechnologyTypeId, ulong TechnologyCatalogVersion);

    internal readonly record struct AvailableAsset(
        ulong AssetId, ulong GeographicEdgeId);

    internal sealed record CachedPhysicalPath(
        AvailableAsset[] AvailableAssets, ulong[] AssetIds);
}

/// <summary>
/// Non-authoritative diagnostics; physical reuse still creates fresh
/// whole-itinerary pricing functions and turn-owned freight quotes.
/// </summary>
public sealed class EconomicFineTransportPathReuseDiagnostics
{
    public int ReusedPaths { get; internal set; }
    public int SearchedPaths { get; internal set; }
    public int UnreachableIntents { get; internal set; }
    /// <summary>
    /// Number of full canonical source BFS trees actually constructed
    /// during this discovery; one can serve many destination intents.
    /// Pure diagnostics, independent of authoritative game state.
    /// </summary>
    public int SourceTreeBuilds { get; internal set; }

    internal void Reset()
    {
        ReusedPaths = 0;
        SearchedPaths = 0;
        UnreachableIntents = 0;
        SourceTreeBuilds = 0;
    }
}
