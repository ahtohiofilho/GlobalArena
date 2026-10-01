namespace GlobalArena.Runtime;

public sealed class EconomyRuntimeState
{
    private static readonly EconomyRuntimeState EmptyInstance =
        new(
            Array.Empty<StrategicStockEntry>());

    public static EconomyRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<StrategicStockEntry> StrategicStocks { get; }

    public EconomyRuntimeState(
        IEnumerable<StrategicStockEntry> strategicStocks)
    {
        ArgumentNullException.ThrowIfNull(
            strategicStocks);

        var canonical =
            strategicStocks
                .ToArray();

        if (canonical.Any(
            stock =>
                stock is null))
        {
            throw new ArgumentException(
                "Strategic stock collection cannot contain null.",
                nameof(strategicStocks));
        }

        canonical =
            canonical
                .OrderBy(
                    stock =>
                        stock.Owner.Value)
                .ThenBy(
                    stock =>
                        stock.Commodity.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            var previous =
                canonical[index - 1];

            var current =
                canonical[index];

            if (previous.Owner == current.Owner
                && previous.Commodity == current.Commodity)
            {
                throw new ArgumentException(
                    "Strategic stock collection cannot contain duplicate owner/commodity keys.",
                    nameof(strategicStocks));
            }
        }

        StrategicStocks =
            Array.AsReadOnly(
                canonical);
    }
}
