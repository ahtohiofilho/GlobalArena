namespace GlobalArena.Simulation;

/// <summary>
/// Caller-owned and opt-in cache of PHYSICAL fine-grid asset paths only.
/// Stores no freight quotes, pricing functions, forecasts, market values or
/// trades. Never part of authoritative WorldState. Not thread-safe.
/// </summary>
public sealed class EconomicFineTransportPathCache
{
    private Dictionary<PathKey, CachedPhysicalPath> _prior = new();

    public int CachedPathCount => _prior.Count;

    internal bool TryReuse(
        PathKey key, string topologySignature,
        IReadOnlyList<AvailableAsset> currentAssets, out ulong[] path)
    {
        path = Array.Empty<ulong>();
        if (!_prior.TryGetValue(key, out var cached) ||
            !string.Equals(cached.TopologySignature, topologySignature,
                StringComparison.Ordinal))
            return false;

        var former = cached.AvailableAssets;
        var a = 0;
        var b = 0;
        // Both sequences are already sorted by the globally unique
        // physical asset ID. An exact match, or a DELETION-ONLY subset,
        // cannot create a new better shortest path. An asset replacement,
        // installation, corridor relocation or new technology is not a
        // subset and must invalidate the old route.
        while (b < currentAssets.Count)
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

        // Even for a deletion-only graph change, the chosen old path must
        // survive wholly. An off-path closure is provably harmless to the
        // canonical min-hop + asset-ID tie-break, not to freight PRICING.
        foreach (var assetId in cached.AssetIds)
        {
            var lo = 0;
            var hi = currentAssets.Count - 1;
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
        Dictionary<PathKey, CachedPhysicalPath> committed) =>
        _prior = committed;

    internal readonly record struct PathKey(
        ulong StartCellId, ulong EndCellId,
        ulong TechnologyTypeId, ulong TechnologyCatalogVersion);

    internal readonly record struct AvailableAsset(
        ulong AssetId, ulong GeographicEdgeId);

    internal sealed record CachedPhysicalPath(
        string TopologySignature, AvailableAsset[] AvailableAssets,
        ulong[] AssetIds);
}

/// <summary>
/// Observational only; these counts cannot alter canonical market results.
/// Physical reuse still requires a new pricing function and new quote.
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
