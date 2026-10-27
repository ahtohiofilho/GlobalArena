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
                StringComparison.Ordinal))
        {
            if (string.Equals(cached.AvailableAssetSignature,
                    availableAssetSignature, StringComparison.Ordinal))
            {
                path = (ulong[])cached.AssetIds.Clone();
                return true;
            }

            // A pure REMOVAL of eligible physical edges cannot introduce a
            // shorter or lexicographically better path. If every asset on
            // the previous chosen path survives unchanged, the previous
            // canonical min-hop path is still exactly optimal. An addition,
            // edge relocation, version change or replacement may improve
            // the route, so those changes always require fresh search.
            var former = cached.AvailableAssetSignature.Split(
                ';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(
                    StringComparer.Ordinal);
            var current = availableAssetSignature.Length == 0
                ? Array.Empty<string>()
                : availableAssetSignature.Split(
                    ';', StringSplitOptions.RemoveEmptyEntries);
            var onlyRemoved = current.Length <= former.Count &&
                current.All(former.Contains);
            if (onlyRemoved && cached.AssetIds.All(id => current.Any(
                    x => x.StartsWith(id.ToString() + ":",
                        StringComparison.Ordinal))))
            {
                path = (ulong[])cached.AssetIds.Clone();
                return true;
            }
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
