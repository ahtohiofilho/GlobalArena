using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Exact partial edge traversal state. The current fine tile remains SOURCE
/// until the crossing completes; progress never places a person inside
/// two nodes at once. This is a pre-commit physical progression, not a
/// second movement command/executor.
/// </summary>
public sealed record FoundationPhysicalEdgeTravelProgress(
    PhysicalWorkerId Worker,
    CivilizationId Owner,
    PhysicalTacticalTileId Source,
    PhysicalTacticalTileId Destination,
    PhysicalTacticalEdgeId Crossing,
    ulong RemainingTravelMicro);

/// <summary>
/// Time allocated in this turn plus the next edge progress or arrival.
/// Slices plug directly into R164 physical presence/demand projector.
/// </summary>
public sealed class FoundationPhysicalEdgeTravelTurnResult
{
    public PhysicalWorkerId Worker { get; }
    public PhysicalTacticalTileId PhysicalTileAfterTurn { get; }
    public FoundationPhysicalEdgeTravelProgress? ContinuingTravel { get; }
    public IReadOnlyList<FoundationWorkerTemporalNodeSlice> TimeSlices { get; }
    public bool Arrived => ContinuingTravel is null;
    public bool IsCommitted => false;

    internal FoundationPhysicalEdgeTravelTurnResult(
        PhysicalWorkerId worker,
        PhysicalTacticalTileId currentTile,
        FoundationPhysicalEdgeTravelProgress? ongoing,
        FoundationWorkerTemporalNodeSlice[] slices)
    {
        Worker = worker;
        PhysicalTileAfterTurn = currentTile;
        ContinuingTravel = ongoing;
        TimeSlices = Array.AsReadOnly(slices);
    }
}

/// <summary>
/// R165 physically validates the exact fine edge against the SAME v10
/// authoritative movement executor, including signed surface restrictions.
/// The duration is explicit upstream input until the approved movement
/// speed/cost policy is connected. No magic geographic distance or
/// arbitrary 1.4-turn rule is introduced.
/// </summary>
public static class FoundationFractionalEdgeTravelProjector
{
    public static FoundationPhysicalEdgeTravelProgress Start(
        PhysicalWorkerRuntimeRecord worker,
        PhysicalTacticalTileId destination,
        ulong crossingDurationMicro,
        PhysicalTacticalWorkerMoveEventExecutor canonicalMovement)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(canonicalMovement);
        if (!worker.TacticalTileId.HasValue || !worker.Occupation.HasValue ||
            crossingDurationMicro == 0UL || !destination.IsValid)
            throw new ArgumentException(
                "Fractional travel requires a placed classified worker and positive whole-edge time.");
        var source = worker.TacticalTileId.Value;
        if (source == destination)
            throw new ArgumentException("Travel must cross one physical edge.");
        var edge = canonicalMovement.CheckEdge(source, destination)
            ?? throw new InvalidOperationException("No physical crossing.");
        return new FoundationPhysicalEdgeTravelProgress(
            worker.Id, worker.Owner, source, destination,
            edge, crossingDurationMicro);
    }

    public static FoundationPhysicalEdgeTravelTurnResult Advance(
        FoundationPhysicalEdgeTravelProgress prior,
        PhysicalWorkerRuntimeRecord worker,
        PhysicalTacticalWorkerMoveEventExecutor canonicalMovement)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(canonicalMovement);
        if (!prior.Worker.IsValid || prior.Worker != worker.Id ||
            prior.Owner != worker.Owner ||
            worker.TacticalTileId != prior.Source ||
            prior.RemainingTravelMicro == 0UL ||
            prior.Source == prior.Destination ||
            !prior.Crossing.IsValid)
            throw new InvalidOperationException(
                "Travel continuation must match the real worker and source.");
        if (canonicalMovement.CheckEdge(prior.Source, prior.Destination) !=
            prior.Crossing)
            throw new InvalidOperationException(
                "Travel progress references a forged or invalid physical edge.");

        const ulong turn = FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro;
        ulong spent = Math.Min(prior.RemainingTravelMicro, turn);
        ulong remaining = prior.RemainingTravelMicro - spent;
        var time = new List<FoundationWorkerTemporalNodeSlice>(2)
        {
            new(worker.Id, prior.Source, checked((uint)spent), 0U,
                PhysicalWorkerActivity.Traveling)
        };

        if (remaining > 0UL)
        {
            return new FoundationPhysicalEdgeTravelTurnResult(
                worker.Id, prior.Source,
                prior with { RemainingTravelMicro = remaining },
                time.ToArray());
        }

        uint usable = checked((uint)(turn - spent));
        if (usable > 0U)
        {
            // Worker keeps the original classified occupation. Arrival
            // resumes production only if the occupation can produce.
            var activity = worker.Occupation is
                (PhysicalWorkerOccupation.Agriculture or
                 PhysicalWorkerOccupation.Forestry or
                 PhysicalWorkerOccupation.Mining)
                ? PhysicalWorkerActivity.Producing
                : PhysicalWorkerActivity.Other;
            time.Add(new FoundationWorkerTemporalNodeSlice(
                worker.Id, prior.Destination, usable,
                activity == PhysicalWorkerActivity.Producing ? usable : 0U,
                activity));
        }
        return new FoundationPhysicalEdgeTravelTurnResult(
            worker.Id, prior.Destination, null, time.ToArray());
    }
}
