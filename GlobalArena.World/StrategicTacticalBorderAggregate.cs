namespace GlobalArena.World;

public sealed class StrategicTacticalBorderAggregate
{
    public StrategicTopology StrategicTopology { get; }

    public IReadOnlyList<TacticalRegion> TacticalRegions { get; }

    public IReadOnlyList<SharedBorderBand> SharedBorderBands { get; }

    public IReadOnlyList<SharedBorderIncidence> SharedBorderIncidences { get; }

    public StrategicTacticalBorderAggregate(
        StrategicTopology strategicTopology,
        IEnumerable<TacticalRegion> tacticalRegions,
        IEnumerable<SharedBorderBand> sharedBorderBands)
    {
        ArgumentNullException.ThrowIfNull(
            strategicTopology);

        ArgumentNullException.ThrowIfNull(
            tacticalRegions);

        ArgumentNullException.ThrowIfNull(
            sharedBorderBands);

        var regionArray =
            tacticalRegions.ToArray();

        if (regionArray.Any(
            region =>
                region is null))
        {
            throw new ArgumentException(
                "Tactical regions cannot contain null values.",
                nameof(tacticalRegions));
        }

        var validStrategicCellIds =
            strategicTopology.Cells
                .Select(
                    cell =>
                        cell.Id)
                .ToHashSet();

        if (regionArray.Any(
            region =>
                !validStrategicCellIds.Contains(
                    region.StrategicCellId)))
        {
            throw new ArgumentException(
                "Every tactical region parent must exist in the strategic topology.",
                nameof(tacticalRegions));
        }

        if (regionArray
            .Select(
                region =>
                    region.StrategicCellId)
            .Distinct()
            .Count()
            != regionArray.Length)
        {
            throw new ArgumentException(
                "Tactical region parent IDs cannot contain duplicates.",
                nameof(tacticalRegions));
        }

        if (regionArray.Length
            != strategicTopology.Cells.Count)
        {
            throw new ArgumentException(
                "Tactical regions must cover every strategic cell exactly once.",
                nameof(tacticalRegions));
        }

        var regionsByStrategicCellId =
            regionArray.ToDictionary(
                region =>
                    region.StrategicCellId);

        var orderedRegions =
            strategicTopology.Cells
                .Select(
                    cell =>
                    {
                        if (!regionsByStrategicCellId.TryGetValue(
                            cell.Id,
                            out var region))
                        {
                            throw new ArgumentException(
                                "Tactical regions must cover every strategic cell exactly once.",
                                nameof(tacticalRegions));
                        }

                        return region;
                    })
                .ToArray();

        var bandArray =
            sharedBorderBands.ToArray();

        if (bandArray.Any(
            band =>
                band is null))
        {
            throw new ArgumentException(
                "Shared border bands cannot contain null values.",
                nameof(sharedBorderBands));
        }

        var validStrategicEdgeIds =
            strategicTopology.Edges
                .Select(
                    edge =>
                        edge.Id)
                .ToHashSet();

        if (bandArray.Any(
            band =>
                !validStrategicEdgeIds.Contains(
                    band.StrategicEdgeId)))
        {
            throw new ArgumentException(
                "Every shared border band edge must exist in the strategic topology.",
                nameof(sharedBorderBands));
        }

        if (bandArray
            .Select(
                band =>
                    band.StrategicEdgeId)
            .Distinct()
            .Count()
            != bandArray.Length)
        {
            throw new ArgumentException(
                "Shared border band edge IDs cannot contain duplicates.",
                nameof(sharedBorderBands));
        }

        if (bandArray.Length
            != strategicTopology.Edges.Count)
        {
            throw new ArgumentException(
                "Shared border bands must cover every strategic edge exactly once.",
                nameof(sharedBorderBands));
        }

        var bandsByStrategicEdgeId =
            bandArray.ToDictionary(
                band =>
                    band.StrategicEdgeId);

        var orderedBands =
            strategicTopology.Edges
                .Select(
                    edge =>
                    {
                        if (!bandsByStrategicEdgeId.TryGetValue(
                            edge.Id,
                            out var band))
                        {
                            throw new ArgumentException(
                                "Shared border bands must cover every strategic edge exactly once.",
                                nameof(sharedBorderBands));
                        }

                        return band;
                    })
                .ToArray();

        var incidences =
            new SharedBorderIncidence[
                strategicTopology.Edges.Count];

        for (var index = 0; index < strategicTopology.Edges.Count; index++)
        {
            var edge =
                strategicTopology.Edges[index];

            var firstIncidentRegion =
                regionsByStrategicCellId[
                    edge.IncidentCellIds[0]];

            var secondIncidentRegion =
                regionsByStrategicCellId[
                    edge.IncidentCellIds[1]];

            incidences[index] =
                new SharedBorderIncidence(
                    edge,
                    orderedBands[index],
                    firstIncidentRegion,
                    secondIncidentRegion);
        }

        StrategicTopology =
            strategicTopology;

        TacticalRegions =
            Array.AsReadOnly(
                orderedRegions);

        SharedBorderBands =
            Array.AsReadOnly(
                orderedBands);

        SharedBorderIncidences =
            Array.AsReadOnly(
                incidences);
    }
}
