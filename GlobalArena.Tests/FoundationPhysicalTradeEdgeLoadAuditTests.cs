using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalTradeEdgeLoadAuditTests
{
    private const string Unit = "test-normalized-cargo-micro";
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static WorldGenerationRequest Request() => new(
        new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0));

    private static PhysicalTacticalIncidenceMap Map() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(Request()).Incidence;

    private static (PhysicalTacticalTileId A, PhysicalTacticalTileId B,
        PhysicalTacticalTileId C, PhysicalTacticalEdgeId AB,
        PhysicalTacticalEdgeId BC) Chain(PhysicalTacticalIncidenceMap map)
    {
        var a = map.TileIncidences[0].PhysicalTacticalTileId;
        var first = map.FineTopology.Cells[0];
        var bId = first.AdjacentCellIds[0];
        var b = new PhysicalTacticalTileId(map.Refinement.FineParameters, bId);
        var cId = map.FineTopology.Cells[checked((int)bId.Value - 1)]
            .AdjacentCellIds.First(id => id != first.Id);
        var c = new PhysicalTacticalTileId(map.Refinement.FineParameters, cId);
        return (a, b, c, Edge(map, a, b), Edge(map, b, c));
    }

    private static PhysicalTacticalEdgeId Edge(PhysicalTacticalIncidenceMap map,
        PhysicalTacticalTileId a, PhysicalTacticalTileId b)
    {
        var id = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(a.FineStrategicCellId) &&
            edge.IncidentCellIds.Contains(b.FineStrategicCellId)).Id;
        return new PhysicalTacticalEdgeId(map.Refinement.FineParameters, id);
    }

    private static FoundationTransportEdgeOwnershipEvidence Owned(
        PhysicalTacticalEdgeId edge, CivilizationId owner) =>
        new(edge, FoundationInfrastructureOwnershipKind.Owned, owner);

    private static FoundationTransportEdgeOwnershipEvidence Natural(
        PhysicalTacticalEdgeId edge) =>
        new(edge, FoundationInfrastructureOwnershipKind.NoInfrastructure, null);

    private static FoundationFreightDeliveryRouteEvidence Delivery(
        ulong id, CommodityId commodity, ulong cargo,
        params PhysicalTacticalTileId[] route) =>
        new(id, commodity, cargo, Unit, route);

    [Fact]
    public void RealCrossingsAggregateBothDirectionsAndDifferentCommoditiesWithoutInventedSales()
    {
        var map = Map();
        var (a, b, c, ab, bc) = Chain(map);
        var trades = new[]
        {
            Delivery(1, Food, 3_000_000UL, a, b, c),
            Delivery(2, Wood, 2_000_000UL, c, b, a),
            Delivery(3, Food, 1_000_000UL, b, a),
            Delivery(4, Food, 7_000_000UL, a)
        };
        var evidence = new[] { Owned(ab, Red), Natural(bc) };
        var audit = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, trades, evidence);

        Assert.Equal(Unit, audit.CargoUnitCode);
        Assert.Equal(4, audit.DeliveryCount);
        Assert.Equal(1, audit.LocalDeliveryCount);
        Assert.Equal(13_000_000UL, audit.TotalDeliveredCargoMicro);
        Assert.Equal(11_000_000UL, audit.TotalEdgeTraversalCargoMicro);
        Assert.Equal(2, audit.UsedEdges.Count);

        var red = Assert.Single(audit.UsedEdges, load => load.Edge == ab);
        Assert.Equal(6_000_000UL, red.TotalCargoMicro);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned, red.OwnershipKind);
        Assert.Equal(Red, red.Owner);
        Assert.Equal(new[] { a, b }, new[] { red.FirstTile, red.SecondTile }
            .OrderBy(x => x.FineStrategicCellId.Value));
        Assert.Equal(new[] { Food, Wood },
            red.ByCommodity.Select(x => x.Commodity));
        Assert.Equal(new[] { 4_000_000UL, 2_000_000UL },
            red.ByCommodity.Select(x => x.CargoMicro));

        var natural = Assert.Single(audit.UsedEdges, load => load.Edge == bc);
        Assert.Equal(5_000_000UL, natural.TotalCargoMicro);
        Assert.Equal(FoundationInfrastructureOwnershipKind.NoInfrastructure,
            natural.OwnershipKind);
        Assert.Null(natural.Owner);
        Assert.Equal(new[] { 3_000_000UL, 2_000_000UL },
            natural.ByCommodity.Select(x => x.CargoMicro));

        // One local sale of seven units crosses NO physical edge. Sum of
        // edge loads is not and must not be mistaken for total sales.
        Assert.NotEqual(audit.TotalEdgeTraversalCargoMicro,
            audit.TotalDeliveredCargoMicro);
    }

    [Fact]
    public void OwnerSnapshotIsHistoricalAndNotRecoloredByLaterEvidence()
    {
        var map = Map();
        var (a, b, _, ab, _) = Chain(map);
        var route = Delivery(1, Food, 4UL, a, b);
        var old = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, new[] { route }, new[] { Owned(ab, Red) });
        var newer = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, new[] { route }, new[] { Owned(ab, Blue) });

        Assert.Equal(Red, Assert.Single(old.UsedEdges).Owner);
        Assert.Equal(Blue, Assert.Single(newer.UsedEdges).Owner);
        Assert.Equal(4UL, Assert.Single(old.UsedEdges).TotalCargoMicro);
        Assert.Equal(4UL, Assert.Single(newer.UsedEdges).TotalCargoMicro);
    }

    [Theory]
    [InlineData(FoundationInfrastructureOwnershipKind.Unknown)]
    [InlineData(FoundationInfrastructureOwnershipKind.NoInfrastructure)]
    [InlineData(FoundationInfrastructureOwnershipKind.UnownedInfrastructure)]
    public void UnknownNaturalAndUnownedInfrastructureAreThreeDistinctStates(
        FoundationInfrastructureOwnershipKind kind)
    {
        var map = Map();
        var (a, b, _, ab, _) = Chain(map);
        var audit = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, new[] { Delivery(1, Food, 1UL, a, b) },
            new[] { new FoundationTransportEdgeOwnershipEvidence(ab, kind, null) });
        var line = Assert.Single(audit.UsedEdges);
        Assert.Equal(kind, line.OwnershipKind);
        Assert.Null(line.Owner);
    }

    [Fact]
    public void PhysicalIdentityAndLoadAreDeterministicUnderDeliveryPermutation()
    {
        var map = Map();
        var (a, b, c, ab, bc) = Chain(map);
        var trades = new[]
        {
            Delivery(1, Food, 30UL, a, b, c),
            Delivery(2, Wood, 20UL, c, b, a),
            Delivery(3, Food, 10UL, b, a),
        };
        var owners = new[] { Natural(bc), Owned(ab, Red) };
        var left = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, trades, owners);
        var right = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, trades.Reverse(), owners.Reverse());
        Assert.Equal(left.TotalDeliveredCargoMicro,
            right.TotalDeliveredCargoMicro);
        Assert.Equal(left.TotalEdgeTraversalCargoMicro,
            right.TotalEdgeTraversalCargoMicro);
        Assert.Equal(left.UsedEdges.Select(x => (
            x.Edge, x.FirstTile, x.SecondTile, x.TotalCargoMicro,
            x.OwnershipKind, x.Owner)),
            right.UsedEdges.Select(x => (
            x.Edge, x.FirstTile, x.SecondTile, x.TotalCargoMicro,
            x.OwnershipKind, x.Owner)));
        Assert.Equal(left.UsedEdges.SelectMany(edge =>
            edge.ByCommodity.Select(x =>
                (edge.Edge, x.Commodity, x.CargoMicro))),
            right.UsedEdges.SelectMany(edge =>
            edge.ByCommodity.Select(x =>
                (edge.Edge, x.Commodity, x.CargoMicro))));
        Assert.Equal(left.UsedEdges.OrderBy(x =>
            x.Edge.FineStrategicEdgeId.Value).Select(x => x.Edge),
            left.UsedEdges.Select(x => x.Edge));
    }

    [Fact]
    public void IncompleteOwnershipOrForeignCorridorFailsClosed()
    {
        var map = Map();
        var (a, b, _, ab, _) = Chain(map);
        var trades = new[] { Delivery(1, Food, 1UL, a, b) };
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, trades,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, trades, new[] { Owned(ab, Red), Owned(ab, Blue) }));
        var foreign = new PhysicalTacticalEdgeId(
            new GoldbergParameters(12, 0), ab.FineStrategicEdgeId);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, trades, new[] { Natural(foreign) }));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, trades,
                new[] { new FoundationTransportEdgeOwnershipEvidence(
                    ab, FoundationInfrastructureOwnershipKind.Owned, null) }));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, trades,
                new[] { new FoundationTransportEdgeOwnershipEvidence(
                    ab, FoundationInfrastructureOwnershipKind.NoInfrastructure, Red) }));
    }

    [Fact]
    public void InvalidForeignRepeatedOrNonAdjacentRouteIsRejected()
    {
        var map = Map();
        var (a, b, _, ab, _) = Chain(map);
        var unrelated = map.TileIncidences.Select(x => x.PhysicalTacticalTileId)
            .First(tile => tile != a &&
                !map.FineTopology.Cells[0]
                    .AdjacentCellIds.Contains(tile.FineStrategicCellId));
        var foreign = new PhysicalTacticalTileId(
            new GoldbergParameters(12, 0), new StrategicCellId(1UL));
        var owners = new[] { Owned(ab, Red) };
        var bad = new[]
        {
            Delivery(1, Food, 1UL, a, unrelated),
            Delivery(1, Food, 1UL, a, a),
            Delivery(1, Food, 1UL, a, b, a),
            Delivery(1, Food, 1UL, a, foreign),
        };
        foreach (var route in bad)
            Assert.Throws<ArgumentException>(() =>
                FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                    map, Unit, new[] { route }, owners));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, new[] {
                    Delivery(1, Food, 1UL, a, b),
                    Delivery(1, Food, 2UL, a, b)
                }, owners));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, new[] {
                    Delivery(1, Food, 1UL, a, b),
                    new FoundationFreightDeliveryRouteEvidence(
                        2, Food, 1UL, "kg", new[] { a, b })
                }, owners));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, new[] {
                    new FoundationFreightDeliveryRouteEvidence(
                        1, Food, 0UL, Unit, new[] { a })
                }, owners));
    }

    [Fact]
    public void EmptyEvidenceIsOnlyAnEmptyAuditNotProofOfAClosedEconomicCycle()
    {
        var map = Map();
        var empty = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, Array.Empty<FoundationFreightDeliveryRouteEvidence>(),
            Array.Empty<FoundationTransportEdgeOwnershipEvidence>());
        Assert.Empty(empty.UsedEdges);
        Assert.Equal(0, empty.DeliveryCount);
        Assert.Equal(0, empty.LocalDeliveryCount);
        Assert.Equal(0UL, empty.TotalDeliveredCargoMicro);
        Assert.Equal(0UL, empty.TotalEdgeTraversalCargoMicro);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, " ", Array.Empty<FoundationFreightDeliveryRouteEvidence>(),
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
    }

    [Fact]
    public void TenThousandFreightDeliveriesAggregateToOneCrossingNotTenThousandVisualObjects()
    {
        var map = Map();
        var (a, b, _, ab, _) = Chain(map);
        var deliveries = Enumerable.Range(1, 10_000)
            .Select(i => Delivery((ulong)i, i % 2 == 0 ? Food : Wood,
                1_000_000UL, i % 2 == 0 ? a : b, i % 2 == 0 ? b : a));
        var audit = FoundationPhysicalTradeEdgeLoadAuditor.Audit(
            map, Unit, deliveries, new[] { Owned(ab, Red) });
        Assert.Equal(10_000, audit.DeliveryCount);
        Assert.Equal(0, audit.LocalDeliveryCount);
        Assert.Equal(10_000_000_000UL, audit.TotalDeliveredCargoMicro);
        Assert.Equal(10_000_000_000UL, audit.TotalEdgeTraversalCargoMicro);
        var row = Assert.Single(audit.UsedEdges);
        Assert.Equal(10_000_000_000UL, row.TotalCargoMicro);
        Assert.Equal(2, row.ByCommodity.Count);
        Assert.All(row.ByCommodity,
            commodity => Assert.Equal(5_000_000_000UL, commodity.CargoMicro));
    }

    [Fact]
    public void OverflowCannotPublishPartiallyAuditedEdgeVolume()
    {
        var map = Map();
        var (a, b, _, ab, _) = Chain(map);
        var trades = new[]
        {
            Delivery(1, Food, ulong.MaxValue, a, b),
            Delivery(2, Wood, 1UL, b, a)
        };
        Assert.Throws<OverflowException>(() =>
            FoundationPhysicalTradeEdgeLoadAuditor.Audit(
                map, Unit, trades, new[] { Owned(ab, Red) }));
    }
}
