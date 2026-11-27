using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public enum FoundationConstructionApproachStatus { AtSite, RouteAvailable, CrossingInProgress, Unreachable, NoBuildHead }

public sealed record FoundationConstructionApproach(
    FoundationConstructionApproachStatus Status,
    ulong? ProjectId,
    IReadOnlyList<PhysicalTacticalTileId> Path,
    BigInteger RemainingTravelMicro);

/// <summary>Only a head Build intention may request approach travel. Route uses the
/// actual signed walking surface and authoritative R166 physical time, not freight prices.
/// No second persistent route ledger; unfinished crossings retain R167 duration.</summary>
public sealed class FoundationConstructionApproachRouter
{
    private readonly PhysicalTacticalIncidenceMap _map;
    private readonly RuntimeWorldBinding _binding;
    private readonly RuntimeWorldBinding _legacyBinding;
    private readonly FoundationFineFirstPhysicalGeography _geo;
    private readonly PhysicalTacticalWorkerMoveEventExecutor _movement;
    private readonly FoundationGroundEdgeTravelTimePolicy _policy;

    public FoundationConstructionApproachRouter(WorldGenerationResult world,
        PhysicalTacticalIncidenceMap map, FoundationFineFirstPhysicalGeography geo,
        FoundationFineFirstWorldgenIdentity signature, FoundationGroundEdgeTravelTimePolicy policy)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _geo = geo ?? throw new ArgumentNullException(nameof(geo));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _policy.RequireValid();
        _movement = new(world, map, geo, signature);
        _binding = RuntimeWorldBinding.FromFineFirst(world, map, geo, signature);
        _legacyBinding = RuntimeWorldBinding.FromGeneratedWorld(world);
        NavalTransitions = new FoundationNavalVoyageTransitions(world, map, geo, signature);
    }

    public FoundationConstructionApproach Plan(WorldState world, PhysicalWorkerId workerId)
    {
        if ((world.WorldBinding != _binding && world.WorldBinding != _legacyBinding) || !world.WorkerOrders.IsInitialized || !world.EdgeTravels.IsInitialized ||
            !world.Constructions.IsInitialized || !world.Transport.IsInitialized)
            throw new InvalidOperationException("Approach requires canonical queues, journeys and projects.");
        var worker = world.Workers.Workers.Single(w => w.Id == workerId);
        var queue = world.WorkerOrders.Queues.Single(q => q.Worker == workerId);
        var head = queue.Orders.FirstOrDefault();
        if (head is null || head.Kind != FoundationWorkerOrderKind.Build)
            return new(FoundationConstructionApproachStatus.NoBuildHead, null,
                Array.Empty<PhysicalTacticalTileId>(), BigInteger.Zero);
        var project = world.Constructions.Projects.Single(p => p.Id == head.ProjectId);
        if (project.IsComplete || project.IsActivated)
            return new(FoundationConstructionApproachStatus.NoBuildHead, project.Id,
                Array.Empty<PhysicalTacticalTileId>(), BigInteger.Zero);
        var pending = world.EdgeTravels.Pending.SingleOrDefault(p => p.Worker == workerId);
        if (pending is not null)
            return new(FoundationConstructionApproachStatus.CrossingInProgress, project.Id,
                Array.AsReadOnly(new[] { pending.Source, pending.Destination }), pending.RemainingMicro);

        var source = worker.TacticalTileId!.Value;
        var fine = _map.Refinement.FineParameters;
        if (source.FineGoldbergParameters != fine || project.Edge.FineGoldbergParameters != fine)
            throw new InvalidOperationException("Foreign approach geometry.");
        var site = _map.FineTopology.Edges.Single(e => e.Id == project.Edge.FineStrategicEdgeId);
        int a = checked((int)site.IncidentCellIds[0].Value - 1);
        int b = checked((int)site.IncidentCellIds[1].Value - 1);
        var kind = _geo.CrossingAtIndices(a, b);
        var goals = site.IncidentCellIds.Where(n =>
            (kind == FoundationFineTransportCrossingKind.Ground ||
             (kind == FoundationFineTransportCrossingKind.Port &&
              _geo.FineElevationRaw[checked((int)n.Value - 1)] > _geo.SeaLevelRaw)) &&
            _geo.CanTraverseOnFoot(checked((int)n.Value - 1))).ToHashSet();
        return FindPath(world, source, goals, project.Id);
    }

    public FoundationConstructionApproach PlanMove(WorldState world, PhysicalWorkerId workerId,
        PhysicalTacticalTileId destination)
    {
        if ((world.WorldBinding != _binding && world.WorldBinding != _legacyBinding) ||
            !world.WorkerOrders.IsInitialized || !world.EdgeTravels.IsInitialized || !world.Transport.IsInitialized)
            throw new InvalidOperationException("Move requires canonical physical orders.");
        var worker = world.Workers.Workers.Single(w => w.Id == workerId);
        var source = worker.TacticalTileId!.Value;
        if (destination.FineGoldbergParameters != _map.Refinement.FineParameters ||
            !destination.IsValid || destination.FineStrategicCellId.Value > _map.Refinement.FineParameters.StrategicCellCount)
            throw new InvalidOperationException("Foreign move destination.");
        var pending = world.EdgeTravels.Pending.SingleOrDefault(p => p.Worker == workerId);
        if (pending is not null)
            return new(FoundationConstructionApproachStatus.CrossingInProgress, null,
                Array.AsReadOnly(new[] { pending.Source, pending.Destination }), pending.RemainingMicro);
        var goals = new HashSet<StrategicCellId>();
        if (_geo.CanTraverseOnFoot(checked((int)destination.FineStrategicCellId.Value - 1)))
            goals.Add(destination.FineStrategicCellId);
        return FindPath(world, source, goals, null);
    }

    private FoundationConstructionApproach FindPath(WorldState world, PhysicalTacticalTileId source,
        HashSet<StrategicCellId> goals, ulong? projectId)
    {
        var fine = _map.Refinement.FineParameters;
        if (goals.Contains(source.FineStrategicCellId))
            return new(FoundationConstructionApproachStatus.AtSite, projectId,
                Array.AsReadOnly(new[] { source }), BigInteger.Zero);
        if (goals.Count == 0 || !_geo.CanTraverseOnFoot(checked((int)source.FineStrategicCellId.Value - 1)))
            return new(FoundationConstructionApproachStatus.Unreachable, projectId,
                Array.Empty<PhysicalTacticalTileId>(), BigInteger.Zero);

        var inventory = world.Transport.Edges.ToDictionary(e => e.Edge);
        var graph = new Dictionary<StrategicCellId, List<(StrategicCellId To, ulong Cost)>>();
        foreach (var edge in _map.FineTopology.Edges)
        {
            var x = edge.IncidentCellIds[0]; var y = edge.IncidentCellIds[1];
            if (!_geo.CanTraverseOnFoot(checked((int)x.Value - 1)) ||
                !_geo.CanTraverseOnFoot(checked((int)y.Value - 1))) continue;
            var key = new PhysicalTacticalEdgeId(fine, edge.Id);
            if (_movement.CheckEdge(new(fine, x), new(fine, y)) != key)
                throw new InvalidOperationException("Approach graph disagrees with movement authority.");
            var cost = FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(inventory[key], _policy).ImprovedCrossingTimeMicro;
            if (!graph.TryGetValue(x, out var gx)) graph[x] = gx = new();
            if (!graph.TryGetValue(y, out var gy)) graph[y] = gy = new();
            gx.Add((y, cost)); gy.Add((x, cost));
        }
        var start = source.FineStrategicCellId;
        var distances = new Dictionary<StrategicCellId, BigInteger> { [start] = BigInteger.Zero };
        var prev = new Dictionary<StrategicCellId, StrategicCellId>();
        var frontier = new PriorityQueue<StrategicCellId, (BigInteger, ulong)>();
        frontier.Enqueue(start, (BigInteger.Zero, start.Value));
        while (frontier.TryDequeue(out var node, out var priority))
        {
            if (distances[node] != priority.Item1) continue;
            if (goals.Contains(node))
            {
                var path = new List<PhysicalTacticalTileId> { new(fine, node) };
                var cursor = node;
                while (cursor != start)
                {
                    cursor = prev[cursor]; path.Add(new(fine, cursor));
                }
                path.Reverse();
                return new(FoundationConstructionApproachStatus.RouteAvailable, projectId,
                    Array.AsReadOnly(path.ToArray()), priority.Item1);
            }
            if (!graph.TryGetValue(node, out var neighbors)) continue;
            foreach (var next in neighbors.OrderBy(n => n.To.Value))
            {
                var candidate = priority.Item1 + next.Cost;
                if (distances.TryGetValue(next.To, out var old) && candidate >= old) continue;
                distances[next.To] = candidate; prev[next.To] = node;
                frontier.Enqueue(next.To, (candidate, next.To.Value));
            }
        }
        return new(FoundationConstructionApproachStatus.Unreachable, projectId,
            Array.Empty<PhysicalTacticalTileId>(), BigInteger.Zero);
    }

    internal FoundationNavalVoyageTransitions NavalTransitions { get; }
    internal PhysicalTacticalIncidenceMap Incidence => _map;
    internal FoundationFineFirstPhysicalGeography Geography => _geo;
    internal PhysicalTacticalWorkerMoveEventExecutor Movement => _movement;
    internal FoundationGroundEdgeTravelTimePolicy Policy => _policy;

    // One existing R167 crossing per invocation; general residual scheduling belongs to R210.
    public (WorldState State, FoundationEdgeTravelTransitionReceipt? Receipt) Advance(
        WorldState world, PhysicalWorkerId worker)
    {
        var approach = Plan(world, worker);
        if (approach.Status == FoundationConstructionApproachStatus.CrossingInProgress)
            return FoundationPersistedEdgeTravelTransitions.Advance(world, worker, _movement);
        if (approach.Status == FoundationConstructionApproachStatus.RouteAvailable)
            return FoundationPersistedEdgeTravelTransitions.StartAndAdvance(
                world, worker, approach.Path[1], _movement, _policy);
        return (world, null);
    }
}
