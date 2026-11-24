using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationWorkerOrderQueueTests
{
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly CivilizationId Foreign = new(2UL);
    private static readonly PhysicalWorkerId Worker = new(7UL);
    private static readonly PhysicalTacticalTileId Node =
        new(new GoldbergParameters(18, 0), new StrategicCellId(178UL));

    private static FoundationWorkerOrderQueue Empty() =>
        FoundationWorkerOrderQueue.Empty(Worker, Owner);

    [Fact]
    public void InsertRemoveAndReorderPreserveIdentityAndPriorSnapshot()
    {
        var empty = Empty();
        var a = empty.Insert(Owner, 0, FoundationWorkerOrderKind.Move, destination: Node);
        var b = a.Insert(Owner, 1, FoundationWorkerOrderKind.Build, projectId: 12UL);
        var c = b.Insert(Owner, 1, FoundationWorkerOrderKind.Occupation,
            occupation: PhysicalWorkerOccupation.Agriculture);
        Assert.Equal(new ulong[] { 1, 3, 2 }, c.Orders.Select(o => o.Id));
        var d = c.Reorder(Owner, 2, 0);
        Assert.Equal(new ulong[] { 2, 1, 3 }, d.Orders.Select(o => o.Id));
        var e = d.Remove(Owner, 1).Insert(Owner, 2, FoundationWorkerOrderKind.Move, destination: Node);
        Assert.Equal(new ulong[] { 2, 3, 4 }, e.Orders.Select(o => o.Id));
        Assert.Equal(5UL, e.NextId);
        Assert.Empty(empty.Orders);
        Assert.Single(a.Orders);
        Assert.Equal(new ulong[] { 1, 3, 2 }, c.Orders.Select(o => o.Id));
        Assert.Equal(12UL, e.Orders[0].ProjectId);
    }

    [Fact]
    public void ActiveHeadPersistsAndOnlyTailCanBeEdited()
    {
        var queue = Empty().Insert(Owner, 0, FoundationWorkerOrderKind.Build, projectId: 5UL)
            .Insert(Owner, 1, FoundationWorkerOrderKind.Move, destination: Node)
            .StartHead(Owner);
        Assert.Throws<InvalidOperationException>(() => queue.Remove(Owner, 1));
        Assert.Throws<InvalidOperationException>(() => queue.Reorder(Owner, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Reorder(Owner, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Insert(Owner, 0, FoundationWorkerOrderKind.Build, projectId: 8UL));
        Assert.Throws<InvalidOperationException>(() => queue.StartHead(Owner));
        Assert.Throws<InvalidOperationException>(() => queue.CompleteHead(Owner, 2));
        var next = queue.Insert(Owner, 2, FoundationWorkerOrderKind.Build, projectId: 9UL)
            .Reorder(Owner, 3, 1);
        Assert.Equal(1UL, next.ActiveOrderId);
        var done = next.CompleteHead(Owner, 1);
        Assert.Null(done.ActiveOrderId);
        Assert.Equal(new ulong[] { 3, 2 }, done.Orders.Select(o => o.Id));
        Assert.Equal(3UL, done.StartHead(Owner).ActiveOrderId);
        Assert.Equal(1UL, queue.ActiveOrderId);
    }

    [Fact]
    public void RejectsForeignEditsAndMalformedPayloads()
    {
        var queue = Empty().Insert(Owner, 0, FoundationWorkerOrderKind.Move, destination: Node);
        Assert.Throws<InvalidOperationException>(() => queue.Insert(Foreign, 1, FoundationWorkerOrderKind.Build, projectId: 1UL));
        Assert.Throws<InvalidOperationException>(() => queue.Remove(Foreign, 1));
        Assert.Throws<InvalidOperationException>(() => queue.Reorder(Foreign, 1, 0));
        Assert.Throws<InvalidOperationException>(() => queue.StartHead(Foreign));
        Assert.Throws<InvalidOperationException>(() => queue.StartHead(Owner).CompleteHead(Foreign, 1));
        Assert.Throws<ArgumentException>(() => queue.Insert(Owner, 1, FoundationWorkerOrderKind.Move));
        Assert.Throws<ArgumentException>(() => queue.Insert(Owner, 1, FoundationWorkerOrderKind.Build, projectId: 0UL));
        Assert.Throws<ArgumentException>(() => queue.Insert(Owner, 1, FoundationWorkerOrderKind.Build, destination: Node, projectId: 1UL));
        Assert.Throws<ArgumentException>(() => queue.Insert(Owner, 1, (FoundationWorkerOrderKind)99));
        Assert.Throws<ArgumentException>(() => queue.Insert(Owner, 1, FoundationWorkerOrderKind.Occupation, occupation: (PhysicalWorkerOccupation)999));
        Assert.Throws<InvalidOperationException>(() => queue.Remove(Owner, 999));
    }

    [Fact]
    public void SnapshotAdmissionRejectsDuplicatesAndInvalidHeadAndCopiesInput()
    {
        var item = new FoundationWorkerOrder(1, FoundationWorkerOrderKind.Move, Destination: Node);
        var input = new[] { item };
        var q = new FoundationWorkerOrderQueue(Worker, Owner, 2, null, input);
        input[0] = item with { Id = 99 };
        Assert.Equal(1UL, q.Orders[0].Id);
        Assert.Throws<ArgumentException>(() => new FoundationWorkerOrderQueue(Worker, Owner, 2, null, new[] { item, item }));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerOrderQueue(Worker, Owner, 1, null, new[] { item }));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerOrderQueue(Worker, Owner, 2, 9, new[] { item }));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerOrderQueue(Worker, Owner, 3, 2, new[] { item, item with { Id = 2 } }));
    }

    [Fact]
    public void QueueSetCanonicalizesWorkersWithoutSortingTheirOrders()
    {
        var a = Empty().Insert(Owner, 0, FoundationWorkerOrderKind.Build, projectId: 1UL)
            .Insert(Owner, 0, FoundationWorkerOrderKind.Move, destination: Node);
        var b = FoundationWorkerOrderQueue.Empty(new PhysicalWorkerId(2UL), Foreign);
        var set = new FoundationWorkerOrderQueueSet(new[] { a, b });
        Assert.Equal(new ulong[] { 2, 7 }, set.Queues.Select(q => q.Worker.Value));
        Assert.Equal(new ulong[] { 2, 1 }, set.Queues[1].Orders.Select(o => o.Id));
        var next = set.Remove(Owner, Worker, 1);
        Assert.Single(next.Queues[1].Orders);
        Assert.Equal(2, set.Queues[1].Orders.Count);
        Assert.Same(b, next.Queues[0]);
        Assert.Throws<InvalidOperationException>(() => set.Remove(Foreign, Worker, 1));
        Assert.Throws<InvalidOperationException>(() => set.Remove(Owner, new PhysicalWorkerId(999UL), 1));
        Assert.Throws<InvalidOperationException>(() => FoundationWorkerOrderQueueSet.Uninitialized.Remove(Owner, Worker, 1));
        Assert.Throws<ArgumentException>(() => new FoundationWorkerOrderQueueSet(new[] { a, a }));
        Assert.False(FoundationWorkerOrderQueueSet.Uninitialized.IsInitialized);
        Assert.True(new FoundationWorkerOrderQueueSet(Array.Empty<FoundationWorkerOrderQueue>()).IsInitialized);
    }
}
