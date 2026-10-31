using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A directed edge in an already validated, single-technology commercial
/// graph. The weight is a raw monetary GEOGRAPHIC numerator, not a movement
/// point count or the whole-service freight quote.
/// </summary>
public sealed record ExperimentalFreightWeightedArc(
    ulong FromCellId, ulong ToCellId, ulong AssetId, BigInteger GeographicWeight)
{
    public void Validate()
    {
        if (FromCellId == 0UL || ToCellId == 0UL ||
            FromCellId == ToCellId || AssetId == 0UL ||
            GeographicWeight.Sign < 0)
            throw new ArgumentException("Freight graph arcs must have valid endpoints, assets and nonnegative costs.");
    }
}

public sealed record ExperimentalFreightWeightedPath(
    BigInteger GeographicWeight,
    IReadOnlyList<ulong> CellIds,
    IReadOnlyList<ulong> AssetIds);

/// <summary>
/// Deterministic operation counts, deliberately not wall-clock promises.
/// Reset for each independent discovery; one source tree serves all declared
/// destinations with the same start and versioned technology.
/// </summary>
public sealed class ExperimentalFreightRouteSearchDiagnostics
{
    public int SourceTreeBuilds { get; internal set; }
    public long ExaminedArcs { get; internal set; }
    public int OfferedServices { get; internal set; }
    public int UnreachableServices { get; internal set; }
    public void Reset()
    {
        SourceTreeBuilds = 0;
        ExaminedArcs = 0;
        OfferedServices = 0;
        UnreachableServices = 0;
    }
}

/// <summary>
/// Pure nonnegative-weight Dijkstra: minimum total geographic MONETARY
/// numerator, then fewer legs, then lexicographically smaller asset-ID path.
/// A zero-weight edge cannot create a favorable loop: fewer legs wins.
/// </summary>
public static class ExperimentalFreightWeightedPathfinder
{
    private sealed record PathState(
        BigInteger Cost, ulong[] Cells, ulong[] Assets)
    {
        public int Hops => Assets.Length;
    }

    public static IReadOnlyDictionary<ulong, ExperimentalFreightWeightedPath> FromSource(
        ulong startCellId, IEnumerable<ExperimentalFreightWeightedArc> arcs,
        ExperimentalFreightRouteSearchDiagnostics? diagnostics = null)
    {
        if (startCellId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(startCellId));
        ArgumentNullException.ThrowIfNull(arcs);
        var adjacency = new Dictionary<ulong, List<ExperimentalFreightWeightedArc>>();
        foreach (var arc in arcs)
        {
            ArgumentNullException.ThrowIfNull(arc);
            arc.Validate();
            if (!adjacency.TryGetValue(arc.FromCellId, out var list))
                adjacency[arc.FromCellId] = list = new();
            list.Add(arc);
        }
        foreach (var list in adjacency.Values)
            list.Sort(static (a, b) =>
            {
                var comparison = a.AssetId.CompareTo(b.AssetId);
                return comparison != 0 ? comparison : a.ToCellId.CompareTo(b.ToCellId);
            });

        var best = new Dictionary<ulong, PathState>
        {
            [startCellId] = new(BigInteger.Zero,
                new[] { startCellId }, Array.Empty<ulong>())
        };
        var queue = new PriorityQueue<ulong, (BigInteger Cost, int Hops, ulong CellId)>();
        queue.Enqueue(startCellId, (BigInteger.Zero, 0, startCellId));
        diagnostics?.SourceTreeBuilds++;

        while (queue.TryDequeue(out var node, out var priority))
        {
            var current = best[node];
            if (current.Cost != priority.Cost || current.Hops != priority.Hops)
                continue;

            if (!adjacency.TryGetValue(node, out var outgoing))
                continue;
            foreach (var edge in outgoing)
            {
                if (diagnostics is not null)
                    diagnostics.ExaminedArcs++;
                var candidateCost = current.Cost + edge.GeographicWeight;
                var candidateHops = checked(current.Hops + 1);
                if (best.TryGetValue(edge.ToCellId, out var previous))
                {
                    if (candidateCost > previous.Cost ||
                        (candidateCost == previous.Cost &&
                         candidateHops > previous.Hops))
                        continue;
                    if (candidateCost == previous.Cost &&
                        candidateHops == previous.Hops &&
                        !LexicographicallyBetter(current.Assets,
                            edge.AssetId, previous.Assets))
                        continue;
                }

                var nodes = new ulong[current.Cells.Length + 1];
                current.Cells.CopyTo(nodes, 0);
                nodes[^1] = edge.ToCellId;
                var assets = new ulong[candidateHops];
                current.Assets.CopyTo(assets, 0);
                assets[^1] = edge.AssetId;
                best[edge.ToCellId] = new PathState(candidateCost, nodes, assets);
                queue.Enqueue(edge.ToCellId,
                    (candidateCost, candidateHops, edge.ToCellId));
            }
        }

        return new ReadOnlyDictionary<ulong, ExperimentalFreightWeightedPath>(
            best.ToDictionary(x => x.Key, x => new ExperimentalFreightWeightedPath(
                x.Value.Cost, Array.AsReadOnly(x.Value.Cells),
                Array.AsReadOnly(x.Value.Assets))));
    }

    private static bool LexicographicallyBetter(
        ulong[] prefix, ulong lastAsset, ulong[] current)
    {
        for (var i = 0; i < prefix.Length; i++)
        {
            if (prefix[i] < current[i]) return true;
            if (prefix[i] > current[i]) return false;
        }
        return lastAsset < current[^1];
    }
}

/// <summary>
/// OPTIONAL commercial itinerary discovery using the VS40 additively
/// separable price policy. It does NOT optimize arbitrary nonadditive
/// itinerary prices, multimodal services, cargo terminals or congestion
/// shared between competing routes: those require a later global solver.
///
/// For each declared start + technology, build ONE cost-optimal source tree.
/// Fixed service overhead and smooth load-dependent saturation are constant
/// across paths for that declared service, so minimizing the exact shared
/// per-edge geographic term minimizes its full ex-ante quote.
/// </summary>
public sealed class ExperimentalCostOptimalFreightItineraryDiscovery
{
    public IReadOnlyList<EconomicTransportServiceOffer> Discover(
        CanonicalPhysicalTacticalSession session,
        CanonicalLandEdgeFactsSnapshot facts,
        ExperimentalLandInfrastructureSnapshot network,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors,
        StrategicEdgeAccessSnapshot access,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<EconomicFineTransportServiceIntent> declaredServices,
        IEnumerable<ExperimentalFreightTechnologyPricing> policies,
        ExperimentalFreightRouteSearchDiagnostics? diagnostics = null)
    {
        diagnostics?.Reset();
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(declaredServices);
        ArgumentNullException.ThrowIfNull(policies);

        var assets = installedAssets.ToArray();
        var intents = declaredServices.ToArray();
        var pricingPolicies = policies.ToArray();
        var prices = new ExperimentalGeographicFreightItineraryPricing(
            session, facts, network, anchors, pricingPolicies);
        if (intents.Any(x => x is null) ||
            intents.Select(x => x!.ServiceId).Distinct().Count() != intents.Length)
            throw new ArgumentException(
                "Services require unique non-null declared identities.",
                nameof(declaredServices));
        var policyMap = pricingPolicies.ToDictionary(
            p => (p.TechnologyTypeId, p.TechnologyCatalogVersion));

        var topology = session.FineWorld.StrategicTopology;
        var resolution = topology.Parameters;
        foreach (var closed in access.ClosedEdgeIds)
            if (!closed.IsValid || closed.Value > (ulong)topology.Edges.Count)
                throw new ArgumentException(
                    "Closed freight crossings must belong to the current fine graph.",
                    nameof(access));

        var ids = new HashSet<ulong>();
        var arcsByTechnology = new Dictionary<(ulong, ulong),
            List<ExperimentalFreightWeightedArc>>();
        var byId = new Dictionary<ulong, EconomicTransportBuiltAsset>();

        foreach (var asset in assets)
        {
            if (asset is null || !ids.Add(asset.AssetId) ||
                asset.FineGeographicEdgeId is not PhysicalTacticalEdgeId edgeId ||
                edgeId.FineGoldbergParameters != resolution ||
                asset.GeographicEdgeId.Value > (ulong)topology.Edges.Count)
                throw new ArgumentException(
                    "Built freight assets must be unique and use qualified fine crossings.",
                    nameof(installedAssets));
            byId.Add(asset.AssetId, asset);
            if (!asset.IsOperational || !access.IsOpen(asset.GeographicEdgeId) ||
                !policyMap.TryGetValue(
                    (asset.TechnologyTypeId, asset.TechnologyCatalogVersion),
                    out var policy))
                continue;

            var edge = topology.Edges[checked((int)asset.GeographicEdgeId.Value - 1)];
            var from = new PhysicalTacticalTileId(resolution, edge.IncidentCellIds[0]);
            var to = new PhysicalTacticalTileId(resolution, edge.IncidentCellIds[1]);
            var forward = facts.Find(from, to);
            var backward = facts.Find(to, from);
            if (forward is null || backward is null)
                throw new InvalidOperationException(
                    "Commercial land services cannot use an aquatic or invalid crossing.");

            var built = network.At(session, from, to);
            var eligible = policy.RequiredInfrastructure switch
            {
                ExperimentalLandInfrastructureKind.StoneRoad =>
                    built.RoadQuality >= ExperimentalLandRoadQuality.StoneRoad,
                ExperimentalLandInfrastructureKind.Highway =>
                    built.RoadQuality >= ExperimentalLandRoadQuality.Highway,
                ExperimentalLandInfrastructureKind.Railway => built.HasRailway,
                _ => false
            };
            if (!eligible) continue;

            var key = (asset.TechnologyTypeId, asset.TechnologyCatalogVersion);
            if (!arcsByTechnology.TryGetValue(key, out var arcs))
                arcsByTechnology[key] = arcs = new();
            arcs.Add(new ExperimentalFreightWeightedArc(
                from.FineStrategicCellId.Value, to.FineStrategicCellId.Value,
                asset.AssetId,
                ExperimentalGeographicFreightItineraryPricing.GeographicContribution(
                    forward, policy)));
            arcs.Add(new ExperimentalFreightWeightedArc(
                to.FineStrategicCellId.Value, from.FineStrategicCellId.Value,
                asset.AssetId,
                ExperimentalGeographicFreightItineraryPricing.GeographicContribution(
                    backward, policy)));
        }

        var outputs = new List<EconomicTransportServiceOffer>();
        var trees = new Dictionary<(ulong Source, ulong Type, ulong Catalog),
            IReadOnlyDictionary<ulong, ExperimentalFreightWeightedPath>>();
        foreach (var intent in intents.OrderBy(x => x.ServiceId))
        {
            if (!anchors.TryGetValue(intent.RouteKey.SourceEconomicPointId,
                    out var start) ||
                !anchors.TryGetValue(intent.RouteKey.DestinationEconomicPointId,
                    out var end) ||
                start.FineGoldbergParameters != resolution ||
                end.FineGoldbergParameters != resolution)
                throw new ArgumentException(
                    "Declared freight services require valid fine economic anchors.",
                    nameof(anchors));
            if (!intent.IsAvailable)
                continue;
            var type = (intent.TechnologyTypeId, intent.TechnologyCatalogVersion);
            if (!policyMap.ContainsKey(type))
                throw new InvalidOperationException(
                    "Declared service needs an explicit versioned freight pricing policy.");
            var group = (start.FineStrategicCellId.Value, type.TechnologyTypeId,
                type.TechnologyCatalogVersion);
            if (!trees.TryGetValue(group, out var routes))
            {
                arcsByTechnology.TryGetValue(type, out var possible);
                routes = ExperimentalFreightWeightedPathfinder.FromSource(
                    group.Item1, possible ??
                        Array.Empty<ExperimentalFreightWeightedArc>(),
                    diagnostics);
                trees.Add(group, routes);
            }

            if (start == end ||
                !routes.TryGetValue(end.FineStrategicCellId.Value, out var route) ||
                route.AssetIds.Count == 0)
            {
                if (diagnostics is not null) diagnostics.UnreachableServices++;
                continue;
            }
            var itinerary = route.AssetIds.Select(id => byId[id]).ToArray();
            var price = prices.CreateForItinerary(
                intent, Array.AsReadOnly(itinerary));
            if (price is not ExperimentalAggregatedLandFreightCostFunction exact ||
                exact.GeographicBaseNumerator != route.GeographicWeight)
                throw new InvalidOperationException(
                    "Optimized geographic total must match the independently priced whole itinerary.");

            outputs.Add(new EconomicTransportServiceOffer(
                intent.ServiceId, intent.RouteKey, route.AssetIds,
                Array.Empty<int>(), intent.ExpectedLoadMicrograms, price));
            if (diagnostics is not null) diagnostics.OfferedServices++;
        }
        return Array.AsReadOnly(outputs.ToArray());
    }
}
