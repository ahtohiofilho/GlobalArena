namespace GlobalArena.World;

public sealed class StrategicHydrologyFieldSet
{
    private readonly int[] _downstreamNodeIndexes;
    private readonly StrategicHydrologyNodeKind[] _nodeKinds;

    public StrategicScalarField Elevation { get; }
    public StrategicLandWaterMap LandWater { get; }
    public StrategicScalarField WaterAvailability { get; }
    public StrategicScalarField FlowAccumulation { get; }

    public StrategicSurfaceGraph SurfaceGraph =>
        Elevation.SurfaceGraph;

    public int Count =>
        _nodeKinds.Length;

    internal StrategicHydrologyFieldSet(
        StrategicScalarField elevation,
        StrategicLandWaterMap landWater,
        StrategicScalarField waterAvailability,
        IEnumerable<int> downstreamNodeIndexes,
        IEnumerable<StrategicHydrologyNodeKind> nodeKinds,
        StrategicScalarField flowAccumulation)
    {
        ArgumentNullException.ThrowIfNull(elevation);
        ArgumentNullException.ThrowIfNull(landWater);
        ArgumentNullException.ThrowIfNull(waterAvailability);
        ArgumentNullException.ThrowIfNull(downstreamNodeIndexes);
        ArgumentNullException.ThrowIfNull(nodeKinds);
        ArgumentNullException.ThrowIfNull(flowAccumulation);

        var downstream =
            downstreamNodeIndexes.ToArray();

        var kinds =
            nodeKinds.ToArray();

        if (downstream.Length != elevation.Count
            || kinds.Length != elevation.Count
            || flowAccumulation.Count != elevation.Count)
        {
            throw new ArgumentException(
                "Hydrology fields must contain exactly one value per strategic node.");
        }

        if (!ReferenceEquals(
            elevation.SurfaceGraph,
            waterAvailability.SurfaceGraph)
            || !ReferenceEquals(
                elevation.SurfaceGraph,
                flowAccumulation.SurfaceGraph)
            || !ReferenceEquals(
                elevation,
                landWater.Elevation))
        {
            throw new ArgumentException(
                "Hydrology fields must share one canonical strategic surface graph and elevation field.");
        }

        Elevation = elevation;
        LandWater = landWater;
        WaterAvailability = waterAvailability;
        FlowAccumulation = flowAccumulation;
        _downstreamNodeIndexes = downstream;
        _nodeKinds = kinds;
    }

    public StrategicHydrologyNodeKind GetNodeKind(
        int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);

        return _nodeKinds[
            nodeIndex];
    }

    public StrategicHydrologyNodeKind GetNodeKind(
        StrategicCellId cellId)
    {
        return GetNodeKind(
            SurfaceGraph.GetNodeIndex(
                cellId));
    }

    public int? GetDownstreamNodeIndex(
        int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);

        var downstream =
            _downstreamNodeIndexes[
                nodeIndex];

        return downstream < 0
            ? null
            : downstream;
    }

    public StrategicCellId? GetDownstreamCellId(
        int nodeIndex)
    {
        var downstream =
            GetDownstreamNodeIndex(
                nodeIndex);

        return downstream.HasValue
            ? SurfaceGraph.GetCellId(
                downstream.Value)
            : null;
    }

    public StrategicCellId? GetDownstreamCellId(
        StrategicCellId cellId)
    {
        return GetDownstreamCellId(
            SurfaceGraph.GetNodeIndex(
                cellId));
    }

    private void ValidateNodeIndex(
        int nodeIndex)
    {
        if (nodeIndex < 0
            || nodeIndex >= _nodeKinds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeIndex),
                "Hydrology node index is outside the canonical strategic range.");
        }
    }
}
