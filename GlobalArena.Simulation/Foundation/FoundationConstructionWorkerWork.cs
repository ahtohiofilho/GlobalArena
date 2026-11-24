using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationConstructionWorkerAssignment(
    PhysicalWorkerId Worker, ulong ProjectId);

public sealed record FoundationConstructionWorkerReceipt(
    PhysicalWorkerId Worker, ulong ProjectId, ulong UsedWorkMicro, ulong ResidualWorkMicro);

public sealed record FoundationConstructionWorkResult(
    WorldState Snapshot, IReadOnlyList<FoundationConstructionWorkerReceipt> Workers);

/// <summary>
/// Opt-in isolated turn calculation. Only stationary, unassigned Other
/// workers may contribute. No production, travel or logistics worker can
/// be allocated here. Caller must invoke once for the entire turn, not
/// once per project. Host/journal integration is a separate gate.
/// </summary>
public static class FoundationConstructionWorkerWork
{
    public static FoundationConstructionWorkResult Apply(
        WorldState world, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        IReadOnlyList<FoundationConstructionWorkerAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(assignments);
        if (!world.Constructions.IsInitialized ||
            !world.Workers.HasAuthoritativeOccupations ||
            !world.Workers.HasAuthoritativeTacticalLocations ||
            geography.FineParameters != incidence.Refinement.FineParameters ||
            geography.CoarseParameters != incidence.Refinement.CoarseParameters ||
            geography.FineElevationRaw.Count != incidence.FineTopology.Cells.Count)
            throw new InvalidOperationException("Construction requires matching physical world and initialized projects.");

        var people = world.Workers.Workers.ToDictionary(w => w.Id);
        var projects = world.Constructions.Projects.ToDictionary(p => p.Id);
        var edges = incidence.FineTopology.Edges.ToDictionary(
            e => new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, e.Id));
        var seen = new HashSet<PhysicalWorkerId>();
        foreach (var a in assignments)
        {
            if (a is null || !a.Worker.IsValid || !seen.Add(a.Worker) ||
                !people.TryGetValue(a.Worker, out var worker) ||
                worker.Activity != PhysicalWorkerActivity.Other ||
                worker.Occupation != PhysicalWorkerOccupation.Unassigned ||
                worker.TacticalTileId is not { } tile ||
                world.EdgeTravels.Pending.Any(p => p.Worker == worker.Id) ||
                !projects.TryGetValue(a.ProjectId, out var project) ||
                project.IsComplete || project.Investor != worker.Owner ||
                !edges.TryGetValue(project.Edge, out var edge) ||
                tile.FineGoldbergParameters != incidence.Refinement.FineParameters ||
                !edge.IncidentCellIds.Contains(tile.FineStrategicCellId))
                throw new InvalidOperationException("Worker must be unique, idle, owned and physically incident to an unfinished project.");
            int first = checked((int)edge.IncidentCellIds[0].Value - 1);
            int second = checked((int)edge.IncidentCellIds[1].Value - 1);
            var kind = geography.CrossingAtIndices(first, second);
            if (kind != FoundationFineTransportCrossingKind.Ground &&
                (kind != FoundationFineTransportCrossingKind.Port ||
                 geography.FineElevationRaw[checked((int)tile.FineStrategicCellId.Value - 1)] <= geography.SeaLevelRaw))
                throw new InvalidOperationException("Construction requires a ground crossing or land terminal of a port.");
        }

        const ulong capacity = FoundationConstructionWorkPolicy.NeutralIndividualProductivityMicro;
        var receipts = new List<FoundationConstructionWorkerReceipt>();
        foreach (var group in assignments.GroupBy(a => a.ProjectId).OrderBy(g => g.Key))
        {
            var project = projects[group.Key];
            var team = group.OrderBy(a => a.Worker.Value).ToArray();
            ulong offered = checked((ulong)team.Length * capacity);
            var progress = new FoundationConstructionProgress(project.FundedMoneyMicro,
                project.RequiredWorkMicro, project.CompletedWorkMicro).Apply(offered);
            ulong used = progress.UsedEffortMicro;
            // Simultaneous equal-productivity builders share the completion
            // time equally (within one micro-unit), not first-worker priority.
            ulong each = used / (ulong)team.Length;
            ulong remainder = used % (ulong)team.Length;
            for (int i = 0; i < team.Length; i++)
            {
                ulong share = each + ((ulong)i < remainder ? 1UL : 0UL);
                receipts.Add(new FoundationConstructionWorkerReceipt(team[i].Worker,
                    project.Id, share, capacity - share));
            }
            projects[project.Id] = project with
            {
                CompletedWorkMicro = progress.Progress.CompletedEffortMicro
            };
        }
        var next = assignments.Count == 0 ? world : world.WithConstructions(
            new FoundationConstructionRuntimeState(projects.Values));
        return new FoundationConstructionWorkResult(next,
            Array.AsReadOnly(receipts.OrderBy(r => r.Worker.Value).ToArray()));
    }
}
