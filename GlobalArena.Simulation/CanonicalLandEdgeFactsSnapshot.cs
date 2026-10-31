using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Physical descriptors of an existing, directed, walkable land edge.
/// This record contains NO economic freight price or unit movement speed.
/// The VS37 quote is explicitly an optional experimental walking policy,
/// not a universal cost for trains, cargo or military formations.
/// </summary>
public sealed record CanonicalDirectedLandEdgeFacts(
    PhysicalTacticalTileId From,
    PhysicalTacticalTileId To,
    ulong UnitSphereChordMicroUnits,
    long SignedElevationDeltaRaw,
    long AverageMoistureRaw,
    bool TouchesForest,
    GeographicLandEdgeCostQuote ExperimentalWalkingQuote);

/// <summary>
/// Immutable, per-session geographic snapshot of directed LAND edges.
/// Built once for the existing 01C terrain. Read-only consumers (movement
/// forecasts, later aggregated freight policies) can reuse underlying
/// physical facts without regenerating terrain or rerunning trigonometry.
///
/// It is deliberately NOT persisted: geography is generated deterministically
/// from the canonical world seed, while infrastructure and freight capacity
/// have separate revision lifecycles. Do not share across session instances.
/// </summary>
public sealed class CanonicalLandEdgeFactsSnapshot : IScalarPhysicalEdgeCostProvider
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly Dictionary<
        (PhysicalTacticalTileId From, PhysicalTacticalTileId To),
        CanonicalDirectedLandEdgeFacts> _lookup;

    public IReadOnlyList<CanonicalDirectedLandEdgeFacts> Edges { get; }
    public int DirectedEdgeCount => Edges.Count;

    public CanonicalLandEdgeFactsSnapshot(CanonicalPhysicalTacticalSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        var terrain = session.CanonicalTerrain ??
            throw new InvalidOperationException(
                "An immutable land-edge facts snapshot requires canonical 01C geography.");
        var builder = new List<CanonicalDirectedLandEdgeFacts>();
        var byEdge = new Dictionary<
            (PhysicalTacticalTileId, PhysicalTacticalTileId),
            CanonicalDirectedLandEdgeFacts>();
        var provider = new CanonicalGeographicLandEdgeCostProvider();
        var fine = session.InitialTile.FineGoldbergParameters;
        var forest = ExperimentalProceduralBiomeClassifier.Biome.Forest;

        foreach (var cell in session.FineWorld.StrategicTopology.Cells)
        {
            if (!session.IsPhysicalWorkableLand(cell.Id))
                continue;

            var from = new PhysicalTacticalTileId(fine, cell.Id);
            var fromIndex = checked((int)cell.Id.Value - 1);
            foreach (var nextId in cell.AdjacentCellIds.OrderBy(id => id.Value))
            {
                if (!session.IsPhysicalWorkableLand(nextId))
                    continue;

                var to = new PhysicalTacticalTileId(fine, nextId);
                var toIndex = checked((int)nextId.Value - 1);
                var quote = provider.Explain(session, from, to);
                var facts = new CanonicalDirectedLandEdgeFacts(
                    from, to, quote.ChordMicroUnits,
                    checked(terrain.Elevation[toIndex] - terrain.Elevation[fromIndex]),
                    (terrain.Moisture[fromIndex] + terrain.Moisture[toIndex]) / 2L,
                    terrain.Biomes[fromIndex].UnderlyingBiome == forest ||
                    terrain.Biomes[toIndex].UnderlyingBiome == forest,
                    quote);

                if (!byEdge.TryAdd((from, to), facts))
                    throw new InvalidOperationException(
                        "Canonical directed land edges must be unique.");
                builder.Add(facts);
            }
        }

        // The topology is undirected but physical costs may be directional.
        // Check all reverse descriptors before publishing the immutable table.
        foreach (var item in builder)
        {
            if (!byEdge.TryGetValue((item.To, item.From), out var opposite) ||
                opposite.UnitSphereChordMicroUnits != item.UnitSphereChordMicroUnits ||
                opposite.SignedElevationDeltaRaw != -item.SignedElevationDeltaRaw ||
                opposite.AverageMoistureRaw != item.AverageMoistureRaw ||
                opposite.TouchesForest != item.TouchesForest)
                throw new InvalidOperationException(
                    "Physical land-edge facts must have reciprocal topology.");
        }

        _lookup = byEdge;
        Edges = Array.AsReadOnly(builder.ToArray());
    }

    public CanonicalDirectedLandEdgeFacts? Find(
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to) =>
        _lookup.TryGetValue((from, to), out var facts) ? facts : null;

    public ulong CostPoints(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "An immutable land-edge snapshot cannot be reused across sessions.");
        return Find(from, to)?.ExperimentalWalkingQuote.TotalPoints ??
            throw new InvalidOperationException(
                "The requested edge has no canonical walkable-land connection.");
    }
}
