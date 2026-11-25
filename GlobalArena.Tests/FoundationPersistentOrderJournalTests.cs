using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPersistentOrderJournalTests
{
    private static WorldState Fixture() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 1).Session.Snapshot;

    [Fact]
    public void OptInHashAndEditsPreserveExistingWorldAndQueueOrder()
    {
        var legacy = Fixture();
        var hash = new CanonicalWorldStateHasher();
        var before = hash.Compute(legacy);
        var world = legacy.DeployInitialWorkerOrders();
        Assert.Equal(20U, hash.Compute(world).FormatVersion);
        Assert.False(legacy.WorkerOrders.IsInitialized);
        Assert.Equal(before, hash.Compute(legacy));
        Assert.Equal(world.Workers.Workers.Count, world.WorkerOrders.Queues.Count);
        var w = world.Workers.Workers.First();
        var a = world.InsertWorkerOrder(w.Owner, w.Id, 0, FoundationWorkerOrderKind.Move, w.TacticalTileId);
        var b = a.InsertWorkerOrder(w.Owner, w.Id, 1, FoundationWorkerOrderKind.Occupation,
            occupation: PhysicalWorkerOccupation.Agriculture);
        var c = b.ReorderWorkerOrder(w.Owner, w.Id, 2, 0);
        Assert.NotEqual(hash.Compute(b), hash.Compute(c));
        Assert.Equal(new ulong[] { 2, 1 }, c.AdvanceRevision().WithWorkers(c.Workers)
            .WorkerOrders.Queues.Single(q => q.Worker == w.Id).Orders.Select(o => o.Id));
        Assert.Empty(world.WorkerOrders.Queues.Single(q => q.Worker == w.Id).Orders);
        var removed = a.RemoveWorkerOrder(w.Owner, w.Id, 1);
        Assert.NotEqual(hash.Compute(world), hash.Compute(removed)); // next ID is canonical
        Assert.Throws<InvalidOperationException>(() => world.DeployInitialWorkerOrders());
    }

    [Fact]
    public void JournalRecomputesEditsAndRejectsTamperingOrStaleEvents()
    {
        var world = Fixture().DeployInitialWorkerOrders();
        var w = world.Workers.Workers.First();
        var other = world.Workers.Workers.First(p => p.Owner != w.Owner);
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(7UL));
        var id = new EventId(new CommandId(turn, 1UL), 1UL);
        var edit = new FoundationPersistentOrderEdit(w.Owner, w.Id,
            FoundationPersistentOrderEditKind.Insert, OrderKind: FoundationWorkerOrderKind.Move,
            Destination: w.TacticalTileId);
        var (next, ev) = FoundationPersistentOrderEditJournal.Record(world, id, edit, context);
        var executor = new FoundationPersistentOrderEditJournal();
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(executor.Execute(world, ev, context)));
        Assert.Equal(world.Revision + 1, next.Revision);
        Assert.Throws<InvalidOperationException>(() => executor.Execute(next, ev, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(world,
            ev with { After = ev.Before }, context));
        Assert.Throws<InvalidOperationException>(() => FoundationPersistentOrderEditJournal.Record(
            world, id, edit with { Issuer = other.Owner }, context));
        var remove = new FoundationPersistentOrderEdit(w.Owner, w.Id,
            FoundationPersistentOrderEditKind.Remove, OrderId: 1);
        var (last, removal) = FoundationPersistentOrderEditJournal.Record(next,
            new EventId(new CommandId(turn, 2UL), 1UL), remove, context);
        var replay = executor.Execute(executor.Execute(world, ev, context), removal, context);
        Assert.Equal(hash.Compute(last), hash.Compute(replay));
        var log = new SimulationEventLog(new[]
        {
            new SimulationEventLogEntry(1UL, ev, wasEligible: true, wasExecuted: true),
            new SimulationEventLogEntry(2UL, removal, wasEligible: true, wasExecuted: true)
        });
        var journalReplay = new SimulationEventLogReplayer(executor).Replay(
            new SimulationReplayInput(world, log, context));
        Assert.Equal(hash.Compute(last), hash.Compute(journalReplay));
        Assert.Empty(last.WorkerOrders.Queues.Single(q => q.Worker == w.Id).Orders);
        Assert.Equal(2UL, last.WorkerOrders.Queues.Single(q => q.Worker == w.Id).NextId);
    }

    [Fact]
    public void R2231BasePreferenceIsCanonicalAndSurvivesFiniteQueueLifecycle()
    {
        var world = Fixture().DeployInitialWorkerOrders();
        var hash = new CanonicalWorldStateHasher();
        var before = hash.Compute(world);
        var w = world.Workers.Workers.First();
        var other = world.Workers.Workers.First(p => p.Owner != w.Owner);
        var rice = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual,
            PhysicalWorkerOccupation.Agriculture, 4);
        var automatic = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic);
        var enabled = world.SetWorkerBaseActivity(w.Owner, w.Id, rice);
        Assert.Equal(23U, hash.Compute(enabled).FormatVersion);
        Assert.Equal(20U, before.FormatVersion);
        Assert.Equal(before, hash.Compute(world));
        Assert.Same(world.Workers, enabled.Workers);
        Assert.NotEqual(hash.Compute(enabled), hash.Compute(enabled.SetWorkerBaseActivity(w.Owner, w.Id, automatic)));
        Assert.NotEqual(hash.Compute(enabled), hash.Compute(enabled.SetWorkerBaseActivity(w.Owner, w.Id,
            new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 1))));
        var queued = enabled.InsertWorkerOrder(w.Owner, w.Id, 0, FoundationWorkerOrderKind.Move, w.TacticalTileId)
            .InsertWorkerOrder(w.Owner, w.Id, 1, FoundationWorkerOrderKind.Move, w.TacticalTileId)
            .ReorderWorkerOrder(w.Owner, w.Id, 2, 0).RemoveWorkerOrder(w.Owner, w.Id, 1)
            .StartWorkerOrder(w.Owner, w.Id).CompleteWorkerOrder(w.Owner, w.Id, 2)
            .AdvanceRevision().WithWorkers(enabled.Workers);
        Assert.Equal(rice, queued.WorkerOrders.Queues.Single(q => q.Worker == w.Id).BaseActivity);
        Assert.Null(queued.WorkerOrders.Queues.Single(q => q.Worker == other.Id).BaseActivity);
        Assert.Throws<InvalidOperationException>(() => enabled.InsertWorkerOrder(w.Owner, w.Id, 0,
            FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Mining));
        Assert.Throws<InvalidOperationException>(() => world.InsertWorkerOrder(w.Owner, w.Id, 0,
            FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Mining)
            .SetWorkerBaseActivity(w.Owner, w.Id, rice));
        Assert.Throws<InvalidOperationException>(() => world.SetWorkerBaseActivity(other.Owner, w.Id, rice));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic, PhysicalWorkerOccupation.Mining));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining, 4));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 7));
    }

    [Fact]
    public void R2231BaseEditJournalReplaysAndRejectsTampering()
    {
        var world = Fixture().DeployInitialWorkerOrders();
        var w = world.Workers.Workers.First();
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(7UL));
        var id = new EventId(new CommandId(turn, 1UL), 1UL);
        var edit = new FoundationPersistentOrderEdit(w.Owner, w.Id,
            FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
            new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4));
        var (next, ev) = FoundationPersistentOrderEditJournal.Record(world, id, edit, context);
        var executor = new FoundationPersistentOrderEditJournal();
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(executor.Execute(world, ev, context)));
        Assert.Equal(world.Revision + 1, next.Revision);
        Assert.Throws<InvalidOperationException>(() => executor.Execute(next, ev, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(world, ev with {
            Edit = edit with { BaseActivity = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic) } }, context));
        Assert.Throws<InvalidOperationException>(() => FoundationPersistentOrderEditJournal.Record(world, id,
            edit with { Crop = 4 }, context));
    }
}
