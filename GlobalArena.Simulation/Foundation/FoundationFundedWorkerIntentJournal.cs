using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Queue insertion position is explicit; no existing order is silently replaced.</summary>
public sealed record FoundationFundedWorkerInsertion(PhysicalWorkerId Worker, int Index);

public sealed record FoundationFundedWorkerIntentEvent(
    EventId Id, CivilizationId Investor, PhysicalTacticalEdgeId Edge,
    ulong MoneyMicro, IReadOnlyList<FoundationFundedWorkerInsertion> Assignments,
    ulong ProjectId, WorldStateHash Before, WorldStateHash After) : ISimulationEvent;

public sealed record FoundationProductiveFundingEvent(
    EventId Id, CivilizationId Investor, FoundationProductiveTarget Target,
    ulong MoneyMicro, ulong RequiredWorkMicro, ulong ProjectId,
    WorldStateHash Before, WorldStateHash After) : ISimulationEvent;

/// <summary>
/// One immutable transaction: fund the canonical project, bind its actual
/// allocated ID to each selected worker's queue. No travel or effort here.
/// Failed validation publishes neither money debit nor queue changes.
/// </summary>
public sealed class FoundationFundedWorkerIntentJournal : ISimulationEventExecutor
{
    private readonly FoundationConstructionFundingExecutor _funding;
    private static readonly CanonicalWorldStateHasher Hasher = new();

    public FoundationFundedWorkerIntentJournal(FoundationConstructionFundingExecutor funding)
        => _funding = funding ?? throw new ArgumentNullException(nameof(funding));

    private (WorldState Next, ulong ProjectId) Apply(WorldState world, EventId id,
        CivilizationId investor, PhysicalTacticalEdgeId edge, ulong moneyMicro,
        IReadOnlyList<FoundationFundedWorkerInsertion> assignments, SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(assignments);
        if (!world.WorkerOrders.IsInitialized || !world.Constructions.IsInitialized ||
            assignments.Any(a => a is null || !a.Worker.IsValid || a.Index < 0) ||
            assignments.Select(a => a.Worker).Distinct().Count() != assignments.Count)
            throw new InvalidOperationException("Invalid construction queue transaction.");
        // Funding alone is valid. Remote workers and currently occupied workers
        // may receive intentions; arrival and scheduling are separate phases.
        var fundingEvent = new FoundationConstructionFundedEvent(id, investor, edge,
            moneyMicro, _funding.Quote(moneyMicro), world.Revision);
        var funded = _funding.Execute(world, fundingEvent, context);
        var project = funded.Constructions.Projects.Single(p => p.Edge == edge && !p.IsActivated);
        var next = funded;
        foreach (var assignment in assignments.OrderBy(a => a.Worker.Value))
            next = next.InsertWorkerOrder(investor, assignment.Worker, assignment.Index,
                FoundationWorkerOrderKind.Build, projectId: project.Id);
        return (next, project.Id);
    }

    public (WorldState Next, FoundationFundedWorkerIntentEvent Event) Record(
        WorldState world, EventId id, CivilizationId investor, PhysicalTacticalEdgeId edge,
        ulong moneyMicro, IEnumerable<FoundationFundedWorkerInsertion> assignments,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        var copy = assignments.ToArray();
        var (next, projectId) = Apply(world, id, investor, edge, moneyMicro, copy, context);
        return (next, new(id, investor, edge, moneyMicro,
            Array.AsReadOnly(copy.OrderBy(a => a.Worker.Value).ToArray()),
            projectId, Hasher.Compute(world), Hasher.Compute(next)));
    }

    internal (WorldState Next, FoundationProductiveFundingEvent Event) RecordProductive(
        WorldState world, EventId id, CivilizationId investor, FoundationProductiveTarget target,
        ulong moneyMicro, SimulationContext context)
    {
        var next = _funding.FundProductive(world, id, investor, target, moneyMicro, context);
        var project = next.ProductiveInfrastructure.Projects.Single(p => p.Target == target && !p.IsActivated);
        return (next, new(id, investor, target, moneyMicro, project.RequiredWorkMicro,
            project.Id, Hasher.Compute(world), Hasher.Compute(next)));
    }

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent, SimulationContext context)
    {
        if (simulationEvent is FoundationProductiveFundingEvent productive)
        {
            if (Hasher.Compute(state) != productive.Before)
                throw new InvalidOperationException("Stale productive funding event.");
            var (funded, expected) = RecordProductive(state, productive.Id, productive.Investor,
                productive.Target, productive.MoneyMicro, context);
            if (expected != productive)
                throw new InvalidOperationException("Productive funding replay diverged.");
            return funded;
        }
        if (simulationEvent is not FoundationFundedWorkerIntentEvent ev ||
            Hasher.Compute(state) != ev.Before || ev.Assignments is null)
            throw new InvalidOperationException("Invalid or stale funding assignment event.");
        var (next, projectId) = Apply(state, ev.Id, ev.Investor, ev.Edge,
            ev.MoneyMicro, ev.Assignments, context);
        if (projectId != ev.ProjectId || Hasher.Compute(next) != ev.After)
            throw new InvalidOperationException("Funding assignment replay diverged.");
        return next;
    }
}
