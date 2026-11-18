using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// One explicitly requested work assignment, not an accepted turn order.
/// A physically present real worker can be assigned to at most ONE crossing
/// in this candidate. The issuer must own the worker. A proposal never
/// creates workers, transport assets, construction progress or money.
/// </summary>
public sealed record FoundationPhysicalConstructionWorkAssignment(
    CivilizationId Issuer,
    PhysicalWorkerId WorkerId,
    PhysicalTacticalEdgeId Edge);

/// <summary>
/// Explicit work efficiency for one available nonproducing physical worker
/// in one potential construction turn. This is NOT the worker's commodity
/// production rate, demand multiplier, wallet, or a resource inventory.
/// </summary>
public sealed class FoundationPhysicalConstructionLaborPolicy
{
    public ulong LaborMicroPerWorkerPerTurn { get; }

    public FoundationPhysicalConstructionLaborPolicy(
        ulong laborMicroPerWorkerPerTurn)
    {
        if (laborMicroPerWorkerPerTurn == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(laborMicroPerWorkerPerTurn),
                "Real construction work capacity must be explicitly positive.");
        LaborMicroPerWorkerPerTurn = laborMicroPerWorkerPerTurn;
    }
}

public sealed record FoundationPhysicalConstructionWorkerContribution(
    PhysicalWorkerId WorkerId,
    CivilizationId Owner,
    PhysicalTacticalTileId WorkerTile,
    PhysicalTacticalEdgeId Edge,
    ulong ContributedLaborMicro,
    ulong UnusedLaborMicro);

public sealed record FoundationPhysicalConstructionEdgeProposal(
    PhysicalTacticalEdgeId Edge,
    ulong RequiredLaborMicro,
    int AssignedWorkerCount,
    ulong CreditableLaborMicro,
    ulong UnusedLaborMicro,
    bool MeetsLaborRequirement,
    ulong PreviouslyCommittedLaborMicro = 0UL);

/// <summary>
/// Read-only potential work for ONE candidate turn, using the already
/// authoritative worker census and v13 natural construction requirements.
/// IsCommitted is always FALSE: no infrastructure upgrade, worker action,
/// material consumption, treasury transfer, journal or economic settlement
/// is published. A labor-complete edge is NOT a completed road.
/// </summary>
public sealed class FoundationPhysicalConstructionWorkProposal
{
    public WorldStateHash SourceWorldHash { get; }
    public IReadOnlyList<FoundationPhysicalConstructionWorkerContribution>
        Workers { get; }
    public IReadOnlyList<FoundationPhysicalConstructionEdgeProposal> Edges { get; }
    public ulong AvailableLaborMicro { get; }
    public ulong CreditableLaborMicro { get; }
    public ulong UnusedLaborMicro { get; }
    public bool IsCommitted => false;

    internal FoundationPhysicalConstructionWorkProposal(
        WorldStateHash sourceHash,
        FoundationPhysicalConstructionWorkerContribution[] workers,
        FoundationPhysicalConstructionEdgeProposal[] edges,
        ulong available, ulong creditable, ulong unused)
    {
        SourceWorldHash = sourceHash;
        Workers = Array.AsReadOnly(workers);
        Edges = Array.AsReadOnly(edges);
        AvailableLaborMicro = available;
        CreditableLaborMicro = creditable;
        UnusedLaborMicro = unused;
    }
}

/// <summary>
/// O(W+E+A log A) candidate where W = real workers, E = fine crossings and
/// A = explicitly assigned workers. The complete v13 construction schema
/// comes ONLY from the bound authoritative world; no caller can substitute
/// a fabricated construction requirement. Every worker is counted at most
/// once and at most its explicit per-turn labor capacity is creditable.
/// </summary>
public static class FoundationPhysicalConstructionWorkPlanner
{
    public static FoundationPhysicalConstructionWorkProposal PlanCandidate(
        WorldState snapshot,
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationPhysicalConstructionLaborPolicy policy,
        IEnumerable<FoundationPhysicalConstructionWorkAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(assignments);

        if (!snapshot.Transport.HasNaturalConstructionLabor ||
            !snapshot.Workers.HasAuthoritativeTacticalLocations ||
            !snapshot.Workers.HasAuthoritativeOccupations ||
            snapshot.WorldBinding !=
                RuntimeWorldBinding.FromGeneratedWorld(generatedWorld) ||
            incidence.Refinement.CoarseParameters !=
                generatedWorld.Request.StrategicParameters)
            throw new InvalidOperationException(
                "Physical construction requires bound v13 construction " +
                "requirements and authoritative real tactical workers.");

        var fine = incidence.Refinement.FineParameters;
        var topology = incidence.FineTopology.Edges.ToDictionary(
            edge => new PhysicalTacticalEdgeId(fine, edge.Id),
            edge => (First: new PhysicalTacticalTileId(
                    fine, edge.IncidentCellIds[0]),
                Second: new PhysicalTacticalTileId(
                    fine, edge.IncidentCellIds[1])));
        var inventory = snapshot.Transport.Edges.ToDictionary(
            edge => edge.Edge);
        if (inventory.Count != topology.Count ||
            inventory.Keys.Any(edge => !topology.ContainsKey(edge)))
            throw new InvalidOperationException(
                "Construction inventory differs from the real bound topology.");

        var workers = snapshot.Workers.Workers.ToDictionary(x => x.Id);
        var staged = new Dictionary<
            PhysicalTacticalEdgeId,
            List<PhysicalWorkerRuntimeRecord>>();
        var seen = new HashSet<PhysicalWorkerId>();
        foreach (var assignment in assignments)
        {
            if (assignment is null ||
                !assignment.Issuer.IsValid ||
                !assignment.WorkerId.IsValid ||
                !assignment.Edge.IsValid ||
                !workers.TryGetValue(assignment.WorkerId, out var worker) ||
                worker.Owner != assignment.Issuer ||
                worker.Activity != PhysicalWorkerActivity.Other ||
                !seen.Add(assignment.WorkerId) ||
                !inventory.TryGetValue(assignment.Edge, out var road) ||
                road.OwnershipKind !=
                    PhysicalTransportOwnershipKind.NoInfrastructure ||
                road.CommittedConstructionLaborMicro >=
                    road.NaturalConstructionLaborMicro ||
                !topology.TryGetValue(assignment.Edge, out var endpoints) ||
                (worker.TacticalTileId != endpoints.First &&
                 worker.TacticalTileId != endpoints.Second))
                throw new InvalidOperationException(
                    "Candidate construction requires an available real " +
                    "nonproducing owner-controlled worker on the actual " +
                    "natural crossing, assigned at most once.");
            if (!staged.TryGetValue(assignment.Edge, out var people))
                staged.Add(assignment.Edge, people = new());
            people.Add(worker);
        }

        var workerContributions =
            new List<FoundationPhysicalConstructionWorkerContribution>(
                seen.Count);
        var edgeProposals = new List<FoundationPhysicalConstructionEdgeProposal>(
            staged.Count);
        ulong available = 0UL, creditable = 0UL, unused = 0UL;
        foreach (var pair in staged.OrderBy(p =>
            p.Key.FineStrategicEdgeId.Value))
        {
            ulong required = inventory[pair.Key].NaturalConstructionLaborMicro ??
                throw new InvalidOperationException(
                    "The canonical v13 labor requirement is missing.");
            ulong already = inventory[pair.Key].CommittedConstructionLaborMicro
                ?? 0UL;
            if (already >= required)
                throw new InvalidOperationException(
                    "No additional construction labor is needed on this edge.");
            ulong remaining = required - already;
            ulong edgeCredited = 0UL, edgeUnused = 0UL;
            foreach (var worker in pair.Value.OrderBy(w => w.Id.Value))
            {
                ulong capacity = policy.LaborMicroPerWorkerPerTurn;
                ulong credited = Math.Min(capacity, remaining);
                ulong left = capacity - credited;
                remaining -= credited;
                available = checked(available + capacity);
                creditable = checked(creditable + credited);
                unused = checked(unused + left);
                edgeCredited = checked(edgeCredited + credited);
                edgeUnused = checked(edgeUnused + left);
                workerContributions.Add(
                    new FoundationPhysicalConstructionWorkerContribution(
                        worker.Id, worker.Owner, worker.TacticalTileId!.Value,
                        pair.Key, credited, left));
            }
            edgeProposals.Add(new FoundationPhysicalConstructionEdgeProposal(
                pair.Key, required, pair.Value.Count, edgeCredited,
                edgeUnused, remaining == 0UL, already));
        }
        if (checked(creditable + unused) != available ||
            workerContributions.Count != seen.Count ||
            edgeProposals.Sum(e => (long)e.AssignedWorkerCount) != seen.Count)
            throw new InvalidOperationException(
                "Construction work cannot invent or lose physical labor.");

        return new FoundationPhysicalConstructionWorkProposal(
            new CanonicalWorldStateHasher().Compute(snapshot),
            workerContributions.ToArray(), edgeProposals.ToArray(),
            available, creditable, unused);
    }
}
