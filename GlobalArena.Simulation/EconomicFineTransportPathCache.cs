namespace GlobalArena.Simulation;

/// <summary>
/// Optional, caller-owned memory of PHYSICAL fine-grid itineraries only.
/// It holds neither freight quotes, priced functions, demand curves nor
/// financial trades. A successful discovery replaces its entire prior view.
/// Do not share across concurrent discoveries or authoritative WorldState.
/// </summary>
public sealed class EconomicFineTransportPathCache
{
    private Dictionary<PathKey, CachedPhysicalPath> _prior = new();

    public int CachedPathCount => _prior.Count;

    internal bool TryReuse(
        PathKey key, string topologySignature, string availableAssetSignature,
        out ulong[] path)
    {
        if (_prior.TryGetValue(key, out var cached) &&
            string.Equals(cached.TopologySignature, topologySignature,
                StringComparison.Ordinal) &&
            string.Equals(cached.AvailableAssetSignature,
                availableAssetSignature, StringComparison.Ordinal))
        {
            // Clone: neither the caller nor a service can change cached IDs.
            path = (ulong[])cached.AssetIds.Clone();
            return true;
        }
        path = Array.Empty<ulong>();
        return false;
    }

    internal void Replace(
        Dictionary<PathKey, CachedPhysicalPath> committed) =>
        _prior = committed;

    internal readonly record struct PathKey(
        ulong StartCellId, ulong EndCellId,
        ulong TechnologyTypeId, ulong TechnologyCatalogVersion);

    internal sealed record CachedPhysicalPath(
        string TopologySignature, string AvailableAssetSignature,
        ulong[] AssetIds);
}

/// <summary>
/// Observational data about exactly matching physical dependencies.
/// A reused path still generates a fresh function and turn-owned freight
/// quote; these counters never affect clearing or canonical state.
/// </summary>
public sealed class EconomicFineTransportPathReuseDiagnostics
{
    public int ReusedPaths { get; internal set; }
    public int SearchedPaths { get; internal set; }
    public int UnreachableIntents { get; internal set; }

    internal void Reset()
    {
        ReusedPaths = 0;
        SearchedPaths = 0;
        UnreachableIntents = 0;
    }
}
