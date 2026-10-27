using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A scheduled CALL at a logical node of an explicitly offered service.
/// A call is NOT inferred when the route passes through an intermediate
/// node. It has no automatic right to load or unload cargo.
/// </summary>
public sealed class EconomicLogicalServiceCall
{
    public EconomicPointId EconomicPointId { get; }
    public EconomicTransportLogicalNodeId NodeId { get; }

    public EconomicLogicalServiceCall(
        EconomicPointId economicPointId, EconomicTransportLogicalNodeId nodeId)
    {
        if (!economicPointId.IsValid || !nodeId.IsValid)
            throw new ArgumentException("A service call needs valid point and node IDs.");
        EconomicPointId = economicPointId;
        NodeId = nodeId;
    }
}

/// <summary>
/// One aggregate pre-cycle cargo forecast riding only between TWO scheduled
/// calls of one service. Other cargo may board or alight at different calls.
/// No individual voyage or vehicle is modeled.
/// </summary>
public sealed class EconomicLogicalServiceConsignment
{
    public ulong ConsignmentId { get; }
    public int BoardCallIndex { get; }
    public int AlightCallIndex { get; }
    public BigInteger ExpectedLoadMicrograms { get; }

    public EconomicLogicalServiceConsignment(
        ulong consignmentId, int boardCallIndex, int alightCallIndex,
        BigInteger expectedLoadMicrograms)
    {
        if (consignmentId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(consignmentId));
        if (boardCallIndex < 0 || alightCallIndex <= boardCallIndex)
            throw new ArgumentException(
                "A consignment must travel forward between distinct calls.");
        if (expectedLoadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));
        ConsignmentId = consignmentId;
        BoardCallIndex = boardCallIndex;
        AlightCallIndex = alightCallIndex;
        ExpectedLoadMicrograms = expectedLoadMicrograms;
    }
}

/// <summary>
/// Deliberately offered, single-technology multi-call pattern. The CALL list
/// controls whether the service stops; physical route nodes between calls
/// are only transit. An alternative direct service is a DIFFERENT pattern.
/// </summary>
public sealed class EconomicLogicalMultiStopPattern
{
    public ulong PatternId { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public bool IsAvailable { get; }
    public IReadOnlyList<EconomicLogicalServiceCall> Calls { get; }
    public IReadOnlyList<EconomicLogicalServiceConsignment> Consignments { get; }

    public EconomicLogicalMultiStopPattern(
        ulong patternId, ulong technologyTypeId, ulong technologyCatalogVersion,
        IEnumerable<EconomicLogicalServiceCall> calls,
        IEnumerable<EconomicLogicalServiceConsignment> consignments,
        bool isAvailable = true)
    {
        if (patternId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(patternId));
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(consignments);
        var scheduled = calls.ToArray();
        var booked = consignments.ToArray();
        if (scheduled.Length < 2 || scheduled.Any(x => x is null) ||
            scheduled.Select(x => x!.NodeId.Value).Distinct().Count() !=
                scheduled.Length ||
            scheduled.Select(x => x!.EconomicPointId.Value).Distinct().Count() !=
                scheduled.Length)
            throw new ArgumentException(
                "A reference pattern needs distinct scheduled nodes and points.",
                nameof(calls));
        if (booked.Any(x => x is null ||
                x.AlightCallIndex >= scheduled.Length) ||
            booked.Select(x => x!.ConsignmentId).Distinct().Count() !=
                booked.Length)
            throw new ArgumentException(
                "Consignment identities and call indices must be valid and unique.",
                nameof(consignments));

        PatternId = patternId;
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        IsAvailable = isAvailable;
        Calls = Array.AsReadOnly(scheduled);
        Consignments = Array.AsReadOnly(
            booked.OrderBy(x => x.ConsignmentId).ToArray());
    }
}

/// <summary>
/// Immutable forecast of the ENTIRE offered pattern, independent of the
/// particular origin-destination consignment being priced. For each physical
/// service segment [i, i+1], ExpectedOnboardMicrograms[i] sums forecasts
/// of ALL consignments whose declared call interval contains that segment.
/// At a call, boarding and alighting totals are independent. These values
/// let a caller-owned cost ruleset share an actual call's overhead without
/// charging every cargo unit as if it were individually unloaded.
/// </summary>
public sealed class EconomicLogicalMultiStopLoadContext
{
    public EconomicLogicalMultiStopPattern Pattern { get; }
    public IReadOnlyList<BigInteger> ExpectedOnboardMicrograms { get; }
    public IReadOnlyList<BigInteger> ExpectedBoardedMicrograms { get; }
    public IReadOnlyList<BigInteger> ExpectedAlightedMicrograms { get; }

    internal EconomicLogicalMultiStopLoadContext(
        EconomicLogicalMultiStopPattern pattern)
    {
        Pattern = pattern;
        var onboard = new BigInteger[pattern.Calls.Count - 1];
        var boarded = new BigInteger[pattern.Calls.Count];
        var alighted = new BigInteger[pattern.Calls.Count];
        foreach (var cargo in pattern.Consignments)
        {
            boarded[cargo.BoardCallIndex] += cargo.ExpectedLoadMicrograms;
            alighted[cargo.AlightCallIndex] += cargo.ExpectedLoadMicrograms;
            for (var leg = cargo.BoardCallIndex; leg < cargo.AlightCallIndex; leg++)
                onboard[leg] += cargo.ExpectedLoadMicrograms;
        }
        ExpectedOnboardMicrograms = Array.AsReadOnly(onboard);
        ExpectedBoardedMicrograms = Array.AsReadOnly(boarded);
        ExpectedAlightedMicrograms = Array.AsReadOnly(alighted);
    }
}

/// <summary>
/// Builds ONE whole-itinerary unit-cost function per aggregate OD cargo
/// forecast, with visibility of OTHER cargo, all scheduled calls, the
/// full service path and the legs actually ridden by this consignment.
/// Cost sharing, handling terms and stop overhead are EXPLICIT caller
/// economics, NEVER an inferred built-in tariff or per-link cost.
/// </summary>
public interface IEconomicLogicalMultiStopPricing
{
    IEconomicTurnFreightCostFunction CreateForConsignment(
        EconomicLogicalMultiStopLoadContext context,
        EconomicLogicalServiceConsignment consignment,
        IReadOnlyList<EconomicTransportLogicalLink> wholePatternLinks,
        IReadOnlyList<EconomicTransportLogicalLink> riddenLinks);
}

/// <summary>
/// Frozen quote and full pattern/cargo/snapshot provenance. The commodity
/// market only consumes Quote; no per-voyage scheduling is introduced.
/// </summary>
public sealed class EconomicLogicalMultiStopSelection
{
    public ulong PatternId { get; }
    public ulong ConsignmentId { get; }
    public ulong ProviderId { get; }
    public ulong ProviderVersion { get; }
    public ulong SnapshotOrdinal { get; }
    public ulong LoadFacilityId { get; }
    public ulong UnloadFacilityId { get; }
    public int BoardCallIndex { get; }
    public int AlightCallIndex { get; }
    public IReadOnlyList<ulong> WholePatternLinkIds { get; }
    public IReadOnlyList<ulong> RiddenLinkIds { get; }
    public EconomicTurnFreightQuote Quote { get; }

    internal EconomicLogicalMultiStopSelection(
        EconomicLogicalMultiStopPattern pattern,
        EconomicLogicalServiceConsignment consignment,
        ulong providerId, ulong providerVersion, ulong snapshotOrdinal,
        ulong loadFacilityId, ulong unloadFacilityId,
        IReadOnlyList<EconomicTransportLogicalLink> wholeLinks,
        IReadOnlyList<EconomicTransportLogicalLink> riddenLinks,
        EconomicTurnFreightQuote quote)
    {
        PatternId = pattern.PatternId;
        ConsignmentId = consignment.ConsignmentId;
        ProviderId = providerId;
        ProviderVersion = providerVersion;
        SnapshotOrdinal = snapshotOrdinal;
        LoadFacilityId = loadFacilityId;
        UnloadFacilityId = unloadFacilityId;
        BoardCallIndex = consignment.BoardCallIndex;
        AlightCallIndex = consignment.AlightCallIndex;
        WholePatternLinkIds = Array.AsReadOnly(
            wholeLinks.Select(x => x.LinkId).ToArray());
        RiddenLinkIds = Array.AsReadOnly(
            riddenLinks.Select(x => x.LinkId).ToArray());
        Quote = quote;
    }
}

/// <summary>
/// Opt-in SMALL reference for explicit single-technology service patterns.
/// Consecutive CALLS are joined by the registered traversal provider,
/// whose returned links must be in the SAME authorized snapshot. The
/// concatenated route must be one simple, continuous physical node path;
/// no partial availability, implicit stops or modal changes are allowed.
/// Every active cargo booking requires a facility for load and unload,
/// but intermediate onboard cargo requires NO handling grant at that call.
/// All input validation is completed before creating ANY pricing function.
/// </summary>
public sealed class EconomicLogicalMultiStopQuoteResolver
{
    public IReadOnlyList<EconomicLogicalMultiStopSelection> Resolve(
        ulong turnOrdinal,
        EconomicLogicalTransportProviderRegistry registry,
        EconomicLogicalTransportAuthorizationSnapshot physicalSnapshot,
        EconomicLogicalCargoOperationsSnapshot operationsSnapshot,
        IEnumerable<EconomicLogicalMultiStopPattern> patterns,
        IEconomicLogicalMultiStopPricing pricing)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(physicalSnapshot);
        ArgumentNullException.ThrowIfNull(operationsSnapshot);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(pricing);
        if (physicalSnapshot.SnapshotOrdinal != operationsSnapshot.SnapshotOrdinal)
            throw new InvalidOperationException(
                "Physical and handling authorizations must use the same snapshot.");

        var declared = patterns.ToArray();
        if (declared.Any(x => x is null) ||
            declared.Select(x => x!.PatternId).Distinct().Count() != declared.Length ||
            declared.SelectMany(x => x!.Consignments)
                .Select(x => x.ConsignmentId).Distinct().Count() !=
                    declared.Sum(x => x!.Consignments.Count))
            throw new ArgumentException(
                "Pattern and cargo identities must be globally unique.", nameof(patterns));

        var ready = new List<ReadyPattern>();
        // Build and validate ALL physical and cargo-capability evidence
        // before evaluating a single ex-ante whole-route pricing function.
        foreach (var pattern in declared.OrderBy(x => x.PatternId))
        {
            if (!pattern.IsAvailable)
                continue;
            var context = new EconomicLogicalMultiStopLoadContext(pattern);
            var facilityPairs = new Dictionary<ulong, (ulong Load, ulong Unload)>();
            foreach (var cargo in pattern.Consignments)
            {
                var boarding = pattern.Calls[cargo.BoardCallIndex];
                var alighting = pattern.Calls[cargo.AlightCallIndex];
                var loading = operationsSnapshot.RequireOperation(
                    boarding.EconomicPointId, boarding.NodeId,
                    pattern.TechnologyTypeId, pattern.TechnologyCatalogVersion,
                    EconomicLogicalCargoOperation.Load);
                var unloading = operationsSnapshot.RequireOperation(
                    alighting.EconomicPointId, alighting.NodeId,
                    pattern.TechnologyTypeId, pattern.TechnologyCatalogVersion,
                    EconomicLogicalCargoOperation.Unload);
                facilityPairs.Add(cargo.ConsignmentId, (loading, unloading));
            }

            var allLinks = new List<EconomicTransportLogicalLink>();
            var visitedNodeIds = new HashSet<ulong>
            {
                pattern.Calls[0].NodeId.Value
            };
            var visitedLinkIds = new HashSet<ulong>();
            var offsets = new int[pattern.Calls.Count];
            ulong registeredProviderId = 0UL;
            ulong registeredProviderVersion = 0UL;
            var reachable = true;
            for (var call = 0; call < pattern.Calls.Count - 1; call++)
            {
                var first = pattern.Calls[call];
                var second = pattern.Calls[call + 1];
                var intent = new EconomicLogicalTransportServiceIntent(
                    pattern.PatternId,
                    new StrategicEconomicRouteKey(
                        first.EconomicPointId, second.EconomicPointId),
                    first.NodeId, second.NodeId,
                    pattern.TechnologyTypeId, pattern.TechnologyCatalogVersion,
                    context.ExpectedOnboardMicrograms[call]);
                var registration = registry.Require(intent);
                physicalSnapshot.RequireEndpoints(intent);
                var provider = registration.Provider;
                if (provider.ProviderId != registration.Definition.ProviderId ||
                    provider.ProviderVersion != registration.Definition.ProviderVersion)
                    throw new InvalidOperationException(
                        "Registered traversal provider identity changed.");
                registeredProviderId = provider.ProviderId;
                registeredProviderVersion = provider.ProviderVersion;
                var proposed = provider.FindItinerary(intent);
                if (proposed is null)
                {
                    reachable = false;
                    break;
                }
                var cursor = first.NodeId;
                var links = proposed.ToArray();
                if (links.Length == 0)
                    throw new InvalidOperationException(
                        "A scheduled call pair requires physical links.");
                foreach (var link in links)
                {
                    if (link is null || !link.IsOperational ||
                        link.TechnologyTypeId != pattern.TechnologyTypeId ||
                        link.TechnologyCatalogVersion != pattern.TechnologyCatalogVersion ||
                        !visitedLinkIds.Add(link.LinkId))
                        throw new InvalidOperationException(
                            "Invalid or repeated whole-pattern transport link.");
                    physicalSnapshot.RequireAuthorizedLink(
                        registeredProviderId, registeredProviderVersion, link);
                    var next = link.FirstNode == cursor ? link.SecondNode :
                        link.SecondNode == cursor ? link.FirstNode :
                        throw new InvalidOperationException(
                            "Scheduled-call segment is not a continuous physical line.");
                    if (!visitedNodeIds.Add(next.Value))
                        throw new InvalidOperationException(
                            "Whole-pattern itinerary must be a simple node path.");
                    cursor = next;
                    allLinks.Add(link);
                }
                if (cursor != second.NodeId)
                    throw new InvalidOperationException(
                        "Scheduled-call segment missed the declared stop.");
                offsets[call + 1] = allLinks.Count;
            }
            if (reachable)
                ready.Add(new ReadyPattern(pattern, context, facilityPairs,
                    Array.AsReadOnly(allLinks.ToArray()), offsets,
                    registeredProviderId, registeredProviderVersion));
        }

        var cheapest = new Dictionary<StrategicEconomicRouteKey,
            EconomicLogicalMultiStopSelection>();
        foreach (var plan in ready)
        {
            foreach (var cargo in plan.Pattern.Consignments)
            {
                var from = plan.Pattern.Calls[cargo.BoardCallIndex];
                var to = plan.Pattern.Calls[cargo.AlightCallIndex];
                var key = new StrategicEconomicRouteKey(
                    from.EconomicPointId, to.EconomicPointId);
                var riddenLinks = Array.AsReadOnly(plan.WholeLinks
                    .Skip(plan.CallOffsets[cargo.BoardCallIndex])
                    .Take(plan.CallOffsets[cargo.AlightCallIndex] -
                        plan.CallOffsets[cargo.BoardCallIndex])
                    .ToArray());
                var costFunction = pricing.CreateForConsignment(
                    plan.Context, cargo, plan.WholeLinks, riddenLinks)
                    ?? throw new InvalidOperationException(
                        "A complete cargo itinerary needs an explicit cost function.");
                var quote = EconomicTurnFreightQuote.FromForecast(
                    turnOrdinal, key, cargo.ExpectedLoadMicrograms, costFunction);
                var facilities = plan.Facilities[cargo.ConsignmentId];
                var selection = new EconomicLogicalMultiStopSelection(
                    plan.Pattern, cargo, plan.ProviderId, plan.ProviderVersion,
                    physicalSnapshot.SnapshotOrdinal, facilities.Load,
                    facilities.Unload, plan.WholeLinks, riddenLinks, quote);
                if (!cheapest.TryGetValue(key, out var prior) ||
                    quote.RawUnitCostPerGram.CompareTo(
                        prior.Quote.RawUnitCostPerGram) < 0 ||
                    (quote.RawUnitCostPerGram.CompareTo(
                        prior.Quote.RawUnitCostPerGram) == 0 &&
                     (selection.PatternId < prior.PatternId ||
                      (selection.PatternId == prior.PatternId &&
                       selection.ConsignmentId < prior.ConsignmentId))))
                    cheapest[key] = selection;
            }
        }
        return Array.AsReadOnly(cheapest.Values
            .OrderBy(x => x.Quote.RouteKey.SourceEconomicPointId.Value)
            .ThenBy(x => x.Quote.RouteKey.DestinationEconomicPointId.Value)
            .ToArray());
    }

    private sealed record ReadyPattern(
        EconomicLogicalMultiStopPattern Pattern,
        EconomicLogicalMultiStopLoadContext Context,
        IReadOnlyDictionary<ulong, (ulong Load, ulong Unload)> Facilities,
        IReadOnlyList<EconomicTransportLogicalLink> WholeLinks,
        int[] CallOffsets, ulong ProviderId, ulong ProviderVersion);
}
