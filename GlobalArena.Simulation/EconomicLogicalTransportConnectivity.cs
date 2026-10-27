using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Node identity in an EXPLICIT transport connectivity graph, independent
/// of Goldberg cell borders or a geographical centroid. A line can end at
/// a node shared with any number of other line segments; this is not a
/// loading terminal, station, port, transfer authorization or cargo stop.
/// </summary>
public readonly record struct EconomicTransportLogicalNodeId
{
    private readonly ulong _value;
    public bool IsValid => _value != 0UL;
    public ulong Value => IsValid ? _value :
        throw new InvalidOperationException("Default logical node is invalid.");

    public EconomicTransportLogicalNodeId(ulong value)
    {
        if (value == 0UL)
            throw new ArgumentOutOfRangeException(nameof(value));
        _value = value;
    }
}

/// <summary>
/// One explicit logical link between two transport nodes. Multiple links,
/// including parallel lines, may meet at either node. The link is NOT a
/// Goldberg BORDER and carries no implicit cargo handling rights.
/// A provider decides which links physically exist and are available;
/// the generic contract does not require built cell-border crossings.
/// </summary>
public sealed class EconomicTransportLogicalLink
{
    public ulong LinkId { get; }
    public EconomicTransportLogicalNodeId FirstNode { get; }
    public EconomicTransportLogicalNodeId SecondNode { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public bool IsOperational { get; }

    public EconomicTransportLogicalLink(
        ulong linkId,
        EconomicTransportLogicalNodeId firstNode,
        EconomicTransportLogicalNodeId secondNode,
        ulong technologyTypeId,
        ulong technologyCatalogVersion,
        bool isOperational = true)
    {
        if (linkId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(linkId));
        if (!firstNode.IsValid || !secondNode.IsValid ||
            firstNode == secondNode)
            throw new ArgumentException(
                "A logical link must join two distinct valid nodes.",
                nameof(secondNode));
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        LinkId = linkId;
        FirstNode = firstNode;
        SecondNode = secondNode;
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        IsOperational = isOperational;
    }
}

/// <summary>
/// An EXPLICIT commercial service intent at two logical network nodes.
/// Intermediate connections are pure transit: they confer no stop, loading,
/// unloading, technology handoff, station or economic demand entitlement.
/// Operations/facilities will require separate verified contracts.
/// </summary>
public sealed class EconomicLogicalTransportServiceIntent
{
    public ulong ServiceId { get; }
    public StrategicEconomicRouteKey RouteKey { get; }
    public EconomicTransportLogicalNodeId StartNode { get; }
    public EconomicTransportLogicalNodeId EndNode { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public BigInteger ExpectedLoadMicrograms { get; }
    public bool IsAvailable { get; }

    public EconomicLogicalTransportServiceIntent(
        ulong serviceId,
        StrategicEconomicRouteKey routeKey,
        EconomicTransportLogicalNodeId startNode,
        EconomicTransportLogicalNodeId endNode,
        ulong technologyTypeId,
        ulong technologyCatalogVersion,
        BigInteger expectedLoadMicrograms,
        bool isAvailable = true)
    {
        if (serviceId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(serviceId));
        if (!routeKey.SourceEconomicPointId.IsValid ||
            !routeKey.DestinationEconomicPointId.IsValid)
            throw new ArgumentException("Invalid commercial endpoints.", nameof(routeKey));
        if (!startNode.IsValid || !endNode.IsValid || startNode == endNode)
            throw new ArgumentException(
                "A service needs two distinct valid logical endpoints.",
                nameof(endNode));
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        if (expectedLoadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));
        ServiceId = serviceId;
        RouteKey = routeKey;
        StartNode = startNode;
        EndNode = endNode;
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        ExpectedLoadMicrograms = expectedLoadMicrograms;
        IsAvailable = isAvailable;
    }
}

/// <summary>
/// Explicit opt-in TRAVERSAL provider. Its versioned implementation owns
/// physical validity and available links for its own domain (constructed
/// lines, navigable space, or future extensions). Null means unreachable.
/// This seam neither introduces modes in an enum nor infers any free path.
/// Returned links must form one contiguous, simple, technology-compatible
/// route; the independent economic quote adapter validates that contract.
/// </summary>
public interface IEconomicLogicalTransportTraversalProvider
{
    ulong ProviderId { get; }
    ulong ProviderVersion { get; }

    IReadOnlyList<EconomicTransportLogicalLink>? FindItinerary(
        EconomicLogicalTransportServiceIntent intent);
}

/// <summary>
/// Complete itinerary price is supplied by the caller. It can be nonadditive
/// and is evaluated ONCE at the cycle's forecast, never from live trades.
/// </summary>
public interface IEconomicLogicalTransportItineraryPricing
{
    IEconomicTurnFreightCostFunction CreateForItinerary(
        EconomicLogicalTransportServiceIntent intent,
        IReadOnlyList<EconomicTransportLogicalLink> orderedLinks);
}

/// <summary>
/// Non-authoritative reference provider for explicit installed line links.
/// A->B and B->C naturally form A->C when B is the SAME logical node.
/// Any number of links may meet at B. Only matching technology AND catalogue
/// version may be chained, with no inferred transshipment or free links.
/// Chooses the fewest-link itinerary, tie-breaking by ordered link IDs.
/// This is not a minimum-whole-service-freight optimizer.
/// </summary>
public sealed class EconomicConstructedLogicalLineProvider :
    IEconomicLogicalTransportTraversalProvider
{
    private readonly Dictionary<ulong,
        List<(EconomicTransportLogicalNodeId Next, EconomicTransportLogicalLink Link)>>
        _adjacency = new();

    public ulong ProviderId { get; }
    public ulong ProviderVersion { get; }

    public EconomicConstructedLogicalLineProvider(
        ulong providerId, ulong providerVersion,
        IEnumerable<EconomicTransportLogicalLink> links)
    {
        if (providerId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(providerId));
        if (providerVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(providerVersion));
        ArgumentNullException.ThrowIfNull(links);
        ProviderId = providerId;
        ProviderVersion = providerVersion;
        var ids = new HashSet<ulong>();
        foreach (var link in links)
        {
            if (link is null || !ids.Add(link.LinkId))
                throw new ArgumentException(
                    "Logical links require unique, non-null identities.",
                    nameof(links));
            if (!link.IsOperational)
                continue;

            void Add(EconomicTransportLogicalNodeId node,
                EconomicTransportLogicalNodeId next)
            {
                if (!_adjacency.TryGetValue(node.Value, out var list))
                    _adjacency[node.Value] = list = new();
                list.Add((next, link));
            }
            Add(link.FirstNode, link.SecondNode);
            Add(link.SecondNode, link.FirstNode);
        }

        foreach (var adjacent in _adjacency.Values)
            adjacent.Sort(static (a, b) =>
            {
                var ordered = a.Link.LinkId.CompareTo(b.Link.LinkId);
                return ordered == 0 ? a.Next.Value.CompareTo(b.Next.Value) : ordered;
            });
    }

    public IReadOnlyList<EconomicTransportLogicalLink>? FindItinerary(
        EconomicLogicalTransportServiceIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!intent.IsAvailable)
            return null;
        var queue = new Queue<EconomicTransportLogicalNodeId>();
        var previous = new Dictionary<ulong,
            (EconomicTransportLogicalNodeId Parent, EconomicTransportLogicalLink Link)>();
        var visited = new HashSet<ulong> { intent.StartNode.Value };
        queue.Enqueue(intent.StartNode);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == intent.EndNode)
            {
                var reversed = new List<EconomicTransportLogicalLink>();
                var cursor = node;
                while (cursor != intent.StartNode)
                {
                    var step = previous[cursor.Value];
                    reversed.Add(step.Link);
                    cursor = step.Parent;
                }
                reversed.Reverse();
                return Array.AsReadOnly(reversed.ToArray());
            }

            if (!_adjacency.TryGetValue(node.Value, out var neighbors))
                continue;
            foreach (var (next, link) in neighbors)
            {
                if (link.TechnologyTypeId != intent.TechnologyTypeId ||
                    link.TechnologyCatalogVersion != intent.TechnologyCatalogVersion ||
                    !visited.Add(next.Value))
                    continue;
                previous.Add(next.Value, (node, link));
                queue.Enqueue(next);
            }
        }
        return null;
    }
}
