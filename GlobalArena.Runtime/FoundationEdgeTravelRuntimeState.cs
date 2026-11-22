using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Opt-in, immutable journal-replayable ledger of INCOMPLETE fine-edge journeys.
/// No copy of freight, tolls, worker residence, or navigation topology is stored.
/// A journey stays at Source until its remaining duration reaches zero.
/// </summary>
public sealed record FoundationPendingEdgeTravel(
    PhysicalWorkerId Worker,
    CivilizationId Owner,
    PhysicalTacticalTileId Source,
    PhysicalTacticalTileId Destination,
    PhysicalTacticalEdgeId Edge,
    ulong RemainingMicro,
    ulong OriginalDurationMicro);

public sealed class FoundationEdgeTravelRuntimeState
{
    private static readonly FoundationEdgeTravelRuntimeState Missing =
        new(false, Array.Empty<FoundationPendingEdgeTravel>());

    public static FoundationEdgeTravelRuntimeState Uninitialized => Missing;

    public bool IsInitialized { get; }
    public IReadOnlyList<FoundationPendingEdgeTravel> Pending { get; }

    public FoundationEdgeTravelRuntimeState(
        IEnumerable<FoundationPendingEdgeTravel> pending)
        : this(true, (pending ?? throw new ArgumentNullException(nameof(pending))).ToArray())
    {
    }

    private FoundationEdgeTravelRuntimeState(bool initialized,
        FoundationPendingEdgeTravel[] records)
    {
        if (!initialized && records.Length > 0)
            throw new InvalidOperationException("Uninitialized journeys cannot contain records.");
        if (records.Any(p => p is null || !p.Worker.IsValid ||
            !p.Owner.IsValid || !p.Source.IsValid || !p.Destination.IsValid ||
            !p.Edge.IsValid || p.Source == p.Destination ||
            p.RemainingMicro == 0UL || p.OriginalDurationMicro == 0UL ||
            p.RemainingMicro > p.OriginalDurationMicro ||
            p.Source.FineGoldbergParameters != p.Destination.FineGoldbergParameters ||
            p.Edge.FineGoldbergParameters != p.Source.FineGoldbergParameters))
            throw new InvalidOperationException("Invalid unfinished physical edge journey.");
        var sorted = records.OrderBy(p => p.Worker.Value).ToArray();
        for (int i = 1; i < sorted.Length; i++)
            if (sorted[i - 1].Worker == sorted[i].Worker)
                throw new InvalidOperationException("A worker cannot have multiple open journeys.");
        IsInitialized = initialized;
        Pending = Array.AsReadOnly(sorted);
    }

    public FoundationEdgeTravelRuntimeState Replace(
        PhysicalWorkerId worker, FoundationPendingEdgeTravel? next)
    {
        if (!IsInitialized || !worker.IsValid ||
            (next is not null && next.Worker != worker))
            throw new InvalidOperationException("Travel ledger replacement is not authorized.");
        var current = Pending.Where(p => p.Worker != worker).ToList();
        if (next is not null) current.Add(next);
        return new FoundationEdgeTravelRuntimeState(current);
    }
}
