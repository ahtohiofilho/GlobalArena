using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable commercial quote with traversed LOGICAL node/link provenance.
/// This is not a station, port, cargo-terminal or physical alignment record;
/// only an explicitly offered SOURCE and DESTINATION may trade.
///
/// Its EconomicTurnFreightQuote is already compatible with the existing
/// transport-independent commodity market resolver.
/// </summary>
public sealed class EconomicLogicalTransportServiceSelection
{
    public ulong ServiceId { get; }
    public ulong ProviderId { get; }
    public ulong ProviderVersion { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public EconomicTurnFreightQuote Quote { get; }
    public IReadOnlyList<EconomicTransportLogicalNodeId> NodePath { get; }
    public IReadOnlyList<ulong> LinkIds { get; }

    internal EconomicLogicalTransportServiceSelection(
        EconomicLogicalTransportServiceIntent intent,
        IEconomicLogicalTransportTraversalProvider provider,
        EconomicTurnFreightQuote quote,
        IReadOnlyList<EconomicTransportLogicalNodeId> nodes,
        IReadOnlyList<EconomicTransportLogicalLink> links)
    {
        ServiceId = intent.ServiceId;
        ProviderId = provider.ProviderId;
        ProviderVersion = provider.ProviderVersion;
        TechnologyTypeId = intent.TechnologyTypeId;
        TechnologyCatalogVersion = intent.TechnologyCatalogVersion;
        Quote = quote;
        NodePath = Array.AsReadOnly(nodes.ToArray());
        LinkIds = Array.AsReadOnly(links.Select(link => link.LinkId).ToArray());
    }
}

/// <summary>
/// Provider-agnostic adapter from a declared logical commercial service
/// to a quote owned by one economic cycle. Does NOT use Goldberg border
/// identities or dispatch on a list of transport modes.
///
/// A provider asserts its domain-specific availability; this adapter
/// validates its route's logical continuity, unique link/node identities,
/// technology/version and declared endpoints, before asking for a
/// caller-defined WHOLE-itinerary cost function. The provider is an
/// explicitly trusted/registered capability, not an untrusted route source.
///
/// Every eligible service's price is evaluated once at its ex-ante load.
/// The cheapest declared service per directional commercial relation wins;
/// exact ties use ServiceId. No automatic intermediate loading, unloading,
/// transfer, connectivity across incompatible technologies, or modal tariff.
/// </summary>
public sealed class EconomicLogicalTransportQuoteResolver
{
    public IReadOnlyList<EconomicLogicalTransportServiceSelection> Resolve(
        ulong turnOrdinal,
        IEconomicLogicalTransportTraversalProvider traversal,
        IEnumerable<EconomicLogicalTransportServiceIntent> serviceIntents,
        IEconomicLogicalTransportItineraryPricing pricing)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(traversal);
        ArgumentNullException.ThrowIfNull(serviceIntents);
        ArgumentNullException.ThrowIfNull(pricing);
        if (traversal.ProviderId == 0UL || traversal.ProviderVersion == 0UL)
            throw new ArgumentException(
                "Traversal provider requires stable positive identity/version.",
                nameof(traversal));

        var intents = serviceIntents.ToArray();
        if (intents.Any(intent => intent is null) ||
            intents.Select(intent => intent!.ServiceId).Distinct().Count() !=
                intents.Length)
            throw new ArgumentException(
                "Commercial service intents require distinct valid identities.",
                nameof(serviceIntents));

        var best = new Dictionary<StrategicEconomicRouteKey,
            EconomicLogicalTransportServiceSelection>();
        foreach (var intent in intents.OrderBy(intent => intent.ServiceId))
        {
            if (!intent.IsAvailable)
                continue;
            var itinerary = traversal.FindItinerary(intent);
            if (itinerary is null)
                continue;

            // Copy once so provider mutation cannot reorder the priced path
            // after its logical continuity has been verified.
            var links = itinerary.ToArray();
            if (links.Length == 0)
                throw new InvalidOperationException(
                    "A reachable inter-node service must have at least one link.");
            var visitedLinks = new HashSet<ulong>();
            var visitedNodes = new HashSet<ulong> { intent.StartNode.Value };
            var nodes = new List<EconomicTransportLogicalNodeId>
            {
                intent.StartNode
            };
            var cursor = intent.StartNode;
            foreach (var link in links)
            {
                if (link is null || !link.IsOperational ||
                    link.TechnologyTypeId != intent.TechnologyTypeId ||
                    link.TechnologyCatalogVersion != intent.TechnologyCatalogVersion ||
                    !visitedLinks.Add(link.LinkId))
                    throw new InvalidOperationException(
                        "Traversal supplied unavailable, duplicate or " +
                        "technology-incompatible logical link.");

                EconomicTransportLogicalNodeId next;
                if (link.FirstNode == cursor)
                    next = link.SecondNode;
                else if (link.SecondNode == cursor)
                    next = link.FirstNode;
                else
                    throw new InvalidOperationException(
                        "A logical line must continue at the same endpoint node.");

                if (!visitedNodes.Add(next.Value))
                    throw new InvalidOperationException(
                        "A reference logical itinerary must be a simple node path.");
                nodes.Add(next);
                cursor = next;
            }

            if (cursor != intent.EndNode)
                throw new InvalidOperationException(
                    "Traversal must terminate at the declared endpoint.");

            var immutableLinks = Array.AsReadOnly(links);
            var cost = pricing.CreateForItinerary(intent, immutableLinks)
                ?? throw new InvalidOperationException(
                    "An offered connected service requires an explicit cost function.");
            var quote = EconomicTurnFreightQuote.FromForecast(
                turnOrdinal, intent.RouteKey,
                intent.ExpectedLoadMicrograms, cost);
            var selection = new EconomicLogicalTransportServiceSelection(
                intent, traversal, quote, nodes, immutableLinks);

            if (!best.TryGetValue(intent.RouteKey, out var current) ||
                selection.Quote.RawUnitCostPerGram.CompareTo(
                    current.Quote.RawUnitCostPerGram) < 0 ||
                (selection.Quote.RawUnitCostPerGram.CompareTo(
                    current.Quote.RawUnitCostPerGram) == 0 &&
                 selection.ServiceId < current.ServiceId))
                best[intent.RouteKey] = selection;
        }
        return Array.AsReadOnly(best.Values.OrderBy(x =>
                x.Quote.RouteKey.SourceEconomicPointId.Value)
            .ThenBy(x => x.Quote.RouteKey.DestinationEconomicPointId.Value)
            .ToArray());
    }
}
