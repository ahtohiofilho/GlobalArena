namespace GlobalArena.World;

public sealed class BoundedTacticalScalarPatch
{
    private readonly BoundedTacticalScalarSample[] _samples;

    private readonly IReadOnlyList<BoundedTacticalScalarSample> _readOnlySamples;

    public StrategicCellId TargetStrategicCellId { get; }

    public GoldbergScaledRefinement Refinement { get; }

    public int Count => _samples.Length;

    public IReadOnlyList<BoundedTacticalScalarSample> Samples =>
        _readOnlySamples;

    internal BoundedTacticalScalarPatch(
        StrategicCellId targetStrategicCellId,
        GoldbergScaledRefinement refinement,
        IEnumerable<BoundedTacticalScalarSample> samples)
    {
        if (!targetStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Target strategic cell ID must be valid.",
                nameof(targetStrategicCellId));
        }

        ArgumentNullException.ThrowIfNull(
            refinement);

        ArgumentNullException.ThrowIfNull(
            samples);

        var materialized =
            samples.ToArray();

        if (materialized.Length == 0)
        {
            throw new ArgumentException(
                "A bounded tactical patch must contain at least one physical sample.",
                nameof(samples));
        }

        var previousFineId =
            0UL;

        for (var index = 0;
             index < materialized.Length;
             index++)
        {
            var sample =
                materialized[index]
                ?? throw new ArgumentException(
                    "Bounded tactical patch samples cannot contain null entries.",
                    nameof(samples));

            if (!sample.IncidentStrategicCellIds.Contains(
                targetStrategicCellId))
            {
                throw new ArgumentException(
                    "Every bounded tactical sample must be incident to the target strategic cell.",
                    nameof(samples));
            }

            if (sample
                .PhysicalTacticalTileId
                .FineGoldbergParameters
                != refinement.FineParameters)
            {
                throw new ArgumentException(
                    "Every physical tactical sample must use the refinement fine Goldberg parameters.",
                    nameof(samples));
            }

            var currentFineId =
                sample
                    .PhysicalTacticalTileId
                    .FineStrategicCellId
                    .Value;

            if (currentFineId
                <= previousFineId)
            {
                throw new ArgumentException(
                    "Bounded tactical samples must be in strict canonical physical identity order.",
                    nameof(samples));
            }

            previousFineId =
                currentFineId;
        }

        TargetStrategicCellId =
            targetStrategicCellId;

        Refinement =
            refinement;

        _samples =
            materialized;

        _readOnlySamples =
            Array.AsReadOnly(
                _samples);
    }

    public BoundedTacticalScalarSample GetSample(
        int sampleIndex)
    {
        if (sampleIndex < 0
            || sampleIndex >= _samples.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleIndex),
                "Bounded tactical sample index is outside the patch range.");
        }

        return _samples[
            sampleIndex];
    }

    public BoundedTacticalScalarSample GetSample(
        PhysicalTacticalTileId physicalTacticalTileId)
    {
        if (!physicalTacticalTileId.IsValid)
        {
            throw new ArgumentException(
                "Physical tactical tile ID must be valid.",
                nameof(physicalTacticalTileId));
        }

        if (physicalTacticalTileId.FineGoldbergParameters
            != Refinement.FineParameters)
        {
            throw new KeyNotFoundException(
                "Physical tactical tile ID does not belong to this refinement.");
        }

        var ordinal =
            physicalTacticalTileId
                .FineStrategicCellId
                .Value;

        var lower =
            0;

        var upper =
            _samples.Length - 1;

        while (lower <= upper)
        {
            var middle =
                lower
                + ((upper - lower) / 2);

            var middleOrdinal =
                _samples[middle]
                    .PhysicalTacticalTileId
                    .FineStrategicCellId
                    .Value;

            if (middleOrdinal
                == ordinal)
            {
                return _samples[
                    middle];
            }

            if (middleOrdinal
                < ordinal)
            {
                lower =
                    middle + 1;
            }
            else
            {
                upper =
                    middle - 1;
            }
        }

        throw new KeyNotFoundException(
            "Physical tactical tile ID is outside the bounded patch.");
    }
}
