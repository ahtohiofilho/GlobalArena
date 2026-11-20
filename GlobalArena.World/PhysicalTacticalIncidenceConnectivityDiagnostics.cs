namespace GlobalArena.World;

/// <summary>
/// R107 experimental structural audit; does not certify physical traversal,
/// unique territory boundaries, market settlement or non-sixfold scales.
/// </summary>
public sealed record PhysicalTacticalIncidenceConnectivityDiagnostics(
    GoldbergParameters CoarseParameters,
    GoldbergParameters FineParameters,
    int CoarseRegions,
    int CoarseEdges,
    int EmptyOwnerRegions,
    int DisconnectedOwnerRegions,
    int CoarseEdgesWithoutFineContact,
    int MinimumFineContactsPerCoarseEdge,
    int MaximumFineContactsPerCoarseEdge)
{
    public bool RegionConnectivityVerified =>
        EmptyOwnerRegions == 0 && DisconnectedOwnerRegions == 0;

    public bool CoarseAdjacencyRepresented =>
        CoarseEdgesWithoutFineContact == 0 &&
        MinimumFineContactsPerCoarseEdge > 0;
}
