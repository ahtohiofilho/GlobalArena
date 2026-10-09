namespace GlobalArena.World;

public sealed class StrategicVertexGeometry
{
    public StrategicVertexId VertexId { get; }

    public SphericalPoint3 Position { get; }

    internal StrategicVertexGeometry(
        StrategicVertexId vertexId,
        SphericalPoint3 position)
    {
        if (!vertexId.IsValid)
        {
            throw new ArgumentException(
                "Strategic vertex geometry requires a valid vertex ID.",
                nameof(vertexId));
        }

        if (!position.IsValid)
        {
            throw new ArgumentException(
                "Strategic vertex geometry position must be a valid normalized spherical point.",
                nameof(position));
        }

        VertexId =
            vertexId;

        Position =
            position;
    }
}
