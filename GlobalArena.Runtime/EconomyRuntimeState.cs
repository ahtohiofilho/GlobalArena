namespace GlobalArena.Runtime;

public sealed class EconomyRuntimeState
{
    private static readonly EconomyRuntimeState EmptyInstance =
        new(
            Array.Empty<EconomicPointRuntimeState>());

    private readonly EconomicPointRuntimeState[] _economicPoints;

    private readonly IReadOnlyList<EconomicPointRuntimeState> _readOnlyEconomicPoints;

    public static EconomyRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<EconomicPointRuntimeState> EconomicPoints =>
        _readOnlyEconomicPoints;

    public EconomyRuntimeState(
        IEnumerable<EconomicPointRuntimeState> economicPoints)
    {
        ArgumentNullException.ThrowIfNull(
            economicPoints);

        var canonical =
            economicPoints.ToArray();

        if (canonical.Any(
            point =>
                point is null))
        {
            throw new ArgumentException(
                "Economy runtime state cannot contain null economic points.",
                nameof(economicPoints));
        }

        canonical =
            canonical
                .OrderBy(
                    point =>
                        point.Id.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            if (canonical[index - 1].Id
                == canonical[index].Id)
            {
                throw new ArgumentException(
                    "Economy runtime state cannot contain duplicate economic point identities.",
                    nameof(economicPoints));
            }
        }

        _economicPoints =
            canonical;

        _readOnlyEconomicPoints =
            Array.AsReadOnly(
                _economicPoints);
    }
}