using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Committed own-worker information only. No host, treasury of other
/// participants, pending plans, global snapshot or market solver.</summary>
public sealed record FoundationPersistentAiWorker(
    PhysicalWorkerId Id, PhysicalTacticalTileId Node,
    PhysicalWorkerActivity Activity, bool Crossing,
    FoundationWorkerOrderQueue Queue);

public sealed record FoundationPersistentAiView(
    CivilizationId Owner, TurnNumber Turn,
    IReadOnlyList<FoundationPersistentAiWorker> Workers)
{
    // One entry per currently occupied own node with verified prior evidence.
    // Missing evidence is absent, never a fabricated zero-price observation.
    public IReadOnlyList<FoundationViewerLocalEconomicClose> LocalEconomics { get; internal init; } =
        Array.Empty<FoundationViewerLocalEconomicClose>();
    public bool AutomaticBaseAvailable { get; internal init; }
    public bool DefaultAutomaticBase { get; internal init; }
}

/// <summary>Explicit opt-in persistent planning contract. Legacy policies
/// remain unchanged. Planning proposes ordinary order edits only.</summary>
public abstract class FoundationPersistentEconomicAiPolicy : IFoundationTacticalAiTurnPolicy
{
    public abstract IReadOnlyList<FoundationPersistentOrderEdit> PlanPersistent(
        FoundationPersistentAiView view);

    IReadOnlyList<FoundationTacticalAiDecision> IFoundationTacticalAiTurnPolicy.Plan(
        CivilizationId civilization, WorldState snapshot,
        PhysicalTacticalIncidenceMap incidence, TurnNumber turn) =>
        throw new InvalidOperationException("Persistent AI requires its restricted planning view.");
}

/// <summary>Conservative first economic policy. Let the existing calendar
/// allocator select the commodity. No second allocation, market or search.
/// Never replace active tasks, explicit manual preferences or crossing.</summary>
public sealed class FoundationPersistentLocalProductionAiPolicy : FoundationPersistentEconomicAiPolicy
{
    public override IReadOnlyList<FoundationPersistentOrderEdit> PlanPersistent(
        FoundationPersistentAiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!view.AutomaticBaseAvailable || view.DefaultAutomaticBase)
            return Array.Empty<FoundationPersistentOrderEdit>();
        return Array.AsReadOnly(view.Workers
            .Where(w => !w.Crossing && w.Queue.Orders.Count == 0 && w.Queue.BaseActivity is null)
            .OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPersistentOrderEdit(view.Owner, w.Id,
                FoundationPersistentOrderEditKind.SetBaseActivity,
                BaseActivity: new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic)))
            .ToArray());
    }
}
