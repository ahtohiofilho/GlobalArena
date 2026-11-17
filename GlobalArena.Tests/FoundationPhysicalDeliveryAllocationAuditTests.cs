using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalDeliveryAllocationAuditTests
{
    private const string CargoUnit = "test-explicit-normalized-cargo-micro";
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId ItemOne = new(1U);
    private static readonly CommodityId ItemTwo = new(2U);

    private static FoundationTacticalHumanAiTurnHost Start() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Policy(
        params CommodityId[] commodities) =>
        new(commodities,
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => commodities.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy FreightPolicy(
        params CommodityId[] commodities) =>
        new(CargoUnit, commodities.Select((item, index) =>
            new FoundationCommodityCargoConversion(
                item, checked((ulong)(index + 1) * 1_000_000UL))));

    private static FoundationPhysicalCommodityDeliveryCandidate[] AllLocal(
        FoundationPhysicalCommodityMarketInputs inputs) =>
        inputs.SupplyLots.Select((lot, index) =>
            new FoundationPhysicalCommodityDeliveryCandidate(
                checked((ulong)index + 1UL),
                lot.Owner, lot.Occupation, lot.Tile,
                lot.Owner, lot.Tile, lot.Commodity,
                lot.CapacityMicroPerTurn, new[] { lot.Tile })).ToArray();

    private static PhysicalTacticalTileId[] ShortestPath(
        PhysicalTacticalIncidenceMap incidence,
        PhysicalTacticalTileId source, PhysicalTacticalTileId destination)
    {
        var visited = new HashSet<StrategicCellId> { source.FineStrategicCellId };
        var previous = new Dictionary<StrategicCellId, StrategicCellId>();
        var pending = new Queue<StrategicCellId>();
        pending.Enqueue(source.FineStrategicCellId);
        while (pending.Count != 0 &&
               !visited.Contains(destination.FineStrategicCellId))
        {
            var id = pending.Dequeue();
            var cell = incidence.FineTopology.Cells[checked((int)id.Value - 1)];
            foreach (var adjacent in cell.AdjacentCellIds.OrderBy(x => x.Value))
            {
                if (!visited.Add(adjacent)) continue;
                previous.Add(adjacent, id);
                pending.Enqueue(adjacent);
            }
        }
        Assert.Contains(destination.FineStrategicCellId, visited);
        var reverse = new List<StrategicCellId> {
            destination.FineStrategicCellId
        };
        while (reverse[^1] != source.FineStrategicCellId)
            reverse.Add(previous[reverse[^1]]);
        reverse.Reverse();
        return reverse.Select(id => new PhysicalTacticalTileId(
            incidence.Refinement.FineParameters, id)).ToArray();
    }

    private static FoundationTransportEdgeOwnershipEvidence[] OwnershipFor(
        PhysicalTacticalIncidenceMap incidence,
        IReadOnlyList<PhysicalTacticalTileId> route)
    {
        var result = new List<FoundationTransportEdgeOwnershipEvidence>();
        for (var i = 1; i < route.Count; i++)
        {
            var a = route[i - 1].FineStrategicCellId;
            var b = route[i].FineStrategicCellId;
            var edge = Assert.Single(incidence.FineTopology.Edges, e =>
                e.IncidentCellIds.Contains(a) &&
                e.IncidentCellIds.Contains(b));
            result.Add(new FoundationTransportEdgeOwnershipEvidence(
                new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters,
                    edge.Id),
                i == 1 ? FoundationInfrastructureOwnershipKind.Owned
                    : FoundationInfrastructureOwnershipKind.Unknown,
                i == 1 ? Red : null));
        }
        return result.ToArray();
    }

    [Fact]
    public void AllRealProducerCapacityCanBeReconciledLocallyWithoutInventedTransport()
    {
        var host = Start();
        var state = host.Snapshot;
        var hash = host.StateHash;
        var market = host.GetCommodityMarketInputs(Policy(ItemOne, ItemTwo));
        var trades = AllLocal(market);

        var audit = host.AuditDeliveryCandidates(
            Policy(ItemOne, ItemTwo), FreightPolicy(ItemOne, ItemTwo),
            trades, Array.Empty<FoundationTransportEdgeOwnershipEvidence>());
        Assert.Equal(12, audit.MarketInputs.DistinctWorkers);
        Assert.Equal(market.SupplyLots.Count, audit.DeliveryCount);
        Assert.Equal(11_000_000UL, audit.TotalAvailableGoodsMicro);
        Assert.Equal(audit.TotalAvailableGoodsMicro,
            audit.TotalAllocatedGoodsMicro);
        Assert.Equal(0UL, audit.Freight.TotalEdgeTraversalCargoMicro);
        Assert.Empty(audit.Freight.UsedEdges);
        Assert.Equal(trades.Length, audit.Freight.LocalDeliveryCount);
        var expectedCargo = trades.Aggregate(0UL, (total, trade) =>
            checked(total + trade.GoodsMicro *
                (trade.Commodity == ItemOne ? 1UL : 2UL)));
        Assert.Equal(expectedCargo, audit.Freight.TotalDeliveredCargoMicro);
        Assert.Same(state, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void PhysicalCrossCivCandidateUsesAnActualPathAndAuditsEveryTraversedEdge()
    {
        var host = Start();
        var hash = host.StateHash;
        var policy = Policy(ItemOne, ItemTwo);
        var local = AllLocal(host.GetCommodityMarketInputs(policy));
        var first = local[0];
        Assert.Equal(Red, first.Seller);
        var buyerTile = Assert.Single(
            host.GetCommodityMarketInputs(policy).DemandByCivilization,
            line => line.Owner == Blue &&
                line.Commodity == first.Commodity).Tile;
        var path = ShortestPath(
            host.Incidence, first.SourceTile, buyerTile);
        Assert.True(path.Length > 1);
        var updated = local.ToArray();
        updated[0] = first with
        {
            Buyer = Blue,
            BuyerTile = buyerTile,
            RouteTiles = path
        };
        var owners = OwnershipFor(host.Incidence, path);
        var audit = host.AuditDeliveryCandidates(
            policy, FreightPolicy(ItemOne, ItemTwo), updated, owners);

        Assert.Equal(11_000_000UL, audit.TotalAllocatedGoodsMicro);
        Assert.Equal(11_000_000UL, audit.TotalAvailableGoodsMicro);
        Assert.Equal(path.Length - 1, audit.Freight.UsedEdges.Count);
        Assert.Equal((ulong)(path.Length - 1) *
            first.GoodsMicro, audit.Freight.TotalEdgeTraversalCargoMicro);
        Assert.Equal(updated.Length - 1, audit.Freight.LocalDeliveryCount);
        Assert.Equal(first.GoodsMicro, audit.Freight.UsedEdges[0].TotalCargoMicro);
        Assert.Contains(audit.Freight.UsedEdges, line =>
            line.OwnershipKind == FoundationInfrastructureOwnershipKind.Owned &&
            line.Owner == Red);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void IncompleteOrOverAllocatedProductionCannotPassAsUniversalSale()
    {
        var host = Start();
        var demand = Policy(ItemOne, ItemTwo);
        var freight = FreightPolicy(ItemOne, ItemTwo);
        var all = AllLocal(host.GetCommodityMarketInputs(demand));
        Assert.Throws<InvalidOperationException>(() =>
            host.AuditDeliveryCandidates(demand, freight, all.Skip(1),
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
        var over = all.ToArray();
        over[0] = over[0] with { GoodsMicro = over[0].GoodsMicro + 1UL };
        Assert.Throws<InvalidOperationException>(() =>
            host.AuditDeliveryCandidates(demand, freight, over,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
        var duplicated = all.Append(all[0]);
        Assert.Throws<ArgumentException>(() =>
            host.AuditDeliveryCandidates(demand, freight, duplicated,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void EveryProposedSellerAndBuyerMustExistInTheSameCommittedCensus()
    {
        var host = Start();
        var demand = Policy(ItemOne, ItemTwo);
        var freight = FreightPolicy(ItemOne, ItemTwo);
        var all = AllLocal(host.GetCommodityMarketInputs(demand));
        foreach (var broken in new[]
        {
            all[0] with { Seller = new CivilizationId(999) },
            all[0] with { Buyer = new CivilizationId(999) },
            all[0] with { BuyerTile = default },
            all[0] with { Commodity = new CommodityId(99) },
            all[0] with { SourceOccupation = PhysicalWorkerOccupation.Mining },
            all[0] with { SourceTile = default }
        })
        {
            var changed = all.ToArray();
            changed[0] = broken;
            Assert.ThrowsAny<Exception>(() =>
                host.AuditDeliveryCandidates(demand, freight, changed,
                    Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
        }
        var wrongRoute = all.ToArray();
        wrongRoute[0] = wrongRoute[0] with
        {
            RouteTiles = new[] { wrongRoute[0].SourceTile,
                wrongRoute[0].SourceTile }
        };
        Assert.Throws<ArgumentException>(() =>
            host.AuditDeliveryCandidates(demand, freight, wrongRoute,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
    }

    [Fact]
    public void PendingOccupationOrdersDoNotAffectRealSupplyButCommitDoes()
    {
        var host = Start();
        var demand = Policy(ItemOne, ItemTwo);
        var freight = FreightPolicy(ItemOne, ItemTwo);
        var before = host.Snapshot;
        var hash = host.StateHash;
        var original = AllLocal(host.GetCommodityMarketInputs(demand));
        host.QueueHumanOccupation(Red, new PhysicalWorkerId(1UL),
            PhysicalWorkerOccupation.Mining);
        var pending = host.AuditDeliveryCandidates(demand, freight, original,
            Array.Empty<FoundationTransportEdgeOwnershipEvidence>());
        Assert.Equal(11_000_000UL, pending.TotalAllocatedGoodsMicro);
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);

        host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.NotEqual(hash, host.StateHash);
        // Old goods quantities still add up, but a source occupation is no
        // longer real. The allocation MUST be reconstructed from the new census.
        Assert.Throws<InvalidOperationException>(() =>
            host.AuditDeliveryCandidates(demand, freight, original,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
        var committed = AllLocal(host.GetCommodityMarketInputs(demand));
        var updated = host.AuditDeliveryCandidates(demand, freight, committed,
            Array.Empty<FoundationTransportEdgeOwnershipEvidence>());
        // The AI's initially traveling worker resumes production on the
        // confirmed collective turn, so capacity grows from 11 to 12.
        // This MUST be counted by the new committed economic census.
        Assert.Equal(12_000_000UL, updated.TotalAvailableGoodsMicro);
        Assert.Equal(updated.TotalAvailableGoodsMicro,
            updated.TotalAllocatedGoodsMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ConversionRequiresExactMicroUnitsAndCompleteCatalog()
    {
        Assert.Throws<ArgumentException>(() =>
            FreightPolicy(ItemOne, ItemOne));
        Assert.Throws<ArgumentException>(() =>
            new FoundationCommodityCargoConversionPolicy(CargoUnit,
                Array.Empty<FoundationCommodityCargoConversion>()));
        Assert.Throws<ArgumentException>(() =>
            new FoundationCommodityCargoConversionPolicy(CargoUnit,
                new[] { new FoundationCommodityCargoConversion(ItemOne, 0UL) }));
        Assert.Throws<ArgumentException>(() =>
            new FoundationCommodityCargoConversionPolicy(" ",
                new[] { new FoundationCommodityCargoConversion(
                    ItemOne, 1_000_000UL) }));

        var host = Start();
        var demand = Policy(ItemOne, ItemTwo);
        var allocations = AllLocal(host.GetCommodityMarketInputs(demand));
        Assert.Throws<ArgumentException>(() =>
            host.AuditDeliveryCandidates(demand, FreightPolicy(ItemOne),
                allocations,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));

        var worker = host.Snapshot.Workers.Workers[0];
        var tiny = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(101),
                Red, worker.CellId, ItemOne, 1UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                worker.TacticalTileId, PhysicalWorkerOccupation.Agriculture)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
        var tile = worker.TacticalTileId!.Value;
        var tinyDelivery = new[]
        {
            new FoundationPhysicalCommodityDeliveryCandidate(
                1UL, Red, PhysicalWorkerOccupation.Agriculture,
                tile, Red, tile, ItemOne, 1UL, new[] { tile })
        };
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalDeliveryAllocationAuditor.Audit(
                tiny, host.Incidence, Policy(ItemOne),
                new FoundationCommodityCargoConversionPolicy(CargoUnit,
                    new[] { new FoundationCommodityCargoConversion(ItemOne, 1UL) }),
                tinyDelivery,
                Array.Empty<FoundationTransportEdgeOwnershipEvidence>()));
    }

    [Fact]
    public void TenThousandWorkersCanFulfillUsingOneRealProducerLotAndOneDemandTile()
    {
        var host = Start();
        var sample = host.Snapshot.Workers.Workers[0];
        var tile = sample.TacticalTileId!.Value;
        var census = new PhysicalWorkerRuntimeState(
            Enumerable.Range(1, 10_000).Select(i =>
                new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId((ulong)i),
                    Red, sample.CellId, ItemOne, 1_000_000UL,
                    PhysicalWorkerActivity.Producing, 1_000_000U,
                    tile, PhysicalWorkerOccupation.Agriculture)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = Policy(ItemOne);
        var first = FoundationPhysicalCommodityDemandProjector.Project(census, demand);
        var lot = Assert.Single(first.SupplyLots);
        Assert.Equal(10_000_000_000UL, lot.CapacityMicroPerTurn);
        var plan = new[]
        {
            new FoundationPhysicalCommodityDeliveryCandidate(
                1UL, Red, lot.Occupation, tile, Red, tile, ItemOne,
                lot.CapacityMicroPerTurn, new[] { tile })
        };
        var result = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, host.Incidence, demand, FreightPolicy(ItemOne),
            plan, Array.Empty<FoundationTransportEdgeOwnershipEvidence>());
        Assert.Equal(10_000, result.MarketInputs.DistinctWorkers);
        Assert.Equal(1, result.DeliveryCount);
        Assert.Equal(10_000_000_000UL, result.TotalAllocatedGoodsMicro);
        Assert.Empty(result.Freight.UsedEdges);
    }
}
