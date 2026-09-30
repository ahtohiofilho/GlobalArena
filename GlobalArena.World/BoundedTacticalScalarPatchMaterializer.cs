namespace GlobalArena.World;

public static class BoundedTacticalScalarPatchMaterializer
{
    public static BoundedTacticalScalarPatch Materialize(
        StrategicScalarField strategicField,
        StrategicCellId targetStrategicCellId,
        GoldbergScaledRefinement refinement)
    {
        ArgumentNullException.ThrowIfNull(
            strategicField);

        ArgumentNullException.ThrowIfNull(
            refinement);

        if (!targetStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Target strategic cell ID must be valid.",
                nameof(targetStrategicCellId));
        }

        var surfaceGraph =
            strategicField.SurfaceGraph;

        _ =
            surfaceGraph.GetNodeIndex(
                targetStrategicCellId);

        if (surfaceGraph
            .StrategicTopology
            .Parameters
            != refinement.CoarseParameters)
        {
            throw new ArgumentException(
                "Strategic field topology must match the refinement coarse Goldberg parameters.",
                nameof(refinement));
        }

        var incidenceMap =
            PhysicalTacticalIncidenceMapper.Materialize(
                refinement);

        var samples =
            incidenceMap
                .TileIncidences
                .Where(
                    incidence =>
                        incidence
                            .IncidentCoarseCellIds
                            .Contains(
                                targetStrategicCellId))
                .OrderBy(
                    incidence =>
                        incidence
                            .PhysicalTacticalTileId
                            .FineStrategicCellId
                            .Value)
                .Select(
                    incidence =>
                        CreateSample(
                            strategicField,
                            incidence))
                .ToArray();

        return new BoundedTacticalScalarPatch(
            targetStrategicCellId,
            refinement,
            samples);
    }

    private static BoundedTacticalScalarSample CreateSample(
        StrategicScalarField strategicField,
        PhysicalTacticalTileIncidence incidence)
    {
        Int128 total =
            0;

        foreach (var strategicCellId
            in incidence.IncidentCoarseCellIds)
        {
            total +=
                strategicField.GetRawValue(
                    strategicCellId);
        }

        var average =
            checked(
                (long)(
                    total
                    / incidence
                        .IncidentCoarseCellIds
                        .Count));

        return new BoundedTacticalScalarSample(
            incidence.PhysicalTacticalTileId,
            incidence.IncidentCoarseCellIds,
            average);
    }
}
