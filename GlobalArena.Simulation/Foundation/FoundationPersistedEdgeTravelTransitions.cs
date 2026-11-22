using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Immutable R167 transition receipt. Its input is the canonical state, not a
/// caller-supplied remaining duration. Replay calls the same deterministic
/// transition and checks the post-state hash.
/// </summary>
public sealed record FoundationEdgeTravelTransitionReceipt(
    PhysicalWorkerId Worker,
    bool Started,
    WorldStateHash Before,
    WorldStateHash After,
    ulong Revision,
    FoundationPhysicalEdgeTravelTurnResult? TurnResult);

/// <summary>
/// R167 opt-in state transition boundary for R165/R166 physical movement.
/// This does not create a second pathfinder or move workers instantaneously.
/// Not yet connected to the collective turn publisher: integration must make
/// this transition an ISimulationEventExecutor event before gameplay cutover.
/// </summary>
public static class FoundationPersistedEdgeTravelTransitions
{
    private static readonly CanonicalWorldStateHasher Hasher = new();

    public static (WorldState State, FoundationEdgeTravelTransitionReceipt Receipt) Start(
        WorldState world, PhysicalWorkerId id,
        PhysicalTacticalTileId destination,
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(policy);
        if (!world.EdgeTravels.IsInitialized)
            throw new InvalidOperationException("Deploy an explicit empty travel ledger first.");
        if (world.EdgeTravels.Pending.Any(p => p.Worker == id))
            throw new InvalidOperationException("Worker already has a crossing in progress.");
        var worker = world.Workers.Workers.SingleOrDefault(w => w.Id == id)
            ?? throw new InvalidOperationException("Unknown physical worker.");
        if (worker.Activity == PhysicalWorkerActivity.Traveling)
            throw new InvalidOperationException("A traveler cannot begin another crossing.");
        var (progress, quote) = FoundationGroundEdgeTravelTimeProjector.Start(
            world, worker, destination, movement, policy);
        var pending = new FoundationPendingEdgeTravel(id, worker.Owner,
            progress.Source, destination, quote.Edge,
            quote.ImprovedCrossingTimeMicro, quote.ImprovedCrossingTimeMicro);
        var nextWorkers = ReplaceWorker(world, worker,
            worker.WithTacticalLocationAndActivity(progress.Source,
                worker.CellId, PhysicalWorkerActivity.Traveling));
        var nextLedger = world.EdgeTravels.Replace(id, pending);
        var before = Hasher.Compute(world);
        var state = world.WithPhysicalEdgeTravel(nextWorkers, nextLedger)
            .AdvanceRevision();
        return (state, new FoundationEdgeTravelTransitionReceipt(
            id, true, before, Hasher.Compute(state), state.Revision, null));
    }

    public static (WorldState State, FoundationEdgeTravelTransitionReceipt Receipt) Advance(
        WorldState world, PhysicalWorkerId id,
        PhysicalTacticalWorkerMoveEventExecutor movement)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(movement);
        if (!world.EdgeTravels.IsInitialized)
            throw new InvalidOperationException("Uninitialized physical travel ledger.");
        var pending = world.EdgeTravels.Pending.SingleOrDefault(p => p.Worker == id)
            ?? throw new InvalidOperationException("Worker has no crossing to continue.");
        var worker = world.Workers.Workers.Single(w => w.Id == id);
        var result = FoundationFractionalEdgeTravelProjector.Advance(
            new FoundationPhysicalEdgeTravelProgress(pending.Worker,
                pending.Owner, pending.Source, pending.Destination,
                pending.Edge, pending.RemainingMicro), worker, movement);
        var nextLedger = world.EdgeTravels.Replace(id, result.ContinuingTravel is null
            ? null
            : pending with
            {
                RemainingMicro = result.ContinuingTravel.RemainingTravelMicro
            });
        var nextWorkers = result.Arrived
            ? ReplaceWorker(world, worker,
                worker.WithTacticalLocationAndActivity(
                    pending.Destination,
                    movement.PresentationRegion(pending.Destination, worker.CellId),
                    result.TimeSlices.LastOrDefault()?.DemandActivity
                        == PhysicalWorkerActivity.Producing
                        ? PhysicalWorkerActivity.Producing
                        : PhysicalWorkerActivity.Other))
            : world.Workers;
        var before = Hasher.Compute(world);
        var state = world.WithPhysicalEdgeTravel(nextWorkers, nextLedger)
            .AdvanceRevision();
        return (state, new FoundationEdgeTravelTransitionReceipt(
            id, false, before, Hasher.Compute(state), state.Revision, result));
    }

    public static WorldState DeployEmpty(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.EdgeTravels.IsInitialized || world.WorldBinding is null ||
            !world.Transport.IsInitialized ||
            !world.Workers.HasAuthoritativeOccupations)
            throw new InvalidOperationException("Initial deployment requires an authoritative physical world.");
        return world.WithPhysicalEdgeTravel(world.Workers,
            new FoundationEdgeTravelRuntimeState(
                Array.Empty<FoundationPendingEdgeTravel>()));
    }

    public static WorldState ReplayStart(
        WorldState before, FoundationEdgeTravelTransitionReceipt expected,
        PhysicalTacticalTileId destination,
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy policy)
    {
        if (!expected.Started || Hasher.Compute(before) != expected.Before)
            throw new InvalidOperationException("Travel event pre-state hash mismatch.");
        var (after, receipt) = Start(before, expected.Worker,
            destination, movement, policy);
        RequireMatching(receipt, expected);
        return after;
    }

    public static WorldState ReplayAdvance(
        WorldState before, FoundationEdgeTravelTransitionReceipt expected,
        PhysicalTacticalWorkerMoveEventExecutor movement)
    {
        if (expected.Started || Hasher.Compute(before) != expected.Before)
            throw new InvalidOperationException("Travel event pre-state hash mismatch.");
        var (after, receipt) = Advance(before, expected.Worker, movement);
        RequireMatching(receipt, expected);
        return after;
    }

    private static void RequireMatching(
        FoundationEdgeTravelTransitionReceipt actual,
        FoundationEdgeTravelTransitionReceipt expected)
    {
        if ((actual.TurnResult is null) != (expected.TurnResult is null) ||
            (actual.TurnResult is not null && expected.TurnResult is not null &&
             (actual.TurnResult.Arrived != expected.TurnResult.Arrived ||
              actual.TurnResult.PhysicalTileAfterTurn != expected.TurnResult.PhysicalTileAfterTurn ||
              actual.TurnResult.ContinuingTravel != expected.TurnResult.ContinuingTravel)) ||
            actual.Before != expected.Before ||
            actual.After != expected.After ||
            actual.Revision != expected.Revision ||
            actual.Started != expected.Started ||
            actual.Worker != expected.Worker ||
            (actual.TurnResult?.TimeSlices.SequenceEqual(
                expected.TurnResult?.TimeSlices ??
                Array.Empty<FoundationWorkerTemporalNodeSlice>()) ?? 
                (expected.TurnResult is null)) == false)
            throw new InvalidOperationException(
                "Physical travel replay diverged from recorded transition.");
    }

    private static PhysicalWorkerRuntimeState ReplaceWorker(
        WorldState world, PhysicalWorkerRuntimeRecord oldWorker,
        PhysicalWorkerRuntimeRecord updated)
    {
        return new PhysicalWorkerRuntimeState(
            world.Workers.Workers.Select(w =>
                w.Id == oldWorker.Id ? updated : w),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
    }
}
