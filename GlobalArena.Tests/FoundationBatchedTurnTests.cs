using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// One game turn can contain several real worker actions but only one
/// authoritative WorldState publication. Physical workers in transit still
/// exist exactly once. Journal events/revision are per action, not per turn.
/// </summary>
public sealed class FoundationBatchedTurnTests
{
    private static FoundationLocalGameHost World() =>
        FoundationLocalGameHost.CreateLocalPreview(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    private static StrategicCellId Neighbor(
        FoundationLocalGameHost world, PhysicalWorkerId workerId)
    {
        var graph = world.Session.GeneratedWorld.StrategicSurfaceGraph;
        var worker = world.ControllableWorkers.Single(x => x.Id == workerId);
        return graph.GetCellId(
            graph.GetNeighborIndexes(graph.GetNodeIndex(worker.CellId))[0]);
    }

    [Fact]
    public void TwoIndependentWorkersAreQueuedWithoutAdvancingTurnOrChangingHash()
    {
        var world = World();
        var original = world.Snapshot;
        var hash = world.StateHash;
        var tile = Neighbor(world, new PhysicalWorkerId(1UL));

        var first = world.QueueMove(new PhysicalWorkerId(1UL), tile);
        var second = world.QueueMove(new PhysicalWorkerId(2UL), tile);

        Assert.Equal(2, world.PendingOrderCount);
        Assert.Equal(new ulong[] { 1, 2 },
            world.PendingOrders.Select(x => x.WorkerId.Value));
        Assert.Equal(tile, first.Destination);
        Assert.Equal(tile, second.Destination);
        Assert.Equal(PhysicalWorkerActivity.Traveling, first.NextActivity);
        Assert.True(first.IsMovement && second.IsMovement);
        Assert.Same(original, world.Snapshot);
        Assert.Equal(0UL, world.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), world.NextTurn);
        Assert.Equal(hash, world.StateHash);
        Assert.Equal(6, world.Snapshot.Workers.Workers.Count);
        Assert.Equal(1UL, original.Workers.Workers.First().CellId.Value);
    }

    [Fact]
    public void TwoQueuedMovesPublishOneTurnWithTwoJournalEventsAndOneReplayHash()
    {
        var world = World();
        var original = world.Snapshot;
        var tile = Neighbor(world, new PhysicalWorkerId(1UL));
        world.QueueMove(new PhysicalWorkerId(2UL), tile); // reverse UI order
        world.QueueMove(new PhysicalWorkerId(1UL), tile);

        var committed = world.EndTurn();

        Assert.Equal(new TurnNumber(1UL), committed.Turn);
        Assert.True(committed.ReplayMatched);
        Assert.Equal(2, committed.ExecutedEvents);
        Assert.Equal(new ulong[] { 1, 2 },
            committed.WorkerEvents.Select(x => x.WorkerId.Value));
        Assert.All(committed.WorkerEvents,
            x => Assert.Equal(committed.ResultingHash, x.ResultingHash));
        Assert.All(committed.WorkerEvents,
            x => Assert.Equal(2, x.ExecutedEvents));
        Assert.All(committed.WorkerEvents,
            x => Assert.Equal(tile, x.Destination));
        Assert.Equal(2UL, world.Snapshot.Revision);
        Assert.Equal(2UL, committed.ResultingRevision); // each event advances revision
        Assert.Equal(new TurnNumber(2UL), world.NextTurn); // one turn, not two
        Assert.Equal(0, world.PendingOrderCount);
        Assert.Equal(committed.ResultingHash, world.StateHash);
        Assert.Equal(9U, committed.ResultingHash.FormatVersion);
        Assert.NotSame(original, world.Snapshot);
        Assert.Equal(6, world.Snapshot.Workers.Workers.Count);
        Assert.Equal(2, world.Snapshot.Workers.Workers.Count(w =>
            w.Id.Value is 1UL or 2UL &&
            w.CellId == tile &&
            w.Activity == PhysicalWorkerActivity.Traveling));
        Assert.Equal(1UL, original.Workers.Workers.First().CellId.Value);
    }

    [Fact]
    public void QueueOrderCanonicalizationMakesReverseUserClickOrderHashIdentical()
    {
        var left = World();
        var right = World();
        var destination = Neighbor(left, new PhysicalWorkerId(1UL));
        left.QueueMove(new PhysicalWorkerId(1UL), destination);
        left.QueueMove(new PhysicalWorkerId(2UL), destination);
        right.QueueMove(new PhysicalWorkerId(2UL), destination);
        right.QueueMove(new PhysicalWorkerId(1UL), destination);

        var a = left.EndTurn();
        var b = right.EndTurn();
        Assert.Equal(a.ResultingHash, b.ResultingHash);
        Assert.Equal(a.ResultingRevision, b.ResultingRevision);
        Assert.Equal(a.WorkerEvents, b.WorkerEvents);
        Assert.Equal(2, a.ExecutedEvents);
        Assert.Equal(2, b.ExecutedEvents);
        Assert.Equal(left.StateHash, right.StateHash);
    }

    [Fact]
    public void CancelAndReplaceOperateOnlyOnPlansNotOnWorkerState()
    {
        var world = World();
        var original = world.Snapshot;
        var worker = new PhysicalWorkerId(1UL);
        var graph = world.Session.GeneratedWorld.StrategicSurfaceGraph;
        var source = world.ControllableWorkers.Single(w => w.Id == worker).CellId;
        var a = graph.GetCellId(
            graph.GetNeighborIndexes(graph.GetNodeIndex(source))[0]);
        var b = graph.GetCellId(
            graph.GetNeighborIndexes(graph.GetNodeIndex(source))[1]);
        world.QueueMove(worker, a);
        world.QueueMove(worker, b); // overwrite, not a second move
        Assert.Single(world.PendingOrders);
        Assert.Equal(b, world.PendingOrders.Single().Destination);
        Assert.False(world.CancelQueuedOrder(new PhysicalWorkerId(2UL)));
        Assert.True(world.CancelQueuedOrder(worker));
        Assert.Empty(world.PendingOrders);
        Assert.False(world.CancelQueuedOrder(worker));
        world.QueueMove(worker, a);
        world.ClearQueuedOrders();
        Assert.Equal(0, world.PendingOrderCount);
        Assert.Same(original, world.Snapshot);
        Assert.Equal(new TurnNumber(1UL), world.NextTurn);
    }

    [Fact]
    public void EmptyTurnClosesTheClockButCreatesNoFakeEventsOrState()
    {
        var world = World();
        var old = world.Snapshot;
        var hash = world.StateHash;
        var receipt = world.EndTurn();

        Assert.Equal(new TurnNumber(1UL), receipt.Turn);
        Assert.Equal(new TurnNumber(2UL), world.NextTurn);
        Assert.Equal(0, receipt.ExecutedEvents);
        Assert.Empty(receipt.WorkerEvents);
        Assert.True(receipt.ReplayMatched);
        Assert.Same(old, world.Snapshot);
        Assert.Equal(0UL, world.Snapshot.Revision);
        Assert.Equal(hash, world.StateHash);
        Assert.Equal(hash, receipt.ResultingHash);
        Assert.Equal(6, world.Snapshot.Workers.Workers.Count);
        Assert.Equal(0, world.PendingOrderCount);
    }

    [Fact]
    public void InvalidOwnershipDestinationAndActivityCannotCorruptPendingPlan()
    {
        var world = World();
        var destination = Neighbor(world, new PhysicalWorkerId(1UL));
        world.QueueMove(new PhysicalWorkerId(1UL), destination);
        var old = world.Snapshot;
        var hash = world.StateHash;

        Assert.Throws<InvalidOperationException>(() =>
            world.QueueMove(new PhysicalWorkerId(4UL), destination));
        Assert.Throws<InvalidOperationException>(() =>
            world.QueueMove(new PhysicalWorkerId(1UL), new StrategicCellId(92UL)));
        Assert.Throws<InvalidOperationException>(() =>
            world.QueueActivity(new PhysicalWorkerId(3UL),
                PhysicalWorkerActivity.Producing)); // military worker has no output
        Assert.Throws<InvalidOperationException>(() =>
            world.QueueActivity(new PhysicalWorkerId(4UL),
                PhysicalWorkerActivity.Traveling)); // foreign
        Assert.Equal(1, world.PendingOrderCount);
        Assert.Equal(new PhysicalWorkerId(1UL), world.PendingOrders[0].WorkerId);
        Assert.Same(old, world.Snapshot);
        Assert.Equal(hash, world.StateHash);
        Assert.Equal(new TurnNumber(1UL), world.NextTurn);
    }

    [Fact]
    public void PhysicalProductionAndTravelCanBeQueuedForDifferentWorkersInSameTurn()
    {
        var world = World();
        var a = new PhysicalWorkerId(1UL);
        var b = new PhysicalWorkerId(2UL);
        var neighbor = Neighbor(world, a);

        world.QueueMove(a, neighbor);
        var first = world.EndTurn();
        Assert.Equal(1, first.ExecutedEvents);
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            world.Snapshot.Workers.Workers.Single(w => w.Id == a).Activity);

        world.QueueActivity(a, PhysicalWorkerActivity.Producing);
        world.QueueMove(b, neighbor);
        var second = world.EndTurn();
        Assert.Equal(new TurnNumber(2UL), second.Turn);
        Assert.Equal(2, second.ExecutedEvents);
        Assert.True(second.ReplayMatched);
        Assert.Equal(new TurnNumber(3UL), world.NextTurn);
        Assert.Equal(3UL, world.Snapshot.Revision);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            world.Snapshot.Workers.Workers.Single(w => w.Id == a).Activity);
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            world.Snapshot.Workers.Workers.Single(w => w.Id == b).Activity);
        Assert.Equal(neighbor,
            world.Snapshot.Workers.Workers.Single(w => w.Id == b).CellId);
        Assert.Equal(6, world.Snapshot.Workers.Workers.Count);
    }

    [Fact]
    public void LegacyImmediateActionsDoNotConsumePendingBatchOrDoubleCommit()
    {
        var world = World();
        var next = Neighbor(world, new PhysicalWorkerId(1UL));
        world.QueueMove(new PhysicalWorkerId(1UL), next);
        Assert.Throws<InvalidOperationException>(() =>
            world.MoveWorker(new PhysicalWorkerId(2UL), next));
        Assert.Throws<InvalidOperationException>(() =>
            world.ChangeWorkerActivity(new PhysicalWorkerId(3UL),
                PhysicalWorkerActivity.Other));
        Assert.Equal(1, world.PendingOrderCount);
        Assert.Equal(new TurnNumber(1UL), world.NextTurn);
        Assert.Equal(0UL, world.Snapshot.Revision);

        var closed = world.EndTurn();
        Assert.Equal(1, closed.ExecutedEvents);
        Assert.Equal(new TurnNumber(2UL), world.NextTurn);
        Assert.Empty(world.PendingOrders);
    }

    [Fact]
    public void ExternalWorldStateReplacementCannotSilentlyCommitQueuedOrders()
    {
        var world = World();
        var destination = Neighbor(world, new PhysicalWorkerId(1UL));
        world.QueueMove(new PhysicalWorkerId(1UL), destination);
        var old = world.Snapshot;
        var different = old.AdvanceRevision();
        world.Session.AcceptSnapshot(different);
        Assert.Throws<InvalidOperationException>(() => world.EndTurn());
        Assert.Throws<InvalidOperationException>(() =>
            world.QueueMove(new PhysicalWorkerId(2UL), destination));
        Assert.Throws<InvalidOperationException>(() =>
            world.ClearQueuedOrders());
        Assert.Same(different, world.Snapshot);
        Assert.Equal(1, world.PendingOrderCount);
        Assert.Equal(new TurnNumber(1UL), world.NextTurn);
    }

    [Fact]
    public void PendingListIsReadOnlySnapshotAndCannotCreatePhantomCommands()
    {
        var world = World();
        var destination = Neighbor(world, new PhysicalWorkerId(1UL));
        world.QueueMove(new PhysicalWorkerId(1UL), destination);
        var snapshot = world.PendingOrders;
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoundationQueuedWorkerOrder>)snapshot).Clear());
        world.QueueMove(new PhysicalWorkerId(2UL), destination);
        Assert.Single(snapshot); // previous snapshot is immutable
        Assert.Equal(2, world.PendingOrderCount);
        Assert.Equal(0UL, world.Snapshot.Revision);
    }

    [Fact]
    public void FullBatchIsEquivalentToAuthoritativePhaseButNotTwoTimeSteps()
    {
        var world = World();
        var destination = Neighbor(world, new PhysicalWorkerId(1UL));
        var initial = world.Snapshot;
        var firstHash = world.StateHash;
        world.QueueMove(new PhysicalWorkerId(2UL), destination);
        world.QueueMove(new PhysicalWorkerId(1UL), destination);
        var receipt = world.EndTurn();

        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.Equal(1UL, receipt.Turn.Value);
        Assert.Equal(2UL, receipt.ResultingRevision);
        Assert.NotEqual(firstHash, receipt.ResultingHash);
        Assert.Equal(1, world.NextTurn.Value - receipt.Turn.Value);
        Assert.Equal(6, initial.Workers.Workers.Count);
        Assert.Equal(6, world.Snapshot.Workers.Workers.Count);
        // Revisions count executed events. Turn numbers count closed batches.
    }
}
