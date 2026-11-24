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
        Assert.Empty(last.WorkerOrders.Queues.Single(q => q.Worker == w.Id).Orders);
        Assert.Equal(2UL, last.WorkerOrders.Queues.Single(q => q.Worker == w.Id).NextId);
    }
}
