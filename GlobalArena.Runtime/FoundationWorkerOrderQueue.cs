using GlobalArena.World;

namespace GlobalArena.Runtime.PersistentOrders;

/// <summary>Intent only: no physical progress, treasury or work is stored here.</summary>
public enum FoundationWorkerOrderKind { Move = 1, Build = 2, Occupation = 3 }

public sealed record FoundationPersistentWorkerIntent(
    ulong Id, FoundationWorkerOrderKind Kind,
    PhysicalTacticalTileId? Destination = null,
    ulong? ProjectId = null,
    PhysicalWorkerOccupation? Occupation = null, byte? Crop = null)
{
    internal bool IsValid => Id != 0 && (!Crop.HasValue || (Crop.Value >= 1 && Crop.Value <= 6 && Kind == FoundationWorkerOrderKind.Occupation && Occupation == PhysicalWorkerOccupation.Agriculture)) && (Kind switch
    {
        FoundationWorkerOrderKind.Move => Destination.HasValue && Destination.Value.IsValid &&
            ProjectId is null && Occupation is null,
        FoundationWorkerOrderKind.Build => Destination is null &&
            ProjectId is > 0 && Occupation is null,
        FoundationWorkerOrderKind.Occupation => Destination is null &&
            ProjectId is null && Occupation.HasValue && Enum.IsDefined(Occupation.Value),
        _ => false
    });
}

/// <summary>
/// Ordered immutable intentions for one worker. The optional active head is
/// protected from ordinary edits; explicit interruption is a separate operation.
/// IDs are monotonic per worker and never reused, even after removal.
/// Physical travel and project eligibility are checked by future executors.
/// </summary>
public sealed class FoundationWorkerOrderQueue
{
    public PhysicalWorkerId Worker { get; }
    public CivilizationId Owner { get; }
    public FoundationWorkerBaseActivity? BaseActivity { get; }
    public ulong NextId { get; }
    public ulong? ActiveOrderId { get; }
    public IReadOnlyList<FoundationPersistentWorkerIntent> Orders { get; }

    public FoundationWorkerOrderQueue(PhysicalWorkerId worker, CivilizationId owner,
        ulong nextId, ulong? activeOrderId, IEnumerable<FoundationPersistentWorkerIntent> orders,
        FoundationWorkerBaseActivity? baseActivity = null)
    {
        ArgumentNullException.ThrowIfNull(orders);
        var rows = orders.ToArray();
        if (!worker.IsValid || !owner.IsValid || nextId == 0 ||
            rows.Any(o => o is null || !o.IsValid || o.Id >= nextId) ||
            rows.Select(o => o.Id).Distinct().Count() != rows.Length ||
            (activeOrderId.HasValue && (rows.Length == 0 || rows[0].Id != activeOrderId.Value)))
            throw new ArgumentException("Invalid worker order queue.");
        if (baseActivity is not null && rows.Any(o => o.Kind == FoundationWorkerOrderKind.Occupation))
            throw new ArgumentException("Base-enabled queues accept finite tasks only.");
        BaseActivity = baseActivity;
        Worker = worker;
        Owner = owner;
        NextId = nextId;
        ActiveOrderId = activeOrderId;
        Orders = Array.AsReadOnly(rows);
    }

    public static FoundationWorkerOrderQueue Empty(PhysicalWorkerId worker, CivilizationId owner)
        => new(worker, owner, 1, null, Array.Empty<FoundationPersistentWorkerIntent>());

    public FoundationWorkerOrderQueue SetBaseActivity(CivilizationId issuer, FoundationWorkerBaseActivity activity)
    {
        Authorize(issuer);
        ArgumentNullException.ThrowIfNull(activity);
        return new(Worker, Owner, NextId, ActiveOrderId, Orders, activity);
    }

    private void Authorize(CivilizationId issuer)
    {
        if (issuer != Owner) throw new InvalidOperationException("Foreign worker queue.");
    }

    private int EditableIndex(ulong id)
    {
        if (ActiveOrderId == id) throw new InvalidOperationException("Active order requires explicit interruption.");
        for (int i = 0; i < Orders.Count; i++) if (Orders[i].Id == id) return i;
        throw new InvalidOperationException("Unknown order.");
    }

    private void RequireInsertionIndex(int index, int count)
    {
        if (index < (ActiveOrderId.HasValue ? 1 : 0) || index > count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    public FoundationWorkerOrderQueue Insert(CivilizationId issuer, int index,
        FoundationWorkerOrderKind kind, PhysicalTacticalTileId? destination = null,
        ulong? projectId = null, PhysicalWorkerOccupation? occupation = null, byte? crop = null)
    {
        Authorize(issuer);
        RequireInsertionIndex(index, Orders.Count);
        var rows = Orders.ToList();
        rows.Insert(index, new FoundationPersistentWorkerIntent(NextId, kind, destination, projectId, occupation, crop));
        return new(Worker, Owner, checked(NextId + 1), ActiveOrderId, rows, BaseActivity);
    }

    public FoundationWorkerOrderQueue Remove(CivilizationId issuer, ulong id)
    {
        Authorize(issuer);
        var rows = Orders.ToList();
        rows.RemoveAt(EditableIndex(id));
        return new(Worker, Owner, NextId, ActiveOrderId, rows);
    }

    /// <summary>Index in the final queue, after removing the moved item.</summary>
    public FoundationWorkerOrderQueue Reorder(CivilizationId issuer, ulong id, int finalIndex)
    {
        Authorize(issuer);
        var old = EditableIndex(id);
        var rows = Orders.ToList();
        var item = rows[old];
        rows.RemoveAt(old);
        RequireInsertionIndex(finalIndex, rows.Count);
        rows.Insert(finalIndex, item);
        return new(Worker, Owner, NextId, ActiveOrderId, rows);
    }

    // These are value-model lifecycle transitions, not authority to execute
    // or interrupt physical travel. R207+ must authorize them in the journal.
    public FoundationWorkerOrderQueue StartHead(CivilizationId issuer)
    {
        Authorize(issuer);
        if (ActiveOrderId.HasValue || Orders.Count == 0)
            throw new InvalidOperationException("No inactive head to start.");
        return new(Worker, Owner, NextId, Orders[0].Id, Orders, BaseActivity);
    }

    public FoundationWorkerOrderQueue InterruptHead(CivilizationId issuer, ulong expectedId)
        => CompleteHead(issuer, expectedId);

    public FoundationWorkerOrderQueue CompleteHead(CivilizationId issuer, ulong expectedId)
    {
        Authorize(issuer);
        if (ActiveOrderId != expectedId)
            throw new InvalidOperationException("Stale or inactive head.");
        return new(Worker, Owner, NextId, null, Orders.Skip(1), BaseActivity);
    }
}

/// <summary>Immutable collection with canonical worker ordering, preserving order within each queue.</summary>
public sealed class FoundationWorkerOrderQueueSet
{
    public bool IsInitialized { get; }
    public IReadOnlyList<FoundationWorkerOrderQueue> Queues { get; }
    public static FoundationWorkerOrderQueueSet Uninitialized { get; } =
        new(false, Array.Empty<FoundationWorkerOrderQueue>());
    public FoundationWorkerOrderQueueSet(IEnumerable<FoundationWorkerOrderQueue> queues)
        : this(true, queues) { }
    private FoundationWorkerOrderQueueSet(bool initialized, IEnumerable<FoundationWorkerOrderQueue> queues)
    {
        ArgumentNullException.ThrowIfNull(queues);
        var rows = queues.ToArray();
        if (rows.Any(q => q is null) || rows.Select(q => q.Worker).Distinct().Count() != rows.Length)
            throw new ArgumentException("Duplicate or invalid worker queue.");
        IsInitialized = initialized;
        Queues = Array.AsReadOnly(rows.OrderBy(q => q.Worker.Value).ToArray());
    }

    private FoundationWorkerOrderQueue Get(CivilizationId issuer, PhysicalWorkerId worker)
    {
        var queue = Queues.SingleOrDefault(q => q.Worker == worker);
        if (!IsInitialized || queue is null || queue.Owner != issuer)
            throw new InvalidOperationException("Unauthorized worker queue.");
        return queue;
    }

    public FoundationWorkerOrderQueueSet SetBaseActivity(CivilizationId issuer, PhysicalWorkerId worker,
        FoundationWorkerBaseActivity activity)
        => Replace(issuer, Get(issuer, worker).SetBaseActivity(issuer, activity));

    public FoundationWorkerOrderQueueSet Insert(CivilizationId issuer, PhysicalWorkerId worker,
        int index, FoundationWorkerOrderKind kind, PhysicalTacticalTileId? destination = null,
        ulong? projectId = null, PhysicalWorkerOccupation? occupation = null, byte? crop = null)
        => Replace(issuer, Get(issuer, worker).Insert(issuer, index, kind, destination, projectId, occupation, crop));

    public FoundationWorkerOrderQueueSet Remove(CivilizationId issuer, PhysicalWorkerId worker, ulong id)
        => Replace(issuer, Get(issuer, worker).Remove(issuer, id));

    public FoundationWorkerOrderQueueSet Reorder(CivilizationId issuer, PhysicalWorkerId worker, ulong id, int finalIndex)
        => Replace(issuer, Get(issuer, worker).Reorder(issuer, id, finalIndex));

    public FoundationWorkerOrderQueueSet StartHead(CivilizationId issuer, PhysicalWorkerId worker)
        => Replace(issuer, Get(issuer, worker).StartHead(issuer));

    public FoundationWorkerOrderQueueSet InterruptHead(CivilizationId issuer, PhysicalWorkerId worker, ulong id)
        => Replace(issuer, Get(issuer, worker).InterruptHead(issuer, id));

    public FoundationWorkerOrderQueueSet CompleteHead(CivilizationId issuer, PhysicalWorkerId worker, ulong id)
        => Replace(issuer, Get(issuer, worker).CompleteHead(issuer, id));

    // No arbitrary replacement from untrusted snapshots.
    private FoundationWorkerOrderQueueSet Replace(CivilizationId issuer, FoundationWorkerOrderQueue next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var prior = Queues.SingleOrDefault(q => q.Worker == next.Worker);
        if (!IsInitialized || prior is null || prior.Owner != issuer || next.Owner != issuer ||
            next.NextId < prior.NextId)
            throw new InvalidOperationException("Unauthorized queue replacement.");
        return new(Queues.Select(q => q.Worker == next.Worker ? next : q));
    }
}
