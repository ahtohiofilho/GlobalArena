using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// One explicit, read-only proposal to allocate an existing individual to
/// operations at their CURRENT physical tile. This is not a persisted worker
/// activity, command or authorization to stop producing. Until the canonical
/// activity model supports logistics, only existing Other workers can serve.
/// </summary>
public sealed record FoundationOperationalWorkerAssignment(
    PhysicalWorkerId Worker,
    FoundationOperationalWorkKind Kind);

public sealed record FoundationPhysicalOperationalNodeAllocation(
    PhysicalTacticalTileId Tile,
    CivilizationId Owner,
    FoundationOperationalWorkAllocation Allocation);

/// <summary>
/// R146 proof bridge: real workers -> their real physical nodes -> real
/// land/coast crossings -> R145 pure proportional allocator. Nothing here
/// changes worker occupation, money, freight, cargo, world state or journal.
/// </summary>
public static class FoundationPhysicalOperationalWorkProjector
{
    public static IReadOnlyList<FoundationPhysicalOperationalNodeAllocation> Project(
        WorldState world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        IReadOnlyList<FoundationOperationalWorkerAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(assignments);
        var fine = incidence.Refinement.FineParameters;
        if (!world.Transport.IsInitialized ||
            !world.Workers.IsInitialized ||
            !world.Workers.HasAuthoritativeTacticalLocations ||
            geography.FineParameters != fine ||
            geography.CoarseParameters != incidence.Refinement.CoarseParameters ||
            geography.FineElevationRaw.Count != incidence.FineTopology.Cells.Count)
            throw new InvalidOperationException(
                "Operational work requires one matching real fine physical world.");

        var people = world.Workers.Workers.ToDictionary(w => w.Id);
        var pools = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner),
            (ulong Land, ulong Port)>();
        var assigned = new HashSet<PhysicalWorkerId>();
        foreach (var proposal in assignments)
        {
            if (proposal is null ||
                (proposal.Kind != FoundationOperationalWorkKind.Land &&
                 proposal.Kind != FoundationOperationalWorkKind.Port) ||
                !assigned.Add(proposal.Worker) ||
                !people.TryGetValue(proposal.Worker, out var worker) ||
                worker.Activity != PhysicalWorkerActivity.Other ||
                worker.TacticalTileId is not { } location ||
                location.FineGoldbergParameters != fine)
                throw new ArgumentException(
                    "Each allocated individual must be a unique real Other worker at a fine tile.",
                    nameof(assignments));
            var key = (location, worker.Owner);
            pools.TryGetValue(key, out var pool);
            const ulong effortPerWorkerMicro = 1_000_000UL;
            pools[key] = proposal.Kind == FoundationOperationalWorkKind.Land
                ? (checked(pool.Land + effortPerWorkerMicro), pool.Port)
                : (pool.Land, checked(pool.Port + effortPerWorkerMicro));
        }

        // Exactly one canonical transport row per real fine crossing.
        var transport = world.Transport.Edges.ToDictionary(e => e.Edge);
        if (transport.Count != incidence.FineTopology.Edges.Count)
            throw new InvalidOperationException(
                "Transport inventory does not cover real fine crossings.");

        var incident = new Dictionary<PhysicalTacticalTileId,
            List<FoundationOperationalWorkEdge>>();
        foreach (var geometric in incidence.FineTopology.Edges)
        {
            var edgeId = new PhysicalTacticalEdgeId(fine, geometric.Id);
            if (!transport.TryGetValue(edgeId, out var row))
                throw new InvalidOperationException(
                    "Transport inventory is missing a physical crossing.");
            var a = geometric.IncidentCellIds[0];
            var b = geometric.IncidentCellIds[1];
            int ai = checked((int)a.Value - 1);
            int bi = checked((int)b.Value - 1);
            var kind = geography.CrossingAtIndices(ai, bi);
            FoundationOperationalWorkKind? operation = kind switch
            {
                FoundationFineTransportCrossingKind.Ground =>
                    FoundationOperationalWorkKind.Land,
                FoundationFineTransportCrossingKind.Port =>
                    FoundationOperationalWorkKind.Port,
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary => null,
                FoundationFineTransportCrossingKind.OpenSea => null,
                _ => throw new InvalidOperationException(
                    "Unknown physical crossing kind.")
            };
            if (operation is null) continue;
            ulong natural = row.NaturalConstructionCostMoneyMicro
                ?? throw new InvalidOperationException(
                    "Finite operational crossing lacks a construction baseline.");
            if (natural == 0UL)
                throw new InvalidOperationException(
                    "Operational crossing has no positive natural weight.");
            ulong invested = row.CommittedInvestmentMoneyMicro.GetValueOrDefault();
            var weighted = new FoundationOperationalWorkEdge(
                edgeId, operation.Value, natural, invested);
            foreach (var cell in geometric.IncidentCellIds)
            {
                // A port is served by the land terminal, never by the water tile.
                int index = checked((int)cell.Value - 1);
                if (operation == FoundationOperationalWorkKind.Port &&
                    geography.FineElevationRaw[index] <= geography.SeaLevelRaw)
                    continue;
                var tile = new PhysicalTacticalTileId(fine, cell);
                if (!incident.TryGetValue(tile, out var list))
                    incident.Add(tile, list = new List<FoundationOperationalWorkEdge>());
                list.Add(weighted);
            }
        }

        var answer = new List<FoundationPhysicalOperationalNodeAllocation>();
        foreach (var item in pools.OrderBy(p => p.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(p => p.Key.Owner.Value))
        {
            incident.TryGetValue(item.Key.Tile, out var edges);
            var allocation = FoundationOperationalWorkAllocator.Allocate(
                item.Value.Land, item.Value.Port,
                edges ?? Array.Empty<FoundationOperationalWorkEdge>());
            answer.Add(new FoundationPhysicalOperationalNodeAllocation(
                item.Key.Tile, item.Key.Owner, allocation));
        }
        return Array.AsReadOnly(answer.ToArray());
    }
}
