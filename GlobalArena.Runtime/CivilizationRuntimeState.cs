namespace GlobalArena.Runtime;

public sealed class CivilizationRuntimeState
{
    private static readonly CivilizationRuntimeState EmptyInstance =
        new(
            Array.Empty<CivilizationRuntimeRecord>());

    private readonly CivilizationRuntimeRecord[] _records;

    private readonly IReadOnlyList<CivilizationRuntimeRecord> _readOnlyRecords;

    private readonly CivilizationId[] _civilizations;

    private readonly IReadOnlyList<CivilizationId> _readOnlyCivilizations;

    public static CivilizationRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<CivilizationRuntimeRecord> Records =>
        _readOnlyRecords;

    public IReadOnlyList<CivilizationId> Civilizations =>
        _readOnlyCivilizations;

    public CivilizationRuntimeState(
        IEnumerable<CivilizationId> civilizations)
        : this(
            CreateIdentityOnlyRecords(
                civilizations))
    {
    }

    public CivilizationRuntimeState(
        IEnumerable<CivilizationRuntimeRecord> records)
    {
        ArgumentNullException.ThrowIfNull(
            records);

        var canonical =
            records.ToArray();

        if (canonical.Any(
            record =>
                record is null))
        {
            throw new ArgumentException(
                "Civilization runtime state cannot contain null records.",
                nameof(records));
        }

        canonical =
            canonical
                .OrderBy(
                    record =>
                        record.Id.Value)
                .ToArray();

        var materializedStarts =
            new HashSet<GlobalArena.World.StrategicCellId>();

        for (var index = 0;
             index < canonical.Length;
             index++)
        {
            var current =
                canonical[index];

            if (!current.Id.IsValid)
            {
                throw new ArgumentException(
                    "Civilization runtime state cannot contain invalid identities.",
                    nameof(records));
            }

            if (index > 0
                && canonical[index - 1].Id
                    == current.Id)
            {
                throw new ArgumentException(
                    "Civilization runtime state cannot contain duplicate identities.",
                    nameof(records));
            }

            if (current.StartCellId
                is GlobalArena.World.StrategicCellId startCellId)
            {
                if (!materializedStarts.Add(
                    startCellId))
                {
                    throw new ArgumentException(
                        "Materialized civilizations cannot share the same strategic start cell.",
                        nameof(records));
                }
            }
        }

        _records =
            canonical;

        _readOnlyRecords =
            Array.AsReadOnly(
                _records);

        _civilizations =
            canonical
                .Select(
                    record =>
                        record.Id)
                .ToArray();

        _readOnlyCivilizations =
            Array.AsReadOnly(
                _civilizations);
    }

    private static CivilizationRuntimeRecord[] CreateIdentityOnlyRecords(
        IEnumerable<CivilizationId> civilizations)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        return civilizations
            .Select(
                civilization =>
                    new CivilizationRuntimeRecord(
                        civilization))
            .ToArray();
    }
}
