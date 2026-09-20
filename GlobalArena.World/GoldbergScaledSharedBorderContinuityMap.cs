namespace GlobalArena.World;

public sealed class GoldbergScaledSharedBorderContinuityMap
{
    public GoldbergScaledRefinementReferenceMap CellReferenceMap { get; }

    public IReadOnlyList<GoldbergScaledSharedBorderReference> BorderReferences { get; }

    internal GoldbergScaledSharedBorderContinuityMap(
        GoldbergScaledRefinementReferenceMap cellReferenceMap,
        IEnumerable<GoldbergScaledSharedBorderReference> borderReferences)
    {
        ArgumentNullException.ThrowIfNull(
            cellReferenceMap);

        ArgumentNullException.ThrowIfNull(
            borderReferences);

        var references =
            borderReferences.ToArray();

        if (references.Length != 30)
        {
            throw new ArgumentException(
                "The reference-pair continuity map must contain exactly 30 border references.",
                nameof(borderReferences));
        }

        if (references.Any(reference => reference is null))
        {
            throw new ArgumentException(
                "Border references cannot contain null values.",
                nameof(borderReferences));
        }

        if (
            references
                .Select(reference => reference.CoarseStrategicEdgeId)
                .Distinct()
                .Count()
            != references.Length)
        {
            throw new ArgumentException(
                "Coarse strategic edge IDs must be unique.",
                nameof(borderReferences));
        }

        if (
            references
                .Select(reference => reference.MiddleFineCellId)
                .Distinct()
                .Count()
            != references.Length)
        {
            throw new ArgumentException(
                "Middle fine strategic cell IDs must be unique.",
                nameof(borderReferences));
        }

        var fineEdgeIds =
            references
                .SelectMany(reference => reference.FineStrategicEdgeIds)
                .ToArray();

        if (fineEdgeIds.Length != 60)
        {
            throw new ArgumentException(
                "The reference-pair continuity map must contain exactly 60 fine strategic edge references.",
                nameof(borderReferences));
        }

        if (fineEdgeIds.Distinct().Count() != fineEdgeIds.Length)
        {
            throw new ArgumentException(
                "Fine strategic edge IDs must be globally unique across border references.",
                nameof(borderReferences));
        }

        var ordered =
            references
                .OrderBy(reference => reference.CoarseStrategicEdgeId.Value)
                .ToArray();

        CellReferenceMap =
            cellReferenceMap;

        BorderReferences =
            Array.AsReadOnly(
                ordered);
    }
}
