using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// A completed fine geography/access snapshot can be economically closed
/// without updating freight quotes in response to the resulting allocations.
/// Later geographic or forecast changes affect ONLY later economic cycles.
/// </summary>
public sealed class FineEconomicCycleSnapshotBoundaryTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    private sealed class Function(ulong itineraryAssetCount)
        : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var d = expectedLoadMicrograms + 1_000_000_000;
            return new FoodTransportCostFraction(
                ((BigInteger)20 + 8 * itineraryAssetCount) * d +
                120_000_000_000, d);
        }
    }

    private sealed class Prices : IEconomicFineTransportItineraryPricing
    {
        public List<Function> Functions { get; } = new();

        public IEconomicTurnFreightCostFunction CreateForItinerary(
            EconomicFineTransportServiceIntent intent,
            IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
        {
            Assert.All(orderedAssets, x =>
            {
                Assert.Equal(intent.TechnologyTypeId, x.TechnologyTypeId);
                Assert.Equal(intent.TechnologyCatalogVersion,
                    x.TechnologyCatalogVersion);
            });
            var f = new Function((ulong)orderedAssets.Count);
            Functions.Add(f);
            return f;
        }
    }

    private sealed class MarketValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(800_000 * (BigInteger)Unit - 10_000 * (BigInteger)q, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(1_600_000 * (BigInteger)q * Unit -
                10_000 * (BigInteger)q * q, 2 * (BigInteger)Unit * Unit);
    }

    [Fact]
    public void NewSnapshotRequotesOnlyAcrossCyclesAndPreservesOriginalMarketLedger()
    {
        var resolution = new GoldbergParameters(4, 0);
        var graph = new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(resolution));
        var edge = graph.StrategicTopology.Edges[0];
        var start = new EconomicPointId(1UL);
        var destination = new EconomicPointId(2UL);
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [start] = new PhysicalTacticalTileId(
                resolution, edge.IncidentCellIds[0]),
            [destination] = new PhysicalTacticalTileId(
                resolution, edge.IncidentCellIds[1])
        };
        var assets = new[]
        {
            new EconomicTransportBuiltAsset(
                11UL, 10UL, new PhysicalTacticalEdgeId(resolution, edge.Id), 1UL)
        };
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                start, new CommodityId(7U),
                EconomicActivityKind.Agriculture, 3 * Unit) },
            new[] { new EconomicDemandFlow(
                destination, new CommodityId(7U), 3 * Unit) });
        var catalog = new FoodCommodityCatalog(new[]
        {
            new FoodCommodityDefinition(
                new CommodityId(7U), "test-commodity", 1_000UL)
        });
        var curves = new[]
        {
            new EconomicTurnCommodityValueOffer(
                destination, new CommodityId(7U), new MarketValue())
        };
        var reservations = new[]
        {
            new EconomicTurnSourceReservation(
                start, new CommodityId(7U),
                EconomicActivityKind.Agriculture, 20_000UL)
        };
        var policy = new EconomicCongestionMarketSearchPolicy(Unit, 10);
        var route = new StrategicEconomicRouteKey(start, destination);
        var discoverer = new EconomicFineTransportServiceItineraryDiscovery();
        var quoter = new EconomicTransportServiceQuoteResolver();
        var market = new EconomicTurnLockedFreightMarketResolver();

        EconomicFineTransportServiceIntent Intent(
            ulong service, ulong expectedLoad) =>
            new(service, route, 10UL, 1UL, new BigInteger(expectedLoad));

        var firstPricing = new Prices();
        var firstOffers = discoverer.Discover(
            graph, anchors, StrategicEdgeAccessSnapshot.AllOpen,
            assets, new[] { Intent(100UL, 500_000_000UL) }, firstPricing);
        var firstSelected = Assert.Single(quoter.Resolve(
            1UL, graph, anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, assets, firstOffers));
        var state = WorldState.CreateInitial();
        var hash = new CanonicalWorldStateHasher().Compute(state);

        var exact = market.Resolve(1UL, flows, catalog,
            new[] { firstSelected.Quote }, curves, reservations, policy);
        var indexed = market.Resolve(1UL, flows, catalog,
            new[] { firstSelected.Quote }, curves, reservations, policy,
            selectionMode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex);

        Assert.Equal(3, exact.Count);
        Assert.Equal(exact.Count, indexed.Count);
        for (var i = 0; i < exact.Count; i++)
        {
            Assert.Equal(exact[i].RawQuantity, indexed[i].RawQuantity);
            Assert.Equal(0, exact[i].RawNetReturn.CompareTo(
                indexed[i].RawNetReturn));
            Assert.Same(firstSelected.Quote, exact[i].FreightQuote);
            Assert.Equal(0, exact[i].RawFreightCharge.CompareTo(
                firstSelected.Quote.ChargeForRawQuantity(
                    exact[i].RawQuantity, 1_000UL)));
        }
        Assert.True(exact[0].RawMarginalCommodityUnitValueAtDecision.CompareTo(
            exact[1].RawMarginalCommodityUnitValueAtDecision) > 0);
        Assert.Equal(1, Assert.Single(firstPricing.Functions).Evaluations);

        // A changed forecast on the NEXT coherent snapshot can change
        // the freight; earlier trades and charges must remain intact.
        var secondPricing = new Prices();
        var secondOffers = discoverer.Discover(
            graph, anchors, StrategicEdgeAccessSnapshot.AllOpen,
            assets, new[] { Intent(100UL, 8_000_000_000UL) }, secondPricing);
        var secondSelected = Assert.Single(quoter.Resolve(
            2UL, graph, anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, assets, secondOffers));
        Assert.True(secondSelected.Quote.RawUnitCostPerGram.CompareTo(
            firstSelected.Quote.RawUnitCostPerGram) < 0);
        Assert.Equal(1UL, firstSelected.Quote.TurnOrdinal);

        // A closed constructed corridor on the next snapshot yields no
        // service despite the same physical asset being listed.
        var closed = new StrategicEdgeAccessSnapshot(new[] { edge.Id });
        var closedPricing = new Prices();
        var closedOffers = discoverer.Discover(
            graph, anchors, closed, assets,
            new[] { Intent(100UL, 8_000_000_000UL) }, closedPricing);
        Assert.Empty(closedOffers);
        Assert.Empty(closedPricing.Functions);
        Assert.Empty(quoter.Resolve(
            3UL, graph, anchors, flows, closed, assets, closedOffers));

        Assert.All(exact, t => Assert.Same(firstSelected.Quote, t.FreightQuote));
        Assert.Equal(1, Assert.Single(firstPricing.Functions).Evaluations);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));
    }
}
