using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Explicit authorized geographic subset for planning. An input
/// boundary, not the future discovery ledger or current-visibility model.
/// Edges are undirected and must have two known endpoints.</summary>
public sealed class FoundationCivilizationKnowledgeScope
{
    private readonly HashSet<PhysicalTacticalTileId> _nodes;
    private readonly HashSet<(PhysicalTacticalTileId, PhysicalTacticalTileId)> _links;

    public FoundationCivilizationKnowledgeScope(
        IEnumerable<PhysicalTacticalTileId> nodes,
        IEnumerable<(PhysicalTacticalTileId A, PhysicalTacticalTileId B)> links)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(links);
        _nodes = nodes.ToHashSet();
        if (_nodes.Any(n => !n.IsValid))
            throw new ArgumentException("Invalid known node.", nameof(nodes));
        _links = new();
        foreach (var (a, b) in links)
        {
            if (!_nodes.Contains(a) || !_nodes.Contains(b) || a == b ||
                a.FineGoldbergParameters != b.FineGoldbergParameters)
                throw new ArgumentException("Known edge needs two distinct known endpoints.", nameof(links));
            _links.Add((a, b));
            _links.Add((b, a));
        }
    }

    public bool KnowsNode(PhysicalTacticalTileId node) => _nodes.Contains(node);
    public bool KnowsLink(PhysicalTacticalTileId a, PhysicalTacticalTileId b) => _links.Contains((a, b));
}
