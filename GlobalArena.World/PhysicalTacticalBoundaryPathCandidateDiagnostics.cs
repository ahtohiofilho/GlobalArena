namespace GlobalArena.World;

/// <summary>
/// R108 exploratory evidence for candidate strategic border corridors.
/// A completed corridor is a chain of REAL fine-grid adjacent cell centers;
/// it is not a certified polygon boundary or a gameplay movement rule.
/// </summary>
public sealed record PhysicalTacticalBoundaryPathCandidateDiagnostics(
    GoldbergParameters CoarseParameters,
    GoldbergParameters FineParameters,
    int StrategicEdgeCount,
    int CompleteEdgeCorridors,
    int MissingAnchorEdges,
    int AmbiguousAnchorEdges,
    int DisconnectedEdgeCorridors,
    int RepeatedTileCorridors,
    int ReusedFineSegmentsAcrossEdges,
    int AlternativeShortestHalfPaths,
    int MinimumCompletedPathCells,
    int MaximumCompletedPathCells)
{
    public bool EveryStrategicEdgeHasAUniqueAuditedCorridor =>
        CompleteEdgeCorridors == StrategicEdgeCount &&
        MissingAnchorEdges == 0 &&
        AmbiguousAnchorEdges == 0 &&
        DisconnectedEdgeCorridors == 0 &&
        RepeatedTileCorridors == 0 &&
        ReusedFineSegmentsAcrossEdges == 0;

    // Alternative shortest paths do not automatically invalidate the topology,
    // but a production boundary renderer must decide them deterministically.
    public bool HasShortestPathAmbiguity => AlternativeShortestHalfPaths != 0;
}
