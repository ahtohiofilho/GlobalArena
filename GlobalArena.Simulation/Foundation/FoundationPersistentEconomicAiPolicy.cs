using System.Numerics;
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
    public BigInteger? OwnCashScaled { get; internal init; }
    public bool FundingAvailable { get; internal init; }
    public IReadOnlyList<FoundationAiAdjacentOpportunity> AdjacentOpportunities { get; internal init; } =
        Array.Empty<FoundationAiAdjacentOpportunity>();
}

/// <summary>One private ordinary construction transaction; the host supplies
/// the issuer. Assignments are copied and validated by the existing journal.</summary>
public sealed record FoundationAiFundingIntent(
    PhysicalTacticalEdgeId Edge, ulong MoneyMicro,
    IReadOnlyList<FoundationFundedWorkerInsertion> Assignments);

/// <summary>Explicit opt-in persistent planning contract. Legacy policies
/// remain unchanged. Planning proposes ordinary order edits only.</summary>
public abstract class FoundationPersistentEconomicAiPolicy : IFoundationTacticalAiTurnPolicy
{
    internal virtual bool RequiresExpansionCandidates => false;
    // First QA funding boundary: exclusive with order edits in this turn,
    // so a rejected competing fund cannot leave dependent provisional IDs.
    public virtual FoundationAiFundingIntent? PlanFunding(FoundationPersistentAiView view) => null;
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

/// <summary>Adjacent public terrain potential valued at the SAME source
/// price vector. Destination market prices and foreign census are not read.</summary>
public sealed record FoundationAiAdjacentOpportunity(
    PhysicalTacticalTileId Source, PhysicalTacticalTileId Destination,
    BigInteger SourcePotential, BigInteger DestinationPotential);

/// <summary>First bounded expansion heuristic, not a market forecast.
/// At most one move per civilization per turn, with a twofold hurdle
/// against source potential per own resident and no last-resident move.</summary>
public sealed class FoundationPersistentExpansionAiPolicy : FoundationPersistentEconomicAiPolicy
{
    internal override bool RequiresExpansionCandidates => true;

    public override IReadOnlyList<FoundationPersistentOrderEdit> PlanPersistent(FoundationPersistentAiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var fallback = new FoundationPersistentLocalProductionAiPolicy();
        if (!view.DefaultAutomaticBase || !view.AutomaticBaseAvailable)
            return fallback.PlanPersistent(view);
        var occupied = view.Workers.Select(w => w.Node).ToHashSet();
        var reserved = view.Workers.SelectMany(w => w.Queue.Orders)
            .Where(o => o.Kind == FoundationWorkerOrderKind.Move && o.Destination.HasValue)
            .Select(o => o.Destination!.Value).ToHashSet();
        var sources = view.Workers.GroupBy(w => w.Node).ToDictionary(g => g.Key, g => g.ToArray());
        var choices = new List<(FoundationPersistentAiWorker Worker, FoundationAiAdjacentOpportunity Edge, BigInteger Gain)>();
        foreach (var edge in view.AdjacentOpportunities)
        {
            if (occupied.Contains(edge.Destination) || reserved.Contains(edge.Destination) ||
                !sources.TryGetValue(edge.Source, out var residents) ||
                edge.SourcePotential <= 0 || edge.DestinationPotential <= 0)
                continue;
            // Count only stationary uncommanded automatic residents. The
            // others may already be leaving; never rely on them as anchors.
            var available = residents.Where(w => !w.Crossing &&
                w.Activity != PhysicalWorkerActivity.Traveling &&
                w.Queue.Orders.Count == 0 &&
                w.Queue.BaseActivity?.Mode != FoundationWorkerBaseMode.Manual)
                .OrderBy(w => w.Id.Value).ToArray();
            if (available.Length < 3) continue;
            BigInteger gain = edge.DestinationPotential * available.Length - 2 * edge.SourcePotential;
            if (gain <= 0) continue;
            choices.Add((available[^1], edge, gain));
        }
        if (choices.Count == 0) return Array.Empty<FoundationPersistentOrderEdit>();
        var best = choices.OrderByDescending(c => c.Gain)
            .ThenBy(c => c.Edge.Source.FineStrategicCellId.Value)
            .ThenBy(c => c.Edge.Destination.FineStrategicCellId.Value)
            .ThenBy(c => c.Worker.Id.Value).First();
        return Array.AsReadOnly(new[] { new FoundationPersistentOrderEdit(view.Owner, best.Worker.Id,
            FoundationPersistentOrderEditKind.Insert, Index: 0,
            OrderKind: FoundationWorkerOrderKind.Move, Destination: best.Edge.Destination) });
    }
}
