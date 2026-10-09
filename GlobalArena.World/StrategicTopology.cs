namespace GlobalArena.World;

public sealed class StrategicTopology
{
    public GoldbergParameters Parameters { get; }

    public IReadOnlyList<StrategicCell> Cells { get; }

    public IReadOnlyList<StrategicEdge> Edges { get; }

    public IReadOnlyList<StrategicVertex> Vertices { get; }

    internal IReadOnlyList<StrategicCellConstructionProvenance>
        CellConstructionProvenance { get; }

    internal StrategicTopology(
        GoldbergParameters parameters,
        IEnumerable<StrategicCell> cells,
        IEnumerable<StrategicEdge> edges,
        IEnumerable<StrategicVertex> vertices,
        IEnumerable<StrategicCellConstructionProvenance>?
            cellConstructionProvenance = null)
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

        var constructionProvenanceArray =
            cellConstructionProvenance?.ToArray()
            ?? Array.Empty<StrategicCellConstructionProvenance>();

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

        if (constructionProvenanceArray.Length != 0
            && constructionProvenanceArray.Length != cellArray.Length)
        {
            throw new ArgumentException(
                "Strategic cell construction provenance must be empty or cover every strategic cell exactly once.",
                nameof(cellConstructionProvenance));
        }

        if (constructionProvenanceArray.Any(
            item =>
                !item.IsValid))
        {
            throw new ArgumentException(
                "Strategic cell construction provenance contains an invalid carrier.",
                nameof(cellConstructionProvenance));
        }

        Parameters = parameters;
        Cells = Array.AsReadOnly(cellArray);
        Edges = Array.AsReadOnly(edgeArray);
        Vertices = Array.AsReadOnly(vertexArray);
        CellConstructionProvenance =
            Array.AsReadOnly(
                constructionProvenanceArray);
    }
}
