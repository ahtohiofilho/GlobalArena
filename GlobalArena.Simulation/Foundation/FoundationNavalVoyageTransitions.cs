using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Isolated physical transition foundation. Not exposed as a
/// gameplay command until collective calendar and finance integration.</summary>
internal sealed class FoundationNavalVoyageTransitions
{
    private readonly PhysicalTacticalIncidenceMap _map;
    private readonly FoundationFineFirstPhysicalGeography _geo;
    private readonly RuntimeWorldBinding _binding;

    internal FoundationNavalVoyageTransitions(WorldGenerationResult world,
        PhysicalTacticalIncidenceMap map, FoundationFineFirstPhysicalGeography geo,
        FoundationFineFirstWorldgenIdentity signature)
    {
        _map = map;
        _geo = geo;
        _binding = RuntimeWorldBinding.FromFineFirst(world, map, geo, signature);
    }

    internal WorldState Deploy(WorldState world, ulong? navalCargoMicroPerWorker = null)
    {
        RequireWorld(world);
        return world.WithPhysicalEdgeTravel(world.Workers, world.EdgeTravels.EnableNavalVoyages(navalCargoMicroPerWorker));
    }

    internal WorldState Embark(WorldState world, PhysicalWorkerId id,
        PhysicalTacticalTileId water, ulong turn, ulong naturalCrossingTimeMicro,
        Func<IReadOnlySet<CivilizationId>, FoundationClosedNodeWorkEvidence?> readPriorWork)
    {
        RequireWorld(world);
        var worker = world.Workers.Workers.Single(w => w.Id == id);
        var origin = worker.TacticalTileId!.Value;
        if (world.EdgeTravels.NavalVoyages is not { } voyages ||
            voyages.Any(v => v.Worker == id) || world.EdgeTravels.Pending.Any(p => p.Worker == id) ||
            worker.Activity == PhysicalWorkerActivity.Traveling)
            throw new InvalidOperationException("Worker cannot embark in current state.");
        var decision = FoundationNavalBoardingPolicy.Evaluate(world, _map, _geo,
            worker.Owner, origin, water, readPriorWork);
        if (!decision.IsAllowed) throw new InvalidOperationException("Boarding denied.");
        var edge = Edge(origin, water);
        var row = world.Transport.Edges.Single(r => r.Edge == edge);
        var duration = FoundationGroundEdgeTravelTimeProjector.FromInfrastructure(row,
            new FoundationGroundEdgeTravelTimePolicy(naturalCrossingTimeMicro)).ImprovedCrossingTimeMicro;
        var voyage = new FoundationNavalVoyage(id, worker.Owner, origin, edge, turn,
            duration, decision.PaidNumerator, decision.PaidDenominator)
        {
            EmbarkationFreightMicroPerWholeCargo = world.EdgeTravels.NavalCargoMicroPerWorker.HasValue
                ? FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(row) : null
        };
        var ledger = world.EdgeTravels.ReplaceNavalVoyage(id, voyage)
            .Replace(id, new(id, worker.Owner, origin, water, edge, duration, duration));
        return Change(world, worker, origin, PhysicalWorkerActivity.Traveling, ledger);
    }

    // Full-map QA continuation only. A vessel may traverse water and
    // disembark at the requested land node, not cross intermediate land.
    // Fixed edge duration makes canonical breadth-first search sufficient.
    internal PhysicalTacticalTileId? FindNextLandingStep(
        PhysicalTacticalTileId source, PhysicalTacticalTileId target, FoundationCivilizationKnowledgeScope? scope = null)
    {
        if (_geo.SurfaceAtIndex(Index(source)) == FoundationFineSurfaceKind.Land ||
            !_geo.CanTraverseOnFoot(Index(target)) ||
            _geo.SurfaceAtIndex(Index(target)) != FoundationFineSurfaceKind.Land)
            return null;
        if (scope is not null && (!scope.KnowsNode(source) || !scope.KnowsNode(target))) return null;
        var start = source.FineStrategicCellId;
        var goal = target.FineStrategicCellId;
        var firstSteps = new Dictionary<StrategicCellId, StrategicCellId>();
        var seen = new HashSet<StrategicCellId> { start };
        var queue = new Queue<StrategicCellId>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var node))
        {
            int a = checked((int)node.Value - 1);
            foreach (var next in _map.FineTopology.Cells[a].AdjacentCellIds.OrderBy(n => n.Value))
            {
                if (scope is not null && !scope.KnowsLink(new(_map.Refinement.FineParameters, node),
                    new(_map.Refinement.FineParameters, next))) continue;
                int b = checked((int)next.Value - 1);
                var kind = _geo.CrossingAtIndices(a, b);
                if (next == goal ? kind != FoundationFineTransportCrossingKind.Port :
                    kind != FoundationFineTransportCrossingKind.OpenSea)
                    continue;
                if (!seen.Add(next)) continue;
                var first = node == start ? next : firstSteps[node];
                if (next == goal) return new PhysicalTacticalTileId(_map.Refinement.FineParameters, first);
                firstSteps.Add(next, first);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    // Direct coastal departure when the ground router has no route.
    // Select the first canonical authorized crossing with a reachable landing.
    internal WorldState? TryDepart(WorldState state, WorldState opening, PhysicalWorkerId id,
        PhysicalTacticalTileId target, ulong turn, ulong naturalCrossingTimeMicro,
        FoundationCivilizationKnowledgeScope? scope)
    {
        var worker = state.Workers.Workers.Single(w => w.Id == id);
        var origin = worker.TacticalTileId!.Value;
        if (_geo.SurfaceAtIndex(Index(origin)) != FoundationFineSurfaceKind.Land ||
            !_geo.CanTraverseOnFoot(Index(target)) || origin == target ||
            (scope is not null && (!scope.KnowsNode(origin) || !scope.KnowsNode(target))))
            return null;
        FoundationClosedNodeWorkEvidence? Read(IReadOnlySet<CivilizationId> eligible) =>
            opening.EdgeTravels.PriorWorkTurn == turn - 1UL && opening.EdgeTravels.PriorWork is { } work
                ? FoundationClosedNodeWorkProjector.Read(work, origin, worker.Owner, eligible) : null;
        foreach (var next in _map.FineTopology.Cells[Index(origin)].AdjacentCellIds.OrderBy(n => n.Value))
        {
            var water = new PhysicalTacticalTileId(_map.Refinement.FineParameters, next);
            if (scope is not null && !scope.KnowsLink(origin, water)) continue;
            if (_geo.CrossingAtIndices(Index(origin), Index(water)) != FoundationFineTransportCrossingKind.Port)
                continue;
            if (!FoundationNavalBoardingPolicy.Evaluate(opening, _map, _geo, worker.Owner, origin, water, Read).IsAllowed)
                continue;
            if (FindNextLandingStep(water, target, scope) is null) continue;
            return Embark(state, id, water, turn, naturalCrossingTimeMicro, Read);
        }
        return null;
    }

    internal WorldState StartNext(WorldState world, PhysicalWorkerId id, PhysicalTacticalTileId destination)
    {
        RequireWorld(world);
        var worker = world.Workers.Workers.Single(w => w.Id == id);
        var voyage = world.EdgeTravels.NavalVoyages?.SingleOrDefault(v => v.Worker == id)
            ?? throw new InvalidOperationException("No active vessel.");
        if (world.EdgeTravels.Pending.Any(p => p.Worker == id))
            throw new InvalidOperationException("Complete the current naval crossing first.");
        var source = worker.TacticalTileId!.Value;
        var edge = Edge(source, destination);
        var kind = _geo.CrossingAtIndices(Index(source), Index(destination));
        if (_geo.SurfaceAtIndex(Index(source)) == FoundationFineSurfaceKind.Land ||
            (kind != FoundationFineTransportCrossingKind.OpenSea &&
             (kind != FoundationFineTransportCrossingKind.Port ||
              _geo.SurfaceAtIndex(Index(destination)) != FoundationFineSurfaceKind.Land ||
              !_geo.CanTraverseOnFoot(Index(destination)))))
            throw new InvalidOperationException("Invalid naval continuation.");
        var duration = voyage.CrossingTimeMicro;
        var ledger = world.EdgeTravels.Replace(id, new(id, worker.Owner, source,
            destination, edge, duration, duration));
        return Change(world, worker, source, PhysicalWorkerActivity.Traveling, ledger);
    }

    internal (WorldState State, ulong UsedMicro, ulong ResidualMicro, FoundationNavalVoyage? Landed)
        Advance(WorldState world, PhysicalWorkerId id, ulong availableMicro)
    {
        RequireWorld(world);
        if (availableMicro == 0 || availableMicro > 1_000_000UL)
            throw new ArgumentOutOfRangeException(nameof(availableMicro));
        var worker = world.Workers.Workers.Single(w => w.Id == id);
        var voyage = world.EdgeTravels.NavalVoyages?.SingleOrDefault(v => v.Worker == id)
            ?? throw new InvalidOperationException("No active vessel.");
        var pending = world.EdgeTravels.Pending.Single(p => p.Worker == id);
        if (Edge(pending.Source, pending.Destination) != pending.Edge)
            throw new InvalidOperationException("Invalid pending naval crossing.");
        ulong used = Math.Min(availableMicro, pending.RemainingMicro);
        ulong remaining = pending.RemainingMicro - used;
        var ledger = world.EdgeTravels.Replace(id, remaining == 0 ? null :
            pending with { RemainingMicro = remaining });
        var node = remaining == 0 ? pending.Destination : pending.Source;
        bool landed = remaining == 0 && _geo.SurfaceAtIndex(Index(node)) == FoundationFineSurfaceKind.Land;
        if (landed) ledger = ledger.ReplaceNavalVoyage(id, null);
        var state = Change(world, worker, node,
            landed ? PhysicalWorkerActivity.Other : PhysicalWorkerActivity.Traveling, ledger);
        return (state, used, availableMicro - used, landed ? voyage : null);
    }

    private void RequireWorld(WorldState world)
    {
        if (world.WorldBinding != _binding || !world.EdgeTravels.IsInitialized ||
            !world.Transport.IsInitialized)
            throw new InvalidOperationException("Naval travel requires its signed physical world.");
    }

    private int Index(PhysicalTacticalTileId node)
    {
        if (!node.IsValid || node.FineGoldbergParameters != _map.Refinement.FineParameters ||
            node.FineStrategicCellId.Value > (ulong)_map.FineTopology.Cells.Count)
            throw new InvalidOperationException("Invalid naval node.");
        return checked((int)node.FineStrategicCellId.Value - 1);
    }

    private PhysicalTacticalEdgeId Edge(PhysicalTacticalTileId source, PhysicalTacticalTileId destination)
    {
        if (!_map.FineTopology.Cells[Index(source)].AdjacentCellIds.Contains(destination.FineStrategicCellId))
            throw new InvalidOperationException("Naval travel must traverse adjacent nodes.");
        _ = Index(destination);
        var edge = _map.FineTopology.Edges.Single(e =>
            e.IncidentCellIds.Contains(source.FineStrategicCellId) &&
            e.IncidentCellIds.Contains(destination.FineStrategicCellId));
        return new(_map.Refinement.FineParameters, edge.Id);
    }

    private WorldState Change(WorldState world, PhysicalWorkerRuntimeRecord worker,
        PhysicalTacticalTileId node, PhysicalWorkerActivity activity, FoundationEdgeTravelRuntimeState ledger)
    {
        var regions = _map.TileIncidences[Index(node)].IncidentCoarseCellIds;
        var region = regions.Contains(worker.CellId) ? worker.CellId : regions[0];
        var updated = worker.WithTacticalLocationAndActivity(node, region, activity);
        var roster = new PhysicalWorkerRuntimeState(world.Workers.Workers.Select(w =>
            w.Id == worker.Id ? updated : w), tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        return world.WithPhysicalEdgeTravel(roster, ledger).AdvanceRevision();
    }
}
