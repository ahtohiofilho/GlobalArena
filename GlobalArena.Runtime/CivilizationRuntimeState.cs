namespace GlobalArena.Runtime;

public sealed class CivilizationRuntimeState
{
    private static readonly CivilizationRuntimeState EmptyInstance =
        new(
            Array.Empty<CivilizationId>());

    public static CivilizationRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<CivilizationId> Civilizations { get; }

    public CivilizationRuntimeState(
        IEnumerable<CivilizationId> civilizations)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        var canonical =
            civilizations
                .ToArray();

        if (canonical.Any(
            civilization =>
                !civilization.IsValid))
        {
            throw new ArgumentException(
                "Civilization roster cannot contain invalid identities.",
                nameof(civilizations));
        }

        canonical =
            canonical
                .OrderBy(
                    civilization =>
                        civilization.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            if (canonical[index - 1]
                == canonical[index])
            {
                throw new ArgumentException(
                    "Civilization roster cannot contain duplicate identities.",
                    nameof(civilizations));
            }
        }

        Civilizations =
            Array.AsReadOnly(
                canonical);
    }
}
