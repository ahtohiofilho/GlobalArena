using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class EconomicPointRuntimeState
{
    private readonly EconomicActivityWorkforceAllocation[] _activityAllocations;

    private readonly IReadOnlyList<EconomicActivityWorkforceAllocation> _readOnlyActivityAllocations;

    public EconomicPointId Id { get; }

    public CivilizationId Owner { get; }

    public StrategicCellId StrategicCellId { get; }

    public ulong Workforce { get; }

    public ulong AllocatedWorkforce { get; }

    public ulong UnallocatedWorkforce =>
        Workforce - AllocatedWorkforce;

    public IReadOnlyList<EconomicActivityWorkforceAllocation> ActivityAllocations =>
        _readOnlyActivityAllocations;

    public EconomicPointRuntimeState(
        EconomicPointId id,
        CivilizationId owner,
        StrategicCellId strategicCellId,
        ulong workforce)
        : this(
            id,
            owner,
            strategicCellId,
            workforce,
            Array.Empty<EconomicActivityWorkforceAllocation>())
    {
    }

    public EconomicPointRuntimeState(
        EconomicPointId id,
        CivilizationId owner,
        StrategicCellId strategicCellId,
        ulong workforce,
        IEnumerable<EconomicActivityWorkforceAllocation> activityAllocations)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Economic point ID must be valid.",
                nameof(id));
        }

        if (!owner.IsValid)
        {
            throw new ArgumentException(
                "Economic point owner must be a valid civilization identity.",
                nameof(owner));
        }

        if (!strategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Economic point strategic routing anchor must be valid.",
                nameof(strategicCellId));
        }

        ArgumentNullException.ThrowIfNull(
            activityAllocations);

        var canonical =
            activityAllocations.ToArray();

        if (canonical.Any(
            allocation =>
                allocation is null))
        {
            throw new ArgumentException(
                "Economic point activity allocations cannot contain null.",
                nameof(activityAllocations));
        }

        canonical =
            canonical
                .OrderBy(
                    allocation =>
                        (byte)allocation.Kind)
                .ToArray();

        ulong allocatedWorkforce = 0UL;

        for (var index = 0;
             index < canonical.Length;
             index++)
        {
            var current =
                canonical[index];

            if (index > 0
                && canonical[index - 1].Kind
                    == current.Kind)
            {
                throw new ArgumentException(
                    "Economic point cannot contain duplicate activity kinds.",
                    nameof(activityAllocations));
            }

            allocatedWorkforce =
                checked(
                    allocatedWorkforce
                    + current.Workforce);
        }

        if (allocatedWorkforce > workforce)
        {
            throw new ArgumentException(
                "Economic point activity workforce cannot exceed total workforce.",
                nameof(activityAllocations));
        }

        Id = id;
        Owner = owner;
        StrategicCellId = strategicCellId;
        Workforce = workforce;
        AllocatedWorkforce = allocatedWorkforce;

        _activityAllocations =
            canonical;

        _readOnlyActivityAllocations =
            Array.AsReadOnly(
                _activityAllocations);
    }
}