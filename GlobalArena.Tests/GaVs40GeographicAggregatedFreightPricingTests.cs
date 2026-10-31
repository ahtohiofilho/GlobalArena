using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs40GeographicAggregatedFreightPricingTests
{
    private const ulong Technology = 101UL;
    private const ulong Catalog = 7UL;
    private const ulong MoneyRuleset = 40UL;
    private static readonly BigInteger Scale = new(100_000_000L);
    private static readonly BigInteger Smoothing = new(1_000_000L);

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static ExperimentalFreightTechnologyPricing Rates(
        ulong technology = Technology, ulong catalog = Catalog,
        ExperimentalLandInfrastructureKind kind =
            ExperimentalLandInfrastructureKind.StoneRoad,
        BigInteger? distance = null, BigInteger? uphill = null,
        BigInteger? wet = null, BigInteger? overhead = null,
        BigInteger? saturation = null) =>
        new(technology, catalog, MoneyRuleset, kind,
            distance ?? 1, uphill ?? 2, wet ?? 1,
            overhead ?? Smoothing, Smoothing,
            saturation ?? 1, Scale);

    private static (PhysicalTacticalTileId First,
        PhysicalTacticalTileId Middle, PhysicalTacticalTileId Last)
        LandPair(CanonicalPhysicalTacticalSession game)
    {
        var first = game.InitialTile;
        var adjacent = game.AdjacentWorkable(first).ToHashSet();
        var last = adjacent.SelectMany(game.AdjacentWorkable)
            .Where(x => x != first && !adjacent.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
        var middle = game.AdjacentWorkable(last)
            .Where(adjacent.Contains)
            .OrderBy(x => x.FineStrategicCellId.Value).First();
        return (first, middle, last);
    }

    private static EconomicTransportBuiltAsset Asset(
        CanonicalPhysicalTacticalSession game,
        PhysicalTacticalTileId from, PhysicalTacticalTileId to,
        ulong id, ulong technology = Technology, ulong catalog = Catalog,
        bool active = true)
    {
        var edge = game.FineWorld.StrategicTopology.Edges.First(x =>
            x.IncidentCellIds.Contains(from.FineStrategicCellId) &&
            x.IncidentCellIds.Contains(to.FineStrategicCellId));
        return new EconomicTransportBuiltAsset(
            id, technology,
            new PhysicalTacticalEdgeId(
                game.InitialTile.FineGoldbergParameters, edge.Id),
            catalog, active);
    }

    private sealed class Fixture
    {
        public CanonicalPhysicalTacticalSession Game { get; }
        public CanonicalLandEdgeFactsSnapshot Facts { get; }
        public ExperimentalLandInfrastructureSnapshot Network { get; }
        public ExperimentalGeographicFreightItineraryPricing Pricing { get; }
        public PhysicalTacticalTileId First { get; }
        public PhysicalTacticalTileId Middle { get; }
        public PhysicalTacticalTileId Last { get; }
        public EconomicTransportBuiltAsset[] Assets { get; }
        public IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> Anchors { get; }
        public StrategicEconomicRouteKey RouteKey { get; }

        public Fixture(
            ExperimentalFreightTechnologyPricing? rates = null,
            bool buildRoads = true)
        {
            Game = Start();
            Facts = new CanonicalLandEdgeFactsSnapshot(Game);
            (First, Middle, Last) = LandPair(Game);
            Assets = new[] {
                Asset(Game, First, Middle, 31UL),
                Asset(Game, Middle, Last, 32UL)
            };
            Network = new ExperimentalLandInfrastructureSnapshot(
                Game, Facts, buildRoads ? Assets : Array.Empty<EconomicTransportBuiltAsset>(),
                new Dictionary<(ulong, ulong), ExperimentalLandInfrastructureKind>
                {
                    [(Technology, Catalog)] = ExperimentalLandInfrastructureKind.StoneRoad
                },
                StrategicEdgeAccessSnapshot.AllOpen, networkRevision: 1UL);
            Anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
            {
                [new EconomicPointId(1UL)] = First,
                [new EconomicPointId(2UL)] = Last
            };
            RouteKey = new StrategicEconomicRouteKey(
                new EconomicPointId(1UL), new EconomicPointId(2UL));
            Pricing = new ExperimentalGeographicFreightItineraryPricing(
                Game, Facts, Network, Anchors, new[] { rates ?? Rates() });
        }

        public EconomicFineTransportServiceIntent Intent(
            BigInteger? forecast = null) =>
            new(18UL, RouteKey, Technology, Catalog,
                forecast ?? 10_000_000L);
    }

    [Fact]
    public void MonetaryBaseUsesTheExistingPhysicalFactsNotWalkingPointQuotes()
    {
        var f = new Fixture();
        var cost = Assert.IsType<ExperimentalAggregatedLandFreightCostFunction>(
            f.Pricing.CreateForItinerary(f.Intent(), f.Assets));
        var first = f.Facts.Find(f.First, f.Middle)!;
        var second = f.Facts.Find(f.Middle, f.Last)!;
        var expectedDistance = (BigInteger)first.UnitSphereChordMicroUnits +
            second.UnitSphereChordMicroUnits;
        var expectedAscent = BigInteger.Max(0, first.SignedElevationDeltaRaw) +
            BigInteger.Max(0, second.SignedElevationDeltaRaw);
        var expectedWet = (BigInteger)first.AverageMoistureRaw *
            first.UnitSphereChordMicroUnits / 1_000_000 +
            (BigInteger)second.AverageMoistureRaw *
            second.UnitSphereChordMicroUnits / 1_000_000;
        Assert.Equal(expectedDistance + 2 * expectedAscent + expectedWet,
            cost.GeographicBaseNumerator);
        Assert.Equal(MoneyRuleset, cost.RulesetVersion);
        Assert.True(cost.GeographicBaseNumerator > 0);
        Assert.Equal(2, f.Assets.Length);
    }

    [Fact]
    public void SmoothLoadPricingCombinesEarlyScaleEconomiesAndGrowingSaturation()
    {
        var f = new Fixture();
        var function = f.Pricing.CreateForItinerary(f.Intent(), f.Assets);
        var zero = function.EvaluateRawUnitCostPerGram(BigInteger.Zero);
        var moderate = function.EvaluateRawUnitCostPerGram(1_000_000L);
        var massive = function.EvaluateRawUnitCostPerGram(1_000_000_000L);
        Assert.True(moderate.CompareTo(zero) < 0);
        Assert.True(massive.CompareTo(moderate) > 0);
        Assert.True(massive.Numerator.Sign > 0);
        var nearby = function.EvaluateRawUnitCostPerGram(1_000_001L);
        Assert.True(nearby.CompareTo(moderate) != 0);
        Assert.True(nearby.CompareTo(massive) < 0);
    }

    [Fact]
    public void EconomicTurnQuoteIsFrozenAtExpectedLoadAndRepricesOnNextTurn()
    {
        var f = new Fixture();
        var function = f.Pricing.CreateForItinerary(f.Intent(), f.Assets);
        var early = EconomicTurnFreightQuote.FromForecast(
            1UL, f.RouteKey, 1_000_000L, function);
        var before = early.RawUnitCostPerGram;
        var charge = early.ChargeForRawQuantity(
            EconomicTransferPolicy.FixedPointDenominator, 1_000UL);
        Assert.Equal(0, charge.CompareTo(new FoodTransportCostFraction(
            before.Numerator * 1_000UL, before.Denominator)));
        var later = EconomicTurnFreightQuote.FromForecast(
            2UL, f.RouteKey, 1_000_000_000L, function);
        Assert.True(later.RawUnitCostPerGram.CompareTo(before) > 0);
        Assert.Same(before, early.RawUnitCostPerGram);
        Assert.Equal(1UL, early.TurnOrdinal);
        Assert.Equal(2UL, later.TurnOrdinal);
    }

    [Fact]
    public void FinancialCostsNeverInferFreeRoadOrCrossings()
    {
        var notBuilt = new Fixture(buildRoads: false);
        Assert.Throws<InvalidOperationException>(() =>
            notBuilt.Pricing.CreateForItinerary(notBuilt.Intent(), notBuilt.Assets));

        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            f.Pricing.CreateForItinerary(f.Intent(),
                new[] { f.Assets[1], f.Assets[0] }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Pricing.CreateForItinerary(f.Intent(),
                new[] { f.Assets[0] }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Pricing.CreateForItinerary(f.Intent(),
                new[] { f.Assets[0], f.Assets[0] }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Pricing.CreateForItinerary(f.Intent(),
                new[] { Asset(f.Game, f.First, f.Middle, 33UL, catalog: 999UL) }));
    }

    [Fact]
    public void RejectsUnspecifiedPricesZeroSmoothingAndNegativeForecasts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExperimentalFreightTechnologyPricing(
                Technology, Catalog, MoneyRuleset,
                ExperimentalLandInfrastructureKind.StoneRoad,
                1, 1, 1, 1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExperimentalFreightTechnologyPricing(
                Technology, Catalog, MoneyRuleset,
                ExperimentalLandInfrastructureKind.StoneRoad,
                -1, 1, 1, 1, 1, 1, 1));
        var f = new Fixture();
        var fn = f.Pricing.CreateForItinerary(f.Intent(), f.Assets);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fn.EvaluateRawUnitCostPerGram(-1));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalGeographicFreightItineraryPricing(
                f.Game, f.Facts, f.Network, f.Anchors,
                new[] { Rates(), Rates() }));
        var noPolicy = new ExperimentalGeographicFreightItineraryPricing(
            f.Game, f.Facts, f.Network, f.Anchors,
            Array.Empty<ExperimentalFreightTechnologyPricing>());
        Assert.Throws<InvalidOperationException>(() =>
            noPolicy.CreateForItinerary(f.Intent(), f.Assets));
    }

    [Fact]
    public void LargeLoadsRemainExactWithNoCapacityOrSaturationCliff()
    {
        var f = new Fixture();
        var fn = f.Pricing.CreateForItinerary(f.Intent(), f.Assets);
        var huge = BigInteger.Pow(10, 42);
        var quote = fn.EvaluateRawUnitCostPerGram(huge);
        Assert.True(quote.Numerator > quote.Denominator);
        Assert.True(fn.EvaluateRawUnitCostPerGram(huge + 1)
            .CompareTo(quote) > 0);
        Assert.True(quote.Denominator.Sign > 0);
    }

    [Fact]
    public void PhysicalDirectionAffectsAscentWithoutModifyingUnderlyingWorld()
    {
        var f = new Fixture();
        var save = f.Game.ExportSaveJson();
        var hash = f.Game.CurrentHash;
        var forward = f.Pricing.CreateForItinerary(f.Intent(), f.Assets);
        var swapped = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [new EconomicPointId(1UL)] = f.Last,
            [new EconomicPointId(2UL)] = f.First
        };
        var reverse = new ExperimentalGeographicFreightItineraryPricing(
            f.Game, f.Facts, f.Network, swapped, new[] { Rates() })
            .CreateForItinerary(f.Intent(), f.Assets.Reverse().ToArray());
        var forwardTotal = Assert.IsType<ExperimentalAggregatedLandFreightCostFunction>(
            forward).GeographicBaseNumerator;
        var reverseTotal = Assert.IsType<ExperimentalAggregatedLandFreightCostFunction>(
            reverse).GeographicBaseNumerator;
        var delta = f.Facts.Find(f.First, f.Middle)!.SignedElevationDeltaRaw +
            f.Facts.Find(f.Middle, f.Last)!.SignedElevationDeltaRaw;
        Assert.Equal(2 * (BigInteger)delta, forwardTotal - reverseTotal);
        Assert.Equal(save, f.Game.ExportSaveJson());
        Assert.Equal(hash, f.Game.CurrentHash);
        Assert.Empty(f.Game.Events);
    }

    [Fact]
    public void ConnectsToExistingFineDiscoveryAndLocksOneOfferedFreightQuote()
    {
        var f = new Fixture();
        var saved = f.Game.ExportSaveJson();
        var hash = f.Game.CurrentHash;
        var geography = new StrategicSurfaceGraph(f.Game.FineWorld.StrategicTopology);
        var offers = new EconomicFineTransportServiceItineraryDiscovery().Discover(
            geography, f.Anchors, StrategicEdgeAccessSnapshot.AllOpen,
            f.Assets, new[] { f.Intent(1_000_000L) }, f.Pricing);
        var offer = Assert.Single(offers);
        Assert.Equal(new[] { 31UL, 32UL }, offer.AssetPath);
        Assert.Empty(offer.TransferAfterLeg);
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                new EconomicPointId(1UL), new CommodityId(7U),
                EconomicActivityKind.Agriculture, 3 * EconomicTransferPolicy.FixedPointDenominator) },
            new[] { new EconomicDemandFlow(
                new EconomicPointId(2UL), new CommodityId(7U),
                3 * EconomicTransferPolicy.FixedPointDenominator) });
        var selections = new EconomicTransportServiceQuoteResolver().Resolve(
            5UL, geography, f.Anchors, flows,
            StrategicEdgeAccessSnapshot.AllOpen, f.Assets, offers);
        var selected = Assert.Single(selections);
        Assert.Equal(18UL, selected.ServiceId);
        Assert.Equal(MoneyRuleset, selected.Quote.RulesetVersion);
        Assert.Equal(1_000_000L, selected.Quote.ExpectedLoadMicrograms);
        Assert.Equal(0, selected.Quote.RawUnitCostPerGram.CompareTo(
            offer.CostFunction.EvaluateRawUnitCostPerGram(1_000_000L)));
        Assert.Equal(saved, f.Game.ExportSaveJson());
        Assert.Equal(hash, f.Game.CurrentHash);
        Assert.Empty(f.Game.Events);
    }

    [Fact]
    public void IncompatibleInfrastructureCannotPriceRailAsRoadOrCreateTransfers()
    {
        var f = new Fixture();
        var railPolicy = new ExperimentalGeographicFreightItineraryPricing(
            f.Game, f.Facts, f.Network, f.Anchors,
            new[] { Rates(kind: ExperimentalLandInfrastructureKind.Railway) });
        Assert.Throws<InvalidOperationException>(() =>
            railPolicy.CreateForItinerary(f.Intent(), f.Assets));
        var none = new ExperimentalGeographicFreightItineraryPricing(
            f.Game, f.Facts, f.Network, f.Anchors,
            new[] { Rates(catalog: 8UL) });
        Assert.Throws<InvalidOperationException>(() =>
            none.CreateForItinerary(f.Intent(), f.Assets));
    }
}
