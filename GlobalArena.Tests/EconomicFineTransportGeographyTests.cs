using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Fine-grid executable reference. No authoritative cycle scheduler, asset
/// persistence, free geographic access or physical delivery is implied.
/// </summary>
public sealed class EconomicFineTransportGeographyTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Coarse = new(2, 0);
    private static readonly GoldbergParameters Fine = new(4, 0);

    private sealed class SmoothCost(ulong baseCost) : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var d = expectedLoadMicrograms + 100_000_000;
            return new FoodTransportCostFraction(
                (BigInteger)baseCost * d + 60_000_000_000, d);
        }
    }

    private sealed class CommodityValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(600_000 * (BigInteger)Unit - 10_000 * (BigInteger)q, Unit);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(1_200_000 * (BigInteger)q * Unit -
                10_000 * (BigInteger)q * q, 2 * (BigInteger)Unit * Unit);
    }

    private static StrategicEconomicRouteKey Route() => new(
        new EconomicPointId(1UL), new EconomicPointId(2UL));

    private static EconomicProductionDemandResult Flows() => new(
        new[] { new EconomicSupplyFlow(
            new EconomicPointId(1UL), new CommodityId(7U),
            EconomicActivityKind.Agriculture, 3 * Unit) },
        new[] { new EconomicDemandFlow(
            new EconomicPointId(2UL), new CommodityId(7U), 3 * Unit) });

    private static (
        StrategicSurfaceGraph Graph,
        StrategicEdge Edge,
        Dictionary<EconomicPointId, PhysicalTacticalTileId> Anchors)
        FineFixture()
    {
        var topology = GoldbergStrategicTopologyGenerator.Generate(Fine);
        var edge = topology.Edges[0];
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [new EconomicPointId(1UL)] =
                new PhysicalTacticalTileId(Fine, edge.IncidentCellIds[0]),
            [new EconomicPointId(2UL)] =
                new PhysicalTacticalTileId(Fine, edge.IncidentCellIds[1])
        };
        return (new StrategicSurfaceGraph(topology), edge, anchors);
    }

    private static IReadOnlyList<EconomicTransportServiceSelection> Select(
        ulong turn, StrategicSurfaceGraph graph,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors,
        IEnumerable<EconomicTransportBuiltAsset> assets,
        IEnumerable<EconomicTransportServiceOffer> offers,
        StrategicEdgeAccessSnapshot? access = null) =>
        new EconomicTransportServiceQuoteResolver().Resolve(
            turn, graph, anchors, Flows(),
            access ?? StrategicEdgeAccessSnapshot.AllOpen, assets, offers);

    [Fact]
    public void QualifiedFineCorridorIdentityPreventsCoarseFineAliasing()
    {
        var edge = new StrategicEdgeId(1UL);
        var fine = new PhysicalTacticalEdgeId(Fine, edge);
        var coarse = new PhysicalTacticalEdgeId(Coarse, edge);
        Assert.NotEqual(fine, coarse);
        Assert.Equal(Fine, fine.FineGoldbergParameters);
        Assert.Equal(edge, fine.FineStrategicEdgeId);
        Assert.False(default(PhysicalTacticalEdgeId).IsValid);
        Assert.Throws<InvalidOperationException>(() =>
            _ = default(PhysicalTacticalEdgeId).FineStrategicEdgeId);
        Assert.Throws<ArgumentException>(() =>
            new PhysicalTacticalEdgeId(default, edge));
        Assert.Throws<ArgumentException>(() =>
            new PhysicalTacticalEdgeId(Fine, default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PhysicalTacticalEdgeId(
                Fine, new StrategicEdgeId(Fine.StrategicEdgeCount + 1UL)));
    }

    [Fact]
    public void FineMeshServiceQuoteFeedsDynamicMarketWithoutStrategicRouting()
    {
        var (graph, edge, anchors) = FineFixture();
        var built = new[]
        {
            new EconomicTransportBuiltAsset(
                11UL, 10UL, new PhysicalTacticalEdgeId(Fine, edge.Id), 3UL),
            new EconomicTransportBuiltAsset(
                12UL, 20UL, new PhysicalTacticalEdgeId(Fine, edge.Id), 3UL)
        };
        var expensive = new SmoothCost(100UL);
        var cheap = new SmoothCost(40UL);
        var offers = new[]
        {
            new EconomicTransportServiceOffer(101UL, Route(),
                new[] { 11UL }, Array.Empty<int>(),
                new BigInteger(500_000_000), expensive),
            new EconomicTransportServiceOffer(102UL, Route(),
                new[] { 12UL }, Array.Empty<int>(),
                new BigInteger(500_000_000), cheap)
        };
        var world = WorldState.CreateInitial();
        var beforeHash = new CanonicalWorldStateHasher().Compute(world);

        var selection = Assert.Single(Select(
            1UL, graph, anchors, built, offers));
        Assert.Equal(102UL, selection.ServiceId);
        Assert.Equal(Fine, selection.FineTopologyParameters);
        Assert.Equal(new[] { 12UL }, selection.AssetIds);
        Assert.Equal(new[] { 20UL }, selection.TechnologyTypeIds);
        Assert.Equal(3UL, selection.TechnologyCatalogVersion);
        Assert.Equal(1, expensive.Evaluations);
        Assert.Equal(1, cheap.Evaluations);

        var trades = new EconomicTurnLockedFreightMarketResolver().Resolve(
            1UL, Flows(),
            new FoodCommodityCatalog(new[]
            {
                new FoodCommodityDefinition(
                    new CommodityId(7U), "test-grain", 1_000UL)
            }),
            new[] { selection.Quote },
            new[]
            {
                new EconomicTurnCommodityValueOffer(
                    new EconomicPointId(2UL), new CommodityId(7U),
                    new CommodityValue())
            },
            new[]
            {
                new EconomicTurnSourceReservation(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 20_000UL)
            },
            new EconomicCongestionMarketSearchPolicy(Unit, 10),
            selectionMode:
                EconomicTurnFreightCandidateSelectionMode.DependencyIndex);

        Assert.Equal(3, trades.Count);
        Assert.All(trades, trade =>
        {
            Assert.Same(selection.Quote, trade.FreightQuote);
            Assert.Equal(0, trade.RawFreightCharge.CompareTo(
                selection.Quote.ChargeForRawQuantity(trade.RawQuantity, 1_000UL)));
        });
        Assert.True(trades[0].RawMarginalCommodityUnitValueAtDecision
            .CompareTo(trades[1].RawMarginalCommodityUnitValueAtDecision) > 0);
        Assert.Equal(1, expensive.Evaluations);
        Assert.Equal(1, cheap.Evaluations);
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(world));

        var next = Assert.Single(Select(2UL, graph, anchors, built,
            new[]
            {
                new EconomicTransportServiceOffer(101UL, Route(),
                    new[] { 11UL }, Array.Empty<int>(),
                    new BigInteger(8_000_000_000), expensive),
                new EconomicTransportServiceOffer(102UL, Route(),
                    new[] { 12UL }, Array.Empty<int>(),
                    new BigInteger(8_000_000_000), cheap)
            }));
        Assert.True(next.Quote.RawUnitCostPerGram.CompareTo(
            selection.Quote.RawUnitCostPerGram) < 0);
        Assert.Equal(1UL, selection.Quote.TurnOrdinal);
    }

    [Fact]
    public void FineServiceRejectsWrongResolutionAndUnqualifiedCoarseAssets()
    {
        var (graph, edge, anchors) = FineFixture();
        var p = new SmoothCost(80UL);
        var offer = new EconomicTransportServiceOffer(
            1UL, Route(), new[] { 1UL }, Array.Empty<int>(),
            BigInteger.Zero, p);
        var good = new[] { new EconomicTransportBuiltAsset(
            1UL, 10UL, new PhysicalTacticalEdgeId(Fine, edge.Id), 1UL) };
        Assert.Single(Select(1UL, graph, anchors, good, new[] { offer }));
        Assert.Throws<ArgumentException>(() => Select(
            1UL, graph, anchors,
            new[] { new EconomicTransportBuiltAsset(
                1UL, 10UL, edge.Id, 1UL) },
            new[] { offer }));
        Assert.Throws<ArgumentException>(() => Select(
            1UL, graph, anchors,
            new[] { new EconomicTransportBuiltAsset(
                1UL, 10UL,
                new PhysicalTacticalEdgeId(Coarse, edge.Id), 1UL) },
            new[] { offer }));

        var wrong = new Dictionary<EconomicPointId, PhysicalTacticalTileId>(anchors)
        {
            [new EconomicPointId(1UL)] =
                new PhysicalTacticalTileId(
                    Coarse, new StrategicCellId(1UL))
        };
        Assert.Throws<ArgumentException>(() =>
            Select(1UL, graph, wrong, good, new[] { offer }));

        var closed = new StrategicEdgeAccessSnapshot(new[] { edge.Id });
        Assert.Empty(Select(1UL, graph, anchors, good, new[] { offer }, closed));
    }

    [Fact]
    public void HistoricalStrategicServicePathRejectsFineAsset()
    {
        var (graph, edge, anchors) = FineFixture();
        var first = edge.IncidentCellIds[0];
        var last = edge.IncidentCellIds[1];
        var legacyEconomy = new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(
                new EconomicPointId(1UL), new CivilizationId(1UL), first, 0UL),
            new EconomicPointRuntimeState(
                new EconomicPointId(2UL), new CivilizationId(1UL), last, 0UL)
        });
        var fineAsset = new EconomicTransportBuiltAsset(
            1UL, 10UL, new PhysicalTacticalEdgeId(Fine, edge.Id), 1UL);
        var offer = new EconomicTransportServiceOffer(
            1UL, Route(), new[] { 1UL }, Array.Empty<int>(),
            BigInteger.Zero, new SmoothCost(40UL));
        Assert.Throws<ArgumentException>(() =>
            new EconomicTransportServiceQuoteResolver().Resolve(
                1UL, graph, legacyEconomy, Flows(),
                StrategicEdgeAccessSnapshot.AllOpen,
                new[] { fineAsset }, new[] { offer }));
        Assert.Single(Select(1UL, graph, anchors,
            new[] { fineAsset }, new[] { offer }));
    }
}
