using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class StrategicEconomicRouteResolution
{
    private readonly StrategicEdgeId[] _searchDependencyEdgeIds;

    private readonly IReadOnlyList<StrategicEdgeId> _readOnlySearchDependencyEdgeIds;

    public bool IsReachable { get; }

    public StrategicEconomicRoute? Route { get; }

    public IReadOnlyList<StrategicEdgeId> SearchDependencyEdgeIds =>
        _readOnlySearchDependencyEdgeIds;

    public StrategicEconomicRouteResolution(
        bool isReachable,
        StrategicEconomicRoute? route,
        IEnumerable<StrategicEdgeId> searchDependencyEdgeIds)
    {
        if (isReachable && route is null)
        {
            throw new ArgumentException(
                "Reachable route resolution requires a route.",
                nameof(route));
        }

        if (!isReachable && route is not null)
        {
            throw new ArgumentException(
                "Unreachable route resolution cannot contain a route.",
                nameof(route));
        }

        ArgumentNullException.ThrowIfNull(
            searchDependencyEdgeIds);

        var canonical =
            searchDependencyEdgeIds.ToArray();

        if (canonical.Any(edgeId => !edgeId.IsValid))
        {
            throw new ArgumentException(
                "Search dependencies must contain valid StrategicEdge identities.",
                nameof(searchDependencyEdgeIds));
        }

        canonical =
            canonical
                .OrderBy(edgeId => edgeId.Value)
                .ToArray();

        for (var index = 1; index < canonical.Length; index++)
        {
            if (canonical[index - 1] == canonical[index])
            {
                throw new ArgumentException(
                    "Search dependencies must be unique.",
                    nameof(searchDependencyEdgeIds));
            }
        }

        IsReachable = isReachable;
        Route = route;

        _searchDependencyEdgeIds = canonical;
        _readOnlySearchDependencyEdgeIds =
            Array.AsReadOnly(_searchDependencyEdgeIds);
    }
}
