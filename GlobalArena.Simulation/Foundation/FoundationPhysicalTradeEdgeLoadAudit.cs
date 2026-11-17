using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit historical infrastructure ownership as observed by the eventual
/// economic-cycle publisher. UNKNOWN is not "no infrastructure"; unowned
/// infrastructure is not the same thing as natural unbuilt connectivity.
/// </summary>
public enum FoundationInfrastructureOwnershipKind : uint
{
    Unknown = 1,
    NoInfrastructure = 2,
    UnownedInfrastructure = 3,
    Owned = 4
}

/// <summary>
/// Caller-supplied ownership evidence. This type does NOT create an asset,
/// claim a civilization owns a tile, or certify its historical provenance.
/// </summary>
public sealed record FoundationTransportEdgeOwnershipEvidence(
    PhysicalTacticalEdgeId Edge,
    FoundationInfrastructureOwnershipKind Kind,
    CivilizationId? Owner);

/// <summary>
/// ONE already-selected freight delivery, identified by a unique ordinal.
/// RouteTiles are physical centers in travel order, including both endpoints;
/// one tile means a local sale and zero transport crossings. The caller must
/// ultimately be the actual closed economic-cycle publisher, NOT the Godot UI.
/// </summary>
public sealed record FoundationFreightDeliveryRouteEvidence(
    ulong DeliveryOrdinal,
    CommodityId Commodity,
    ulong CargoMicro,
    string CargoUnitCode,
    IReadOnlyList<PhysicalTacticalTileId> RouteTiles);

/// <summary>
/// Audited volume of one commodity on ONE physical dual-graph crossing.
/// Volumes in opposite directions add; one route can consume several edges.
/// </summary>
public sealed record FoundationPhysicalEdgeCommodityLoad(
    CommodityId Commodity,
    ulong CargoMicro);

/// <summary>
/// Immutable, one-row-per-used-edge projection, not a visual line or a market
/// settlement. The geometry is the crossing between the centers of FirstTile
/// and SecondTile, NOT a road running along the shared geometric border.
/// </summary>
public sealed class FoundationPhysicalTradeEdgeLoad
{
    public PhysicalTacticalEdgeId Edge { get; }
    public PhysicalTacticalTileId FirstTile { get; }
    public PhysicalTacticalTileId SecondTile { get; }
    public ulong TotalCargoMicro { get; }
    public FoundationInfrastructureOwnershipKind OwnershipKind { get; }
    public CivilizationId? Owner { get; }
    public IReadOnlyList<FoundationPhysicalEdgeCommodityLoad> ByCommodity { get; }

    internal FoundationPhysicalTradeEdgeLoad(
        PhysicalTacticalEdgeId edge,
        PhysicalTacticalTileId first,
        PhysicalTacticalTileId second,
        ulong volume,
        FoundationTransportEdgeOwnershipEvidence ownership,
        FoundationPhysicalEdgeCommodityLoad[] byCommodity)
    {
        Edge = edge;
        FirstTile = first;
        SecondTile = second;
        TotalCargoMicro = volume;
        OwnershipKind = ownership.Kind;
        Owner = ownership.Owner;
        ByCommodity = Array.AsReadOnly(byCommodity);
    }
}

/// <summary>
/// Result of STRICT STRUCTURAL AUDITING of caller-supplied freight evidence.
/// It is deliberately NOT the host's last closed economic cycle: no such
/// production settlement/publication exists yet in GA-Next v11. Never render
/// this output as factual historical trade without the future authority gate.
/// </summary>
public sealed class FoundationPhysicalTradeEdgeLoadAudit
{
    public string CargoUnitCode { get; }
    public int DeliveryCount { get; }
    public int LocalDeliveryCount { get; }
    public ulong TotalDeliveredCargoMicro { get; }
    public ulong TotalEdgeTraversalCargoMicro { get; }
    public IReadOnlyList<FoundationPhysicalTradeEdgeLoad> UsedEdges { get; }

    internal FoundationPhysicalTradeEdgeLoadAudit(
        string unit, int deliveries, int locals, ulong delivered,
        ulong traversed, FoundationPhysicalTradeEdgeLoad[] usedEdges)
    {
        CargoUnitCode = unit;
        DeliveryCount = deliveries;
        LocalDeliveryCount = locals;
        TotalDeliveredCargoMicro = delivered;
        TotalEdgeTraversalCargoMicro = traversed;
        UsedEdges = Array.AsReadOnly(usedEdges);
    }
}

/// <summary>
/// Validates real fine-topology crossings and conserves cargo along submitted
/// routes. Accepts no made-up shortest paths, default edge ownership, or
/// implicit commodity conversion. All submitted quantities must ALREADY be in
/// the single explicitly declared comparable cargo-micro unit.
/// </summary>
public static class FoundationPhysicalTradeEdgeLoadAuditor
{
    private sealed class EdgeBucket
    {
        public ulong Total;
        public Dictionary<CommodityId, ulong> Commodities = new();
    }

    public static FoundationPhysicalTradeEdgeLoadAudit Audit(
        PhysicalTacticalIncidenceMap incidence,
        string cargoUnitCode,
        IEnumerable<FoundationFreightDeliveryRouteEvidence> deliveries,
        IEnumerable<FoundationTransportEdgeOwnershipEvidence> ownership)
    {
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(deliveries);
        ArgumentNullException.ThrowIfNull(ownership);
        if (string.IsNullOrWhiteSpace(cargoUnitCode) ||
            cargoUnitCode != cargoUnitCode.Trim())
            throw new ArgumentException(
                "Comparable cargo unit must be explicitly defined.",
                nameof(cargoUnitCode));

        var fine = incidence.Refinement.FineParameters;
        var cellCount = incidence.FineTopology.Cells.Count;
        var crossings = new Dictionary<
            (ulong First, ulong Second),
            (PhysicalTacticalEdgeId Edge, PhysicalTacticalTileId First,
             PhysicalTacticalTileId Second)>(incidence.FineTopology.Edges.Count);
        foreach (var geometricBorder in incidence.FineTopology.Edges)
        {
            var a = geometricBorder.IncidentCellIds[0];
            var b = geometricBorder.IncidentCellIds[1];
            var edge = new PhysicalTacticalEdgeId(fine, geometricBorder.Id);
            if (!crossings.TryAdd((a.Value, b.Value), (
                edge, new PhysicalTacticalTileId(fine, a),
                new PhysicalTacticalTileId(fine, b))))
                throw new InvalidOperationException(
                    "Physical fine topology contains a duplicated crossing.");
        }

        var owners = new Dictionary<PhysicalTacticalEdgeId,
            FoundationTransportEdgeOwnershipEvidence>();
        foreach (var entry in ownership)
        {
            if (entry is null || !entry.Edge.IsValid ||
                entry.Edge.FineGoldbergParameters != fine ||
                !Enum.IsDefined(entry.Kind) ||
                (entry.Kind == FoundationInfrastructureOwnershipKind.Owned
                    ? entry.Owner is not { IsValid: true }
                    : entry.Owner.HasValue))
                throw new ArgumentException(
                    "Historical infrastructure evidence must use a valid " +
                    "physical edge and explicit ownership state.", nameof(ownership));
            if (!owners.TryAdd(entry.Edge, entry))
                throw new ArgumentException(
                    "An edge has more than one historical ownership entry.",
                    nameof(ownership));
        }

        var loads = new Dictionary<PhysicalTacticalEdgeId, EdgeBucket>();
        var ids = new HashSet<ulong>();
        var deliveredCargo = 0UL;
        var traversalCargo = 0UL;
        var localCount = 0;
        var deliveryCount = 0;
        foreach (var delivery in deliveries)
        {
            if (delivery is null || delivery.DeliveryOrdinal == 0UL ||
                !delivery.Commodity.IsValid || delivery.CargoMicro == 0UL ||
                delivery.CargoUnitCode != cargoUnitCode ||
                delivery.RouteTiles is null || delivery.RouteTiles.Count == 0)
                throw new ArgumentException(
                    "Every delivery needs a positive unique ordinal, known " +
                    "commodity, positive cargo and matching common unit.",
                    nameof(deliveries));
            if (!ids.Add(delivery.DeliveryOrdinal))
                throw new ArgumentException(
                    "A delivery cannot be counted more than once.",
                    nameof(deliveries));

            var seenTiles = new HashSet<PhysicalTacticalTileId>();
            PhysicalTacticalTileId? previous = null;
            foreach (var tile in delivery.RouteTiles)
            {
                if (!tile.IsValid || tile.FineGoldbergParameters != fine ||
                    tile.FineStrategicCellId.Value > (ulong)cellCount ||
                    !seenTiles.Add(tile))
                    throw new ArgumentException(
                        "Delivery route contains a foreign, repeated or invalid fine tile.",
                        nameof(deliveries));
                if (previous is PhysicalTacticalTileId start)
                {
                    var a = Math.Min(start.FineStrategicCellId.Value,
                        tile.FineStrategicCellId.Value);
                    var b = Math.Max(start.FineStrategicCellId.Value,
                        tile.FineStrategicCellId.Value);
                    if (!crossings.TryGetValue((a, b), out var crossing))
                        throw new ArgumentException(
                            "Delivery route cannot jump across non-adjacent tiles.",
                            nameof(deliveries));
                    if (!owners.ContainsKey(crossing.Edge))
                        throw new InvalidOperationException(
                            "A used crossing has no historical infrastructure " +
                            "status; unknown must be supplied explicitly.");
                    if (!loads.TryGetValue(crossing.Edge, out var bucket))
                        loads.Add(crossing.Edge, bucket = new EdgeBucket());
                    bucket.Total = checked(bucket.Total + delivery.CargoMicro);
                    bucket.Commodities[delivery.Commodity] = checked(
                        bucket.Commodities.GetValueOrDefault(delivery.Commodity) +
                        delivery.CargoMicro);
                    traversalCargo = checked(traversalCargo + delivery.CargoMicro);
                }
                previous = tile;
            }

            deliveryCount = checked(deliveryCount + 1);
            if (delivery.RouteTiles.Count == 1)
                localCount = checked(localCount + 1);
            deliveredCargo = checked(deliveredCargo + delivery.CargoMicro);
        }

        var edges = loads
            .OrderBy(pair => pair.Key.FineStrategicEdgeId.Value)
            .Select(pair =>
            {
                // Each output edge is a real, uniquely qualified physical
                // crossing. Its endpoints are canonical and immutable.
                var geometric = incidence.FineTopology.Edges[
                    checked((int)pair.Key.FineStrategicEdgeId.Value - 1)];
                var tileA = new PhysicalTacticalTileId(
                    fine, geometric.IncidentCellIds[0]);
                var tileB = new PhysicalTacticalTileId(
                    fine, geometric.IncidentCellIds[1]);
                var commodityLoads = pair.Value.Commodities
                    .OrderBy(x => x.Key.Value)
                    .Select(x => new FoundationPhysicalEdgeCommodityLoad(
                        x.Key, x.Value))
                    .ToArray();
                if (commodityLoads.Aggregate(0UL, (sum, x) =>
                    checked(sum + x.CargoMicro)) != pair.Value.Total)
                    throw new InvalidOperationException(
                        "Commodity-level and total edge cargo disagree.");
                return new FoundationPhysicalTradeEdgeLoad(
                    pair.Key, tileA, tileB, pair.Value.Total,
                    owners[pair.Key], commodityLoads);
            })
            .ToArray();

        if (edges.Aggregate(0UL, (sum, edge) =>
            checked(sum + edge.TotalCargoMicro)) != traversalCargo)
            throw new InvalidOperationException(
                "Per-edge cargo differs from real route traversals.");

        return new FoundationPhysicalTradeEdgeLoadAudit(
            cargoUnitCode, deliveryCount, localCount, deliveredCargo,
            traversalCargo, edges);
    }
}
