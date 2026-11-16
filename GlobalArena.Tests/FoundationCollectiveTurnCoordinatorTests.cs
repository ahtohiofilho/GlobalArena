using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Collective authority is a separate opt-in game owner. It does NOT switch
/// the currently homologated single-player Godot preview to fake multiplayer.
/// </summary>
public sealed class FoundationCollectiveTurnCoordinatorTests
{
    private static WorldGenerationRequest Request(ulong seed = 20261012UL) =>
        new(new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0));

    private static FoundationCollectiveTurnCoordinator World() =>
        new(FoundationLocalGameHost.CreateLocalPreview(Request()).Session);

    private static CivilizationId Civ(ulong value) => new(value);
    private static PhysicalWorkerId Worker(ulong value) => new(value);

    private static StrategicCellId Neighbor(
        FoundationCollectiveTurnCoordinator host, ulong workerId)
    {
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var worker = host.Snapshot.Workers.Workers.Single(w => w.Id.Value == workerId);
        return graph.GetCellId(graph.GetNeighborIndexes(
            graph.GetNodeIndex(worker.CellId))[0]);
    }

    [Fact]
    public void AllCivilizationsAreRequiredButNoneStartsReady()
    {
        var host = World();
        Assert.Equal(new ulong[] { 1UL, 2UL },
            host.Participants.Select(c => c.Value));
        Assert.Empty(host.ReadyCivilizations);
        Assert.False(host.AllRequiredReady);
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        Assert.Null(host.TryCloseReadyTurn());
        Assert.Throws<InvalidOperationException>(() => host.CloseReadyTurn());
        Assert.Equal(0UL, host.Snapshot.Revision);
    }

    [Fact]
    public void OneCivilizationReadyCannotAdvanceTheOtherCivilizationsClock()
    {
        var host = World();
        var snapshot = host.Snapshot;
        var hash = host.StateHash;
        var target = Neighbor(host, 1UL);
        host.QueueMove(Civ(1), Worker(1), target);
        host.SetReady(Civ(1));

        Assert.Null(host.TryCloseReadyTurn());
        Assert.False(host.AllRequiredReady);
        Assert.Equal(new ulong[] { 1UL },
            host.ReadyCivilizations.Select(c => c.Value));
        Assert.Equal(1, host.PendingOrderCount);
        Assert.Same(snapshot, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
    }

    [Fact]
    public void BothCivilizationsMoveOwnWorkersInOneCollectiveTurnAndOneSnapshot()
    {
        var host = World();
        var initial = host.Snapshot;
        var left = Neighbor(host, 1UL);
        var right = Neighbor(host, 4UL);
        var first = host.QueueMove(Civ(1), Worker(1), left);
        var second = host.QueueMove(Civ(2), Worker(4), right);

        Assert.Equal(Civ(1), first.Issuer);
        Assert.Equal(Civ(2), second.Issuer);
        Assert.Equal(2, host.PendingOrderCount);
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(1, host.GetOwnOrders(Civ(1)).Count);
        Assert.Equal(1, host.GetOwnOrders(Civ(2)).Count);

        host.SetReady(Civ(2));
        Assert.Null(host.TryCloseReadyTurn());
        host.SetReady(Civ(1));
        var closed = host.CloseReadyTurn();

        Assert.Equal(new TurnNumber(1UL), closed.Turn);
        Assert.Equal(2, closed.ExecutedEvents);
        Assert.True(closed.ReplayMatched);
        Assert.Equal(new ulong[] { 1UL, 4UL },
            closed.Events.Select(ev => ev.WorkerId.Value));
        Assert.Equal(new ulong[] { 1UL, 2UL },
            closed.Events.Select(ev => ev.Issuer.Value));
        Assert.All(closed.Events, ev => Assert.True(ev.TraversedEdge.HasValue));
        Assert.Equal(2UL, closed.Revision);
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.NotSame(initial, host.Snapshot);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Equal(9U, closed.Hash.FormatVersion);
        Assert.Equal(host.StateHash, closed.Hash);
        Assert.Equal(left,
            host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL).CellId);
        Assert.Equal(right,
            host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL).CellId);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
        Assert.Equal(0UL, initial.Revision);
    }

    [Fact]
    public void ReversingReadyOrderAndQueueArrivalCannotChangeCollectiveHash()
    {
        var one = World();
        var two = World();
        var left = Neighbor(one, 1UL);
        var right = Neighbor(one, 4UL);

        one.QueueMove(Civ(1), Worker(1), left);
        one.QueueMove(Civ(2), Worker(4), right);
        one.SetReady(Civ(1));
        one.SetReady(Civ(2));

        two.QueueMove(Civ(2), Worker(4), right);
        two.QueueMove(Civ(1), Worker(1), left);
        two.SetReady(Civ(2));
        two.SetReady(Civ(1));

        var first = one.CloseReadyTurn();
        var second = two.CloseReadyTurn();
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(one.StateHash, two.StateHash);
        Assert.Equal(2, first.ExecutedEvents);
    }

    [Fact]
    public void OnceReadyCivilizationCannotAlterOrdersUntilItReopens()
    {
        var host = World();
        var target = Neighbor(host, 1UL);
        var snapshot = host.Snapshot;
        host.QueueMove(Civ(1), Worker(1), target);
        host.SetReady(Civ(1));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueMove(Civ(1), Worker(2), target));
        Assert.Throws<InvalidOperationException>(() =>
            host.CancelOwnOrder(Civ(1), Worker(1)));
        Assert.Equal(1, host.PendingOrderCount);
        Assert.True(host.ReadyCivilizations.Contains(Civ(1)));

        host.Reopen(Civ(1));
        Assert.Empty(host.ReadyCivilizations);
        Assert.True(host.CancelOwnOrder(Civ(1), Worker(1)));
        host.QueueMove(Civ(1), Worker(2), target);
        Assert.Equal(new ulong[] { 2UL },
            host.GetOwnOrders(Civ(1)).Select(x => x.WorkerId.Value));
        Assert.Same(snapshot, host.Snapshot);
    }

    [Fact]
    public void PrivateCivQueueDoesNotExposeOtherCivilizationsOrAllowForeignCommands()
    {
        var host = World();
        var firstTarget = Neighbor(host, 1UL);
        var secondTarget = Neighbor(host, 4UL);
        host.QueueMove(Civ(1), Worker(1), firstTarget);
        host.QueueMove(Civ(2), Worker(4), secondTarget);
        Assert.Equal(new ulong[] { 1UL },
            host.GetOwnOrders(Civ(1)).Select(x => x.WorkerId.Value));
        Assert.Equal(new ulong[] { 4UL },
            host.GetOwnOrders(Civ(2)).Select(x => x.WorkerId.Value));
        Assert.Throws<InvalidOperationException>(() =>
            host.GetOwnOrders(Civ(3)));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueMove(Civ(1), Worker(4), secondTarget));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueMove(Civ(2), Worker(1), firstTarget));
        Assert.Throws<InvalidOperationException>(() => host.SetReady(Civ(3)));
        Assert.Equal(2, host.PendingOrderCount);
    }

    [Fact]
    public void OneCivilizationCanReadyWithoutCommandsOtherCivilizationCanWork()
    {
        var host = World();
        var next = Neighbor(host, 4UL);
        host.QueueMove(Civ(2), Worker(4), next);
        host.SetReady(Civ(1));
        Assert.False(host.AllRequiredReady);
        Assert.Null(host.TryCloseReadyTurn());
        host.SetReady(Civ(2));
        var receipt = host.TryCloseReadyTurn();
        Assert.NotNull(receipt);
        Assert.Equal(1, receipt.ExecutedEvents);
        Assert.Equal(Civ(2), receipt.Events[0].Issuer);
        Assert.Equal(1UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
    }

    [Fact]
    public void FullyEmptyCollectiveTurnAdvancesClockWithoutFakeWorkersOrJournalEvents()
    {
        var host = World();
        var before = host.Snapshot;
        var hash = host.StateHash;
        host.SetReady(Civ(2));
        host.SetReady(Civ(1));
        var result = host.TryCloseReadyTurn();
        Assert.NotNull(result);
        Assert.Equal(0, result.ExecutedEvents);
        Assert.Empty(result.Events);
        Assert.True(result.ReplayMatched);
        Assert.Equal(hash, result.Hash);
        Assert.Equal(0UL, result.Revision);
        Assert.Same(before, host.Snapshot);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        Assert.Empty(host.ReadyCivilizations);
        Assert.False(host.AllRequiredReady);
    }

    [Fact]
    public void MovementAndProductionBelongToDifferentCivilizationsInOneTurn()
    {
        var host = World();
        var left = Neighbor(host, 1UL);
        var right = Neighbor(host, 4UL);
        host.QueueMove(Civ(1), Worker(1), left);
        host.QueueMove(Civ(2), Worker(4), right);
        host.SetReady(Civ(1));
        host.SetReady(Civ(2));
        host.CloseReadyTurn();

        host.QueueActivity(Civ(1), Worker(1), PhysicalWorkerActivity.Producing);
        host.QueueActivity(Civ(2), Worker(4), PhysicalWorkerActivity.Other);
        host.SetReady(Civ(2));
        host.SetReady(Civ(1));
        var second = host.CloseReadyTurn();
        Assert.Equal(new TurnNumber(2UL), second.Turn);
        Assert.Equal(2, second.ExecutedEvents);
        Assert.All(second.Events, e => Assert.Null(e.TraversedEdge));
        Assert.Equal(4UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(3UL), host.NextTurn);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            host.Snapshot.Workers.Workers.Single(x => x.Id.Value == 1).Activity);
        Assert.Equal(PhysicalWorkerActivity.Other,
            host.Snapshot.Workers.Workers.Single(x => x.Id.Value == 4).Activity);
    }

    [Fact]
    public void InvalidOwnerTeleportAndZeroCapacityProductionLeaveBothPlansUntouched()
    {
        var host = World();
        var target = Neighbor(host, 1UL);
        var original = host.Snapshot;
        var hash = host.StateHash;
        host.QueueMove(Civ(1), Worker(1), target);
        var distant = host.Session.GeneratedWorld.StrategicSurfaceGraph.GetCellId(46);
        Assert.ThrowsAny<Exception>(() =>
            host.QueueMove(Civ(1), Worker(2), distant));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueActivity(Civ(1), Worker(3),
                PhysicalWorkerActivity.Producing));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueMove(Civ(2), Worker(1), target));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueMove(Civ(3), Worker(1), target));
        Assert.Equal(1, host.PendingOrderCount);
        Assert.Equal(hash, host.StateHash);
        Assert.Same(original, host.Snapshot);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
    }

    [Fact]
    public void ExternalWorldStateChangeFailsClosedAndPreservesPendingDecisions()
    {
        var host = World();
        var neighbor = Neighbor(host, 1UL);
        host.QueueMove(Civ(1), Worker(1), neighbor);
        host.SetReady(Civ(1));
        host.SetReady(Civ(2));
        var replacement = host.Snapshot.AdvanceRevision();
        host.Session.AcceptSnapshot(replacement);

        Assert.Throws<InvalidOperationException>(() => host.CloseReadyTurn());
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueMove(Civ(1), Worker(2), neighbor));
        Assert.Throws<InvalidOperationException>(() => host.Reopen(Civ(1)));
        Assert.Equal(1, host.PendingOrderCount);
        Assert.Equal(2, host.ReadyCivilizations.Count);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        Assert.Same(replacement, host.Snapshot);
    }

    [Fact]
    public void RequiredWorkerlessCivilizationMustAlsoBeReadyBeforeTheTurnCloses()
    {
        var session = CanonicalGameSession.Start(Request());
        var civilizations = new CivilizationRuntimeState(
            new[] { Civ(1), Civ(2), Civ(3) });
        session.AcceptSnapshot(session.Snapshot.WithCivilizations(civilizations)
            .WithWorkers(new PhysicalWorkerRuntimeState(new[]
            {
                new PhysicalWorkerRuntimeRecord(Worker(1), Civ(1),
                    new StrategicCellId(1UL), new CommodityId(1U),
                    1_000_000UL, PhysicalWorkerActivity.Producing),
                new PhysicalWorkerRuntimeRecord(Worker(2), Civ(2),
                    new StrategicCellId(3UL), new CommodityId(2U),
                    1_000_000UL, PhysicalWorkerActivity.Producing)
            })));
        var coordinator = new FoundationCollectiveTurnCoordinator(session);
        coordinator.SetReady(Civ(1));
        coordinator.SetReady(Civ(2));
        Assert.Null(coordinator.TryCloseReadyTurn()); // Civ 3 participates, no worker
        coordinator.SetReady(Civ(3));
        var closed = coordinator.CloseReadyTurn();
        Assert.Equal(0, closed.ExecutedEvents);
        Assert.Equal(0UL, coordinator.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), coordinator.NextTurn);
        Assert.Equal(3, coordinator.Participants.Count);
    }

    [Fact]
    public void ReplacingOwnQueuedPlanKeepsOneEffectiveOrderPerWorker()
    {
        var host = World();
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var source = host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1).CellId;
        var neighbors = graph.GetNeighborIndexes(graph.GetNodeIndex(source));
        var a = graph.GetCellId(neighbors[0]);
        var b = graph.GetCellId(neighbors[1]);
        host.QueueMove(Civ(1), Worker(1), a);
        host.QueueMove(Civ(1), Worker(1), b);
        Assert.Single(host.GetOwnOrders(Civ(1)));
        Assert.Equal(b, host.GetOwnOrders(Civ(1))[0].Destination);
        host.SetReady(Civ(1));
        host.SetReady(Civ(2));
        var closed = host.CloseReadyTurn();
        Assert.Equal(1, closed.ExecutedEvents);
        Assert.Equal(b, closed.Events[0].Destination);
        Assert.Equal(1UL, host.Snapshot.Revision);
    }

    [Fact]
    public void PendingOrderCollectionsAreReadOnlyCopies()
    {
        var host = World();
        var a = Neighbor(host, 1UL);
        host.QueueMove(Civ(1), Worker(1), a);
        var snapshot = host.GetOwnOrders(Civ(1));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoundationCivilizationWorkerOrder>)snapshot).Clear());
        host.CancelOwnOrder(Civ(1), Worker(1));
        Assert.Single(snapshot);
        Assert.Empty(host.GetOwnOrders(Civ(1)));
        Assert.Equal(0UL, host.Snapshot.Revision);
    }

    [Fact]
    public void LegacyV8WorldCannotBeUsedAsIfItHadPhysicalWorkers()
    {
        var session = CanonicalGameSession.Start(Request());
        Assert.Throws<InvalidOperationException>(() =>
            new FoundationCollectiveTurnCoordinator(session));
    }
}
