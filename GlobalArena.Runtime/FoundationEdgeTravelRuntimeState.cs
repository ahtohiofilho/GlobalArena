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

/// <summary>Temporary vessel contract, fixed at embarkation.</summary>
public sealed record FoundationNavalVoyage(
    PhysicalWorkerId Worker, CivilizationId Owner,
    PhysicalTacticalTileId Origin, PhysicalTacticalEdgeId EmbarkationEdge,
    ulong EmbarkationTurn, ulong CrossingTimeMicro,
    ulong PaidNumerator, ulong PaidDenominator);

public sealed class FoundationEdgeTravelRuntimeState
{
    private static readonly FoundationEdgeTravelRuntimeState Missing =
        new(false, Array.Empty<FoundationPendingEdgeTravel>());

    public static FoundationEdgeTravelRuntimeState Uninitialized => Missing;

    public bool IsInitialized { get; }
    public IReadOnlyList<FoundationPendingEdgeTravel> Pending { get; }
    public IReadOnlyList<FoundationNavalVoyage>? NavalVoyages { get; }

    public FoundationEdgeTravelRuntimeState(
        IEnumerable<FoundationPendingEdgeTravel> pending)
        : this(true, (pending ?? throw new ArgumentNullException(nameof(pending))).ToArray())
    {
    }

    private FoundationEdgeTravelRuntimeState(bool initialized,
        FoundationPendingEdgeTravel[] records,
        FoundationNavalVoyage[]? naval = null)
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
        if (naval is not null)
        {
            if (!initialized || naval.Any(v => v is null || !v.Worker.IsValid ||
                !v.Owner.IsValid || !v.Origin.IsValid || !v.EmbarkationEdge.IsValid ||
                v.Origin.FineGoldbergParameters != v.EmbarkationEdge.FineGoldbergParameters ||
                v.EmbarkationTurn == 0 || v.CrossingTimeMicro == 0 ||
                v.PaidDenominator == 0 || v.PaidNumerator > v.PaidDenominator) ||
                naval.Select(v => v.Worker).Distinct().Count() != naval.Length)
                throw new InvalidOperationException("Invalid naval voyage ledger.");
            NavalVoyages = Array.AsReadOnly(naval.OrderBy(v => v.Worker.Value).ToArray());
        }
    }

    public FoundationEdgeTravelRuntimeState EnableNavalVoyages()
    {
        if (!IsInitialized || NavalVoyages is not null)
            throw new InvalidOperationException("Naval ledger requires an initialized legacy travel ledger.");
        return new FoundationEdgeTravelRuntimeState(true, Pending.ToArray(), Array.Empty<FoundationNavalVoyage>());
    }

    public FoundationEdgeTravelRuntimeState ReplaceNavalVoyage(
        PhysicalWorkerId worker, FoundationNavalVoyage? voyage)
    {
        if (NavalVoyages is null || !worker.IsValid || (voyage is not null && voyage.Worker != worker))
            throw new InvalidOperationException("Naval voyage ledger replacement is not authorized.");
        var entries = NavalVoyages.Where(v => v.Worker != worker).ToList();
        if (voyage is not null) entries.Add(voyage);
        return new FoundationEdgeTravelRuntimeState(true, Pending.ToArray(), entries.ToArray());
    }

    public FoundationEdgeTravelRuntimeState Replace(
        PhysicalWorkerId worker, FoundationPendingEdgeTravel? next)
    {
        if (!IsInitialized || !worker.IsValid ||
            (next is not null && next.Worker != worker))
            throw new InvalidOperationException("Travel ledger replacement is not authorized.");
        var current = Pending.Where(p => p.Worker != worker).ToList();
        if (next is not null) current.Add(next);
        return new FoundationEdgeTravelRuntimeState(true, current.ToArray(), NavalVoyages?.ToArray());
    }
}
