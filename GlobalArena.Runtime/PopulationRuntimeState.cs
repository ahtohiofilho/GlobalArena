namespace GlobalArena.Runtime;

/// <summary>
/// Canonical resident population snapshot, keyed by economic point ID.
/// Uninitialized means no authoritative census has been supplied yet.
/// An initialized empty census is different from the absence of census data.
/// This checkpoint localizes all recorded residents to economic points.
/// </summary>
public sealed class PopulationRuntimeState
{
    public static PopulationRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<PopulationPointRuntimeState>());

    private readonly IReadOnlyList<PopulationPointRuntimeState> _points;

    public bool IsInitialized { get; }

    public IReadOnlyList<PopulationPointRuntimeState> PointPopulations => _points;

    public PopulationRuntimeState(
        IEnumerable<PopulationPointRuntimeState> points)
        : this(true, points)
    {
    }

    private PopulationRuntimeState(
        bool isInitialized,
        IEnumerable<PopulationPointRuntimeState> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var canonical = points.ToArray();
        if (canonical.Any(point => point is null))
            throw new ArgumentException(
                "Population state cannot contain null census records.",
                nameof(points));

        canonical = canonical
            .OrderBy(point => point.EconomicPointId.Value)
            .ToArray();

        for (var index = 1; index < canonical.Length; index++)
        {
            if (canonical[index - 1].EconomicPointId
                == canonical[index].EconomicPointId)
                throw new ArgumentException(
                    "Population state cannot contain duplicate economic point IDs.",
                    nameof(points));
        }

        IsInitialized = isInitialized;
        _points = Array.AsReadOnly(canonical);
    }
}
