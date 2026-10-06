using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class StrategicEdgeAccessSnapshot
{
    private static readonly StrategicEdgeAccessSnapshot OpenInstance =
        new(
            Array.Empty<StrategicEdgeId>());

    private readonly StrategicEdgeId[] _closedEdgeIds;

    private readonly IReadOnlyList<StrategicEdgeId> _readOnlyClosedEdgeIds;

    private readonly HashSet<StrategicEdgeId> _closedEdges;

    public static StrategicEdgeAccessSnapshot AllOpen =>
        OpenInstance;

    public IReadOnlyList<StrategicEdgeId> ClosedEdgeIds =>
        _readOnlyClosedEdgeIds;

    public StrategicEdgeAccessSnapshot(
        IEnumerable<StrategicEdgeId> closedEdgeIds)
    {
        ArgumentNullException.ThrowIfNull(
            closedEdgeIds);

        var canonical =
            closedEdgeIds.ToArray();

        if (canonical.Any(
            edgeId =>
                !edgeId.IsValid))
        {
            throw new ArgumentException(
                "Strategic edge access snapshot cannot contain invalid edge identities.",
                nameof(closedEdgeIds));
        }

        canonical =
            canonical
                .OrderBy(
                    edgeId =>
                        edgeId.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            if (canonical[index - 1]
                == canonical[index])
            {
                throw new ArgumentException(
                    "Strategic edge access snapshot cannot contain duplicate closed edges.",
                    nameof(closedEdgeIds));
            }
        }

        _closedEdgeIds =
            canonical;

        _readOnlyClosedEdgeIds =
            Array.AsReadOnly(
                _closedEdgeIds);

        _closedEdges =
            new HashSet<StrategicEdgeId>(
                _closedEdgeIds);
    }

    public bool IsOpen(
        StrategicEdgeId edgeId)
    {
        if (!edgeId.IsValid)
        {
            throw new ArgumentException(
                "Strategic edge identity must be valid.",
                nameof(edgeId));
        }

        return !_closedEdges.Contains(
            edgeId);
    }
}
