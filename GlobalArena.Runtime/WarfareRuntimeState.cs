namespace GlobalArena.Runtime;

public sealed class WarfareRuntimeState
{
    private static readonly WarfareRuntimeState EmptyInstance =
        new(
            Array.Empty<MilitaryUnitRuntimeState>());

    public static WarfareRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<MilitaryUnitRuntimeState> Units { get; }

    public WarfareRuntimeState(
        IEnumerable<MilitaryUnitRuntimeState> units)
    {
        ArgumentNullException.ThrowIfNull(
            units);

        var canonical =
            units
                .ToArray();

        if (canonical.Any(
            unit =>
                unit is null))
        {
            throw new ArgumentException(
                "Military unit collection cannot contain null.",
                nameof(units));
        }

        canonical =
            canonical
                .OrderBy(
                    unit =>
                        unit.Id.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            if (canonical[index - 1].Id
                == canonical[index].Id)
            {
                throw new ArgumentException(
                    "Military unit collection cannot contain duplicate unit identities.",
                    nameof(units));
            }
        }

        Units =
            Array.AsReadOnly(
                canonical);
    }
}
