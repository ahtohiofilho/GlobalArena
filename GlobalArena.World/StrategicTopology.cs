namespace GlobalArena.World;

public sealed class StrategicTopology
{
    public GoldbergParameters Parameters { get; }

    public IReadOnlyList<StrategicCell> Cells { get; }

    public IReadOnlyList<StrategicEdge> Edges { get; }

    public IReadOnlyList<StrategicVertex> Vertices { get; }

    internal StrategicTopology(
        GoldbergParameters parameters,
        IEnumerable<StrategicCell> cells,
        IEnumerable<StrategicEdge> edges,
        IEnumerable<StrategicVertex> vertices)
    {
        if (!parameters.IsValid)
        {
            throw new ArgumentException(
                "Goldberg parameters must be valid.",
                nameof(parameters));
        }

        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(vertices);

        var cellArray = cells.ToArray();
        var edgeArray = edges.ToArray();
        var vertexArray = vertices.ToArray();

        if ((ulong)cellArray.Length != parameters.StrategicCellCount
            || (ulong)edgeArray.Length != parameters.StrategicEdgeCount
            || (ulong)vertexArray.Length != parameters.StrategicVertexCount)
        {
            throw new ArgumentException(
                "Strategic topology entity counts must match Goldberg parameters.");
        }

        for (var index = 0; index < cellArray.Length; index++)
        {
            if (cellArray[index].Id.Value != (ulong)index + 1UL)
            {
                throw new ArgumentException(
                    "Strategic cell IDs must be contiguous canonical one-based ordinals.",
                    nameof(cells));
            }
        }

        for (var index = 0; index < edgeArray.Length; index++)
        {
            if (edgeArray[index].Id.Value != (ulong)index + 1UL)
            {
                throw new ArgumentException(
                    "Strategic edge IDs must be contiguous canonical one-based ordinals.",
                    nameof(edges));
            }
        }

        for (var index = 0; index < vertexArray.Length; index++)
        {
            if (vertexArray[index].Id.Value != (ulong)index + 1UL)
            {
                throw new ArgumentException(
                    "Strategic vertex IDs must be contiguous canonical one-based ordinals.",
                    nameof(vertices));
            }
        }

        Parameters = parameters;
        Cells = Array.AsReadOnly(cellArray);
        Edges = Array.AsReadOnly(edgeArray);
        Vertices = Array.AsReadOnly(vertexArray);
    }
}
