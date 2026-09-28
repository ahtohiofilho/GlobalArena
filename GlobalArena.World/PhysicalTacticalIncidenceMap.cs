namespace GlobalArena.World;

public sealed class PhysicalTacticalIncidenceMap
{
    public GoldbergScaledRefinement Refinement { get; }

    public StrategicTopology CoarseTopology { get; }

    public StrategicTopology FineTopology { get; }

    public IReadOnlyList<PhysicalTacticalTileIncidence> TileIncidences { get; }

    internal PhysicalTacticalIncidenceMap(
        GoldbergScaledRefinement refinement,
        StrategicTopology coarseTopology,
        StrategicTopology fineTopology,
        IEnumerable<PhysicalTacticalTileIncidence> tileIncidences)
    {
        ArgumentNullException.ThrowIfNull(refinement);
        ArgumentNullException.ThrowIfNull(coarseTopology);
        ArgumentNullException.ThrowIfNull(fineTopology);
        ArgumentNullException.ThrowIfNull(tileIncidences);

        if (coarseTopology.Parameters != refinement.CoarseParameters)
        {
            throw new ArgumentException(
                "Coarse topology parameters must match the refinement contract.",
                nameof(coarseTopology));
        }

        if (fineTopology.Parameters != refinement.FineParameters)
        {
            throw new ArgumentException(
                "Fine topology parameters must match the refinement contract.",
                nameof(fineTopology));
        }

        var incidences =
            tileIncidences.ToArray();

        if (incidences.Length != fineTopology.Cells.Count)
        {
            throw new ArgumentException(
                "Physical incidence map must contain exactly one incidence for every fine topology cell.",
                nameof(tileIncidences));
        }

        if (incidences.Any(incidence => incidence is null))
        {
            throw new ArgumentException(
                "Physical incidence map cannot contain null entries.",
                nameof(tileIncidences));
        }

        var ordered =
            incidences
                .OrderBy(
                    incidence =>
                        incidence
                            .PhysicalTacticalTileId
                            .FineStrategicCellId
                            .Value)
                .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            var tileId =
                ordered[index]
                    .PhysicalTacticalTileId;

            if (tileId.FineGoldbergParameters != refinement.FineParameters)
            {
                throw new ArgumentException(
                    "Every physical tile identity must use the refinement fine Goldberg parameters.",
                    nameof(tileIncidences));
            }

            var expectedFineCellId =
                (ulong)index + 1UL;

            if (tileId.FineStrategicCellId.Value != expectedFineCellId)
            {
                throw new ArgumentException(
                    "Physical incidence map must cover every fine topology cell exactly once in canonical identity order.",
                    nameof(tileIncidences));
            }
        }

        Refinement =
            refinement;

        CoarseTopology =
            coarseTopology;

        FineTopology =
            fineTopology;

        TileIncidences =
            Array.AsReadOnly(
                ordered);
    }
}