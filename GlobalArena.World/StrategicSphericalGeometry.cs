namespace GlobalArena.World;

public sealed class StrategicSphericalGeometry
{
    public IReadOnlyList<StrategicCellGeometry> Cells { get; }

    public IReadOnlyList<StrategicVertexGeometry> Vertices { get; }

    internal StrategicSphericalGeometry(
        IEnumerable<StrategicCellGeometry> cells,
        IEnumerable<StrategicVertexGeometry> vertices)
    {
        ArgumentNullException.ThrowIfNull(
            cells);

        ArgumentNullException.ThrowIfNull(
            vertices);

        var cellArray =
            cells.ToArray();

        var vertexArray =
            vertices.ToArray();

        for (var index = 0;
             index < cellArray.Length;
             index++)
        {
            if (cellArray[index].CellId.Value
                != (ulong)index + 1UL)
            {
                throw new ArgumentException(
                    "Strategic cell geometry IDs must be contiguous canonical one-based ordinals.",
                    nameof(cells));
            }
        }

        for (var index = 0;
             index < vertexArray.Length;
             index++)
        {
            if (vertexArray[index].VertexId.Value
                != (ulong)index + 1UL)
            {
                throw new ArgumentException(
                    "Strategic vertex geometry IDs must be contiguous canonical one-based ordinals.",
                    nameof(vertices));
            }
        }

        Cells =
            Array.AsReadOnly(
                cellArray);

        Vertices =
            Array.AsReadOnly(
                vertexArray);
    }

    public StrategicCellGeometry GetCell(
        StrategicCellId cellId)
    {
        if (!cellId.IsValid
            || cellId.Value > (ulong)Cells.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellId));
        }

        return Cells[
            checked((int)cellId.Value - 1)];
    }

    public StrategicVertexGeometry GetVertex(
        StrategicVertexId vertexId)
    {
        if (!vertexId.IsValid
            || vertexId.Value > (ulong)Vertices.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vertexId));
        }

        return Vertices[
            checked((int)vertexId.Value - 1)];
    }
}
