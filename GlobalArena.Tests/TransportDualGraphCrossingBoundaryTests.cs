using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Goldberg geometric borders key DUAL cell-to-cell crossings, rather than
/// route segments running along the geometric vertices of those borders.
/// Geographic passage is not permission for intermediate cargo operations.
/// </summary>
public sealed class TransportDualGraphCrossingBoundaryTests
{
    private sealed class WholeCrossingPrice : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            return new FoodTransportCostFraction(
                expectedLoadMicrograms + 100_000_000,
                expectedLoadMicrograms + 50_000_000);
        }
    }

    private sealed class WholeItineraryPricing : IEconomicFineTransportItineraryPricing
    {
        public List<WholeCrossingPrice> Functions { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
        {
            Assert.Equal(new[] { 11UL, 12UL },
                orderedAssets.Select(asset => asset.AssetId));
            Assert.All(orderedAssets, asset =>
            {
                Assert.Equal(intent.TechnologyTypeId, asset.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion,
                    asset.TechnologyCatalogVersion);
            });
            var function = new WholeCrossingPrice();
            Functions.Add(function);
            return function;
        }
    }

    [Fact]
    public void TwoBuiltBorderCrossingsAdvanceAcrossThreeTilesWithoutIntermediateCargoStop()
    {
        var fine = new GoldbergParameters(4, 0);
        var topology = GoldbergStrategicTopologyGenerator.Generate(fine);
        var graph = new StrategicSurfaceGraph(topology);

        // The GEOMETRIC first edge has two VERTEX endpoints, but the
        // traversal endpoints are its two INCIDENT CELLS, not those vertices.
        var firstBorder = topology.Edges[0];
        var a = firstBorder.IncidentCellIds[0];
        var b = firstBorder.IncidentCellIds[1];
        var secondBorder = topology.Edges.First(edge =>
            edge.Id != firstBorder.Id &&
            edge.IncidentCellIds.Contains(b) &&
            !edge.IncidentCellIds.Contains(a));
        var c = secondBorder.IncidentCellIds.First(cell => cell != b);

        Assert.Equal(2, firstBorder.IncidentCellIds.Count);
        Assert.Equal(2, firstBorder.IncidentVertexIds.Count);
        Assert.Equal(graph.NodeCount, topology.Cells.Count);
        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);

        var crossingAssets = new[]
        {
            new EconomicTransportBuiltAsset(
                11UL, 10UL,
                new PhysicalTacticalEdgeId(fine, firstBorder.Id), 1UL),
            new EconomicTransportBuiltAsset(
                12UL, 10UL,
                new PhysicalTacticalEdgeId(fine, secondBorder.Id), 1UL)
        };

        // Point 3 has REAL commodity demand at the intermediate tile B,
        // but no service has been offered with point 3 as a destination.
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [new EconomicPointId(1UL)] = new(fine, a),
            [new EconomicPointId(2UL)] = new(fine, c),
            [new EconomicPointId(3UL)] = new(fine, b)
        };
        var source = new EconomicPointId(1UL);
        var destination = new EconomicPointId(2UL);
        var intermediate = new EconomicPointId(3UL);
        var commodity = new CommodityId(7U);
        var flows = new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(source, commodity,
                    EconomicActivityKind.Agriculture, 3UL)
            },
            new[]
            {
                new EconomicDemandFlow(destination, commodity, 3UL),
                new EconomicDemandFlow(intermediate, commodity, 3UL)
            });
        var pricing = new WholeItineraryPricing();
        var intent = new EconomicFineTransportServiceIntent(
            101UL, new StrategicEconomicRouteKey(source, destination),
            10UL, 1UL, new BigInteger(500_000_000));
        var discoverer = new EconomicFineTransportServiceItineraryDiscovery();
        var offers = discoverer.Discover(
            graph, anchors, StrategicEdgeAccessSnapshot.AllOpen,
            crossingAssets, new[] { intent }, pricing);

        var offer = Assert.Single(offers);
        Assert.Equal(new[] { 11UL, 12UL }, offer.AssetPath);
        Assert.Equal(0, Assert.Single(pricing.Functions).Evaluations);

        var selections = new EconomicTransportServiceQuoteResolver().Resolve(
            1UL, graph, anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, crossingAssets, offers);
        var chosen = Assert.Single(selections);
        Assert.Equal(destination,
            chosen.Quote.RouteKey.DestinationEconomicPointId);
        Assert.Equal(new[] { 11UL, 12UL }, chosen.AssetIds);
        Assert.Equal(1, Assert.Single(pricing.Functions).Evaluations);

        // Topological adjacency alone must not create an extra service.
        // Removing one built crossing makes the offered A->C service
        // unreachable even though A/B/C remain geographically adjacent.
        var missingSecondCrossing = discoverer.Discover(
            graph, anchors, StrategicEdgeAccessSnapshot.AllOpen,
            crossingAssets.Take(1), new[] { intent },
            new WholeItineraryPricing());
        Assert.Empty(missingSecondCrossing);
    }
}
