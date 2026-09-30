namespace GlobalArena.World;

public sealed class StrategicCivilizationStartCandidateSet
{
    private readonly StrategicCellId[] _candidateCellIds;

    private readonly IReadOnlyList<StrategicCellId> _readOnlyCandidateCellIds;

    public StrategicCellId InitialReferenceCellId { get; }

    public IReadOnlyList<StrategicCellId> CandidateCellIds =>
        _readOnlyCandidateCellIds;

    public int Count =>
        _candidateCellIds.Length;

    internal StrategicCivilizationStartCandidateSet(
        StrategicCellId initialReferenceCellId,
        IEnumerable<StrategicCellId> candidateCellIds)
    {
        if (!initialReferenceCellId.IsValid)
        {
            throw new ArgumentException(
                "Initial reference cell ID must be valid.",
                nameof(initialReferenceCellId));
        }

        ArgumentNullException.ThrowIfNull(
            candidateCellIds);

        var candidates =
            candidateCellIds.ToArray();

        if (candidates.Length == 0)
        {
            throw new ArgumentException(
                "At least one civilization start candidate is required.",
                nameof(candidateCellIds));
        }

        if (candidates.Any(
            cellId =>
                !cellId.IsValid))
        {
            throw new ArgumentException(
                "Civilization start candidate IDs must be valid.",
                nameof(candidateCellIds));
        }

        if (candidates.Distinct().Count()
            != candidates.Length)
        {
            throw new ArgumentException(
                "Civilization start candidate IDs must be unique.",
                nameof(candidateCellIds));
        }

        InitialReferenceCellId =
            initialReferenceCellId;

        _candidateCellIds =
            candidates;

        _readOnlyCandidateCellIds =
            Array.AsReadOnly(
                _candidateCellIds);
    }
}
