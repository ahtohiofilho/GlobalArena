using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit COMPUTATIONAL search budget, not a freight, infrastructure or
/// economic-volume limit. An exhausted budget throws instead of reporting
/// an incomplete set of routes as the cheapest one.
/// </summary>
public sealed class EconomicFinePriceAwareSearchBudget
{
    public ulong MaxExaminedLinks { get; }
    public ulong MaxCompletePaths { get; }

    public EconomicFinePriceAwareSearchBudget(
        ulong maxExaminedLinks, ulong maxCompletePaths)
    {
        if (maxExaminedLinks == 0UL)
            throw new ArgumentOutOfRangeException(nameof(maxExaminedLinks));
        if (maxCompletePaths == 0UL)
            throw new ArgumentOutOfRangeException(nameof(maxCompletePaths));
        MaxExaminedLinks = maxExaminedLinks;
        MaxCompletePaths = maxCompletePaths;
    }
}

/// <summary>
/// Optional exact graph pruning for the whole-route price oracle.
/// Default Exhaustive preserves the existing reference path.
/// </summary>
public enum EconomicFinePriceAwareSearchMode : byte
{
    Exhaustive = 0,
    TerminalLeafPruning = 1
}

/// <summary>
/// Optional measurements; no impact on the selection, freight, or WorldState.
/// Caller owns the instance and must not share it between concurrent calls.
/// </summary>
public sealed class EconomicFinePriceAwareSearchDiagnostics
{
    public ulong ExaminedLinks { get; internal set; }
    public ulong CompletePaths { get; internal set; }
    public ulong EvaluatedFreightFunctions { get; internal set; }
    public ulong SelectedServices { get; internal set; }
    public ulong PrunedPhysicalVertices { get; internal set; }
    public ulong RejectedPrunedLinks { get; internal set; }

    internal void Reset() =>
        ExaminedLinks = CompletePaths =
            EvaluatedFreightFunctions = SelectedServices =
                PrunedPhysicalVertices = RejectedPrunedLinks = 0UL;
}

/// <summary>
/// Small-graph exact oracle over ALL SIMPLE built-asset itineraries of every
/// explicitly authorized unimodal service intent. Because whole-itinerary
/// price functions need not be additive, Dijkstra on per-edge prices cannot
/// generally solve this economic route-choice problem.
///
/// Every complete simple path is priced from the PRE-CYCLE forecast, once.
/// The cheapest path per service and then cheapest service per directional
/// economic pair wins; ties are path-lexical, then stable ServiceId.
/// No free geography links, modal transfers, cost coefficients or capacity
/// ceilings are introduced. Exhaustive enumeration can be exponential, so
/// this is deliberately a bounded small-world REFERENCE, not production
/// full-planet routing. No partial result is ever returned upon exhaustion.
/// </summary>
public sealed class EconomicFinePriceAwareServiceResolver
{
    public IReadOnlyList<EconomicTransportServiceSelection> Resolve(
        ulong turnOrdinal,
        StrategicSurfaceGraph fineGeography,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> finePointAnchors,
        EconomicProductionDemandResult flows,
        StrategicEdgeAccessSnapshot access,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<EconomicFineTransportServiceIntent> serviceIntents,
        IEconomicFineTransportItineraryPricing pricing,
        EconomicFinePriceAwareSearchBudget budget,
        EconomicFinePriceAwareSearchDiagnostics? diagnostics = null,
        EconomicFinePriceAwareSearchMode searchMode =
            EconomicFinePriceAwareSearchMode.Exhaustive)
    {
        diagnostics?.Reset();
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(fineGeography);
        ArgumentNullException.ThrowIfNull(finePointAnchors);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(serviceIntents);
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(budget);
        if (searchMode is not EconomicFinePriceAwareSearchMode.Exhaustive and
            not EconomicFinePriceAwareSearchMode.TerminalLeafPruning)
            throw new ArgumentOutOfRangeException(nameof(searchMode));

        var resolution = fineGeography.StrategicTopology.Parameters;
        var anchors = new Dictionary<EconomicPointId, int>();
        foreach (var (point, tile) in finePointAnchors)
        {
            if (!point.IsValid || !tile.IsValid ||
                tile.FineGoldbergParameters != resolution ||
                !anchors.TryAdd(
                    point, fineGeography.GetNodeIndex(tile.FineStrategicCellId)))
                throw new ArgumentException(
                    "Invalid or foreign fine market anchor.",
                    nameof(finePointAnchors));
        }

        var sources = flows.SupplyFlows
            .GroupBy(x => x.EconomicPointId)
            .ToDictionary(g => g.Key,
                g => g.Select(x => x.CommodityId).ToHashSet());
        var destinations = flows.DemandFlows
            .GroupBy(x => x.EconomicPointId)
            .ToDictionary(g => g.Key,
                g => g.Select(x => x.CommodityId).ToHashSet());
        if (sources.Keys.Concat(destinations.Keys).Any(
                point => !anchors.ContainsKey(point)))
            throw new ArgumentException(
                "Economic flow lacks a fine anchor.", nameof(finePointAnchors));

        var edges = fineGeography.StrategicTopology.Edges
            .ToDictionary(x => x.Id);
        if (access.ClosedEdgeIds.Any(id => !edges.ContainsKey(id)))
            throw new ArgumentException(
                "Closed corridor outside fine topology.", nameof(access));

        var assetMap = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        var adjacency = Enumerable.Range(0, fineGeography.NodeCount)
            .Select(_ => new List<Link>()).ToArray();
        foreach (var asset in installedAssets)
        {
            if (asset is null ||
                asset.FineGeographicEdgeId is not PhysicalTacticalEdgeId fineId ||
                fineId.FineGoldbergParameters != resolution ||
                !edges.TryGetValue(asset.GeographicEdgeId, out var edge) ||
                !assetMap.TryAdd(asset.AssetId, asset))
                throw new ArgumentException(
                    "Invalid, duplicate or foreign built asset.",
                    nameof(installedAssets));
            if (!asset.IsOperational || !access.IsOpen(asset.GeographicEdgeId))
                continue;

            var a = fineGeography.GetNodeIndex(edge.IncidentCellIds[0]);
            var b = fineGeography.GetNodeIndex(edge.IncidentCellIds[1]);
            adjacency[a].Add(new Link(b, asset.AssetId,
                asset.TechnologyTypeId, asset.TechnologyCatalogVersion));
            adjacency[b].Add(new Link(a, asset.AssetId,
                asset.TechnologyTypeId, asset.TechnologyCatalogVersion));
        }
        foreach (var links in adjacency)
            links.Sort(static (a, b) =>
            {
                var order = a.AssetId.CompareTo(b.AssetId);
                return order != 0 ? order : a.NextNode.CompareTo(b.NextNode);
            });

        var intents = serviceIntents.ToArray();
        if (intents.Any(x => x is null) ||
            intents.Select(x => x!.ServiceId).Distinct().Count() != intents.Length)
            throw new ArgumentException(
                "Missing or duplicate service identity.", nameof(serviceIntents));
        foreach (var intent in intents)
            if (!anchors.ContainsKey(intent.RouteKey.SourceEconomicPointId) ||
                !anchors.ContainsKey(intent.RouteKey.DestinationEconomicPointId))
                throw new ArgumentException(
                    "Service intent has no fine economic anchor.",
                    nameof(serviceIntents));

        var best = new Dictionary<StrategicEconomicRouteKey, Candidate>();
        foreach (var intent in intents.OrderBy(x => x.ServiceId))
        {
            if (!intent.IsAvailable ||
                !sources.TryGetValue(intent.RouteKey.SourceEconomicPointId,
                    out var sourceFoods) ||
                !destinations.TryGetValue(intent.RouteKey.DestinationEconomicPointId,
                    out var destinationFoods) ||
                !sourceFoods.Overlaps(destinationFoods))
                continue;

            var start = anchors[intent.RouteKey.SourceEconomicPointId];
            var end = anchors[intent.RouteKey.DestinationEconomicPointId];
            if (start == end)
                continue;

            var selected = SearchIntent(
                intent, start, end, adjacency, assetMap, pricing,
                budget, diagnostics, searchMode);
            if (selected is null)
                continue;

            if (!best.TryGetValue(intent.RouteKey, out var existing) ||
                selected.UnitCost.CompareTo(existing.UnitCost) < 0 ||
                (selected.UnitCost.CompareTo(existing.UnitCost) == 0 &&
                 selected.Intent.ServiceId < existing.Intent.ServiceId))
                best[intent.RouteKey] = selected;
        }

        var result = best.OrderBy(x => x.Key.SourceEconomicPointId.Value)
            .ThenBy(x => x.Key.DestinationEconomicPointId.Value)
            .Select(x =>
            {
                var candidate = x.Value;
                var offer = new EconomicTransportServiceOffer(
                    candidate.Intent.ServiceId,
                    candidate.Intent.RouteKey,
                    candidate.AssetIds, Array.Empty<int>(),
                    candidate.Intent.ExpectedLoadMicrograms,
                    candidate.PriceFunction);
                var quote = EconomicTurnFreightQuote.FromPreparedServiceUnitCost(
                    turnOrdinal, x.Key, candidate.RulesetVersion,
                    candidate.Intent.ExpectedLoadMicrograms,
                    candidate.UnitCost);
                return new EconomicTransportServiceSelection(
                    offer, quote, candidate.OrderedAssets);
            }).ToArray();
        if (diagnostics is not null)
            diagnostics.SelectedServices = checked((ulong)result.Length);
        return Array.AsReadOnly(result);
    }

    private static Candidate? SearchIntent(
        EconomicFineTransportServiceIntent intent, int start, int end,
        IReadOnlyList<List<Link>> adjacency,
        IReadOnlyDictionary<ulong, EconomicTransportBuiltAsset> assetMap,
        IEconomicFineTransportItineraryPricing pricing,
        EconomicFinePriceAwareSearchBudget budget,
        EconomicFinePriceAwareSearchDiagnostics? diagnostics,
        EconomicFinePriceAwareSearchMode searchMode)
    {
        var admissible = searchMode ==
            EconomicFinePriceAwareSearchMode.TerminalLeafPruning
            ? KeepPossibleSimplePathVertices(
                start, end, intent.TechnologyTypeId,
                intent.TechnologyCatalogVersion, adjacency, diagnostics)
            : null;
        var visited = new bool[adjacency.Count];
        var stack = new List<Frame> { new(start, 0) };
        var path = new List<ulong>();
        visited[start] = true;
        Candidate? best = null;
        ulong examined = 0UL;
        ulong completed = 0UL;

        while (stack.Count > 0)
        {
            var top = stack.Count - 1;
            var frame = stack[top];

            if (frame.Node == end)
            {
                completed = checked(completed + 1UL);
                if (completed > budget.MaxCompletePaths)
                    throw new InvalidOperationException(
                        "Exact route search exhausted complete-path budget.");
                if (diagnostics is not null)
                    diagnostics.CompletePaths++;

                var pathIds = path.ToArray();
                var ordered = Array.AsReadOnly(
                    pathIds.Select(id => assetMap[id]).ToArray());
                var priceFunction = pricing.CreateForItinerary(intent, ordered)
                    ?? throw new InvalidOperationException(
                        "Route pricing function is missing.");
                var version = priceFunction.RulesetVersion;
                if (version == 0UL)
                    throw new InvalidOperationException(
                        "Route price function has no ruleset version.");
                var cost = priceFunction.EvaluateRawUnitCostPerGram(
                    intent.ExpectedLoadMicrograms)
                    ?? throw new InvalidOperationException(
                        "Route price function returned null cost.");
                if (version != priceFunction.RulesetVersion)
                    throw new InvalidOperationException(
                        "Route price function changed ruleset while being evaluated.");
                if (diagnostics is not null)
                    diagnostics.EvaluatedFreightFunctions++;

                if (best is null || cost.CompareTo(best.UnitCost) < 0 ||
                    (cost.CompareTo(best.UnitCost) == 0 &&
                     ComparePaths(pathIds, best.AssetIds) < 0))
                    best = new Candidate(
                        intent, pathIds, ordered, priceFunction, version, cost);

                visited[frame.Node] = false;
                stack.RemoveAt(top);
                if (path.Count > 0)
                    path.RemoveAt(path.Count - 1);
                continue;
            }

            if (frame.NextIndex == adjacency[frame.Node].Count)
            {
                visited[frame.Node] = false;
                stack.RemoveAt(top);
                if (path.Count > 0)
                    path.RemoveAt(path.Count - 1);
                continue;
            }

            var link = adjacency[frame.Node][frame.NextIndex];
            stack[top] = new Frame(frame.Node, frame.NextIndex + 1);
            if (admissible is not null && !admissible[link.NextNode])
            {
                if (diagnostics is not null)
                    diagnostics.RejectedPrunedLinks++;
                continue;
            }
            examined = checked(examined + 1UL);
            if (examined > budget.MaxExaminedLinks)
                throw new InvalidOperationException(
                    "Exact route search exhausted explored-link budget.");
            if (diagnostics is not null)
                diagnostics.ExaminedLinks++;

            if (visited[link.NextNode] ||
                link.TechnologyTypeId != intent.TechnologyTypeId ||
                link.TechnologyCatalogVersion != intent.TechnologyCatalogVersion)
                continue;

            visited[link.NextNode] = true;
            path.Add(link.AssetId);
            stack.Add(new Frame(link.NextNode, 0));
        }
        return best;
    }

    /// <summary>
    /// Exact physical pruning for SIMPLE source-to-destination paths.
    /// A nonterminal vertex with at most one eligible incident physical
    /// edge cannot be an interior vertex of a simple route. Repeatedly
    /// removing such leaves preserves EVERY eligible simple path.
    /// Parallel constructed assets count as multiple incidences, which may
    /// retain unnecessary branches but never removes a valid path.
    /// No economic assumptions about price functions or edge additivity.
    /// </summary>
    private static bool[] KeepPossibleSimplePathVertices(
        int start, int end, ulong technologyTypeId, ulong catalogVersion,
        IReadOnlyList<List<Link>> adjacency,
        EconomicFinePriceAwareSearchDiagnostics? diagnostics)
    {
        var retained = Enumerable.Repeat(true, adjacency.Count).ToArray();
        var degree = new int[adjacency.Count];
        var queue = new Queue<int>();
        for (var node = 0; node < adjacency.Count; node++)
        {
            foreach (var link in adjacency[node])
            {
                if (link.TechnologyTypeId == technologyTypeId &&
                    link.TechnologyCatalogVersion == catalogVersion)
                    degree[node]++;
            }
            if (node != start && node != end && degree[node] <= 1)
                queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!retained[node] || node == start || node == end ||
                degree[node] > 1)
                continue;
            retained[node] = false;
            if (diagnostics is not null)
                diagnostics.PrunedPhysicalVertices++;
            foreach (var link in adjacency[node])
            {
                if (link.TechnologyTypeId != technologyTypeId ||
                    link.TechnologyCatalogVersion != catalogVersion ||
                    !retained[link.NextNode])
                    continue;
                degree[link.NextNode]--;
                if (link.NextNode != start && link.NextNode != end &&
                    degree[link.NextNode] <= 1)
                    queue.Enqueue(link.NextNode);
            }
        }
        return retained;
    }

    private static int ComparePaths(
        IReadOnlyList<ulong> a, IReadOnlyList<ulong> b)
    {
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var order = a[i].CompareTo(b[i]);
            if (order != 0)
                return order;
        }
        return a.Count.CompareTo(b.Count);
    }

    private readonly record struct Frame(int Node, int NextIndex);
    private readonly record struct Link(
        int NextNode, ulong AssetId,
        ulong TechnologyTypeId, ulong TechnologyCatalogVersion);

    private sealed record Candidate(
        EconomicFineTransportServiceIntent Intent,
        ulong[] AssetIds,
        IReadOnlyList<EconomicTransportBuiltAsset> OrderedAssets,
        IEconomicTurnFreightCostFunction PriceFunction,
        ulong RulesetVersion,
        FoodTransportCostFraction UnitCost);
}
