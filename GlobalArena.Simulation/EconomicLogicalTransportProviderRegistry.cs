using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Declarative binding between ONE versioned technology and ONE versioned
/// traversal capability. This is not a named transport-mode enum, tariff,
/// physical path or permission to load cargo. Provider behavior remains code.
/// </summary>
public sealed class EconomicLogicalTransportTechnologyDefinition
{
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public ulong ProviderId { get; }
    public ulong ProviderVersion { get; }

    public EconomicLogicalTransportTechnologyDefinition(
        ulong technologyTypeId, ulong technologyCatalogVersion,
        ulong providerId, ulong providerVersion)
    {
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        if (providerId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(providerId));
        if (providerVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(providerVersion));
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        ProviderId = providerId;
        ProviderVersion = providerVersion;
    }
}

/// <summary>
/// Immutable-in-membership registry of caller-injected provider objects and
/// versioned technology definitions. Unknown technologies fail closed.
/// This is opt-in and NOT an authoritative persisted technology catalogue.
/// </summary>
public sealed class EconomicLogicalTransportProviderRegistry
{
    private readonly Dictionary<(ulong, ulong),
        EconomicLogicalTransportTechnologyDefinition> _technologies = new();
    private readonly Dictionary<(ulong, ulong),
        IEconomicLogicalTransportTraversalProvider> _providers = new();

    public EconomicLogicalTransportProviderRegistry(
        IEnumerable<EconomicLogicalTransportTechnologyDefinition> technologies,
        IEnumerable<IEconomicLogicalTransportTraversalProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var provider in providers)
        {
            if (provider is null || provider.ProviderId == 0UL ||
                provider.ProviderVersion == 0UL ||
                !_providers.TryAdd(
                    (provider.ProviderId, provider.ProviderVersion), provider))
                throw new ArgumentException(
                    "Traversal providers require unique positive identity/version.",
                    nameof(providers));
        }

        foreach (var definition in technologies)
        {
            if (definition is null ||
                !_technologies.TryAdd(
                    (definition.TechnologyTypeId,
                     definition.TechnologyCatalogVersion), definition))
                throw new ArgumentException(
                    "Technology definitions must have unique versioned identities.",
                    nameof(technologies));
            if (!_providers.ContainsKey(
                (definition.ProviderId, definition.ProviderVersion)))
                throw new ArgumentException(
                    "Each technology must bind to a registered versioned provider.",
                    nameof(technologies));
        }
    }

    internal (EconomicLogicalTransportTechnologyDefinition Definition,
        IEconomicLogicalTransportTraversalProvider Provider) Require(
            EconomicLogicalTransportServiceIntent intent)
    {
        if (!_technologies.TryGetValue(
            (intent.TechnologyTypeId, intent.TechnologyCatalogVersion),
            out var definition))
            throw new InvalidOperationException(
                "No registered transport capability for the requested technology.");
        var provider = _providers[
            (definition.ProviderId, definition.ProviderVersion)];
        if (provider.ProviderId != definition.ProviderId ||
            provider.ProviderVersion != definition.ProviderVersion)
            throw new InvalidOperationException(
                "Registered traversal provider identity changed.");
        return (definition, provider);
    }
}

/// <summary>
/// An explicit, snapshot-local authorization of a physical/logical link by
/// the provider responsible for it. Supplying this attestation does not
/// imply an actual bridge, road, port, tariff, cargo terminal or free mode
/// transfer. World-snapshot producers must establish physical legitimacy.
/// </summary>
public sealed class EconomicLogicalTransportAuthorizedLink
{
    public ulong ProviderId { get; }
    public ulong ProviderVersion { get; }
    public EconomicTransportLogicalLink Link { get; }

    public EconomicLogicalTransportAuthorizedLink(
        ulong providerId, ulong providerVersion,
        EconomicTransportLogicalLink link)
    {
        if (providerId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(providerId));
        if (providerVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(providerVersion));
        Link = link ?? throw new ArgumentNullException(nameof(link));
        ProviderId = providerId;
        ProviderVersion = providerVersion;
    }
}

/// <summary>
/// Explicit immutable *logical* economic point anchor in this snapshot.
/// A node is NOT automatically a commercial facility: the anchor merely
/// binds a DECLARED service endpoint to one network node. Future physical
/// load/discharge authorization must remain separate.
/// </summary>
public sealed class EconomicLogicalTransportPointAnchor
{
    public EconomicPointId EconomicPointId { get; }
    public EconomicTransportLogicalNodeId NodeId { get; }

    public EconomicLogicalTransportPointAnchor(
        EconomicPointId economicPointId,
        EconomicTransportLogicalNodeId nodeId)
    {
        if (!economicPointId.IsValid || !nodeId.IsValid)
            throw new ArgumentException(
                "Commercial endpoint anchor requires valid point and node.");
        EconomicPointId = economicPointId;
        NodeId = nodeId;
    }
}

/// <summary>
/// Immutable non-authoritative post-movement authorization snapshot.
/// Every service endpoint must be explicitly anchored and every provider
/// link must match the authorized exact ID, endpoints, technology/version,
/// and operational state. No adjacency, economic stop or infrastructure
/// is synthesized from the node graph.
/// </summary>
public sealed class EconomicLogicalTransportAuthorizationSnapshot
{
    private readonly Dictionary<EconomicPointId, EconomicTransportLogicalNodeId>
        _pointAnchors = new();
    private readonly Dictionary<(ulong ProviderId, ulong ProviderVersion, ulong LinkId),
        EconomicTransportLogicalLink> _links = new();

    public ulong SnapshotOrdinal { get; }

    public EconomicLogicalTransportAuthorizationSnapshot(
        ulong snapshotOrdinal,
        IEnumerable<EconomicLogicalTransportPointAnchor> pointAnchors,
        IEnumerable<EconomicLogicalTransportAuthorizedLink> authorizedLinks)
    {
        if (snapshotOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(snapshotOrdinal));
        ArgumentNullException.ThrowIfNull(pointAnchors);
        ArgumentNullException.ThrowIfNull(authorizedLinks);
        SnapshotOrdinal = snapshotOrdinal;

        foreach (var anchor in pointAnchors)
            if (anchor is null ||
                !_pointAnchors.TryAdd(anchor.EconomicPointId, anchor.NodeId))
                throw new ArgumentException(
                    "Commercial point anchors must be unique and valid.",
                    nameof(pointAnchors));

        foreach (var allowed in authorizedLinks)
            if (allowed is null ||
                !_links.TryAdd(
                    (allowed.ProviderId, allowed.ProviderVersion,
                     allowed.Link.LinkId), allowed.Link))
                throw new ArgumentException(
                    "Authorized provider/link identities must be unique.",
                    nameof(authorizedLinks));
    }

    internal void RequireEndpoints(EconomicLogicalTransportServiceIntent intent)
    {
        if (!_pointAnchors.TryGetValue(
                intent.RouteKey.SourceEconomicPointId, out var source) ||
            !_pointAnchors.TryGetValue(
                intent.RouteKey.DestinationEconomicPointId, out var destination) ||
            source != intent.StartNode || destination != intent.EndNode)
            throw new InvalidOperationException(
                "Commercial endpoints lack matching explicit snapshot anchors.");
    }

    internal void RequireAuthorizedLink(
        ulong providerId, ulong providerVersion,
        EconomicTransportLogicalLink link)
    {
        if (!_links.TryGetValue(
            (providerId, providerVersion, link.LinkId), out var current) ||
            !current.IsOperational || !link.IsOperational ||
            current.TechnologyTypeId != link.TechnologyTypeId ||
            current.TechnologyCatalogVersion != link.TechnologyCatalogVersion ||
            !((current.FirstNode == link.FirstNode &&
               current.SecondNode == link.SecondNode) ||
              (current.FirstNode == link.SecondNode &&
               current.SecondNode == link.FirstNode)))
            throw new InvalidOperationException(
                "Itinerary link does not match the authorized physical snapshot.");
    }
}

/// <summary>
/// Quote and complete provenance with immutable snapshot ordinal. The
/// economic market still consumes the unchanged EconomicTurnFreightQuote.
/// </summary>
public sealed class EconomicLogicalRegisteredServiceSelection
{
    public ulong AuthorizationSnapshotOrdinal { get; }
    public EconomicLogicalTransportServiceSelection Selection { get; }
    public EconomicTurnFreightQuote Quote => Selection.Quote;

    internal EconomicLogicalRegisteredServiceSelection(
        ulong snapshotOrdinal, EconomicLogicalTransportServiceSelection selection)
    {
        AuthorizationSnapshotOrdinal = snapshotOrdinal;
        Selection = selection;
    }
}

/// <summary>
/// Strict opt-in entrypoint for registered, snapshot-authorized traversal.
/// It invokes the existing exact quote adapter per declared service, using
/// a guarded provider that rejects unapproved links BEFORE calling pricing.
/// Every eligible offer is still quoted ONCE; the cheapest offer per
/// economic point pair wins by exact rational cost, then ServiceId.
/// </summary>
public sealed class EconomicRegisteredLogicalTransportQuoteResolver
{
    public IReadOnlyList<EconomicLogicalRegisteredServiceSelection> Resolve(
        ulong turnOrdinal,
        EconomicLogicalTransportProviderRegistry registry,
        EconomicLogicalTransportAuthorizationSnapshot snapshot,
        IEnumerable<EconomicLogicalTransportServiceIntent> serviceIntents,
        IEconomicLogicalTransportItineraryPricing pricing)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(serviceIntents);
        ArgumentNullException.ThrowIfNull(pricing);
        var intents = serviceIntents.ToArray();
        if (intents.Any(x => x is null) ||
            intents.Select(x => x!.ServiceId).Distinct().Count() != intents.Length)
            throw new ArgumentException(
                "Commercial service identities must be unique.", nameof(serviceIntents));

        // Fail closed on unknown technologies/endpoints even when no
        // matching physical route happens to be found. No partial return.
        var registrations = intents
            .Select(intent =>
            {
                var registration = registry.Require(intent);
                snapshot.RequireEndpoints(intent);
                return (Intent: intent, registration.Provider,
                    registration.Definition);
            }).OrderBy(x => x.Intent.ServiceId).ToArray();

        var chosen = new Dictionary<StrategicEconomicRouteKey,
            EconomicLogicalRegisteredServiceSelection>();
        var quoter = new EconomicLogicalTransportQuoteResolver();
        foreach (var (intent, provider, definition) in registrations)
        {
            if (!intent.IsAvailable)
                continue;
            var guarded = new AuthorizedTraversal(provider, snapshot, definition);
            var one = quoter.Resolve(
                turnOrdinal, guarded, new[] { intent }, pricing);
            if (one.Count == 0)
                continue;

            var candidate = new EconomicLogicalRegisteredServiceSelection(
                snapshot.SnapshotOrdinal, one[0]);
            if (!chosen.TryGetValue(intent.RouteKey, out var current) ||
                candidate.Quote.RawUnitCostPerGram.CompareTo(
                    current.Quote.RawUnitCostPerGram) < 0 ||
                (candidate.Quote.RawUnitCostPerGram.CompareTo(
                    current.Quote.RawUnitCostPerGram) == 0 &&
                 candidate.Selection.ServiceId < current.Selection.ServiceId))
                chosen[intent.RouteKey] = candidate;
        }
        return Array.AsReadOnly(chosen.Values
            .OrderBy(x => x.Quote.RouteKey.SourceEconomicPointId.Value)
            .ThenBy(x => x.Quote.RouteKey.DestinationEconomicPointId.Value)
            .ToArray());
    }

    private sealed class AuthorizedTraversal(
        IEconomicLogicalTransportTraversalProvider provider,
        EconomicLogicalTransportAuthorizationSnapshot snapshot,
        EconomicLogicalTransportTechnologyDefinition definition)
        : IEconomicLogicalTransportTraversalProvider
    {
        public ulong ProviderId => definition.ProviderId;
        public ulong ProviderVersion => definition.ProviderVersion;

        public IReadOnlyList<EconomicTransportLogicalLink>? FindItinerary(
            EconomicLogicalTransportServiceIntent intent)
        {
            if (provider.ProviderId != ProviderId ||
                provider.ProviderVersion != ProviderVersion)
                throw new InvalidOperationException(
                    "Registered traversal provider identity changed during discovery.");
            var proposed = provider.FindItinerary(intent);
            if (proposed is null)
                return null;

            var links = proposed.ToArray();
            foreach (var link in links)
            {
                if (link is null)
                    throw new InvalidOperationException(
                        "Registered traversal provider supplied a null link.");
                snapshot.RequireAuthorizedLink(ProviderId, ProviderVersion, link);
            }
            return Array.AsReadOnly(links);
        }
    }
}
