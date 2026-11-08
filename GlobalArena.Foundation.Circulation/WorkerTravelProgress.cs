namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// Single universal time budget for one worker within a caller-owned period.
/// The caller must persist the returned copy after each action; there is
/// intentionally no separate "movement point" account.
/// </summary>
public readonly record struct WorkerActionTimeBudget
{
    public long TotalRaw { get; }
    public long SpentRaw { get; }
    public long RemainingRaw => TotalRaw - SpentRaw;

    private WorkerActionTimeBudget(long totalRaw, long spentRaw)
    {
        TotalRaw = totalRaw;
        SpentRaw = spentRaw;
    }

    public static WorkerActionTimeBudget Create(long totalRaw)
    {
        if (totalRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(totalRaw));
        return new WorkerActionTimeBudget(totalRaw, 0);
    }

    public WorkerActionTimeBudget Spend(long amountRaw)
    {
        if (amountRaw < 0 || amountRaw > RemainingRaw)
            throw new ArgumentOutOfRangeException(nameof(amountRaw),
                "Cannot spend a negative time or exceed remaining universal time.");
        return new WorkerActionTimeBudget(TotalRaw, checked(SpentRaw + amountRaw));
    }
}

/// <summary>
/// One worker is physically on an edge until elapsed travel meets required
/// time. Destination becomes available only at the arrival boundary.
/// No additional per-turn movement wallet or hidden teleport is created.
/// </summary>
public readonly record struct WorkerEdgeTransit
{
    public long OriginNodeId { get; }
    public long DestinationNodeId { get; }
    public long RequiredTimeRaw { get; }
    public long ElapsedTimeRaw { get; }
    public bool IsArrived => ElapsedTimeRaw == RequiredTimeRaw;
    public long? CurrentNodeId => IsArrived ? DestinationNodeId : null;

    private WorkerEdgeTransit(long originId, long destinationId,
        long requiredTimeRaw, long elapsedTimeRaw)
    {
        OriginNodeId = originId;
        DestinationNodeId = destinationId;
        RequiredTimeRaw = requiredTimeRaw;
        ElapsedTimeRaw = elapsedTimeRaw;
    }

    public static WorkerEdgeTransit Begin(long originId, long destinationId, long requiredTimeRaw)
    {
        if (originId < 0 || destinationId < 0 || originId == destinationId)
            throw new ArgumentException("A traversal requires two distinct valid node identifiers.");
        if (requiredTimeRaw < 1)
            throw new ArgumentOutOfRangeException(nameof(requiredTimeRaw));
        return new WorkerEdgeTransit(originId, destinationId, requiredTimeRaw, 0);
    }

    internal WorkerEdgeTransit WithElapsed(long elapsedRaw)
    {
        if (elapsedRaw < ElapsedTimeRaw || elapsedRaw > RequiredTimeRaw)
            throw new ArgumentOutOfRangeException(nameof(elapsedRaw));
        return new WorkerEdgeTransit(OriginNodeId, DestinationNodeId,
            RequiredTimeRaw, elapsedRaw);
    }
}

public readonly record struct WorkerTravelAdvance(
    WorkerEdgeTransit Transit,
    WorkerActionTimeBudget Budget,
    long TimeUsedRaw)
{
    public bool Arrived => Transit.IsArrived;
    public long? ArrivedAtNodeId => Transit.CurrentNodeId;
}

/// <summary>
/// Deterministic, strictly sequential resolution of one explicit travel action.
/// The caller owns timestamp ordering and arbitration with other workers.
/// Budget remainder is preserved for production, construction, or another
/// edge in the SAME period after confirmed arrival.
/// </summary>
public static class WorkerTravelProgress
{
    public static WorkerTravelAdvance Advance(
        WorkerEdgeTransit transit,
        WorkerActionTimeBudget periodBudget,
        long timeRequestedRaw)
    {
        if (transit.RequiredTimeRaw <= 0 ||
            transit.ElapsedTimeRaw < 0 ||
            transit.ElapsedTimeRaw > transit.RequiredTimeRaw ||
            transit.OriginNodeId < 0 ||
            transit.DestinationNodeId < 0 ||
            transit.OriginNodeId == transit.DestinationNodeId)
            throw new ArgumentException("Invalid in-progress traversal.", nameof(transit));
        if (transit.IsArrived)
            throw new InvalidOperationException("Arrival already completed. Start a different edge.");
        if (periodBudget.TotalRaw < 0 ||
            periodBudget.SpentRaw < 0 ||
            periodBudget.SpentRaw > periodBudget.TotalRaw)
            throw new ArgumentException("Invalid universal time budget.", nameof(periodBudget));
        if (timeRequestedRaw < 0 || timeRequestedRaw > periodBudget.RemainingRaw)
            throw new ArgumentOutOfRangeException(nameof(timeRequestedRaw));

        var remainingOnEdge = transit.RequiredTimeRaw - transit.ElapsedTimeRaw;
        var used = Math.Min(timeRequestedRaw, remainingOnEdge);
        return new WorkerTravelAdvance(
            transit.WithElapsed(checked(transit.ElapsedTimeRaw + used)),
            periodBudget.Spend(used),
            used);
    }
}
