using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Exact domain facade consumed by Main.cs and the Godot 4.7.2 Mono smoke.
/// This is a real canonical Human+AI collective turn, not a second UI-owned
/// worker store, AI model stub or fabricated settlement result.
/// </summary>
public sealed class FoundationHumanAiLocalPreviewTests
{
    private static FoundationHumanAiLocalPreview Create() =>
        FoundationHumanAiLocalPreview.CreateLocalPreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static StrategicCellId Adjacent(
        FoundationHumanAiLocalPreview host, PhysicalWorkerId workerId)
    {
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var worker = host.Snapshot.Workers.Workers.Single(x => x.Id == workerId);
        return graph.GetCellId(graph.GetNeighborIndexes(
            graph.GetNodeIndex(worker.CellId))[0]);
    }

    [Fact]
    public void SceneHostBootstrapsCanonicalCensusAndExplicitHumanPlusAi()
    {
        var game = Create();
        Assert.Equal(new CivilizationId(1UL), game.PlayerCivilization);
        Assert.Equal(new ulong[] { 2UL }, game.AiCivilizations.Select(x => x.Value));
        Assert.Empty(game.ReadyCivilizations);
        Assert.Equal(6, game.Snapshot.Workers.Workers.Count);
        Assert.Equal(3, game.ControllableWorkers.Count);
        Assert.Equal(9U, game.StateHash.FormatVersion);
        Assert.Equal(0UL, game.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), game.NextTurn);
        Assert.Null(game.LastTurnReceipt);
        Assert.Equal(0, game.LastAiEventCount);
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL).Activity);
        Assert.Equal(2_000_000UL,
            game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                .OutputMicroPerTurn);
        Assert.Equal(PhysicalWorkerActivity.Military,
            game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 6UL).Activity);
        Assert.Equal(0UL,
            game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 6UL)
                .OutputMicroPerTurn);
    }

    [Fact]
    public void EnqueueTwoHumanMovesDoesNotChangeAuthorityOrPrematurelyRunAi()
    {
        var game = Create();
        var initial = game.Snapshot;
        var hash = game.StateHash;
        var destination = Adjacent(game, new PhysicalWorkerId(1UL));
        game.QueueMove(new PhysicalWorkerId(1UL), destination);
        game.QueueMove(new PhysicalWorkerId(2UL), destination);
        Assert.Equal(2, game.PendingOrderCount);
        Assert.Equal(new ulong[] { 1UL, 2UL },
            game.PendingOrders.Select(order => order.WorkerId.Value));
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            game.PendingOrders[0].NextActivity);
        Assert.Same(initial, game.Snapshot);
        Assert.Equal(hash, game.StateHash);
        Assert.Equal(0UL, game.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), game.NextTurn);
        Assert.Empty(game.ReadyCivilizations);
        Assert.Null(game.LastTurnReceipt);
    }

    [Fact]
    public void EndTurnClosesTwoHumanOrdersAndOneRealAiActionInOneCollectiveJournal()
    {
        var game = Create();
        var old = game.Snapshot;
        var destination = Adjacent(game, new PhysicalWorkerId(1UL));
        game.QueueMove(new PhysicalWorkerId(1UL), destination);
        game.QueueMove(new PhysicalWorkerId(2UL), destination);
        var receipt = game.EndTurn();
        Assert.Equal(new TurnNumber(1UL), receipt.Turn);
        Assert.Equal(3, receipt.ExecutedEvents);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(new ulong[] { 1UL, 2UL, 4UL },
            receipt.Events.Select(ev => ev.WorkerId.Value));
        Assert.Equal(new ulong[] { 1UL, 1UL, 2UL },
            receipt.Events.Select(ev => ev.Issuer.Value));
        Assert.Equal(1, game.LastAiEventCount);
        Assert.Equal(3UL, receipt.Revision);
        Assert.Equal(3UL, game.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), game.NextTurn);
        Assert.Equal(receipt.Hash, game.StateHash);
        Assert.Equal(9U, receipt.Hash.FormatVersion);
        Assert.Same(receipt, game.LastTurnReceipt);
        Assert.Equal(0, game.PendingOrderCount);
        Assert.Empty(game.ReadyCivilizations);
        Assert.Equal(6, game.Snapshot.Workers.Workers.Count);
        Assert.NotSame(old, game.Snapshot);
        Assert.All(game.Snapshot.Workers.Workers
            .Where(w => w.Id.Value is 1UL or 2UL),
            w => Assert.Equal(destination, w.CellId));
        Assert.Equal(PhysicalWorkerActivity.Producing,
            game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL).Activity);
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            old.Workers.Workers.Single(w => w.Id.Value == 4UL).Activity);
    }

    [Fact]
    public void RepeatedIdleTurnsDoNotRegenerateTheAiProductionEvent()
    {
        var game = Create();
        var initial = game.Snapshot;
        var first = game.EndTurn();
        Assert.Single(first.Events); // only AI worker #4 resumes production
        Assert.Equal(1, game.LastAiEventCount);
        Assert.Equal(1UL, game.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), game.NextTurn);
        Assert.NotSame(initial, game.Snapshot);
        var afterFirst = game.Snapshot;
        var hash = game.StateHash;
        var second = game.EndTurn();
        Assert.Empty(second.Events);
        Assert.Equal(0, game.LastAiEventCount);
        Assert.Equal(1UL, game.Snapshot.Revision);
        Assert.Equal(new TurnNumber(3UL), game.NextTurn);
        Assert.Same(afterFirst, game.Snapshot);
        Assert.Equal(hash, game.StateHash);
    }

    [Fact]
    public void ProductionAfterTravelAndAnotherWorkerMovementCanShareTheNextTurn()
    {
        var game = Create();
        var first = new PhysicalWorkerId(1UL);
        var second = new PhysicalWorkerId(2UL);
        var source = game.Snapshot.Workers.Workers.Single(w => w.Id == first).CellId;
        var destination = Adjacent(game, first);
        game.QueueMove(first, destination);
        game.EndTurn();
        Assert.Equal(2UL, game.Snapshot.Revision); // human move + AI resume

        var work = game.QueueActivity(first, PhysicalWorkerActivity.Producing);
        var move = game.QueueMove(second, destination);
        Assert.False(work.IsMovement);
        Assert.True(move.IsMovement);
        var receipt = game.EndTurn();
        Assert.Equal(new TurnNumber(2UL), receipt.Turn);
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.Equal(0, game.LastAiEventCount);
        Assert.Equal(4UL, game.Snapshot.Revision);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            game.Snapshot.Workers.Workers.Single(w => w.Id == first).Activity);
        Assert.Equal(destination,
            game.Snapshot.Workers.Workers.Single(w => w.Id == second).CellId);
        Assert.Equal(source,
            game.Session.GeneratedWorld.StrategicSurfaceGraph.GetCellId(0));
    }

    [Fact]
    public void CancelReplaceAndInvalidMovesNeverExecuteAiOrAlterWorkerState()
    {
        var game = Create();
        var worker = new PhysicalWorkerId(1UL);
        var graph = game.Session.GeneratedWorld.StrategicSurfaceGraph;
        var at = game.Snapshot.Workers.Workers.Single(w => w.Id == worker).CellId;
        var near = graph.GetNeighborIndexes(graph.GetNodeIndex(at));
        var initial = game.Snapshot;
        var hash = game.StateHash;
        var a = graph.GetCellId(near[0]);
        var b = graph.GetCellId(near[1]);
        Assert.True(game.CanMove(worker, a));
        game.QueueMove(worker, a);
        game.QueueMove(worker, b);
        Assert.Single(game.PendingOrders);
        Assert.Equal(b, game.PendingOrders.Single().Destination);
        Assert.True(game.CancelQueuedOrder(worker));
        Assert.False(game.CancelQueuedOrder(worker));
        Assert.Empty(game.PendingOrders);
        Assert.False(game.CanMove(worker, at));
        Assert.False(game.CanMove(worker, new StrategicCellId(93UL)));
        Assert.Throws<InvalidOperationException>(() => game.QueueMove(
            worker, new StrategicCellId(93UL)));
        Assert.Throws<InvalidOperationException>(() => game.QueueMove(
            new PhysicalWorkerId(4UL), b));
        Assert.Throws<InvalidOperationException>(() => game.QueueActivity(
            new PhysicalWorkerId(6UL), PhysicalWorkerActivity.Producing));
        Assert.Same(initial, game.Snapshot);
        Assert.Equal(hash, game.StateHash);
        Assert.Empty(game.ReadyCivilizations);
        Assert.Equal(0, game.LastAiEventCount);
    }

    [Fact]
    public void SceneHostDoesNotExposeOpponentAsControllableWorker()
    {
        var game = Create();
        Assert.Equal(new ulong[] { 1UL, 2UL, 3UL },
            game.ControllableWorkers.Select(worker => worker.Id.Value));
        Assert.False(game.CanMove(new PhysicalWorkerId(4UL),
            Adjacent(game, new PhysicalWorkerId(4UL))));
        Assert.False(game.CanChangeActivity(
            new PhysicalWorkerId(4UL), PhysicalWorkerActivity.Producing));
        Assert.False(game.CanChangeActivity(
            new PhysicalWorkerId(3UL), PhysicalWorkerActivity.Producing));
        Assert.False(game.CanChangeActivity(
            new PhysicalWorkerId(1UL), PhysicalWorkerActivity.Producing));
        Assert.Empty(game.PendingOrders);
    }

    [Fact]
    public void TwoIndependentVisualSessionsReplayIdenticalHumanAiTurnHashes()
    {
        var one = Create();
        var two = Create();
        var destination = Adjacent(one, new PhysicalWorkerId(1UL));
        one.QueueMove(new PhysicalWorkerId(1UL), destination);
        one.QueueMove(new PhysicalWorkerId(2UL), destination);
        two.QueueMove(new PhysicalWorkerId(2UL), destination);
        two.QueueMove(new PhysicalWorkerId(1UL), destination);
        var first = one.EndTurn();
        var second = two.EndTurn();
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(one.StateHash, two.StateHash);
        Assert.Equal(one.Snapshot.Workers.Workers.Count,
            two.Snapshot.Workers.Workers.Count);
        Assert.Equal(3, first.ExecutedEvents);
        Assert.Equal(1, one.LastAiEventCount);
    }

    [Fact]
    public void SnapshotReplacementBeforeEndTurnFailsWithoutImplicitReconciliation()
    {
        var game = Create();
        var destination = Adjacent(game, new PhysicalWorkerId(1UL));
        game.QueueMove(new PhysicalWorkerId(1UL), destination);
        var changed = game.Snapshot.AdvanceRevision();
        game.Session.AcceptSnapshot(changed);
        Assert.Throws<InvalidOperationException>(() => game.EndTurn());
        Assert.Equal(new TurnNumber(1UL), game.NextTurn);
        Assert.Single(game.PendingOrders);
        Assert.Same(changed, game.Snapshot);
        Assert.Null(game.LastTurnReceipt);
    }

    [Fact]
    public void HumanAiPreviewNeverCreatesEconomicPointOrSettlement()
    {
        var game = Create();
        game.QueueMove(new PhysicalWorkerId(1UL),
            Adjacent(game, new PhysicalWorkerId(1UL)));
        var receipt = game.EndTurn();
        Assert.Equal(2UL, receipt.Revision); // one human move + one AI activity
        Assert.Empty(game.Snapshot.Economy.EconomicPoints);
        Assert.False(game.Snapshot.Population.IsInitialized);
        Assert.Empty(game.Snapshot.Warfare.Units);
        Assert.Equal(6, game.Snapshot.Workers.Workers.Count);
    }
}
