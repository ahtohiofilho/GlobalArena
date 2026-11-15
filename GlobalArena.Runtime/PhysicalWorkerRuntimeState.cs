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

    public PhysicalWorkerRuntimeRecord(
        PhysicalWorkerId id, CivilizationId owner, StrategicCellId cellId,
        CommodityId commodity, ulong outputMicroPerTurn,
        PhysicalWorkerActivity activity, uint demandMultiplierMicro = 1_000_000U)
    {
        if (!id.IsValid || !owner.IsValid || !cellId.IsValid ||
            !commodity.IsValid || !Enum.IsDefined(activity) ||
            demandMultiplierMicro == 0U)
            throw new ArgumentException("Invalid physical worker identity, location or policy.");

        Id = id;
        Owner = owner;
        CellId = cellId;
        Commodity = commodity;
        OutputMicroPerTurn = outputMicroPerTurn;
        Activity = activity;
        DemandMultiplierMicro = demandMultiplierMicro;
    }

    public PhysicalWorkerRuntimeRecord WithLocationAndActivity(
        StrategicCellId cell, PhysicalWorkerActivity activity) =>
        new(Id, Owner, cell, Commodity, OutputMicroPerTurn,
            activity, DemandMultiplierMicro);
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
    public IReadOnlyList<PhysicalWorkerRuntimeRecord> Workers { get; }

    public PhysicalWorkerRuntimeState(
        IEnumerable<PhysicalWorkerRuntimeRecord> workers)
        : this(true, (workers ?? throw new ArgumentNullException(nameof(workers))).ToArray())
    {
    }

    private PhysicalWorkerRuntimeState(
        bool initialized, PhysicalWorkerRuntimeRecord[] workers)
    {
        if (!initialized && workers.Length != 0)
            throw new InvalidOperationException("Missing worker census must be empty.");
        if (workers.Any(x => x is null))
            throw new ArgumentException("Worker census cannot contain null records.");

        var sorted = workers.OrderBy(x => x.Id.Value).ToArray();
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index - 1].Id == sorted[index].Id)
                throw new ArgumentException("Physical worker IDs must be unique.");
        }

        IsInitialized = initialized;
        Workers = Array.AsReadOnly(sorted);
    }
}
