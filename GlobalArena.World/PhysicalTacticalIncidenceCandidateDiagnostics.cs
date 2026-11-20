namespace GlobalArena.World;

/// <summary>
/// Research-only evidence about a candidate Goldberg scaled lineage.
/// This does not authorize use in production incidence, movement or markets.
/// Counts are sampled from the SAME deterministic nearest-owner partition
/// used by PhysicalTacticalIncidenceMapper.Materialize.
/// </summary>
public sealed record PhysicalTacticalIncidenceCandidateDiagnostics(
    GoldbergParameters CoarseParameters,
    GoldbergParameters FineParameters,
    int CoarseCells,
    int FineCells,
    int InteriorTiles,
    int EdgeSharedTiles,
    int VertexSharedTiles,
    int UnsupportedOwnerCountTiles,
    int MatchedCoarseEdgeSignatures,
    int MatchedCoarseVertexSignatures,
    int ExpectedCoarseEdges,
    int ExpectedCoarseVertices,
    int DistinctObservedEdgeSignatures,
    int DistinctObservedVertexSignatures)
{
    public int CoveredTiles => checked(
        InteriorTiles + EdgeSharedTiles + VertexSharedTiles +
        UnsupportedOwnerCountTiles);

    /// <summary>
    /// Structural evidence only: matching signatures does not establish
    /// unique, connected, non-overlapping border path materialization.
    /// </summary>
    public bool EveryCoarseEdgeSignatureRepresented =>
        MatchedCoarseEdgeSignatures == ExpectedCoarseEdges &&
        DistinctObservedEdgeSignatures == ExpectedCoarseEdges;

    public bool EveryCoarseVertexSignatureRepresented =>
        MatchedCoarseVertexSignatures == ExpectedCoarseVertices &&
        DistinctObservedVertexSignatures == ExpectedCoarseVertices;
}
