using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs41CostOptimalFreightItineraryTests
{
    private const ulong Technology = 101UL;
    private const ulong Catalog = 7UL;

    private static ExperimentalFreightTechnologyPricing Rate(
        ulong technology = Technology,
        BigInteger? distance = null,
        BigInteger? ascent = null) =>
        new(technology, Catalog, 41UL,
            ExperimentalLandInfrastructureKind.StoneRoad,
            distance ?? 1, ascent ?? 2, 1,
            1_000_000, 1_000_000, 1, 100_000_000);

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static EconomicTransportBuiltAsset Asset(
        CanonicalPhysicalTacticalSession game, PhysicalTacticalTileId from,
        PhysicalTacticalTileId to, ulong id, ulong type = Technology,
        bool active = true)
    {
        var edge = game.FineWorld.StrategicTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(from.FineStrategicCellId) &&
            e.IncidentCellIds.Contains(to.FineStrategicCellId));
        return new EconomicTransportBuiltAsset(
            id, type, new PhysicalTacticalEdgeId(
                game.InitialTile.FineGoldbergParameters, edge.Id),
            Catalog, active);
    }

    private sealed class Fixture
    {
        public CanonicalPhysicalTacticalSession Game { get; } = Start();
        public CanonicalLandEdgeFactsSnapshot Facts { get; }
        public ExperimentalLandInfrastructureSnapshot Network { get; }
        public PhysicalTacticalTileId First { get; }
        public PhysicalTacticalTileId Middle { get; }
        public PhysicalTacticalTileId Last { get; }
        public EconomicTransportBuiltAsset[] Assets { get; }
        public Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors { get; }

        public Fixture(bool secondary = false)
        {
            Facts = new CanonicalLandEdgeFactsSnapshot(Game);
            First = Game.InitialTile;
            var neighbors = Game.AdjacentWorkable(First).ToHashSet();
            Last = neighbors.SelectMany(Game.AdjacentWorkable)
                .Where(x => x != First && !neighbors.Contains(x))
                .OrderBy(x => x.FineStrategicCellId.Value).First();
            Middle = Game.AdjacentWorkable(Last).Where(neighbors.Contains)
                .OrderBy(x => x.FineStrategicCellId.Value).First();
            var installed = new List<EconomicTransportBuiltAsset>
            {
                Asset(Game, First, Middle, 1UL),
                Asset(Game, Middle, Last, 2UL)
            };
            if (secondary)
            {
                installed.Add(Asset(Game, First, Middle, 11UL, 102UL));
                installed.Add(Asset(Game, Middle, Last, 12UL, 102UL));
            }
            Assets = installed.ToArray();
            var map = new Dictionary<(ulong, ulong), ExperimentalLandInfrastructureKind>
            {
                [(Technology, Catalog)] = ExperimentalLandInfrastructureKind.StoneRoad,
                [(102UL, Catalog)] = ExperimentalLandInfrastructureKind.StoneRoad
            };
            Network = new ExperimentalLandInfrastructureSnapshot(
                Game, Facts, Assets, map, StrategicEdgeAccessSnapshot.AllOpen,
                networkRevision: 1UL);
            Anchors = new()
            {
                [new EconomicPointId(1UL)] = First,
                [new EconomicPointId(2UL)] = Last,
                [new EconomicPointId(3UL)] = Middle
            };
        }

        public EconomicFineTransportServiceIntent Intent(
            ulong id = 20UL, ulong endPoint = 2UL,
            ulong technology = Technology, BigInteger? forecast = null,
            bool available = true) =>
            new(id, new StrategicEconomicRouteKey(new EconomicPointId(1UL),
                new EconomicPointId(endPoint)), technology, Catalog,
                forecast ?? 5_000_000, available);

        public IReadOnlyList<EconomicTransportServiceOffer> Discover(
            IEnumerable<EconomicFineTransportServiceIntent> intents,
            ExperimentalFreightRouteSearchDiagnostics? diagnostics = null,
            IEnumerable<EconomicTransportBuiltAsset>? assets = null,
            IEnumerable<ExperimentalFreightTechnologyPricing>? policies = null,
            StrategicEdgeAccessSnapshot? access = null) =>
            new ExperimentalCostOptimalFreightItineraryDiscovery().Discover(
                Game, Facts, Network, Anchors,
                access ?? StrategicEdgeAccessSnapshot.AllOpen,
                assets ?? Assets, intents,
                policies ?? new[] { Rate() }, diagnostics);
    }

    [Fact]
    public void PrefersThreeInexpensiveLegsOverTwoExpensiveLegs()
    {
        var arcs = new[]
        {
            new ExperimentalFreightWeightedArc(1, 2, 10, 100),
            new ExperimentalFreightWeightedArc(2, 5, 11, 100),
            new ExperimentalFreightWeightedArc(1, 3, 20, 10),
            new ExperimentalFreightWeightedArc(3, 4, 21, 10),
            new ExperimentalFreightWeightedArc(4, 5, 22, 10)
        };
        var paths = ExperimentalFreightWeightedPathfinder.FromSource(1, arcs);
        var chosen = paths[5];
        Assert.Equal(new BigInteger(30), chosen.GeographicWeight);
        Assert.Equal(new ulong[] { 20, 21, 22 }, chosen.AssetIds);
        Assert.Equal(new ulong[] { 1, 3, 4, 5 }, chosen.CellIds);
        Assert.Equal(new ulong[] { 10, 11 }, new ulong[] { 10, 11 });
    }

    [Fact]
    public void EqualPriceUsesFewerLegsThenLexicographicAssetIds()
    {
        var arcs = new[]
        {
            new ExperimentalFreightWeightedArc(1, 2, 9, 10),
            new ExperimentalFreightWeightedArc(2, 5, 8, 10),
            new ExperimentalFreightWeightedArc(1, 3, 2, 10),
            new ExperimentalFreightWeightedArc(3, 5, 11, 10),
            new ExperimentalFreightWeightedArc(1, 4, 1, 5),
            new ExperimentalFreightWeightedArc(4, 6, 3, 5),
            new ExperimentalFreightWeightedArc(6, 5, 4, 10)
        };
        var original = ExperimentalFreightWeightedPathfinder.FromSource(1, arcs);
        var shuffled = ExperimentalFreightWeightedPathfinder.FromSource(1, arcs.Reverse());
        Assert.Equal(new ulong[] { 2, 11 }, original[5].AssetIds);
        Assert.Equal(original[5].AssetIds.ToArray(), shuffled[5].AssetIds.ToArray());
        Assert.Equal(new BigInteger(20), original[5].GeographicWeight);
    }

    [Fact]
    public void ZeroWeightCyclesAndHugeArbitraryPrecisionCostsAreFinite()
    {
        var huge = BigInteger.Pow(10, 50);
        var arcs = new[]
        {
            new ExperimentalFreightWeightedArc(1, 2, 1, BigInteger.Zero),
            new ExperimentalFreightWeightedArc(2, 1, 1, BigInteger.Zero),
            new ExperimentalFreightWeightedArc(2, 3, 2, huge),
            new ExperimentalFreightWeightedArc(1, 3, 3, huge)
        };
        var tree = ExperimentalFreightWeightedPathfinder.FromSource(1, arcs);
        Assert.Equal(new ulong[] { 3 }, tree[3].AssetIds);
        Assert.Equal(huge, tree[3].GeographicWeight);
        Assert.Single(tree[1].CellIds);
        Assert.Throws<ArgumentException>(() =>
            ExperimentalFreightWeightedPathfinder.FromSource(1,
                new[] { new ExperimentalFreightWeightedArc(0, 2, 1, 1) }));
        Assert.Throws<ArgumentException>(() =>
            ExperimentalFreightWeightedPathfinder.FromSource(1,
                new[] { new ExperimentalFreightWeightedArc(1, 2, 1, -1) }));
    }

    [Fact]
    public void OneSourceTreeServesMultipleDestinationsAndLoadForecasts()
    {
        var f = new Fixture();
        var before = f.Game.ExportSaveJson();
        var hash = f.Game.CurrentHash;
        var intents = new[]
        {
            f.Intent(20, endPoint: 3, forecast: 1_000_000),
            f.Intent(21, endPoint: 2, forecast: 5_000_000),
            f.Intent(22, endPoint: 2, forecast: 100_000_000)
        };
        var diagnostics = new ExperimentalFreightRouteSearchDiagnostics();
        var offers = f.Discover(intents, diagnostics);
        Assert.Equal(3, offers.Count);
        Assert.Equal(1, diagnostics.SourceTreeBuilds);
        Assert.Equal(3, diagnostics.OfferedServices);
        Assert.Equal(0, diagnostics.UnreachableServices);
        Assert.True(diagnostics.ExaminedArcs > 0);
        Assert.Equal(new ulong[] { 1 }, offers[0].AssetPath);
        Assert.Equal(new ulong[] { 1, 2 }, offers[1].AssetPath);
        Assert.Equal(offers[1].AssetPath, offers[2].AssetPath);
        Assert.NotSame(offers[1].CostFunction, offers[2].CostFunction);
        var quoteA = EconomicTurnFreightQuote.FromForecast(
            1, offers[1].RouteKey, offers[1].ExpectedLoadMicrograms,
            offers[1].CostFunction);
        var quoteB = EconomicTurnFreightQuote.FromForecast(
            2, offers[2].RouteKey, offers[2].ExpectedLoadMicrograms,
            offers[2].CostFunction);
        Assert.NotEqual(0,
            quoteA.RawUnitCostPerGram.CompareTo(quoteB.RawUnitCostPerGram));
        Assert.Equal(before, f.Game.ExportSaveJson());
        Assert.Equal(hash, f.Game.CurrentHash);
        Assert.Empty(f.Game.Events);
    }

    [Fact]
    public void CommercialMarketPicksCheaperOfTwoExplicitTechnologies()
    {
        var f = new Fixture(secondary: true);
        var offers = f.Discover(
            new[] { f.Intent(20), f.Intent(21, technology: 102) },
            policies: new[] { Rate(distance: 5), Rate(102, distance: 1) });
        Assert.Equal(2, offers.Count);
        Assert.Equal(new ulong[] { 1, 2 }, offers[0].AssetPath);
        Assert.Equal(new ulong[] { 11, 12 }, offers[1].AssetPath);
        var graph = new StrategicSurfaceGraph(f.Game.FineWorld.StrategicTopology);
        var unit = EconomicTransferPolicy.FixedPointDenominator;
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                new EconomicPointId(1), new CommodityId(7),
                EconomicActivityKind.Agriculture, 3 * unit) },
            new[] { new EconomicDemandFlow(
                new EconomicPointId(2), new CommodityId(7), 3 * unit) });
        var selected = Assert.Single(new EconomicTransportServiceQuoteResolver()
            .Resolve(1, graph, f.Anchors, flows,
                StrategicEdgeAccessSnapshot.AllOpen, f.Assets, offers));
        Assert.Equal(21UL, selected.ServiceId);
        Assert.Equal(new ulong[] { 11, 12 }, selected.AssetIds);
    }

    [Fact]
    public void ClosedInfrastructureUnavailableServicesAndUnknownVersionsNeverMakeFreeRoutes()
    {
        var f = new Fixture();
        var initial = Assert.Single(f.Discover(new[] { f.Intent() }));
        var edge = f.Assets[0].GeographicEdgeId;
        var closed = f.Discover(new[] { f.Intent() },
            access: new StrategicEdgeAccessSnapshot(new[] { edge }));
        Assert.Empty(closed);
        Assert.Equal(new ulong[] { 1, 2 }, initial.AssetPath);
        Assert.Empty(f.Discover(new[] { f.Intent(20, available: false) }));
        Assert.Empty(f.Discover(new[] { f.Intent() },
            assets: new[] { f.Assets[0] }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Discover(new[] { f.Intent(20, technology: 999) }));
        Assert.Throws<ArgumentException>(() =>
            f.Discover(new[] { f.Intent(20), f.Intent(20) }));
    }

    [Fact]
    public void CostOptimalPriceMatchesIndependentWholeItineraryPricing()
    {
        var f = new Fixture();
        var offer = Assert.Single(f.Discover(new[] { f.Intent() }));
        var original = new ExperimentalGeographicFreightItineraryPricing(
            f.Game, f.Facts, f.Network, f.Anchors, new[] { Rate() })
            .CreateForItinerary(f.Intent(), f.Assets);
        var exact = Assert.IsType<ExperimentalAggregatedLandFreightCostFunction>(
            offer.CostFunction);
        var independent = Assert.IsType<ExperimentalAggregatedLandFreightCostFunction>(
            original);
        Assert.Equal(independent.GeographicBaseNumerator,
            exact.GeographicBaseNumerator);
        Assert.Equal(0, exact.EvaluateRawUnitCostPerGram(1_000_000)
            .CompareTo(independent.EvaluateRawUnitCostPerGram(1_000_000)));
    }
}
