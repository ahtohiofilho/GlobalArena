using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Canonical, individual physical worker ID in the new foundation.
/// This is NOT an aggregate legacy EconomicPoint.Workforce member.
/// </summary>
public readonly record struct PhysicalWorkerId
{
    private readonly ulong _value;
    public bool IsValid => _value != 0UL;
    public ulong Value => IsValid ? _value :
        throw new InvalidOperationException("Uninitialized physical worker ID.");

    public PhysicalWorkerId(ulong value)
    {
        if (value == 0UL)
            throw new ArgumentOutOfRangeException(nameof(value));
        _value = value;
    }
}

public enum PhysicalWorkerActivity : uint
{
    Producing = 1,
    Traveling = 2,
    Military = 3,
    Other = 4
}

/// <summary>
/// v11 physical-worker productive occupation (WHAT the individual works on).
/// Distinct from Producing/Traveling/Military (WHAT the individual is doing).
/// These roles are not derived from CommodityId or from the legacy economic
/// point TradeLogistics enum. Unassigned is explicit for non-producers.
/// </summary>
public enum PhysicalWorkerOccupation : uint
{
    Unassigned = 0,
    Agriculture = 1,
    Forestry = 2,
    Mining = 3
}

/// <summary>
/// All accounting quantities are integer micro-units (1e6); authoritative
/// worker facts never contain doubles. Multipliers apply to demand exactly
/// once at the worker's current physical cell, even during travel/military.
/// </summary>
public sealed record PhysicalWorkerRuntimeRecord
{
    public PhysicalWorkerId Id { get; }
    public CivilizationId Owner { get; }
    public StrategicCellId CellId { get; }
    public CommodityId Commodity { get; }
    public ulong OutputMicroPerTurn { get; }
    public PhysicalWorkerActivity Activity { get; }
    public uint DemandMultiplierMicro { get; }

    // v10 opt-in: typed G(18,0) physical tile. The v9 CellId remains only
    // a coarse compatibility/presentation region when this is present.
    public PhysicalTacticalTileId? TacticalTileId { get; }

    // v11 opt-in. Null = schema v8/v9/v10 with no occupation assigned,
    // NOT an actual occupation of Unassigned.
    public PhysicalWorkerOccupation? Occupation { get; }

    public PhysicalWorkerRuntimeRecord(
        PhysicalWorkerId id, CivilizationId owner, StrategicCellId cellId,
        CommodityId commodity, ulong outputMicroPerTurn,
        PhysicalWorkerActivity activity, uint demandMultiplierMicro = 1_000_000U,
        PhysicalTacticalTileId? tacticalTileId = null,
        PhysicalWorkerOccupation? occupation = null)
    {
        if (!id.IsValid || !owner.IsValid || !cellId.IsValid ||
            !commodity.IsValid || !Enum.IsDefined(activity) ||
            demandMultiplierMicro == 0U ||
            (tacticalTileId.HasValue && !tacticalTileId.Value.IsValid) ||
            (occupation.HasValue && (!tacticalTileId.HasValue ||
                !Enum.IsDefined(occupation.Value) ||
                (activity == PhysicalWorkerActivity.Producing &&
                 occupation.Value == PhysicalWorkerOccupation.Unassigned))))
            throw new ArgumentException("Invalid physical worker identity, location or policy.");

        Id = id;
        Owner = owner;
        CellId = cellId;
        Commodity = commodity;
        OutputMicroPerTurn = outputMicroPerTurn;
        Activity = activity;
        DemandMultiplierMicro = demandMultiplierMicro;
        TacticalTileId = tacticalTileId;
        Occupation = occupation;
    }

    public PhysicalWorkerRuntimeRecord WithLocationAndActivity(
        StrategicCellId cell, PhysicalWorkerActivity activity)
    {
        if (TacticalTileId.HasValue)
            throw new InvalidOperationException(
                "v10 physical workers cannot move via v9 coarse Goldberg cells.");
        return new(Id, Owner, cell, Commodity, OutputMicroPerTurn,
            activity, DemandMultiplierMicro, occupation: Occupation);
    }

    /// <summary>
    /// Authorized v11 in-place assignment, not an implicit reclassification
    /// of earlier workers or a change to output, demand, item, or location.
    /// Only use from an accepted, replayed tactical occupational event.
    /// </summary>
    public PhysicalWorkerRuntimeRecord WithTacticalOccupation(
        PhysicalWorkerOccupation nextOccupation)
    {
        if (!TacticalTileId.HasValue || !Occupation.HasValue ||
            !Enum.IsDefined(nextOccupation) ||
            nextOccupation == Occupation.Value ||
            (Activity == PhysicalWorkerActivity.Producing &&
             nextOccupation == PhysicalWorkerOccupation.Unassigned))
            throw new InvalidOperationException(
                "Occupation assignment needs a different valid v11 productive role.");
        return new(Id, Owner, CellId, Commodity, OutputMicroPerTurn,
            Activity, DemandMultiplierMicro, TacticalTileId, nextOccupation);
    }

    public PhysicalWorkerRuntimeRecord WithTacticalLocationAndActivity(
        PhysicalTacticalTileId destination, StrategicCellId presentationRegion,
        PhysicalWorkerActivity activity)
    {
        if (!TacticalTileId.HasValue || !destination.IsValid ||
            destination.FineGoldbergParameters !=
                TacticalTileId.Value.FineGoldbergParameters)
            throw new InvalidOperationException(
                "Tactical movement requires an existing compatible v10 position.");
        return new(Id, Owner, presentationRegion, Commodity, OutputMicroPerTurn,
            activity, DemandMultiplierMicro, destination, Occupation);
    }
}

/// <summary>
/// Immutable authoritative census with explicit initialization marker.
/// An absent census is NOT equivalent to a known empty census.
/// </summary>
public sealed class PhysicalWorkerRuntimeState
{
    private static readonly PhysicalWorkerRuntimeState Missing =
        new(false, Array.Empty<PhysicalWorkerRuntimeRecord>());

    public static PhysicalWorkerRuntimeState Uninitialized => Missing;
    public bool IsInitialized { get; }
    public bool HasAuthoritativeTacticalLocations { get; }
    public bool HasAuthoritativeOccupations { get; }
    public IReadOnlyList<PhysicalWorkerRuntimeRecord> Workers { get; }

    public PhysicalWorkerRuntimeState(
        IEnumerable<PhysicalWorkerRuntimeRecord> workers,
        bool tacticalLocationsAuthoritative = false,
        bool occupationsAuthoritative = false)
        : this(true, (workers ?? throw new ArgumentNullException(nameof(workers))).ToArray(),
            tacticalLocationsAuthoritative, occupationsAuthoritative)
    {
    }

    private PhysicalWorkerRuntimeState(
        bool initialized, PhysicalWorkerRuntimeRecord[] workers,
        bool tacticalLocationsAuthoritative = false,
        bool occupationsAuthoritative = false)
    {
        if (!initialized && workers.Length != 0)
            throw new InvalidOperationException("Missing worker census must be empty.");
        if (workers.Any(x => x is null))
            throw new ArgumentException("Worker census cannot contain null records.");

        // The census is homogeneous by schema: never mix v9 coarse workers
        // and v10 fine workers within one authoritative snapshot.
        if (workers.Any(x => x.TacticalTileId.HasValue != tacticalLocationsAuthoritative))
            throw new ArgumentException(
                "Tactical worker positions must be complete and explicitly opted into v10.");
        if (occupationsAuthoritative && !tacticalLocationsAuthoritative)
            throw new ArgumentException(
                "Productive occupations require authoritative physical tactical tiles.");
        if (workers.Any(x => x.Occupation.HasValue != occupationsAuthoritative))
            throw new ArgumentException(
                "Worker occupations must be homogeneous and explicitly opted into v11.");

        if (tacticalLocationsAuthoritative && workers.Length > 0)
        {
            var parameters = workers[0].TacticalTileId!.Value.FineGoldbergParameters;
            if (workers.Any(x =>
                x.TacticalTileId!.Value.FineGoldbergParameters != parameters))
                throw new ArgumentException(
                    "All tactical worker positions must use one fine topology.");
        }

        var sorted = workers.OrderBy(x => x.Id.Value).ToArray();
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index - 1].Id == sorted[index].Id)
                throw new ArgumentException("Physical worker IDs must be unique.");
        }

        IsInitialized = initialized;
        HasAuthoritativeTacticalLocations = tacticalLocationsAuthoritative;
        HasAuthoritativeOccupations = occupationsAuthoritative;
        Workers = Array.AsReadOnly(sorted);
    }
}
