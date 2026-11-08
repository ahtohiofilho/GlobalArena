namespace GlobalArena.Foundation.Agriculture;

/// <summary>
/// An immutable snapshot of external logical nodes and their adjacency.
/// This is NOT a world generator and does not own a second topology.
/// All edges represent one logical hop; no spherical distance is stored.
/// </summary>
public readonly record struct AgriculturalGeographicNode(
    long StableNodeId,
    int ElevationRaw,
    int TemperatureRaw,
    int MoistureRaw,
    bool IsLand);

public sealed class AgriculturalLogicalGeography
{
    private readonly AgriculturalGeographicNode[] _nodes;
    private readonly int[] _offsets;
    private readonly int[] _neighbors;

    public int NodeCount => _nodes.Length;
    public int AdjacencyCount => _neighbors.Length;

    /// <summary>
    /// Indexed by the caller's authoritative stable node ordering. Indices are
    /// local slots; StableNodeId remains the authoritative identity.
    /// </summary>
    public AgriculturalLogicalGeography(
        IReadOnlyList<AgriculturalGeographicNode> nodes,
        IReadOnlyList<int> neighborOffsets,
        IReadOnlyList<int> neighborIndexes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(neighborOffsets);
        ArgumentNullException.ThrowIfNull(neighborIndexes);

        _nodes = nodes.ToArray();
        _offsets = neighborOffsets.ToArray();
        _neighbors = neighborIndexes.ToArray();
        if (_nodes.Length == 0)
            throw new ArgumentException("Logical geography needs at least one node.", nameof(nodes));
        if (_offsets.Length != _nodes.Length + 1 ||
            _offsets[0] != 0 ||
            _offsets[^1] != _neighbors.Length)
            throw new ArgumentException("Invalid adjacency offset boundaries.", nameof(neighborOffsets));

        var ids = new HashSet<long>();
        for (int i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            if (node.StableNodeId < 0 || !ids.Add(node.StableNodeId))
                throw new ArgumentException("Node identifiers must be unique nonnegative stable IDs.", nameof(nodes));
            if (node.ElevationRaw < -AgriculturalSuitability.Scale ||
                node.ElevationRaw > AgriculturalSuitability.Scale ||
                node.TemperatureRaw < -AgriculturalSuitability.Scale ||
                node.TemperatureRaw > AgriculturalSuitability.Scale ||
                node.MoistureRaw < 0 || node.MoistureRaw > AgriculturalSuitability.Scale)
                throw new ArgumentOutOfRangeException(nameof(nodes), "Physical fields are outside the laboratory fixed-point envelope.");

            int from = _offsets[i];
            int to = _offsets[i + 1];
            if (from < 0 || to > _neighbors.Length || to <= from)
                throw new ArgumentException("Every node must have a nonempty adjacency range.", nameof(neighborOffsets));

            int previous = -1;
            for (int j = from; j < to; j++)
            {
                int neighbor = _neighbors[j];
                if (neighbor <= previous || neighbor == i || neighbor >= _nodes.Length)
                    throw new ArgumentException("Neighbors must be strictly increasing, in range and non-self.", nameof(neighborIndexes));
                previous = neighbor;
            }
        }

        // The geography bridge requires an undirected logical graph, preventing
        // an accidental one-sided slope calculation at an edge.
        for (int i = 0; i < _nodes.Length; i++)
            foreach (int neighbor in GetNeighbors(i))
                if (!GetNeighbors(neighbor).Contains(i))
                    throw new ArgumentException("Every logical edge must be reciprocal.", nameof(neighborIndexes));
    }

    public AgriculturalGeographicNode GetNode(int nodeIndex) =>
        _nodes[ValidateIndex(nodeIndex)];

    public ReadOnlySpan<int> GetNeighbors(int nodeIndex)
    {
        int i = ValidateIndex(nodeIndex);
        return _neighbors.AsSpan(_offsets[i], _offsets[i + 1] - _offsets[i]);
    }

    private int ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_nodes.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return index;
    }
}
