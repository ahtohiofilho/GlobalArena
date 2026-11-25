using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>One commodity at one physical node, scoped to the viewer's own
/// worker-time and sales. Imports/exports cross physical nodes, not borders.
/// Market price is the shared buyer-node price observed in that close,
/// exposed only where the viewer had positive demand. Null is not zero.</summary>
public sealed record FoundationLocalEconomicCommodityRow(
    CommodityId Commodity, ulong ProducedMicro, ulong DemandMicro,
    ulong SoldMicro, ulong ReceivedMicro, ulong ExportedMicro, ulong ImportedMicro,
    ulong? LocalPriceMicro, BigInteger GrossSalesScaled, BigInteger SellerNetScaled);

public sealed record FoundationViewerLocalEconomicClose(
    CivilizationId Viewer, PhysicalTacticalTileId Node, ulong EconomicCycleOrdinal,
    WorldStateHash CommittedWorldHash, IReadOnlyList<FoundationLocalEconomicCommodityRow> Commodities);

internal sealed record FoundationOwnedLocalEconomicRow(
    CivilizationId Owner, PhysicalTacticalTileId Node, FoundationLocalEconomicCommodityRow Commodity);

/// <summary>Derived only from the actual temporal calendar and already
/// reconciled accounting. No route, price, demand or market recomputation.
/// The server retains one close, never exposes all owners' rows.</summary>
internal sealed class FoundationSettledLocalEconomics
{
    internal ulong CycleOrdinal { get; }
    internal IReadOnlyList<FoundationOwnedLocalEconomicRow> Rows { get; }
    private readonly Dictionary<(CivilizationId, PhysicalTacticalTileId), IReadOnlyList<FoundationLocalEconomicCommodityRow>> _byOwnerNode;

    internal FoundationSettledLocalEconomics(ulong cycleOrdinal, IEnumerable<FoundationOwnedLocalEconomicRow> rows)
    {
        if (cycleOrdinal == 0UL) throw new ArgumentOutOfRangeException(nameof(cycleOrdinal));
        CycleOrdinal = cycleOrdinal;
        Rows = Array.AsReadOnly(rows.OrderBy(r => r.Owner.Value)
            .ThenBy(r => r.Node.FineStrategicCellId.Value).ThenBy(r => r.Commodity.Commodity.Value).ToArray());
        _byOwnerNode = Rows.GroupBy(r => (r.Owner, r.Node)).ToDictionary(g => g.Key,
            g => (IReadOnlyList<FoundationLocalEconomicCommodityRow>)Array.AsReadOnly(g.Select(r => r.Commodity).ToArray()));
    }

    internal static FoundationSettledLocalEconomics FromClose(ulong ordinal,
        FoundationTemporalEconomicTurnInputs calendar,
        IReadOnlyList<FoundationPhysicalEconomicAccountingLine> lines,
        FoundationSettledLocalPriceSnapshot prices, IEnumerable<CommodityId> commodities)
    {
        if (prices.CycleOrdinal != ordinal) throw new InvalidOperationException("Local evidence cycle mismatch.");
        var catalog = commodities.OrderBy(c => c.Value).ToArray();
        var production = calendar.Production.ToLookup(p => (p.Owner, p.Node, p.Commodity));
        var demand = calendar.Presence.Demand.ToLookup(p => (p.Owner, p.Node, p.Commodity));
        var sales = lines.ToLookup(l => (Owner: l.Seller, Node: l.SourceTile, l.Commodity));
        var purchases = lines.ToLookup(l => (Owner: l.Buyer, Node: l.BuyerTile, l.Commodity));
        var rows = new List<FoundationOwnedLocalEconomicRow>();
        foreach (var bucket in calendar.Presence.Buckets)
        foreach (var item in catalog)
        {
            var key = (bucket.Owner, bucket.Node, item);
            var outgoing = sales[key];
            var incoming = purchases[key];
            var output = production[key].Aggregate(0UL, (sum, p) => checked(sum + p.ProducedMicro));
            var need = demand[key].Aggregate(0UL, (sum, p) => checked(sum + p.DemandMicro));
            var sold = outgoing.Aggregate(0UL, (sum, l) => checked(sum + l.GoodsMicro));
            var received = incoming.Aggregate(0UL, (sum, l) => checked(sum + l.GoodsMicro));
            if (sold != output) throw new InvalidOperationException("Local settled output must equal own sales.");
            rows.Add(new(bucket.Owner, bucket.Node, new(item, output, need, sold, received,
                outgoing.Where(l => l.BuyerTile != bucket.Node).Aggregate(0UL, (sum, l) => checked(sum + l.GoodsMicro)),
                incoming.Where(l => l.SourceTile != bucket.Node).Aggregate(0UL, (sum, l) => checked(sum + l.GoodsMicro)),
                need > 0UL ? prices.ObservedPrice(bucket.Node, item) : null,
                outgoing.Aggregate(BigInteger.Zero, (sum, l) => sum + l.GrossScaled),
                outgoing.Aggregate(BigInteger.Zero, (sum, l) => sum + l.SellerNetScaled))));
        }
        return new(ordinal, rows);
    }

    internal IReadOnlyList<FoundationLocalEconomicCommodityRow>? Read(CivilizationId owner, PhysicalTacticalTileId node) =>
        _byOwnerNode.TryGetValue((owner, node), out var rows) ? rows : null;

    internal bool Identical(FoundationSettledLocalEconomics other) =>
        CycleOrdinal == other.CycleOrdinal && Rows.SequenceEqual(other.Rows);
}
