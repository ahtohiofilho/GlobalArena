using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

internal sealed record FoundationSettledLocalPrice(
    PhysicalTacticalTileId Node, CommodityId Commodity, ulong PriceMicro);

/// <summary>Server-side observation of actual buyer-node market prices from
/// one verified temporal close. No route, owner, volume, or financial solve.
/// Missing observations are distinct from an actual zero price.</summary>
internal sealed class FoundationSettledLocalPriceSnapshot
{
    internal const ulong InitialReferencePriceMicro =
        FoundationPhysicalEconomicAccountingProposal.CommodityMicro;
    internal ulong CycleOrdinal { get; }
    internal IReadOnlyList<FoundationSettledLocalPrice> Prices { get; }
    private readonly Dictionary<(PhysicalTacticalTileId, CommodityId), ulong> _byMarket;

    private FoundationSettledLocalPriceSnapshot(ulong cycleOrdinal,
        IEnumerable<FoundationSettledLocalPrice> prices)
    {
        if (cycleOrdinal == 0UL) throw new ArgumentOutOfRangeException(nameof(cycleOrdinal));
        CycleOrdinal = cycleOrdinal;
        var ordered = prices.OrderBy(p => p.Node.FineStrategicCellId.Value)
            .ThenBy(p => p.Commodity.Value).ToArray();
        Prices = Array.AsReadOnly(ordered);
        _byMarket = ordered.ToDictionary(p => (p.Node, p.Commodity), p => p.PriceMicro);
    }

    internal static FoundationSettledLocalPriceSnapshot FromAccounting(
        ulong cycleOrdinal, IEnumerable<FoundationPhysicalEconomicAccountingLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var prices = lines.GroupBy(line => (line.BuyerTile, line.Commodity))
            .Select(group =>
            {
                var quotes = group.Select(line => line.MarketPriceMicroPerWholeGood).Distinct().ToArray();
                if (quotes.Length != 1 || group.Any(line => line.GoodsMicro == 0UL))
                    throw new InvalidOperationException("Inconsistent settled local market evidence.");
                return new FoundationSettledLocalPrice(group.Key.BuyerTile, group.Key.Commodity, quotes[0]);
            });
        return new FoundationSettledLocalPriceSnapshot(cycleOrdinal, prices);
    }

    internal ulong? ObservedPrice(PhysicalTacticalTileId node, CommodityId commodity) =>
        _byMarket.TryGetValue((node, commodity), out var price) ? price : null;

    internal bool Identical(FoundationSettledLocalPriceSnapshot other) =>
        CycleOrdinal == other.CycleOrdinal && Prices.SequenceEqual(other.Prices);
}
