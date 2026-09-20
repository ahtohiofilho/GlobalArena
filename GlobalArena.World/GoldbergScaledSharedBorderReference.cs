namespace GlobalArena.World;

public sealed class GoldbergScaledSharedBorderReference
{
    public StrategicEdgeId CoarseStrategicEdgeId { get; }

    public StrategicCellId MiddleFineCellId { get; }

    public IReadOnlyList<StrategicEdgeId> FineStrategicEdgeIds { get; }

    public GoldbergScaledSharedBorderReference(
        StrategicEdgeId coarseStrategicEdgeId,
        StrategicCellId middleFineCellId,
        IEnumerable<StrategicEdgeId> fineStrategicEdgeIds)
    {
        if (!coarseStrategicEdgeId.IsValid)
        {
            throw new ArgumentException(
                "Coarse strategic edge ID must be valid.",
                nameof(coarseStrategicEdgeId));
        }

        if (!middleFineCellId.IsValid)
        {
            throw new ArgumentException(
                "Middle fine strategic cell ID must be valid.",
                nameof(middleFineCellId));
        }

        ArgumentNullException.ThrowIfNull(
            fineStrategicEdgeIds);

        var fineEdgeIds =
            fineStrategicEdgeIds.ToArray();

        if (fineEdgeIds.Length != 2)
        {
            throw new ArgumentException(
                "A scaled shared-border reference must contain exactly two fine strategic edge IDs.",
                nameof(fineStrategicEdgeIds));
        }

        if (fineEdgeIds.Any(id => !id.IsValid))
        {
            throw new ArgumentException(
                "Fine strategic edge IDs must be valid.",
                nameof(fineStrategicEdgeIds));
        }

        if (fineEdgeIds[0] == fineEdgeIds[1])
        {
            throw new ArgumentException(
                "Fine strategic edge IDs must be distinct.",
                nameof(fineStrategicEdgeIds));
        }

        CoarseStrategicEdgeId =
            coarseStrategicEdgeId;

        MiddleFineCellId =
            middleFineCellId;

        FineStrategicEdgeIds =
            Array.AsReadOnly(
                fineEdgeIds);
    }
}
