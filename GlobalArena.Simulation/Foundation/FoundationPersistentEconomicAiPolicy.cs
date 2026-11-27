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
    IReadOnlyList<FoundationPersistentAiWorker> Workers);

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
