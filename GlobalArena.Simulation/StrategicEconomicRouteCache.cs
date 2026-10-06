using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class StrategicEconomicRouteCacheLookup
{
    public StrategicEconomicRouteResolution Resolution { get; }

    public bool WasCacheHit { get; }

    public bool IsReachable =>
        Resolution.IsReachable;

    public StrategicEconomicRoute? Route =>
        Resolution.Route;

    public IReadOnlyList<StrategicEdgeId> SearchDependencyEdgeIds =>
        Resolution.SearchDependencyEdgeIds;

    public StrategicEconomicRouteCacheLookup(
        StrategicEconomicRouteResolution resolution,
        bool wasCacheHit)
    {
        ArgumentNullException.ThrowIfNull(
            resolution);

        Resolution = resolution;
        WasCacheHit = wasCacheHit;
    }
}

public sealed class StrategicEconomicRouteCache
{
    private readonly StrategicEconomicRouteResolver _resolver;

    private StrategicEdgeAccessSnapshot _accessSnapshot;

    private readonly Dictionary<StrategicEconomicRouteKey, CacheEntry> _entries =
        new();

    private readonly Dictionary<StrategicEdgeId, HashSet<StrategicEconomicRouteKey>> _keysByDependencyEdge =
        new();

    public StrategicEdgeAccessSnapshot AccessSnapshot =>
        _accessSnapshot;

    public int Count =>
        _entries.Count;

    public StrategicEconomicRouteCache(
        StrategicEconomicRouteResolver resolver,
        StrategicEdgeAccessSnapshot accessSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            resolver);

        ArgumentNullException.ThrowIfNull(
            accessSnapshot);

        resolver.ValidateAccessSnapshot(
            accessSnapshot);

        _resolver = resolver;
        _accessSnapshot = accessSnapshot;
    }

    public StrategicEconomicRouteCacheLookup Resolve(
        EconomyRuntimeState economy,
        StrategicEconomicRouteKey key)
    {
        ArgumentNullException.ThrowIfNull(
            economy);

        if (!key.SourceEconomicPointId.IsValid
            || !key.DestinationEconomicPointId.IsValid)
        {
            throw new ArgumentException(
                "Route cache key must contain valid EconomicPoint identities.",
                nameof(key));
        }

        EconomicPointRuntimeState sourcePoint;
        EconomicPointRuntimeState destinationPoint;

        try
        {
            sourcePoint =
                FindPoint(
                    economy,
                    key.SourceEconomicPointId,
                    "Source");

            destinationPoint =
                FindPoint(
                    economy,
                    key.DestinationEconomicPointId,
                    "Destination");
        }
        catch
        {
            Evict(
                key);

            throw;
        }

        if (_entries.TryGetValue(
            key,
            out var cached))
        {
            if (cached.SourceCellId == sourcePoint.StrategicCellId
                && cached.DestinationCellId == destinationPoint.StrategicCellId)
            {
                return new StrategicEconomicRouteCacheLookup(
                    cached.Resolution,
                    true);
            }

            Evict(
                key);
        }

        var resolution =
            _resolver.ResolveDetailed(
                economy,
                key,
                _accessSnapshot);

        Insert(
            key,
            sourcePoint.StrategicCellId,
            destinationPoint.StrategicCellId,
            resolution);

        return new StrategicEconomicRouteCacheLookup(
            resolution,
            false);
    }

    public IReadOnlyList<StrategicEconomicRouteKey> UpdateAccessSnapshot(
        StrategicEdgeAccessSnapshot accessSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            accessSnapshot);

        _resolver.ValidateAccessSnapshot(
            accessSnapshot);

        var previous =
            new HashSet<StrategicEdgeId>(
                _accessSnapshot.ClosedEdgeIds);

        var next =
            new HashSet<StrategicEdgeId>(
                accessSnapshot.ClosedEdgeIds);

        var changed =
            previous
                .Concat(next)
                .Distinct()
                .Where(edgeId =>
                    previous.Contains(edgeId)
                    != next.Contains(edgeId))
                .OrderBy(edgeId => edgeId.Value)
                .ToArray();

        var invalidated =
            InvalidateDependenciesCore(
                changed);

        _accessSnapshot =
            accessSnapshot;

        return invalidated;
    }

    public IReadOnlyList<StrategicEconomicRouteKey> InvalidateDependencies(
        IEnumerable<StrategicEdgeId> changedEdgeIds)
    {
        ArgumentNullException.ThrowIfNull(
            changedEdgeIds);

        var supplied =
            changedEdgeIds.ToArray();

        if (supplied.Any(edgeId => !edgeId.IsValid))
        {
            throw new ArgumentException(
                "Changed dependencies must contain valid StrategicEdge identities.",
                nameof(changedEdgeIds));
        }

        var canonical =
            supplied
                .Distinct()
                .OrderBy(edgeId => edgeId.Value)
                .ToArray();

        var edgeCount =
            (ulong)_resolver
                .SurfaceGraph
                .StrategicTopology
                .Edges
                .Count;

        foreach (var edgeId in canonical)
        {
            if (edgeId.Value > edgeCount)
            {
                throw new KeyNotFoundException(
                    $"Strategic edge {edgeId.Value} does not belong to this surface graph.");
            }
        }

        return InvalidateDependenciesCore(
            canonical);
    }

    public IReadOnlyList<StrategicEconomicRouteKey> Clear()
    {
        var keys =
            CanonicalizeKeys(
                _entries.Keys);

        _entries.Clear();
        _keysByDependencyEdge.Clear();

        return keys;
    }

    private IReadOnlyList<StrategicEconomicRouteKey> InvalidateDependenciesCore(
        IReadOnlyList<StrategicEdgeId> changedEdgeIds)
    {
        var affected =
            new HashSet<StrategicEconomicRouteKey>();

        foreach (var edgeId in changedEdgeIds)
        {
            if (_keysByDependencyEdge.TryGetValue(
                edgeId,
                out var keys))
            {
                affected.UnionWith(
                    keys);
            }
        }

        var canonical =
            CanonicalizeKeys(
                affected);

        foreach (var key in canonical)
        {
            Evict(
                key);
        }

        return canonical;
    }

    private void Insert(
        StrategicEconomicRouteKey key,
        StrategicCellId sourceCellId,
        StrategicCellId destinationCellId,
        StrategicEconomicRouteResolution resolution)
    {
        Evict(
            key);

        _entries.Add(
            key,
            new CacheEntry(
                sourceCellId,
                destinationCellId,
                resolution));

        foreach (var edgeId in resolution.SearchDependencyEdgeIds)
        {
            if (!_keysByDependencyEdge.TryGetValue(
                edgeId,
                out var keys))
            {
                keys =
                    new HashSet<StrategicEconomicRouteKey>();

                _keysByDependencyEdge.Add(
                    edgeId,
                    keys);
            }

            keys.Add(
                key);
        }
    }

    private bool Evict(
        StrategicEconomicRouteKey key)
    {
        if (!_entries.Remove(
            key,
            out var entry))
        {
            return false;
        }

        foreach (var edgeId in entry.Resolution.SearchDependencyEdgeIds)
        {
            if (!_keysByDependencyEdge.TryGetValue(
                edgeId,
                out var keys)
                || !keys.Remove(key))
            {
                throw new InvalidOperationException(
                    "Route cache reverse dependency index is inconsistent.");
            }

            if (keys.Count == 0)
            {
                _keysByDependencyEdge.Remove(
                    edgeId);
            }
        }

        return true;
    }

    private static EconomicPointRuntimeState FindPoint(
        EconomyRuntimeState economy,
        EconomicPointId pointId,
        string role)
    {
        foreach (var point in economy.EconomicPoints)
        {
            if (point.Id == pointId)
            {
                return point;
            }
        }

        throw new KeyNotFoundException(
            $"{role} economic point {pointId.Value} does not exist in the Economy state.");
    }

    private static IReadOnlyList<StrategicEconomicRouteKey> CanonicalizeKeys(
        IEnumerable<StrategicEconomicRouteKey> keys)
    {
        return Array.AsReadOnly(
            keys
                .OrderBy(key => key.SourceEconomicPointId.Value)
                .ThenBy(key => key.DestinationEconomicPointId.Value)
                .ToArray());
    }

    private sealed class CacheEntry
    {
        public StrategicCellId SourceCellId { get; }

        public StrategicCellId DestinationCellId { get; }

        public StrategicEconomicRouteResolution Resolution { get; }

        public CacheEntry(
            StrategicCellId sourceCellId,
            StrategicCellId destinationCellId,
            StrategicEconomicRouteResolution resolution)
        {
            SourceCellId = sourceCellId;
            DestinationCellId = destinationCellId;
            Resolution = resolution;
        }
    }
}
