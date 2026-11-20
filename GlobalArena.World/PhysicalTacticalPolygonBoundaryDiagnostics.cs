namespace GlobalArena.World;

/// <summary>
/// R109 research-only evidence from ACTUAL fine Goldberg polygon edges,
/// not interpolated lines between tile centers. A single chosen owner per
/// tile is used solely for inspecting an unambiguous overlay partition.
/// It does not override the authoritative multi-owner incidence, geography,
/// movement, sovereignty or economy.
/// </summary>
public sealed record PhysicalTacticalPolygonBoundaryDiagnostics(
    GoldbergParameters CoarseParameters,
    GoldbergParameters FineParameters,
    int FinePolygonEdges,
    int SharedOwnerTiles,
    int PolygonBoundaryEdges,
    int ExpectedStrategicPairs,
    int RepresentedStrategicPairs,
    int UnexpectedNonNeighborPairs,
    int BranchingStrategicPairPaths,
    int DisconnectedStrategicPairPaths,
    int ClosedStrategicPairCycles,
    int SegmentsWithInvalidVertexIdentity)
{
    public int MissingStrategicPairs =>
        ExpectedStrategicPairs - RepresentedStrategicPairs;

    public bool StructuralOverlayCandidate =>
        MissingStrategicPairs == 0 &&
        UnexpectedNonNeighborPairs == 0 &&
        BranchingStrategicPairPaths == 0 &&
        DisconnectedStrategicPairPaths == 0 &&
        ClosedStrategicPairCycles == 0 &&
        SegmentsWithInvalidVertexIdentity == 0;
}
